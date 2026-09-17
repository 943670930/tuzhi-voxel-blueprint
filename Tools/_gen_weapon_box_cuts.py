import os
import re
import uuid

SCRIPT_GUID = "301e406fcbe8aa945a3dfd229e2555a7"
ROOT = r"D:\Project\TuZhi\Assets\VoxelBlueprints\Weapons"

WEAPONS = [
    ("iron_spear", "iron_spear", "two"),
    ("dagger_bronze", "dagger_bronze", "two"),
    ("iron_hammer", "iron_hammer", "two"),
    ("javelin", "javelin", "two"),
    ("javelin_pouch", "javelin_pouch", "single"),
    ("wooden_shield", "wooden_shield", "three"),
    ("shotgun", "shotgun", "shotgun"),
]


def read_bounds(item):
    path = os.path.join(ROOT, item, f"PmWeaponVoxelStyle_{item}.asset")
    text = open(path, encoding="utf-8").read()
    sx = int(re.search(r"sizeX: (\d+)", text).group(1))
    sy = int(re.search(r"sizeY: (\d+)", text).group(1))
    sz = int(re.search(r"sizeZ: (\d+)", text).group(1))
    enabled = [i for i, m in enumerate(re.findall(r"enabled: (\d)", text)) if m == "1"]

    def idx_to_xyz(i):
        z = i // (sx * sy)
        rem = i % (sx * sy)
        y = rem // sx
        x = rem % sx
        return x, y, z

    pts = [idx_to_xyz(i) for i in enabled]
    xs, ys, zs = zip(*pts)
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


def read_source_guid(item):
    meta = os.path.join(ROOT, item, f"PmWeaponVoxelStyle_{item}.asset.meta")
    return open(meta, encoding="utf-8").read().split("guid: ")[1].split()[0]


def make_box(label, center, size):
    return {"label": label, "center": center, "size": size}


def seed_two(minp, maxp):
    minx, miny, minz = minp
    maxx, maxy, maxz = maxp
    ex, ey, ez = maxx - minx + 1, maxy - miny + 1, maxz - minz + 1
    size = (ex, ey + 1, ez)
    cx = minx + ex * 0.5
    cz = minz + ez * 0.5
    blade_y = maxy + 0.5 - size[1] * 0.25 + 2
    handle_y = miny + 0.5 - size[1] * 0.25 - 1
    return [
        make_box("\u5203", (cx, blade_y, cz), size),
        make_box("\u67c4", (cx, handle_y, cz), size),
    ]


def seed_single(minp, maxp, label="\u67aa\u888b"):
    minx, miny, minz = minp
    maxx, maxy, maxz = maxp
    ex, ey, ez = maxx - minx + 1, maxy - miny + 1, maxz - minz + 1
    size = (ex, ey, ez + 1)
    cx = minx + ex * 0.5
    cy = miny + ey * 0.5
    cz = minz + ez * 0.5
    return [make_box(label, (cx, cy, cz), size)]


def seed_three(minp, maxp):
    minx, miny, minz = minp
    maxx, maxy, maxz = maxp
    ex, ey, ez = maxx - minx + 1, maxy - miny + 1, maxz - minz + 1
    band = max(1, ey // 3)
    labels = ["mesh_0", "mesh_1", "mesh_2"]
    boxes = []
    for i, lab in enumerate(labels):
        y0 = miny + i * band
        y1 = maxy if i == 2 else miny + (i + 1) * band - 1
        bey = y1 - y0 + 1
        size = (ex, bey + 1, ez)
        cy = y0 + bey * 0.5
        cx = minx + ex * 0.5
        cz = minz + ez * 0.5
        boxes.append(make_box(lab, (cx, cy, cz), size))
    return boxes


def yaml_box(b):
    lab = b["label"]
    cx, cy, cz = b["center"]
    sx, sy, sz = b["size"]
    return (
        f'  - label: "{lab}"\n'
        f"    min: {{x: 0, y: 0, z: 0}}\n"
        f"    max: {{x: 0, y: 0, z: 0}}\n"
        f"    center: {{x: {cx}, y: {cy}, z: {cz}}}\n"
        f"    size: {{x: {sx}, y: {sy}, z: {sz}}}\n"
        f"    euler: {{x: 0, y: 0, z: 0}}"
    )


def write_meta(path, folder=False):
    g = uuid.uuid4().hex
    if folder:
        body = (
            f"fileFormatVersion: 2\n"
            f"guid: {g}\n"
            f"folderAsset: yes\n"
            f"DefaultImporter:\n"
            f"  externalObjects: {{}}\n"
            f"  userData: \n"
            f"  assetBundleName: \n"
            f"  assetBundleVariant: \n"
        )
    else:
        body = (
            f"fileFormatVersion: 2\n"
            f"guid: {g}\n"
            f"NativeFormatImporter:\n"
            f"  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n"
            f"  userData: \n"
            f"  assetBundleName: \n"
            f"  assetBundleVariant: \n"
        )
    open(path, "w", encoding="utf-8", newline="\n").write(body)


def main():
    for item, item_id, mode in WEAPONS:
        minp, maxp = read_bounds(item)
        src_guid = read_source_guid(item)
        if mode == "two":
            boxes = seed_two(minp, maxp)
            labels = ["\u5203", "\u67c4"]
        elif mode == "single":
            boxes = seed_single(minp, maxp)
            labels = ["\u67aa\u888b"]
        elif mode == "shotgun":
            boxes = seed_two(minp, maxp)
            labels = ["\u67aa\u8eab", "\u67aa\u6258"]
        else:
            boxes = seed_three(minp, maxp)
            labels = ["mesh_0", "mesh_1", "mesh_2"]

        part_dir = os.path.join(ROOT, item, "Part")
        os.makedirs(part_dir, exist_ok=True)
        if not os.path.exists(part_dir + ".meta"):
            write_meta(part_dir + ".meta", folder=True)

        asset_name = f"{item}_WeaponPartBoxCut"
        asset_path = os.path.join(part_dir, asset_name + ".asset")
        label_yaml = "\n".join(f'  - "{l}"' for l in labels)
        reject_yaml = "\n".join(f'  - label: "{l}"\n    cells: []' for l in labels)
        boxes_yaml = "\n".join(yaml_box(b) for b in boxes)
        content = (
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
            f"  m_Name: {asset_name}\n"
            "  m_EditorClassIdentifier: \n"
            f"  source: {{fileID: 11400000, guid: {src_guid}, type: 2}}\n"
            f"  weaponItemId: {item_id}\n"
            "  partLabels:\n"
            f"{label_yaml}\n"
            "  boxes:\n"
            f"{boxes_yaml}\n"
            "  rejected:\n"
            f"{reject_yaml}\n"
            "  claimed:\n"
            f"{reject_yaml}\n"
        )
        open(asset_path, "w", encoding="utf-8", newline="\n").write(content)
        if not os.path.exists(asset_path + ".meta"):
            write_meta(asset_path + ".meta")
        print(item, "parts", len(boxes), "->", asset_path)


if __name__ == "__main__":
    main()
