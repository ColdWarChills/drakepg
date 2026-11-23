"""Pose vocabulary and the poses of the clean 3D hero (v2), shared by build_hero.py (which bakes them into the FBX
actions) and render.py (which renders the same actions). Body frame = armature rest frame: +X his left, -Y forward,
+Z up. Every pose ends grounded: the Hips move down/up so the lowest BODY vertex (not the sword) sits on z = 0."""
import math

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ARM = None
SIDES = {"Right": -1, "Left": 1}
FWD = Vector((0, -1, 0))
PITCHES = {}
BODY = ("Head", "Hair", "Headband", "Shirt", "Arms", "Hands", "Belt", "Trousers", "Boots")


def init(arm):
    global ARM
    ARM = arm


def rot(bone, axis, deg):
    """Rotate a bone about an axis in the body (armature rest) frame; the parent's pose carries it along."""
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


def arm_down(side, deg):                  # from the A-pose toward the body (negative = up, sideways)
    rot(side + "UpperArm", (0, 1, 0), SIDES[side] * deg)


def arm_fwd(side, deg):                   # swing the upper arm forward (negative = back)
    rot(side + "UpperArm", (1, 0, 0), -deg)


def arm_in(side, deg):                    # swing the upper arm across the chest
    rot(side + "UpperArm", (0, 0, 1), SIDES[side] * deg)


def clav_up(side, deg):
    rot(side + "Shoulder", (0, 1, 0), -SIDES[side] * deg)


def elbow(side, deg):                     # flexion: forearm toward the biceps
    rot(side + "LowerArm", bdir(side + "LowerArm").cross(FWD), deg)


def forearm_twist(side, deg):             # about the forearm's own axis
    pb = ARM.pose.bones[side + "LowerArm"]
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = pb.rotation_quaternion @ Quaternion((0, 1, 0), math.radians(deg))


def wrist(side, deg, axis=None):
    a = bdir(side + "Hand").cross(FWD) if axis is None else Vector(axis)
    rot(side + "Hand", a, deg)


def hip_flex(side, deg):                  # thigh forward (negative = back)
    rot(side + "UpperLeg", (1, 0, 0), -deg)
    PITCHES[side] = PITCHES.get(side, 0) - deg


def leg_out(side, deg):
    rot(side + "UpperLeg", (0, 1, 0), -SIDES[side] * deg)


def knee(side, deg):
    rot(side + "LowerLeg", (1, 0, 0), deg)
    PITCHES[side] = PITCHES.get(side, 0) + deg


def feet_flat(extra=None):
    for side, p in PITCHES.items():
        rot(side + "Foot", (1, 0, 0), -p + (extra or {}).get(side, 0))


def bend(bone, deg):                      # lean forward
    rot(bone, (1, 0, 0), deg)


def twist(bone, deg):                     # turn so his right shoulder comes forward (toward profile in the game view)
    rot(bone, (0, 0, 1), deg)


def side_bend(bone, deg):
    rot(bone, (0, 1, 0), deg)


def reset():
    for pb in ARM.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    PITCHES.clear()
    ARM.location = (0, 0, 0)
    ARM.rotation_euler = (0, 0, 0)


def body_points():
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for name in BODY:
        ob = bpy.data.objects.get(name)
        if ob is None:
            continue
        e = ob.evaluated_get(dg)
        me = e.to_mesh()
        co = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        M = np.array(e.matrix_world)
        pts.append(co.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3])
        e.to_mesh_clear()
    return np.concatenate(pts)


def ground():
    """Hips move so the lowest body vertex sits on z = 0 (armature at the origin, unrotated)."""
    move("Hips", (0, 0, -body_points()[:, 2].min()))
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- poses
def pose_rest():
    pass


def stance_legs():
    twist("Hips", 28)                     # hips toward profile, so the stride reads wide at the game angle
    leg_out("Left", 6)
    hip_flex("Left", 26)                  # far (left) leg forward
    knee("Left", 20)
    leg_out("Right", 7)
    hip_flex("Right", -22)                # near (right) leg back, almost straight
    knee("Right", 8)
    feet_flat({"Right": 0})
    rot("RightFoot", (0, 0, 1), 10)       # back foot turned out a little


