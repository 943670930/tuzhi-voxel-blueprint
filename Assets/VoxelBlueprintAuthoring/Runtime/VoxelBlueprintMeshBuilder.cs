using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring
{
    public static class VoxelBlueprintMeshBuilder
    {
        const float CellSize = 1f;
        static readonly Vector3 LightDir = new Vector3(0.45f, 1f, 0.38f).normalized;
        static readonly List<Vector3> Vertices = new List<Vector3>(32768);
        static readonly List<Vector3> Normals = new List<Vector3>(32768);
        static readonly List<Color32> Colors = new List<Color32>(32768);
        static readonly List<Vector2> UVs = new List<Vector2>(32768);
        static readonly List<int> Triangles = new List<int>(98304);

        public static int Fill(VoxelBlueprintAsset asset, Mesh mesh, out Vector3 boundsCenter, out float boundsRadius)
        {
            return Fill(asset, mesh, out boundsCenter, out boundsRadius, null);
        }

        public static int Fill(
            VoxelBlueprintAsset asset,
            Mesh mesh,
            out Vector3 boundsCenter,
            out float boundsRadius,
            System.Func<int, int, int, bool> includeCell)
        {
            boundsCenter = Vector3.zero;
            boundsRadius = 1f;
            if (asset == null || mesh == null)
                return 0;

            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            UVs.Clear();
            Triangles.Clear();

            Vector3 gridCenter = new Vector3(asset.sizeX, asset.sizeY, asset.sizeZ) * 0.5f;
            int drawn = 0;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!VoxelBlueprintInteriorUtil.TryGetMaterializedCell(asset, x, y, z, out VoxelBlueprintAsset.Cell cell))
                    continue;
                if (includeCell != null && !includeCell(x, y, z))
                    continue;
                Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - gridCenter;
                AppendExposedCube(asset, includeCell, x, y, z, center, cell.color);
                drawn++;
            }

            mesh.indexFormat = IndexFormat.UInt32;
            mesh.Clear(false);
            if (drawn == 0 || Vertices.Count == 0)
                return 0;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, UVs);
            mesh.SetTriangles(Triangles, 0, true);
            mesh.RecalculateBounds();
            boundsCenter = mesh.bounds.center;
            boundsRadius = Mathf.Max(1f, mesh.bounds.extents.magnitude + CellSize);
            return drawn;
        }

        static void AppendExposedCube(
            VoxelBlueprintAsset asset,
            System.Func<int, int, int, bool> includeCell,
            int x,
            int y,
            int z,
            Vector3 center,
            Color32 color)
        {
            float half = CellSize * 0.5f;
            if (!Occupied(asset, includeCell, x, y, z - 1)) AddFace(asset, x, y, z, center, half, color, 0, 0, -1);
            if (!Occupied(asset, includeCell, x, y, z + 1)) AddFace(asset, x, y, z, center, half, color, 0, 0, 1);
            if (!Occupied(asset, includeCell, x - 1, y, z)) AddFace(asset, x, y, z, center, half, color, -1, 0, 0);
            if (!Occupied(asset, includeCell, x + 1, y, z)) AddFace(asset, x, y, z, center, half, color, 1, 0, 0);
            if (!Occupied(asset, includeCell, x, y - 1, z)) AddFace(asset, x, y, z, center, half, color, 0, -1, 0);
            if (!Occupied(asset, includeCell, x, y + 1, z)) AddFace(asset, x, y, z, center, half, color, 0, 1, 0);
        }

        static void AddFace(VoxelBlueprintAsset asset, int x, int y, int z, Vector3 center, float half, Color32 color, int nx, int ny, int nz)
        {
            Vector3 n = new Vector3(nx, ny, nz);
            GetFaceCorners(nx, ny, nz, out int ox0, out int oy0, out int oz0, out int ox1, out int oy1, out int oz1, out int ox2, out int oy2, out int oz2, out int ox3, out int oy3, out int oz3);
            float face = 0.5f + 0.5f * Mathf.Clamp01(Vector3.Dot(n, LightDir));
            int first = Vertices.Count;
            AddVertex(asset, x, y, z, center, half, color, nx, ny, nz, ox0, oy0, oz0, face, new Vector2(0f, 0f), n);
            AddVertex(asset, x, y, z, center, half, color, nx, ny, nz, ox1, oy1, oz1, face, new Vector2(0f, 1f), n);
            AddVertex(asset, x, y, z, center, half, color, nx, ny, nz, ox2, oy2, oz2, face, new Vector2(1f, 1f), n);
            AddVertex(asset, x, y, z, center, half, color, nx, ny, nz, ox3, oy3, oz3, face, new Vector2(1f, 0f), n);
            Triangles.Add(first);
            Triangles.Add(first + 1);
            Triangles.Add(first + 2);
            Triangles.Add(first);
            Triangles.Add(first + 2);
            Triangles.Add(first + 3);
        }

        static void AddVertex(VoxelBlueprintAsset asset, int x, int y, int z, Vector3 center, float half, Color32 color, int nx, int ny, int nz, int ox, int oy, int oz, float face, Vector2 uv, Vector3 n)
        {
            Vertices.Add(center + new Vector3(ox * half, oy * half, oz * half));
            Normals.Add(n);
            UVs.Add(uv);
            float ao = 1f - CornerAo(asset, x, y, z, nx, ny, nz, ox, oy, oz) * 0.28f;
            float s = face * ao;
            Colors.Add(new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * s), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * s), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * s), 0, 255),
                color.a));
        }

        static int CornerAo(VoxelBlueprintAsset asset, int x, int y, int z, int nx, int ny, int nz, int ox, int oy, int oz)
        {
            bool side1;
            bool side2;
            bool corner;
            if (nx != 0)
            {
                side1 = Occupied(asset, x + nx, y + oy, z);
                side2 = Occupied(asset, x + nx, y, z + oz);
                corner = Occupied(asset, x + nx, y + oy, z + oz);
            }
            else if (ny != 0)
            {
                side1 = Occupied(asset, x + ox, y + ny, z);
                side2 = Occupied(asset, x, y + ny, z + oz);
                corner = Occupied(asset, x + ox, y + ny, z + oz);
            }
            else
            {
                side1 = Occupied(asset, x + ox, y, z + nz);
                side2 = Occupied(asset, x, y + oy, z + nz);
                corner = Occupied(asset, x + ox, y + oy, z + nz);
            }
            if (side1 && side2)
                return 3;
            return (side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0);
        }

        static void GetFaceCorners(int nx, int ny, int nz, out int ox0, out int oy0, out int oz0, out int ox1, out int oy1, out int oz1, out int ox2, out int oy2, out int oz2, out int ox3, out int oy3, out int oz3)
        {
            if (nz == -1)
            {
                ox0 = -1; oy0 = -1; oz0 = -1;
                ox1 = -1; oy1 = 1; oz1 = -1;
                ox2 = 1; oy2 = 1; oz2 = -1;
                ox3 = 1; oy3 = -1; oz3 = -1;
            }
            else if (nz == 1)
            {
                ox0 = -1; oy0 = -1; oz0 = 1;
                ox1 = 1; oy1 = -1; oz1 = 1;
                ox2 = 1; oy2 = 1; oz2 = 1;
                ox3 = -1; oy3 = 1; oz3 = 1;
            }
            else if (nx == -1)
            {
                ox0 = -1; oy0 = -1; oz0 = -1;
                ox1 = -1; oy1 = -1; oz1 = 1;
                ox2 = -1; oy2 = 1; oz2 = 1;
                ox3 = -1; oy3 = 1; oz3 = -1;
            }
            else if (nx == 1)
            {
                ox0 = 1; oy0 = -1; oz0 = -1;
                ox1 = 1; oy1 = 1; oz1 = -1;
                ox2 = 1; oy2 = 1; oz2 = 1;
                ox3 = 1; oy3 = -1; oz3 = 1;
            }
            else if (ny == -1)
            {
                ox0 = -1; oy0 = -1; oz0 = -1;
                ox1 = 1; oy1 = -1; oz1 = -1;
                ox2 = 1; oy2 = -1; oz2 = 1;
                ox3 = -1; oy3 = -1; oz3 = 1;
            }
            else
            {
                ox0 = -1; oy0 = 1; oz0 = -1;
                ox1 = -1; oy1 = 1; oz1 = 1;
                ox2 = 1; oy2 = 1; oz2 = 1;
                ox3 = 1; oy3 = 1; oz3 = -1;
            }
        }

        static bool Occupied(VoxelBlueprintAsset asset, int x, int y, int z)
        {
            return Occupied(asset, null, x, y, z);
        }

        static bool Occupied(VoxelBlueprintAsset asset, System.Func<int, int, int, bool> includeCell, int x, int y, int z)
        {
            if (!VoxelBlueprintInteriorUtil.TryGetMaterializedCell(asset, x, y, z, out VoxelBlueprintAsset.Cell cell))
                return false;
            return includeCell == null || includeCell(x, y, z);
        }
    }
}
