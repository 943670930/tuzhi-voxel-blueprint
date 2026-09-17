import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"D:\Project\TuZhi\Assets\VoxelBlueprints\Weapons")


def rgba_to_tuple(rgba: int):
    return (
        rgba & 0xFF,
        (rgba >> 8) & 0xFF,
        (rgba >> 16) & 0xFF,
        (rgba >> 24) & 0xFF,
    )


def load_cells(asset_path: Path):
    text = asset_path.read_text(encoding="utf-8")
    sx = int(re.search(r"sizeX: (\d+)", text).group(1))
    sy = int(re.search(r"sizeY: (\d+)", text).group(1))
    sz = int(re.search(r"sizeZ: (\d+)", text).group(1))
    enabled = [m == "1" for m in re.findall(r"enabled: (\d)", text)]
    rgba_vals = [int(m.group(1)) for m in re.finditer(r"rgba: (\d+)", text)]
    cells = {}
    for i, on in enumerate(enabled):
        if not on:
            continue
        z = i // (sx * sy)
        rem = i % (sx * sy)
        y = rem // sx
        x = rem % sx
        cells[(x, y, z)] = rgba_to_tuple(rgba_vals[i])
    return sx, sy, sz, cells


def draw_elevation(draw, origin_x, axis, label, sx, sy, sz, cells, font, step=12):
    draw.text((origin_x, 12), label, fill=(215, 229, 244, 255), font=font)
    if axis == "front":
        width, height = sx, sz
        for z in range(sz):
            for x in range(sx):
                ray = [(x, y, z) for y in range(sy)]
                hit = [p for p in ray if p in cells]
                if not hit:
                    continue
                p = max(hit, key=lambda q: q[1])
                x0 = origin_x + x * step
                y0 = 40 + (sz - 1 - z) * step
                draw.rectangle((x0, y0, x0 + step - 1, y0 + step - 1), fill=cells[p], outline=(17, 26, 39, 255))
    elif axis == "side":
        width, height = sy, sz
        for z in range(sz):
            for y in range(sy):
                ray = [(x, y, z) for x in range(sx)]
                hit = [p for p in ray if p in cells]
                if not hit:
                    continue
                p = max(hit, key=lambda q: q[0])
                x0 = origin_x + y * step
                y0 = 40 + (sz - 1 - z) * step
                draw.rectangle((x0, y0, x0 + step - 1, y0 + step - 1), fill=cells[p], outline=(17, 26, 39, 255))
    else:
        width, height = sx, sy
        for y in range(sy):
            for x in range(sx):
                ray = [(x, y, z) for z in range(sz)]
                hit = [p for p in ray if p in cells]
                if not hit:
                    continue
                p = max(hit, key=lambda q: q[2])
                x0 = origin_x + x * step
                y0 = 40 + (sy - 1 - y) * step
                draw.rectangle((x0, y0, x0 + step - 1, y0 + step - 1), fill=cells[p], outline=(17, 26, 39, 255))
    draw.rectangle(
        (origin_x - 1, 39, origin_x + width * step - 1, 40 + height * step - 1),
        outline=(96, 119, 147, 255),
        width=2,
    )
    return width * step


def make_preview(item_id: str):
    asset = ROOT / item_id / f"PmWeaponVoxelStyle_{item_id}.asset"
    if not asset.exists():
        raise FileNotFoundError(asset)
    sx, sy, sz, cells = load_cells(asset)
    out = asset.with_name(f"PmWeaponVoxelStyle_{item_id}_Preview.png")
    step = max(6, min(18, 900 // max(sx, sy, sz)))
    panel_w = max(sx, sy) * step + 40
    im = Image.new("RGBA", (panel_w * 3 + 80, max(sz, sy) * step + 90), (25, 31, 42, 255))
    draw = ImageDraw.Draw(im)
    font = ImageFont.load_default()
    x = 30
    for axis, label in (
        ("front", "FRONT (+Z)"),
        ("side", "RIGHT (+X)"),
        ("top", "TOP (+Y)"),
    ):
        w = draw_elevation(draw, x, axis, label, sx, sy, sz, cells, font, step)
        x += w + 30
    draw.text(
        (30, im.height - 24),
        f"{item_id}  |  grid {sx} x {sy} x {sz}  |  {len(cells)} cells",
        fill=(155, 181, 210, 255),
        font=font,
    )
    im.save(out)
    print("preview ->", out)
    return out


def main():
    items = sys.argv[1:] or ["shotgun"]
    for item in items:
        make_preview(item)


if __name__ == "__main__":
    main()
