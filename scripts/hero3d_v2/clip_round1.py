"""Step A of round 2: two baked test clips on the UNCHANGED round-1 model, for the Unity playback test.
blender -b art/redo/hero3d_v2/round1/hero.blend --python scripts/hero3d_v2/clip_round1.py -- <out dir>
Writes <out>/hero.fbx (actions "Stance" and "TestSwing", 24 fps), clip_joints.json, then the caller copies
hero_look.json + face.png and writes READY.txt. The pose code is round 1's render.py pose_stance, copied here so later
changes to render.py do not change this clip."""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

OUT = sys.argv[sys.argv.index("--") + 1:][0]
os.makedirs(OUT, exist_ok=True)
ARM = bpy.data.objects["HeroRig"]
SIDES = {"Right": -1, "Left": 1}
FWD = Vector((0, -1, 0))

# ---- strip the outline hull (render-only) so the FBX carries the plain meshes, as round 1's hero.fbx did
for ob in bpy.data.objects:
    if ob.type != 'MESH':
        continue
    for m in [m for m in ob.modifiers if m.type == 'SOLIDIFY']:
        ob.modifiers.remove(m)
    names = [m.name if m else "" for m in ob.data.materials]
    if "Outline" in names:
        ob.data.materials.pop(index=names.index("Outline"))


# ---- round 1 pose vocabulary (hero_lib.rot/move + render.py helpers)
def rot(bone, axis, deg):
    pb = ARM.pose.bones[bone]
    pb.rotation_mode = 'QUATERNION'
    M = ARM.data.bones[bone].matrix_local.to_3x3()
    R = Matrix.Rotation(math.radians(deg), 3, Vector(axis).normalized())
    pb.rotation_quaternion = (M.inverted() @ R @ M).to_quaternion() @ pb.rotation_quaternion


def move(bone, offset):
    pb = ARM.pose.bones[bone]
    M = ARM.data.bones[bone].matrix_local.to_3x3()
    pb.location = pb.location + M.inverted() @ Vector(offset)


def bdir(name):
    b = ARM.data.bones[name]
    return (b.tail_local - b.head_local).normalized()


PITCHES = {}


def arm_down(side, deg): rot(side + "UpperArm", (0, 1, 0), SIDES[side] * deg)
def arm_fwd(side, deg): rot(side + "UpperArm", (1, 0, 0), -deg)
def clav_up(side, deg): rot(side + "Shoulder", (0, 1, 0), -SIDES[side] * deg)
def elbow(side, deg): rot(side + "LowerArm", bdir(side + "LowerArm").cross(FWD), deg)
def wrist(side, deg): rot(side + "Hand", bdir(side + "Hand").cross(FWD), deg)
def bend(bone, deg): rot(bone, (1, 0, 0), deg)
def twist(bone, deg): rot(bone, (0, 0, 1), deg)


def forearm_twist(side, deg):
    pb = ARM.pose.bones[side + "LowerArm"]
    pb.rotation_quaternion = pb.rotation_quaternion @ Quaternion((0, 1, 0), math.radians(deg))


def hip_flex(side, deg):
    rot(side + "UpperLeg", (1, 0, 0), -deg)
    PITCHES[side] = PITCHES.get(side, 0) - deg


def leg_out(side, deg): rot(side + "UpperLeg", (0, 1, 0), -SIDES[side] * deg)


def knee(side, deg):
    rot(side + "LowerLeg", (1, 0, 0), deg)
    PITCHES[side] = PITCHES.get(side, 0) + deg


def feet_flat():
    for side, p in PITCHES.items():
        rot(side + "Foot", (1, 0, 0), -p)


def reset():
    for pb in ARM.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    PITCHES.clear()


def legs(extra_knee=0.0):
    leg_out("Right", 4)
    hip_flex("Right", -14)
    knee("Right", 22 + extra_knee)
    leg_out("Left", 3)
    hip_flex("Left", 26 + extra_knee * 0.5)
    knee("Left", 14 + extra_knee)
    feet_flat()
    rot("RightFoot", (0, 0, 1), 18)


def stance_body(chest_twist=0.0):
    bend("Spine", 4)
    twist("Spine", -4)
    twist("Chest", -8 + chest_twist)
    bend("Chest", 2)


def right_arm_stance():
    arm_down("Right", 36)
    arm_fwd("Right", 62)
    elbow("Right", 48)
    forearm_twist("Right", -20)
    wrist("Right", -10)


