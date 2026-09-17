using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vr3.VoxelBlueprintAuthoring;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintFbxSurfaceImportUtil
    {
        public const string Sword01FbxPath = "Assets/3DModel/OpenGameArtFantasySword01/Sword01/Model/Sword01.fbx";
        public const string Sword01TexturePath = "Assets/3DModel/OpenGameArtFantasySword01/Sword01/Textures/Sword01_00.png";
        public const string OutputFolder = "Assets/VoxelBlueprints/Imported3DModels/OpenGameArtFantasySword01_VoxelV01";
        public const string BlueprintAssetPath = OutputFolder + "/PmWeaponVoxelStyle_OpenGameArtFantasySword01_VoxelV01.asset";
        public const string PreviewObjectName = "OpenGameArtFantasySword01_VoxelPreview";
        const int TargetCellsOnLongestAxis = 42;

        [InitializeOnLoadMethod]
        static void AutoImportOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(BlueprintAssetPath) != null)
                    return;
                if (!File.Exists(Sword01FbxPath))
                    return;
                ImportSword01Preview(false);
            };
        }

        [MenuItem("Tools/Voxel Blueprint Authoring/Import OpenGameArt Sword01 Preview")]
        public static void ImportSword01PreviewMenu() => ImportSword01Preview(true);

        [MenuItem("Tools/Voxel Blueprint Authoring/Reimport OpenGameArt Sword01 From Model (Full Handle)")]
        public static void ReimportSword01FullHandleMenu() => ImportSword01Preview(false, false);

        public static void ImportSword01PreviewBatch() => ImportSword01Preview(false, false);

        public static void ImportSword01Preview(bool logDialog, bool updateScenePreview = true)
        {
            if (!VoxelBlueprintWeaponSurfaceImportCore.ImportWeaponSurface(
                Sword01FbxPath,
                Sword01TexturePath,
                OutputFolder,
                BlueprintAssetPath,
                "Source and processing record",
                "- **Model:** OpenGameArt *Low Poly Fantasy Swords* — `Sword01` (`Sword01.fbx` + `Sword01_00.png`).\n" +
                "- **Source project copy:** `Assets/3DModel/OpenGameArtFantasySword01/Sword01/`.\n" +
                "- **Axes:** Unity Y-up; weapon rest pose uses mesh import orientation (blade along local Y, visual front +Z per TuZhi convention).\n" +
                "- **Colour:** sampled from `Sword01_00.png` UVs at surface stamps.",
                TargetCellsOnLongestAxis,
                logDialog))
                return;

            VoxelBlueprintAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBlueprintAsset>(BlueprintAssetPath);
            if (asset == null)
                return;
            if (!updateScenePreview)
                return;
            if (!TryReadPitchFromSource(OutputFolder, out float cellWorldSize))
                cellWorldSize = 0.023316f;
            PlaceOrUpdateScenePreview(asset, cellWorldSize);
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
                return float.TryParse(line.Substring(start + 1, end - start - 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pitch);
            }
            return false;
        }

        static void PlaceOrUpdateScenePreview(VoxelBlueprintAsset asset, float cellWorldSize)
        {
            VoxelBlueprintSceneDisplay display = Object.FindObjectOfType<VoxelBlueprintSceneDisplay>();
            GameObject go;
            if (display == null)
            {
                go = new GameObject(PreviewObjectName);
                display = go.AddComponent<VoxelBlueprintSceneDisplay>();
                go.transform.position = new Vector3(0f, 1.2f, 0f);
            }
            else
            {
                go = display.gameObject;
                if (go.name != PreviewObjectName)
                    go.name = PreviewObjectName;
            }

            display.blueprint = asset;
            display.cellWorldSize = cellWorldSize;
            display.Rebuild();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
