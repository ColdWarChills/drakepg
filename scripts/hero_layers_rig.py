#!/usr/bin/env python3
"""Layered hero TEST, part 2: joints, masks, parts, poses (see hero_layers.py and docs/PLANS.md).

Every part is a crop of the same 1536 canvas (the rig pose), so with every angle at 0 the parts sit where they were
drawn. A joint is a pivot and a radius: the child is cut at a line through the pivot square to its bone, and the parent
keeps the child's pixels inside the joint's circle, so a turned limb opens no gap. A gear piece is an edit of the rig
pose cut by the same masks. Angles are world turns from the rig pose, clockwise on screen, in degrees.

Usage (from the repo root):
  python scripts/hero_layers_rig.py cut            # art/redo/layers/parts/<set>/<part>.png + rig.json
  python scripts/hero_layers_rig.py diff           # how far each gear edit strays from the rig pose
  python scripts/hero_layers_rig.py show [pose..]  # art/redo/layers/_poses.png (the Python preview of the poses)
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import hero_layers as hl  # noqa: E402
import hero_poses as hp  # noqa: E402

OUT = hl.OUT
PARTS = OUT / "parts"
N = hp.CANVAS
HERO_PX = 1034  # the game's bare hero is this tall on the same canvas: weapon sizes are fractions of it

# n = the near arm and leg (the hero's right, on the left of the picture, the main hand), f = the far ones.
J = {
    "waist": (780, 690), "neck": (800, 425),
    "n_shoulder": (688, 495), "n_elbow": (598, 618), "n_wrist": (468, 742),
    "f_shoulder": (843, 500), "f_elbow": (965, 615), "f_wrist": (1062, 735),
    "n_hip": (738, 770), "n_knee": (680, 985), "n_ankle": (612, 1272),
    "f_hip": (828, 770), "f_knee": (848, 985), "f_ankle": (872, 1262),
}
RADIUS = {"neck": 34, "n_shoulder": 62, "f_shoulder": 62, "n_hip": 70, "f_hip": 65}
NEAR_ARM = [(700, 380), (704, 520), (668, 562), (636, 640), (620, 900), (380, 900), (380, 405), (560, 405), (640, 380)]
FAR_ARM = [(835, 380), (842, 470), (848, 572), (870, 640), (900, 700), (950, 900), (1180, 900), (1180, 380)]
NECK_Y, BELT_TOP, BELT_BOTTOM, LEG_SPLIT = 425, 655, 700, 772

# name: (parent, joint it turns on, draw order). Back to front.
TREE = {
    "hips": (None, "waist", 30), "torso": ("hips", "waist", 50), "head": ("torso", "neck", 60),
    "n_upper": ("torso", "n_shoulder", 74), "n_fore": ("n_upper", "n_elbow", 70), "n_hand": ("n_fore", "n_wrist", 72),
    "f_upper": ("torso", "f_shoulder", 10), "f_fore": ("f_upper", "f_elbow", 8), "f_hand": ("f_fore", "f_wrist", 9.4),
    "n_thigh": ("hips", "n_hip", 42), "n_shin": ("n_thigh", "n_knee", 40), "n_foot": ("n_shin", "n_ankle", 41),
    "f_thigh": ("hips", "f_hip", 22), "f_shin": ("f_thigh", "f_knee", 20), "f_foot": ("f_shin", "f_ankle", 21),
}
WEAPON_Z = {"n": 71, "f": 9.2}  # behind its fist, in front of its forearm
FAR_FRONT = {"f_fore": 76, "f_hand": 78, "f_weapon": 77}  # a far hand that reaches in front of the body
# Which parts a gear piece redraws.
SLOTS = {
    "chest": ["torso", "n_upper", "n_fore", "f_upper", "f_fore"],
    "legs": ["hips", "n_thigh", "n_shin", "n_foot", "f_thigh", "f_shin", "f_foot"],
    "helm": ["head"],
}
GEAR = {"chest_leather": "chest", "chest_scale": "chest", "helm_cap": "helm", "helm_coif": "helm",
        "legs_plain": "legs", "legs_plate": "legs"}
WEAPONS = {"iron_sword": ("wpn_iron_sword", 0.78, 0.62), "oak_staff": ("wpn_oak_staff", 0.45, 1.0),
           "rusty_dagger": ("wpn_rusty_dagger", 0.89, 0.3)}

yy, xx = np.mgrid[:N, :N]


def load(name: str) -> np.ndarray:
    return np.asarray(hp.cleaned(OUT / f"{name}_s{hl.PICKS[name]}.png"))


def poly(points) -> np.ndarray:
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    return np.asarray(m) > 0


def beyond(p, q) -> np.ndarray:
    """Pixels past the line through p square to p->q."""
    return (xx - p[0]) * (q[0] - p[0]) + (yy - p[1]) * (q[1] - p[1]) > 0


def disc(c, r) -> np.ndarray:
    return (xx - c[0]) ** 2 + (yy - c[1]) ** 2 <= r * r


def refine(alpha: np.ndarray) -> None:
    """Centre the elbow, wrist, knee and ankle pivots in their limb and size their circles from its width."""
    near, far = poly(NEAR_ARM), poly(FAR_ARM)
    body = ~(near | far)
    lim = {"n_elbow": near, "n_wrist": near, "f_elbow": far, "f_wrist": far,
           "n_knee": body & (xx < LEG_SPLIT), "n_ankle": body & (xx < LEG_SPLIT),
           "f_knee": body & (xx >= LEG_SPLIT), "f_ankle": body & (xx >= LEG_SPLIT)}
    before = {"elbow": "shoulder", "wrist": "elbow", "knee": "hip", "ankle": "knee"}
    for j, m in lim.items():
        s, kind = j.split("_")
        a, b = np.array(J[f"{s}_{before[kind]}"], float), np.array(J[j], float)
        d = (b - a) / np.linalg.norm(b - a)
        nrm = np.array([-d[1], d[0]])
        ts = np.arange(-130, 131)
        pts = np.rint(b[None] + ts[:, None] * nrm[None]).astype(int)
        on = (alpha[pts[:, 1], pts[:, 0]] > 128) & m[pts[:, 1], pts[:, 0]]
        i = int(np.argmin(np.where(on, np.abs(ts), 999)))
        lo = hi = i
        while lo > 0 and on[lo - 1]:
            lo -= 1
        while hi < len(ts) - 1 and on[hi + 1]:
            hi += 1
        mid = (ts[lo] + ts[hi]) / 2
        J[j] = tuple(np.round(b + mid * nrm, 1))
        RADIUS[j] = round((ts[hi] - ts[lo]) / 2 + 3)


def loose(alpha: np.ndarray, zone: np.ndarray, seed) -> np.ndarray:
    """The drawn pixels of a zone that do not hang together with its limb (a headband tail, a bit of collar)."""
    m = Image.fromarray(((alpha > 128) & zone).astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(m, (round(seed[0]), round(seed[1])), 128)
    return np.asarray(m) == 255


def territories(alpha: np.ndarray) -> dict[str, np.ndarray]:
    near, far = poly(NEAR_ARM), poly(FAR_ARM)
    for s, arm in (("n", near), ("f", far)):
        a, b = J[f"{s}_shoulder"], J[f"{s}_elbow"]
        arm &= ~loose(alpha, arm, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
    t = {}
    for s, arm in (("n", near), ("f", far)):
        e, w = J[f"{s}_elbow"], J[f"{s}_wrist"]
        past_e = beyond(e, w)
        past_w = beyond(w, (2 * w[0] - e[0], 2 * w[1] - e[1]))
        t[f"{s}_upper"], t[f"{s}_fore"], t[f"{s}_hand"] = arm & ~past_e, arm & past_e & ~past_w, arm & past_w
    arms = near | far
    head = (yy < NECK_Y) & ~arms
    body = ~arms & ~head
    t["torso"] = body & (yy < BELT_BOTTOM)
    legs = np.zeros((N, N), bool)
    for s in "nf":
        h, k, a = J[f"{s}_hip"], J[f"{s}_knee"], J[f"{s}_ankle"]
        side = (xx < LEG_SPLIT) if s == "n" else (xx >= LEG_SPLIT)
        leg = body & side & beyond(h, k) & (yy > BELT_BOTTOM)
        past_k = beyond(k, a)
        past_a = beyond(a, (2 * a[0] - k[0], 2 * a[1] - k[1]))
        t[f"{s}_thigh"], t[f"{s}_shin"], t[f"{s}_foot"] = leg & ~past_k, leg & past_k & ~past_a, leg & past_a
        legs |= leg
    t["hips"] = body & (yy >= BELT_TOP) & ~legs
    own = {k: v.copy() for k, v in t.items()}
    t["head"] = head | (disc(J["neck"], RADIUS["neck"]) & own["torso"])  # the head brings its neck
    for part, (parent, joint, _) in TREE.items():
        if parent and part != "head" and part != "torso":
            t[parent] = t[parent] | (disc(J[joint], RADIUS[joint]) & own[part])
    return t


def crop(img: np.ndarray, mask: np.ndarray):
    out = img.copy()
    out[..., 3] = np.where(mask, img[..., 3], 0)
    im = Image.fromarray(out)
    box = im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    return im.crop(box), box


def grips(grip_img: np.ndarray, t) -> dict:
    """Where each fist holds: the green rod's axis through the fist, pointing out of the thumb side (to the front)."""
    r, g, b, a = (grip_img[..., i].astype(int) for i in range(4))
    green = (a > 128) & (g > 150) & (g - r > 70) & (g - b > 70)
    out = {}
    for s in "nf":
        arm = poly(NEAR_ARM if s == "n" else FAR_ARM)
        ys, xs = np.nonzero(green & arm)
        pts = np.stack([xs, ys], 1).astype(float)
        c = pts.mean(0)
        _, _, vt = np.linalg.svd(pts - c, full_matrices=False)
        d = vt[0] if vt[0][0] > 0 else -vt[0]
        hy, hx = np.nonzero(t[f"{s}_hand"] & (grip_img[..., 3] > 128) & ~green)
        fist = np.array([hx.mean(), hy.mean()])
        at = c + d * float((fist - c) @ d)
        out[s] = {"at": [round(float(at[0]), 1), round(float(at[1]), 1)],
                  "angle": round(math.degrees(math.atan2(d[1], d[0])), 1)}
    return out


