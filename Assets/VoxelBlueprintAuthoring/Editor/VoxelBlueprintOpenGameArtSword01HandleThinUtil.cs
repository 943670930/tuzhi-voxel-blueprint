using UnityEditor;
using UnityEngine;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintOpenGameArtSword01HandleThinUtil
    {
        public const string BlueprintAssetPath = VoxelBlueprintFbxSurfaceImportUtil.BlueprintAssetPath;
        const int HandleMaxY = 8;
        const int SpineMaxY = 7;
        const int CenterX = 5;
        const int CenterZ = 2;

        [MenuItem("Tools/Voxel Blueprint Authoring/Thin OpenGameArt Sword01 Handle")]
        public static void ThinHandleMenu() => ThinHandle(true);

        public static void ThinHandleBatch() => ThinHandle(false);

        public static bool ThinHandle(bool logDialog)
        {
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(BlueprintAssetPath);
            if (asset == null)
            {
                if (logDialog)
                    EditorUtility.DisplayDialog("Voxel Blueprint", "Blueprint not found:\n" + BlueprintAssetPath, "OK");
                return false;
            }

            int removed = 0;
            for (int y = 0; y <= HandleMaxY && y < asset.sizeY; y++)
            {
                if (y <= SpineMaxY)
                    removed += ThinSpineSlice(asset, y);
                else
                    removed += ThinTransitionSlice(asset, y);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (logDialog)
                EditorUtility.DisplayDialog("Voxel Blueprint", "Handle thinned. Removed " + removed + " cells.", "OK");
            else
                Debug.Log("[VoxelBlueprint] OpenGameArt Sword01 handle thinned. Removed " + removed + " cells.");
            return true;
        }

        static int ThinSpineSlice(VoxelBlueprintAsset asset, int y)
        {
            int bestX = -1;
            int bestZ = -1;
            int bestScore = int.MaxValue;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                int score = Mathf.Abs(x - CenterX) + Mathf.Abs(z - CenterZ);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestZ = z;
                }
            }

            if (bestX < 0)
                return 0;

            int removed = 0;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (x == bestX && z == bestZ)
                    continue;
                asset.SetCell(x, y, z, false, cell.color, cell.value);
                removed++;
            }
            return removed;
        }

        static int ThinTransitionSlice(VoxelBlueprintAsset asset, int y)
        {
            int removed = 0;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (z == CenterZ && x >= CenterX - 1 && x <= CenterX + 1)
                    continue;
                asset.SetCell(x, y, z, false, cell.color, cell.value);
                removed++;
            }
            return removed;
        }
    }
}
