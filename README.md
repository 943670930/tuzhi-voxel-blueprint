# TuZhi Voxel Blueprint / 图纸体素工程

Unity **2022.3.30f1c1** editor project. No paid plugins (no Auto Hand, PuppetMaster, PicaVoxel, Final IK).

无付费插件。这是体素**图纸编辑**工程，不是完整 VR 对战游戏。

---

## 功能 / What it does

中文：在 Unity 里编辑可移植的 `VoxelBlueprintAsset`（盔甲、武器等体素图纸）：切片绘制、从可读 Mesh 表面体素化、3D 预览、与 MagicaVoxel `.vox` 互导。可把 `Assets/VoxelBlueprintAuthoring/` 整夹（含 `.meta`）拷到别的 Unity 工程。

English: Edit portable `VoxelBlueprintAsset` grids in Unity (armor, weapons, etc.): slice painting, surface voxelize from a readable mesh, orbit 3D preview, MagicaVoxel `.vox` import/export. Copy the whole `Assets/VoxelBlueprintAuthoring/` folder (including `.meta`) into another Unity project.

不包含 / Not included: VR3 运行时穿戴、战斗、PicaVoxel 生成、Auto Hand、PuppetMaster。

---

## 如何使用 / How to use

1. 用 Unity **2022.3.30f1c1** 打开本仓库（不要提交 `Library/`）。  
   Open this repo in Unity **2022.3.30f1c1** (do not commit `Library/`).
2. 菜单 **Tools > Voxel Blueprint Authoring > Open**。
3. 创建或选中一个 `VoxelBlueprintAsset`。  
   Create or select a `VoxelBlueprintAsset`.
4. 常用流程 / Typical flow:
   - MagicaVoxel 画体素 → **Import .vox Into Blueprint** → 切片微调 → **Export Blueprint As .vox**
   - 或把可读 Mesh 拖进窗口做表面烘焙 / Or drag a readable `MeshFilter` into the window to bake the surface
5. 图纸资源在 `Assets/VoxelBlueprints/`。命名与轴向约定见 `Docs/AI/UnitVoxelBlueprintConventions.md`。  
   Blueprint assets live under `Assets/VoxelBlueprints/`. Naming and axes: `Docs/AI/UnitVoxelBlueprintConventions.md`.
6. 更细的编辑器说明：`Assets/VoxelBlueprintAuthoring/README.md`。  
   More editor detail: `Assets/VoxelBlueprintAuthoring/README.md`.

Python 辅助脚本在 `Tools/`（例如体素转低模预览）。不要把第三方 `.exe`（如 Instant Meshes）传进 Git。  
Python helpers are in `Tools/`. Do not commit third-party binaries such as Instant Meshes.

---

## 许可注意 / License notes

本仓库**不含**付费 Asset Store 插件，也**不含**许可未核清的第三方模型（例如爱给网装甲、未验证的 AI 人体模型、PicaVoxel 拷贝）。

This repo **does not** include paid Asset Store plugins, or third-party meshes whose redistribution rights were not verified (e.g. Aigei armor, unverified AI humanoids, PicaVoxel copies).

OpenGameArt 等已在对应 `SOURCE.md` 记录来源的样本可以参考；使用前请自己核对原许可。  
Samples that already have a `SOURCE.md` (e.g. OpenGameArt) are for reference; check the original license before reuse.