def cut() -> None:
    rig = load("rig")
    refine(rig[..., 3])
    t = territories(rig[..., 3])
    PARTS.mkdir(parents=True, exist_ok=True)
    meta = {"scale": 240 / HERO_PX, "joints": J, "radius": RADIUS, "sets": {}, "weapons": {},
            "parts": [{"name": p, "parent": par, "joint": j, "z": z} for p, (par, j, z) in TREE.items()],
            "weaponZ": WEAPON_Z, "farFront": FAR_FRONT, "slots": SLOTS}
    cover = np.zeros((N, N), bool)
    for m in t.values():
        cover |= m
    lost = int(((rig[..., 3] > 128) & ~cover).sum())
    print("pixels of the rig in no part:", lost)
    sets = {"bare": (rig, list(TREE))}
    for name, slot in GEAR.items():
        if name in hl.PICKS:
            sets[name] = (load(name), SLOTS[slot])
    for name, (img, parts) in sets.items():
        (PARTS / name).mkdir(exist_ok=True)
        meta["sets"][name] = {}
        for p in parts:
            mask = t[p]
            if p == "head" and name != "bare":
                # A helm may hang below the neck line (a coif on the shoulders): keep what the edit changed there.
                d = np.abs(img[..., :3].astype(int) - rig[..., :3].astype(int)).sum(-1)
                changed = ((d > 90) | ((img[..., 3] > 128) != (rig[..., 3] > 128))) & (yy < 560) & (yy >= NECK_Y - 40)
                grown = np.asarray(Image.fromarray((changed * 255).astype(np.uint8)).filter(
                    ImageFilter.MinFilter(5)).filter(
                    ImageFilter.MaxFilter(9))) > 0
                mask = mask | (grown & (yy >= NECK_Y))
            im, box = crop(img, mask)
            im.save(PARTS / name / f"{p}.png")
            meta["sets"][name][p] = {"file": f"{name}/{p}.png", "x": box[0], "y": box[1]}
    if "grip" in hl.PICKS:
        meta["grips"] = grips(load("grip"), t)
    for wid, (art, grip, size) in WEAPONS.items():
        im = Image.open(hp.ROOT / "unity" / "Assets" / "Resources" / "Art" / f"{art}.png").convert("RGBA")
        im = im.crop(im.getbbox())
        h = round(size * HERO_PX)
        im = im.resize((round(im.width * h / im.height), h), Image.LANCZOS)
        im.save(PARTS / f"{wid}.png")
        meta["weapons"][wid] = {"file": f"{wid}.png", "gx": im.width / 2, "gy": round(grip * h, 1)}
    (PARTS / "rig.json").write_text(json.dumps(meta, indent=1))
    print("joints", {k: (v, RADIUS.get(k)) for k, v in J.items()})
    print("grips", meta.get("grips"))


