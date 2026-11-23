"""Re-import a clip FBX into an empty Blender and compare the posed bone heads/tails with clip_joints.json.
blender -b --factory-startup --python scripts/hero3d_v2/check_clip.py -- <dir with hero.fbx + clip_joints.json>
Prints the action names as imported and the largest distance in mm; writes the action names (as Blender re-imports
them) back into clip_joints.json under "fbx_actions_as_imported"."""
import json
import os
import sys

import bpy
from mathutils import Vector

D = sys.argv[sys.argv.index("--") + 1:][0]
data = json.load(open(os.path.join(D, "clip_joints.json")))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(D, "hero.fbx"), automatic_bone_orientation=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
acts = [a.name for a in bpy.data.actions]
print("ACTIONS", acts)
sc = bpy.context.scene
worst = 0.0
worst_tail = 0.0
for clip, frames in data["clips"].items():
    act = next((bpy.data.actions[a] for a in acts if a == clip or a.endswith("|" + clip)), None)
    assert act is not None, f"action {clip} missing"
    arm.animation_data.action = act
    try:
        arm.animation_data.action_slot = act.slots[0]
    except Exception:
        pass
    for f, J in frames.items():
        sc.frame_set(int(f))
        bpy.context.view_layer.update()
        for name, hj in J.items():
            pb = arm.pose.bones[name]
            M = arm.matrix_world @ pb.matrix
            head = M.translation
            ln = (Vector(data["rest"][name]["tail"]) - Vector(data["rest"][name]["head"])).length
            tail = M @ Vector((0, ln, 0))
            worst = max(worst, (head - Vector(hj["head"])).length)
            worst_tail = max(worst_tail, (tail - Vector(hj["tail"])).length)
    print(f"{clip}: frames {list(frames)} checked; frame range {tuple(act.frame_range)}")
print(f"MAX HEAD ERROR {worst * 1000:.4f} mm; MAX TAIL ERROR {worst_tail * 1000:.4f} mm (tail = head + bone Y * rest length)")
data["fbx_actions_as_imported"] = acts
data["reimport_check"] = {"max_head_error_mm": round(worst * 1000, 4), "max_tail_error_mm": round(worst_tail * 1000, 4),
                          "how": "Blender 5.2 FBX import (automatic_bone_orientation off) into an empty scene, "
                                 "world positions of each posed bone vs this file"}
json.dump(data, open(os.path.join(D, "clip_joints.json"), "w"), indent=1)
assert worst < 0.001 and worst_tail < 0.001, "posed bones differ by 1 mm or more"
print("CLIP CHECK OK")
