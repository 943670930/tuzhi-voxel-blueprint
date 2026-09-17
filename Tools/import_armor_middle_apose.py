from __future__ import annotations

import math
import uuid
from pathlib import Path

import numpy as np
import trimesh
from PIL import Image

IMPORTS = Path(r"D:\AI3DTools\Imports\AigeiFantasyRpgArmor")
OBJ = IMPORTS / "model.obj"
OUT = Path(r"D:\Project\TuZhi\Assets\3DModel\AigeiFantasyRpgArmor")
TARGET_DEG = 30.0
BLEND = 8.0
STEEL = np.array([0.62, 0.64, 0.68], dtype=np.float64)


def load_middle():
    scene = trimesh.load(str(OBJ), force="scene")
    body = scene.geometry["object_1"]
    verts = np.asarray(body.vertices, dtype=np.float64)
    verts[:, 0] -= verts[:, 0].mean()
    faces = np.asarray(body.faces, dtype=np.int32)
    uv = None
    if getattr(body.visual, "uv", None) is not None:
        uv = np.asarray(body.visual.uv, dtype=np.float64)
    mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    return mesh, uv


def near_segment_xy(verts, a, b, radius):
    pts = verts[:, :2]
    ab = b - a
    denom = float(np.dot(ab, ab))
    t = np.clip(((pts - a) @ ab) / max(denom, 1e-8), 0.0, 1.0)
    proj = a + t[:, None] * ab
    return np.flatnonzero(np.linalg.norm(pts - proj, axis=1) <= radius)


def rot_xy(points, pivot, ang, weight):
    c = math.cos(ang)
    s = math.sin(ang)
    out = points.copy()
    d0 = points[:, 0] - pivot[0]
    d1 = points[:, 1] - pivot[1]
    x2 = c * d0 - s * d1 + pivot[0]
    y2 = s * d0 + c * d1 + pivot[1]
    out[:, 0] = x2 * weight + points[:, 0] * (1.0 - weight)
    out[:, 1] = y2 * weight + points[:, 1] * (1.0 - weight)
    return out


def pose_arm(verts, vi, sign, tag):
    pts = verts[vi]
    high = pts[:, 1] >= np.percentile(pts[:, 1], 88)
    shoulder = pts[high].mean(axis=0)
    low = pts[:, 1] <= np.percentile(pts[:, 1], 20)
    hand = pts[low].mean(axis=0)
    cur = hand[:2] - shoulder[:2]
    rad = math.radians(TARGET_DEG)
    tgt = np.array([sign * math.sin(rad), -math.cos(rad)], dtype=np.float64)
    ang = math.atan2(tgt[1], tgt[0]) - math.atan2(cur[1], cur[0])
    w = np.zeros(len(verts), dtype=np.float64)
    w[vi] = 1.0
    near = (shoulder[1] - verts[:, 1]) < BLEND
    w[vi] = np.where(near[vi], np.clip((shoulder[1] - verts[vi, 1]) / BLEND, 0.0, 1.0), 1.0)
    posed = rot_xy(verts, shoulder, ang, w)
    print(tag, "verts", len(vi), "apose_deg", round(math.degrees(ang), 1))
    return posed


def export_obj(path: Path, verts, faces, uv):
    mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    if uv is not None and len(uv) == len(verts):
        mesh.visual.uv = uv
    mesh.export(str(path))


