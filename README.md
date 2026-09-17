# TuZhi Voxel Blueprint / 图纸体素工程

Unity **2022.3.30f1c1** editor project. Converts 3D meshes into voxel (pixel-block) blueprints. This repo has **no paid plugins**.

Unity **2022.3.30f1c1** 编辑器工程。作用是把 **3D 模型转成像素化/体素图纸**。本仓库**不含付费插件**。

---

## 功能 / What it does

中文：把可读 Mesh（或 MagicaVoxel `.vox`）变成可编辑的 `VoxelBlueprintAsset` 体素网格：表面体素化、切片绘制、3D 预览、导出 `.vox`。可把 `Assets/VoxelBlueprintAuthoring/` 整夹（含 `.meta`）拷到别的 Unity 工程。

English: Turn a readable mesh (or MagicaVoxel `.vox`) into an editable `VoxelBlueprintAsset` grid: surface voxelize, slice paint, 3D preview, export `.vox`. Copy the whole `Assets/VoxelBlueprintAuthoring/` folder (including `.meta`) into another Unity project.

---

## 如何使用 / How to use

1. 用 Unity **2022.3.30f1c1** 打开本仓库（不要提交 `Library/`）。  
   Open this repo in Unity **2022.3.30f1c1** (do not commit `Library/`).
2. 菜单 **Tools > Voxel Blueprint Authoring > Open**。
3. 创建或选中一个 `VoxelBlueprintAsset`。  
   Create or select a `VoxelBlueprintAsset`.
4. 常用流程 / Typical flow:
   - 把可读 Mesh 拖进窗口做表面烘焙 → 切片微调 → 需要时 **Export .vox**  
     Drag a readable `MeshFilter` in to bake the surface → slice-edit → optionally **Export .vox**
   - 或 MagicaVoxel → **Import .vox Into Blueprint**
5. 图纸在 `Assets/VoxelBlueprints/`。约定见 `Docs/AI/UnitVoxelBlueprintConventions.md`。  
   Assets live under `Assets/VoxelBlueprints/`. Conventions: `Docs/AI/UnitVoxelBlueprintConventions.md`.
6. 编辑器细节：`Assets/VoxelBlueprintAuthoring/README.md`。

---

## 若要实现切割 / If you want cutting

本仓库里的「切割」按钮只是**图纸盒切**：把体素网格拆成部件资产，**不需要商业插件**。

The in-repo **Cut** button is **authoring box-cut** only: split a voxel grid into part assets. **No commercial plugin required.**

若要在 **Play 运行时**用刃口真正切开体素（挖格、掉块、重生成 mesh），需要商业插件：

For **runtime** blade cutting (carve cells, drop chunks, rebuild mesh) you need commercial plugins:

| 需求 / Need | 商业插件 / Paid plugin |
|---|---|
| 运行时体素体积、按格破坏与重网格 / Runtime voxel volume, per-cell destroy, remesh | **PicaVoxel**（Asset Store） |
| 切开后人体按肢体布娃娃 / Ragdoll limbs after a cut | **PuppetMaster**（RootMotion） |
| 用手抓起切下来的块（可选） / Grab cut pieces by hand (optional) | **Auto Hand** |

运行时切开走的是 PicaVoxel `Volume` + 自研刃扫（如 `HdPicaVoxelBodyPart.TryCutVolume`）。没有 PicaVoxel 就没有这套运行时切割。

Runtime cuts use PicaVoxel `Volume` plus a custom blade sweep (`HdPicaVoxelBodyPart.TryCutVolume`). Without PicaVoxel there is no that runtime cut stack.

本 Git 仓库**不会**包含上述付费插件。

This git repo **does not** ship those paid plugins.

---

## 许可注意 / License notes

不含许可未核清的第三方模型（爱给网装甲、未验证 AI 人体等）。OpenGameArt 等样本见各目录 `SOURCE.md`，使用前请核原许可。

Does not include third-party meshes with unverified redistribution rights. Check each `SOURCE.md` (e.g. OpenGameArt) before reuse.
