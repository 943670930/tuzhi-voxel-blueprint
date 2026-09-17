# Voxel Blueprint Authoring

## Pixel-first workflow with MagicaVoxel

1. Design the armor directly as voxels in MagicaVoxel. Start at the same dimensions as the target blueprint whenever practical.
2. In **Tools > Voxel Blueprint Authoring**, create or select a `VoxelBlueprintAsset`.
3. Use **Import .vox Into Blueprint** to load the MagicaVoxel model, then inspect it with the integrated 3D preview and mannequin.
4. Make the last fit and palette adjustments in the slice painter. Use **Export Blueprint As .vox** to send the result back to MagicaVoxel.

The `.vox` bridge supports one voxel model, its dimensions, occupied cells, and the 256-color palette. It deliberately does not import MagicaVoxel scenes, animation, materials, or render settings, because those do not map to the game's blueprint data.

This folder is a portable Unity editor module for voxel-blueprint work. Copy the entire `VoxelBlueprintAuthoring` folder, including every `.meta` file, into the `Assets/` folder of another Unity project.

It has no VR3, PicaVoxel, scene, prefab, or third-party dependency. In Unity, open **Tools > Voxel Blueprint Authoring > Open**.

The module provides:

- `VoxelBlueprintAsset`: portable grid/cell asset data;
- a 2D X/Y/Z slice painter with Undo support;
- mesh-surface baking from readable `MeshFilter` geometry;
- an orbitable 3D voxel preview with only exposed faces.

It intentionally does not include VR3's runtime armour-wearing, PicaVoxel generation, combat, or character integration. Those systems remain in this project unchanged. A future project-specific exporter can translate a portable `VoxelBlueprintAsset` into any runtime format.
