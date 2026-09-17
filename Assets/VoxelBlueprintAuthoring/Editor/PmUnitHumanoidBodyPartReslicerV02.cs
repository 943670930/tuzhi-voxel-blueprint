using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    /// <summary>
    /// Re-slices the approved full-body V01 cells without voxelising, colouring,
    /// scaling, or creating any new cells. V02 corrects the pelvis/thigh seam:
    /// the two side clusters at the top of the legs follow their UpperLegs, the
    /// lower central Torso band becomes Hips, and the detached waist-side cells
    /// follow their respective hands instead of appearing as Hips fragments.
    /// </summary>
    public static class PmUnitHumanoidBodyPartReslicerV02
    {
        const string SourcePath = "Assets/VoxelBlueprints/Imported3DModels/AIGeneratedHumanoid_VoxelV01/PmUnitVoxelFullBodyStyle_AIGeneratedHumanoid_VoxelV01.asset";
        const string V01Folder = "Assets/VoxelBlueprints/Imported3DModels/AIGeneratedHumanoid_UnitParts_FromFullBody_V01";
        const string V02Folder = "Assets/VoxelBlueprints/Imported3DModels/AIGeneratedHumanoid_UnitParts_FromFullBody_V02";
        const string Vr3TargetFolder = "D:/SVNRoot/trunk/VR3/Assets/Temp";
        const string Prefix = "PmUnitVoxelBodyPartStyle_AIGeneratedHumanoid_";
        const string Suffix = "_V01";

        static readonly string[] Labels =
        {
            "Head", "Torso", "Hips",
            "UpperArm_L", "Forearm_L", "Hand_L",
            "UpperArm_R", "Forearm_R", "Hand_R",
            "UpperLeg_L", "LowerLeg_L", "Foot_L",
            "UpperLeg_R", "LowerLeg_R", "Foot_R",
        };

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("TuZhi/Unit/Re-slice AIGeneratedHumanoid V02 (Hips + Upper Legs)")]
        public static void ReSliceFromMenu()
        {
            string result = ReSlice();
            if (result.StartsWith("FAIL", StringComparison.Ordinal))
                Debug.LogError(result);
            else
                Debug.Log(result);
        }

        public static string ReSlice()
        {
            VoxelBlueprintAsset source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(SourcePath);
            if (source == null)
                return "FAIL: missing authoritative full-body blueprint: " + SourcePath;

            if (!AssetDatabase.IsValidFolder(V02Folder))
                AssetDatabase.CreateFolder("Assets/VoxelBlueprints/Imported3DModels", "AIGeneratedHumanoid_UnitParts_FromFullBody_V02");

            var owners = new Dictionary<Vector3Int, string>();
            for (int i = 0; i < Labels.Length; i++)
            {
                string label = Labels[i];
                string path = V01Folder + "/" + Prefix + label + Suffix + ".asset";
                VoxelBlueprintAsset oldPart = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(path);
                if (oldPart == null)
                    return "FAIL: missing V01 part: " + path;
                if (!TryFindSourceOffset(source, oldPart, out Vector3Int offset))
                    return "FAIL: V01 part cannot be located in source: " + label;

                for (int z = 0; z < oldPart.sizeZ; z++)
                for (int y = 0; y < oldPart.sizeY; y++)
                for (int x = 0; x < oldPart.sizeX; x++)
                {
                    if (!oldPart.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                        continue;
                    Vector3Int sourceCell = new Vector3Int(x, y, z) + offset;
                    if (owners.ContainsKey(sourceCell))
                        return "FAIL: duplicate V01 source-cell ownership at " + sourceCell;
                    owners.Add(sourceCell, label);
                }
            }

            int sourceCount = CountEnabled(source);
            if (owners.Count != sourceCount)
                return "FAIL: V01 ownership=" + owners.Count + ", source=" + sourceCount + ". No assets were changed.";

            var output = new Dictionary<string, List<Vector3Int>>();
            for (int i = 0; i < Labels.Length; i++) output.Add(Labels[i], new List<Vector3Int>());
            foreach (KeyValuePair<Vector3Int, string> pair in owners)
                output[ResolveV02Owner(pair.Key, pair.Value)].Add(pair.Key);

            int outputCount = 0;
            for (int i = 0; i < Labels.Length; i++)
                outputCount += output[Labels[i]].Count;
            if (outputCount != sourceCount)
                return "FAIL: V02 ownership=" + outputCount + ", source=" + sourceCount + ". No assets were changed.";

            for (int i = 0; i < Labels.Length; i++)
                WritePartAsset(source, Labels[i], output[Labels[i]]);

            WriteSourceRecord(output, sourceCount);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CopyV02ToVr3();
            return "[PmUnitHumanoidBodyPartReslicerV02] OK: " + sourceCount
                   + " source cells assigned exactly once. Hips=" + output["Hips"].Count
                   + ", UpperLeg_L=" + output["UpperLeg_L"].Count
                   + ", UpperLeg_R=" + output["UpperLeg_R"].Count + ".";
        }

        static string ResolveV02Owner(Vector3Int p, string oldOwner)
        {
            if (oldOwner == "Hips" && p.y >= 28 && p.y <= 29 && p.z >= 8)
            {
                if (p.x >= 22) return "Hand_R";
                if (p.x <= 3) return "Hand_L";
            }

            if (p.y >= 5 && p.y <= 15)
            {
                if (p.x >= 16 && p.x <= 20) return "LowerLeg_R";
                if (p.x >= 6 && p.x <= 10) return "LowerLeg_L";
            }

            if (p.y >= 16 && p.y <= 28)
            {
                if (p.x >= 14 && p.x <= 20) return "UpperLeg_R";
                if (p.x >= 6 && p.x <= 12) return "UpperLeg_L";
                if (oldOwner == "Hips" || oldOwner == "Torso") return "Hips";
            }

            if (oldOwner == "Torso" && p.y >= 29 && p.y <= 34 && p.x >= 6 && p.x <= 20)
                return "Hips";
            return oldOwner;
        }

        static bool TryFindSourceOffset(VoxelBlueprintAsset source, VoxelBlueprintAsset part, out Vector3Int offset)
        {
            offset = default;
            if (!TryFindFirstEnabled(part, out Vector3Int first, out Color32 firstColor))
                return false;

            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell candidate)
                    || !candidate.enabled || !candidate.color.Equals(firstColor))
                    continue;
                Vector3Int trial = new Vector3Int(x, y, z) - first;
                if (!FitsSource(source, part, trial))
                    continue;
                offset = trial;
                return true;
            }
            return false;
        }

        static bool FitsSource(VoxelBlueprintAsset source, VoxelBlueprintAsset part, Vector3Int offset)
        {
            for (int z = 0; z < part.sizeZ; z++)
            for (int y = 0; y < part.sizeY; y++)
            for (int x = 0; x < part.sizeX; x++)
            {
                if (!part.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                Vector3Int p = new Vector3Int(x, y, z) + offset;
                if (!source.TryGetCell(p.x, p.y, p.z, out VoxelBlueprintAsset.Cell sourceCell)
                    || !sourceCell.enabled || !sourceCell.color.Equals(cell.color))
                    return false;
            }
            return true;
        }

        static void WritePartAsset(VoxelBlueprintAsset source, string label, List<Vector3Int> cells)
        {
            if (cells.Count == 0)
                throw new InvalidOperationException("V02 part is empty: " + label);

            Vector3Int min = cells[0], max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                min = Vector3Int.Min(min, cells[i]);
                max = Vector3Int.Max(max, cells[i]);
            }
            Vector3Int size = max - min + Vector3Int.one;
            string basePath = V02Folder + "/" + Prefix + label + Suffix;
            string assetPath = basePath + ".asset";
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VoxelBlueprintAsset>();
                asset.name = Prefix + label + Suffix;
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.Resize(size.x, size.y, size.z, preserveCells: false);
            asset.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int sourceCell = cells[i];
                source.TryGetCell(sourceCell.x, sourceCell.y, sourceCell.z, out VoxelBlueprintAsset.Cell cell);
                Vector3Int local = sourceCell - min;
                asset.SetCell(local.x, local.y, local.z, true, cell.color, cell.value);
            }
            EditorUtility.SetDirty(asset);
            MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, basePath + ".vox"), asset);
        }

        static bool TryFindFirstEnabled(VoxelBlueprintAsset asset, out Vector3Int point, out Color32 color)
        {
            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
                if (asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled)
                {
                    point = new Vector3Int(x, y, z);
                    color = cell.color;
                    return true;
                }
            point = default;
            color = default;
            return false;
        }

        static int CountEnabled(VoxelBlueprintAsset asset)
        {
            int count = 0;
            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
                if (asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) && cell.enabled) count++;
            return count;
        }

        static void WriteSourceRecord(Dictionary<string, List<Vector3Int>> output, int total)
        {
            var lines = new List<string>
            {
                "# Source and processing record",
                "",
                "- **Authoritative source:** `PmUnitVoxelFullBodyStyle_AIGeneratedHumanoid_VoxelV01.asset`.",
                "- **Processing:** direct source-cell reassignment only; no re-voxelization, recolouring, filling, dilation, or scaling.",
                "- **Axes:** Unity Y-up; visual front is +Z.",
                "- **Left/right:** human anatomical. Facing the camera, `_L` is the person's left (viewer's right). See `Docs/AI/UnitVoxelBlueprintConventions.md`.",
                "- **V02 seam:** side clusters at the old Hips/UpperLeg boundary belong to the corresponding UpperLeg; the lower central Torso band belongs to Hips; detached Hips side cells at y=28..29/z=8..9 belong to the corresponding Hand.",
                "- **Integrity:** the 15 parts contain every one of the `" + total + "` enabled source cells exactly once.",
                "",
                "| Part | Enabled voxels |",
                "| --- | ---: |",
            };
            for (int i = 0; i < Labels.Length; i++)
                lines.Add("| " + Labels[i] + " | " + output[Labels[i]].Count + " |");
            File.WriteAllLines(Path.Combine(ProjectRoot, V02Folder, "SOURCE.md"), lines);
        }

        // Copy only content files. VR3 deliberately retains its .meta files and
        // their GUIDs, so existing editor references remain stable.
        static void CopyV02ToVr3()
        {
            if (!Directory.Exists(Vr3TargetFolder))
                throw new DirectoryNotFoundException("VR3 Assets/Temp folder not found: " + Vr3TargetFolder);

            string sourceRoot = Path.Combine(ProjectRoot, V02Folder);
            for (int i = 0; i < Labels.Length; i++)
            {
                string baseName = Prefix + Labels[i] + Suffix;
                File.Copy(Path.Combine(sourceRoot, baseName + ".asset"), Path.Combine(Vr3TargetFolder, baseName + ".asset"), true);
                File.Copy(Path.Combine(sourceRoot, baseName + ".vox"), Path.Combine(Vr3TargetFolder, baseName + ".vox"), true);
            }
            File.Copy(Path.Combine(sourceRoot, "SOURCE.md"), Path.Combine(Vr3TargetFolder, "SOURCE.md"), true);
        }
    }
}
