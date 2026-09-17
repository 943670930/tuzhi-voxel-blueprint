using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintVr3WeaponBatchImportUtil
    {
        const string Vr3ProjectRoot = @"D:\SVNRoot\trunk\VR3";
        const string TuZhiModelBatchRoot = "Assets/3DModel/Vr3WeaponBatch";
        const string TuZhiBlueprintWeaponsRoot = "Assets/VoxelBlueprints/Weapons";
        const string WeaponFullPrefix = "PmWeaponVoxelStyle_";
        const int TargetCellsOnLongestAxis = 42;

        public readonly struct WeaponBatchSpec
        {
            public readonly string itemId;
            public readonly string displayName;
            public readonly string vr3FbxAssetPath;
            public readonly string vr3TextureAssetPath;
            public readonly string meshNameFilter;

            public WeaponBatchSpec(
                string itemId,
                string displayName,
                string vr3FbxAssetPath,
                string vr3TextureAssetPath = null,
                string meshNameFilter = null)
            {
                this.itemId = itemId;
                this.displayName = displayName;
                this.vr3FbxAssetPath = vr3FbxAssetPath;
                this.vr3TextureAssetPath = vr3TextureAssetPath;
                this.meshNameFilter = meshNameFilter;
            }

            public string SeriesFolder => itemId;

            public string BlueprintAssetFileName => WeaponFullPrefix + itemId + ".asset";

            public string TuZhiModelFolder => TuZhiModelBatchRoot + "/" + itemId;

            public string TuZhiBlueprintFolder => TuZhiBlueprintWeaponsRoot + "/" + itemId;

            public string TuZhiBlueprintAssetPath => TuZhiBlueprintFolder + "/" + BlueprintAssetFileName;
        }

        public static readonly WeaponBatchSpec[] ActiveWeaponBatch =
        {
            new WeaponBatchSpec(
                "iron_spear",
                "铁枪",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Models/SM_Wep_Spear_01.fbx",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Textures/Dungeons_Texture_01.png"),
            new WeaponBatchSpec(
                "dagger_bronze",
                "匕首",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Models/SM_Wep_Straightsword_01.fbx",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Textures/Dungeons_Texture_01.png"),
            new WeaponBatchSpec(
                "iron_hammer",
                "铁锤",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Models/SM_Wep_Hammer_Large_Stone_01.fbx",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Textures/Dungeons_Texture_01.png"),
            new WeaponBatchSpec(
                "javelin",
                "投枪",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Models/SM_Wep_Spear_02.fbx",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Textures/Dungeons_Texture_02.png"),
            new WeaponBatchSpec(
                "javelin_pouch",
                "枪袋",
                "Assets/SourcesMaterial/Art/Grruzam Archer Animation/Modeling/Unity_Grruzam_BaseModeling_Archer_Default.FBX",
                meshNameFilter: "Quiver_Mesh"),
            new WeaponBatchSpec(
                "wooden_shield",
                "木盾",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Models/SM_Wep_Shield_Chitin_01.fbx",
                "Assets/SourcesMaterial/Art/Map/PolygonDungeon/Textures/Dungeons_Texture_01.png"),
            new WeaponBatchSpec(
                "shotgun",
                "霰弹枪",
                "Assets/SourcesMaterial/Art/Low Poly ShotGun Weapon Pack 1/Models/Weapons/ShotGun_B.fbx",
                "Assets/SourcesMaterial/Art/Low Poly ShotGun Weapon Pack 1/Texture/Low Poly Weapon.png"),
        };

        static readonly HashSet<string> AlreadyDoneItemIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "sword_iron",
            "giant_sword",
        };

        const string PendingBatchFlagFile = "Tools/_run_voxel_batch_phase1.flag";

        [InitializeOnLoadMethod]
        static void TryRunPendingPhase1Batch()
        {
            string flagPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, PendingBatchFlagFile);
            if (!File.Exists(flagPath))
                return;
            try { File.Delete(flagPath); }
            catch (IOException) { return; }
            EditorApplication.delayCall += () => BatchVoxelizeActiveWeapons(reimportExisting: false, showDialog: true);
        }

        [MenuItem("Tools/Voxel Blueprint Authoring/Batch/Phase1 Voxelize Active VR3 Weapons")]
        public static void BatchVoxelizeActiveWeaponsMenu() => BatchVoxelizeActiveWeapons(reimportExisting: false);

        [MenuItem("Tools/Voxel Blueprint Authoring/Batch/Phase1 Voxelize Active VR3 Weapons (Force Reimport)")]
        public static void BatchVoxelizeActiveWeaponsForceMenu() => BatchVoxelizeActiveWeapons(reimportExisting: true);

        public static void BatchVoxelizeActiveWeaponsBatchMode() =>
            BatchVoxelizeActiveWeapons(reimportExisting: false, showDialog: false);

        public static void BatchVoxelizeShotgunBatchMode()
        {
            for (int i = 0; i < ActiveWeaponBatch.Length; i++)
            {
                WeaponBatchSpec spec = ActiveWeaponBatch[i];
                if (spec.itemId != "shotgun")
                    continue;
                if (TryVoxelizeWeapon(spec, out string detail))
                    Debug.Log("[VoxelBlueprintBatch] OK shotgun -> " + spec.TuZhiBlueprintAssetPath + " " + detail);
                else
                    Debug.LogError("[VoxelBlueprintBatch] FAIL shotgun — " + detail);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            Debug.LogError("[VoxelBlueprintBatch] shotgun spec missing from ActiveWeaponBatch");
        }

        public static void BatchSeedShotgunBoxCutBatchMode()
        {
            for (int i = 0; i < ActiveWeaponBatch.Length; i++)
            {
                WeaponBatchSpec spec = ActiveWeaponBatch[i];
                if (spec.itemId != "shotgun")
                    continue;
                VoxelBlueprintAsset source =
                    AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(spec.TuZhiBlueprintAssetPath);
                if (source == null)
                {
                    Debug.LogError("[VoxelBlueprintBoxCutSeed] missing blueprint " + spec.TuZhiBlueprintAssetPath);
                    return;
                }

                VoxelBlueprintWeaponPartBoxCut cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(source);
                VoxelBlueprintWeaponPartBoxCutSeedUtil.SeedDefaultBoxes(cut, spec.itemId);
                VoxelBlueprintWeaponPartBoxCutUtil.Persist(cut, source);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[VoxelBlueprintBoxCutSeed] OK shotgun parts=" + cut.PartCount);
                return;
            }

            Debug.LogError("[VoxelBlueprintBoxCutSeed] shotgun spec missing from ActiveWeaponBatch");
        }

        public static void BatchVoxelizeActiveWeapons(bool reimportExisting, bool showDialog = true)
        {
            if (!Directory.Exists(Vr3ProjectRoot))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog(
                        "Voxel Blueprint Batch",
                        "VR3 project root not found:\n" + Vr3ProjectRoot,
                        "OK");
                else
                    Debug.LogError("[VoxelBlueprintBatch] VR3 project root not found: " + Vr3ProjectRoot);
                return;
            }

            var report = new StringBuilder();
            int ok = 0;
            int skipped = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < ActiveWeaponBatch.Length; i++)
                {
                    WeaponBatchSpec spec = ActiveWeaponBatch[i];
                    float progress = (i + 1f) / ActiveWeaponBatch.Length;
                    if (!Application.isBatchMode
                        && EditorUtility.DisplayCancelableProgressBar(
                            "Voxelize VR3 weapons",
                            spec.displayName + " (" + spec.itemId + ")",
                            progress))
                    {
                        report.AppendLine("CANCELLED after " + spec.displayName);
                        break;
                    }

                    if (AlreadyDoneItemIds.Contains(spec.itemId))
                    {
                        skipped++;
                        report.AppendLine("SKIP (already done): " + spec.displayName + " / " + spec.itemId);
                        continue;
                    }

                    if (!reimportExisting && BlueprintHasEnabledCells(spec.TuZhiBlueprintAssetPath))
                    {
                        skipped++;
                        report.AppendLine("SKIP (blueprint exists): " + spec.displayName + " -> " + spec.TuZhiBlueprintAssetPath);
                        continue;
                    }

                    if (TryVoxelizeWeapon(spec, out string detail))
                    {
                        ok++;
                        report.AppendLine("OK: " + spec.displayName + " -> " + spec.TuZhiBlueprintAssetPath + " " + detail);
                    }
                    else
                    {
                        failed++;
                        report.AppendLine("FAIL: " + spec.displayName + " / " + spec.itemId + " — " + detail);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = "Done " + ok + ", skipped " + skipped + ", failed " + failed + ".";
            Debug.Log("[VoxelBlueprintBatch] " + summary + "\n" + report);
            if (showDialog)
                EditorUtility.DisplayDialog("Voxel Blueprint Batch", summary + "\n\n" + report, "OK");
        }

        public static bool TryVoxelizeWeapon(WeaponBatchSpec spec, out string detail)
        {
            detail = null;
            if (!TryCopyVr3AssetIntoTuZhi(spec.vr3FbxAssetPath, spec.TuZhiModelFolder, out string tuZhiFbxPath, out string copyError))
            {
                detail = copyError;
                return false;
            }

            string tuZhiTexturePath = null;
            if (!string.IsNullOrEmpty(spec.vr3TextureAssetPath))
            {
                if (!TryCopyVr3AssetIntoTuZhi(spec.vr3TextureAssetPath, spec.TuZhiModelFolder, out tuZhiTexturePath, out copyError))
                {
                    detail = copyError;
                    return false;
                }
            }

            AssetDatabase.Refresh();

            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, spec.TuZhiBlueprintFolder));
            if (!VoxelBlueprintWeaponSurfaceImportCore.ImportWeaponSurface(
                    tuZhiFbxPath,
                    tuZhiTexturePath,
                    spec.TuZhiBlueprintFolder,
                    spec.TuZhiBlueprintAssetPath,
                    "VR3 weapon batch: " + spec.displayName,
                    "- **ItemId:** `" + spec.itemId + "`\n"
                    + "- **Display:** " + spec.displayName + "\n"
                    + "- **VR3 FBX:** `" + spec.vr3FbxAssetPath + "`\n"
                    + (string.IsNullOrEmpty(spec.meshNameFilter) ? "" : "- **Mesh filter:** `" + spec.meshNameFilter + "`\n")
                    + "- **Batch:** Phase1 surface voxelize only (cut / VR3 copy is manual next).",
                    TargetCellsOnLongestAxis,
                    logDialog: false,
                    meshNameFilter: spec.meshNameFilter))
            {
                detail = "surface import failed";
                return false;
            }

            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(spec.TuZhiBlueprintAssetPath);
            if (asset == null)
            {
                detail = "blueprint missing after import";
                return false;
            }

            detail = asset.sizeX + "x" + asset.sizeY + "x" + asset.sizeZ;
            return true;
        }

        static bool BlueprintHasEnabledCells(string blueprintAssetPath)
        {
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(blueprintAssetPath);
            if (asset == null)
                return false;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled)
                    return true;
            }
            return false;
        }

        static bool TryCopyVr3AssetIntoTuZhi(
            string vr3AssetPath,
            string tuZhiFolderAssetPath,
            out string tuZhiAssetPath,
            out string error)
        {
            tuZhiAssetPath = null;
            error = null;
            if (string.IsNullOrEmpty(vr3AssetPath))
            {
                error = "empty VR3 asset path";
                return false;
            }

            string srcAbs = Path.Combine(Vr3ProjectRoot, vr3AssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(srcAbs))
            {
                error = "VR3 file missing: " + vr3AssetPath;
                return false;
            }

            string tuZhiFolderAbs = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                tuZhiFolderAssetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(tuZhiFolderAbs);
            string dstAbs = Path.Combine(tuZhiFolderAbs, Path.GetFileName(srcAbs));
            File.Copy(srcAbs, dstAbs, overwrite: true);
            tuZhiAssetPath = tuZhiFolderAssetPath + "/" + Path.GetFileName(srcAbs);
            return true;
        }
    }
}
