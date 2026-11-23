"""Render the built hero in the flat look (EEVEE, emission-only toon + hull) from the SAME actions and meshes that are
exported (the "Stance" action in hero.blend), plus the bend tests posed by poses.py.
blender -b <hero.blend> --python scripts/hero3d_v2/render.py -- <mode> <out dir> [--yaw 40] [--pitch 5] [--outline 0.0055]
modes: look (stance at 4x game size, with and without the sword), turnaround, bends, face_turn, overlay.
Each render is <out>/<name>.png; <out>/meta.json holds the ground point (world origin), pixels per metre and the
projected landmarks of each picture, for sheets.py.
Sides: his right = -X in the rest frame; the weapon socket is socket_hand_R (his right fist, the near one)."""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import hero_lib as L  # noqa: E402
import poses as PZ  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
MODE, OUT = argv[0], argv[1]


def opt(k, d):
    return type(d)(argv[argv.index(k) + 1]) if k in argv else d


YAW = opt("--yaw", 40.0)
PITCH = opt("--pitch", 5.0)
OUTLINE = opt("--outline", L.OUTLINE_M)
os.makedirs(OUT, exist_ok=True)
ARM = bpy.data.objects["HeroRig"]
PZ.init(ARM)
L.set_outline_thickness(OUTLINE)
GAME_PPM = 240 / 1.75                     # game pixels per metre (hero 240 px tall)
SWORD = bpy.data.objects.get("Sword")


def show_sword(on):
    if SWORD:
        SWORD.hide_render = not on


def no_action():
    if ARM.animation_data:
        ARM.animation_data.action = None


def cam_game(ppm, size, yaw, pitch, zc):
    """Camera looking along +Y (tilted down by pitch); the hero turned by yaw (40 = facing right, chest to us)."""
    ARM.rotation_euler = (0, 0, math.radians(yaw))
    p = math.radians(pitch)
    d = Vector((0, math.cos(p), -math.sin(p)))
    L.render_setup(size)
    return L.ortho_camera(Vector((0, 0, zc)) - d * 10, d, scale=max(size) / ppm)


def socket(name):
    M = ARM.matrix_world @ ARM.pose.bones[name].matrix
    return M.translation.copy(), (M.to_3x3() @ Vector((0, 1, 0))).normalized()


LANDMARKS = {"head": ("socket_head", "head"), "fist_R": ("socket_hand_R", "head"), "fist_L": ("socket_hand_L", "head"),
             "foot_R": ("RightFoot", "head"), "foot_L": ("LeftFoot", "head"), "toe_R": ("RightToes", "tail"),
             "toe_L": ("LeftToes", "tail"), "hips": ("Hips", "head"), "chest": ("Chest", "head")}
META = {}


def shoot(name, ppm, size, yaw, pitch, zc, label=""):
    cam = cam_game(ppm, size, yaw, pitch, zc)
    bpy.context.view_layer.update()
    gx, gy, _ = L.project(cam, (0, 0, 0))
    lm = {}
    for k, (b, end) in LANDMARKS.items():
        pb = ARM.pose.bones[b]
        p = ARM.matrix_world @ (pb.head if end == "head" else pb.tail)
        x, y, z = L.project(cam, p)
        lm[k] = [round(x, 1), round(y, 1), round(z, 4)]
    sc = bpy.context.scene
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    META[name] = {"ground": [gx, gy], "ppm": ppm, "size": list(size), "label": label, "yaw": yaw, "landmarks": lm}
    print("RENDER", name, "ground", round(gx, 1), round(gy, 1))
    return cam


if MODE == "fit":                        # numbers only: the live pose_stance vs the drawn hero's landmarks
    import numpy as np
    no_action()
    PZ.apply(PZ.pose_stance)
    ARM.rotation_euler = (0, 0, math.radians(YAW))
    cam = cam_game(GAME_PPM * 4, (1400, 1400), YAW, PITCH, 0.85)
    bpy.context.view_layer.update()
    pts = PZ.body_points()
    pr = np.array([L.project(cam, p)[:2] for p in pts[::7]])
    y0, y1 = pr[:, 1].min(), pr[:, 1].max()
    H = y1 - y0
    low = pr[pr[:, 1] > y1 - 0.04 * H]
    gx = (low[:, 0].min() + low[:, 0].max()) / 2
    drawn = {"head": (0.02, 0.85), "fist_L": (0.21, 0.75), "fist_R": (-0.175, 0.47), "toe_L": (0.17, 0.02),
             "foot_R": (-0.21, 0.05)}
    for k, (b, end) in LANDMARKS.items():
        pb = ARM.pose.bones[b]
        x, y, z = L.project(cam, ARM.matrix_world @ (pb.head if end == "head" else pb.tail))
        d = drawn.get(k)
        print(f"FIT {k:7s} ({(x - gx) / H:+.3f}, {(y1 - y) / H:.3f}) depth {z:.3f}" + (f"   drawn {d}" if d else ""))
    print(f"FIT width {(pr[:, 0].min() - gx) / H:+.3f} .. {(pr[:, 0].max() - gx) / H:+.3f}  (drawn -0.271 .. +0.271)")
    zr = L.project(cam, socket("socket_hand_R")[0])[2]
    zl = L.project(cam, socket("socket_hand_L")[0])[2]
    print(f"FIT near-hand margin {zl - zr:+.3f} m (must be > 0)")
