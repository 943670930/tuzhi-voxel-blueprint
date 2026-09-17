from __future__ import annotations

import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, r"D:\Project\TuZhi\Tools")
from split_armor_v06_parts import (
    LABELS,
    OUT,
    PREFIX,
    SRC_VOX,
    SUFFIX,
    components,
    label_cell,
    parse_vox,
)

sys.path.insert(0, r"D:\AI3DTools\Imports")
from build_medieval_voxel_blueprints import write_asset, write_vox

SOURCE_GUID = "8f19baa6e5964d2ba66cca0c5611aed2"
CUT_SCRIPT_GUID = "40cda13d96d6429380f47076e8ff2b0c"
CUT_PATH = OUT / "AigeiFantasyRpgArmor_VoxelV06_PartBoxCut.asset"
MIRROR_L = {
    "UpperArm_L", "Forearm_L", "Hand_L",
    "UpperLeg_L", "LowerLeg_L", "Foot_L",
}
PAIRS = (
    ("UpperArm_L", "UpperArm_R"),
    ("Forearm_L", "Forearm_R"),
    ("Hand_L", "Hand_R"),
    ("UpperLeg_L", "UpperLeg_R"),
    ("LowerLeg_L", "LowerLeg_R"),
    ("Foot_L", "Foot_R"),
)


def assign(cells):
    comps = components(cells)
    buckets = {name: [] for name in LABELS}
    for ci, idx in enumerate(comps):
        for i in idx:
            x, y, z = map(int, cells[i])
            buckets[label_cell(ci, x, y)].append(i)
    return buckets


def write_part(name, world, cols):
    world = np.asarray(world, dtype=np.int32)
    cols = np.asarray(cols, dtype=np.uint8)
    mn = world.min(axis=0)
    local = world - mn
    size = local.max(axis=0) + 1
    asset = PREFIX + name + SUFFIX
    write_vox(OUT / (asset + ".vox"), size, local, cols)
    write_asset(OUT / (asset + ".asset"), asset, size, local, cols)


def write_cut(aabb, size_x):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % CUT_SCRIPT_GUID,
        "  m_Name: AigeiFantasyRpgArmor_VoxelV06_PartBoxCut",
        "  m_EditorClassIdentifier: ",
        "  source: {fileID: 11400000, guid: %s, type: 2}" % SOURCE_GUID,
        "  boxes:",
    ]
    for name in LABELS:
        mn, mx = aabb[name]
        size = mx - mn + 1
        center = mn.astype(np.float64) + size.astype(np.float64) * 0.5
        mirror = 1 if name in MIRROR_L else 0
        lines.append("  - label: %s" % name)
        lines.append("    min: {x: %d, y: %d, z: %d}" % (int(mn[0]), int(mn[1]), int(mn[2])))
        lines.append("    max: {x: %d, y: %d, z: %d}" % (int(mx[0]), int(mx[1]), int(mx[2])))
        lines.append("    center: {x: %s, y: %s, z: %s}" % (center[0], center[1], center[2]))
        lines.append("    size: {x: %d, y: %d, z: %d}" % (int(size[0]), int(size[1]), int(size[2])))
        lines.append("    euler: {x: 0, y: 0, z: 0}")
        lines.append("    mirror: %d" % mirror)
    lines.append("  armMirror: 0")
    lines.append("  limbViewerLrSwapDone: 1")
    for key in ("rejected", "claimed"):
        lines.append("  %s:" % key)
        for name in LABELS:
            lines.append("  - label: %s" % name)
            lines.append("    cells: []")
    CUT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main():
    cells, colors = parse_vox(SRC_VOX)
    size_x = int(cells[:, 0].max()) + 1
    buckets = assign(cells)
    aabb = {}
    for name in LABELS:
        pts = cells[buckets[name]]
        aabb[name] = (pts.min(axis=0), pts.max(axis=0))
    for dest, src in PAIRS:
        idx = buckets[src]
        world = cells[idx].copy()
        world[:, 0] = size_x - 1 - world[:, 0]
        write_part(dest, world, colors[idx])
        print("mirror", dest, "from", src, len(idx))
    write_cut(aabb, size_x)
    md = OUT / "SOURCE.md"
    text = md.read_text(encoding="utf-8")
    text = text.replace("- **Part mirror:** none.",
                        "- **Part mirror:** `_L` limbs are the `_R` drawings X-flipped through the full-body grid center. Dest-side source plates are not used.")
    md.write_text(text, encoding="utf-8")
    print("cut", CUT_PATH)


if __name__ == "__main__":
    main()