def diff() -> None:
    """A gear edit should change only its own parts: the share of other pixels that moved, and the outline's drift."""
    rig = load("rig")
    refine(rig[..., 3])
    t = territories(rig[..., 3])
    ra = rig[..., 3] > 128
    for p in sorted(OUT.glob("*_s*.png")):
        name = p.stem.rpartition("_s")[0]
        if name not in GEAR:
            continue
        img = np.asarray(hp.cleaned(p))
        own = np.zeros((N, N), bool)
        for part in SLOTS[GEAR[name]]:
            own |= t[part]
        own = np.asarray(Image.fromarray((own * 255).astype(np.uint8)).filter(
            ImageFilter.MaxFilter(31))) > 0
        d = np.abs(img[..., :3].astype(int) - rig[..., :3].astype(int)).sum(-1)
        ia = img[..., 3] > 128
        moved = ((d > 90) & ia & ra) | (ia != ra)
        outside = moved & ~own
        box, rbox = hp.figure_box(Image.fromarray(img)), hp.figure_box(Image.fromarray(rig))
        print(f"{p.stem:22} changed outside its parts: {outside.sum():6d} px ({outside.sum() / ra.sum():.1%})  "
              f"box drift {[b - a for a, b in zip(rbox, box)]}")


# ---------------------------------------------------------------------------------------------------- poses

