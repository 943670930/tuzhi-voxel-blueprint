from pathlib import Path
import trimesh
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"D:\Project\TuZhi")
MESH = Path(r"D:\AI3DTools\output\exec-6efcfa92-cb74-49b3-bbc3-05b9af762d8b_hunyuan_shape.glb")
OUT = ROOT / "Assets" / "VoxelBlueprints" / "HelmetConceptV01" / "PmUnitVoxelHelmetStyle_IceVisor_True3DV03.asset"
META = OUT.with_suffix(".asset.meta")
PREVIEW = OUT.with_name("PmUnitVoxelHelmetStyle_IceVisor_True3DV03_Preview.png")

mesh = trimesh.load(MESH, force="mesh")
pitch = max(mesh.extents) / 40.0
voxels = mesh.voxelized(pitch)
points = voxels.points
minp = points.min(axis=0)

# Hunyuan is Y-up. VoxelBlueprint uses Z-up, so (X, Z, Y) is retained.
raw = []
for p in points:
    x = round((p[0] - minp[0]) / pitch)
    y = round((p[2] - minp[2]) / pitch)  # depth
    z = round((p[1] - minp[1]) / pitch)  # height
    # Hunyuan inferred a separate full-size studio floor at the lowest level.
    # It is not connected to the helmet and must not become part of the armour.
    if z > 0:
        raw.append((x, y, z))
minx, miny, minz = min(x for x,y,z in raw), min(y for x,y,z in raw), min(z for x,y,z in raw)
raw = [(x-minx, y-miny, z-minz) for x,y,z in raw]
W, D, H = max(x for x,y,z in raw)+1, max(y for x,y,z in raw)+1, max(z for x,y,z in raw)+1

# Concept palette, mapped onto the generated complete 3D shell.  The details
# are volume-aware: visor and cheeks exist only on the foremost surface.
ICE=(190,235,255,255); BLUE=(105,145,190,255); SLATE=(57,79,112,255); VISOR=(8,24,61,255); CYAN=(102,220,255,255)
front_y = {}
for x,y,z in raw: front_y[(x,z)] = max(front_y.get((x,z), -1), y)
cells = {}
for x,y,z in raw:
    nx=x/max(1,W-1); nz=z/max(1,H-1); is_front=y >= front_y[(x,z)]-1
    col = BLUE if nz > .35 else SLATE
    if nz > .78 and .34 < nx < .66: col = ICE
    if nz > .70 and (nx < .22 or nx > .78): col = SLATE
    # The image concept's strong face signature is placed only on the front shell.
    if is_front and .39 < nz < .57 and .19 < nx < .81: col = VISOR
    if is_front and .18 < nz < .44 and ((.06 < nx < .25) or (.75 < nx < .94)): col = CYAN
    if nz < .18 and (.18 < nx < .82): col = SLATE
    cells[(x,y,z)] = col

def packed(c): return c[0] | (c[1]<<8) | (c[2]<<16) | (c[3]<<24)
lines=["%YAML 1.1","%TAG !u! tag:unity3d.com,2011:","--- !u!114 &11400000","MonoBehaviour:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}","  m_GameObject: {fileID: 0}","  m_Enabled: 1","  m_EditorHideFlags: 0","  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}","  m_Name: PmUnitVoxelHelmetStyle_IceVisor_True3DV03","  m_EditorClassIdentifier: ",f"  sizeX: {W}",f"  sizeY: {D}",f"  sizeZ: {H}","  cells:"]
for z in range(H):
    for y in range(D):
        for x in range(W):
            c=cells.get((x,y,z),(255,255,255,255)); enabled=1 if (x,y,z) in cells else 0
            lines += [f"  - enabled: {enabled}","    color:","      serializedVersion: 2",f"      rgba: {packed(c)}","    value: 4"]
OUT.parent.mkdir(parents=True,exist_ok=True); OUT.write_text("\n".join(lines)+"\n",encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 784e29b9cb7e4edbbb9a8731b8ee6e45\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",encoding="utf-8")

# Actual exposed-cube isometric render of the resulting 3D cells, for review.
S=13; im=Image.new("RGBA",(1100,900),(25,31,42,255)); d=ImageDraw.Draw(im); font=ImageFont.load_default(); ox,oy=540,710
def point(x,y,z): return (ox+(x-y)*S, oy-(x+y)*S//2-z*S)
def shade(c,f): return tuple(min(255,max(0,round(v*f))) for v in c[:3])+(255,)
for (x,y,z),c in sorted(cells.items(), key=lambda item: (item[0][0]+item[0][1]+item[0][2], item[0][2])):
    p000=point(x,y,z); p100=point(x+1,y,z); p010=point(x,y+1,z); p110=point(x+1,y+1,z); p001=point(x,y,z+1); p101=point(x+1,y,z+1); p011=point(x,y+1,z+1); p111=point(x+1,y+1,z+1)
    if (x,y,z+1) not in cells: d.polygon([p001,p101,p111,p011],fill=shade(c,1.12),outline=(18,26,38,255))
    if (x,y+1,z) not in cells: d.polygon([p010,p110,p111,p011],fill=shade(c,.92),outline=(18,26,38,255))
    if (x+1,y,z) not in cells: d.polygon([p100,p110,p111,p101],fill=shade(c,.72),outline=(18,26,38,255))
d.text((24,22),"TRUE 3D VOXEL HELMET — Hunyuan volume sampled to exposed shell cells",fill=(215,229,244,255),font=font)
d.text((24,865),f"{W} × {D} × {H} grid | {len(cells)} shell cells | real depth, sides and back",fill=(155,181,210,255),font=font)
print(f"Wrote {OUT} with {len(cells)} shell cells; grid {W} x {D} x {H}")
