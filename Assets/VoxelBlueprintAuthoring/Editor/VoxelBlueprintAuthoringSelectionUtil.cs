using System.Collections.Generic;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    internal static class VoxelBlueprintAuthoringSelectionUtil
    {
        internal struct ClipboardEntry
        {
            public Vector3Int offset;
            public VoxelBlueprintAsset.Cell cell;
        }

        internal static List<ClipboardEntry> Clipboard;

        internal static Vector3 GridCenter(VoxelBlueprintAsset asset) =>
            new Vector3(asset.sizeX, asset.sizeY, asset.sizeZ) * 0.5f;

        internal static Vector3 CellWorld(VoxelBlueprintAsset asset, Vector3Int cell) =>
            new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f) - GridCenter(asset);

        internal static bool TryPickEnabledCell(
            VoxelBlueprintAsset asset, Camera camera, Rect rect, Vector2 guiPos, out Vector3Int cell)
        {
            cell = default;
            if (asset == null || camera == null || rect.width < 2f || rect.height < 2f)
                return false;
            Ray ray = VoxelBlueprintPreview3D.GuiRay(camera, rect, guiPos);
            Vector3 gridCenter = GridCenter(asset);
            Vector3 o = ray.origin + gridCenter;
            Vector3 d = ray.direction;
            if (d.sqrMagnitude < 1e-12f)
                return false;
            d.Normalize();
            Vector3Int size = new Vector3Int(asset.sizeX, asset.sizeY, asset.sizeZ);
            float tmin = 0f;
            float tmax = 1e6f;
            if (!ClipRayAabb(o, d, size, ref tmin, ref tmax))
                return false;
            int x = Mathf.Clamp(Mathf.FloorToInt((o + d * (tmin + 1e-4f)).x), 0, size.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((o + d * (tmin + 1e-4f)).y), 0, size.y - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((o + d * (tmin + 1e-4f)).z), 0, size.z - 1);
            int stepX = d.x >= 0f ? 1 : -1;
            int stepY = d.y >= 0f ? 1 : -1;
            int stepZ = d.z >= 0f ? 1 : -1;
            float tDeltaX = Mathf.Abs(d.x) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.x);
            float tDeltaY = Mathf.Abs(d.y) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.y);
            float tDeltaZ = Mathf.Abs(d.z) < 1e-8f ? 1e30f : Mathf.Abs(1f / d.z);
            float tMaxX = NextVoxelT(x, stepX, o.x, d.x);
            float tMaxY = NextVoxelT(y, stepY, o.y, d.y);
            float tMaxZ = NextVoxelT(z, stepZ, o.z, d.z);
            int guard = size.x + size.y + size.z + 8;
            for (int i = 0; i < guard; i++)
            {
                if (Enabled(asset, x, y, z))
                {
                    cell = new Vector3Int(x, y, z);
                    return true;
                }
                if (tMaxX < tMaxY)
                {
                    if (tMaxX > tmax + 1e-4f)
                        return false;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY < tMaxZ)
                {
                    if (tMaxY > tmax + 1e-4f)
                        return false;
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    if (tMaxZ > tmax + 1e-4f)
                        return false;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            return false;
        }

        internal static void MarqueeSelect(
            VoxelBlueprintAsset asset,
            Camera camera,
            Rect rect,
            Rect guiSelect,
            HashSet<Vector3Int> selection,
            bool addToSelection)
        {
            if (asset == null || camera == null || selection == null)
                return;
            if (!addToSelection)
                selection.Clear();
            if (guiSelect.width < 2f && guiSelect.height < 2f)
                return;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!Enabled(asset, x, y, z))
                    continue;
                Vector3 world = CellWorld(asset, new Vector3Int(x, y, z));
                if (!VoxelBlueprintPreview3D.WorldToGui(camera, rect, world, out Vector2 gui))
                    continue;
                if (gui.x < guiSelect.xMin || gui.x > guiSelect.xMax || gui.y < guiSelect.yMin || gui.y > guiSelect.yMax)
                    continue;
                selection.Add(new Vector3Int(x, y, z));
            }
        }

        internal static bool TryGetSelectionMin(HashSet<Vector3Int> selection, out Vector3Int min)
        {
            min = default;
            if (selection == null || selection.Count == 0)
                return false;
            min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (Vector3Int cell in selection)
                min = Vector3Int.Min(min, cell);
            return true;
        }

        internal static void CopySelection(HashSet<Vector3Int> selection, VoxelBlueprintAsset asset)
        {
            if (selection == null || asset == null || selection.Count == 0)
                return;
            if (!TryGetSelectionMin(selection, out Vector3Int min))
                return;
            var list = new List<ClipboardEntry>(selection.Count);
            foreach (Vector3Int cell in selection)
            {
                if (!asset.TryGetCell(cell.x, cell.y, cell.z, out VoxelBlueprintAsset.Cell voxel) || !voxel.enabled)
                    continue;
                list.Add(new ClipboardEntry { offset = cell - min, cell = voxel });
            }
            Clipboard = list;
        }

        internal static void CutSelection(HashSet<Vector3Int> selection, VoxelBlueprintAsset asset)
        {
            CopySelection(selection, asset);
            EraseSelection(selection, asset);
        }

        internal static void EraseSelection(HashSet<Vector3Int> selection, VoxelBlueprintAsset asset)
        {
            if (selection == null || asset == null)
                return;
            foreach (Vector3Int cell in selection)
                asset.SetCell(cell.x, cell.y, cell.z, false, default, 0);
            selection.Clear();
        }

        internal static int PasteClipboard(VoxelBlueprintAsset asset, Vector3Int anchorMin, bool eraseSourceOverlap)
        {
            if (asset == null || Clipboard == null || Clipboard.Count == 0)
                return 0;
            int placed = 0;
            if (eraseSourceOverlap)
            {
                for (int i = 0; i < Clipboard.Count; i++)
                {
                    ClipboardEntry entry = Clipboard[i];
                    Vector3Int p = anchorMin + entry.offset;
                    if (InBounds(asset, p))
                        asset.SetCell(p.x, p.y, p.z, false, default, 0);
                }
            }
            for (int i = 0; i < Clipboard.Count; i++)
            {
                ClipboardEntry entry = Clipboard[i];
                Vector3Int p = anchorMin + entry.offset;
                if (!InBounds(asset, p))
                    continue;
                asset.SetCell(p.x, p.y, p.z, true, entry.cell.color, entry.cell.value);
                placed++;
            }
            return placed;
        }

        internal static bool CanMoveSelection(VoxelBlueprintAsset asset, HashSet<Vector3Int> selection, Vector3Int delta)
        {
            if (asset == null || selection == null || selection.Count == 0 || delta == Vector3Int.zero)
                return false;
            foreach (Vector3Int cell in selection)
            {
                Vector3Int target = cell + delta;
                if (!InBounds(asset, target))
                    return false;
                if (Enabled(asset, target.x, target.y, target.z) && !selection.Contains(target))
                    return false;
            }
            return true;
        }

        internal static bool TryMoveSelection(VoxelBlueprintAsset asset, HashSet<Vector3Int> selection, Vector3Int delta)
        {
            if (!CanMoveSelection(asset, selection, delta))
                return false;
            var cells = new List<(Vector3Int from, VoxelBlueprintAsset.Cell cell)>(selection.Count);
            foreach (Vector3Int cell in selection)
            {
                if (!asset.TryGetCell(cell.x, cell.y, cell.z, out VoxelBlueprintAsset.Cell voxel) || !voxel.enabled)
                    continue;
                cells.Add((cell, voxel));
            }
            foreach ((Vector3Int from, VoxelBlueprintAsset.Cell _) in cells)
                asset.SetCell(from.x, from.y, from.z, false, default, 0);
            selection.Clear();
            foreach ((Vector3Int from, VoxelBlueprintAsset.Cell voxel) in cells)
            {
                Vector3Int target = from + delta;
                asset.SetCell(target.x, target.y, target.z, true, voxel.color, voxel.value);
                selection.Add(target);
            }
            return true;
        }

        internal static int FillSelectionMesh(
            VoxelBlueprintAsset asset,
            HashSet<Vector3Int> selection,
            Mesh mesh,
            out Vector3 boundsCenter,
            out float boundsRadius)
        {
            if (asset == null || mesh == null || selection == null || selection.Count == 0)
            {
                boundsCenter = Vector3.zero;
                boundsRadius = 1f;
                if (mesh != null)
                    mesh.Clear();
                return 0;
            }
            return VoxelBlueprintMeshBuilder.Fill(
                asset,
                mesh,
                out boundsCenter,
                out boundsRadius,
                (x, y, z) => selection.Contains(new Vector3Int(x, y, z)));
        }

        static bool Enabled(VoxelBlueprintAsset asset, int x, int y, int z) =>
            asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled;

        static bool InBounds(VoxelBlueprintAsset asset, Vector3Int p) =>
            p.x >= 0 && p.y >= 0 && p.z >= 0 && p.x < asset.sizeX && p.y < asset.sizeY && p.z < asset.sizeZ;

        static float NextVoxelT(int cell, int step, float origin, float dir)
        {
            if (Mathf.Abs(dir) < 1e-8f)
                return 1e30f;
            float boundary = step > 0 ? cell + 1 : cell;
            return (boundary - origin) / dir;
        }

        static bool ClipRayAabb(Vector3 o, Vector3 d, Vector3Int size, ref float tmin, ref float tmax)
        {
            if (!ClipAxis(o.x, d.x, size.x, ref tmin, ref tmax))
                return false;
            if (!ClipAxis(o.y, d.y, size.y, ref tmin, ref tmax))
                return false;
            if (!ClipAxis(o.z, d.z, size.z, ref tmin, ref tmax))
                return false;
            return tmin <= tmax;
        }

        static bool ClipAxis(float origin, float dir, int size, ref float tmin, ref float tmax)
        {
            if (Mathf.Abs(dir) < 1e-8f)
                return origin >= 0f && origin <= size;
            float t1 = (0f - origin) / dir;
            float t2 = (size - origin) / dir;
            if (t1 > t2)
            {
                float tmp = t1;
                t1 = t2;
                t2 = tmp;
            }
            tmin = Mathf.Max(tmin, t1);
            tmax = Mathf.Min(tmax, t2);
            return tmin <= tmax;
        }
    }
}
