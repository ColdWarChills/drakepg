#!/usr/bin/env python3
"""A clean skeleton for the hero (the designer, 2026-11-20: "DO a proper skeleton and then attach only").

Bones first, art later. Every bone has a fixed length and an angle relative to its parent (0 = straight on from the
parent), with limits where a real joint stops: elbows and knees bend one way only, the wrist a little. The hand is a
bone (wrist to knuckles) and the sword is fixed to it (same grip, same angle in the fist, always): the sword's
direction comes only from shoulder, elbow and wrist, never the other way round. Nothing is mirrored or swapped.

The standing pose comes from today's rig joints (unity/Bones/hero/rig.json, not changed); the skeleton is written to
unity/Bones/hero/skeleton.json. Drawn here in Python, outside the game (no Unity code yet).

  python scripts/skeleton.py json       write skeleton.json
  python scripts/skeleton.py standing   the bones over today's standing hero, with their lengths
  python scripts/skeleton.py clip       the bare skeleton: each arm joint through its range, then a sword cut
  python scripts/skeleton.py compare    the worst hand moment of the last clip beside the skeleton at that moment
  (--force replaces this script's own pictures)
"""
import json
import math
import os
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
BONES = ROOT / "unity" / "Bones" / "hero"
OUT = ROOT / "art" / "tests" / "sheets" / "skeleton"
RIG = json.loads((BONES / "rig.json").read_text())
P = {k: tuple(v) for k, v in RIG["joints"].items()}
# the points today's rig has no joint for (rig canvas px, read off the standing parts)
P.update({"head_top": (796, 205), "mid_hip": (783, 770), "n_knuckles": (407, 789), "f_knuckles": (1100, 787),
          "n_toe": (652, 1334), "f_toe": (1006, 1306)})

# name, parent, attached at the parent's "head" or "tail", from point, to point, limits (local deg) or None
BONE_LIST = [
    ("hips", None, None, "waist", "mid_hip", None),
    ("spine", "hips", "head", "waist", "neck", (-215, -135)),   # points up from the hips (about -174)
    ("head", "spine", "tail", "neck", "head_top", (-35, 35)),
    ("n_collar", "spine", "tail", "neck", "n_shoulder", None),     # collar and pelvis bones never move
    ("f_collar", "spine", "tail", "neck", "f_shoulder", None),
    ("n_upper", "n_collar", "tail", "n_shoulder", "n_elbow", (-250, 70)),
    ("n_fore", "n_upper", "tail", "n_elbow", "n_wrist", (-150, 0)),
    ("n_hand", "n_fore", "tail", "n_wrist", "n_knuckles", (-30, 50)),
    ("f_upper", "f_collar", "tail", "f_shoulder", "f_elbow", (-250, 70)),
    ("f_fore", "f_upper", "tail", "f_elbow", "f_wrist", (-150, 0)),
    ("f_hand", "f_fore", "tail", "f_wrist", "f_knuckles", (-30, 50)),
    ("n_pelvis", "hips", "tail", "mid_hip", "n_hip", None),
    ("f_pelvis", "hips", "tail", "mid_hip", "f_hip", None),
    ("n_thigh", "n_pelvis", "tail", "n_hip", "n_knee", None),
    ("n_shin", "n_thigh", "tail", "n_knee", "n_ankle", (-5, 150)),
    ("n_foot", "n_shin", "tail", "n_ankle", "n_toe", (-90, 30)),
    ("f_thigh", "f_pelvis", "tail", "f_hip", "f_knee", None),
    ("f_shin", "f_thigh", "tail", "f_knee", "f_ankle", (-5, 150)),
    ("f_foot", "f_shin", "tail", "f_ankle", "f_toe", (-90, 30)),
]
# the sword in the near fist: its grip point part way along the hand bone, the blade at a fixed angle to the hand
# (thumb side, towards the front); lengths from iron_sword.png (grip row 500, guard row 456, 641 rows)
SWORD = {"bone": "n_hand", "grip_along": 0.55, "angle": -112,   # -112: as in today's standing picture
         "tip": 500, "guard": 44, "pommel": 141}


def norm(a):
    return (a + 180) % 360 - 180


def ang(p, q):
    return math.degrees(math.atan2(q[1] - p[1], q[0] - p[0]))


