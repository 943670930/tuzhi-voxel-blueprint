from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# Camera-matched high-detail voxel relief generated from the approved concept image.
# It intentionally preserves the image palette per cell; this is a shape approval
# prototype, not yet a back-complete wearable helmet.
ROOT = Path(r"D:\Project\TuZhi")
SOURCE = Path(r"C:\Users\PaPa\.codex\generated_images\01a04d05-182a-73b1-b676-f7ac9e68309b\exec-6efcfa92-cb74-49b3-bbc3-05b9af762d8b.png")
OUT = ROOT / "Assets" / "VoxelBlueprints" / "HelmetConceptV01" / "PmUnitVoxelHelmetStyle_IceVisor_ImageMatchReliefV02.asset"
META = OUT.with_suffix(".asset.meta")
PREVIEW = OUT.with_name("PmUnitVoxelHelmetStyle_IceVisor_ImageMatchReliefV02_Preview.png")
W, D, H = 48, 8, 60

source = Image.open(SOURCE).convert("RGBA")
alpha = source.getchannel("A")
bbox = alpha.point(lambda a: 255 if a > 128 else 0).getbbox()
if not bbox: raise RuntimeError("Concept image has no opaque subject.")
crop = source.crop(bbox)
scale = min(W / crop.width, H / crop.height)
size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
small = crop.resize(size, Image.Resampling.LANCZOS)
canvas = Image.new("RGBA", (W, H), (0,0,0,0))
canvas.alpha_composite(small, ((W-size[0])//2, (H-size[1])//2))
cells = {}
for iz in range(H):
    for ix in range(W):
        r,g,b,a = canvas.getpixel((ix, H - 1 - iz))
        if a < 150: continue
        # Four cells of depth make this a real voxel volume while the front
        # layer remains colour-accurate to the approved concept art.
        for iy in range(2, 6):
            shade = 0.58 + (iy - 2) * 0.14
            cells[(ix, iy, iz)] = (round(r*shade), round(g*shade), round(b*shade), 255)

def packed(c): return c[0] | (c[1] << 8) | (c[2] << 16) | (c[3] << 24)
lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0", "  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}", "  m_Name: PmUnitVoxelHelmetStyle_IceVisor_ImageMatchReliefV02", "  m_EditorClassIdentifier: ", f"  sizeX: {W}", f"  sizeY: {D}", f"  sizeZ: {H}", "  cells:"]
for z in range(H):
    for y in range(D):
        for x in range(W):
            c = cells.get((x,y,z), (255,255,255,255)); enabled = 1 if (x,y,z) in cells else 0
            lines += [f"  - enabled: {enabled}", "    color:", "      serializedVersion: 2", f"      rgba: {packed(c)}", "    value: 4"]
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 88d7c1aafe704d15827bb75847b0a98b\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

# Exact front-face verification image made from the same cells.
preview = Image.new("RGBA", (760, 860), (25,31,42,255)); draw = ImageDraw.Draw(preview); font=ImageFont.load_default()
scale_preview = 12; ox=(760-W*scale_preview)//2; oy=70
for z in range(H):
    for x in range(W):
        cell = cells.get((x,5,z))
        if cell:
            x0=ox+x*scale_preview; y0=oy+(H-1-z)*scale_preview
            draw.rectangle((x0,y0,x0+scale_preview-1,y0+scale_preview-1), fill=cell)
draw.rectangle((ox-1,oy-1,ox+W*scale_preview,oy+H*scale_preview), outline=(100,125,155,255), width=2)
draw.text((30,25), "IMAGE-MATCHED FRONT — actual voxel colours", fill=(215,229,244,255), font=font)
draw.text((30,815), f"48 × 8 × 60 grid | {len(cells)} enabled cells | camera-matched relief", fill=(155,181,210,255), font=font)
print(f"Wrote {OUT} with {len(cells)} enabled cells")