elif MODE == "look":
    PZ.reset()
    PZ.use_action("Stance", 1)
    ARM.rotation_euler = (0, 0, math.radians(YAW))
    cam = cam_game(GAME_PPM * 4, (1400, 1400), YAW, PITCH, 0.85)
    bpy.context.view_layer.update()
    pr, dr = socket("socket_hand_R")
    pl, _ = socket("socket_hand_L")
    _, _, zr = L.project(cam, pr)
    _, _, zl = L.project(cam, pl)
    print(f"SIDES socket_hand_R depth {zr:.3f}  socket_hand_L depth {zl:.3f}  margin {zl - zr:.3f} m "
          "(smaller = nearer the camera)")
    assert zr < zl, "the weapon hand must be the near hand"
    print("BLADE DIR (world)", tuple(round(c, 2) for c in dr))
    show_sword(True)
    shoot("look", GAME_PPM * 4, (1400, 1400), YAW, PITCH, 0.85, "3D, sword")
    show_sword(False)
    shoot("look_bare", GAME_PPM * 4, (1400, 1400), YAW, PITCH, 0.85, "3D, bare hands")
    for part in ("Sword", "Head", "Hair", "Shirt", "Hands", "Arms", "Belt"):   # masks for the sword-crossing test
        if bpy.data.objects.get(part) is None:
            continue
        keep = {o.name: o.hide_render for o in bpy.data.objects}
        for o in bpy.data.objects:
            o.hide_render = o.type == 'MESH' and o.name != part
        shoot("mask_" + part, GAME_PPM * 4, (1400, 1400), YAW, PITCH, 0.85, part)
        for o in bpy.data.objects:
            o.hide_render = keep[o.name]
    META["sides"] = {"depth_R": zr, "depth_L": zl, "margin_m": zl - zr}
elif MODE == "turnaround":
    no_action()
    PZ.apply(PZ.pose_rest)
    show_sword(False)
    for i, (yaw, lab) in enumerate(((0, "front"), (YAW, "3/4 front (game angle)"), (90, "side"), (180, "back"))):
        shoot(f"t{i}_{int(yaw)}", GAME_PPM * 3 * 2, (1120, 1660), yaw, 0 if yaw in (0, 90, 180) else PITCH, 0.93, lab)
elif MODE == "bends":
    no_action()
    for name, label, fn in PZ.BENDS + PZ.SIDE:
        PZ.apply(fn)
        show_sword(name == "b_socket")
        shoot(name, GAME_PPM * 3 * 2, (1000, 1840), 90 if name.startswith("s_") else YAW, PITCH, 1.08, label)
elif MODE == "face_turn":
    PZ.reset()
    PZ.use_action("Stance", 1)
    no_action()
    PZ.apply(PZ.pose_rest)
    show_sword(False)
    hc, _ = socket("socket_head")
    for i, yaw in enumerate((0, 25, 45, 70, 90)):
        ARM.rotation_euler = (0, 0, math.radians(yaw))
        bpy.context.view_layer.update()
        shoot(f"f{i}_{yaw}", GAME_PPM * 3 * 3, (520, 560), yaw, PITCH, hc.z + 0.005, f"yaw {yaw}")
elif MODE == "overlay":
    no_action()
    PZ.apply(lambda: [(PZ.arm_down(s, 20), PZ.leg_out(s, 7.5)) for s in PZ.SIDES])
    show_sword(False)
    S = L.S
    for name, yaw in (("ov_front", 0), ("ov_right", 90)):
        ARM.rotation_euler = (0, 0, math.radians(yaw))
        L.render_setup((1024, 1024))
        L.ortho_camera((0, -10, (968 - 512) * S), (0, 1, 0), scale=1024 * S)
        bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
mp = os.path.join(OUT, "meta.json")
old = json.load(open(mp)) if os.path.exists(mp) else {}
old.update(META)
with open(mp, "w") as f:
    json.dump(old, f, indent=1)
print("DONE", MODE)
