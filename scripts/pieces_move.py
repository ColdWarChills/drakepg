#!/usr/bin/env python3
"""Today's hero in pieces, moving (PLANS "The hero's animation plan", step 2 on try 2's pieces): the parts a sword blow
needs, each tested moving at 3x, then one sword blow at game size on the Fen Road.

Built on scripts/pieces.py (today's look cut into pieces on the picture's own skeleton; imported, never edited) and
its near arm set pieces_arm (sleeve, cuff, forearm, the rod-measured fists). New here:
- an armless torso: the shirt's side under his right (near) arm painted in, its outline running inside the sleeve's
  round top (a disc on the shoulder joint), so whatever way the arm points the disc covers the edge and no painted
  shoulder is left on the torso (try 2's ghost);
- a seat piece on the hips (the trousers' top, from the belt to the crotch, carried up under the belt) with the thighs'
  tops cut round under it, and round knees, so the legs lunge without gaps;
- the far arm (his free left arm) with round ends, its drawn guard fist kept;
- the torso turning a little (narrowed across the spine, the shoulders following);
- the blow: whole-body keys, eased per beat, travel to the monster, the shout face at the hit, a smear on the swing.

  python scripts/pieces_move.py make <set>               the pieces -> unity/Bones/hero/pieces_<set>/ (never overwritten)
  python scripts/pieces_move.py test <set> <part> <out>  one part moving at 3x: shoulder | torso | legs | far | face
  python scripts/pieces_move.py keys <set> <out>         the blow's keys, whole body at 3x
  python scripts/pieces_move.py clip <set> <out.mp4>     the blow at game size, x1 then half speed, + strips
"""
import json
import math
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import skeleton as sk  # noqa: E402
import puppet as pp  # noqa: E402
import pieces as pc  # noqa: E402

ROOT = sk.ROOT
ART = pc.ART
BONES = sk.BONES
OUT = ROOT / "art" / "tests" / "sheets" / "pieces"
TRIES = ROOT / "art" / "redo" / "pieces"
SRC = pc.source("base")
J = {k: tuple(v) for k, v in SRC["joints"].items()}
HERO_H = SRC["hero_h"]
SRC_W, SRC_H = Image.open(SRC["file"]).size
pc.use_joints(SRC["joints"], HERO_H)
STAND0 = sk.pose_points({})
GAME_K = 235 / HERO_H                 # picture px -> game px

# ---------------------------------------------------------------- the armless torso
# the shirt's side under his near arm (picture px, from the collar down to where the shirt shows below the sleeve);
# between (186, 222) and (181, 317) it runs inside the sleeve's round top (radius 52 on the shoulder joint (160, 268))
TORSO_SIDE = [(203, 202), (190, 214), (183, 230), (178, 252), (176, 275), (177, 300), (180, 322), (181, 345),
              (184, 372), (190, 400), (198, 425)]
TORSO_IN = [(300, 425), (300, 202)]   # closes the polygon inside the shirt


