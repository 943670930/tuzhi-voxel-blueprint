using System;

namespace Vr3.VoxelBlueprintAuthoring
{
    public static class VoxelBlueprintInteriorUtil
    {
        public static bool IsUnitNakedBodyPartAssetName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return false;
            if (!assetName.StartsWith("PmUnitVoxelBodyPartStyle_", StringComparison.Ordinal))
                return false;
            if (assetName.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (assetName.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        public static VoxelBlueprintInteriorMode RecommendedInteriorMode(string assetName)
        {
            return IsUnitNakedBodyPartAssetName(assetName)
                ? VoxelBlueprintInteriorMode.SolidDepthFill
                : VoxelBlueprintInteriorMode.SurfaceShell;
        }

        public static bool TryGetMaterializedCell(VoxelBlueprintAsset asset, int x, int y, int z, out VoxelBlueprintAsset.Cell cell)
        {
            cell = default;
            if (asset == null || !asset.TryGetCell(x, y, z, out cell))
                return false;

            if (cell.enabled)
                return true;

            if (asset.interiorMode != VoxelBlueprintInteriorMode.SolidDepthFill)
                return false;

            if (!TryGetColumnSpan(asset, x, y, out int firstZ, out int lastZ))
                return false;
            if (z <= firstZ || z >= lastZ)
                return false;

            cell = NearestColumnCell(asset, x, y, z, firstZ, lastZ);
            return true;
        }

        public static bool IsMaterializedCellEnabled(VoxelBlueprintAsset asset, int x, int y, int z)
        {
            return TryGetMaterializedCell(asset, x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled;
        }

        static bool TryGetColumnSpan(VoxelBlueprintAsset asset, int x, int y, out int firstZ, out int lastZ)
        {
            firstZ = -1;
            lastZ = -1;
            if (asset == null)
                return false;

            for (int z = 0; z < asset.sizeZ; z++)
            {
                if (!asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (firstZ < 0)
                    firstZ = z;
                lastZ = z;
            }

            return firstZ >= 0 && lastZ > firstZ;
        }

        static VoxelBlueprintAsset.Cell NearestColumnCell(VoxelBlueprintAsset asset, int x, int y, int z, int firstZ, int lastZ)
        {
            int frontDistance = z - firstZ;
            int backDistance = lastZ - z;
            int start = frontDistance <= backDistance ? firstZ : lastZ;
            int direction = start == firstZ ? 1 : -1;
            for (int sample = start; sample >= firstZ && sample <= lastZ; sample += direction)
            {
                if (asset.TryGetCell(x, y, sample, out VoxelBlueprintAsset.Cell cell) && cell.enabled)
                    return cell;
            }

            throw new InvalidOperationException("Expected a surface voxel in an occupied column.");
        }
    }
}
