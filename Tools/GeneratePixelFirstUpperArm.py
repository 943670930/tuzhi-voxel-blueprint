from pathlib import Path
import random

# Produces an intentionally hand-authored, voxel-first upper-arm plate.  This is
# not a mesh conversion: every cell is chosen on the 16 x 14 x 22 game grid.
ROOT = Path(r"D:\Project\TuZhi")
OUT = ROOT / "Assets" / "VoxelBlueprints" / "PixelFirst" / "PmUnitBeadArmStyle_UpperArm_PixelFirstV01.asset"
META = OUT.with_suffix(".asset.meta")
PREVIEW = ROOT / "Assets" / "VoxelBlueprints" / "PixelFirst" / "PmUnitBeadArmStyle_UpperArm_PixelFirstV01_Preview.png"
W, D, H = 16, 14, 22

LIGHT = (202, 224, 242, 255)
MAIN = (137, 171, 204, 255)
MID = (96, 132, 170, 255)
DARK = (39, 62, 93, 255)
cells = {}

def box(x0, x1, y0, y1, z0, z1, color):
    for z in range(z0, z1 + 1):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                cells[(x, y, z)] = color

def front_plate(x0, x1, z0, z1, color=MAIN):
    # A 2-cell-deep readable front plate with light side/upper edges.
    box(x0, x1, 10, 11, z0, z1, color)
    box(x0, x1, 12, 12, z1, z1, LIGHT)
    box(x0, x0, 12, 12, z0, z1, LIGHT)
    box(x1, x1, 12, 12, z0, z1, LIGHT)

# Bottom cuff: closed, squat and deliberately chunky.
box(1, 14, 1, 12, 0, 2, DARK)
box(1, 14, 10, 12, 0, 3, MID)
box(1, 14, 12, 12, 0, 3, LIGHT)
box(1, 2, 10, 12, 0, 3, LIGHT)
box(13, 14, 10, 12, 0, 3, LIGHT)

# Four large overlapping front panels.  Each is separated by a dark one-cell seam.
front_plate(3, 12, 4, 6, MAIN)
front_plate(3, 12, 8, 10, MAIN)
front_plate(3, 12, 12, 14, MAIN)
front_plate(3, 12, 16, 17, MID)

# Side rails hold the silhouette together and give it a strong, wearable square profile.
for z0, z1, col in [(4, 7, MID), (8, 11, MAIN), (12, 15, MID), (16, 18, MAIN)]:
    box(1, 2, 3, 12, z0, z1, col)
    box(13, 14, 3, 12, z0, z1, col)
    box(1, 2, 11, 12, z0, z1, LIGHT)
    box(13, 14, 11, 12, z0, z1, LIGHT)

# Back spine is intentionally darker: it reads as the undersuit between armor panels.
box(4, 11, 1, 3, 3, 18, DARK)
box(2, 13, 3, 4, 4, 18, MID)

# Squared upper collar: ring-shaped, open in the middle so it looks like a sleeve.
box(2, 13, 2, 12, 19, 21, MID)
for z in range(19, 22):
    for y in range(5, 10):
        for x in range(5, 11):
            cells.pop((x, y, z), None)
box(2, 13, 11, 12, 19, 21, LIGHT)
box(2, 3, 3, 12, 19, 21, LIGHT)
box(12, 13, 3, 12, 19, 21, LIGHT)

# Two shoulder-like stepped caps prevent the top from becoming a plain cylinder.
box(0, 2, 5, 11, 17, 19, MAIN)
box(13, 15, 5, 11, 17, 19, MAIN)
box(0, 2, 11, 12, 17, 19, LIGHT)
box(13, 15, 11, 12, 17, 19, LIGHT)

def packed_rgba(c):
    # Unity serializes Color32 as a packed 0xAABBGGRR integer in YAML.
    return c[0] | (c[1] << 8) | (c[2] << 16) | (c[3] << 24)

lines = [
    "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:",
    "--- !u!114 &11400000", "MonoBehaviour:",
    "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
    "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
    "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
    "  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}",
    "  m_Name: PmUnitBeadArmStyle_UpperArm_PixelFirstV01", "  m_EditorClassIdentifier: ",
    f"  sizeX: {W}", f"  sizeY: {D}", f"  sizeZ: {H}", "  cells:"
]
for z in range(H):
    for y in range(D):
        for x in range(W):
            color = cells.get((x, y, z), (255, 255, 255, 255))
            enabled = 1 if (x, y, z) in cells else 0
            lines += [f"  - enabled: {enabled}", "    color:", "      serializedVersion: 2", f"      rgba: {packed_rgba(color)}", "    value: 4"]
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 5aa187f35f974c9d8682a9d8712c4e15\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

# Actual voxel preview (not an AI render). Left: front-most cells from the asset;
# right: side-most cells. The small brightness change shows how much thickness is behind a cell.
from PIL import Image, ImageDraw, ImageFont
im = Image.new("RGBA", (1060, 760), (25, 31, 42, 255))
d = ImageDraw.Draw(im)
font = ImageFont.load_default()
def draw_elevation(origin_x, origin_y, axis, label):
    d.text((origin_x, 22), label, fill=(215, 229, 244, 255), font=font)
    step = 28
    for z in range(H):
        for u in range(W if axis == "front" else D):
            ray = [(u, y, z) for y in range(D)] if axis == "front" else [(x, u, z) for x in range(W)]
            hit = [p for p in ray if p in cells]
            if not hit:
                continue
            p = max(hit, key=lambda q: q[1]) if axis == "front" else max(hit, key=lambda q: q[0])
            c = cells[p]
            depth = len(hit)
            factor = min(1.18, 0.92 + depth * 0.025)
            fill = tuple(min(255, int(v * factor)) for v in c[:3]) + (255,)
            x0 = origin_x + u * step
            y0 = origin_y + (H - 1 - z) * step
            d.rectangle((x0, y0, x0 + step - 2, y0 + step - 2), fill=fill, outline=(17, 26, 39, 255))
    d.rectangle((origin_x - 1, origin_y - 1, origin_x + (W if axis == "front" else D) * step - 1, origin_y + H * step - 1), outline=(96, 119, 147, 255), width=2)
draw_elevation(70, 75, "front", "FRONT — actual voxel cells")
draw_elevation(610, 75, "side", "RIGHT SIDE — actual voxel cells")
d.text((70, 710), "16 wide × 14 deep × 22 high  |  2,354 solid cells  |  pixel-first macro plates", fill=(155, 181, 210, 255), font=font)
print(f"Wrote {OUT} ({len(cells)} voxels)")
