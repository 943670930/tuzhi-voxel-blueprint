from pathlib import Path

import bpy

BLEND = Path(r"D:\Project\TuZhi\Assets\3DModel\AigeiFantasyRpgArmor\AigeiFantasyRpgArmor_Middle_object_1.blend")
OUT = Path(r"D:\Project\TuZhi\Assets\3DModel\AigeiFantasyRpgArmor\AigeiFantasyRpgArmor_Middle_object_1_edited.obj")


def main():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("no mesh in blend")
    verts = []
    faces = []
    for obj in meshes:
        mw = obj.matrix_world
        mesh = obj.data
        base = len(verts)
        for v in mesh.vertices:
            p = mw @ v.co
            verts.append((float(p.x), float(p.y), float(p.z)))
        mesh.calc_loop_triangles()
        for tri in mesh.loop_triangles:
            faces.append((base + tri.vertices[0] + 1, base + tri.vertices[1] + 1, base + tri.vertices[2] + 1))
    lines = ["o Armor_Middle_edited"]
    for x, y, z in verts:
        lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
    for a, b, c in faces:
        lines.append(f"f {a} {b} {c}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("exported", OUT, "verts", len(verts), "faces", len(faces), "objects", len(meshes))


if __name__ == "__main__":
    main()
