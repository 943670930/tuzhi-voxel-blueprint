using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintWeaponPartBoxCutSeedUtil
    {
        public static bool TryComputeEnabledBounds(VoxelBlueprintAsset source, out Vector3Int min, out Vector3Int max)
        {
            min = Vector3Int.zero;
            max = Vector3Int.zero;
            bool any = false;
            if (source == null)
                return false;
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                Vector3Int p = new Vector3Int(x, y, z);
                if (!any)
                {
                    min = max = p;
                    any = true;
                }
                else
                {
                    min = Vector3Int.Min(min, p);
                    max = Vector3Int.Max(max, p);
                }
            }

            return any;
        }

        public static bool HasSeededBoxes(VoxelBlueprintWeaponPartBoxCut cut)
        {
            if (cut == null || cut.boxes == null || cut.boxes.Count == 0)
                return false;
            for (int i = 0; i < cut.boxes.Count; i++)
            {
                VoxelBlueprintWeaponPartBoxCut.Box box = cut.boxes[i];
                box.EnsureOriented();
                if (box.size.x >= 1f && box.size.y >= 1f && box.size.z >= 1f)
                    return true;
            }

            return false;
        }

        public static void SeedDefaultBoxes(VoxelBlueprintWeaponPartBoxCut cut, string itemId)
        {
            if (cut == null || cut.source == null)
                return;
            if (!TryComputeEnabledBounds(cut.source, out Vector3Int min, out Vector3Int max))
                return;

            cut.weaponItemId = string.IsNullOrWhiteSpace(itemId) ? cut.weaponItemId : itemId.Trim();
            Vector3Int extent = max - min + Vector3Int.one;

            if (itemId == "javelin_pouch")
                SeedSinglePart(cut, min, max, extent, "枪袋");
            else if (itemId == "wooden_shield")
                SeedThreePartsAlongY(cut, min, max, extent, "mesh_0", "mesh_1", "mesh_2");
            else if (itemId == "shotgun")
                SeedTwoPartsAlongY(cut, min, max, extent, "枪身", "枪托");
            else
                SeedBladeHandleAlongY(cut, min, max, extent);
            cut.EnsureDefaults();
        }

        static void SeedSinglePart(
            VoxelBlueprintWeaponPartBoxCut cut,
            Vector3Int min,
            Vector3Int max,
            Vector3Int extent,
            string label)
        {
            Vector3 size = new Vector3(extent.x, extent.y, extent.z + 1f);
            Vector3 center = CellCenter(min, max);
            cut.partLabels = new List<string> { label };
            cut.boxes = new List<VoxelBlueprintWeaponPartBoxCut.Box> { MakeBox(label, center, size) };
        }

        static void SeedThreePartsAlongY(
            VoxelBlueprintWeaponPartBoxCut cut,
            Vector3Int min,
            Vector3Int max,
            Vector3Int extent,
            string label0,
            string label1,
            string label2)
        {
            cut.partLabels = new List<string> { label0, label1, label2 };
            cut.boxes = new List<VoxelBlueprintWeaponPartBoxCut.Box>(3);
            int band = Mathf.Max(1, extent.y / 3);
            for (int i = 0; i < 3; i++)
            {
                int yMin = min.y + i * band;
                int yMax = i == 2 ? max.y : min.y + (i + 1) * band - 1;
                Vector3Int bandMin = new Vector3Int(min.x, yMin, min.z);
                Vector3Int bandMax = new Vector3Int(max.x, yMax, max.z);
                Vector3Int bandExtent = bandMax - bandMin + Vector3Int.one;
                Vector3 size = new Vector3(bandExtent.x, bandExtent.y + 1f, bandExtent.z);
                Vector3 center = CellCenter(bandMin, bandMax);
                cut.boxes.Add(MakeBox(cut.partLabels[i], center, size));
            }
        }

        static void SeedBladeHandleAlongY(
            VoxelBlueprintWeaponPartBoxCut cut,
            Vector3Int min,
            Vector3Int max,
            Vector3Int extent)
        {
            SeedTwoPartsAlongY(
                cut,
                min,
                max,
                extent,
                VoxelBlueprintWeaponPartBoxCut.DefaultBladeLabel,
                VoxelBlueprintWeaponPartBoxCut.DefaultHandleLabel);
        }

        static void SeedTwoPartsAlongY(
            VoxelBlueprintWeaponPartBoxCut cut,
            Vector3Int min,
            Vector3Int max,
            Vector3Int extent,
            string upperLabel,
            string lowerLabel)
        {
            Vector3 size = new Vector3(extent.x, extent.y + 1f, extent.z);
            float centerX = min.x + extent.x * 0.5f;
            float centerZ = min.z + extent.z * 0.5f;
            float upperCenterY = max.y + 0.5f - size.y * 0.25f + 2f;
            float lowerCenterY = min.y + 0.5f - size.y * 0.25f - 1f;
            cut.partLabels = new List<string> { upperLabel, lowerLabel };
            cut.boxes = new List<VoxelBlueprintWeaponPartBoxCut.Box>
            {
                MakeBox(upperLabel, new Vector3(centerX, upperCenterY, centerZ), size),
                MakeBox(lowerLabel, new Vector3(centerX, lowerCenterY, centerZ), size),
            };
        }

        static Vector3 CellCenter(Vector3Int min, Vector3Int max)
        {
            Vector3Int extent = max - min + Vector3Int.one;
            return new Vector3(min.x + extent.x * 0.5f, min.y + extent.y * 0.5f, min.z + extent.z * 0.5f);
        }

        static VoxelBlueprintWeaponPartBoxCut.Box MakeBox(string label, Vector3 center, Vector3 size)
        {
            var box = new VoxelBlueprintWeaponPartBoxCut.Box
            {
                label = label,
                center = center,
                size = Vector3.Max(size, Vector3.one),
                euler = Vector3.zero,
            };
            box.EnsureOriented();
            return box;
        }

        [MenuItem("Tools/Voxel Blueprint Authoring/Batch/Phase1b Seed Weapon Part Box Cuts")]
        public static void BatchSeedWeaponPartBoxCutsMenu() => BatchSeedWeaponPartBoxCuts(reseedExisting: false);

        [MenuItem("Tools/Voxel Blueprint Authoring/Batch/Phase1b Seed Weapon Part Box Cuts (Force Reseed)")]
        public static void BatchSeedWeaponPartBoxCutsForceMenu() => BatchSeedWeaponPartBoxCuts(reseedExisting: true);

        public static void BatchSeedWeaponPartBoxCutsBatchMode() =>
            BatchSeedWeaponPartBoxCuts(reseedExisting: false, showDialog: false);

        public static void BatchSeedWeaponPartBoxCuts(bool reseedExisting, bool showDialog = true)
        {
            var report = new StringBuilder();
            int ok = 0;
            int skipped = 0;
            int failed = 0;

            for (int i = 0; i < VoxelBlueprintVr3WeaponBatchImportUtil.ActiveWeaponBatch.Length; i++)
            {
                VoxelBlueprintVr3WeaponBatchImportUtil.WeaponBatchSpec spec =
                    VoxelBlueprintVr3WeaponBatchImportUtil.ActiveWeaponBatch[i];
                float progress = (i + 1f) / VoxelBlueprintVr3WeaponBatchImportUtil.ActiveWeaponBatch.Length;
                if (!Application.isBatchMode
                    && EditorUtility.DisplayCancelableProgressBar(
                        "Seed weapon part box cuts",
                        spec.displayName + " (" + spec.itemId + ")",
                        progress))
                {
                    report.AppendLine("CANCELLED after " + spec.displayName);
                    break;
                }

                VoxelBlueprintAsset source =
                    AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(spec.TuZhiBlueprintAssetPath);
                if (source == null)
                {
                    failed++;
                    report.AppendLine("FAIL: missing blueprint " + spec.TuZhiBlueprintAssetPath);
                    continue;
                }

                VoxelBlueprintWeaponPartBoxCut cut = VoxelBlueprintWeaponPartBoxCutUtil.LoadOrCreateCutFor(source);
                if (cut == null)
                {
                    failed++;
                    report.AppendLine("FAIL: could not create cut for " + spec.itemId);
                    continue;
                }

                if (!reseedExisting && HasSeededBoxes(cut))
                {
                    skipped++;
                    report.AppendLine("SKIP (already seeded): " + spec.itemId);
                    continue;
                }

                SeedDefaultBoxes(cut, spec.itemId);
                VoxelBlueprintWeaponPartBoxCutUtil.Persist(cut, source);
                ok++;
                report.AppendLine(
                    "OK: " + spec.displayName
                    + " -> " + AssetDatabase.GetAssetPath(cut)
                    + " parts=" + cut.PartCount);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = "Done " + ok + ", skipped " + skipped + ", failed " + failed + ".";
            Debug.Log("[VoxelBlueprintBoxCutSeed] " + summary + "\n" + report);
            if (showDialog)
                EditorUtility.DisplayDialog("Weapon Part Box Cut Seed", summary + "\n\n" + report, "OK");
        }
    }
}