def left_arm_head():
    arm_down("Left", 30)
    arm_fwd("Left", -22)
    elbow("Left", 35)
    twist("Neck", 6)
    twist("Head", 8)
    bend("Head", 4)


def pose_stance():                          # == round 1 render.py pose_stance
    legs()
    stance_body()
    right_arm_stance()
    left_arm_head()


def pose_up():                              # frame 8: right arm overhead, chest turned back, a slight knee bend
    legs(6)
    stance_body(-14)
    clav_up("Right", 15)
    arm_down("Right", -120)
    arm_fwd("Right", 10)
    elbow("Right", 40)
    left_arm_head()


def pose_down():                            # frame 16: arm down and forward, chest turned into the blow, knees deep
    legs(18)
    stance_body(16)
    bend("Chest", 8)
    arm_down("Right", 20)
    arm_fwd("Right", 80)
    elbow("Right", 10)
    left_arm_head()


# ---- grounding: the stance lifted so the lowest vertex is on z=0, as round 1's ground() did; done as a Hips move
def lowest_z():
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    z = 1e9
    for ob in bpy.data.objects:
        if ob.type == 'MESH':
            e = ob.evaluated_get(dg)
            me = e.to_mesh()
            z = min(z, min((e.matrix_world @ v.co).z for v in me.vertices))
            e.to_mesh_clear()
    return z


def grounded(fn):
    reset()
    fn()
    move("Hips", (0, 0, -lowest_z()))


# ---- actions
scene = bpy.context.scene
scene.render.fps = 24
scene.frame_start, scene.frame_end = 1, 24
ARM.animation_data_create()
LAST = {}


def key_all(frame):
    bpy.context.view_layer.update()
    for pb in ARM.pose.bones:
        q = pb.rotation_quaternion.copy()
        if pb.name in LAST and LAST[pb.name].dot(q) < 0:      # keep the quaternion on the same side: no flips
            q.negate()
            pb.rotation_quaternion = q
        LAST[pb.name] = q.copy()
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)
        pb.keyframe_insert("scale", frame=frame)


def make_action(name, keys):
    ARM.animation_data.action = None
    LAST.clear()
    for frame, fn in keys:
        grounded(fn)
        key_all(frame)
    act = ARM.animation_data.action
    act.name = name
    act.use_fake_user = True
    return act


ACTS = {"Stance": make_action("Stance", [(1, pose_stance), (24, pose_stance)]),
        "TestSwing": make_action("TestSwing", [(1, pose_stance), (8, pose_up), (16, pose_down), (24, pose_stance)])}
ARM.animation_data.action = None
reset()


def joints():
    bpy.context.view_layer.update()
    return {pb.name: {"head": [round(c, 6) for c in pb.head], "tail": [round(c, 6) for c in pb.tail]}
            for pb in ARM.pose.bones}


def slot_for(act):
    try:
        return act.slots[0] if len(act.slots) else None
    except AttributeError:
        return None


def use_action(act):
    ARM.animation_data.action = act
    s = slot_for(act)
    if s is not None:
        try:
            ARM.animation_data.action_slot = s
        except Exception:
            pass


FRAMES = {"Stance": [1], "TestSwing": [1, 8, 12, 16, 24]}
data = {"axes": "Blender armature space, metres: Z up, the hero faces -Y, his right is -X, his left is +X. "
                "FBX export converts to Y up / -Z forward (Unity: Y up, he faces +Z after the importer's flip).",
        "rest": {b.name: {"head": [round(c, 6) for c in b.head_local], "tail": [round(c, 6) for c in b.tail_local]}
                 for b in ARM.data.bones},
        "clips": {}}
for name, frames in FRAMES.items():
    use_action(ACTS[name])
    data["clips"][name] = {}
    for f in frames:
        scene.frame_set(f)
        data["clips"][name][str(f)] = joints()

# ---- export: both actions baked, every frame, no simplification
ARM.animation_data.action = None
reset()
scene.frame_set(1)
FBX = dict(use_selection=False, object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=False, mesh_smooth_type='FACE',
           add_leaf_bones=False, use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True,
           bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
           bake_anim_step=1.0, bake_anim_simplify_factor=0.0, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
           apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, path_mode='COPY', embed_textures=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "hero.fbx"), **FBX)
data["fbx_export"] = {k: (sorted(v) if isinstance(v, set) else v) for k, v in FBX.items()}
data["fps"] = 24
with open(os.path.join(OUT, "clip_joints.json"), "w") as f:
    json.dump(data, f, indent=1)
print("CLIPS WRITTEN", OUT, list(ACTS))
