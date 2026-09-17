using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintPartBoxCutUtil
    {
        public const string SourcePath =
            "Assets/VoxelBlueprints/Imported3DModels/AigeiFantasyRpgArmor_VoxelV04/PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV04.asset";
        public const string PartsFolder =
            "Assets/VoxelBlueprints/Imported3DModels/AigeiFantasyRpgArmor_UnitParts_V04";
        public const string CutAssetPath = PartsFolder + "/AigeiFantasyRpgArmor_V04_PartBoxCut.asset";
        public const string Prefix = "PmUnitVoxelBodyPartStyle_AigeiFantasyRpgArmor_";
        public const string Suffix = "_V04";
        const string Vr3Temp = "D:/SVNRoot/trunk/VR3/Assets/Temp";
        const string Vr3Res = "D:/SVNRoot/trunk/VR3/Assets/LightGunShooting/Resources/UnitVoxelArmor/AigeiFantasyRpgArmor";
        const string V06Folder = "Assets/VoxelBlueprints/Good/AigeiFantasyRpgArmor_VoxelV06";
        const string V06PartsFolder = V06Folder + "/Part";
        const string V06CutAssetPath = V06PartsFolder + "/AigeiFantasyRpgArmor_VoxelV06_PartBoxCut.asset";
        const string V06Suffix = "_VoxelV06";

        struct OutputSpec
        {
            public string partsFolder;
            public string cutAssetPath;
            public string prefix;
            public string suffix;
            public string sourceAssetName;
            public bool copyArmorToResources;
        }

        const string FullBodyNamePrefix = "PmUnitVoxelFullBodyStyle_";
        const string BodyPartNamePrefix = "PmUnitVoxelBodyPartStyle_";

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static OutputSpec LegacyArmorV04Spec() => new OutputSpec
        {
            partsFolder = PartsFolder,
            cutAssetPath = CutAssetPath,
            prefix = Prefix,
            suffix = Suffix,
            sourceAssetName = "PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV04",
            copyArmorToResources = true,
        };

        static OutputSpec ArmorV06Spec() => new OutputSpec
        {
            partsFolder = V06PartsFolder,
            cutAssetPath = V06CutAssetPath,
            prefix = Prefix,
            suffix = V06Suffix,
            sourceAssetName = "PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06",
            copyArmorToResources = true,
        };

        static void DerivePartNaming(string fullBodyAssetName, out string prefix, out string suffix)
        {
            if (string.IsNullOrEmpty(fullBodyAssetName)
                || !fullBodyAssetName.StartsWith(FullBodyNamePrefix, StringComparison.Ordinal))
            {
                prefix = BodyPartNamePrefix;
                suffix = "";
                return;
            }

            string rest = fullBodyAssetName.Substring(FullBodyNamePrefix.Length);
            int split = rest.LastIndexOf('_');
            if (split <= 0)
            {
                prefix = BodyPartNamePrefix + rest + "_";
                suffix = "";
                return;
            }

            string series = rest.Substring(0, split);
            suffix = rest.Substring(split);
            prefix = BodyPartNamePrefix + series + "_";
        }

        static OutputSpec SpecFor(VoxelBlueprintAsset source)
        {
            if (source == null)
                return LegacyArmorV04Spec();

            string sourcePath = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
            if (sourcePath.Contains("AigeiFantasyRpgArmor_VoxelV06", StringComparison.Ordinal))
                return ArmorV06Spec();
            if (sourcePath.EndsWith(
                    "/PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV04.asset",
                    StringComparison.Ordinal))
                return LegacyArmorV04Spec();

            string sourceDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sourceDir))
                return LegacyArmorV04Spec();

            string parentFolderName = Path.GetFileName(sourceDir);
            string partsFolder = sourceDir + "/Part";
            string cutAssetPath = partsFolder + "/" + parentFolderName + "_PartBoxCut.asset";
            DerivePartNaming(source.name, out string prefix, out string suffix);

            return new OutputSpec
            {
                partsFolder = partsFolder,
                cutAssetPath = cutAssetPath,
                prefix = prefix,
                suffix = suffix,
                sourceAssetName = source.name,
                copyArmorToResources = sourcePath.IndexOf("AigeiFantasyRpgArmor", StringComparison.Ordinal) >= 0,
            };
        }

        public static string CopyPartsToVr3(VoxelBlueprintAsset source)
        {
            if (source == null)
                return "FAIL: missing source";
            return CopyToVr3(SpecFor(source));
        }

        [MenuItem("TuZhi/Unit/Copy Cut Parts To VR3")]
        public static void CopyLastCutPartsToVr3Menu()
        {
            string sourcePath = EditorPrefs.GetString("TuZhi.PartBoxCut.LastSource", "");
            VoxelBlueprintAsset source = !string.IsNullOrEmpty(sourcePath)
                ? AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(sourcePath)
                : null;
            if (source == null)
                source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(SourcePath);
            string status = CopyPartsToVr3(source);
            if (status.StartsWith("FAIL", StringComparison.Ordinal))
                Debug.LogError(status);
            else
                Debug.Log(status);
        }

        public static VoxelBlueprintPartBoxCut LoadOrCreateCut()
        {
            return LoadOrCreateCutFor(AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(SourcePath));
        }

        public static VoxelBlueprintPartBoxCut LoadOrCreateCutFor(VoxelBlueprintAsset source)
        {
            if (source == null)
                source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(SourcePath);
            OutputSpec spec = SpecFor(source);
            VoxelBlueprintPartBoxCut cut = AssetDatabase.LoadAssetAtPath<VoxelBlueprintPartBoxCut>(spec.cutAssetPath);
            if (cut == null)
            {
                EnsureFolder(spec.partsFolder);
                cut = ScriptableObject.CreateInstance<VoxelBlueprintPartBoxCut>();
                cut.source = source;
                cut.EnsureBoxes();
                AssetDatabase.CreateAsset(cut, spec.cutAssetPath);
            }
            bool lrDone = cut.limbViewerLrSwapDone;
            cut.EnsureBoxes();
            if (cut.source != source)
            {
                cut.source = source;
                EditorUtility.SetDirty(cut);
            }
            if (!lrDone && cut.limbViewerLrSwapDone)
                EditorUtility.SetDirty(cut);
            if (HasStubBoxes(cut))
                SeedBoxesFromCurrentParts(cut);
            Persist(cut, source);
            return cut;
        }

        public static void Persist(VoxelBlueprintPartBoxCut cut, VoxelBlueprintAsset source = null)
        {
            if (cut == null)
                return;
            EditorUtility.SetDirty(cut);
            AssetDatabase.SaveAssets();
            string path = AssetDatabase.GetAssetPath(cut);
            if (!string.IsNullOrEmpty(path))
                EditorPrefs.SetString("TuZhi.PartBoxCut", path);
            VoxelBlueprintAsset bindSource = source != null ? source : cut.source;
            if (bindSource != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(bindSource);
                if (!string.IsNullOrEmpty(sourcePath))
                    EditorPrefs.SetString("TuZhi.PartBoxCut.LastSource", sourcePath);
            }
        }

        public static VoxelBlueprintPartBoxCut LoadLastOrDefault()
        {
            string sourcePath = EditorPrefs.GetString("TuZhi.PartBoxCut.LastSource", "");
            if (!string.IsNullOrEmpty(sourcePath))
            {
                VoxelBlueprintAsset source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(sourcePath);
                if (source != null)
                    return LoadOrCreateCutFor(source);
            }

            string path = EditorPrefs.GetString("TuZhi.PartBoxCut", "");
            if (string.IsNullOrEmpty(path))
                path = EditorPrefs.GetString("TuZhi.ArmorBoxCut", "");
            if (!string.IsNullOrEmpty(path))
            {
                VoxelBlueprintPartBoxCut last = AssetDatabase.LoadAssetAtPath<VoxelBlueprintPartBoxCut>(path);
                if (last != null)
                    return last;
            }
            return LoadOrCreateCut();
        }

        static bool IsStub(VoxelBlueprintPartBoxCut.Box box)
        {
            box.EnsureOriented();
            return box.size.x <= 1.5f && box.size.y <= 1.5f && box.size.z <= 1.5f
                && box.center.sqrMagnitude < 3f;
        }

        public static bool HasStubBoxes(VoxelBlueprintPartBoxCut cut)
        {
            if (cut == null)
                return false;
            cut.EnsureBoxes();
            for (int i = 0; i < cut.boxes.Length; i++)
            {
                if (IsStub(cut.boxes[i]))
                    return true;
            }
            return false;
        }

        static VoxelBlueprintPartBoxCut.Box FlipBoxX(VoxelBlueprintPartBoxCut.Box src, int sizeX)
        {
            src.EnsureOriented();
            int minX = Mathf.FloorToInt(src.center.x - src.size.x * 0.5f + 0.0001f);
            int minY = Mathf.FloorToInt(src.center.y - src.size.y * 0.5f + 0.0001f);
            int minZ = Mathf.FloorToInt(src.center.z - src.size.z * 0.5f + 0.0001f);
            int maxX = minX + Mathf.Max(1, Mathf.RoundToInt(src.size.x)) - 1;
            int maxY = minY + Mathf.Max(1, Mathf.RoundToInt(src.size.y)) - 1;
            int maxZ = minZ + Mathf.Max(1, Mathf.RoundToInt(src.size.z)) - 1;
            int last = sizeX - 1;
            VoxelBlueprintPartBoxCut.Box box = VoxelBlueprintPartBoxCut.Box.FromAabb(
                src.label,
                new Vector3Int(last - maxX, minY, minZ),
                new Vector3Int(last - minX, maxY, maxZ));
            box.euler = src.euler;
            box.euler.y = -box.euler.y;
            box.euler.z = -box.euler.z;
            return box;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        public static void SeedBoxesFromCurrentParts(VoxelBlueprintPartBoxCut cut)
        {
            VoxelBlueprintAsset source = cut.source;
            if (source == null)
                throw new InvalidOperationException("missing source blueprint");
            OutputSpec spec = SpecFor(source);
            cut.EnsureBoxes();
            Undo.RecordObject(cut, "Seed Part Boxes");
            int sizeX = source.sizeX;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
                {
                    string label = VoxelBlueprintPartBoxCut.Labels[i];
                    bool dest = cut.IsArmMirrorDest(label);
                    if (pass == 0 && dest)
                        continue;
                    if (pass == 1 && !dest)
                        continue;
                    string path = spec.partsFolder + "/" + spec.prefix + label + spec.suffix + ".asset";
                    VoxelBlueprintAsset part = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(path);
                    VoxelBlueprintPartBoxCut.Box box = cut.boxes[i];
                    bool keepMirror = box.mirror;
                    box.label = label;
                    bool filled = false;
                    if (part != null && TryFindSourceOffset(source, part, out Vector3Int offset)
                        && TryPartAabb(part, out Vector3Int min, out Vector3Int max))
                    {
                        box = VoxelBlueprintPartBoxCut.Box.FromAabb(label, min + offset, max + offset);
                        filled = true;
                    }
                    if (!filled && dest)
                    {
                        int mate = cut.IndexOf(VoxelBlueprintPartBoxCut.ArmMate(label));
                        if (mate >= 0 && !IsStub(cut.boxes[mate]))
                        {
                            box = FlipBoxX(cut.boxes[mate], sizeX);
                            box.label = label;
                            filled = true;
                        }
                    }
                    box.mirror = keepMirror;
                    cut.boxes[i] = box;
                }
            }
            cut.limbViewerLrSwapDone = true;
            Persist(cut);
        }

        public static string Slice(VoxelBlueprintPartBoxCut cut)
        {
            VoxelBlueprintAsset source = cut.source;
            if (source == null)
                return "FAIL: missing source";
            OutputSpec spec = SpecFor(source);
            cut.EnsureBoxes();
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out int enabled, out int leftover);
            EnsureFolder(spec.partsFolder);
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
                WritePart(cut, source, VoxelBlueprintPartBoxCut.Labels[i], buckets[VoxelBlueprintPartBoxCut.Labels[i]], spec);
            ClearLeftoverCellsOnSource(cut, leftover);
            WriteSourceRecord(buckets, enabled, leftover, cut, spec);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string copyStatus = CopyToVr3(spec);
            string ok = "OK: " + enabled + " source cells, assigned by boxes" + MirrorNote(cut) + ".";
            if (leftover > 0)
                ok += " leftover " + leftover + " cleared from source, not worn.";
            return ok;
        }

        static void ClearLeftoverCellsOnSource(VoxelBlueprintPartBoxCut cut, int leftover)
        {
            VoxelBlueprintAsset source = cut != null ? cut.source : null;
            if (source == null || leftover <= 0)
                return;
            Undo.RecordObject(source, "Clear Unboxed Armor Cells");
            int cleared = 0;
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (!CountsAsSliceLeftover(cut, x, y, z))
                    continue;
                source.SetCell(x, y, z, false, cell.color, cell.value);
                cleared++;
            }
            if (cleared <= 0)
                return;
            EditorUtility.SetDirty(source);
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
                MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, Path.ChangeExtension(assetPath, ".vox")), source);
        }

        public static void ExecuteSliceV06()
        {
            string sourcePath = V06Folder + "/PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06.asset";
            VoxelBlueprintAsset source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(sourcePath);
            if (source == null)
                throw new InvalidOperationException("FAIL: missing V06 full body");
            VoxelBlueprintPartBoxCut cut = LoadOrCreateCutFor(source);
            string status = Slice(cut);
            if (status.StartsWith("FAIL"))
                throw new InvalidOperationException(status);
            Debug.Log(status);
        }

        public static void ExecuteRotateV06Yaw180AndRecut()
        {
            string status = RotateV06Yaw180AndRecut();
            if (status.StartsWith("FAIL"))
                throw new InvalidOperationException(status);
            Debug.Log(status);
        }

        public static string RotateV06Yaw180AndRecut()
        {
            string sourcePath = V06Folder + "/PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06.asset";
            VoxelBlueprintAsset source = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(sourcePath);
            if (source == null)
                return "FAIL: missing V06 full body";
            VoxelBlueprintPartBoxCut cut = LoadOrCreateCutFor(source);
            if (cut == null || cut.source != source)
                return "FAIL: missing V06 box cut";
            Undo.RegisterCompleteObjectUndo(source, "V06 Yaw 180");
            Undo.RegisterCompleteObjectUndo(cut, "V06 Yaw 180 Boxes");
            RotateAssetCellsYaw180(source);
            RotateCutYaw180(cut, source.sizeX, source.sizeZ);
            EditorUtility.SetDirty(source);
            Persist(cut);
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
                MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, Path.ChangeExtension(assetPath, ".vox")), source);
            WriteV06YawRecord();
            return Slice(cut);
        }

        static void RotateAssetCellsYaw180(VoxelBlueprintAsset asset)
        {
            int sx = asset.sizeX;
            int sy = asset.sizeY;
            int sz = asset.sizeZ;
            var copy = new VoxelBlueprintAsset.Cell[sx * sy * sz];
            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell);
                copy[x + sx * (y + sy * z)] = cell;
            }
            asset.Clear();
            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                VoxelBlueprintAsset.Cell cell = copy[x + sx * (y + sy * z)];
                if (!cell.enabled)
                    continue;
                asset.SetCell(sx - 1 - x, y, sz - 1 - z, true, cell.color, cell.value);
            }
        }

        static void RotateCutYaw180(VoxelBlueprintPartBoxCut cut, int sizeX, int sizeZ)
        {
            cut.EnsureBoxes();
            for (int i = 0; i < cut.boxes.Length; i++)
                cut.boxes[i] = Yaw180Box(cut.boxes[i], sizeX, sizeZ);
            RemapCellMarksYaw180(cut.rejected, sizeX, sizeZ);
            RemapCellMarksYaw180(cut.claimed, sizeX, sizeZ);
        }

        static VoxelBlueprintPartBoxCut.Box Yaw180Box(VoxelBlueprintPartBoxCut.Box box, int sizeX, int sizeZ)
        {
            box.EnsureOriented();
            box.center = new Vector3(sizeX - box.center.x, box.center.y, sizeZ - box.center.z);
            box.euler = (Quaternion.AngleAxis(180f, Vector3.up) * box.Rotation).eulerAngles;
            Vector3 h = box.size * 0.5f;
            Quaternion rot = box.Rotation;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 s = new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f);
                Vector3 p = box.center + rot * Vector3.Scale(h, s);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            box.min = new Vector3Int(Mathf.FloorToInt(min.x), Mathf.FloorToInt(min.y), Mathf.FloorToInt(min.z));
            box.max = new Vector3Int(Mathf.FloorToInt(max.x - 0.001f), Mathf.FloorToInt(max.y - 0.001f), Mathf.FloorToInt(max.z - 0.001f));
            return box;
        }

        static void RemapCellMarksYaw180(List<VoxelBlueprintPartBoxCut.RejectedCells> lists, int sizeX, int sizeZ)
        {
            if (lists == null)
                return;
            for (int i = 0; i < lists.Count; i++)
            {
                List<Vector3Int> cells = lists[i] != null ? lists[i].cells : null;
                if (cells == null)
                    continue;
                for (int c = 0; c < cells.Count; c++)
                {
                    Vector3Int p = cells[c];
                    cells[c] = new Vector3Int(sizeX - 1 - p.x, p.y, sizeZ - 1 - p.z);
                }
            }
        }

        static void WriteV06YawRecord()
        {
            string path = Path.Combine(ProjectRoot, V06Folder, "SOURCE.md");
            if (!File.Exists(path))
                return;
            string text = File.ReadAllText(path);
            const string line = "- **Yaw 180:** full-body cells and part box-cut boxes were rotated 180° about Y through the grid center so visual front is +Z (was opposite the V01 flesh drawing). Parts were recut from that rotated source.";
            if (text.Contains("Yaw 180:"))
                return;
            File.WriteAllText(path, text.TrimEnd() + Environment.NewLine + line + Environment.NewLine);
        }

        static string MirrorNote(VoxelBlueprintPartBoxCut cut)
        {
            var names = new List<string>();
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                if (cut.IsArmMirrorDest(label))
                    names.Add(label);
            }
            if (names.Count == 0)
                return "";
            return "; mirror dest " + string.Join(",", names);
        }

        static Dictionary<string, List<Vector3Int>> AssignCells(VoxelBlueprintPartBoxCut cut, out int enabled, out int leftover)
        {
            VoxelBlueprintAsset source = cut.source;
            var buckets = new Dictionary<string, List<Vector3Int>>();
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
                buckets[VoxelBlueprintPartBoxCut.Labels[i]] = new List<Vector3Int>();
            var owner = new Dictionary<Vector3Int, string>();
            enabled = 0;
            leftover = 0;
            if (source == null)
                return buckets;
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                enabled++;
                string label = cut.OwnerOf(x, y, z);
                if (label == null)
                    continue;
                var p = new Vector3Int(x, y, z);
                owner[p] = label;
            }
            foreach (KeyValuePair<Vector3Int, string> pair in owner)
                buckets[pair.Value].Add(pair.Key);
            foreach (KeyValuePair<Vector3Int, string> pair in owner)
            {
                if (!cut.IsArmMirrorSource(pair.Value))
                    continue;
                string destLabel = VoxelBlueprintPartBoxCut.ArmMate(pair.Value);
                if (string.IsNullOrEmpty(destLabel) || !cut.IsArmMirrorDest(destLabel))
                    continue;
                int mx = source.sizeX - 1 - pair.Key.x;
                if (mx < 0 || mx >= source.sizeX)
                    continue;
                buckets[destLabel].Add(new Vector3Int(mx, pair.Key.y, pair.Key.z));
            }
            leftover = 0;
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                var p = new Vector3Int(x, y, z);
                if (owner.ContainsKey(p))
                    continue;
                if (MirrorDestDiscards(cut, source, p, owner))
                    continue;
                leftover++;
            }
            return buckets;
        }

        public static void CountOwners(VoxelBlueprintPartBoxCut cut, int[] perLabel, out int leftover)
        {
            leftover = 0;
            if (perLabel == null || perLabel.Length != VoxelBlueprintPartBoxCut.PartCount)
                throw new ArgumentException("perLabel");
            Array.Clear(perLabel, 0, perLabel.Length);
            VoxelBlueprintAsset source = cut.source;
            if (source == null)
                return;
            cut.EnsureBoxes();
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out _, out leftover);
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
                perLabel[i] = buckets[VoxelBlueprintPartBoxCut.Labels[i]].Count;
        }

        public static bool CountsAsSliceLeftover(VoxelBlueprintPartBoxCut cut, int x, int y, int z)
        {
            if (cut == null || cut.source == null)
                return false;
            if (cut.OwnerOf(x, y, z) != null)
                return false;
            VoxelBlueprintAsset source = cut.source;
            int mx = source.sizeX - 1 - x;
            if (mx >= 0 && mx < source.sizeX
                && source.TryGetCell(mx, y, z, out VoxelBlueprintAsset.Cell mate) && mate.enabled)
            {
                string srcLabel = cut.OwnerOf(mx, y, z);
                if (srcLabel != null && cut.IsArmMirrorSource(srcLabel))
                    return false;
            }
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                if (cut.IsArmMirrorDest(label) && cut.boxes[i].Contains(x, y, z))
                    return false;
            }
            return true;
        }

        static bool MirrorDestDiscards(
            VoxelBlueprintPartBoxCut cut, VoxelBlueprintAsset source, Vector3Int p,
            Dictionary<Vector3Int, string> owner)
        {
            int mx = source.sizeX - 1 - p.x;
            var flipped = new Vector3Int(mx, p.y, p.z);
            if (owner.TryGetValue(flipped, out string srcLabel) && cut.IsArmMirrorSource(srcLabel))
                return true;
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                if (cut.IsArmMirrorDest(label) && cut.boxes[i].Contains(p.x, p.y, p.z))
                    return true;
            }
            return false;
        }

        static void WritePart(VoxelBlueprintPartBoxCut cut, VoxelBlueprintAsset source, string label, List<Vector3Int> cells, OutputSpec spec)
        {
            string basePath = spec.partsFolder + "/" + spec.prefix + label + spec.suffix;
            string assetPath = basePath + ".asset";
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VoxelBlueprintAsset>();
                asset.name = spec.prefix + label + spec.suffix;
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            if (cells == null || cells.Count == 0)
            {
                asset.Resize(1, 1, 1, preserveCells: false);
                asset.Clear();
                EditorUtility.SetDirty(asset);
                MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, basePath + ".vox"), asset);
                return;
            }
            Vector3Int min = cells[0], max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                min = Vector3Int.Min(min, cells[i]);
                max = Vector3Int.Max(max, cells[i]);
            }
            Vector3Int size = max - min + Vector3Int.one;
            asset.Resize(size.x, size.y, size.z, preserveCells: false);
            asset.Clear();
            bool dest = cut.IsArmMirrorDest(label);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int src = cells[i];
                Vector3Int colorAt = dest
                    ? new Vector3Int(source.sizeX - 1 - src.x, src.y, src.z)
                    : src;
                source.TryGetCell(colorAt.x, colorAt.y, colorAt.z, out VoxelBlueprintAsset.Cell cell);
                Vector3Int local = src - min;
                asset.SetCell(local.x, local.y, local.z, true, cell.color, cell.value);
            }
            EditorUtility.SetDirty(asset);
            MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, basePath + ".vox"), asset);
        }

        static void WriteSourceRecord(Dictionary<string, List<Vector3Int>> output, int total, int leftover, VoxelBlueprintPartBoxCut cut, OutputSpec spec)
        {
            var dests = new List<string>();
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                if (cut.IsArmMirrorDest(label))
                    dests.Add(label);
            }
            string mirrorLine = dests.Count == 0
                ? "- **Part mirror:** none."
                : "- **Part mirror:** " + string.Join(", ", dests) + " drawing is the mate part X-flipped through the full-body grid center. Dest-side source cells are not used.";
            var lines = new List<string>
            {
                "# Source and processing record",
                "",
                "- **Authoritative source:** `" + spec.sourceAssetName + "` (not edited).",
                "- **Processing:** enabled cells copied once by axis-aligned boxes in `" + Path.GetFileName(spec.cutAssetPath) + "`. No re-voxelize, recolour, fill, or dilation.",
                "- **Axes:** Unity Y-up; visual front +Z. A-pose facing the viewer is +Z.",
                "- **Left/right:** human anatomical. Facing the camera, `_L` is the person's left (viewer's right). See `Docs/AI/UnitVoxelBlueprintConventions.md`.",
                "- **Overlap:** Hand/Foot, then Forearm/LowerLeg, then UpperArm/UpperLeg, then Head, Hips, Torso.",
                mirrorLine,
                "- **Archive:** TuZhi `" + spec.partsFolder + "` is the master.",
                leftover > 0
                    ? "- **Integrity:** leftover `" + leftover + "` of `" + total + "` enabled source cells were outside boxes: not copied to parts, not worn, then cleared from the full-body asset so they do not appear."
                    : "- **Integrity:** 15 parts contain every one of the `" + total + "` enabled source cells exactly once.",
                "",
                "| Part | Enabled voxels |",
                "| --- | ---: |",
            };
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                lines.Add("| " + label + " | " + output[label].Count + " |");
            }
            File.WriteAllLines(Path.Combine(ProjectRoot, spec.partsFolder, "SOURCE.md"), lines);
        }

        static string CopyToVr3(OutputSpec spec)
        {
            if (!Directory.Exists(Vr3Temp))
                return "FAIL: VR3 Temp folder missing: " + Vr3Temp;
            int overwritten = CopyFolder(Vr3Temp, spec);
            if (spec.copyArmorToResources)
            {
                if (!Directory.Exists(Vr3Res))
                    return "FAIL: VR3 armor Resources folder missing: " + Vr3Res;
                overwritten += CopyFolder(Vr3Res, spec);
            }
            return "OK: overwrote " + overwritten + " part file(s) in VR3 (asset/vox only, meta unchanged) from "
                + spec.partsFolder;
        }

        static string ResolveVr3DestSuffix(OutputSpec spec)
        {
            if (spec.prefix.IndexOf("AIGeneratedHumanoid", StringComparison.Ordinal) >= 0)
                return "_V01";
            return spec.suffix;
        }

        static int CopyFolder(string dest, OutputSpec spec)
        {
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);
            string sourceRoot = Path.Combine(ProjectRoot, spec.partsFolder);
            string destSuffix = ResolveVr3DestSuffix(spec);
            int overwritten = 0;
            for (int i = 0; i < VoxelBlueprintPartBoxCut.PartCount; i++)
            {
                string label = VoxelBlueprintPartBoxCut.Labels[i];
                string sourceName = spec.prefix + label + spec.suffix;
                string destName = spec.prefix + label + destSuffix;
                if (TryCopyPartContentOverwrite(sourceRoot, dest, sourceName, destName, ".asset"))
                    overwritten++;
                if (TryCopyPartContentOverwrite(sourceRoot, dest, sourceName, destName, ".vox"))
                    overwritten++;
            }
            string md = Path.Combine(sourceRoot, "SOURCE.md");
            if (File.Exists(md))
            {
                string mdName = dest == Vr3Res
                    ? (spec.suffix.IndexOf("V06", StringComparison.Ordinal) >= 0 ? "SOURCE_V06.md" : "SOURCE_V04.md")
                    : "SOURCE.md";
                File.Copy(md, Path.Combine(dest, mdName), true);
            }
            if (spec.suffix.IndexOf("V06", StringComparison.Ordinal) >= 0)
            {
                string off = Path.Combine(sourceRoot, "AigeiFantasyRpgArmor_VoxelV06_WearOffset.asset");
                if (File.Exists(off))
                    File.Copy(off, Path.Combine(dest, "AigeiFantasyRpgArmor_VoxelV06_WearOffset.asset"), true);
            }
            return overwritten;
        }

        static bool TryCopyPartContentOverwrite(
            string sourceRoot,
            string dest,
            string sourceBaseName,
            string destBaseName,
            string ext)
        {
            string src = Path.Combine(sourceRoot, sourceBaseName + ext);
            string dst = Path.Combine(dest, destBaseName + ext);
            if (!File.Exists(src) || !File.Exists(dst))
                return false;
            File.Copy(src, dst, true);
            return true;
        }

        static bool TryPartAabb(VoxelBlueprintAsset part, out Vector3Int min, out Vector3Int max)
        {
            min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            bool any = false;
            for (int z = 0; z < part.sizeZ; z++)
            for (int y = 0; y < part.sizeY; y++)
            for (int x = 0; x < part.sizeX; x++)
            {
                if (!part.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                any = true;
                min = Vector3Int.Min(min, new Vector3Int(x, y, z));
                max = Vector3Int.Max(max, new Vector3Int(x, y, z));
            }
            return any;
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
                    || !candidate.enabled || !ColorsEqual(candidate.color, firstColor))
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
                    || !sourceCell.enabled || !ColorsEqual(sourceCell.color, cell.color))
                    return false;
            }
            return true;
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

        static bool ColorsEqual(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