def build():
    bones, rest = {}, {}
    for name, parent, at, a, b, lim in BONE_LIST:
        d = ang(P[a], P[b])
        local = d if parent is None else norm(d - ang(P[bones[parent]["from"]], P[bones[parent]["to"]]))
        if lim and not lim[0] <= local <= lim[1]:
            fits = [x for x in (local - 360, local + 360) if lim[0] <= x <= lim[1]]
            local = fits[0] if fits else min(max(local, lim[0]), lim[1])   # a hair past a limit: on the limit
        bones[name] = {"parent": parent, "at": at, "from": a, "to": b, "length": round(math.dist(P[a], P[b]), 1),
                       "limits": list(lim) if lim else None}
        rest[name] = round(local, 1)
    return bones, rest


BONES_DEF, STAND = build()
ORDER = [b[0] for b in BONE_LIST]


def pose_points(local, root=None):
    """Forward kinematics: {bone: (head, tail, world angle)} for local angles (missing bones: standing)."""
    out = {}
    for name in ORDER:
        b = BONES_DEF[name]
        loc = local.get(name, STAND[name])
        if b["parent"] is None:
            head, w = (root or P[b["from"]]), loc
        else:
            ph, pt, pw = out[b["parent"]]
            head, w = (ph if b["at"] == "head" else pt), pw + loc
        r = math.radians(w)
        out[name] = (head, (head[0] + b["length"] * math.cos(r), head[1] + b["length"] * math.sin(r)), w)
    return out


def sword_points(pts):
    h, t, w = pts[SWORD["bone"]]
    g = SWORD["grip_along"]
    grip = (h[0] + (t[0] - h[0]) * g, h[1] + (t[1] - h[1]) * g)
    a = math.radians(w + SWORD["angle"])
    u = (math.cos(a), math.sin(a))
    at = lambda s: (grip[0] + u[0] * s, grip[1] + u[1] * s)
    return {"grip": grip, "tip": at(SWORD["tip"]), "guard": at(SWORD["guard"]), "pommel": at(-SWORD["pommel"]),
            "angle": w + SWORD["angle"]}


# ---------------------------------------------------------------- drawing
GROUND = (118, 104, 138)
NEAR, FAR, MID = (238, 214, 168), (150, 140, 170), (214, 196, 160)
INK = (40, 32, 48)
SS = 2


def font(n):
    try:
        return ImageFont.truetype("arial.ttf", n)
    except OSError:
        return ImageFont.load_default()


def side(name):
    return FAR if name.startswith("f_") else NEAR if name.startswith("n_") else MID


def bone_poly(h, t, wide):
    """A bone: round at the joint, widest a fifth of the way, narrowing to the next joint."""
    L = math.dist(h, t)
    if L < 1:
        return [h]
    ux, uy = (t[0] - h[0]) / L, (t[1] - h[1]) / L
    px, py = -uy, ux
    m = (h[0] + ux * L * 0.2, h[1] + uy * L * 0.2)
    return [h, (m[0] + px * wide, m[1] + py * wide), t, (m[0] - px * wide, m[1] - py * wide)]


def draw_skeleton(img, pts, scale=1.0, off=(0, 0), alpha=255, labels=False, sword=True):
    d = ImageDraw.Draw(img, "RGBA")
    S = lambda p: ((p[0] - off[0]) * scale * SS, (p[1] - off[1]) * scale * SS)
    wide = lambda n: (26 if n in ("spine", "hips") else 18 if "thigh" in n or "upper" in n or n == "head" else 14) * scale * SS
    order = [n for n in ORDER if n.startswith("f_")] + [n for n in ORDER if not n.startswith(("f_", "n_"))] + \
            [n for n in ORDER if n.startswith("n_") and n != "n_hand"]
    for n in order:
        h, t, w = pts[n]
        if n.endswith("_hand"):
            draw_fist(d, S, pts[n], side(n), scale, alpha)
            continue
        d.polygon([S(p) for p in bone_poly(h, t, wide(n) / (scale * SS))], fill=side(n) + (alpha,), outline=INK + (alpha,), width=max(1, int(3 * scale * SS)))
        r = (16 if n in ("n_upper", "f_upper", "n_thigh", "f_thigh") else 11) * scale * SS
        x, y = S(h)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, alpha), outline=INK + (alpha,), width=max(1, int(3 * scale * SS)))
    if sword:
        sw = sword_points(pts)
        draw_sword(d, S, sw, scale, alpha)
    draw_fist(d, S, pts["n_hand"], NEAR, scale, alpha)
    if labels:
        f = font(int(15 * SS))
        for n in ORDER:
            h, t, w = pts[n]
            if n in ("n_pelvis", "f_pelvis", "n_collar", "f_collar"):
                continue
            mx, my = S(((h[0] + t[0]) / 2, (h[1] + t[1]) / 2))
            d.text((mx + 12 * SS, my - 8 * SS), f"{n.replace('n_', 'near ').replace('f_', 'far ')} {BONES_DEF[n]['length']:.0f}",
                   fill=(255, 255, 255), font=f, stroke_width=2 * SS, stroke_fill=(0, 0, 0))


