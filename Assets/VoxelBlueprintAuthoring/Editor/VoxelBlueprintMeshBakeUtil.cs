using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintMeshBakeUtil
    {
        public const string Sword01BlueprintPath = VoxelBlueprintFbxSurfaceImportUtil.BlueprintAssetPath;
        public const string Sword01BakedFolder = "Assets/BakedMeshes/Weapons/OpenGameArtFantasySword01_VoxelV01";
        public const string Sword01MeshAssetPath = Sword01BakedFolder + "/OpenGameArtFantasySword01_VoxelV01_Mesh.asset";
        public const string IronSwordBlueprintPath = "Assets/VoxelBlueprints/Imported3DModels/Vr3IronSword_Sword4_VoxelV01/PmWeaponVoxelStyle_Vr3IronSword_Sword4_VoxelV01.asset";
        public const string IronSwordBakedFolder = "Assets/BakedMeshes/Weapons/Vr3IronSword_Sword4_VoxelV01";
        public const string IronSwordMeshAssetPath = IronSwordBakedFolder + "/Vr3IronSword_Sword4_VoxelV01_Mesh.asset";

        [InitializeOnLoadMethod]
        static void AutoBakeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (AssetDatabase.LoadAssetAtPath<Mesh>(Sword01MeshAssetPath) != null)
                    return;
                if (AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(Sword01BlueprintPath) == null)
                    return;
                BakeOpenGameArtSword01(false);
            };
        }

        [MenuItem("Tools/Voxel Blueprint Authoring/Bake OpenGameArt Sword01 Mesh")]
        public static void BakeOpenGameArtSword01Menu() => BakeOpenGameArtSword01(true);

        public static void BakeOpenGameArtSword01Batch() => BakeOpenGameArtSword01(false);

        [MenuItem("Tools/Voxel Blueprint Authoring/Bake VR3 Iron Sword Mesh")]
        public static void BakeVr3IronSwordMenu() => BakeVr3IronSword(true);

        public static void BakeVr3IronSwordBatch() => BakeVr3IronSword(false);

        public static bool BakeVr3IronSword(bool logDialog) =>
            BakeWeaponMesh(
                IronSwordBlueprintPath,
                "Assets/VoxelBlueprints/Imported3DModels/Vr3IronSword_Sword4_VoxelV01",
                IronSwordMeshAssetPath,
                IronSwordBakedFolder,
                logDialog);

        public static bool BakeOpenGameArtSword01(bool logDialog) =>
            BakeWeaponMesh(
                Sword01BlueprintPath,
                VoxelBlueprintFbxSurfaceImportUtil.OutputFolder,
                Sword01MeshAssetPath,
                Sword01BakedFolder,
                logDialog);

        static bool BakeWeaponMesh(
            string blueprintPath,
            string blueprintFolder,
            string meshAssetPath,
            string bakedFolder,
            bool logDialog)
        {
            VoxelBlueprintAsset blueprint = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(blueprintPath);
            if (blueprint == null)
            {
                if (logDialog)
                    EditorUtility.DisplayDialog("Voxel Blueprint", "Blueprint asset is missing:\n" + blueprintPath, "OK");
                return false;
            }

            if (!TryReadPitchFromSource(blueprintFolder, out float cellWorldSize))
                cellWorldSize = 0.023316f;

            if (!TryBakeBlueprintWorldMesh(blueprint, meshAssetPath, cellWorldSize, out string error))
            {
                if (logDialog)
                    EditorUtility.DisplayDialog("Voxel Blueprint", error, "OK");
                else
                    Debug.LogError("[VoxelBlueprintMeshBake] " + error);
                return false;
            }

            WriteBakedSourceMarkdown(blueprint, meshAssetPath, bakedFolder, blueprintPath, cellWorldSize);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logDialog)
                EditorUtility.DisplayDialog("Voxel Blueprint", "Baked mesh saved to:\n" + meshAssetPath, "OK");
            else
                Debug.Log("[VoxelBlueprintMeshBake] Baked mesh ready: " + meshAssetPath);
            return true;
        }

        public static bool TryBakeBlueprintWorldMesh(VoxelBlueprintAsset blueprint, string meshAssetPath, float cellWorldSize, out string error)
        {
            error = null;
            if (blueprint == null)
            {
                error = "Blueprint is null.";
                return false;
            }
            if (cellWorldSize <= 0f)
            {
                error = "Cell world size must be positive.";
                return false;
            }

            Mesh baked = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(meshAssetPath),
                indexFormat = IndexFormat.UInt32
            };
            int drawn = VoxelBlueprintMeshBuilder.Fill(blueprint, baked, out _, out _);
            if (drawn <= 0)
            {
                error = "Blueprint has no materialized voxels.";
                Object.DestroyImmediate(baked);
                return false;
            }

            Vector3[] vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] *= cellWorldSize;
            baked.vertices = vertices;
            baked.RecalculateBounds();

            string folder = Path.GetDirectoryName(meshAssetPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, meshAssetPath);
            }
            else
            {
                existing.Clear(false);
                EditorUtility.CopySerialized(baked, existing);
                existing.name = baked.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(baked);
            }

            return true;
        }

        static bool TryReadPitchFromSource(string blueprintFolder, out float pitch)
        {
            pitch = 0f;
            string path = Path.Combine(blueprintFolder, "SOURCE.md");
            if (!File.Exists(path))
                return false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.Contains("**Pitch:**"))
                    continue;
                int start = line.IndexOf('`');
                if (start < 0)
                    continue;
                int end = line.IndexOf('`', start + 1);
                if (end <= start)
                    continue;
                return float.TryParse(line.Substring(start + 1, end - start - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out pitch);
            }
            return false;
        }

        static void WriteBakedSourceMarkdown(
            VoxelBlueprintAsset blueprint,
            string meshAssetPath,
            string bakedFolder,
            string blueprintPath,
            float cellWorldSize)
        {
            Bounds bounds = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath).bounds;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Baked weapon mesh");
            sb.AppendLine();
            sb.AppendLine("- **Blueprint:** `" + blueprintPath + "`");
            sb.AppendLine("- **Baked mesh:** `" + meshAssetPath + "`");
            sb.AppendLine("- **Cell world size:** `" + cellWorldSize.ToString("F6", CultureInfo.InvariantCulture) + "` m");
            sb.AppendLine("- **Grid:** `" + blueprint.sizeX + " x " + blueprint.sizeY + " x " + blueprint.sizeZ + "`");
            sb.AppendLine("- **Mesh bounds (m):** `" + bounds.size.x.ToString("F3", CultureInfo.InvariantCulture) + " x " + bounds.size.y.ToString("F3", CultureInfo.InvariantCulture) + " x " + bounds.size.z.ToString("F3", CultureInfo.InvariantCulture) + "`");
            sb.AppendLine("- **Colour:** vertex colors from blueprint cells.");
            sb.AppendLine("- **Usage:** assign mesh + `VR3/Voxel Cube Unlit Stereo` material on weapon visual MeshFilter/MeshRenderer.");
            File.WriteAllText(Path.Combine(bakedFolder, "SOURCE.md"), sb.ToString(), Encoding.UTF8);
        }
    }
}
