using UnityEditor;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintOpenGameArtSword01HandlePipeline
    {
        [MenuItem("Tools/Voxel Blueprint Authoring/Reimport OpenGameArt Sword01 And Thin Handle")]
        public static void ReimportAndThinHandleMenu()
        {
            ReimportAndThinHandleBatch();
            EditorUtility.DisplayDialog("Voxel Blueprint", "OpenGameArt Sword01 reimported and handle thinned.", "OK");
        }

        public static void ReimportAndThinHandleBatch()
        {
            VoxelBlueprintFbxSurfaceImportUtil.ImportSword01Preview(false, false);
            VoxelBlueprintOpenGameArtSword01HandleThinUtil.ThinHandle(false);
        }
    }
}
