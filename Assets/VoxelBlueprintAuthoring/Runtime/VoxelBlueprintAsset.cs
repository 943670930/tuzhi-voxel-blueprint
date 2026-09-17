using System;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring
{
    /// <summary>
    /// Portable voxel blueprint data. This asset intentionally has no dependency
    /// on VR3 gameplay, third-party voxel runtimes, scenes, or prefabs, so its whole folder can
    /// be copied into a clean Unity project.
    /// </summary>
    [CreateAssetMenu(fileName = "VoxelBlueprint", menuName = "Voxel Blueprint Authoring/Blueprint")]
    public sealed class VoxelBlueprintAsset : ScriptableObject
    {
        [Serializable]
        public struct Cell
        {
            public bool enabled;
            public Color32 color;
            public byte value;
        }

        public int sizeX = 16;
        public int sizeY = 16;
        public int sizeZ = 16;
        public VoxelBlueprintInteriorMode interiorMode = VoxelBlueprintInteriorMode.SurfaceShell;
        [SerializeField] Cell[] cells;

        public int CellCount => Mathf.Max(1, sizeX) * Mathf.Max(1, sizeY) * Mathf.Max(1, sizeZ);

        public void Resize(int x, int y, int z, bool preserveCells = true)
        {
            x = Mathf.Max(1, x);
            y = Mathf.Max(1, y);
            z = Mathf.Max(1, z);
            if (x == sizeX && y == sizeY && z == sizeZ && cells != null && cells.Length == x * y * z)
                return;

            Cell[] old = cells;
            int oldX = sizeX;
            int oldY = sizeY;
            int oldZ = sizeZ;
            sizeX = x;
            sizeY = y;
            sizeZ = z;
            cells = new Cell[CellCount];
            if (!preserveCells || old == null)
                return;

            for (int cz = 0; cz < Mathf.Min(oldZ, sizeZ); cz++)
            for (int cy = 0; cy < Mathf.Min(oldY, sizeY); cy++)
            for (int cx = 0; cx < Mathf.Min(oldX, sizeX); cx++)
                cells[Index(cx, cy, cz)] = old[cx + oldX * (cy + oldY * cz)];
        }

        public void Clear()
        {
            EnsureGrid();
            Array.Clear(cells, 0, cells.Length);
        }

        public bool TryGetCell(int x, int y, int z, out Cell cell)
        {
            EnsureGrid();
            if (!InBounds(x, y, z))
            {
                cell = default;
                return false;
            }
            cell = cells[Index(x, y, z)];
            return true;
        }

        public void SetCell(int x, int y, int z, bool enabled, Color32 color, byte value = 4)
        {
            EnsureGrid();
            if (!InBounds(x, y, z))
                return;
            cells[Index(x, y, z)] = new Cell { enabled = enabled, color = color, value = value };
        }

        void EnsureGrid()
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeY = Mathf.Max(1, sizeY);
            sizeZ = Mathf.Max(1, sizeZ);
            if (cells == null || cells.Length != CellCount)
                Resize(sizeX, sizeY, sizeZ, preserveCells: true);
        }

        bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < sizeX && y < sizeY && z < sizeZ;
        int Index(int x, int y, int z) => x + sizeX * (y + sizeY * z);
    }
}
