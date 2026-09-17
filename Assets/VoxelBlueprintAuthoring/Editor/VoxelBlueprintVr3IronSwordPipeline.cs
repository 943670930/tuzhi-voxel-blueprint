namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    public static class VoxelBlueprintVr3IronSwordPipeline
    {
        public static void BatchImportAndBake()
        {
            VoxelBlueprintVr3IronSwordImportUtil.ImportIronSword(false);
            VoxelBlueprintMeshBakeUtil.BakeVr3IronSword(false);
        }
    }
}