def draw_fist(d, S, bone, colour, scale, alpha):
    """A fist on the hand bone, lopsided on purpose: the thumb bump is on the blade side, the knuckles a white bar at
    the far end, so a flipped or reversed hand would show at once."""
    h, t, w = bone
    L = math.dist(h, t)
    ux, uy = (t[0] - h[0]) / L, (t[1] - h[1]) / L
    a = math.radians(w + SWORD["angle"])       # towards the blade: the thumb side
    sx, sy = math.cos(a), math.sin(a)
    px, py = (-uy, ux) if (-uy * sx + ux * sy) > 0 else (uy, -ux)
    W = 26
    P_ = lambda al, ac: S((h[0] + ux * al + px * ac, h[1] + uy * al + py * ac))
    body = [P_(0, -W * 0.7), P_(L * 0.25, -W), P_(L, -W), P_(L * 1.05, 0), P_(L, W * 0.9), P_(L * 0.55, W * 1.25),
            P_(L * 0.3, W * 1.05), P_(0, W * 0.7)]
    d.polygon(body, fill=colour + (alpha,), outline=INK + (alpha,), width=max(1, int(3 * scale * SS)))
    d.line([P_(L * 0.97, -W * 0.8), P_(L * 0.97, W * 0.7)], fill=(255, 255, 255, alpha), width=max(2, int(7 * scale * SS)))
    d.line([P_(L * 0.35, W * 0.95), P_(L * 0.62, W * 1.15)], fill=INK + (alpha,), width=max(1, int(3 * scale * SS)))


def draw_sword(d, S, sw, scale, alpha):
    a = math.radians(sw["angle"])
    px, py = -math.sin(a), math.cos(a)
    g, tip, pom = sw["guard"], sw["tip"], sw["pommel"]
    d.line([S(pom), S(g)], fill=(110, 70, 40, alpha), width=max(2, int(12 * scale * SS)))
    blade = [(g[0] + px * 9, g[1] + py * 9), (tip[0], tip[1]), (g[0] - px * 9, g[1] - py * 9)]
    d.polygon([S(p) for p in blade], fill=(200, 205, 215, alpha), outline=INK + (alpha,), width=max(1, int(2 * scale * SS)))
    d.line([S((g[0] + px * 34, g[1] + py * 34)), S((g[0] - px * 34, g[1] - py * 34))], fill=(205, 170, 80, alpha), width=max(2, int(9 * scale * SS)))
    x, y = S(pom)
    r = 11 * scale * SS
    d.ellipse([x - r, y - r, x + r, y + r], fill=(160, 160, 170, alpha), outline=INK + (alpha,))


def canvas(w, h, bg=GROUND):
    return Image.new("RGB", (w * SS, h * SS), bg)


def done(img):
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def guard(path):
    if path.exists() and "--force" not in sys.argv:
        raise SystemExit(f"{path} exists (use --force to replace this script's own picture)")
    OUT.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------- poses
def local_of(absdirs):
    """Local angles from wanted world directions of some bones (the rest standing)."""
    pts = pose_points({})
    local = {}
    world = {}
    for name in ORDER:
        b = BONES_DEF[name]
        pw = 0 if b["parent"] is None else world[b["parent"]]
        w = absdirs.get(name, pw + STAND[name] if b["parent"] else STAND[name])
        lo = norm(w - pw) if b["parent"] else w
        lim = b["limits"]
        if lim and not lim[0] <= lo <= lim[1]:
            lo2 = lo - 360 if lo > lim[1] else lo + 360
            if lim[0] <= lo2 <= lim[1]:
                lo = lo2
        local[name], world[name] = lo, pw + lo
    return local


