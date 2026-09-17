from pathlib import Path

import bpy

SRC = Path(r"D:\Project\TuZhi\Assets\3DModel\AigeiFantasyRpgArmor\AigeiFantasyRpgArmor_Middle_object_1.obj")
BLEND = Path(r"E:\Blender\AigeiFantasyRpgArmor_Middle_object_1.blend")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(
        filepath=str(SRC),
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
    )
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("no mesh")
    obj = meshes[0]
    obj.name = "Armor_Middle_object_1"
    light_data = bpy.data.lights.new("Key", "SUN")
    light_data.energy = 3.0
    light = bpy.data.objects.new("Key", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (40.0, 80.0, 120.0)
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print("saved", BLEND)


if __name__ == "__main__":
    main()
