from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# Direct hand-authored voxel interpretation of the approved helmet concept.
ROOT = Path(r"D:\Project\TuZhi")
OUT = ROOT / "Assets" / "VoxelBlueprints" / "HelmetConceptV01" / "PmUnitVoxelHelmetStyle_IceVisorConceptV01.asset"
META = OUT.with_suffix(".asset.meta")
PREVIEW = OUT.with_name("PmUnitVoxelHelmetStyle_IceVisorConceptV01_Preview.png")
W, D, H = 16, 14, 16
ICE = (190, 235, 255, 255)       # crown and edge highlight
BLUE = (105, 145, 190, 255)      # main blue-grey shell
SLATE = (57, 79, 112, 255)       # structural slate panels
VISOR = (8, 24, 61, 255)         # deep navy visor
CYAN = (102, 220, 255, 255)      # two bright cheek recognizers
cells = {}
def box(x0, x1, y0, y1, z0, z1, color):
    for z in range(z0, z1 + 1):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): cells[(x,y,z)] = color

# Crown: a single raised block, echoing the concept rather than smoothing it.
box(6, 9, 6, 9, 13, 15, ICE)
box(4, 11, 7, 10, 11, 12, BLUE)
box(3, 12, 10, 10, 9, 10, BLUE)     # squared brow
box(3, 12, 11, 11, 10, 10, ICE)     # brow highlight

# Visor is deliberately a single dark, readable slit.
box(4, 11, 11, 11, 7, 8, VISOR)
box(3, 3, 10, 11, 7, 9, SLATE)
box(12, 12, 10, 11, 7, 9, SLATE)

# Bright cheek plates create the instantly legible "helmet" face.
box(2, 4, 10, 11, 3, 6, CYAN)
box(11, 13, 10, 11, 3, 6, CYAN)
box(2, 2, 11, 11, 3, 6, ICE)
box(13, 13, 11, 11, 3, 6, ICE)
box(5, 6, 11, 11, 3, 5, SLATE)
box(9, 10, 11, 11, 3, 5, SLATE)

# Side and rear shell: thin slabs only, leaving the head volume open.
box(2, 3, 5, 10, 7, 11, SLATE)
box(12, 13, 5, 10, 7, 11, SLATE)
box(3, 12, 3, 4, 6, 10, SLATE)
box(3, 12, 4, 4, 11, 12, BLUE)

# Open neck ring with a visible gap at the front centre.
box(3, 5, 8, 10, 1, 2, BLUE)
box(10, 12, 8, 10, 1, 2, BLUE)
box(3, 12, 4, 5, 1, 2, SLATE)
box(3, 3, 5, 10, 1, 2, ICE)
box(12, 12, 5, 10, 1, 2, ICE)

def packed(c): return c[0] | (c[1] << 8) | (c[2] << 16) | (c[3] << 24)
lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0", "  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}", "  m_Name: PmUnitVoxelHelmetStyle_IceVisorConceptV01", "  m_EditorClassIdentifier: ", f"  sizeX: {W}", f"  sizeY: {D}", f"  sizeZ: {H}", "  cells:"]
for z in range(H):
    for y in range(D):
        for x in range(W):
            c = cells.get((x,y,z), (255,255,255,255)); enabled = 1 if (x,y,z) in cells else 0
            lines += [f"  - enabled: {enabled}", "    color:", "      serializedVersion: 2", f"      rgba: {packed(c)}", "    value: 4"]
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 0f03c3f2469b4dfdaac8f5d68e9a0f82\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

# Cell-truth orthographic preview for quick review before opening Unity.
im = Image.new("RGBA", (1060, 600), (25,31,42,255)); draw = ImageDraw.Draw(im); font = ImageFont.load_default()
def elevation(origin_x, axis, label):
    draw.text((origin_x, 20), label, fill=(215,229,244,255), font=font); step=30
    for z in range(H):
        for u in range(W if axis=="front" else D):
            ray=[(u,y,z) for y in range(D)] if axis=="front" else [(x,u,z) for x in range(W)]
            hit=[p for p in ray if p in cells]
            if not hit: continue
            p=max(hit,key=lambda q:q[1]) if axis=="front" else max(hit,key=lambda q:q[0])
            x0,y0=origin_x+u*step,65+(H-1-z)*step
            draw.rectangle((x0,y0,x0+step-2,y0+step-2), fill=cells[p], outline=(17,26,39,255))
    width=(W if axis=="front" else D)*step
    draw.rectangle((origin_x-1,64,origin_x+width-1,65+H*step-1), outline=(96,119,147,255), width=2)
elevation(70,"front","FRONT — ice visor helmet")
elevation(610,"side","RIGHT SIDE — open neck ring")
draw.text((70,555), f"16 × 14 × 16 grid  |  {len(cells)} enabled cells  |  ICE / BLUE / SLATE / VISOR / CYAN palette", fill=(155,181,210,255), font=font)
print(f"Wrote {OUT} with {len(cells)} enabled cells")
