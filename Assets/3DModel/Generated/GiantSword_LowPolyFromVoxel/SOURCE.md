# Giant sword low-poly from voxel blueprint

- **Full blueprint (reference):** `Assets/VoxelBlueprints/Weapons/OpenGameArtFantasySword01_VoxelV01/PmWeaponVoxelStyle_OpenGameArtFantasySword01_VoxelV01.asset`
- **Mesh source:** part blueprints `刃` + `柄` (handle edits live in the `柄` part asset).
- **Pitch:** `0.023316` m
- **Assembly:** each part mesh translated by `assemblyLocalOffsetMeters` from Part/SOURCE.md.
- **Block mesh:** sparse-cubes culled voxel faces (`smooth=False`, `simplify=False`).
- **Block:** 2232 tris
- **Low-poly mesh (main):** [Instant Meshes](https://github.com/wjakob/instant-meshes) field-aligned retopology on the block OBJ.
  - Target faces: ~500 (approximate; IM subdivides coarse voxel input first).
  - Batch flags: `-b` align boundaries, `-c` crease angle, quad-dominant output.
  - Vertex colors: nearest-neighbor transfer from block mesh.
- **Low-poly (Instant Meshes):** 1186 tris
- **Generator:** `Tools/voxel_blueprint_to_lowpoly_mesh.py`
- **Instant Meshes binary:** `Tools/instant-meshes/Instant Meshes.exe`
- **Not hooked to VR3 prefab.**