def near_arm(upper, fore, wrist, spine=None, **more):
    """A pose from the near arm's world directions (upper, fore) and the wrist bend (relative to the forearm)."""
    want = {"n_upper": upper, "n_fore": fore, "n_hand": fore + wrist}
    if spine is not None:
        want["spine"] = ang(P["waist"], P["neck"]) + spine
    want.update(more)
    return local_of(want)                 # the arm keeps its world directions when the spine leans


# the sword cut: four key poses (world directions; 0 = forward, -90 = up, 90 = down), the hero facing right
KEYS = {
    "ready": near_arm(105, 30, 10),
    "wind-up": near_arm(-70, -150, -20, spine=-8),
    "hit": near_arm(50, 45, 50, spine=8),
    "after": near_arm(100, 95, 40, spine=4),
}
CUT = [("ready", 10), ("wind-up", 11), ("wind-up", 4), ("hit", 3), ("after", 5), ("after", 6), ("ready", 12)]


def blend(a, b, t):
    out = {}
    for n in ORDER:
        x, y = a.get(n, STAND[n]), b.get(n, STAND[n])
        out[n] = x + (y - x) * t          # straight in local angles: stays inside every joint's limits
    return out


def ease(t):
    return 0.5 - 0.5 * math.cos(math.pi * t)


def cut_frames():
    frames, prev = [], KEYS["ready"]
    for key, n in CUT:
        for i in range(1, n + 1):
            frames.append((blend(prev, KEYS[key], ease(i / n)), key))
        prev = KEYS[key]
    return frames


def range_frames():
    base = KEYS["ready"]
    frames = []
    def run(bone, vals, label):
        for v in vals:
            p = dict(base)
            p[bone] = v
            frames.append((p, label))
    lim = lambda n: BONES_DEF[n]["limits"]
    s = base["n_fore"]
    run("n_fore", [s + (0 - s) * ease(i / 8) for i in range(9)] + [0 + (-150) * ease(i / 18) for i in range(19)]
        + [-150 + (s + 150) * ease(i / 12) for i in range(13)], "elbow: straight to fully bent (150)")
    w = base["n_hand"]
    run("n_hand", [w + (-30 - w) * ease(i / 8) for i in range(9)] + [-30 + 80 * ease(i / 16) for i in range(17)]
        + [50 + (w - 50) * ease(i / 8) for i in range(9)], "wrist: its whole range (-30 to 50)")
    u = base["n_upper"]
    run("n_upper", [u + (-230 - u) * ease(i / 20) for i in range(21)] + [-230 + (u + 230) * ease(i / 16) for i in range(17)],
        "shoulder: the arm up over the head and back")
    return frames


# ---------------------------------------------------------------- checks
def check(frames):
    worst = {"length": 0.0, "limit": 0.0, "sword angle": 0.0, "sword grip": 0.0}
    flips = set()
    g0 = None
    for local, _ in frames:
        pts = pose_points(local)
        for n in ORDER:
            h, t, w = pts[n]
            worst["length"] = max(worst["length"], abs(math.dist(h, t) - BONES_DEF[n]["length"]))
            lim = BONES_DEF[n]["limits"]
            lo = local.get(n, STAND[n])
            if lim:
                worst["limit"] = max(worst["limit"], lim[0] - lo, lo - lim[1])
        sw = sword_points(pts)
        h, t, w = pts["n_hand"]
        worst["sword angle"] = max(worst["sword angle"], abs(norm(sw["angle"] - w - SWORD["angle"])))
        gd = math.dist(sw["grip"], h)
        g0 = gd if g0 is None else g0
        worst["sword grip"] = max(worst["sword grip"], abs(gd - g0))
        hx, hy = t[0] - h[0], t[1] - h[1]
        bx, by = math.cos(math.radians(sw["angle"])), math.sin(math.radians(sw["angle"]))
        flips.add(hx * by - hy * bx > 0)          # which side of the hand the blade leaves: must never change
    return worst, len(flips) == 1


