using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring
{
    public static class VoxelBlueprintArmorFleshClip
    {
        public delegate bool Occupied(int x, int y, int z);

        public static bool ShouldShowFlesh(
            Occupied occupied,
            int sizeX,
            int sizeY,
            int sizeZ,
            Vector3 originInArmorGrid,
            Vector3 fleshInArmorGrid)
        {
            int px = Mathf.FloorToInt(fleshInArmorGrid.x);
            int py = Mathf.FloorToInt(fleshInArmorGrid.y);
            int pz = Mathf.FloorToInt(fleshInArmorGrid.z);
            if (IsOccupied(occupied, sizeX, sizeY, sizeZ, px, py, pz))
                return false;

            Vector3 delta = fleshInArmorGrid - originInArmorGrid;
            float len = delta.magnitude;
            if (len < 1e-4f)
                return true;

            if (!TryFirstArmorHitDistance(occupied, sizeX, sizeY, sizeZ, originInArmorGrid, delta / len, out float hit))
                return true;
            return len <= hit + 0.51f;
        }

        static bool IsOccupied(Occupied occupied, int sizeX, int sizeY, int sizeZ, int x, int y, int z)
        {
            if (occupied == null)
                return false;
            if (x < 0 || y < 0 || z < 0 || x >= sizeX || y >= sizeY || z >= sizeZ)
                return false;
            return occupied(x, y, z);
        }

        static bool TryFirstArmorHitDistance(
            Occupied occupied,
            int sizeX,
            int sizeY,
            int sizeZ,
            Vector3 origin,
            Vector3 dir,
            out float hit)
        {
            hit = 0f;
            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);
            int stepX = dir.x >= 0f ? 1 : -1;
            int stepY = dir.y >= 0f ? 1 : -1;
            int stepZ = dir.z >= 0f ? 1 : -1;
            float tMaxX = AxisT(origin.x, dir.x, x, stepX, out float tDeltaX);
            float tMaxY = AxisT(origin.y, dir.y, y, stepY, out float tDeltaY);
            float tMaxZ = AxisT(origin.z, dir.z, z, stepZ, out float tDeltaZ);
            float t = 0f;
            int maxSteps = sizeX + sizeY + sizeZ + 16;
            for (int i = 0; i < maxSteps; i++)
            {
                if (IsOccupied(occupied, sizeX, sizeY, sizeZ, x, y, z))
                {
                    hit = t;
                    return true;
                }

                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    x += stepX;
                }
                else if (tMaxY < tMaxZ)
                {
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    y += stepY;
                }
                else
                {
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    z += stepZ;
                }
            }

            return false;
        }

        static float AxisT(float origin, float dir, int cell, int step, out float tDelta)
        {
            if (Mathf.Abs(dir) < 1e-8f)
            {
                tDelta = float.PositiveInfinity;
                return float.PositiveInfinity;
            }

            tDelta = 1f / Mathf.Abs(dir);
            float next = step > 0 ? cell + 1 - origin : origin - cell;
            return next * tDelta;
        }
    }
}
