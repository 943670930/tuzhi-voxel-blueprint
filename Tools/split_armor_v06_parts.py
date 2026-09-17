from __future__ import annotations

import struct
import sys
import uuid
from collections import deque
from pathlib import Path

import numpy as np

sys.path.insert(0, r"D:\AI3DTools\Imports")
from build_medieval_voxel_blueprints import write_asset, write_vox

SRC_VOX = Path(
    r"D:\Project\TuZhi\Assets\VoxelBlueprints\Good\AigeiFantasyRpgArmor_VoxelV06"
    r"\PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06.vox"
)
OUT = Path(r"D:\Project\TuZhi\Assets\VoxelBlueprints\Good\AigeiFantasyRpgArmor_VoxelV06\Part")
PREFIX = "PmUnitVoxelBodyPartStyle_AigeiFantasyRpgArmor_"
SUFFIX = "_VoxelV06"
LABELS = (
    "Head", "Torso", "Hips",
    "UpperArm_L", "Forearm_L", "Hand_L",
    "UpperArm_R", "Forearm_R", "Hand_R",
    "UpperLeg_L", "LowerLeg_L", "Foot_L",
    "UpperLeg_R", "LowerLeg_R", "Foot_R",
)


def parse_vox(path: Path):
    data = path.read_bytes()
    pos = data.find(b"XYZI")
    count = struct.unpack_from("<I", data, pos + 12)[0]
    raw = np.frombuffer(data, dtype=np.uint8, offset=pos + 16, count=count * 4).reshape(count, 4)
    pal_pos = data.find(b"RGBA")
    pal = np.frombuffer(data, dtype=np.uint8, offset=pal_pos + 12, count=256 * 4).reshape(256, 4)
    cells = raw[:, :3].astype(np.int32)
    colors = np.array([tuple(pal[int(i) - 1][:3]) + (255,) for i in raw[:, 3]], dtype=np.uint8)
    return cells, colors


def components(cells):
    occ = {tuple(map(int, c)): i for i, c in enumerate(cells)}
    seen = set()
    comps = []
    nbrs = ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))
    for c in occ:
        if c in seen:
            continue
        q = deque([c])
        seen.add(c)
        idx = [occ[c]]
        while q:
            x, y, z = q.popleft()
            for dx, dy, dz in nbrs:
                n = (x + dx, y + dy, z + dz)
                if n in occ and n not in seen:
                    seen.add(n)
                    q.append(n)
                    idx.append(occ[n])
        comps.append(np.array(idx, dtype=np.int32))
    comps.sort(key=lambda a: -len(a))
    return comps


def label_cell(comp_i, x, y):
    if comp_i == 4:
        return "Head"
    if comp_i == 0:
        return "Torso"
    if comp_i == 1:
        if y >= 40:
            return "Hips"
        return "UpperLeg_L" if x < 21 else "UpperLeg_R"
    if comp_i == 2:
        if y >= 57:
            return "UpperArm_R"
        if y >= 52:
            return "Forearm_R"
        return "Hand_R"
    if comp_i == 7:
        return "UpperArm_L"
    if comp_i == 6:
        return "Forearm_L"
    if comp_i == 8:
        return "Hand_L"
    if comp_i == 5:
        return "LowerLeg_L"
    if comp_i == 3:
        return "LowerLeg_R"
    if comp_i in (9, 11):
        return "Foot_L"
    if comp_i == 10:
        return "Foot_R"
    raise RuntimeError("unmapped component %s" % comp_i)


def write_meta(path: Path, folder=False, native=False, text=False):
    guid = uuid.uuid4().hex
    if folder:
        body = (
            "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid
        )
    elif native:
        body = (
            "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 0\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % guid
        )
    elif text:
        body = (
            "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid
        )
    else:
        body = (
            "fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid
        )
    path.write_text(body, encoding="utf-8", newline="\n")


def main():
    cells, colors = parse_vox(SRC_VOX)
    comps = components(cells)
    if len(comps) != 12:
        raise RuntimeError("expected 12 plates, got %s" % len(comps))
    buckets = {name: [] for name in LABELS}
    used = set()
    for ci, idx in enumerate(comps):
        for i in idx:
            x, y, z = map(int, cells[i])
            key = (x, y, z)
            if key in used:
                raise RuntimeError("duplicate cell %s" % (key,))
            used.add(key)
            buckets[label_cell(ci, x, y)].append(i)
    if len(used) != len(cells):
        raise RuntimeError("leftover %s" % (len(cells) - len(used),))
    empty = [n for n in LABELS if not buckets[n]]
    if empty:
        raise RuntimeError("empty parts %s" % empty)
    OUT.mkdir(parents=True, exist_ok=True)
    if not (OUT.parent / "Part.meta").exists() and not (Path(str(OUT) + ".meta")).exists():
        write_meta(Path(str(OUT) + ".meta"), folder=True)
    lines = [
        "# Source and processing record",
        "",
        "- **Authoritative source:** `PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06` (not edited). V04/V05 were not edited.",
        "- **Processing:** enabled cells copied once into 15 spatial parts. No re-voxelize, recolour, fill, or dilation.",
        "- **Plates:** 12 connected components. Separate plates mapped whole. Fused right-arm plate split on Y (`Hand_R` y<52, `Forearm_R` 52-56, `UpperArm_R` y>=57). Hips plate split on Y/X (`Hips` y>=40, thighs y<40, `_L` x<21).",
        "- **Axes:** Unity Y-up; visual front +Z. Rest pose is the V06 mesh (not restamped to a new A-pose).",
        "- **Left/right:** human anatomical. `_L` is low-X. See `Docs/AI/UnitVoxelBlueprintConventions.md`.",
        "- **Part mirror:** none.",
        "- **Integrity:** 15 parts contain every one of the `%s` enabled source cells exactly once." % len(cells),
        "",
        "| Part | Enabled voxels |",
        "| --- | ---: |",
    ]
    for name in LABELS:
        idx = buckets[name]
        part_cells = cells[idx] 
        part_cols = colors[idx]
        mn = part_cells.min(axis=0)
        local = part_cells - mn
        size = local.max(axis=0) + 1
        asset = PREFIX + name + SUFFIX
        write_vox(OUT / (asset + ".vox"), size, local, part_cols)
        write_asset(OUT / (asset + ".asset"), asset, size, local, part_cols)
        write_meta(OUT / (asset + ".asset.meta"), native=True)
        write_meta(OUT / (asset + ".vox.meta"))
        lines.append("| %s | %s |" % (name, len(idx)))
        print(name, len(idx), "size", tuple(int(v) for v in size))
    (OUT / "SOURCE.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    write_meta(OUT / "SOURCE.md.meta", text=True)
    print("ok", len(cells))


if __name__ == "__main__":
    main()