def armless(arr, lab):
    """The picture with the near arm taken off: the shirt inside TORSO_SIDE painted where the arm lay (its nearby
    shade, a soft shadow under the armpit), the new outline given the art's warm rim light; the arm's pixels gone."""
    h, w = arr.shape[:2]
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon(TORSO_SIDE + TORSO_IN, fill=255)
    poly = np.asarray(m) > 0
    out = arr.copy()
    out[lab > 0, 3] = 0
    fill = poly & (lab > 0)
    yy, xx = np.mgrid[0:h, 0:w]
    shirt = poly & (lab == 0) & (arr[..., 3] > 200) & (xx < 225) & (yy > 250) & (yy < 330)
    base = np.median(arr[shirt][:, :3], axis=0)
    shade = np.percentile(arr[shirt][:, :3].sum(1), 20)
    dark = np.median(arr[shirt & (arr[..., :3].sum(2) <= shade)][:, :3], axis=0)
    # the armpit's shadow: darker towards the side and down the arm's line
    t = np.clip((200 - xx) / 30.0, 0, 1)
    col = base[None, None, :] * (1 - t[..., None]) + dark[None, None, :] * t[..., None]
    out[fill, :3] = col[fill].astype(np.uint8)
    out[fill, 3] = 255
    # the rim: a thin warm line along the new side (sampled off the shirt's own lit edge below the sleeve)
    edge = np.zeros((h, w), np.uint8)
    rim_img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(rim_img).line(TORSO_SIDE[:-1], fill=255, width=4)
    rim = (np.asarray(rim_img) > 0) & poly & (lab > 0)
    rc = arr[(yy > 345) & (yy < 380) & (xx > 170) & (xx < 195) & (arr[..., 3] > 200)]
    rc = rc[np.argsort(rc[:, :3].sum(1))[-max(1, len(rc) // 8):]][:, :3].mean(0)
    out[rim, :3] = rc.astype(np.uint8)
    del edge
    # cut outside the outline where the arm was (the polygon is the shirt's new edge)
    gone = (lab > 0) & ~poly
    out[gone, 3] = 0
    return out


# ---------------------------------------------------------------- the set
BODY = dict(pc.BODY)
END_R = dict(pc.END_R)
BELT_Y = 456                          # the belt's lower edge (picture px; the belt runs level from y 422)
TAIL_R = {"n_thigh": 84, "f_thigh": 72, "f_upper": 34, "f_fore": 26, "n_shin": 62, "f_shin": 58}
# the baggy trousers: a knee is as wide as the leg; the knee's round ends take only the trousers' own cloth (not the
# boot under it, not the lit outline); the ankle's round end only the boot
KNEE_R = {"n_shin": 84, "f_shin": 72}
FOOT_R = {"n_foot": 62, "f_foot": 58}           # the foot's round top inside the boot (the boot's own leather)
FEET_Z = {"n_foot": 4.5, "f_foot": 2.5}         # under their shins: the boot shaft's round end lies over the foot
# the seat (picture px): from under the belt to just above the crotch (its apex at (262, 572)), inside both legs'
# outer outlines so a thigh swinging out never leaves it hanging past the leg
SEAT = [(196, 452), (336, 452), (330, 495), (305, 525), (266, 545), (232, 528), (205, 498)]


def cloth_masks(arr):
    r, g, b, a = [arr[..., i].astype(int) for i in range(4)]
    trousers = (a > 200) & (g > r + 8) & (b > r + 5) & (g < 120)
    boot = (a > 200) & (r > g + 8) & (g > b) & (r < 150)
    return trousers, boot


def leg_side(side):
    """Pixels on that leg's side of the line between the legs (the crotch's middle, x about 262)."""
    h, w = SRC_H, SRC_W
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    dn = pc.seg(xx, yy, J["n_hip"], J["n_knee"])[0]
    df = pc.seg(xx, yy, J["f_hip"], J["f_knee"])[0]
    return (dn <= df) if side == "n" else (df < dn)


def owner_side(n, names, owner):
    """For a tail disc: only pixels owned by this piece or by its child (no other limb's pixels)."""
    child = {"n_thigh": "n_shin", "f_thigh": "f_shin", "f_upper": "f_fore", "f_fore": "f_fist", "n_shin": "n_foot",
             "f_shin": "f_foot"}[n]
    return (owner == names.index(n)) | (owner == names.index(child))


def set_dir(name):
    """A kept set: unity/Bones/hero/pieces_<name>/; a try (try_*): art/redo/pieces/sets/<name>/ (git-ignored)."""
    return TRIES / "sets" / name if name.startswith("try_") else BONES / f"pieces_{name}"


def make(set_name):
    d = set_dir(set_name)
    if d.exists():
        sys.exit(f"kept, not overwritten: {d}")
    d.mkdir(parents=True)
    TRIES.mkdir(parents=True, exist_ok=True)
    arr = pc.picture(SRC)
    lab, _, _ = pc.arm_labels(SRC, arr)
    body = armless(arr, lab)
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    rest = body[..., 3] > 0
    upper = yy < BELT_Y                 # torso (with the belt), head, far arm above the belt's lower edge; legs below
    names = list(BODY)
    score = np.stack([pc.seg(xx, yy, J[BODY[n][1]], J[BODY[n][2]])[0] / BODY[n][3] for n in names])
    legs = np.array([n.endswith(("thigh", "shin", "foot")) for n in names])
    score[legs] += np.where(upper, 99, 0)[None]
    score[~legs] += np.where(upper, 0, 99)[None]
    # the far arm hangs over the belt (its fist above it): keep it whole whatever the zone
    owner = np.argmin(score, axis=0)
    parts = {"_note": f"pieces_move.py make {set_name}: try 2's pieces (pieces_arm + today's art) with an armless "
                      "torso, a seat on the hips, round hips and knees; a/b = the piece's joints at its bone's "
                      "head/tail (picture px, scale 1).",
             "source": "base", "fist_z": 14, "sword_z": 13, "fists": {}}
    trousers, boot = cloth_masks(body)
    for i, n in enumerate(names):
        bone, a, b, _, z = BODY[n]
        z = FEET_Z.get(n, z)
        m = rest & (owner == i)
        if n in END_R:
            r = KNEE_R.get(n, FOOT_R.get(n, END_R[n]))
            disc = rest & (np.hypot(xx - J[a][0], yy - J[a][1]) <= r)
            if n.endswith("thigh"):
                disc &= ~upper
            if n in KNEE_R:
                disc &= trousers & leg_side(n[0])
            if n in FOOT_R:
                disc &= boot & leg_side(n[0])
            m |= disc
        if n in TAIL_R:                 # a round end at the tail joint too (over the next piece: the knee, the elbow)
            disc = rest & (np.hypot(xx - J[b][0], yy - J[b][1]) <= TAIL_R[n]) & owner_side(n, names, owner)
            if n.endswith("thigh"):
                disc &= trousers
            if n.endswith("shin"):
                disc &= boot
            m |= disc
        if n.endswith("thigh"):         # each thigh keeps to its own leg (the legs touch at the crotch)
            m &= leg_side(n[0])
        m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0
        m &= rest
        p, o = pc.piece_from(body, m)
        p = pp.largest_only(p)
        pc.save_piece(d, n, p, o, J[a], J[b], z, parts, bone=bone)
    # the seat: the trousers' top between the hips (over both thighs' round tops, under the belt)
    sm = Image.new("L", (w, h), 0)
    ImageDraw.Draw(sm).polygon(SEAT, fill=255)
    sm = (np.asarray(sm.filter(ImageFilter.GaussianBlur(1.5))) / 255.0)
    seat = body.copy()
    seat[..., 3] = (seat[..., 3] * sm * (~upper)).astype(np.uint8)
    p, o = pc.piece_from(seat, seat[..., 3] > 0)
    pc.save_piece(d, "seat", p, o, J["waist"], J["mid_hip"], 7, parts, bone="hips")
    # the near arm: pieces_arm as it is (sleeve with its round top, cuff inside, forearm), over the whole body
    ad, ap = pc.load_set("arm")
    for n in ("n_upper", "n_fore", "n_cuff"):
        shutil.copy(ad / ap[n]["file"], d / ap[n]["file"])
        parts[n] = dict(ap[n], z=ap[n]["z"] + pc.NEAR_Z)
    for v in ("back", "open"):
        shutil.copy(ad / ap["fists"][v]["file"], d / ap["fists"][v]["file"])
        parts["fists"][v] = ap["fists"][v]
    # the shout: the game's face_shout (cut for the rig's head, unity/Bones/hero/fight.json) brought onto today's head
    # (the rig head fits today's head at FACE_FIT: scale, then its top-left at (ox, oy) picture px)
    fj = json.loads((BONES / "fight.json").read_text())["faces"]["shout"]
    s, ox, oy = FACE_FIT
    rh = json.loads((BONES / "rig.json").read_text())["sets"]["bare"]["head"]
    f = Image.open(BONES / fj["file"]).convert("RGBA")
    f = f.resize((round(f.width * s), round(f.height * s)), Image.LANCZOS)
    f.save(d / "face_shout.png")
    fx, fy = (fj["x"] - rh["x"]) * s + ox, (fj["y"] - rh["y"]) * s + oy
    hp = parts["head"]
    parts["face_shout"] = {"file": "face_shout.png", "bone": "head", "z": hp["z"] + 0.5, "face": "shout",
                           "a": [J["neck"][0] - fx, J["neck"][1] - fy], "b": [J["head_top"][0] - fx, J["head_top"][1] - fy]}
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print("wrote", d)


FACE_FIT = (0.859, 103, 11)         # the rig's bare head on today's head (best fit, mean colour error 36 / 765)


def load(set_name):
    d = set_dir(set_name)
    return d, json.loads((d / "parts.json").read_text())


# ---------------------------------------------------------------- drawing
def piece_affine(p, bone_pts, wide=1.0):
    q = dict(p)
    q["wide"] = p.get("wide", 1.0) * wide
    return pp.piece_affine(q, bone_pts)


def items(parts, d, pts, fist="back", sword=True, turn=1.0, extra=(), face=None):
    """[(z, draw)] for every piece of the set on the posed bones; turn narrows the torso across the spine; face: a
    face piece laid on the head ("shout"), or none."""
    out = pp.arm_items(parts, d, pts, fist, sword) if "n_upper" in parts else []    # (pieces.py adds the cuff)
    for name, p in parts.items():
        if not isinstance(p, dict) or "bone" not in p or name in ("n_upper", "n_fore", "n_cuff", "fists"):
            continue
        if p.get("face") and p["face"] != face:
            continue
        o, ex, ey = piece_affine(p, pts[p["bone"]], turn if name == "torso" else 1.0)
        out.append((p["z"], lambda c, k, off, f=d / p["file"], o=o, ex=ex, ey=ey: pp.draw_affine(c, pp.pic(f), o, ex, ey, k, off)))
    out.extend(extra)
    return out


def posed(pose):
    """Forward kinematics for a pose; a torso turn (narrowed across the spine) narrows the collars the same way, so
    the shoulders stay in the shirt's armholes."""
    turn = pose.get("turn", 1.0)
    local = dict(pose.get("local", {}))
    saved = {}
    if abs(turn - 1) > 1e-6:
        sp = STAND0["spine"][2]
        for c in ("n_collar", "f_collar"):
            h, t, w = STAND0[c]
            rel = math.radians(w - sp)
            L = sk.BONES_DEF[c]["length"]
            al, ac = L * math.cos(rel), L * math.sin(rel) * turn
            saved[c] = L
            sk.BONES_DEF[c]["length"] = math.hypot(al, ac)
            local[c] = math.degrees(math.atan2(ac, al))
    pts = sk.pose_points(local, pose.get("root"))
    for c, L in saved.items():
        sk.BONES_DEF[c]["length"] = L
    return pts


def render(set_name, pose, k, off, size, bg=None):
    d, parts = load(set_name)
    pts = posed(pose)
    img = pp.draw_items(items(parts, d, pts, pose.get("fist", "back"), True, pose.get("turn", 1.0),
                              pose.get("extra", ()), pose.get("face")), k, off, size, bg)
    return img, pts


# ---------------------------------------------------------------- poses: world directions -> local angles
def local_of(want):
    """Local angles from wanted world directions (deg; the rest standing), kept continuous: no wrapping to +-180,
    so a blend between two keys turns each joint the short way the keys were written for."""
    local, world = {}, {}
    for name in sk.ORDER:
        b = sk.BONES_DEF[name]
        pw = 0 if b["parent"] is None else world[b["parent"]]
        w0 = STAND0[name][2]
        w = want.get(name, (pw + sk.STAND[name]) if b["parent"] else sk.STAND[name])
        lo = w - pw if b["parent"] else w
        local[name], world[name] = lo, pw + lo
    return local


def arm_pose(upper, elbow, wrist, **more):
    """The near arm from the upper arm's world direction and the elbow and wrist bends (as pieces.READY)."""
    fore = upper + elbow
    want = {"n_upper": upper, "n_fore": fore, "n_hand": fore + wrist}
    want.update(more)
    return local_of(want)


def test_shoulder(set_name, out):
    """The near arm raised from hanging to over the head, the elbow and fist as in the wind-up, at 3x game size."""
    k = 3 * GAME_K
    cols = [(108, -75, 35), (60, -60, 30), (0, -60, 30), (-45, -60, 20), (-90, -60, 10), (-120, -40, 0), (-150, -20, 0)]
    cell = (340, 520)
    sheet = Image.new("RGB", (cell[0] * len(cols), cell[1] + 30), (226, 222, 210))
    dr = ImageDraw.Draw(sheet)
    for j, (u, e, wr) in enumerate(cols):
        local = arm_pose(u, e, wr)
        sh = STAND0["n_upper"][0]
        off = (sh[0] - cell[0] / k * 0.45, sh[1] - cell[1] / k * 0.55)
        img, _ = render(set_name, {"local": local}, k, off, cell, (226, 222, 210, 255))
        sheet.paste(img.convert("RGB"), (j * cell[0], 30))
        dr.text((j * cell[0] + 8, 6), f"upper arm {u}", fill=(40, 30, 30), font=sk.font(18))
    sheet.save(out)
    print("wrote", out)


GROUND_Y = max(J["n_toe"][1], J["f_toe"][1])     # the soles' line, picture px (standing)


def body(arm=(108, -75, 35), far=None, spine=0.0, head=0.0, legs=None, hips=0.0, turn=1.0, plant="f", x=0.0,
         fist="back", face=None, lift=0.0):
    """A whole-body pose. arm: the near arm (upper arm world direction, elbow bend, wrist bend). far: the far arm as
    (upper, forearm, hand) world directions (None: as drawn, the guard fist up). spine, head, hips: turned from
    standing (deg, + leans forward). legs: world directions (n_thigh, n_shin, f_thigh, f_shin). The feet stay flat
    (their standing world angles). plant: the foot that stays where it stood ("n" or "f"), moved x forward; the other
    foot's sole is put on the ground too if it would sink (the hips drop so the lowest sole is on the ground)."""
    want = {"hips": STAND0["hips"][2] + hips, "spine": STAND0["spine"][2] + spine, "head": STAND0["head"][2] + head,
            "n_foot": STAND0["n_foot"][2], "f_foot": STAND0["f_foot"][2]}
    u, e, w = arm
    want.update({"n_upper": u, "n_fore": u + e, "n_hand": u + e + w})
    if far:
        want.update({"f_upper": far[0], "f_fore": far[1], "f_hand": far[2]})
    if legs:
        want.update(dict(zip(["n_thigh", "n_shin", "f_thigh", "f_shin"], legs)))
    local = local_of(want)
    pts = sk.pose_points(local)
    toe = {s: pts[f"{s}_foot"][1] for s in "nf"}
    heel = {s: pts[f"{s}_foot"][0] for s in "nf"}
    T0 = {s: STAND0[f"{s}_foot"][1] for s in "nf"}
    dx = T0[plant][0] + x - toe[plant][0]
    dy = max(T0[s][1] - toe[s][1] for s in "nf")      # the lowest sole stays on the ground (the other is lifted)
    w0 = STAND0["hips"][0]
    return {"local": local, "root": (w0[0] + dx, w0[1] + dy - lift), "turn": turn, "fist": fist, "face": face}


def body_sheet(set_name, poses, out, k=None, cell=(520, 760)):
    """Whole-body poses side by side at 3x game size, feet on one ground line."""
    k = k or 3 * GAME_K
    sheet = Image.new("RGB", (cell[0] * len(poses), cell[1] + 30), (226, 222, 210))
    dr = ImageDraw.Draw(sheet)
    for j, (label, pose) in enumerate(poses):
        cx = pose["root"][0]
        off = (cx - cell[0] / k * 0.55, GROUND_Y + 20 - cell[1] / k)
        img, _ = render(set_name, pose, k, off, cell, (226, 222, 210, 255))
        sheet.paste(img.convert("RGB"), (j * cell[0], 30))
        dr.text((j * cell[0] + 8, 6), label, fill=(40, 30, 30), font=sk.font(18))
    sheet.save(out)
    print("wrote", out)


def test_legs(set_name, out):
    L0 = tuple(STAND0[n][2] for n in ("n_thigh", "n_shin", "f_thigh", "f_shin"))
    poses = [("standing", body(legs=L0)),
             ("half lunge", body(legs=(112, 118, 55, 100), hips=4)),
             ("lunge", body(legs=(122, 128, 42, 104), hips=8, spine=10)),
             ("deep lunge", body(legs=(130, 135, 30, 108), hips=12, spine=18)),
             ("crouch back", body(legs=(98, 120, 62, 88), hips=-6, spine=-10, plant="n"))]
    body_sheet(set_name, poses, out)


def test_torso(set_name, out):
    poses = [("standing", body()), ("lean fwd 12", body(spine=12, head=6)), ("lean fwd 22", body(spine=22, head=10)),
             ("lean back 12", body(spine=-12, head=-6)), ("turn 0.9", body(turn=0.9)), ("turn 0.82 lean 15", body(turn=0.82, spine=15))]
    body_sheet(set_name, poses, out)


def test_far(set_name, out):
    poses = [("as drawn", body()), ("fist at the hip", body(far=(95, 20, 0))), ("fist at the hip 2", body(far=(105, 45, 20))),
             ("forward guard", body(far=(40, -40, -60))), ("chest", body(far=(80, -60, -80)))]
    body_sheet(set_name, poses, out)


TESTS = {"shoulder": test_shoulder, "legs": test_legs, "torso": test_torso, "far": test_far}


# ---------------------------------------------------------------- the blow
G = 1 / GAME_K                         # game px -> picture px
FAR0 = tuple(STAND0[n][2] for n in ("f_upper", "f_fore", "f_hand"))
LEGS0 = tuple(STAND0[n][2] for n in ("n_thigh", "n_shin", "f_thigh", "f_shin"))
HERO_X, MON_X = 330, 950               # BattleScreen staging (game px), as pieces.still
# how far the front foot travels for the hit (game px): the blade's tip a little into the Bog Lurker's snout
TRAVEL = 300 * G
# Whole-body keys (world directions, deg: 0 forward, 90 down, -90 up), written so every blend turns the short,
# intended way. arm = (upper arm, elbow bend, wrist bend); the blade leaves the back fist at its own grip, -87 deg
# from the hand bone (pieces_arm's fist_back), so blade = upper + elbow + wrist - 87.
KEYS = {
    # standing ready (try 2's still): the blade up and forward, his left fist up in guard
    "ready": dict(arm=(108, -75, 35), far=FAR0, spine=0, head=0, legs=LEGS0, hips=0, turn=1.0, x=0),
    # the rise goes round the back (the upper arm turns 108 -> 205 -> 298), so the fist, forearm and blade never
    # pass in front of his face; the later keys are written one turn on (+360) so every blend stays continuous
    "lift": dict(arm=(205, -25, 30), far=(60, -20, -40), spine=-6, head=-3, legs=(98, 120, 62, 88), hips=-4,
                 turn=0.94, x=-4 * G),
    # wind-up: the upper arm raised up and forward (reached round the back), the elbow folded so the fist is up over his head, the blade lying back;
    # weight on the back foot, leaning back and turned a little away, the free fist reaching at the enemy
    "wind": dict(arm=(298, -58, 45), far=(40, -40, -60), spine=-10, head=-6, legs=(98, 120, 62, 88), hips=-6,
                 turn=0.92, x=-6 * G),
    # the coil's hold: a little further back (the strain)
    "wind2": dict(arm=(293, -60, 44), far=(38, -42, -62), spine=-12, head=-7, legs=(97, 121, 63, 89), hips=-7,
                  turn=0.9, x=-8 * G),
    # the hit: lunge, the arm long and reaching, the blade on a slant at the snout; the free fist pulled back behind
    # the hip (a clear fist there, not a blob between the thighs)
    "hit": dict(arm=(406, -2, 67), far=(180, 120, 130), spine=42, head=18, legs=(138, 140, 26, 112), hips=18, turn=0.86,
                x=TRAVEL, face="shout"),
    # follow-through: the blade carries on down and forward
    "after": dict(arm=(460, -6, 52), far=(178, 115, 120), spine=32, head=12, legs=(134, 137, 30, 110), hips=14,
                  turn=0.92, x=TRAVEL, face="shout"),
}
# beats (frames at 60 fps): from key, to key, frames, ease, per-part delays (frames: joints break in order), hop
SEGMENTS = [
    ("ready", "ready", 4, "linear", {}, 0),
    ("ready", "lift", 5, "quad_in", {}, 0),                 # the arm swings down and back behind him
    ("lift", "wind", 5, "quad_out", {"arm": 1}, 0),         # and on up over his head
    ("wind", "wind2", 4, "linear", {}, 0),
    ("wind2", "hit", 7, "quad_in", {"spine": 1, "head": 1.5, "upper": 1.5, "fore": 2.2, "hand": 2.8, "far": 1}, 14),
    ("hit", "hit", 4, "linear", {}, 0),                     # the contact freeze
    ("hit", "after", 7, "quad_out", {"hand": 0.5, "spine": 1.5}, 0),
    ("after", "after", 3, "linear", {}, 0),
    ("after", "ready_r", 14, "sine", {"arm": 2}, 16),
]
KEYS["ready_r"] = dict(KEYS["ready"], arm=(108 + 360, -75, 35))   # ready again, one turn on
STRIKE0 = sum(s[2] for s in SEGMENTS[:4])
CONTACT = sum(s[2] for s in SEGMENTS[:5])          # the frame of the hit
TOTAL = sum(s[2] for s in SEGMENTS)
EASE = {"linear": lambda t: t, "cubic_out": lambda t: 1 - (1 - t) ** 3, "quad_in": lambda t: t * t,
        "quad_out": lambda t: 1 - (1 - t) ** 2, "sine": lambda t: 0.5 - 0.5 * math.cos(math.pi * t)}
X_EASE = {"quad_in": EASE["sine"]}     # the dash: the body arrives as the arm whips


def lerp(a, b, t):
    if isinstance(a, tuple):
        return tuple(x + (y - x) * t for x, y in zip(a, b))
    return a + (b - a) * t


def pose_at(f):
    """The blow's pose at frame f (60 fps, any fraction): body() parameters blended part by part, feet planted."""
    t0 = 0.0
    for idx, (a, b, n, ease, delay, hop) in enumerate(SEGMENTS):
        if f <= t0 + n or idx == len(SEGMENTS) - 1:
            i = min(max(f - t0, 0.0), n)
            A, B = KEYS[a], KEYS[b]
            D = max(delay.values()) if delay else 0.0

            def tt(part, group=None):
                d = delay.get(part, delay.get(group, 0.0))
                u = min(max((i - d) / max(n - D, 1e-6), 0.0), 1.0)
                return (X_EASE.get(ease, EASE[ease]) if part == "x" else EASE[ease])(u)
            au, ae, aw = A["arm"]
            bu, be, bw = B["arm"]
            up = lerp(au, bu, tt("upper", "arm"))                    # each arm bone on its own delay (world dirs)
            fo = lerp(au + ae, bu + be, tt("fore", "arm"))
            ha = lerp(au + ae + aw, bu + be + bw, tt("hand", "arm"))
            # a face comes on at the key that has it (the shout on the contact frame) and goes early on the way out
            face = (A.get("face") if i < n - 1e-6 else B.get("face")) if B.get("face") else                 (A.get("face") if i < 0.25 * n else None)
            p = body(arm=(up, fo - up, ha - fo), far=lerp(A["far"], B["far"], tt("far")),
                     spine=lerp(A["spine"], B["spine"], tt("spine")), head=lerp(A["head"], B["head"], tt("head")),
                     legs=lerp(A["legs"], B["legs"], tt("legs")), hips=lerp(A["hips"], B["hips"], tt("hips")),
                     turn=lerp(A["turn"], B["turn"], tt("spine")), x=lerp(A["x"], B["x"], tt("x")),
                     lift=hop * G * math.sin(math.pi * i / n), face=face)
            p["key"] = f"{a} > {b}" if a != b else a
            return p
        t0 += n
    raise ValueError(f)


def blade_points(set_name, pts):
    """The sword's guard and tip (picture px) on the posed hand, as the renderer places it."""
    d, parts = load(set_name)
    fist = parts["fists"]["back"]
    o, ex, ey = pp.piece_affine(fist, pts["n_hand"])
    so, sex, sey = pc.sword_affine(fist, o, ex, ey)
    P = lambda q: (so[0] + q[0] * sex[0] + q[1] * sey[0], so[1] + q[0] * sex[1] + q[1] * sey[1])
    top = pp.pic(pp.SWORD).getbbox()[1]
    return P((pp.SWORD_GRIP[0], pp.SWORD_GUARD)), P((pp.SWORD_GRIP[0], top + 10))


def check_frames(step=0.5):
    """Every frame: the elbow folds one way only (forward), knees one way; the largest step of each joint. The wrist
    past skeleton.py's range (-30..50) is listed too (as ("wrist", frame, bend)), not as a fault."""
    worst, bad, prev = {}, [], None
    lo, hi = -30, 50          # skeleton.py BONES n_hand (use_joints drops the limits)
    f = 0.0
    while f <= TOTAL:
        loc = pose_at(f)["local"]
        if loc["n_fore"] > 2 or loc["n_fore"] < -155:
            bad.append((f, "near elbow", round(loc["n_fore"], 1)))
        if not lo <= loc["n_hand"] <= hi:
            bad.append((f, "wrist (beyond skeleton range)", round(loc["n_hand"], 1)))
        for kn in ("n_shin", "f_shin"):
            if loc[kn] < -3:
                bad.append((f, kn, round(loc[kn], 1)))
        if prev:
            for n in sk.ORDER:
                worst[n] = max(worst.get(n, 0), abs(loc[n] - prev[n]))
        prev = loc
        f += step
    return bad, worst


# ---------------------------------------------------------------- staging (pieces.still's battle staging)
S2 = 2
W2, H2, RAISE2, GROUND2, CUT2 = 1280 * S2, 720 * S2, 90 * S2, 500 * S2, 600 * S2
K2 = 235 * S2 / HERO_H                  # picture px -> 2x frame px
ANCHOR = (J["waist"][0], GROUND_Y + 12)  # the standing hero's middle at the soles (pieces.still)
_stage = {}


def backdrop():
    if "bg" not in _stage:
        bg = Image.open(ART / "bg_fen_road@2x.webp").convert("RGBA")
        sc = max(W2 / bg.width, H2 / bg.height)
        bg = bg.resize((round(bg.width * sc), round(bg.height * sc)), Image.LANCZOS)
        frame = Image.new("RGBA", (W2, H2), (20, 16, 28, 255))
        frame.alpha_composite(bg, ((W2 - bg.width) // 2, (H2 - bg.height) // 2 - RAISE2))
        mon = Image.open(ART / "mon_bog_lurker.webp").convert("RGBA").transpose(Image.FLIP_LEFT_RIGHT)
        mw = 340 * S2
        mon = mon.resize((mw, round(mon.height * mw / mon.width)), Image.LANCZOS)
        _stage.update(bg=frame, mon=mon)
    return _stage["bg"], _stage["mon"]


def to_frame(p):
    return (HERO_X * S2 + (p[0] - ANCHOR[0]) * K2, GROUND2 + (p[1] - ANCHOR[1]) * K2)


def stage(set_name, pose, smear=(), flash=0.0, mon_dx=0.0):
    """One frame at game size (1280 x 600: the battle cut above the menu), made at 2x, then down to 1x."""
    bg, mon = backdrop()
    frame = bg.copy()
    pts = posed(pose)
    shadow = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(shadow)
    fx = to_frame(((pts["n_foot"][1][0] + pts["f_foot"][1][0]) / 2, 0))[0] + 20 * S2
    gd.ellipse([fx - 80 * S2, GROUND2 - 9 * S2, fx + 80 * S2, GROUND2 + 7 * S2], fill=(30, 22, 14, 80))
    mx = MON_X * S2 + mon_dx * S2
    gd.ellipse([mx - 150 * S2, GROUND2 - 10 * S2, mx + 160 * S2, GROUND2 + 8 * S2], fill=(30, 22, 14, 80))
    frame.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6 * S2)))
    m = mon
    if flash > 0:
        white = Image.new("RGBA", m.size, (255, 255, 255, 255))
        white.putalpha(m.getchannel("A").point(lambda v: int(v * flash)))
        m = m.copy()
        m.alpha_composite(white)
    frame.alpha_composite(m, (round(mx - m.width // 2), GROUND2 - m.getbbox()[3]))
    if smear:                           # crescents between the guard's and the tip's last places, under the hero
        lay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
        dr = ImageDraw.Draw(lay)
        for (g0, t0), (g1, t1), al in smear:
            dr.polygon([to_frame(g0), to_frame(t0), to_frame(t1), to_frame(g1)], fill=(255, 250, 235, int(255 * al)))
        frame.alpha_composite(lay.filter(ImageFilter.GaussianBlur(1.0 * S2)))
    root = pose["root"]                 # the hero: drawn at twice the frame's scale, then down (clean edges)
    off = (root[0] - 700, -80)
    kk = K2 * 2
    size = (round(1500 * kk), round(1150 * kk))
    hero, _ = render(set_name, pose, kk, off, size)
    hero = hero.resize((size[0] // 2, size[1] // 2), Image.LANCZOS)
    o = to_frame(off)
    frame.alpha_composite(hero, (round(o[0]), round(o[1])))
    frame = frame.crop((0, 0, W2, CUT2)).convert("RGB")
    return frame.resize((W2 // S2, CUT2 // S2), Image.LANCZOS), pts


def smear_at(set_name, f, root, span=3.0, sub=0.25, inner=0.75):
    """The swing's smear at frame f: a thin crescent along the tip's path over the last `span` frames of the strike
    (sampled every `sub` frame, so the arc is smooth), from the tip in to `inner` of the blade; brightest at the
    newest edge, fading behind. It follows the body (the blade's places relative to the hips), so the dash doesn't
    stretch it. Only from the strike to just after contact."""
    if not STRIKE0 + 1 <= f <= CONTACT + 2:
        return []
    fade = 1.0 if f <= CONTACT else 0.55 if f <= CONTACT + 1 else 0.25
    t = max(STRIKE0, f - span)
    pts = []
    while t <= min(f, CONTACT) + 1e-6:
        p = pose_at(t)
        g, tip = blade_points(set_name, posed(p))
        r = p["root"]
        q = (g[0] + (tip[0] - g[0]) * inner, g[1] + (tip[1] - g[1]) * inner)
        pts.append(((q[0] - r[0], q[1] - r[1]), (tip[0] - r[0], tip[1] - r[1])))
        t += sub
    A = lambda v: (v[0] + root[0], v[1] + root[1])
    n = len(pts)
    return [((A(pts[j - 1][0]), A(pts[j - 1][1])), (A(pts[j][0]), A(pts[j][1])), 0.95 * fade * (j / n) ** 1.2)
            for j in range(1, n)]


def run(set_name, step):
    """[(frame, pose, pts, f)] for the blow sampled every `step` frames (1: x1 at 60 fps; 0.5: half speed, real
    in-betweens)."""
    out = []
    f = 0.0
    while f <= TOTAL + 1e-6:
        pose = pose_at(f)
        pts = posed(pose)
        smear = smear_at(set_name, f, pose["root"])
        fl = 0.85 if CONTACT <= f < CONTACT + 2 else 0.35 if CONTACT + 2 <= f < CONTACT + 4 else 0.0
        kick = 6 * max(0.0, 1 - (f - CONTACT - 1) / 11) if CONTACT + 1 <= f < CONTACT + 12 else 0.0
        img, _ = stage(set_name, pose, smear, fl, kick)
        out.append((img, pose, pts, f))
        f += step
    return out


def clip(set_name, out):
    out = Path(out)
    if out.exists():
        sys.exit(f"kept, not overwritten: {out}")
    bad, worst = check_frames(0.5)
    print("joint faults (and the wrist past skeleton.py's range):", bad or "none")
    print("largest turn per half frame (deg, > 6):", {k: round(v, 1) for k, v in worst.items() if v > 6})
    x1 = run(set_name, 1.0)
    half = run(set_name, 0.5)
    tmp = Path(tempfile.mkdtemp())
    seq = [(img, "x1") for img, *_ in x1] + [(x1[-1][0], "x1")] * 20 + \
          [(img, "half speed") for img, *_ in half] + [(half[-1][0], "half speed")] * 20
    for i, (img, lab) in enumerate(seq):
        im = img.copy()
        ImageDraw.Draw(im).text((14, 12), lab, fill=(255, 255, 255), font=sk.font(22), stroke_width=2,
                                stroke_fill=(0, 0, 0))
        im.save(tmp / f"f{i:04d}.png")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "60", "-i", str(tmp / "f%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", str(out)], check=True)
    print("wrote", out, len(seq), "frames,", TOTAL, "frames at x1 =", round(TOTAL / 60, 2), "s")
    strips(set_name, x1, out.with_name(out.stem + "_frames.png"), out.with_name(out.stem + "_hand_3x.png"))
    return x1


def strips(set_name, x1, out_frames, out_hand):
    """A frame strip at game size (crops around the hero) and the sword hand at 3x game size, the same frames."""
    picks = sorted({0, 7, 9, 13, STRIKE0 + 2, STRIKE0 + 4, STRIKE0 + 6, CONTACT, CONTACT + 5, CONTACT + 9,
                    CONTACT + 15, TOTAL - 1})
    cw, ch = 420, 340
    sheet = Image.new("RGB", (cw * 6, (ch + 26) * 2), (24, 22, 28))
    hsheet = Image.new("RGB", (330 * 6, 356 * 2), (24, 22, 28))
    dr, hd = ImageDraw.Draw(sheet), ImageDraw.Draw(hsheet)
    k3 = 3 * GAME_K
    for j, fi in enumerate(picks[:12]):
        img, pose, pts, f = x1[min(fi, len(x1) - 1)]
        cx = to_frame(pose["root"])[0] / S2
        x0 = int(min(max(cx - cw * 0.45, 0), 1280 - cw))
        c, r = j % 6, j // 6
        sheet.paste(img.crop((x0, 600 - ch - 30, x0 + cw, 600 - 30)), (c * cw, r * (ch + 26) + 26))
        dr.text((c * cw + 6, r * (ch + 26) + 4), f"frame {fi}: {pose['key']}", fill=(235, 230, 220), font=sk.font(16))
        hand = pts["n_hand"][0]
        him, _ = render(set_name, pose, k3, (hand[0] - 165 / k3, hand[1] - 165 / k3), (330, 330), (196, 186, 160, 255))
        hsheet.paste(him.convert("RGB"), (c * 330, r * 356 + 26))
        hd.text((c * 330 + 6, r * 356 + 4), f"frame {fi}: {pose['key']}", fill=(235, 230, 220), font=sk.font(16))
    for o, im in ((out_frames, sheet), (out_hand, hsheet)):
        if Path(o).exists():
            sys.exit(f"kept, not overwritten: {o}")
        im.save(o)
        print("wrote", o)


def keys_sheet(set_name, out):
    """The blow's keys side by side at 3x game size (no travel)."""
    poses = []
    for k in ("ready", "wind", "hit", "after"):
        p = body(**dict(KEYS[k], x=0))
        poses.append((k, p))
    body_sheet(set_name, poses, out, cell=(760, 760))


def main():
    a = sys.argv[1:]
    if not a:
        print(__doc__)
    elif a[0] == "make":
        make(a[1])
    elif a[0] == "test":
        TESTS[a[2]](a[1], a[3])
    elif a[0] == "keys":
        keys_sheet(a[1], a[2])
    elif a[0] == "clip":
        clip(a[1], a[2])
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