# ---------------------------------------------------------------- outputs
def standing():
    path = OUT / "skeleton_standing.png"
    guard(path)
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import bones_parts as bp
    hero, _, _ = bp.Puppet(sets=("bare_v2",)).draw({}, weapons={"n": "iron_sword"}, ground=bp.GROUND)
    box = (330, 150, 1230, 1380)
    pic = hero.convert("RGB").crop(box)
    W, H = pic.size
    left = pic.copy()
    bare, _, _ = bp.Puppet(sets=("bare_v2",)).draw({}, ground=bp.GROUND)     # no sword under the bones
    right = Image.blend(bare.convert("RGB").crop(box), Image.new("RGB", pic.size, GROUND), 0.65)
    big = right.resize((W * SS, H * SS))
    draw_skeleton(big, pose_points({}), off=box[:2], labels=True)
    right = done(big)
    sheet = Image.new("RGB", (W * 2 + 20, H + 60), (24, 24, 28))
    sheet.paste(left, (0, 0))
    sheet.paste(right, (W + 20, 0))
    d = ImageDraw.Draw(sheet)
    d.text((12, H + 12), "Today's standing hero | the new skeleton on him (lengths in rig px). The fist is a bone; "
           "the sword is fixed in it. Drawn outside the game.", fill=(220, 214, 200), font=font(20))
    sheet.save(path)
    print(path.relative_to(ROOT))
    for n in ORDER:
        b = BONES_DEF[n]
        print(f"  {n:9s} {b['length']:6.1f}  standing {STAND[n]:7.1f}  limits {b['limits']}")