def rot(v, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def ang(v):
    return math.degrees(math.atan2(v[1], v[0]))


def ik(a, target, l1, l2, bend):
    """Two bones from a to target: the directions of both, the middle joint on the `bend` side (+1 clockwise)."""
    dx, dy = target[0] - a[0], target[1] - a[1]
    d = max(abs(l1 - l2) + 0.5, min(math.hypot(dx, dy), l1 + l2 - 0.5))
    base = math.atan2(dy, dx)
    inner = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    d1 = base + bend * inner
    mid = (a[0] + l1 * math.cos(d1), a[1] + l1 * math.sin(d1))
    end = (a[0] + d * math.cos(base), a[1] + d * math.sin(base))
    return math.degrees(d1), ang(sub(end, mid))


def norm(a):
    return (a + 180) % 360 - 180


class Rig:
    def __init__(self):
        self.m = json.loads((PARTS / "rig.json").read_text())
        self.j = {k: tuple(v) for k, v in self.m["joints"].items()}
        self.img = {}

    def length(self, a, b):
        return math.dist(self.j[a], self.j[b])

    def rest_dir(self, a, b):
        return ang(sub(self.j[b], self.j[a]))

    def pose(self, hips=(0, 0), torso=0, head=0, n=None, f=None, nfoot=(0, 0), ffoot=(0, 0), weapon=None,
             far_front=False, on_weapon=None, nrot=None, frot=None, lean=0):
        """A pose from hand and foot targets.
        n / f: (dx, dy, bend[, wrist]) the wrist from its shoulder; wrist is the angle from the forearm to the weapon
        (-90 = square to it, the tip on the thumb side), kept between about -125 and -40 so no wrist breaks; without
        it the fist follows the forearm. on_weapon=(t, bend): the far fist closes on the near hand's weapon, t px
        along it from the grip (the Unity test solves that arm again every frame, so the fist stays on the shaft).
        Returns world turns per part plus the root offset and the ankles (the legs are solved again in Unity)."""
        j, g = self.j, self.m["grips"]
        p = {"hips": lean, "torso": torso, "head": head, "root": list(hips), "far_front": far_front, "on": None}
        waist = add(j["waist"], hips)
        wrists = {}
        for s, spec in (("n", n), ("f", f)):
            sh = add(waist, rot(sub(j[f"{s}_shoulder"], j["waist"]), torso))
            l1, l2 = self.length(f"{s}_shoulder", f"{s}_elbow"), self.length(f"{s}_elbow", f"{s}_wrist")
            r1, r2 = self.rest_dir(f"{s}_shoulder", f"{s}_elbow"), self.rest_dir(f"{s}_elbow", f"{s}_wrist")
            if s == "f" and on_weapon:
                t, bend = on_weapon
                wdir = p["n_hand"] + g["n"]["angle"]
                grip_n = add(wrists["n"], rot(sub(g["n"]["at"], j["n_wrist"]), p["n_hand"]))
                hold = add(grip_n, rot((t, 0), wdir))
                hand = norm(wdir - g["f"]["angle"])
                if abs(hand) > 90:
                    hand = norm(hand + 180)
                target = sub(hold, rot(sub(g["f"]["at"], j["f_wrist"]), hand))
                d1, d2 = ik(sh, target, l1, l2, bend)
                p["f_hand"] = hand
                p["on"] = {"t": t, "bend": bend}
            elif spec is None:  # as drawn
                d1, d2 = r1, r2
                p[f"{s}_hand"] = 0
            else:
                target = add(sh, spec[:2])
                d1, d2 = ik(sh, target, l1, l2, spec[2])
                p[f"{s}_hand"] = norm(d2 + spec[3] - g[s]["angle"]) if len(spec) > 3 else norm(d2 - r2)
            p[f"{s}_upper"], p[f"{s}_fore"] = norm(d1 - r1), norm(d2 - r2)
            el = add(sh, rot((l1, 0), d1))
            wrists[s] = add(el, rot((l2, 0), d2))
        if nrot is not None:
            p["n_hand"] = nrot
        if frot is not None:
            p["f_hand"] = frot
        p["n_ankle"], p["f_ankle"] = list(nfoot), list(ffoot)
        p["weapon"] = weapon or []
        return p

    def solve(self, p):
        """World turn and pivot position of every part (the same sums as the Unity test)."""
        j = self.j
        turn = {k: p.get(k, 0) for k in TREE}
        waist = add(j["waist"], p["root"])
        for s in "nf":
            hip = add(waist, rot(sub(j[f"{s}_hip"], j["waist"]), turn["hips"]))
            ankle = add(j[f"{s}_ankle"], p[f"{s}_ankle"])
            l1, l2 = self.length(f"{s}_hip", f"{s}_knee"), self.length(f"{s}_knee", f"{s}_ankle")
            # Knee to the front. Measured from the same solve of the rig pose, so the rig pose itself is turn 0.
            d1, d2 = ik(hip, ankle, l1, l2, -1)
            r1, r2 = ik(j[f"{s}_hip"], j[f"{s}_ankle"], l1, l2, -1)
            turn[f"{s}_thigh"], turn[f"{s}_shin"] = norm(d1 - r1), norm(d2 - r2)
            turn[f"{s}_foot"] = p.get(f"{s}_foot", 0)
        at = {}
        for part, (parent, joint, _) in TREE.items():
            if parent is None:
                at[part] = waist
            else:
                pj = TREE[parent][1]
                at[part] = add(at[parent], rot(sub(j[joint], j[pj]), turn[parent]))
        return turn, at

    def part(self, set_name, part):
        key = (set_name, part)
        if key not in self.img:
            e = self.m["sets"][set_name][part]
            self.img[key] = (Image.open(PARTS / e["file"]).convert("RGBa"), e["x"], e["y"])
        return self.img[key]

    def draw(self, p, gear=None, size=(1536, 1536), offset=(0, 0), guides=False):
        """The pose as a picture. gear: {slot: set name}."""
        turn, at = self.solve(p)
        wear = {}
        for slot, parts in SLOTS.items():
            for part in parts:
                wear[part] = (gear or {}).get(slot) or "bare"
        layers = []
        for part, (_, joint, z) in TREE.items():
            if p.get("far_front") and part in FAR_FRONT:
                z = FAR_FRONT[part]
            im, x0, y0 = self.part(wear.get(part, "bare"), part)
            layers.append((z, im, (x0, y0), self.j[joint], at[part], turn[part]))
        g = self.m.get("grips")
        for s, wid in zip("nf", p.get("weapon") or []):
            if not wid or not g:
                continue
            w = self.m["weapons"][wid]
            key = ("w", wid)
            if key not in self.img:
                self.img[key] = Image.open(PARTS / w["file"]).convert("RGBa")
            hand = f"{s}_hand"
            hold = add(at[hand], rot(sub(g[s]["at"], self.j[f"{s}_wrist"]), turn[hand]))
            z = FAR_FRONT["f_weapon"] if s == "f" and p.get("far_front") else WEAPON_Z[s]
            layers.append((z, self.img[key], (0, 0), (w["gx"], w["gy"]), hold, turn[hand] + g[s]["angle"] + 90))
        out = Image.new("RGBA", size, (0, 0, 0, 0))
        for z, im, origin, pivot, where, deg in sorted(layers, key=lambda v: v[0]):
            c, s_ = math.cos(math.radians(-deg)), math.sin(math.radians(-deg))
            # canvas point -> source point: R(-deg)(p - where) + pivot - origin
            px, py = pivot[0] - origin[0], pivot[1] - origin[1]
            wx, wy = where[0] + offset[0], where[1] + offset[1]
            data = (c, -s_, px - c * wx + s_ * wy, s_, c, py - s_ * wx - c * wy)
            out.alpha_composite(im.transform(size, Image.AFFINE, data, Image.BICUBIC).convert("RGBA"))
        if guides:
            d = ImageDraw.Draw(out)
            for part, (_, joint, _) in TREE.items():
                x, y = at[part][0] + offset[0], at[part][1] + offset[1]
                d.ellipse((x - 5, y - 5, x + 5, y + 5), outline=(255, 0, 255, 255), width=2)
        return out


SWORD, STAFF, DAGGERS = ["iron_sword"], ["oak_staff"], ["rusty_dagger", "rusty_dagger"]
STANCE = dict(hips=(0, 32), nfoot=(-10, 0), ffoot=(36, 0))


def poses(r: Rig) -> dict:
    """The key poses: a ready stance, a wind-up and a strike per weapon. The test moves between them."""
    P = r.pose
    return {
        "rest": P(),
        "sword_ready": P(**STANCE, torso=4, n=(150, 150, 1, -58), f=(80, 200, 1), weapon=SWORD),
        "sword_wind": P(hips=(-28, 18), nfoot=(-10, 0), ffoot=(36, 0), torso=-9, head=-3, n=(-40, -170, -1, -80),
                        f=(170, 70, 1), weapon=SWORD),
        "sword_strike": P(hips=(120, 72), nfoot=(-40, 0), ffoot=(230, 0), torso=26, head=-12, n=(235, 215, 1, -45),
                          f=(-70, 185, 1), weapon=SWORD),
        "staff_ready": P(**STANCE, torso=3, n=(170, 60, 1, -50), on_weapon=(-220, -1), far_front=True, weapon=STAFF),
        "staff_wind": P(hips=(-24, 16), nfoot=(-10, 0), ffoot=(36, 0), torso=-7, head=-3, n=(215, -25, 1, -48),
                        on_weapon=(-200, -1), far_front=True, weapon=STAFF),
        "staff_strike": P(hips=(105, 66), nfoot=(-40, 0), ffoot=(215, 0), torso=22, head=-10, n=(235, 120, 1, -50),
                          on_weapon=(-160, -1), far_front=True, weapon=STAFF),
        "dagger_ready": P(**STANCE, torso=5, n=(150, 110, 1, -55), f=(190, 90, 1, -55), weapon=DAGGERS),
        "dagger_wind": P(hips=(-20, 26), nfoot=(-10, 0), ffoot=(36, 0), torso=-4, n=(-40, 110, 1, -60),
                         f=(200, 40, 1, -60), weapon=DAGGERS),
        "dagger_strike": P(hips=(125, 72), nfoot=(-40, 0), ffoot=(235, 0), torso=26, head=-12, n=(255, 105, 1, -38),
                           f=(-40, 170, 1, -70), weapon=DAGGERS),
        # The hand is a choice: the same sword in the left (far) hand.
        "sword_left_ready": P(**STANCE, torso=4, n=(70, 205, 1), f=(175, 125, 1, -58), weapon=[None, "iron_sword"]),
    }


def export() -> None:
    """parts/poses.json for the Unity test (LayeredHero.cs)."""
    (PARTS / "poses.json").write_text(json.dumps(poses(Rig()), indent=1))
    print(PARTS / "poses.json")


def show(names) -> None:
    r = Rig()
    ps = poses(r)
    names = names or list(ps)
    gear, guides = None, False
    if "--guides" in names:
        names.remove("--guides")
        guides = True
    for n in [n for n in names if "=" in n]:
        names.remove(n)
        gear = dict(kv.split(":") for kv in n.split("=")[1].split(","))
    names = names or list(ps)
    cell, size, cols = (500, 500), (2000, 2000), min(5, len(names))
    rows = (len(names) + cols - 1) // cols
    font = ImageFont.truetype("arialbd.ttf", 20)
    sheet = Image.new("RGB", (cell[0] * cols, cell[1] * rows), (214, 205, 182))
    for i, n in enumerate(names):
        im = r.draw(ps[n], gear=gear, size=size, offset=(230, 480), guides=guides)
        tile = Image.new("RGBA", im.size, (214, 205, 182, 255))
        ImageDraw.Draw(tile).line((0, 1345 + 480, size[0], 1345 + 480), fill=(200, 40, 40, 255), width=3)
        tile.alpha_composite(im)
        at = (cell[0] * (i % cols), cell[1] * (i // cols))
        sheet.paste(tile.convert("RGB").resize(cell, Image.LANCZOS), at)
        ImageDraw.Draw(sheet).text((at[0] + 10, at[1] + 8), n, fill=(60, 50, 45), font=font)
    sheet.save(OUT / "_poses.png")
    print(OUT / "_poses.png", sheet.size)


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "cut":
        cut()
    elif cmd == "diff":
        diff()
    elif cmd == "export":
        export()
    elif cmd == "show":
        show(sys.argv[2:])
