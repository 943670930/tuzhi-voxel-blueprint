# Weapon part cut record

- **Weapon Item Id:** `giant_sword` (VR3 ItemState.ItemId).
- **Authoritative source:** `PmWeaponVoxelStyle_OpenGameArtFantasySword01_VoxelV01` (not edited except clearing unboxed cells).
- **Source grid:** `11 x 43 x 5`.
- **Cell world size:** `0.023316` m.
- **Assembly:** part meshes use cropped local grids; place each part visual at `assemblyLocalOffsetMeters` under the same weapon voxel anchor as the full mesh.
- **Processing:** enabled cells copied once by axis-aligned boxes in `OpenGameArtFantasySword01_VoxelV01_WeaponPartBoxCut.asset`.
- **Overlap:** earlier parts in the label list win when multiple boxes cover the same cell.
- **Archive:** TuZhi `Assets/VoxelBlueprints/Imported3DModels/OpenGameArtFantasySword01_VoxelV01/Part` is the master.
- **Integrity:** all `505` enabled source cells were assigned to a weapon part.

| Part | Enabled voxels | sourcePackMin | assemblyLocalOffsetMeters |
| --- | ---: | --- | --- |
| 刃 | 374 | `(1,12,1)` | `(0.000000,0.139896,0.000000)` |
| 柄 | 131 | `(0,0,0)` | `(0.000000,-0.361398,0.000000)` |
