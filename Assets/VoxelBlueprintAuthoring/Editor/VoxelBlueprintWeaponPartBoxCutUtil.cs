using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintWeaponPartBoxCutUtil
    {
        const string WeaponFullPrefix = "PmWeaponVoxelStyle_";
        const string Vr3ProjectRoot = @"D:\SVNRoot\trunk\VR3";
        const string Vr3ItemBindingsFolder = "Assets/LightGunShooting/VoxelBlueprints/Weapons/_ItemBindings";

        struct OutputSpec
        {
            public string partsFolder;
            public string cutAssetPath;
            public string prefix;
            public string suffix;
            public string sourceAssetName;
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static bool IsWeaponBlueprint(VoxelBlueprintAsset asset)
        {
            if (asset == null)
                return false;
            string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (path.IndexOf("/Weapons/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return asset.name.StartsWith(WeaponFullPrefix, StringComparison.Ordinal);
        }

        static OutputSpec SpecFor(VoxelBlueprintAsset source)
        {
            if (source == null)
                throw new InvalidOperationException("missing source");

            string sourcePath = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
            string sourceDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sourceDir))
                throw new InvalidOperationException("missing source folder");

            string parentFolderName = Path.GetFileName(sourceDir);
            DerivePartNaming(source.name, out string prefix, out string suffix);
            return new OutputSpec
            {
                partsFolder = sourceDir + "/Part",
                cutAssetPath = sourceDir + "/Part/" + parentFolderName + "_WeaponPartBoxCut.asset",
                prefix = prefix,
                suffix = suffix,
                sourceAssetName = source.name,
            };
        }

        static void DerivePartNaming(string sourceAssetName, out string prefix, out string suffix)
        {
            suffix = "";
            if (sourceAssetName.StartsWith(WeaponFullPrefix, StringComparison.Ordinal))
            {
                string rest = sourceAssetName.Substring(WeaponFullPrefix.Length);
                prefix = "PmWeaponVoxelPartStyle_" + rest + "_";
                return;
            }

            prefix = "PmWeaponVoxelPart_" + sourceAssetName + "_";
        }

        static string SanitizeFileLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "Part";
            label = label.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                label = label.Replace(c, '_');
            return label;
        }

        public static VoxelBlueprintWeaponPartBoxCut LoadOrCreateCutFor(VoxelBlueprintAsset source)
        {
            if (source == null)
                return null;
            OutputSpec spec = SpecFor(source);
            VoxelBlueprintWeaponPartBoxCut cut =
                AssetDatabase.LoadAssetAtPath<VoxelBlueprintWeaponPartBoxCut>(spec.cutAssetPath);
            if (cut == null)
            {
                EnsureFolder(spec.partsFolder);
                cut = ScriptableObject.CreateInstance<VoxelBlueprintWeaponPartBoxCut>();
                cut.source = source;
                cut.EnsureDefaults();
                AssetDatabase.CreateAsset(cut, spec.cutAssetPath);
            }

            if (cut.source != source)
            {
                cut.source = source;
                EditorUtility.SetDirty(cut);
            }

            cut.EnsureDefaults();
            return cut;
        }

        public static void Persist(VoxelBlueprintWeaponPartBoxCut cut, VoxelBlueprintAsset source = null)
        {
            if (cut == null)
                return;
            if (source != null)
                cut.source = source;
            cut.EnsureDefaults();
            EditorUtility.SetDirty(cut);
            string path = AssetDatabase.GetAssetPath(cut);
            if (!string.IsNullOrEmpty(path))
            {
                EditorPrefs.SetString("TuZhi.WeaponPartBoxCut", path);
                if (cut.source != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(cut.source);
                    if (!string.IsNullOrEmpty(sourcePath))
                        EditorPrefs.SetString("TuZhi.WeaponPartBoxCut.LastSource", sourcePath);
                }
            }
        }

        public static string Slice(VoxelBlueprintWeaponPartBoxCut cut)
        {
            VoxelBlueprintAsset source = cut != null ? cut.source : null;
            if (source == null)
                return "FAIL: missing source";
            OutputSpec spec = SpecFor(source);
            cut.EnsureDefaults();
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out int enabled, out int leftover);
            EnsureFolder(spec.partsFolder);
            for (int i = 0; i < cut.PartCount; i++)
            {
                string label = cut.PartLabel(i);
                WritePart(source, label, buckets[label], spec);
            }

            ClearLeftoverCellsOnSource(cut, leftover);
            WriteSourceRecord(buckets, enabled, leftover, cut, spec);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string ok = "OK: " + enabled + " source cells, assigned by weapon boxes.";
            if (leftover > 0)
                ok += " leftover " + leftover + " cleared from source, not exported.";
            return ok;
        }

        public static string SliceAndCopyToVr3(VoxelBlueprintWeaponPartBoxCut cut)
        {
            string sliceStatus = Slice(cut);
            if (sliceStatus.StartsWith("FAIL", StringComparison.Ordinal))
                return sliceStatus;
            string copyStatus = CopyPartsToVr3(cut);
            if (copyStatus.StartsWith("FAIL", StringComparison.Ordinal))
                return sliceStatus + "\n" + copyStatus;
            return sliceStatus + " " + copyStatus;
        }

        public static string CopyPartsToVr3(VoxelBlueprintWeaponPartBoxCut cut)
        {
            if (cut == null || cut.source == null)
                return "FAIL: missing cut/source";
            string itemId = NormalizeWeaponItemId(cut.weaponItemId);
            if (string.IsNullOrEmpty(itemId))
                return "FAIL: Weapon Item Id 为空；请填写与 VR3 ItemState.ItemId 一致的 id（如 giant_sword）。";
            OutputSpec spec = SpecFor(cut.source);
            cut.EnsureDefaults();
            string tuZhiPartsAbs = Path.Combine(ProjectRoot, spec.partsFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(tuZhiPartsAbs))
                return "FAIL: Part folder missing; run slice first: " + spec.partsFolder;

            if (!TryResolveVr3PartFolders(cut.source, out string vr3BlueprintPartFolder, out string vr3MeshPartFolder, out string tuZhiMeshPartFolder))
                return "FAIL: could not resolve VR3 weapon part folders";

            int copied = 0;
            int baked = 0;
            float cellWorldSize = ReadCellWorldSize(cut.source, spec);

            for (int i = 0; i < cut.PartCount; i++)
            {
                string label = cut.PartLabel(i);
                string baseName = spec.prefix + SanitizeFileLabel(label) + spec.suffix;
                string srcAsset = Path.Combine(tuZhiPartsAbs, baseName + ".asset");
                string dstAsset = Path.Combine(
                    Vr3ProjectRoot,
                    vr3BlueprintPartFolder.Replace('/', Path.DirectorySeparatorChar),
                    baseName + ".asset");
                copied += CopyFileIfExists(srcAsset, dstAsset);
                copied += CopyFileIfExists(
                    Path.Combine(tuZhiPartsAbs, baseName + ".vox"),
                    Path.Combine(
                        Vr3ProjectRoot,
                        vr3BlueprintPartFolder.Replace('/', Path.DirectorySeparatorChar),
                        baseName + ".vox"));

                VoxelBlueprintAsset partBlueprint = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(spec.partsFolder + "/" + baseName + ".asset");
                if (partBlueprint == null)
                    continue;

                string tuZhiMeshAssetPath = tuZhiMeshPartFolder + "/" + baseName + "_Mesh.asset";
                if (!VoxelBlueprintMeshBakeUtil.TryBakeBlueprintWorldMesh(
                        partBlueprint,
                        tuZhiMeshAssetPath,
                        cellWorldSize,
                        out string bakeError))
                    return "FAIL: bake " + label + ": " + bakeError;

                baked++;
                string srcMesh = Path.Combine(
                    ProjectRoot,
                    tuZhiMeshAssetPath.Replace('/', Path.DirectorySeparatorChar));
                string dstMesh = Path.Combine(
                    Vr3ProjectRoot,
                    vr3MeshPartFolder.Replace('/', Path.DirectorySeparatorChar),
                    baseName + "_Mesh.asset");
                copied += CopyFileIfExists(srcMesh, dstMesh);
            }

            copied += CopyFileIfExists(
                Path.Combine(tuZhiPartsAbs, "SOURCE.md"),
                Path.Combine(
                    Vr3ProjectRoot,
                    vr3BlueprintPartFolder.Replace('/', Path.DirectorySeparatorChar),
                    "SOURCE.md"));

            string bindingStatus = WriteItemBindingManifest(
                cut,
                itemId,
                spec,
                vr3BlueprintPartFolder,
                vr3MeshPartFolder,
                cellWorldSize);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "OK: copied " + copied + " file(s) to VR3 (blueprint Part + baked meshes), baked " + baked
                + " part mesh(es). VR3 blueprint: `" + vr3BlueprintPartFolder + "`, mesh: `" + vr3MeshPartFolder + "`. "
                + bindingStatus;
        }

        static string NormalizeWeaponItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return "";
            return itemId.Trim();
        }

        static string SanitizeItemIdFileName(string itemId)
        {
            itemId = NormalizeWeaponItemId(itemId);
            if (string.IsNullOrEmpty(itemId))
                return "unknown_item";
            foreach (char c in Path.GetInvalidFileNameChars())
                itemId = itemId.Replace(c, '_');
            return itemId;
        }

        static string WriteItemBindingManifest(
            VoxelBlueprintWeaponPartBoxCut cut,
            string itemId,
            OutputSpec spec,
            string vr3BlueprintPartFolder,
            string vr3MeshPartFolder,
            float cellWorldSize)
        {
            VoxelBlueprintAsset source = cut.source;
            string series = source.name.StartsWith(WeaponFullPrefix, StringComparison.Ordinal)
                ? source.name.Substring(WeaponFullPrefix.Length)
                : source.name;
            Vector3Int sourceGridSize = new Vector3Int(source.sizeX, source.sizeY, source.sizeZ);
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out _, out _);
            var partLines = new List<string>(cut.PartCount);
            for (int i = 0; i < cut.PartCount; i++)
            {
                string label = cut.PartLabel(i);
                string baseName = spec.prefix + SanitizeFileLabel(label) + spec.suffix;
                string blueprintAssetPath = vr3BlueprintPartFolder + "/" + baseName + ".asset";
                string meshAssetPath = vr3MeshPartFolder + "/" + baseName + "_Mesh.asset";
                ComputePackBounds(buckets[label], out Vector3Int packMin, out Vector3Int partGridSize);
                Vector3 assemblyOffset = ComputeAssemblyLocalOffsetMeters(
                    packMin,
                    partGridSize,
                    sourceGridSize,
                    cellWorldSize);
                partLines.Add(
                    "    {\n"
                    + "      \"label\": \"" + EscapeJson(label) + "\",\n"
                    + "      \"blueprintAssetPath\": \"" + EscapeJson(blueprintAssetPath) + "\",\n"
                    + "      \"meshAssetPath\": \"" + EscapeJson(meshAssetPath) + "\",\n"
                    + "      \"sourcePackMinX\": " + packMin.x + ",\n"
                    + "      \"sourcePackMinY\": " + packMin.y + ",\n"
                    + "      \"sourcePackMinZ\": " + packMin.z + ",\n"
                    + "      \"partGridX\": " + partGridSize.x + ",\n"
                    + "      \"partGridY\": " + partGridSize.y + ",\n"
                    + "      \"partGridZ\": " + partGridSize.z + ",\n"
                    + "      \"assemblyOffsetX\": " + assemblyOffset.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n"
                    + "      \"assemblyOffsetY\": " + assemblyOffset.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n"
                    + "      \"assemblyOffsetZ\": " + assemblyOffset.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n"
                    + "    }");
            }

            string json =
                "{\n"
                + "  \"itemId\": \"" + EscapeJson(itemId) + "\",\n"
                + "  \"voxelSeries\": \"" + EscapeJson(series) + "\",\n"
                + "  \"sourceBlueprintName\": \"" + EscapeJson(spec.sourceAssetName) + "\",\n"
                + "  \"blueprintPartFolder\": \"" + EscapeJson(vr3BlueprintPartFolder) + "\",\n"
                + "  \"meshPartFolder\": \"" + EscapeJson(vr3MeshPartFolder) + "\",\n"
                + "  \"sourceGridX\": " + sourceGridSize.x + ",\n"
                + "  \"sourceGridY\": " + sourceGridSize.y + ",\n"
                + "  \"sourceGridZ\": " + sourceGridSize.z + ",\n"
                + "  \"cellWorldSize\": " + cellWorldSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n"
                + "  \"parts\": [\n"
                + string.Join(",\n", partLines)
                + "\n  ]\n"
                + "}";

            string fileName = SanitizeItemIdFileName(itemId) + ".json";
            string dst = Path.Combine(
                Vr3ProjectRoot,
                Vr3ItemBindingsFolder.Replace('/', Path.DirectorySeparatorChar),
                fileName);
            string dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(dst, json);
            return "Item binding: `" + Vr3ItemBindingsFolder + "/" + fileName + "` (itemId=" + itemId + ").";
        }

        static void ComputePackBounds(List<Vector3Int> cells, out Vector3Int packMin, out Vector3Int partGridSize)
        {
            if (cells == null || cells.Count == 0)
            {
                packMin = Vector3Int.zero;
                partGridSize = Vector3Int.one;
                return;
            }

            Vector3Int min = cells[0];
            Vector3Int max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                min = Vector3Int.Min(min, cells[i]);
                max = Vector3Int.Max(max, cells[i]);
            }

            packMin = min;
            partGridSize = max - min + Vector3Int.one;
        }

        static Vector3 ComputeAssemblyLocalOffsetMeters(
            Vector3Int sourcePackMin,
            Vector3Int partGridSize,
            Vector3Int sourceGridSize,
            float cellWorldSize)
        {
            if (cellWorldSize <= 0f)
                return Vector3.zero;

            Vector3 fullCenter = new Vector3(sourceGridSize.x, sourceGridSize.y, sourceGridSize.z) * 0.5f;
            Vector3 partCenter = new Vector3(partGridSize.x, partGridSize.y, partGridSize.z) * 0.5f;
            Vector3 offsetCells = new Vector3(sourcePackMin.x, sourcePackMin.y, sourcePackMin.z) - fullCenter + partCenter;
            return offsetCells * cellWorldSize;
        }

        static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static bool TryResolveVr3PartFolders(
            VoxelBlueprintAsset source,
            out string vr3BlueprintPartFolder,
            out string vr3MeshPartFolder,
            out string tuZhiMeshPartFolder)
        {
            vr3BlueprintPartFolder = null;
            vr3MeshPartFolder = null;
            tuZhiMeshPartFolder = null;
            if (source == null)
                return false;

            string series = source.name.StartsWith(WeaponFullPrefix, StringComparison.Ordinal)
                ? source.name.Substring(WeaponFullPrefix.Length)
                : source.name;
            vr3BlueprintPartFolder = "Assets/LightGunShooting/VoxelBlueprints/Weapons/" + series + "/Part";
            vr3MeshPartFolder = "Assets/LightGunShooting/Art/Weapons/BakedMeshes/" + series + "/Part";
            tuZhiMeshPartFolder = "Assets/BakedMeshes/Weapons/" + series + "/Part";
            return true;
        }

        static float ReadCellWorldSize(VoxelBlueprintAsset source, OutputSpec spec)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
            string sourceDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(sourceDir)
                && TryReadPitchFromSource(Path.Combine(ProjectRoot, sourceDir), out float pitch))
                return pitch;
            if (TryReadPitchFromSource(Path.Combine(ProjectRoot, spec.partsFolder), out pitch))
                return pitch;
            return 0.023316f;
        }

        static bool TryReadPitchFromSource(string folderAbs, out float pitch)
        {
            pitch = 0f;
            string path = Path.Combine(folderAbs, "SOURCE.md");
            if (!File.Exists(path))
                return false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.Contains("Pitch:") && !line.Contains("**Cell world size:**"))
                    continue;
                int start = line.IndexOf('`');
                if (start < 0)
                    continue;
                int end = line.IndexOf('`', start + 1);
                if (end <= start)
                    continue;
                if (float.TryParse(
                        line.Substring(start + 1, end - start - 1),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out pitch))
                    return pitch > 0f;
            }
            return false;
        }

        static int CopyFileIfExists(string src, string dst)
        {
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                return 0;
            string dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.Copy(src, dst, true);
            return 1;
        }

        static void ClearLeftoverCellsOnSource(VoxelBlueprintWeaponPartBoxCut cut, int leftover)
        {
            VoxelBlueprintAsset source = cut != null ? cut.source : null;
            if (source == null || leftover <= 0)
                return;
            Undo.RecordObject(source, "Clear Unboxed Weapon Cells");
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (cut.OwnerOf(x, y, z) != null)
                    continue;
                source.SetCell(x, y, z, false, cell.color, cell.value);
            }

            EditorUtility.SetDirty(source);
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
                MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, Path.ChangeExtension(assetPath, ".vox")), source);
        }

        static Dictionary<string, List<Vector3Int>> AssignCells(
            VoxelBlueprintWeaponPartBoxCut cut,
            out int enabled,
            out int leftover)
        {
            VoxelBlueprintAsset source = cut.source;
            var buckets = new Dictionary<string, List<Vector3Int>>();
            for (int i = 0; i < cut.PartCount; i++)
                buckets[cut.PartLabel(i)] = new List<Vector3Int>();
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
                owner[new Vector3Int(x, y, z)] = label;
            }

            foreach (KeyValuePair<Vector3Int, string> pair in owner)
                buckets[pair.Value].Add(pair.Key);
            for (int z = 0; z < source.sizeZ; z++)
            for (int y = 0; y < source.sizeY; y++)
            for (int x = 0; x < source.sizeX; x++)
            {
                if (!source.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (owner.ContainsKey(new Vector3Int(x, y, z)))
                    continue;
                leftover++;
            }

            return buckets;
        }

        public static void CountOwners(VoxelBlueprintWeaponPartBoxCut cut, int[] perLabel, out int leftover)
        {
            leftover = 0;
            if (cut == null || perLabel == null)
                throw new ArgumentException("cut/perLabel");
            cut.EnsureDefaults();
            if (perLabel.Length != cut.PartCount)
                throw new ArgumentException("perLabel length");
            Array.Clear(perLabel, 0, perLabel.Length);
            if (cut.source == null)
                return;
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out _, out leftover);
            for (int i = 0; i < cut.PartCount; i++)
                perLabel[i] = buckets[cut.PartLabel(i)].Count;
        }

        public static bool CountsAsSliceLeftover(VoxelBlueprintWeaponPartBoxCut cut, int x, int y, int z)
        {
            if (cut == null || cut.source == null)
                return false;
            return cut.OwnerOf(x, y, z) == null;
        }

        static void WritePart(
            VoxelBlueprintAsset source,
            string label,
            List<Vector3Int> cells,
            OutputSpec spec)
        {
            string safeLabel = SanitizeFileLabel(label);
            string basePath = spec.partsFolder + "/" + spec.prefix + safeLabel + spec.suffix;
            string assetPath = basePath + ".asset";
            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VoxelBlueprintAsset>();
                asset.name = spec.prefix + safeLabel + spec.suffix;
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

            Vector3Int min = cells[0];
            Vector3Int max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                min = Vector3Int.Min(min, cells[i]);
                max = Vector3Int.Max(max, cells[i]);
            }

            Vector3Int size = max - min + Vector3Int.one;
            asset.Resize(size.x, size.y, size.z, preserveCells: false);
            asset.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int src = cells[i];
                source.TryGetCell(src.x, src.y, src.z, out VoxelBlueprintAsset.Cell cell);
                Vector3Int local = src - min;
                asset.SetCell(local.x, local.y, local.z, true, cell.color, cell.value);
            }

            EditorUtility.SetDirty(asset);
            MagicaVoxelVoxCodec.Export(Path.Combine(ProjectRoot, basePath + ".vox"), asset);
        }

        static void WriteSourceRecord(
            Dictionary<string, List<Vector3Int>> output,
            int total,
            int leftover,
            VoxelBlueprintWeaponPartBoxCut cut,
            OutputSpec spec)
        {
            VoxelBlueprintAsset source = cut.source;
            Vector3Int sourceGridSize = source != null
                ? new Vector3Int(source.sizeX, source.sizeY, source.sizeZ)
                : Vector3Int.one;
            float cellWorldSize = ReadCellWorldSize(source, spec);
            var lines = new List<string>
            {
                "# Weapon part cut record",
                "",
                "- **Weapon Item Id:** `" + NormalizeWeaponItemId(cut.weaponItemId) + "` (VR3 ItemState.ItemId).",
                "- **Authoritative source:** `" + spec.sourceAssetName + "` (not edited except clearing unboxed cells).",
                "- **Source grid:** `" + sourceGridSize.x + " x " + sourceGridSize.y + " x " + sourceGridSize.z + "`.",
                "- **Cell world size:** `" + cellWorldSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "` m.",
                "- **Assembly:** part meshes use cropped local grids; place each part visual at `assemblyLocalOffsetMeters` under the same weapon voxel anchor as the full mesh.",
                "- **Processing:** enabled cells copied once by axis-aligned boxes in `" + Path.GetFileName(spec.cutAssetPath) + "`.",
                "- **Overlap:** earlier parts in the label list win when multiple boxes cover the same cell.",
                "- **Archive:** TuZhi `" + spec.partsFolder + "` is the master.",
                leftover > 0
                    ? "- **Integrity:** leftover `" + leftover + "` of `" + total + "` enabled source cells were outside boxes and cleared from the full weapon asset."
                    : "- **Integrity:** all `" + total + "` enabled source cells were assigned to a weapon part.",
                "",
                "| Part | Enabled voxels | sourcePackMin | assemblyLocalOffsetMeters |",
                "| --- | ---: | --- | --- |",
            };
            for (int i = 0; i < cut.PartCount; i++)
            {
                string label = cut.PartLabel(i);
                ComputePackBounds(output[label], out Vector3Int packMin, out Vector3Int partGridSize);
                Vector3 assemblyOffset = ComputeAssemblyLocalOffsetMeters(
                    packMin,
                    partGridSize,
                    sourceGridSize,
                    cellWorldSize);
                lines.Add("| " + label + " | " + output[label].Count + " | `("
                    + packMin.x + "," + packMin.y + "," + packMin.z + ")` | `("
                    + assemblyOffset.x.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ","
                    + assemblyOffset.y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ","
                    + assemblyOffset.z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ")` |");
            }

            File.WriteAllLines(Path.Combine(ProjectRoot, spec.partsFolder, "SOURCE.md"), lines);
        }

        static void EnsureFolder(string folder)
        {
            if (Directory.Exists(folder))
                return;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        public static bool TryBuildAssembledWeaponBlockMesh(
            VoxelBlueprintWeaponPartBoxCut cut,
            out Mesh mesh,
            out float cellWorldSize,
            out string error)
        {
            mesh = null;
            cellWorldSize = 0f;
            error = null;
            if (cut == null || cut.source == null)
            {
                error = "缺少武器图纸或切割配置。";
                return false;
            }

            OutputSpec spec = SpecFor(cut.source);
            cellWorldSize = ReadCellWorldSize(cut.source, spec);
            if (cellWorldSize <= 0f)
            {
                error = "Cell world size 无效。";
                return false;
            }

            cut.EnsureDefaults();
            Dictionary<string, List<Vector3Int>> buckets = AssignCells(cut, out _, out _);
            Vector3Int sourceGridSize = new Vector3Int(cut.source.sizeX, cut.source.sizeY, cut.source.sizeZ);
            var combines = new List<CombineInstance>(cut.PartCount);
            var tempMeshes = new List<Mesh>(cut.PartCount);
            for (int i = 0; i < cut.PartCount; i++)
            {
                string label = cut.PartLabel(i);
                string basePath = spec.partsFolder + "/" + spec.prefix + SanitizeFileLabel(label) + spec.suffix + ".asset";
                VoxelBlueprintAsset part = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(basePath);
                if (part == null)
                {
                    error = "找不到部件图纸: " + basePath + "；请先执行武器切割。";
                    DisposeMeshes(tempMeshes);
                    return false;
                }

                Mesh partMesh = BakeTransientBlueprintMesh(part, cellWorldSize, out int drawn);
                if (partMesh == null || drawn <= 0)
                {
                    error = "部件 `" + label + "` 没有体素。";
                    DisposeMeshes(tempMeshes);
                    return false;
                }

                ComputePackBounds(buckets[label], out Vector3Int packMin, out Vector3Int partGridSize);
                Vector3 offset = ComputeAssemblyLocalOffsetMeters(
                    packMin,
                    partGridSize,
                    sourceGridSize,
                    cellWorldSize);
                tempMeshes.Add(partMesh);
                combines.Add(new CombineInstance
                {
                    mesh = partMesh,
                    transform = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one),
                });
            }

            mesh = new Mesh
            {
                name = cut.source.name + "_BlockAssembly",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);
            VoxelBlueprintMeshWeldUtil.Weld(mesh, cellWorldSize * 0.05f);
            mesh.RecalculateBounds();
            DisposeMeshes(tempMeshes);
            return mesh.vertexCount > 0;
        }

        static Mesh BakeTransientBlueprintMesh(VoxelBlueprintAsset blueprint, float cellWorldSize, out int drawn)
        {
            drawn = 0;
            if (blueprint == null || cellWorldSize <= 0f)
                return null;
            Mesh mesh = new Mesh
            {
                name = blueprint.name + "_Bake",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            drawn = VoxelBlueprintMeshBuilder.Fill(blueprint, mesh, out _, out _);
            if (drawn <= 0)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] *= cellWorldSize;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void DisposeMeshes(List<Mesh> meshes)
        {
            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i] != null)
                    UnityEngine.Object.DestroyImmediate(meshes[i]);
            }
            meshes.Clear();
        }
    }
}
