"""Rig the 3D hero (hero_mesh.blend + joints.json), put the sword in his right fist, key a move from a JSON file and
render it from the battle camera.

blender -b --factory-startup --python rig_hero.py -- <mesh dir> <move.json> <out dir> [--res 640] [--frames a-b]
        [--step N] [--stills f,f,...] [--yaw -35] [--blur 0.5] [--save]
Keys are written in the battle world: the hero faces +X (screen right), +Z up, his right side toward -Y (camera side).
Hand keys give the GRIP point (where the sword handle sits in the fist) and the blade direction; the arm follows by IK,
so the elbow is always a real elbow and the sword never leaves the fist.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import h3d_lib as L  # noqa: E402

a = sys.argv[sys.argv.index("--") + 1:]
MESH_DIR, MOVE, OUT = a[0], a[1], a[2]


def opt(name, default, cast=str):
    return cast(a[a.index(name) + 1]) if name in a else default


RES = opt("--res", 640, int)
SIZE = tuple(int(x) for x in opt("--size", f"{RES}x{RES}").split("x"))
YAW = opt("--yaw", -35.0, float)
BLUR = opt("--blur", 0.0, float)
STEP = opt("--step", 1, int)
STILLS = [int(x) for x in opt("--stills", "", str).split(",") if x]
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=os.path.join(MESH_DIR, "hero_mesh.blend"))
hero = bpy.data.objects["Hero"]
J = {k: (Vector(v) if isinstance(v, list) else v) for k, v in json.load(open(os.path.join(MESH_DIR, "joints.json"))).items()}
mv = json.load(open(MOVE))

# ---------------------------------------------------------------- armature (rest = the mesh's A-pose, facing -Y)
arm_data = bpy.data.armatures.new("Rig")
rig = bpy.data.objects.new("Rig", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None, deform=True, connect=False):
    b = eb.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.use_deform = deform
    if parent:
        b.parent = eb[parent]
        b.use_connect = connect
    return b


pel, spi, che, nec, hea, top = J["pelvis"], J["spine"], J["chest"], J["neck"], J["head"], J["head_top"]
bone("root", (0, pel.y, 0), (0, pel.y + 0.25, 0), deform=False)
bone("hips", pel, spi, "root")
bone("spine", spi, che, "hips", connect=True)
bone("chest", che, nec, "spine", connect=True)
bone("neck", nec, hea, "chest", connect=True)
bone("head", hea, top, "neck", connect=True)
for s, sg in (("R", -1), ("L", 1)):
    sh, el, wr, fi = J[f"shoulder.{s}"], J[f"elbow.{s}"].copy(), J[f"wrist.{s}"], J[f"fist.{s}"]
    el.y += 0.015                                   # a slight backward bend so the IK knows how the elbow folds
    hip, kn, an, to = J[f"hip.{s}"], J[f"knee.{s}"].copy(), J[f"ankle.{s}"], J[f"toe.{s}"]
    kn.y -= 0.015                                   # and the knee forward
    bone(f"clavicle.{s}", Vector((sg * 0.03, nec.y, sh.z - 0.01)), sh, "chest")
    bone(f"upper_arm.{s}", sh, el, f"clavicle.{s}", connect=True)
    bone(f"forearm.{s}", el, wr, f"upper_arm.{s}", connect=True)
    bone(f"hand.{s}", wr, fi + (fi - wr) * 0.9, f"forearm.{s}", connect=True)
    bone(f"thigh.{s}", hip, kn, "hips")
    bone(f"shin.{s}", kn, an, f"thigh.{s}", connect=True)
    bone(f"foot.{s}", an, to, f"shin.{s}", connect=True)
    # controls
    hb = eb[f"hand.{s}"]
    c = bone(f"ik_hand.{s}", hb.head, hb.tail, "root", deform=False)
    c.roll = hb.roll
    fb = eb[f"foot.{s}"]
    c = bone(f"ik_foot.{s}", fb.head, fb.tail, "root", deform=False)
    c.roll = fb.roll
    bone(f"pole_arm.{s}", el + Vector((0, 0.5, 0)), el + Vector((0, 0.6, 0)), "root", deform=False)
    bone(f"pole_leg.{s}", kn + Vector((0, -0.5, 0)), kn + Vector((0, -0.6, 0)), "root", deform=False)
# the sword's own bone in the right fist: it turns about the palm's normal, so the handle can tilt in the fist the way
# a real grip lets it (the blade always leaves the fist by the thumb and first finger, never by the little finger)
_fi, _fa = J["fist.R"], (J["wrist.R"] - J["elbow.R"]).normalized()
_b0 = Vector((0, -1, 0))
_b0 = (_b0 - _fa * _b0.dot(_fa)).normalized()
_g = _fi + _fa * mv.get("grip_down", 0.0)
bone("grip.R", _g, _g + _fa.cross(_b0).normalized() * 0.05, "hand.R", deform=False)
bpy.ops.object.mode_set(mode='OBJECT')

# bind: our own weights. Each vertex takes the nearest bone segments (inverse distance^4, the 3 nearest within 1.6x
# the nearest distance), then the weights are smoothed along the mesh edges. Blender's heat weighting fails on
# generated meshes (inner shells), this never does.
def bind_by_distance(obj, smooth=int(opt("--smooth", 8, int))):
    deform = [b for b in rig.data.bones if b.use_deform]
    V = np.array([v.co[:] for v in obj.data.vertices])
    H = np.array([b.head_local[:] for b in deform])
    T = np.array([b.tail_local[:] for b in deform])
    d = np.empty((len(V), len(deform)))
    for i in range(len(deform)):
        seg = T[i] - H[i]
        t = np.clip(((V - H[i]) @ seg) / max(seg @ seg, 1e-9), 0, 1)
        d[:, i] = np.linalg.norm(V - (H[i] + np.outer(t, seg)), axis=1)
    dmin = d.min(1, keepdims=True)
    w = np.where(d <= dmin * 1.6 + 0.004, 1.0 / (d ** 4 + 1e-7), 0.0)
    order = np.argsort(-w, axis=1)
    keep = np.zeros_like(w, dtype=bool)
    np.put_along_axis(keep, order[:, :3], True, axis=1)
    w = np.where(keep, w, 0)
    w /= w.sum(1, keepdims=True)
    E = np.array([e.vertices[:] for e in obj.data.edges])
    for _ in range(smooth):
        acc = np.zeros_like(w)
        cnt = np.zeros(len(V))
        np.add.at(acc, E[:, 0], w[E[:, 1]])
        np.add.at(acc, E[:, 1], w[E[:, 0]])
        np.add.at(cnt, E[:, 0], 1)
        np.add.at(cnt, E[:, 1], 1)
        nb = acc / np.maximum(cnt, 1)[:, None]
        w = 0.5 * w + 0.5 * nb
    w /= w.sum(1, keepdims=True)
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    for i, b in enumerate(deform):
        vg = obj.vertex_groups.new(name=b.name)
        idx = np.nonzero(w[:, i] > 0.01)[0]
        for wv in np.unique(np.round(w[idx, i], 2)):
            sel = idx[np.round(w[idx, i], 2) == wv]
            vg.add(sel.tolist(), float(wv), 'REPLACE')
    return int((w.max(1) < 0.01).sum())


print("UNWEIGHTED", bind_by_distance(hero), "of", len(hero.data.vertices))
am = hero.modifiers.new("Armature", 'ARMATURE')
am.object = rig
hero.parent = rig
bpy.context.view_layer.objects.active = hero
while hero.modifiers.find("Armature") > 0:
    bpy.ops.object.modifier_move_up(modifier="Armature")

# constraints
pb = rig.pose.bones
for s in ("R", "L"):
    k = pb[f"forearm.{s}"].constraints.new('IK')
    k.target, k.subtarget = rig, f"ik_hand.{s}"
    k.pole_target, k.pole_subtarget = rig, f"pole_arm.{s}"
    k.chain_count = 2
    cr = pb[f"hand.{s}"].constraints.new('COPY_ROTATION')
    cr.target, cr.subtarget = rig, f"ik_hand.{s}"
    k = pb[f"shin.{s}"].constraints.new('IK')
    k.target, k.subtarget = rig, f"ik_foot.{s}"
    k.pole_target, k.pole_subtarget = rig, f"pole_leg.{s}"
    k.chain_count = 2
    cr = pb[f"foot.{s}"].constraints.new('COPY_ROTATION')
    cr.target, cr.subtarget = rig, f"ik_foot.{s}"


def best_pole(chain_bone, mid_bone, rest_mid):
    k = pb[chain_bone].constraints["IK"]
    best = None
    for ang in range(-180, 180, 5):
        k.pole_angle = math.radians(ang)
        bpy.context.view_layer.update()
        d = (rig.matrix_world @ pb[mid_bone].tail - rest_mid).length
        if best is None or d < best[0]:
            best = (d, ang)
    k.pole_angle = math.radians(best[1])
    return best


rig.rotation_euler = (0, 0, 0)
bpy.context.view_layer.update()
for s in ("R", "L"):
    print("POLE arm", s, best_pole(f"forearm.{s}", f"upper_arm.{s}", rig.data.bones[f"upper_arm.{s}"].tail_local.copy()))
    print("POLE leg", s, best_pole(f"shin.{s}", f"thigh.{s}", rig.data.bones[f"thigh.{s}"].tail_local.copy()))

# ---------------------------------------------------------------- sword in the right fist
sword = L.make_sword(mv.get("sword_length", 0.95))
fi, wr, el = J["fist.R"], J["wrist.R"], J["elbow.R"]
fa = (wr - el).normalized()                       # forearm axis, toward the hand
b0 = Vector((0, -1, 0))                           # the blade points forward (-Y) at rest, square to the forearm
b0 = (b0 - fa * b0.dot(fa)).normalized()
n0 = (-fa - b0 * (-fa).dot(b0)).normalized()     # blade width in the plane of blade and forearm (edge leads a chop)
R0 = Matrix((n0, b0.cross(n0), b0)).transposed()  # sword local X = width, Z = blade
sword.matrix_world = Matrix.Translation(fi + fa * mv.get("grip_down", 0.0)) @ R0.to_4x4()
sword.parent = rig
sword.parent_type = 'BONE'
sword.parent_bone = "grip.R"
sword.matrix_parent_inverse = (rig.matrix_world @ rig.data.bones["grip.R"].matrix_local @
                               Matrix.Translation((0, rig.data.bones["grip.R"].length, 0))).inverted()

# ---------------------------------------------------------------- the move
FACE = Matrix.Rotation(math.radians(90), 4, 'Z')   # rest faces -Y; battle faces +X
rig.matrix_world = FACE
bpy.context.view_layer.update()
W2R = FACE.inverted()                              # battle world -> rig space
rest = {n: rig.data.bones[n].matrix_local.copy() for n in rig.data.bones.keys()}
hand_rest = rest["ik_hand.R"]
grip_off_rest = (fi + fa * mv.get("grip_down", 0.0)) - rig.data.bones["ik_hand.R"].head_local   # wrist -> grip (rig)


def frame_from(blade, up):
    """Rotation (rig space) that turns the rest blade b0 into `blade` and rest n0 toward `up` (both world)."""
    b = (W2R.to_3x3() @ Vector(blade)).normalized()
    u = (W2R.to_3x3() @ Vector(up)).normalized()
    n = (u - b * u.dot(b)).normalized()
    f = n.cross(b).normalized()
    # rest frame columns (n0, fa, b0) must map to (n, ?, b): fa = b0 x n0 reversed -> keep handedness
    Rnew = Matrix((n, b.cross(n), b)).transposed()
    Rold = Matrix((n0, b0.cross(n0), b0)).transposed()
    return Rnew @ Rold.inverted()


# rest frame of the right hand (rig space): forearm axis, thumb side (= the blade at rest), palm normal
FA0 = (J["wrist.R"] - J["elbow.R"]).normalized()
TH0 = (b0 - FA0 * b0.dot(FA0)).normalized()
REST_FRAME = Matrix((FA0, TH0, FA0.cross(TH0))).transposed()
WRIST_DEV = (-15.0, 30.0)     # the wrist bends toward the thumb at most 15 deg, toward the little finger 30
GRIP_TILT = (-10.0, 30.0)     # the handle tilts in the fist by up to this much more
hand_log = []


def solve_hand(f, hk):
    """Grip point + asked blade direction -> the hand's target matrix. The blade can only leave the fist on the thumb
    side, at an angle to the forearm the wrist and the grip can make: theta (from square to the forearm, toward the
    forearm's own direction) within WRIST_DEV + GRIP_TILT. The forearm direction comes from the IK, so it is solved in
    a few rounds. Logs the asked and the given blade."""
    G = W2R @ Vector(hk["p"])
    want = (W2R.to_3x3() @ Vector(hk["blade"])).normalized()
    fdir = (G - pb["upper_arm.R"].head).normalized()
    lo, hi = WRIST_DEV[0] + GRIP_TILT[0], WRIST_DEV[1] + GRIP_TILT[1]
    r_prev = None
    for _ in range(5):
        r = want - fdir * want.dot(fdir)
        if r.length < 1e-3:
            r = r_prev if r_prev is not None else fdir.orthogonal()
        r = r.normalized()
        r_prev = r
        th = math.degrees(math.atan2(want.dot(fdir), want.dot(r)))
        thc = max(lo, min(hi, hk.get("th", th)))       # a key may ask for the angle itself ("th"); "blade" then only
                                                        # gives the side and the swing plane
        dev = max(WRIST_DEV[0], min(WRIST_DEV[1], thc))
        tilt = thc - dev
        dr, tr = math.radians(dev), math.radians(tilt)
        d = fdir * math.cos(dr) - r * math.sin(dr)            # the hand's own axis (wrist to knuckles)
        t = fdir * math.sin(dr) + r * math.cos(dr)            # its thumb side, square to it
        Rm = Matrix((d, t, d.cross(t))).transposed() @ REST_FRAME.inverted()
        wrist = G - Rm @ grip_off_rest
        m = Matrix.Translation(wrist) @ (Rm @ hand_rest.to_3x3()).to_4x4()
        key_bone("ik_hand.R", f, mat=m)
        gp = pb["grip.R"]
        gp.rotation_mode = 'QUATERNION'
        gp.rotation_quaternion = Quaternion((0, 1, 0), -tr)
        gp.keyframe_insert("rotation_quaternion", frame=f)
        bpy.context.view_layer.update()
        fdir = (pb["hand.R"].head - pb["forearm.R"].head).normalized()
    got = (rig.matrix_world.inverted().to_3x3() @ sword.matrix_world.to_3x3().col[2]).normalized()
    fw, gw = FACE.to_3x3() @ fdir, FACE.to_3x3() @ got        # back to battle world: x forward, z up
    hand_log.append((f, round(th), round(thc), round(dev), round(tilt), round(math.degrees(got.angle(want))),
                     "arm", round(math.degrees(math.atan2(fw.z, fw.x))), "blade", round(math.degrees(math.atan2(gw.z, gw.x)))))
    return m


def key_bone(name, f, loc=None, rot=None, mat=None):
    p = pb[name]
    if mat is not None:
        p.rotation_mode = 'QUATERNION'
        p.matrix = mat
        bpy.context.view_layer.update()
        p.keyframe_insert("location", frame=f)
        p.rotation_mode = 'QUATERNION'
        p.keyframe_insert("rotation_quaternion", frame=f)
        return
    if loc is not None:
        p.location = Vector(loc)
        p.keyframe_insert("location", frame=f)
    if rot is not None:
        p.rotation_mode = 'XYZ'
        p.rotation_euler = [math.radians(x) for x in rot]
        p.keyframe_insert("rotation_euler", frame=f)


reach_log = []
for k in mv["keys"]:
    f = k["f"]
    bpy.context.scene.frame_set(f)
    # body: root offset (world) and FK turns (degrees, bone local XYZ)
    root_w = Vector(k.get("root", (0, 0, 0)))
    key_bone("root", f, loc=(rig.data.bones["root"].matrix_local.to_3x3().inverted() @ (W2R.to_3x3() @ root_w)))
    for b in ("hips", "spine", "chest", "neck", "head", "clavicle.R", "clavicle.L"):
        key_bone(b, f, rot=k.get(b, (0, 0, 0)))
    bpy.context.view_layer.update()
    root_m = rig.matrix_world @ pb["root"].matrix
    # feet: ankle point (world) + foot pitch
    for s in ("R", "L"):
        fk = k.get(f"foot.{s}")
        if fk is None:
            continue
        p = W2R @ Vector(fk[:3])
        pitch = fk[3] if len(fk) > 3 else 0.0
        R = Matrix.Rotation(math.radians(pitch), 4, 'X')
        m = Matrix.Translation(p) @ R @ rest[f"ik_foot.{s}"].to_3x3().to_4x4()
        key_bone(f"ik_foot.{s}", f, mat=m)
    # poles before the hands: the sword hand is solved against this frame's elbow
    for s in ("R", "L"):
        for pole in (f"pole_arm.{s}", f"pole_leg.{s}"):
            if pole in k:
                p = W2R @ Vector(k[pole])
                m = Matrix.Translation(p) @ rest[pole].to_3x3().to_4x4()
                key_bone(pole, f, mat=m)
    bpy.context.view_layer.update()
    # hands: grip point + blade direction (+ thumb direction) in world
    for s in ("R", "L"):
        hk = k.get(f"hand.{s}")
        if hk is None:
            continue
        if s == "R":
            m = solve_hand(f, hk)
        else:
            rot = Matrix.Rotation(math.radians(hk.get("twist", 0)), 3, 'Z')
            p = W2R @ Vector(hk["p"])
            m = Matrix.Translation(p) @ (rot @ rest["ik_hand.L"].to_3x3()).to_4x4()
        key_bone(f"ik_hand.{s}", f, mat=m)
        bpy.context.view_layer.update()
        got = rig.matrix_world @ pb[f"hand.{s}"].head
        want = rig.matrix_world @ m.translation
        reach_log.append((f, s, round((got - want).length, 3)))
print("REACH (frame, hand, metres off)", [r for r in reach_log if r[2] > 0.01])
print("HAND (frame, blade asked deg from square, given, wrist, grip tilt, off by deg, forearm and blade angle on screen)", hand_log)

sc = bpy.context.scene
frames = [k["f"] for k in mv["keys"]]
sc.frame_start, sc.frame_end = min(frames), max(frames)
if "frames" in a:
    s0, s1 = opt("--frames", "0-0").split("-")
    sc.frame_start, sc.frame_end = int(s0), int(s1)
def fcurves(act):
    if hasattr(act, "fcurves") and len(act.fcurves):
        return list(act.fcurves)
    out = []
    for lay in getattr(act, "layers", []):
        for st in lay.strips:
            for cb in st.channelbags:
                out += list(cb.fcurves)
    return out


if mv.get("interp"):
    for fc in fcurves(rig.animation_data.action):
        for kp in fc.keyframe_points:
            kp.interpolation = mv["interp"]

L.render_setup(SIZE, transparent=True)
if "--no-outline" in a:
    for m in hero.modifiers:
        if m.type == 'SOLIDIFY':
            m.show_render = False
if "--outline" in a:
    for m in hero.modifiers:
        if m.type == 'SOLIDIFY':
            m.thickness = opt("--outline", 0.006, float)
if BLUR:
    sc.render.use_motion_blur = True
    sc.render.motion_blur_shutter = BLUR
L.camera(target=tuple(float(x) for x in opt("--target", ",".join(map(str, mv.get("cam_target", (0.25, 0, 0.82))))).split(",")), yaw=YAW, pitch=mv.get("cam_pitch", 6.0),
         ortho=opt("--ortho", mv.get("cam_ortho", 2.9), float))
if "--save" in a:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "hero_rigged.blend"))
todo = STILLS or list(range(sc.frame_start, sc.frame_end + 1, STEP))
if "--track" in a:
    # where the grip, the blade tip, the wrist and the elbow land on screen in every frame (pixels), for hand close-ups
    from bpy_extras.object_utils import world_to_camera_view
    tip_z = max(v.co.z for v in sword.data.vertices)
    trk = {}
    for f in range(sc.frame_start, sc.frame_end + 1):
        sc.frame_set(f)
        pts = {"grip": sword.matrix_world @ Vector((0, 0, 0)), "tip": sword.matrix_world @ Vector((0, 0, tip_z)),
               "wrist": rig.matrix_world @ pb["hand.R"].head, "elbow": rig.matrix_world @ pb["forearm.R"].head}
        out = {}
        for k, co in pts.items():
            v = world_to_camera_view(sc, sc.camera, co)
            out[k] = [round(v.x * SIZE[0], 1), round((1 - v.y) * SIZE[1], 1)]
        trk[f] = out
    json.dump(trk, open(opt("--track", "track.json"), "w"), indent=0)
    if "--no-render" in a:
        todo = []
for f in todo:
    sc.frame_set(f)
    sc.render.filepath = os.path.join(OUT, f"f{f:03d}.png")
    bpy.ops.render.render(write_still=True)
print("DONE", len(todo))
