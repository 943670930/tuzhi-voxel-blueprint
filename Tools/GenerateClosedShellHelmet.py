from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

ROOT=Path(r"D:\Project\TuZhi")
SOURCE=Path(r"C:\Users\PaPa\.codex\generated_images\01a04d05-182a-73b1-b676-f7ac9e68309b\exec-6efcfa92-cb74-49b3-bbc3-05b9af762d8b.png")
OUT=ROOT/"Assets"/"VoxelBlueprints"/"HelmetConceptV01"/"PmUnitVoxelHelmetStyle_IceVisor_ClosedShellV04.asset"
META=OUT.with_suffix(".asset.meta"); PREVIEW=OUT.with_name("PmUnitVoxelHelmetStyle_IceVisor_ClosedShellV04_Preview.png")
W,D,H=48,30,60
im=Image.open(SOURCE).convert("RGBA"); alpha=im.getchannel("A"); bbox=alpha.point(lambda a:255 if a>128 else 0).getbbox()
crop=im.crop(bbox); scale=min(W/crop.width,H/crop.height); size=(round(crop.width*scale),round(crop.height*scale)); small=crop.resize(size,Image.Resampling.LANCZOS)
front=Image.new("RGBA",(W,H),(0,0,0,0)); front.alpha_composite(small,((W-size[0])//2,(H-size[1])//2))
mask={(x,z):front.getpixel((x,H-1-z)) for z in range(H) for x in range(W) if front.getpixel((x,H-1-z))[3]>150}
xs=[x for x,z in mask]; zs=[z for x,z in mask]; cx=(min(xs)+max(xs))*0.5; cz=(min(zs)+max(zs))*0.5; rx=(max(xs)-min(xs))*0.54; rz=(max(zs)-min(zs))*0.54
cells={}; front_y={}; back_y={}
for (x,z),c in mask.items():
    ellipse=max(0.0,1.0-((x-cx)/max(1,rx))**2-((z-cz)/max(1,rz))**2)
    # Deep, rounded crown; shallower at the neck so it remains open/wearable.
    depth=3+int(10*math.sqrt(ellipse))
    if z < cz-rz*.45: depth=max(3,depth-5)
    fy=min(D-2,D//2+depth); by=max(1,D//2-depth)
    front_y[(x,z)]=fy; back_y[(x,z)]=by
    cells[(x,fy,z)]=(c[0],c[1],c[2],255)
    cells[(x,by,z)]=(65,86,119,255)

# Close only silhouette edges; interior stays hollow like a helmet shell.
for (x,z),c in mask.items():
    if any((x+dx,z+dz) not in mask for dx,dz in ((1,0),(-1,0),(0,1),(0,-1))):
        fy,by=front_y[(x,z)],back_y[(x,z)]
        for y in range(by,fy+1):
            f=0.62+0.28*(y-by)/max(1,fy-by)
            cells[(x,y,z)]=(round(c[0]*f),round(c[1]*f),round(c[2]*f),255)

def packed(c):return c[0]|(c[1]<<8)|(c[2]<<16)|(c[3]<<24)
lines=["%YAML 1.1","%TAG !u! tag:unity3d.com,2011:","--- !u!114 &11400000","MonoBehaviour:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}","  m_GameObject: {fileID: 0}","  m_Enabled: 1","  m_EditorHideFlags: 0","  m_Script: {fileID: 11500000, guid: 20a00d18131e9354a944475a25bcdc49, type: 3}","  m_Name: PmUnitVoxelHelmetStyle_IceVisor_ClosedShellV04","  m_EditorClassIdentifier: ",f"  sizeX: {W}",f"  sizeY: {D}",f"  sizeZ: {H}","  cells:"]
for z in range(H):
 for y in range(D):
  for x in range(W):
   c=cells.get((x,y,z),(255,255,255,255)); lines += [f"  - enabled: {1 if (x,y,z) in cells else 0}","    color:","      serializedVersion: 2",f"      rgba: {packed(c)}","    value: 4"]
OUT.parent.mkdir(parents=True,exist_ok=True); OUT.write_text("\n".join(lines)+"\n",encoding="utf-8")
META.write_text("fileFormatVersion: 2\nguid: 2dcf97348f6c4c3f9ecb1fd3a169f3a0\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",encoding="utf-8")

# Truthful two-view preview: original-colour front shell and depth cross-section.
out=Image.new("RGBA",(1130,860),(25,31,42,255)); d=ImageDraw.Draw(out); font=ImageFont.load_default(); step=12
d.text((40,22),"FRONT — concept colours projected onto the curved front shell",fill=(215,229,244,255),font=font)
for (x,z),y in front_y.items():
 c=cells[(x,y,z)]; px=50+x*step; py=75+(H-1-z)*step; d.rectangle((px,py,px+step-1,py+step-1),fill=c)
d.rectangle((49,74,50+W*step,75+H*step),outline=(100,125,155,255),width=2)
d.text((670,22),"SIDE — real closed volume",fill=(215,229,244,255),font=font)
for z in range(H):
 for y in range(D):
  hits=[cells[(x,y,z)] for x in range(W) if (x,y,z) in cells]
  if not hits:continue
  # nearest visible outer-side colour for section drawing
  c=hits[0]; px=680+y*step; py=75+(H-1-z)*step; d.rectangle((px,py,px+step-1,py+step-1),fill=c)
d.rectangle((679,74,680+D*step,75+H*step),outline=(100,125,155,255),width=2)
d.text((40,825),f"48 × 30 × 60 grid | {len(cells)} shell cells | closed front, sides and back — not a flat relief",fill=(155,181,210,255),font=font)
print(f"Wrote {OUT} with {len(cells)} cells")
