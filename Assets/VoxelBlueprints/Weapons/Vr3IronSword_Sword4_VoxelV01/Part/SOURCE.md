# Weapon part cut record

- **Weapon Item Id:** `sword_iron` (VR3 ItemState.ItemId).
- **Authoritative source:** `PmWeaponVoxelStyle_Vr3IronSword_Sword4_VoxelV01` (not edited except clearing unboxed cells).
- **Source grid:** `8 x 43 x 3`.
- **Cell world size:** `0.048465` m.
- **Assembly:** part meshes use cropped local grids; place each part visual at `assemblyLocalOffsetMeters` under the same weapon voxel anchor as the full mesh.
- **Processing:** enabled cells copied once by axis-aligned boxes in `Vr3IronSword_Sword4_VoxelV01_WeaponPartBoxCut.asset`.
- **Overlap:** earlier parts in the label list win when multiple boxes cover the same cell.
- **Archive:** TuZhi `Assets/VoxelBlueprints/Weapons/Vr3IronSword_Sword4_VoxelV01/Part` is the master.
- **Integrity:** all `151` enabled source cells were assigned to a weapon part.

| Part | Enabled voxels | sourcePackMin | assemblyLocalOffsetMeters |
| --- | ---: | --- | --- |
| 刃 | 94 | `(2,13,1)` | `(0.000000,0.315023,0.000000)` |
| 柄 | 57 | `(0,0,0)` | `(0.000000,-0.726975,0.000000)` |
