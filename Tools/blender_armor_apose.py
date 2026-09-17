import math
from pathlib import Path

import bpy

ROOT = Path(r"D:\Project\TuZhi")
SRC = ROOT / "Assets/3DModel/AigeiFantasyRpgArmor/AigeiFantasyRpgArmor_Middle_object_1.obj"
OUT_DIR = ROOT / "Assets/3DModel/AigeiFantasyRpgArmor"
OUT_OBJ = OUT_DIR / "AigeiFantasyRpgArmor_Middle_object_1_TPose.obj"
OUT_PNG = OUT_DIR / "AigeiFantasyRpgArmor_Middle_object_1_TPose_front.png"
TPOSE_DEG = 90.0


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_obj():
    bpy.ops.wm.obj_import(
        filepath=str(SRC),
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
    )
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("no mesh imported")
    obj = meshes[0]
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def bbox(obj):
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    xs = [p.x for p in pts]
    ys = [p.y for p in pts]
    zs = [p.z for p in pts]
    return (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))


def add_bone(arm, name, parent, head, tail):
    bone = arm.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    if parent is not None:
        bone.parent = parent
        bone.use_connect = False
    return bone


def build_armature(obj):
    xmin, ymin, zmin, xmax, ymax, zmax = bbox(obj)
    cx = 0.5 * (xmin + xmax)
    cz = 0.5 * (zmin + zmax)
    height = ymax - ymin
    width = xmax - xmin
    hip_y = ymin + 0.48 * height
    chest_y = ymin + 0.70 * height
    neck_y = ymin + 0.82 * height
    shoulder_y = ymin + 0.80 * height
    sx = 0.22 * width
    bpy.ops.object.armature_add(enter_editmode=True, location=(0.0, 0.0, 0.0))
    arm_obj = bpy.context.object
    arm_obj.name = "ArmorArmature"
    arm = arm_obj.data
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.delete()
    hips = add_bone(arm, "Hips", None, (cx, hip_y, cz), (cx, chest_y, cz))
    spine = add_bone(arm, "Spine", hips, (cx, chest_y, cz), (cx, neck_y, cz))
    add_bone(
        arm,
        "UpperArm.R",
        spine,
        (cx + sx, shoulder_y, cz),
        (cx + sx * 1.35, ymin + 0.62 * height, cz),
    )
    add_bone(
        arm,
        "ForeArm.R",
        arm.edit_bones["UpperArm.R"],
        (cx + sx * 1.35, ymin + 0.62 * height, cz),
        (cx + sx * 1.55, ymin + 0.50 * height, cz),
    )
    add_bone(
        arm,
        "UpperArm.L",
        spine,
        (cx - sx, shoulder_y, cz),
        (cx - sx * 1.35, ymin + 0.62 * height, cz),
    )
    add_bone(
        arm,
        "ForeArm.L",
        arm.edit_bones["UpperArm.L"],
        (cx - sx * 1.35, ymin + 0.62 * height, cz),
        (cx - sx * 1.55, ymin + 0.50 * height, cz),
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def bind(mesh, arm_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def pose_apose(arm_obj):
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="POSE")
    rad = math.radians(TPOSE_DEG)
    # Blender Y-up import: X right, Y up, Z forward. Rotate upper arms around Z.
    for name, sign in (("UpperArm.R", 1.0), ("UpperArm.L", -1.0)):
        pb = arm_obj.pose.bones[name]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, sign * rad)
    for name, sign in (("ForeArm.R", 1.0), ("ForeArm.L", -1.0)):
        pb = arm_obj.pose.bones[name]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")


def apply_and_export(mesh, arm_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.wm.obj_export(
        filepath=str(OUT_OBJ),
        export_selected_objects=True,
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
    )


def render_front(mesh):
    xmin, ymin, zmin, xmax, ymax, zmax = bbox(mesh)
    cx = 0.5 * (xmin + xmax)
    cy = 0.5 * (ymin + ymax)
    span = max(xmax - xmin, ymax - ymin)
    cam_data = bpy.data.cameras.new("FrontCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = span * 1.15
    cam = bpy.data.objects.new("FrontCam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (cx, cy, zmax + span)
    cam.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.scene.camera = cam
    light_data = bpy.data.lights.new("Key", "SUN")
    light_data.energy = 3.0
    light = bpy.data.objects.new("Key", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (cx + span * 0.3, cy + span * 0.5, zmax + span)
    light.rotation_euler = (math.radians(-25), math.radians(20), 0.0)
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1400
    scene.render.filepath = str(OUT_PNG)
    scene.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = False
    world.color = (0.05, 0.05, 0.05)
    bpy.ops.render.render(write_still=True)


def main():
    if not SRC.exists():
        raise RuntimeError("missing " + str(SRC))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    clear()
    mesh = import_obj()
    arm_obj = build_armature(mesh)
    bind(mesh, arm_obj)
    pose_apose(arm_obj)
    apply_and_export(mesh, arm_obj)
    render_front(mesh)
    print("wrote", OUT_OBJ)
    print("front", OUT_PNG)


if __name__ == "__main__":
    main()