def stance_torso():
    bend("Spine", 4)                      # slight forward lean
    twist("Spine", -10)                   # chest turned back toward the viewer
    twist("Chest", -12)
    bend("Chest", 5)
    twist("Neck", 8)                      # head toward the enemy
    twist("Head", 10)
    bend("Head", -4)


def stance_far_arm():                     # far (left) arm forward, fist raised to chest height, clear of the torso
    arm_down("Left", 30)
    arm_fwd("Left", 62)
    elbow("Left", 128)
    forearm_twist("Left", 30)
    wrist("Left", -10)


def stance_near_arm():                    # near (right) arm back and low, fist by the hip (the sword hand)
    arm_down("Right", 30)
    arm_fwd("Right", -26)
    elbow("Right", 35)
    forearm_twist("Right", -30)
    wrist("Right", 20)


def pose_stance():
    stance_legs()
    stance_torso()
    stance_far_arm()
    stance_near_arm()


BENDS = [  # name, label, function (same poses as round 1)
    ("b_elbow90", "elbow bent 90 deg", lambda: (arm_down("Right", 35), arm_fwd("Right", 15), elbow("Right", 90))),
    ("b_elbow140", "elbow bent 140 deg", lambda: (arm_down("Right", 35), arm_fwd("Right", 15), elbow("Right", 140))),
    ("b_knee90", "knee bent 90 deg (thigh lifted 50)", lambda: (hip_flex("Right", 50), knee("Right", 90))),
    ("b_armfwd", "arm straight forward", lambda: (arm_down("Right", 45), arm_fwd("Right", 90))),
    ("b_overhead", "arm overhead (135 deg up from rest)", lambda: (clav_up("Right", 20), arm_down("Right", -135))),
    ("b_twist", "torso twisted 45 deg", lambda: (twist("Spine", 20), twist("Chest", 25), arm_down("Right", 30),
                                                 arm_down("Left", 30))),
    ("b_lunge", "deep lunge", lambda: (hip_flex("Left", 75), knee("Left", 85), hip_flex("Right", -35), knee("Right", 15),
                                       leg_out("Right", 5), leg_out("Left", 5), feet_flat(), bend("Spine", 10),
                                       arm_down("Right", 30), arm_fwd("Right", 40), elbow("Right", 30),
                                       arm_down("Left", 30), arm_fwd("Left", -30))),
    ("b_socket", "stance + sword on socket_hand_R", lambda: pose_stance()),
]
SIDE = [("s_elbow90", "side: elbow 90", BENDS[0][2]), ("s_elbow140", "side: elbow 140", BENDS[1][2]),
        ("s_knee90", "side: knee 90", BENDS[2][2]), ("s_overhead", "side: arm overhead", BENDS[4][2])]


def apply(fn):
    reset()
    fn()
    ground()


# ---------------------------------------------------------------- actions
LAST = {}


def key_all(frame):
    bpy.context.view_layer.update()
    for pb in ARM.pose.bones:
        q = pb.rotation_quaternion.copy()
        if pb.name in LAST and LAST[pb.name].dot(q) < 0:
            q.negate()
            pb.rotation_quaternion = q
        LAST[pb.name] = q.copy()
        for path in ("rotation_quaternion", "location", "scale"):
            pb.keyframe_insert(path, frame=frame)


def make_action(name, keys):
    """keys: [(frame, pose fn)]; each key is the pose applied from rest and grounded."""
    ARM.animation_data_create()
    ARM.animation_data.action = None
    LAST.clear()
    for frame, fn in keys:
        apply(fn)
        key_all(frame)
    act = ARM.animation_data.action
    act.name = name
    act.use_fake_user = True
    ARM.animation_data.action = None
    return act


def use_action(name, frame=1):
    act = bpy.data.actions[name]
    ARM.animation_data_create()
    ARM.animation_data.action = act
    try:
        if len(act.slots):
            ARM.animation_data.action_slot = act.slots[0]
    except AttributeError:
        pass
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
