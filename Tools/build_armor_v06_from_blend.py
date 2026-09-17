from __future__ import annotations

import struct
import sys
from pathlib import Path

import numpy as np
import trimesh

sys.path.insert(0, r"D:\AI3DTools\Imports")
from build_medieval_voxel_blueprints import preview, write_asset, write_vox

sys.path.insert(0, r"D:\AI3DTools\Imports\AigeiFantasyRpgArmor")
from build_aigei_fantasy_rpg_armor_fullbody_v02 import nearest_vertex, ortho, quantize

MESH = Path(
    r"D:\Project\TuZhi\Assets\3DModel\AigeiFantasyRpgArmor\AigeiFantasyRpgArmor_Middle_object_1_edited.obj"
)
OUT = Path(r"D:\Project\TuZhi\Assets\VoxelBlueprints\Good\AigeiFantasyRpgArmor_VoxelV06")
NAME = "PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV06"
HEAD_WIDTH_CELLS = 8
V02_VOX = Path(
    r"D:\Project\TuZhi\Assets\VoxelBlueprints\Imported3DModels\AigeiFantasyRpgArmor_VoxelV02"
    r"\PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV02.vox"
)
ORIG_OBJ = Path(r"D:\AI3DTools\Imports\AigeiFantasyRpgArmor\model.obj")
V02_PITCH = 1.032029


def parse_vox(path: Path):
    data = path.read_bytes()
    pos = data.find(b"XYZI")
    count = struct.unpack_from("<I", data, pos + 12)[0]
    raw = np.frombuffer(data, dtype=np.uint8, offset=pos + 16, count=count * 4).reshape(count, 4)
    pal_pos = data.find(b"RGBA")
    pal = np.frombuffer(data, dtype=np.uint8, offset=pal_pos + 12, count=256 * 4).reshape(256, 4)
    cells = raw[:, :3].astype(np.int32)
    colors = np.array([pal[int(i) - 1][:3] for i in raw[:, 3]], dtype=np.uint8)
    return cells, colors


def vert_colors_from_v02(orig_mesh):
    cells, pal = parse_vox(V02_VOX)
    points = orig_mesh.voxelized(V02_PITCH).points
    origin = points.min(axis=0) - cells.min(axis=0).astype(np.float64) * V02_PITCH
    world = origin + cells.astype(np.float64) * V02_PITCH
    idx = nearest_vertex(world, orig_mesh.vertices, V02_PITCH)
    return pal[idx]


def head_width(verts):
    ymin, ymax = float(verts[:, 1].min()), float(verts[:, 1].max())
    yn = (verts[:, 1] - ymin) / max(1e-6, ymax - ymin)
    band = verts[yn > 0.82]
    if len(band) < 8:
        band = verts[yn > 0.75]
    return float(band[:, 0].max() - band[:, 0].min())


def main():
    orig_geom = trimesh.load(str(ORIG_OBJ), force="scene").geometry["object_1"]
    orig_verts = np.asarray(orig_geom.vertices, dtype=np.float64)
    orig_verts[:, 0] -= orig_verts[:, 0].mean()
    orig_mesh = trimesh.Trimesh(vertices=orig_verts, faces=np.asarray(orig_geom.faces), process=False)
    vert_rgb = vert_colors_from_v02(orig_mesh)
    raw = trimesh.load(str(MESH), force="mesh", process=False)
    src = np.asarray(raw.vertices, dtype=np.float64)
    if len(src) != len(vert_rgb):
        raise RuntimeError("edited vert count %s != original %s" % (len(src), len(vert_rgb)))
    verts = np.empty_like(src)
    verts[:, 0] = src[:, 0]
    verts[:, 1] = src[:, 2]
    verts[:, 2] = src[:, 1]
    verts[:, 0] -= verts[:, 0].mean()
    mesh = trimesh.Trimesh(vertices=verts, faces=np.asarray(raw.faces), process=False)
    hw = head_width(mesh.vertices)
    pitch = hw / float(HEAD_WIDTH_CELLS)
    points = mesh.voxelized(pitch).points
    lower = points.min(axis=0)
    cells = np.rint((points - lower) / pitch).astype(np.int32)
    size = cells.max(axis=0) + 1
    if max(int(v) for v in size) > 255:
        raise RuntimeError("grid exceeds 255: %s" % (tuple(map(int, size)),))
    nearest = nearest_vertex(mesh.vertices, points, pitch)
    colors = quantize(vert_rgb[nearest])
    ymin, ymax = float(cells[:, 1].min()), float(cells[:, 1].max())
    yn = (cells[:, 1] - ymin) / max(1.0, ymax - ymin)
    head = cells[yn > 0.82]
    head_aabb = (
        int(head[:, 0].max() - head[:, 0].min() + 1),
        int(head[:, 1].max() - head[:, 1].min() + 1),
        int(head[:, 2].max() - head[:, 2].min() + 1),
    )
    OUT.mkdir(parents=True, exist_ok=True)
    write_vox(OUT / f"{NAME}.vox", size, cells, colors)
    write_asset(OUT / f"{NAME}.asset", NAME, size, cells, colors)
    preview(OUT / f"{NAME}_iso.png", NAME, size, cells, colors)
    ortho(OUT / f"{NAME}_front.png", size, cells, colors, "front", "front +Z")
    md = (
        "# Source and processing record\n\n"
        "- **Model:** TuZhi `Assets/3DModel/AigeiFantasyRpgArmor/AigeiFantasyRpgArmor_Middle_object_1.blend` exported as `AigeiFantasyRpgArmor_Middle_object_1_edited.obj`. V04/V05 were not edited.\n"
        "- **Keep:** middle armor figure from that blend.\n"
        "- **Colour:** Steel jpg is missing. Vertex colours copied from V02 voxels onto original `object_1` vertex indices, then carried to the edited mesh (same 23449 verts). Moved plates keep rest-pose paint.\n"
        "- **Axes:** Unity Y-up; visual front +Z. X recentered.\n"
        "- **Density:** flesh Head AABB is `7 x 10 x 9` (width 7, height 10, depth 9). Target helmet width 8.\n"
        f"- **Voxel processing:** surface voxelize only, no fill, no split. Pitch `{pitch:.6f}`. Enabled `{len(cells):,}`; grid `{int(size[0])} x {int(size[1])} x {int(size[2])}`. Helmet band AABB about `{head_aabb[0]} x {head_aabb[1]} x {head_aabb[2]}`.\n"
        "- **Left/right:** human anatomical. See `Docs/AI/UnitVoxelBlueprintConventions.md`.\n"
    )
    (OUT / "SOURCE.md").write_text(md, encoding="utf-8")
    print("pitch", pitch, "enabled", len(cells), "size", tuple(int(v) for v in size), "head_aabb", head_aabb)
    print("unique colors", len(set(colors)))


if __name__ == "__main__":
    main()
