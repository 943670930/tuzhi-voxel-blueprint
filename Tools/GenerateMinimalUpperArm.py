from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# Deliberately sparse, pixel-first sleeve: silhouette and plate rhythm first;
# never fill the volume merely because a grid cell is available.
ROOT = Path(r"D:\Project\TuZhi")
OUT = ROOT / "Assets" / "VoxelBlueprints" / "PixelFirst" / "PmUnitBeadArmStyle_UpperArm_MinimalV02.asset"
META = OUT.with_suffix(".asset.meta")
PREVIEW = OUT.with_name("PmUnitBeadArmStyle_UpperArm_MinimalV02_Preview.png")
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

# Three detached front lamellae: the essential readable armour language.
for z0, z1, col in [(5, 7, MAIN), (9, 11, MID), (13, 15, MAIN)]:
    box(4, 11, 11, 11, z0, z1, col)
    box(4, 4, 11, 11, z0, z1, LIGHT)
    box(11, 11, 11, 11, z0, z1, LIGHT)
    box(4, 11, 11, 11, z1, z1, LIGHT)

# Pair of narrow side rails establishes depth without solidifying the sleeve.
box(2, 2, 7, 11, 4, 16, MID)
box(13, 13, 7, 11, 4, 16, MID)
box(2, 2, 11, 11, 4, 16, LIGHT)
box(13, 13, 11, 11, 4, 16, LIGHT)

# Minimal dark back spine — undersuit, not another armour wall.
box(6, 9, 2, 2, 5, 16, DARK)

# Open square collar: four thin rails, clearly communicating "sleeve".
box(3, 12, 10, 10, 18, 18, LIGHT)
box(3, 3, 4, 10, 18, 19, MAIN)
box(12, 12, 4, 10, 18, 19, MAIN)
box(3, 12, 4, 4, 19, 19, MID)

# Bottom cuff: two thin brackets, intentionally open at the centre/back.
box(3, 12, 10, 10, 2, 2, LIGHT)
box(3, 3, 7, 10, 2, 3, MAIN)
box(12, 12, 7, 10, 2, 3, MAIN)
box(5, 10, 3, 3, 2, 2, DARK)

def packed(c): return c[0] | (c[1] << 8) | (c[2] << 16) | (c[3] << 24)
lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0", "  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}", "  m_Name: PmUnitBeadArmStyle_UpperArm_MinimalV02", "  m_EditorClassIdentifier: ", f"  sizeX: {W}", f"  sizeY: {D}", f"  sizeZ: {H}", "  cells:"]
for z in range(H):
    for y in range(D):
        for x in range(W):
            c = cells.get((x, y, z), (255, 255, 255, 255)); enabled = 1 if (x, y, z) in cells else 0
            lines += [f"  - enabled: {enabled}", "    color:", "      serializedVersion: 2", f"      rgba: {packed(c)}", "    value: 4"]
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 61b9d0e042b54f79bc1ab5d5a3e7d1d4\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

# Simple cell-truth preview: front and side elevations, no smooth rendering.
im = Image.new("RGBA", (1060, 760), (25, 31, 42, 255)); draw = ImageDraw.Draw(im); font = ImageFont.load_default()
def elevation(origin_x, axis, label):
    draw.text((origin_x, 22), label, fill=(215,229,244,255), font=font); step = 28
    for z in range(H):
        for u in range(W if axis == "front" else D):
            ray = [(u,y,z) for y in range(D)] if axis == "front" else [(x,u,z) for x in range(W)]
            hit = [p for p in ray if p in cells]
            if not hit: continue
            p = max(hit, key=lambda q:q[1]) if axis == "front" else max(hit, key=lambda q:q[0])
            x0, y0 = origin_x + u*step, 75+(H-1-z)*step
            draw.rectangle((x0,y0,x0+step-2,y0+step-2), fill=cells[p], outline=(17,26,39,255))
    width = (W if axis == "front" else D)*step
    draw.rectangle((origin_x-1,74,origin_x+width-1,75+H*step-1), outline=(96,119,147,255), width=2)
elevation(70,"front","FRONT — three armour plates")
elevation(610,"side","RIGHT SIDE — open sleeve")
draw.text((70,710), f"16 × 14 × 22 grid  |  {len(cells)} solid cells  |  hollow minimal upper-arm armour", fill=(155,181,210,255), font=font)
print(f"Wrote {OUT} with {len(cells)} cells")
