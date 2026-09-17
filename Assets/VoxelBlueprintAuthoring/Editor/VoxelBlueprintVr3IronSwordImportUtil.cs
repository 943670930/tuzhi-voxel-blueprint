using System.IO;
using UnityEditor;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintVr3IronSwordImportUtil
    {
        public const string FbxPath = "Assets/3DModel/Vr3IronSword_Sword4/Sword4_FBX.fbx";
        public const string TexturePath = "Assets/3DModel/Vr3IronSword_Sword4/Textures/Sword4_Albedo_Yellow.png";
        public const string OutputFolder = "Assets/VoxelBlueprints/Imported3DModels/Vr3IronSword_Sword4_VoxelV01";
        public const string BlueprintAssetPath = OutputFolder + "/PmWeaponVoxelStyle_Vr3IronSword_Sword4_VoxelV01.asset";
        public const string BlueprintAssetFileName = "PmWeaponVoxelStyle_Vr3IronSword_Sword4_VoxelV01.asset";
        const int TargetCellsOnLongestAxis = 42;

        public static bool IsIronSwordBlueprintAsset(string assetPath) =>
            !string.IsNullOrEmpty(assetPath)
            && Path.GetFileName(assetPath) == BlueprintAssetFileName;

        [MenuItem("Tools/Voxel Blueprint Authoring/Import VR3 Iron Sword (Sword4)")]
        public static void ImportIronSwordMenu() => ImportIronSword(true);

        public static bool ImportIronSword(bool logDialog) =>
            ImportIronSwordToBlueprint(BlueprintAssetPath, logDialog);

        public static bool ImportIronSwordToBlueprint(string blueprintAssetPath, bool logDialog)
        {
            string outputFolder = Path.GetDirectoryName(blueprintAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(outputFolder))
                return false;
            return VoxelBlueprintWeaponSurfaceImportCore.ImportWeaponSurface(
                FbxPath,
                TexturePath,
                outputFolder,
                blueprintAssetPath,
                "Source and processing record",
                "- **Model:** VR3 `WeaponPrefab/铁剑` — Blink `Sword4_FBX.fbx` + `Sword4_Albedo_Yellow.png`.\n" +
                "- **Source project copy:** `Assets/3DModel/Vr3IronSword_Sword4/`.\n" +
                "- **Axes:** Unity Y-up; mesh import orientation from FBX.\n" +
                "- **Colour:** sampled from `Sword4_Albedo_Yellow.png` UVs at surface stamps.",
                TargetCellsOnLongestAxis,
                logDialog);
        }
    }
}