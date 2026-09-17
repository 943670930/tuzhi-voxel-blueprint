using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintOpenGameArtSword01Vr3Pipeline
    {
        public const string Vr3MeshAssetPath =
            "Assets/LightGunShooting/Art/Weapons/BakedMeshes/OpenGameArtFantasySword01_VoxelV01/OpenGameArtFantasySword01_VoxelV01_Mesh.asset";
        public const string Vr3BlueprintAssetPath =
            "Assets/LightGunShooting/VoxelBlueprints/Weapons/OpenGameArtFantasySword01_VoxelV01/PmWeaponVoxelStyle_OpenGameArtFantasySword01_VoxelV01.asset";
        [MenuItem("Tools/Voxel Blueprint Authoring/Bake OpenGameArt Sword01 Mesh (TuZhi only)")]
        public static void BakeOpenGameArtSword01TuZhiMenu()
        {
            if (!VoxelBlueprintMeshBakeUtil.BakeOpenGameArtSword01(true))
                EditorUtility.DisplayDialog("Voxel Blueprint", "Bake failed. See Console.", "OK");
        }

        public static void BakeAndCopyToVr3Batch()
        {
            Debug.LogWarning("[VoxelBlueprint] Whole-weapon copy to VR3 is removed. Use Weapon Box Cut → 切割并复制到 VR3.");
        }

        public static bool TryBakeAndCopyToVr3(bool logDialog)
        {
            _ = logDialog;
            Debug.LogWarning("[VoxelBlueprint] Whole-weapon copy to VR3 is removed. Use Weapon Box Cut → 切割并复制到 VR3.");
            return VoxelBlueprintMeshBakeUtil.BakeOpenGameArtSword01(false);
        }

        static bool BakeAndCopyToVr3(bool logDialog) => TryBakeAndCopyToVr3(logDialog);
    }
}
