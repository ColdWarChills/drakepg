"""Re-export a hero FBX with mesh objects that share a bone's name renamed (Unity's Humanoid setup fails on the clash).
blender -b --factory-startup --python scripts/hero3d_unity/rename_fbx.py -- <in.fbx> <clip_joints.json> <out.fbx>
Export settings are read from clip_joints.json ("fbx_export"); the actions keep their names."""
import json
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, JOINTS, OUT = argv[0], argv[1], argv[2]
settings = json.load(open(JOINTS))["fbx_export"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC, automatic_bone_orientation=False, use_anim=True)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
bones = {b.name for b in arm.data.bones}
for ob in bpy.data.objects:
    if ob.type == 'MESH' and ob.name in bones:
        print("RENAME", ob.name, "->", ob.name + "Mesh")
        ob.name = ob.name + "Mesh"
# Actions come back as "<armature>|<action>": name them back.
for a in bpy.data.actions:
    if "|" in a.name:
        a.name = a.name.split("|")[-1]
print("ACTIONS", [a.name for a in bpy.data.actions])
kw = {k: v for k, v in settings.items() if k not in ("filepath",)}
kw["object_types"] = set(kw.get("object_types", ["ARMATURE", "MESH"]))
bpy.ops.export_scene.fbx(filepath=OUT, **kw)
print("DONE", OUT)
