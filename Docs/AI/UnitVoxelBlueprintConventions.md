# Unit Voxel Blueprint Conventions

This is the canonical rule set for Unit voxel blueprints. Codex and Cursor must
follow this document; do not create a competing convention in a generator,
scene, prefab, or MonoBehaviour.

Keep this file identical to VR3 `Docs/AI/UnitVoxelBlueprintConventions.md`.

## Coordinate system

- Unity uses Y-up.
- Visual front is **+Z** for both voxel blueprints and Unit roots.
- Canonical rest pose for new full-body work is **A-pose facing the viewer**:
  the person stands upright, chest and face toward **+Z** (that is the side
  that looks at you in a head-on +Z view / `_front.png`), arms down and
  slightly out (A, not T-pose, not a one-hand-on-hip rest). **+X** is the
  person's right. This A-pose facing you **is** the positive / front
  direction; do not treat Inspector orbit yaw or a source mesh's original
  hip-hand pose as front.
- Box-cut WASD/RF are world-locked to that A-pose: **W** +Z (front), **S** −Z,
  **D** +X (person's right), **A** −X, **R** +Y, **F** −Y. Orbit does not remap
  keys. Preview letters sit on those world directions on the view.
- Do not restamp already-approved drawings to A-pose unless the user asks.
- A body-replacement host must not add a hidden global 180-degree yaw to a
  blueprint that already follows this convention.
- Every generated asset's `SOURCE.md` must record the axis conversion, front
  direction, and rest pose (A-pose facing +Z, or an explicit exception).

## Left and right (human anatomical)

`_L` / `_R` follow a person standing in the world, not MagicaVoxel X, not
"source grid mirrored vs bones", and not the viewer's screen left.

When the figure is in that A-pose facing the camera (front **+Z** toward the
viewer):

- **`_L`** is the person's own left (viewer's right).
- **`_R`** is the person's own right (viewer's left).

Wear `UpperArm_L` on `Shoulder_L`, and the same for the other limb pairs.
On a full-body grid with front +Z and +X toward the person's right, `_L` cells
sit on the **low-X** side and `_R` on the **high-X** side.

Split, box-cut, seed, preview, and copy-to-VR3 must all use this naming. If a
preview yaw shows the face, `_L` must appear on the viewer's right.

## Source, voxels, and colours

- Use a real 3D mesh as source; preserve the original outside the Unity project.
- Mesh import and full-body authoring store **surface voxels only**. Do not bake
  solid fill into imported cell data, and do not use image relief or 2.5D geometry.
- Each `VoxelBlueprintAsset` has an **Interior Mode** (`SurfaceShell` or
  `SolidDepthFill`). This controls how stored cells are materialized into runtime
  volumes and preview meshes; it does not change the stored surface cells.
  - **Unit naked body parts** (`PmUnitVoxelBodyPartStyle_*`, excluding Armor /
    Helmet names): use **`SolidDepthFill`**.
  - **Armor, helmet, wearables, props**: use **`SurfaceShell`**.
- `SolidDepthFill` fills only the depth between the front and back stored surface
  cells in each X/Y column. It is not a license to fill marker gaps, bridge parts,
  dilate silhouettes, or change pitch.
- Choose one physical voxel pitch from the complete model before it is split.
  All parts from that model must share this pitch.
- When an approved full-body voxel blueprint exists, create its body parts by
  copying its enabled cells and RGBA values exactly once into spatial parts. Do
  not re-voxelize, recolour, dilate, or rescale those approved cells. Interior
  mode is set per part asset after the copy; it must not rewrite stored cells.
- Before delivery, verify that the union of part **stored** cells matches the
  approved whole-body cell count and colour multiset.

## Folder layout (new splits only)

Approved full-body blueprints live under `Assets/VoxelBlueprints/Good/<FullBodyName>/`.
New parts sliced from that full body go in a `Part` subfolder next to the full-body
asset, for example:

`Assets/VoxelBlueprints/Good/AIGeneratedHumanoid_VoxelV01/Part`

Do not move or rewrite older part folders (`Imported3DModels/...UnitParts...`,
`FromFullBody_V01/V02`, and so on) unless the user asks. Generators write into
`Part` only for splits started after this layout.

## Unit body mapping

Use 15 visual parts:

`Head`, `Torso`, `Hips`, `UpperArm_L`, `Forearm_L`, `Hand_L`, `UpperArm_R`,
`Forearm_R`, `Hand_R`, `UpperLeg_L`, `LowerLeg_L`, `Foot_L`, `UpperLeg_R`,
`LowerLeg_R`, `Foot_R`.

- Preserve all original Unit physics: PicaVoxel volumes, colliders, rigidbodies,
  PuppetMaster, and bead-arm simulation.
- Hide only original visual renderers under the selected Marker subtree.
- Never solve marker gaps by scaling individual parts or by filling new voxels.
- If a temporary visual-chain alignment is used, it may translate a complete
  part but must preserve its scale and its left/right placement. Record that it
  shortens the rest pose and validate it in Unity.
- When a Unit must display this visual without the replacement Host, bake the
  approved 15 generated meshes as ordinary `MeshFilter` / `MeshRenderer`
  children of the original markers. Persist the meshes and shared material,
  remove the temporary Host, and keep only the original visual renderers
  hidden; do not bake or remove physics components.
- Unit spawn, warm-pool, formation, reveal, and validation gates must treat a
  complete set of `__VoxelBlueprintBaked_*` meshes as the body display being
  ready. They must not wait for, rebuild, remount, recolour, or reactivate the
  legacy `PmVoxelFromBox` display path when that baked set exists.

## Required verification

- Inspect the full blueprint from front, back, left, and right before splitting.
- Inspect the assembled Unit in Unity in the same views.
- Confirm: front is A-pose facing the viewer along +Z, `_L`/`_R` match human
  left/right (facing the camera, person's left is the viewer's right), colours
  match the approved whole body,
  the body has no accidental global scale increase, and the parts follow the
  intended physical markers in Play Mode.
- For placement defects, record final Marker world position, rotation,
  lossyScale, local box, actual Volume bounds, and generated renderer bounds
  before changing placement logic.