def render_front(path: Path, verts, faces, width=720):
    a = verts[faces[:, 0]]
    b = verts[faces[:, 1]]
    c = verts[faces[:, 2]]
    n = np.cross(b - a, c - a)
    nlen = np.linalg.norm(n, axis=1, keepdims=True)
    n = n / np.maximum(nlen, 1e-8)
    light = np.array([0.28, 0.52, 0.80], dtype=np.float64)
    light /= np.linalg.norm(light)
    shade = 0.16 + 0.84 * np.clip(n @ light, 0.0, 1.0)
    rgb = (STEEL[None, :] * shade[:, None] * 255.0).astype(np.uint8)

    minx, maxx = float(verts[:, 0].min()), float(verts[:, 0].max())
    miny, maxy = float(verts[:, 1].min()), float(verts[:, 1].max())
    span_x = maxx - minx
    span_y = maxy - miny
    pad = 0.05 * max(span_x, span_y)
    minx -= pad
    maxx += pad
    miny -= pad
    maxy += pad
    span_x = maxx - minx
    span_y = maxy - miny
    height = max(32, int(round(width * span_y / span_x)))
    sx = (width - 1) / span_x
    sy = (height - 1) / span_y
    img = np.full((height, width, 3), 28, dtype=np.uint8)
    zbuf = np.full((height, width), -1e9, dtype=np.float32)

    def to_px(x, y):
        return (x - minx) * sx, (maxy - y) * sy

    for i in range(len(faces)):
        p0 = a[i]
        p1 = b[i]
        p2 = c[i]
        x0, y0 = to_px(p0[0], p0[1])
        x1, y1 = to_px(p1[0], p1[1])
        x2, y2 = to_px(p2[0], p2[1])
        xmin = max(0, int(math.floor(min(x0, x1, x2))))
        xmax = min(width - 1, int(math.ceil(max(x0, x1, x2))))
        ymin = max(0, int(math.floor(min(y0, y1, y2))))
        ymax = min(height - 1, int(math.ceil(max(y0, y1, y2))))
        if xmin > xmax or ymin > ymax:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-8:
            continue
        col = rgb[i]
        z0, z1, z2 = p0[2], p1[2], p2[2]
        for py in range(ymin, ymax + 1):
            for px in range(xmin, xmax + 1):
                w0 = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / den
                w1 = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / den
                w2 = 1.0 - w0 - w1
                if w0 < -1e-5 or w1 < -1e-5 or w2 < -1e-5:
                    continue
                z = w0 * z0 + w1 * z1 + w2 * z2
                if z >= zbuf[py, px]:
                    zbuf[py, px] = z
                    img[py, px] = col
    Image.fromarray(img, "RGB").save(path)


def write_folder_meta(folder: Path):
    meta = folder.parent / (folder.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def main():
    mesh, uv = load_middle()
    verts = np.asarray(mesh.vertices, dtype=np.float64)
    faces = np.asarray(mesh.faces, dtype=np.int32)
    vi_p = np.unique(
        np.concatenate(
            [
                near_segment_xy(verts, np.array([16.0, 168.0]), np.array([36.0, 136.0]), 9.0),
                near_segment_xy(verts, np.array([36.0, 136.0]), np.array([24.0, 100.0]), 9.0),
            ]
        )
    )
    vi_m = np.unique(
        np.concatenate(
            [
                near_segment_xy(verts, np.array([-16.0, 168.0]), np.array([-22.0, 136.0]), 9.0),
                near_segment_xy(verts, np.array([-22.0, 136.0]), np.array([-15.0, 104.0]), 9.0),
            ]
        )
    )
    OUT.mkdir(parents=True, exist_ok=True)
    write_folder_meta(OUT)
    orig_path = OUT / "AigeiFantasyRpgArmor_Middle_object_1.obj"
    posed_path = OUT / "AigeiFantasyRpgArmor_Middle_object_1_APose.obj"
    png_path = OUT / "AigeiFantasyRpgArmor_Middle_object_1_APose_front.png"
    export_obj(orig_path, verts, faces, uv)
    posed = pose_arm(verts, vi_p, 1.0, "plusX")
    posed = pose_arm(posed, vi_m, -1.0, "minusX")
    export_obj(posed_path, posed, faces, uv)
    render_front(png_path, posed, faces)
    (OUT / "SOURCE.md").write_text(
        "# Source and processing record\n\n"
        "- **Original:** `D:/AI3DTools/Imports/AigeiFantasyRpgArmor/model.obj` was not edited.\n"
        "- **Keep:** middle standing figure only (`object_1`, 25456 faces). `object_0` / `object_2` packed copies and weapons dropped.\n"
        "- **Pose:** isolate each arm by an X/Y box above the fauld; rotate in XY around the shoulder to A-pose (35 deg from down). Original middle extract kept as `AigeiFantasyRpgArmor_Middle_object_1.obj`.\n"
        "- **Axes:** Unity Y-up; visual front +Z. X recentered.\n"
        "- **Colour:** source Steel texture was not in the import folder; front PNG is lit gray steel.\n",
        encoding="utf-8",
    )
    print("wrote", posed_path)
    print("front", png_path)


if __name__ == "__main__":
    main()
