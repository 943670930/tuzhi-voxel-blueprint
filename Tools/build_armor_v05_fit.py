import collections
import re
import struct
import uuid
from pathlib import Path

SRC = Path(
    r"D:\Project\TuZhi\Assets\VoxelBlueprints\Good\AigeiFantasyRpgArmor_VoxelV04"
    r"\PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV04.asset"
)
OUT_DIR = Path(r"D:\Project\TuZhi\Assets\VoxelBlueprints\Good\AigeiFantasyRpgArmor_VoxelV05")
NAME = "PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV05"
SCRIPT_GUID = "20a00d18131e9354a944475a25bcdc49"


def guid():
    return uuid.uuid4().hex


def write_meta(path: Path, g: str, folder=False, native=False):
    if folder:
        txt = (
            "fileFormatVersion: 2\n"
            f"guid: {g}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
    elif native:
        txt = (
            "fileFormatVersion: 2\n"
            f"guid: {g}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 0\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
    else:
        txt = (
            "fileFormatVersion: 2\n"
            f"guid: {g}\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
    path.write_text(txt, encoding="utf-8")


def chunk(cid: bytes, body: bytes, children: bytes = b"") -> bytes:
    return cid + struct.pack("<ii", len(body), len(children)) + body + children


def main():
    text = SRC.read_text(encoding="utf-8")
    sx = int(re.search(r"sizeX: (\d+)", text).group(1))
    sy = int(re.search(r"sizeY: (\d+)", text).group(1))
    sz = int(re.search(r"sizeZ: (\d+)", text).group(1))
    flags = [int(x) for x in re.findall(r"- enabled: ([01])", text)]
    colors = [int(x) for x in re.findall(r"rgba: (\d+)", text)]
    n = sx * sy * sz
    if len(flags) != n or len(colors) != n:
        raise SystemExit("cell count mismatch")

    nx, ny, nz = round(sx * 8 / 13), round(sy * 8 / 13), round(sz * 8 / 13)
    buckets = [collections.Counter() for _ in range(nx * ny * nz)]

    def sidx(x, y, z):
        return x + sx * (y + sy * z)

    def didx(x, y, z):
        return x + nx * (y + ny * z)

    for z in range(sz):
        for y in range(sy):
            for x in range(sx):
                i = sidx(x, y, z)
                if not flags[i]:
                    continue
                dx = min(nx - 1, x * nx // sx)
                dy = min(ny - 1, y * ny // sy)
                dz = min(nz - 1, z * nz // sz)
                buckets[didx(dx, dy, dz)][colors[i]] += 1

    out_flags = [0] * (nx * ny * nz)
    out_colors = [4294967295] * (nx * ny * nz)
    enabled = 0
    for i, ctr in enumerate(buckets):
        if not ctr:
            continue
        out_flags[i] = 1
        out_colors[i] = ctr.most_common(1)[0][0]
        enabled += 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    header = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: {NAME}\n"
        "  m_EditorClassIdentifier: \n"
        f"  sizeX: {nx}\n"
        f"  sizeY: {ny}\n"
        f"  sizeZ: {nz}\n"
        "  cells:\n"
    )
    chunks = [header]
    for i in range(nx * ny * nz):
        chunks.append(
            f"  - enabled: {out_flags[i]}\n"
            "    color:\n"
            "      serializedVersion: 2\n"
            f"      rgba: {out_colors[i]}\n"
            "    value: 4\n"
        )
    asset_path = OUT_DIR / (NAME + ".asset")
    asset_path.write_text("".join(chunks), encoding="utf-8")

    palette = []
    index = {}
    xyzi = []
    for z in range(nz):
        for y in range(ny):
            for x in range(nx):
                i = didx(x, y, z)
                if not out_flags[i]:
                    continue
                rgba = out_colors[i]
                if rgba not in index:
                    if len(palette) >= 255:
                        r, g, b = rgba & 255, (rgba >> 8) & 255, (rgba >> 16) & 255
                        best, bd = 1, 10**9
                        for pi, prgba in enumerate(palette):
                            pr, pg, pb = prgba & 255, (prgba >> 8) & 255, (prgba >> 16) & 255
                            d = (pr - r) ** 2 + (pg - g) ** 2 + (pb - b) ** 2
                            if d < bd:
                                bd, best = d, pi + 1
                        index[rgba] = best
                    else:
                        palette.append(rgba)
                        index[rgba] = len(palette)
                xyzi.append((x, y, z, index[rgba]))

    xyzi_body = struct.pack("<i", len(xyzi)) + b"".join(
        struct.pack("BBBB", x, y, z, ci) for x, y, z, ci in xyzi
    )
    size_body = struct.pack("<iii", nx, ny, nz)
    rgba_body = bytearray()
    for i in range(256):
        if i < len(palette):
            u = palette[i]
            rgba_body += bytes([u & 255, (u >> 8) & 255, (u >> 16) & 255, (u >> 24) & 255])
        else:
            rgba_body += bytes([0, 0, 0, 255])
    children = (
        chunk(b"SIZE", size_body)
        + chunk(b"XYZI", xyzi_body)
        + chunk(b"RGBA", bytes(rgba_body))
    )
    vox = b"VOX " + struct.pack("<i", 150) + chunk(b"MAIN", b"", children)
    vox_path = OUT_DIR / (NAME + ".vox")
    vox_path.write_bytes(vox)

    md = (
        "# Source and processing record\n\n"
        "- **Authoritative source:** `PmUnitVoxelFullBodyStyle_AigeiFantasyRpgArmor_VoxelV04` (not edited).\n"
        "- **Purpose:** shrink armor full-body so head width is about 8 cells "
        "(body head is 7; slightly larger, not a full extra ring).\n"
        f"- **Scale:** linear 8/13. Grid {sx} x {sy} x {sz} -> {nx} x {ny} x {nz}.\n"
        "- **Method:** occupied-cell aggregation; each destination cell keeps the most frequent source RGBA.\n"
        "- **Axes:** Unity Y-up; visual front +Z.\n"
        "- **Left/right:** human anatomical. See `Docs/AI/UnitVoxelBlueprintConventions.md`.\n"
        f"- **Integrity:** source enabled 7500; destination enabled {enabled}.\n"
    )
    md_path = OUT_DIR / "SOURCE.md"
    md_path.write_text(md, encoding="utf-8")

    write_meta(Path(str(OUT_DIR) + ".meta"), guid(), folder=True)
    write_meta(Path(str(asset_path) + ".meta"), guid(), native=True)
    write_meta(Path(str(vox_path) + ".meta"), guid())
    write_meta(Path(str(md_path) + ".meta"), guid())
    print("dst", nx, ny, nz, "enabled", enabled)


if __name__ == "__main__":
    main()