def clip():
    path = OUT / "skeleton_clip.mp4"
    guard(path)
    rng, cut = range_frames(), cut_frames()
    slow = [(p, k) for p, k in cut for _ in (0, 1)]
    plan = [(p, "the joints: " + k) for p, k in rng] + [(p, f"sword cut: {k}") for p, k in cut] + \
           [(p, f"sword cut, half speed: {k}") for p, k in slow]
    worst, steady = check(rng + cut)
    print("checks over every frame:", {k: round(v, 3) for k, v in worst.items()},
          "| blade always leaves the same side of the fist:", steady)
    box = (230, 0, 1330, 1400)
    sc = 0.5
    tmp = tempfile.mkdtemp(prefix="skel_")
    f = font(int(22 * SS))
    for i, (local, label) in enumerate(plan):
        w, h = int((box[2] - box[0]) * sc), int((box[3] - box[1]) * sc)
        img = canvas(w, h + 40)
        draw_skeleton(img, pose_points(local), scale=sc, off=box[:2])
        d = ImageDraw.Draw(img)
        lo = local
        d.text((14 * SS, (h + 6) * SS), label, fill=(255, 255, 255), font=f)
        d.text((14 * SS, 10 * SS), f"elbow {-lo['n_fore']:.0f}   wrist {lo['n_hand']:.0f}", fill=(255, 255, 255), font=f,
               stroke_width=SS, stroke_fill=(0, 0, 0))
        done(img).save(os.path.join(tmp, f"c{i:04d}.png"))
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import bones_sheets as bs
    bs.run_ffmpeg(["-framerate", "24", "-i", os.path.join(tmp, "c%04d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p",
                   "-crf", "20", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", str(path)])
    print(path.relative_to(ROOT), len(plan), "frames", round(len(plan) / 24, 1), "s")
    # the key poses as stills
    tiles = []
    for key in ("ready", "wind-up", "hit", "after"):
        img = canvas(int(1100 * 0.4), int(1400 * 0.4) + 34)
        draw_skeleton(img, pose_points(KEYS[key]), scale=0.4, off=box[:2])
        ImageDraw.Draw(img).text((12 * SS, int(1400 * 0.4 + 4) * SS), key, fill=(255, 255, 255), font=font(int(22 * SS)))
        tiles.append(done(img))
    st = Image.new("RGB", (sum(t.width for t in tiles) + 8 * 3, tiles[0].height), (24, 24, 28))
    x = 0
    for t in tiles:
        st.paste(t, (x, 0))
        x += t.width + 8
    st.save(OUT / "skeleton_cut_keys.png")
    print((OUT / "skeleton_cut_keys.png").relative_to(ROOT))


def write_json():
    path = BONES / "skeleton.json"
    if path.exists() and "--force" not in sys.argv:
        raise SystemExit(f"{path} exists")
    data = {"note": "The hero's skeleton (scripts/skeleton.py). Rig canvas px; screen degrees (0 right, 90 down, "
                    "clockwise). A bone's angle is relative to its parent (0 = straight on); 'standing' is today's "
                    "standing pose. Limits are where the joint stops. The sword is fixed to the near hand bone.",
            "points": {k: [round(v[0], 1), round(v[1], 1)] for k, v in P.items()},
            "bones": {n: BONES_DEF[n] for n in ORDER}, "standing": STAND, "sword": SWORD,
            "keys": {k: {n: round(v, 1) for n, v in pose.items()} for k, pose in KEYS.items()}}
    path.write_text(json.dumps(data, indent=1))
    print(path.relative_to(ROOT))


def compare():
    """The worst hand moment of the last clip (arm_pieces_clip: the fist picture turned to follow the sword) beside
    the skeleton holding the sword at the same angle from the same shoulder, its wrist kept inside its limit."""
    path = OUT / "skeleton_vs_today.png"
    guard(path)
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import arm_pieces_clip as ac
    import bones_parts as bp
    im0, hand, at0 = ac.frame("new", 0, -45, 121.7)
    sw = ac.SW[hand]
    blade0 = ac.REST - 45 + 121.7
    G = bp.add(at0["n_hand"], bp.rot(bp.sub(tuple(sw["grip"]["at"]), tuple(sw["joints"]["n_wrist"])), blade0 - sw["grip"]["angle"]))
    worst = None
    for d in range(-80, 101, 2):                       # the sword turn of the last clip
        im, h, at, rel, fo = ac.hold_frame("new", at0["n_upper"], G, blade0 + d, hand)
        bend = norm(rel - ac.SW[h]["relation"])
        if worst is None or abs(bend) > abs(worst[0]):
            worst = (bend, d, im, at)
    bend, d, im, at = worst
    B = blade0 + d
    f_old = ang(at["n_fore"], at["n_hand"])
    u_old = ang(at["n_upper"], at["n_fore"])
    lo, hi = BONES_DEF["n_hand"]["limits"]
    wrist = min(max(norm(B - SWORD["angle"] - f_old), lo), hi)
    fore = B - SWORD["angle"] - wrist
    bendn = norm(fore - u_old)
    upper = u_old if -150 <= bendn <= 0 else (fore if bendn > 0 else fore + 150)
    local = near_arm(upper, fore, wrist)
    box, sc = (300, 330, 900, 930), 0.8
    W = int((box[2] - box[0]) * sc)
    left = im.convert("RGB").crop(box).resize((W, W), Image.LANCZOS)
    big = canvas(W, W)
    draw_skeleton(big, pose_points(local), scale=sc, off=box[:2])
    right = done(big)
    sheet = Image.new("RGB", (W * 2 + 16, W + 96), (24, 24, 28))
    sheet.paste(left, (0, 0))
    sheet.paste(right, (W + 16, 0))
    dr = ImageDraw.Draw(sheet)
    f = font(20)
    dr.text((10, W + 8), "Last clip: the fist picture is", fill=(255, 220, 200), font=f)
    dr.text((10, W + 34), f"turned {abs(bend):.0f} deg off how it was drawn", fill=(255, 220, 200), font=f)
    dr.text((W + 26, W + 8), "Skeleton, same sword, same place:", fill=(220, 255, 220), font=f)
    dr.text((W + 26, W + 34), f"wrist bent {abs(wrist):.0f} deg (it stops at {lo} / {hi})", fill=(220, 255, 220), font=f)
    dr.text((10, W + 66), "Drawn outside the game.", fill=(200, 196, 186), font=font(16))
    sheet.save(path)
    print(path.relative_to(ROOT), "worst bend", round(bend, 1), "at sword turn", d, "| skeleton wrist", round(wrist, 1),
          "elbow", round(local["n_fore"], 1))


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    {"json": write_json, "standing": standing, "clip": clip, "compare": compare}.get(cmd, lambda: print(__doc__))()
