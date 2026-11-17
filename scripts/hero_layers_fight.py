#!/usr/bin/env python3
"""Layered hero, step 1 of the rebuild: a sword fight with drawn upper bodies (docs/PLANS.md, "Layered hero rebuild").

The first test (hero_layers_rig.py) turned every part on a joint and lost to the drawn poses in wind-ups and strikes:
no twist, no arm toward the viewer, a calm face, the whole fist over the weapon. Here each key moment (ready, wind,
strike, hurt) has its upper body drawn whole: torso, both arms, hands and belt, an edit of the rig pose with a green
rod in the sword fist (up_<moment> in hero_layers.py). What stays on joints:
  - the legs (rig parts on two-bone IK, so the feet stay planted and stride between moments),
  - the head (the rig's head, so one helm picture serves every moment) with a drawn face on top (shout, hurt).
The weapon lies between the body and the fist that was drawn gripping the rod, so the handle sits inside the fist.
Gear: the chest and the gloves are an edit of the same moment (set_<moment>); helm, legs and boots are rig parts.

Usage (from the repo root):
  python scripts/hero_layers_fight.py cut            # art/redo/layers/parts/fight/... + parts/fight.json
  python scripts/hero_layers_fight.py show [--src]   # art/redo/layers/_fight.png (bare and the full set)
"""
import json
import os
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import hero_layers as hl  # noqa: E402
import hero_layers_rig as hr  # noqa: E402
import hero_poses as hp  # noqa: E402

N = hr.N
PARTS = hr.PARTS
FIGHT = PARTS / "fight"
MOMENTS = ["ready", "wind", "strike", "hurt"]
FACE = {"ready": None, "wind": None, "strike": "shout", "hurt": "hurt"}
SET = {"helm": "helm_coif", "legs": "legs_plate", "boots": "boots_steel", "chest": "set", "gloves": "set"}
# Where the legs stand under each drawn body: the waist from its rest place and the ankles from theirs, the feet's
# turn, and how much deeper than "legs just reach" the hips sink. The hips drop until both legs reach.
# The rig's legs are shorter than the legs the pose pictures draw (their heroes stand a head-top higher on the same
# feet): the rig legs are drawn this much longer, so every hold stands the same height on the same ground.
LEGS_LONG = 1.12
STAND = {
    "ready": dict(root=(10, 0), nfoot=(-112, 0), ffoot=(118, 0), sink=5),
    # The wind-up coils low over wide legs, lower than the rig's legs sit: its own drawn legs too (as the strike below).
    "wind": dict(root=(23, -24), drawn=True, nfoot=(-101, 0), ffoot=(108, 0)),
    # The lunge is deeper than the rig's legs are long (the drawn thigh is 1.7 times the rig's): it brings its own
    # drawn legs. root: how far the picture's belt stands from the rig's waist; its own height is kept.
    # Its legs are drawn; nfoot / ffoot say where that picture's feet are, for the rig legs on the way in and out.
    "strike": dict(root=(262, 0), drawn=True, nfoot=(-10, 0), ffoot=(548, 0)),
    "hurt": dict(root=(-70, 0), nfoot=(-112, 0), ffoot=(40, -14), sink=30, f_foot=-10),
}
# Overrides where the picture can't tell: the blade's direction (degrees, 0 = forward, -90 = up).
BLADE = {"wind": -170, "hurt": -16}
# The belt where the fists and the rod hide it: its middle and tilt.
BELT = {"ready": ((780, 690), 0), "hurt": ((684, 693), 0)}
# The pose picture of a moment when it is not `up_<moment>`: the strike with its far arm thrown back and down like a
# fencer's (the first one raised it behind the head, where it read as a stick once the helm covered it).
POSE = {"strike": "arm_strike"}
# An arm the pose picture can't show (it runs behind the head): painted in, corners in the pose picture. None now.
ARM = {}
WEAPON_Z = {"wind": 55}  # behind the head while it is drawn back
# Step 2, hands: what else the two hands can hold, each in ready and strike (up_<hold>_<moment> in hero_layers.py).
# left: a sword in the left fist. dual: a weapon in each fist (the left one's rod is pink). staff: one pole in both.
HOLDS = ["left", "dual", "staff"]
HOLD_MOMENTS = [f"{h}_{m}" for h in HOLDS for m in ("ready", "strike")]
# Where a hold's drawn legs stand: the feet's lowest point, the middle of a ready stance, and how far the rear foot
# steps forward into the strike (as the sword's own drawn wind-up and strike stand).
FEET_Y, FEET_MID, STEP = 1350, 790, 110
# Which way a hold's weapon points when the rod alone can't tell (it sticks out both sides of the fist).
HINT = {"ready": (1, -0.5), "strike": (1, 0.3)}
# The two fists on a pole where the picture can't tell them apart from other gaps: ((x, y), (x, y)).
FISTS = {}
Z = {"legs": 46, "legs_set": 47, "boots_set": 48, "body": 50, "chest": 51, "hands": 52, "gloves": 53, "head": 60, "face": 61, "weapon": 71, "fist": 72, "fist2": 72,
     "glove_fist": 73}

yy, xx = hr.yy, hr.xx


def chan(img):
    return (img[..., i].astype(int) for i in range(4))


def grow(mask, r):
    im = Image.fromarray(mask.astype(np.uint8) * 255)
    while r > 0:
        k = min(r, 5)
        im = im.filter(ImageFilter.MaxFilter(2 * k + 1))
        r -= k
    return np.asarray(im) > 0


def shrink(mask, r):
    return ~grow(~mask, r)


def spread(lab, valid, steps=80):
    """Unlabelled pixels take the label of the nearest labelled one."""
    lab = lab.copy()
    for _ in range(steps):
        todo = (lab == 0) & valid
        if not todo.any():
            break
        new = lab.copy()
        for axis, s in ((0, 1), (0, -1), (1, 1), (1, -1)):
            new = np.where(todo & (new == 0), np.roll(lab, s, axis), new)
        lab = new
    return lab


def enclosed(body, reach):
    """Pixels with the body on both sides, left and right or above and below, within `reach`."""
    def toward(axis, sign):
        acc = np.zeros_like(body)
        for s in range(1, reach + 1):
            acc |= np.roll(body, sign * s, axis)
        return acc
    return (toward(1, 1) & toward(1, -1)) | (toward(0, 1) & toward(0, -1))


def fill(img, holes, src):
    """Paint the holes with the colours of `src` around them (they end up under the weapon or the head)."""
    ys, xs = np.nonzero(holes)
    if not len(ys):
        return
    y0, y1, x0, x1 = max(ys.min() - 3, 0), ys.max() + 4, max(xs.min() - 3, 0), xs.max() + 4
    sub, todo = img[y0:y1, x0:x1], holes[y0:y1, x0:x1].copy()
    rgb = sub[..., :3].astype(float)
    known = src[y0:y1, x0:x1] & ~todo
    for _ in range(200):
        if not todo.any():
            break
        acc, cnt = np.zeros_like(rgb), np.zeros(todo.shape)
        for axis, s in ((0, 1), (0, -1), (1, 1), (1, -1)):
            k = np.roll(known, s, axis)
            acc += np.roll(rgb, s, axis) * k[..., None]
            cnt += k
        new = todo & (cnt > 0)
        if not new.any():
            break
        rgb[new] = acc[new] / cnt[new][:, None]
        known |= new
        todo &= ~new
    sub[..., :3] = np.rint(rgb).astype(np.uint8)
    sub[..., 3] = np.where(holes[y0:y1, x0:x1] & known, 255, sub[..., 3])


def skin_of(img):
    r, g, b, a = chan(img)
    return (a > 128) & (r > 125) & (g < 0.79 * r) & (b < 0.52 * r) & (g > 0.5 * r)


def red_of(img, loose=False):
    r, g, b, a = chan(img)
    if loose:
        return (a > 128) & (r > 50) & (r - g > 26) & (r - b > 26) & (g < 95)
    return (a > 128) & (r > 70) & (r - g > 40) & (r - b > 40) & (g < 85)


def classes(img, legs_near=None):
    """The picture split into legs (trousers, boots), the green rod and the upper body."""
    r, g, b, a = chan(img)
    op = a > 128
    pink = op & (r - g > 60) & (b - g > 60)  # the second rod (the left fist's, when both fists hold one)
    green = (op & (g - r > 28) & (g - b > 28)) | pink
    teal = op & ~green & (g - r >= 8) & (b - r >= 4) & (r < 100) & (g < 130) & (yy > 600)
    if legs_near is not None:
        teal &= legs_near
    low = np.median(yy[teal])
    brown = op & ~green & ~teal & (r > g) & (g >= b) & (r - b >= 18) & (r < 150)
    boots = brown & (yy > low)
    if legs_near is not None:
        boots &= legs_near
    dark = op & ~green & ~teal & ~boots & (np.maximum(np.maximum(r, g), b) < 46)
    rim = op & grow(~op, 7) & ~green & ~teal & ~boots  # the pale rim around the figure: it belongs to what it lies on
    lab = np.zeros((N, N), np.uint8)
    lab[teal | boots] = 1
    lab[op & ~green & ~teal & ~boots & ~dark & ~rim] = 2
    lab[green] = 3
    lab = spread(lab, op)
    return {"legs": lab == 1, "up": lab == 2, "rod": grow(lab == 3, 4) & op, "green": green & ~pink, "pink": pink, "teal": teal,
            "brown": brown}


def belt(img, c):
    """The belt's middle and tilt: brown above the trousers."""
    under = np.zeros((N, N), bool)
    for k in range(4, 50, 4):
        under |= np.roll(c["teal"], -k, 0)
    m = c["brown"] & c["up"] & under & (yy > 560)
    ys, xs = np.nonzero(m)
    pts = np.stack([xs, ys], 1).astype(float)
    mid = pts.mean(0)
    _, _, vt = np.linalg.svd(pts - mid, full_matrices=False)
    d = vt[0] if vt[0][0] > 0 else -vt[0]
    t = (pts - mid) @ d
    mid = mid + d * (np.percentile(t, 2) + np.percentile(t, 98)) / 2
    return (float(mid[0]), float(mid[1])), math.degrees(math.atan2(d[1], d[0]))


def rod(c, skin, colour="green", hint=None, both=False):
    """Where the fist holds the rod and which way the long end points (or, with a hint, the end on the hint's side).
    both: one pole in two fists: (the rear fist, the front fist, the pole's middle between them, its direction)."""
    ys, xs = np.nonzero(c[colour])
    pts = np.stack([xs, ys], 1).astype(float)
    mid = pts.mean(0)
    _, _, vt = np.linalg.svd(pts - mid, full_matrices=False)
    d = vt[0]
    t = (pts - mid) @ d
    lo, hi = t.min(), t.max()
    bins = np.bincount(((t - lo) // 4).astype(int))
    best, run, start, gaps = (0, 0), 0, 0, {}
    for i, v in enumerate(bins):
        if v == 0:
            if run == 0:
                start = i
            run += 1
            best = max(best, (run, start))
            gaps[start] = run
        else:
            run = 0
    if both:
        two = sorted(sorted(gaps.items(), key=lambda q: -q[1])[:2])
        if len(two) < 2 or min(q[1] for q in two) * 4 < 16:
            raise ValueError(f"the pole shows {sorted(gaps.items())} as gaps, not two fists")
        a, b = (mid + d * (lo + 4 * (s + n / 2)) for s, n in two)
        if a[0] > b[0]:
            a, b = b, a
        way = (b - a) / np.linalg.norm(b - a)
        if hint is not None and way[0] * hint[0] + way[1] * hint[1] < 0:
            way = -way
        return (tuple(map(float, a)), tuple(map(float, b)), tuple(map(float, (a + b) / 2)),
                math.degrees(math.atan2(way[1], way[0])))
    if best[0] * 4 >= 24:
        g = lo + 4 * (best[1] + best[0] / 2)
    else:
        ends = []
        for e, sgn in ((lo, -1), (hi, 1)):
            q = mid + d * (e + sgn * 28)
            ends.append((int((skin & hr.disc(q, 42)).sum()), e + sgn * 26))
        g = max(ends)[1]
    at = mid + d * g
    blade = d if hi - g > g - lo else -d
    # A fist near the rod's middle leaves no long end: there the hint decides.
    if hint is not None and min(hi - g, g - lo) > 0.6 * max(hi - g, g - lo):
        blade = d if d[0] * hint[0] + d[1] * hint[1] > 0 else -d
    return (float(at[0]), float(at[1])), math.degrees(math.atan2(blade[1], blade[0]))


class Head:
    """Finds the rig's head in a pose picture: the place of its neck and its turn."""

    def __init__(self, r: hr.Rig):
        e = r.m["sets"]["bare"]["head"]
        im = np.asarray(Image.open(PARTS / e["file"]).convert("RGBA")).copy()
        self.full = Image.fromarray(im).convert("RGBa")
        im[red_of(im)] = 0  # the headband's tails fly differently in every picture
        self.im = Image.fromarray(im).convert("RGBa")
        self.origin = (e["x"], e["y"])
        self.neck = r.j["neck"]

    def canvas(self, im, deg, where, scale=1):
        """The head turned by deg about its neck, the neck at `where`."""
        c, s_ = math.cos(math.radians(-deg)), math.sin(math.radians(-deg))
        px, py = self.neck[0] - self.origin[0], self.neck[1] - self.origin[1]
        wx, wy = where
        data = (c, -s_, px - c * wx + s_ * wy, s_, c, py - s_ * wx - c * wy)
        out = im.transform((N, N), Image.AFFINE, data, Image.BICUBIC)
        return out.resize((N // scale, N // scale), Image.BOX) if scale > 1 else out

    def search(self, pic, turns, scale):
        n = N // scale
        p = np.asarray(Image.fromarray(pic).convert("RGBa").resize((n, n), Image.BOX)).astype(float) / 255
        prgb = p[..., :3] + (1 - p[..., 3:4]) * np.array([1.0, 0.0, 1.0])
        fp, fp2 = np.fft.rfft2(prgb, axes=(0, 1)), np.fft.rfft2(prgb ** 2, axes=(0, 1))
        best = None
        for deg in turns:
            t = np.asarray(self.canvas(self.im, deg, (N / 2, N / 2), scale)).astype(float) / 255
            m, mh = t[..., 3], t[..., :3]
            fm = np.conj(np.fft.rfft2(m))
            ssd = (mh ** 2 / np.maximum(m, 1e-3)[..., None]).sum()
            for ch in range(3):
                ssd = ssd - 2 * np.fft.irfft2(np.conj(np.fft.rfft2(mh[..., ch])) * fp[..., ch], s=(n, n))
                ssd = ssd + np.fft.irfft2(fm * fp2[..., ch], s=(n, n))
            ssd = ssd / m.sum()
            i = np.unravel_index(np.argmin(ssd), ssd.shape)
            if best is None or ssd[i] < best[0]:
                dy, dx = (v - n if v > n // 2 else v for v in i)
                best = (float(ssd[i]), deg, (N / 2 + scale * dx, N / 2 + scale * dy))
        return best

    def find(self, pic):
        _, deg, _ = self.search(pic, range(-45, 46, 3), 4)
        score, deg, at = self.search(pic, range(deg - 2, deg + 3), 2)
        return at, deg, score

    def shape(self, deg, at):
        return np.asarray(self.canvas(self.full, deg, at))[..., 3] > 100


def behead(img, up, head: Head, at, deg):
    """The pose picture's own head, to be cut away (the rig's head is drawn in its place)."""
    r, g, b, a = chan(img)
    sil = head.shape(deg, at)
    upv = hr.rot((0, -1), deg)
    above = (xx - at[0]) * upv[0] + (yy - at[1]) * upv[1]
    hair = (np.maximum(np.maximum(r, g), b) < 105) & (r >= g) & (g >= b)
    red = red_of(img, True) & (above > -260) & (np.hypot(xx - at[0], yy - at[1]) < 420)
    gone = sil | (grow(sil, 9) & (above > 22)) | (hair & grow(sil, 32) & (above > 40)) | grow(red, 4)
    # What is left of the pose's own hair around the rig's head: thin scraps, not an arm or a fist.
    left = up & ~gone
    scraps = left & ~(grow(shrink(left, 7), 9)) & grow(sil, 70) & (above > 10)
    return (gone | scraps) & up, red


def gear_split(pic, c, bare_skin_arms):
    """A set picture's upper body split into the chest and the gloves (what covers the bare hands and wrists)."""
    sk = skin_of(pic)
    seed = shrink(bare_skin_arms & ~sk & c["up"], 3)
    return grow(seed, 12) & c["up"] & ~sk


def forearm(img, body, name, skin, colours=None):
    """Paints the arm of ARM into the picture, in the skin's colour with the figure's pale rim; returns the body with it."""
    if name not in ARM:
        return body
    src = img if colours is None else colours
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon(ARM[name], fill=255)
    bar = np.asarray(m) > 0
    near = grow(bar, 60)
    tone = np.median(src[skin & near][:, :3], axis=0)
    op = src[..., 3] > 128
    rim = np.median(src[op & grow(~op, 3) & near & ~skin][:, :3], axis=0)
    new = bar & ~body
    whole = bar | body
    img[new, :3] = np.rint(tone).astype(np.uint8)
    img[new & grow(~whole, 5), :3] = np.rint(rim).astype(np.uint8)
    img[new, 3] = 255
    return whole


def solid(mask):
    """The mask without its dust: stray specks that touch no solid piece of it (left where a rod's end was cut away)."""
    core = shrink(mask, 3)
    if not core.any():
        return mask
    ys, xs = np.nonzero(mask)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    sub, reach = mask[y0:y1, x0:x1], core[y0:y1, x0:x1]
    while True:
        more = grow(reach, 5) & sub
        if (more == reach).all():
            break
        reach = more
    out = np.zeros_like(mask)
    out[y0:y1, x0:x1] = reach
    return out


def unrim(img, body):
    """Repaints the figure's pale rim where a pose picture drew it inside the body (along an arm that lies over the
    shirt): there it reads as a yellow line."""
    r, g, b, _ = chan(img)
    line = body & shrink(body, 10) & (r > 238) & (g > 212) & (b > 105) & (b < 178)
    if line.sum() < 40:
        return 0
    line = grow(line, 2) & body
    fill(img, line, body & ~line)
    return int(line.sum())


def save(img, mask, path):
    kept = solid(mask)
    if (mask & ~kept).any():
        print(f"   {path.parent.name}/{path.name}: {int((mask & ~kept).sum())} px of dust dropped")
    im, box = hr.crop(img, kept)
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    return {"file": str(path.relative_to(PARTS)).replace("\\", "/"), "x": box[0], "y": box[1]}


def cut():
    r = hr.Rig()
    head = Head(r)
    rig = hr.load("rig")
    out = {"moments": {}, "faces": {}, "hips": {}, "z": Z, "set": SET, "legsLong": LEGS_LONG}
    debug = []
    # Faces: what a face edit changed on the rig's head.
    for name in ("shout", "hurt"):
        f = hr.load(f"face_{name}")
        d = np.abs(f[..., :3].astype(int) - rig[..., :3].astype(int)).sum(-1)
        ch = (d > 55) & (yy < hr.NECK_Y + 30) & (f[..., 3] > 128)
        ch = grow(shrink(ch, 1), 6) & (f[..., 3] > 128)
        ch &= grow(shrink(skin_of(rig) & (yy < hr.NECK_Y + 30), 6), 13)  # the face only: no hair, a helm covers that
        ys, xs = np.nonzero(ch)
        box = hr.disc((np.median(xs), np.median(ys)), 150)
        out["faces"][name] = save(f, ch & box, FIGHT / f"face_{name}.png")
    # The rig's hips without their belt: every drawn body brings its own.
    for s in ("bare", "legs_plate", "legs_plain", "legs_leather"):
        e = r.m["sets"][s]["hips"]
        im = np.asarray(Image.open(PARTS / e["file"]).convert("RGBA")).copy()
        cutoff = hr.BELT_BOTTOM - 14 - e["y"]
        im[:max(cutoff, 0), :, 3] = 0
        (FIGHT / "hips").mkdir(parents=True, exist_ok=True)
        Image.fromarray(im).save(FIGHT / "hips" / f"{s}.png")
        out["hips"][s] = {"file": f"fight/hips/{s}.png", "x": e["x"], "y": e["y"]}
    ready_left = {}
    only = [n for n in os.environ.get("ONLY", "").split(",") if n]
    if only:
        out["moments"] = json.loads((PARTS / "fight.json").read_text())["moments"]
    for name in MOMENTS + [n for n in HOLD_MOMENTS if f"up_{n}" in hl.PICKS]:
        if only and name not in only:
            continue
        pic = hr.load(POSE.get(name, f"up_{name}")).copy()
        c = classes(pic)
        mid, tilt = BELT.get(name) or belt(pic, c)
        at, deg, score = head.find(pic)
        skin = skin_of(pic)
        hold, _, moment = name.rpartition("_")
        hint = HINT[moment] if hold else None
        grip2, blade2 = None, None
        if hold == "staff":
            if name in FISTS:
                a, b = (np.array(q, float) for q in FISTS[name])
                way = (b - a) if (b - a) @ np.array(hint) > 0 else (a - b)
                a, b, grip, blade = tuple(a), tuple(b), tuple((a + b) / 2), math.degrees(math.atan2(way[1], way[0]))
            else:
                a, b, grip, blade = rod(c, skin & c["up"], hint=hint, both=True)
            grips = [a, b]
        else:
            grip, blade = rod(c, skin & c["up"], hint=hint)
            grips = [grip]
            if hold == "dual":
                grip2, blade2 = rod(c, skin & c["up"], "pink", hint)
                blade2 = BLADE.get(name + "2", blade2)
                grips.append(grip2)
        blade = BLADE.get(name, blade)
        if hold:
            # A hold's own drawn legs: feet on the ground, a ready stance in the middle, the strike a step ahead of it.
            ys, xs = np.nonzero(c["legs"])
            x0, x1, y1 = np.percentile(xs, 0.2), np.percentile(xs, 99.8), np.percentile(ys, 99.8)
            if moment == "ready":
                shift = FEET_MID - (x0 + x1) / 2
                ready_left[hold] = x0 + shift
            else:
                shift = ready_left.get(hold, 411) + STEP - x0
            STAND[name] = dict(root=(int(round(shift + mid[0] - r.j["waist"][0])), int(round(FEET_Y - y1))), drawn=True,
                               nfoot=(0, 0), ffoot=(0, 0))
        gone, red = behead(pic, c["up"], head, at, deg)
        body = c["up"] & ~gone
        # Holes: the rod and the headband's tails where they lay over the body.
        holes = (c["rod"] | (grow(red, 4) & gone)) & enclosed(body, 40)
        fill(pic, holes, body & ~c["rod"])
        body = (body | holes) & ~(c["rod"] & ~holes)
        body = forearm(pic, body, name, skin & body)
        n = unrim(pic, body)
        if n:
            print(f"   {name}: {n} px of pale rim inside the body repainted")
        # The fists that grip: their skin around the grip and the outline on it.
        def fist_at(g):
            near = skin & body & hr.disc(g, 66)
            ys, xs = np.nonzero(near)
            i = int(np.argmin((xs - g[0]) ** 2 + (ys - g[1]) ** 2))
            m = Image.fromarray(near.astype(np.uint8) * 255).copy()
            ImageDraw.floodfill(m, (int(xs[i]), int(ys[i])), 128)
            fm = np.asarray(m) == 128
            return (grow(fm, 3) & body & hr.disc(g, 72) & ~skin) | fm

        files = {"body": save(pic, body, FIGHT / name / "body.png"),
                 "fist": save(pic, fist_at(grips[0]), FIGHT / name / "fist.png")}
        if len(grips) > 1:
            files["fist2"] = save(pic, fist_at(grips[1]), FIGHT / name / "fist2.png")
        v = {"pivot": [round(mid[0], 1), round(mid[1], 1)], "lean": round(tilt, 1),
             "neck": [at[0], at[1]], "head": deg, "grip": [round(grip[0], 1), round(grip[1], 1)],
             "blade": round(blade, 1), "weaponZ": WEAPON_Z.get(name, Z["weapon"]),
             "face": FACE.get(name, "shout" if moment == "strike" else None), "stand": STAND[name], "files": files}
        if grip2:
            v["grip2"], v["blade2"] = [round(grip2[0], 1), round(grip2[1], 1)], round(blade2, 1)
        print(f"{name:7} belt {v['pivot']} tilt {v['lean']}  neck {v['neck']} turn {deg} (fit {score:.3f})  "
              f"grip {v['grip']} blade {v['blade']}")
        # The full set's chest and gloves (set_<moment>), and each body armor drawn on this moment (body_<armor>_<moment>,
        # step 4a: the whole drawn upper body incl. a robe's skirt, the hands as drawn, no gloves split).
        for armor in ["set"] + list(hl.BODY_ARMOR):
            key = f"set_{name}" if armor == "set" else f"body_{armor}_{name}"
            if not (key in hl.PICKS or (hl.OUT / f"{key}_s6100.png").exists()):
                continue
            hl.PICKS.setdefault(key, 6100)
            sp = hr.load(key).copy()
            sc = classes(sp, grow(c["legs"], 14))
            if armor != "set":  # all of it but the rod (a robe's shaded red reads as boots); the rod's outline goes too
                sc["rod"] = grow(sc["rod"], 4) & (sp[..., 3] > 128)
                sc["up"] = (sp[..., 3] > 128) & ~sc["rod"]
            # The head stays where the bare pose has it, so an armor picture loses that same head and headband (its own
            # red search would take a red robe or reddish leather with it).
            sgone, sred = behead(sp, sc["up"], head, at, deg) if armor == "set" else (grow(gone, 2) & sc["up"], red)
            sbody = sc["up"] & ~sgone
            sholes = (sc["rod"] | (grow(sred, 4) & sgone)) & enclosed(sbody, 40)
            if armor != "set":  # a rod lying across a robe pokes out past its edge: fill it where armor is above and below
                sholes |= sc["rod"] & np.maximum.accumulate(sbody, 0) & np.maximum.accumulate(sbody[::-1], 0)[::-1]
            fill(sp, sholes, sbody & ~sc["rod"])
            sbody = (sbody | sholes) & ~(sc["rod"] & ~sholes)
            sbody = forearm(sp, sbody, name, skin & body, pic)
            if armor != "set":
                # Below the belt only what the armor changed (a robe's skirt): the legs and boots it left as they were
                # are the legs' own layers.
                same = np.abs(sp[..., :3].astype(int) - pic[..., :3].astype(int)).sum(-1) < 70
                sbody &= ~(grow(c["legs"], 3) & (same | ~(sp[..., 3] > 128)))
                files[f"body_{armor}"] = save(sp, sbody, FIGHT / name / f"body_{armor}.png")
                continue
            gloves = gear_split(sp, sc, skin & body) & sbody
            files["chest"] = save(sp, sbody & ~gloves, FIGHT / name / "chest.png")
            files["gloves"] = save(sp, gloves, FIGHT / name / "gloves.png")
            files["glove_fist"] = save(sp, gloves & hr.disc(grip, 84), FIGHT / name / "glove_fist.png")
            files["hands"] = save(pic, body & grow(gloves, 5), FIGHT / name / "hands.png")
        if STAND[name].get("drawn"):
            lg = c["legs"]
            lholes = c["rod"] & enclosed(lg, 40)
            fill(pic, lholes, lg & ~c["rod"])
            files["legs"] = save(pic, lg | lholes, FIGHT / name / "legs.png")
            if (hl.OUT / f"low_{name}_s{hl.PICKS.get(f'low_{name}', 6100)}.png").exists():
                hl.PICKS.setdefault(f"low_{name}", 6100)
                lp = hr.load(f"low_{name}").copy()
                lr, lgr, lb, la = chan(lp)
                area = (la > 128) & ~((lgr - lr > 45) & (lgr - lb > 45)) & grow(lg, 16) & ~c["up"]
                # Below the knee (where the bare legs wear their boots) it is the boots' picture.
                lab = np.zeros((N, N), np.uint8)
                lab[c["teal"] & lg] = 1
                lab[c["brown"] & lg] = 2
                lab = spread(lab, area | lg)
                fill(lp, c["rod"] & enclosed(area, 40), area & ~c["rod"])
                area |= c["rod"] & enclosed(area, 40)
                files["legs_set"] = save(lp, area, FIGHT / name / "legs_set.png")
                files["boots_set"] = save(lp, area & (lab == 2), FIGHT / name / "boots_set.png")
        out["moments"][name] = v
        dbg = Image.fromarray(pic).convert("RGB")
        dr = ImageDraw.Draw(dbg)
        e = hr.rot((90, 0), tilt)
        dr.line((mid[0] - e[0], mid[1] - e[1], mid[0] + e[0], mid[1] + e[1]), fill=(255, 0, 255), width=5)
        e = hr.rot((220, 0), blade)
        dr.line((grip[0], grip[1], grip[0] + e[0], grip[1] + e[1]), fill=(255, 255, 0), width=5)
        if grip2:
            e = hr.rot((220, 0), blade2)
            dr.line((grip2[0], grip2[1], grip2[0] + e[0], grip2[1] + e[1]), fill=(255, 120, 255), width=5)
        for g in grips:
            dr.ellipse((g[0] - 9, g[1] - 9, g[0] + 9, g[1] + 9), fill=(255, 60, 60))
        dr.ellipse((at[0] - 9, at[1] - 9, at[0] + 9, at[1] + 9), fill=(0, 200, 255))
        debug.append(dbg.crop((180, 120, 1380, 1400)).resize((450, 480)))
    (PARTS / "fight.json").write_text(json.dumps(out, indent=1))
    sheet = Image.new("RGB", (450 * 5, 480 * ((len(debug) + 4) // 5)), (70, 80, 75))
    for i, d in enumerate(debug):
        sheet.paste(d, (450 * (i % 5), 480 * (i // 5)))
    sheet.save(hl.OUT / "_fight_debug.png")


# ------------------------------------------------------------------------------------------------- the pose

LEGS = ["hips", "n_thigh", "n_shin", "n_foot", "f_thigh", "f_shin", "f_foot"]
BOOTS = ["n_shin", "n_foot", "f_shin", "f_foot"]


def bones(r: hr.Rig, side, stretch=None):
    """A leg's two bones at rest, stretched: (thigh vector, shin vector, (across, along))."""
    j = r.j
    sx, sy = (stretch or {}).get(side, (1, LEGS_LONG))
    k = hr.sub(j[f"{side}_knee"], j[f"{side}_hip"])
    a = hr.sub(j[f"{side}_ankle"], j[f"{side}_knee"])
    return (k[0] * sx, k[1] * sy), (a[0] * sx, a[1] * sy), (sx, sy)


def stand(r: hr.Rig, v, extra=None):
    """The rig pose under a drawn body: the hips sink until both legs reach their ankles."""
    s = dict(v["stand"])
    s.update(extra or {})
    p = r.pose(hips=s["root"], lean=v["lean"], nfoot=s["nfoot"], ffoot=s["ffoot"])
    p["n_foot"], p["f_foot"] = s.get("n_foot", 0), s.get("f_foot", 0)
    p["stretch"] = s.get("stretch")
    j = r.j
    for _ in range(200):
        waist = hr.add(j["waist"], p["root"])
        ok = True
        for side in "nf":
            hip = hr.add(waist, hr.rot(hr.sub(j[f"{side}_hip"], j["waist"]), p["hips"]))
            ankle = hr.add(j[f"{side}_ankle"], p[f"{side}_ankle"])
            k, a, _ = bones(r, side, p["stretch"])
            ok &= math.dist(hip, ankle) <= 0.985 * (math.hypot(*k) + math.hypot(*a))
        if ok:
            break
        p["root"][1] += 2
    p["root"][1] += s.get("sink", 0)
    return p


def legs(r: hr.Rig, p):
    """Rig.solve with stretched legs (the cut-out trick for a lunge deeper than the legs are long):
    every part's turn, place and scale."""
    turns, at = r.solve(p)
    scale = {part: (1, 1) for part in LEGS}
    for side in "nf":
        k, a, sc = bones(r, side, p.get("stretch"))
        hip = at[f"{side}_thigh"]
        ankle = hr.add(r.j[f"{side}_ankle"], p[f"{side}_ankle"])
        d1, d2 = hr.ik(hip, ankle, math.hypot(*k), math.hypot(*a), -1)
        t1, t2 = hr.norm(d1 - hr.ang(k)), hr.norm(d2 - hr.ang(a))
        turns[f"{side}_thigh"], turns[f"{side}_shin"] = t1, t2
        at[f"{side}_shin"] = hr.add(hip, hr.rot(k, t1))
        at[f"{side}_foot"] = hr.add(at[f"{side}_shin"], hr.rot(a, t2))
        scale[f"{side}_thigh"] = scale[f"{side}_shin"] = sc
    return turns, at, scale


def paste(out, im, origin, pivot, where, deg, scale=(1, 1)):
    c, s_ = math.cos(math.radians(-deg)), math.sin(math.radians(-deg))
    px, py = pivot[0] - origin[0], pivot[1] - origin[1]
    sx, sy = scale
    data = (c / sx, -s_ / sx, px - (c * where[0] - s_ * where[1]) / sx,
            s_ / sy, c / sy, py - (s_ * where[0] + c * where[1]) / sy)
    out.alpha_composite(im.transform(out.size, Image.AFFINE, data, Image.BICUBIC).convert("RGBA"))


class Fight:
    def __init__(self):
        self.r = hr.Rig()
        self.m = json.loads((PARTS / "fight.json").read_text())
        self.img = {}

    def pic(self, e):
        if e["file"] not in self.img:
            self.img[e["file"]] = Image.open(PARTS / e["file"]).convert("RGBa")
        return self.img[e["file"]], (e["x"], e["y"])

    def draw(self, name, gear=None, size=(N, N), offset=(0, 0), turn=0, extra=None, weapon="iron_sword", weapon2=None):
        """gear: {slot: set}, slots helm / chest / gloves / legs / boots. turn: the upper body's extra lean.
        weapon2: what the other fist holds, in a moment drawn with a rod in each fist."""
        r, v, gear = self.r, self.m["moments"][name], gear or {}
        f = v["files"]
        layers = []
        st = dict(v["stand"])
        st.update(extra or {})
        if st.get("drawn"):
            waist = hr.add((r.j["waist"][0] + st["root"][0], v["pivot"][1] + st["root"][1]), offset)
            for key, slot in (("legs", None), ("legs_set", "legs"), ("boots_set", "boots")):
                if key in f and (slot is None or gear.get(slot)):
                    im, origin = self.pic(f[key])
                    layers.append((Z[key], im, origin, v["pivot"], waist, turn, (1, 1)))
        else:
            p = stand(r, v, extra)
            turns, at, scale = legs(r, p)
            waist = hr.add(at["hips"], offset)
        for part in ([] if st.get("drawn") else LEGS):
            s = gear.get("boots") if part in BOOTS and gear.get("boots") else gear.get("legs") or "bare"
            e = self.m["hips"][s] if part == "hips" else r.m["sets"][s][part]
            im, origin = self.pic(e)
            layers.append((hr.TREE[part][2], im, origin, r.j[hr.TREE[part][1]], hr.add(at[part], offset), turns[part],
                           scale[part]))

        def upper(key, z):
            if key in f:
                im, origin = self.pic(f[key])
                layers.append((z, im, origin, v["pivot"], waist, turn, (1, 1)))

        chest, gloves = gear.get("chest") and "chest" in f, gear.get("gloves") and "gloves" in f
        armor = f"body_{gear.get('chest')}"
        if armor in f:  # a body armor drawn on this moment: its whole upper body, the hands as drawn
            chest = gloves = False
            upper(armor, Z["chest"])
            upper("fist", Z["fist"])
            upper("fist2", Z["fist"])
        elif not chest:
            upper("body", Z["body"])
            upper("fist", Z["fist"])
            upper("fist2", Z["fist"])
        else:
            upper("chest", Z["chest"])
            if not gloves:
                upper("hands", Z["hands"])
                upper("fist", Z["fist"])
        if gloves:
            upper("gloves", Z["gloves"])
            upper("glove_fist", Z["glove_fist"])
        neck = hr.add(waist, hr.rot(hr.sub(v["neck"], v["pivot"]), turn))
        im, origin = self.pic(r.m["sets"][gear.get("helm") or "bare"]["head"])
        layers.append((Z["head"], im, origin, r.j["neck"], neck, v["head"] + turn, (1, 1)))
        if v["face"]:
            im, origin = self.pic(self.m["faces"][v["face"]])
            layers.append((Z["face"], im, origin, r.j["neck"], neck, v["head"] + turn, (1, 1)))
        if weapon:
            w = r.m["weapons"][weapon]
            im, _ = self.pic({"file": w["file"], "x": 0, "y": 0})
            grip = hr.add(waist, hr.rot(hr.sub(v["grip"], v["pivot"]), turn))
            layers.append((v["weaponZ"], im, (0, 0), (w["gx"], w["gy"]), grip, v["blade"] + 90 + turn, (1, 1)))
        if weapon2 and "grip2" in v:
            w = r.m["weapons"][weapon2]
            im, _ = self.pic({"file": w["file"], "x": 0, "y": 0})
            grip = hr.add(waist, hr.rot(hr.sub(v["grip2"], v["pivot"]), turn))
            layers.append((v["weaponZ"] - 0.5, im, (0, 0), (w["gx"], w["gy"]), grip, v["blade2"] + 90 + turn, (1, 1)))
        out = Image.new("RGBA", size, (0, 0, 0, 0))
        for z, im, origin, pivot, where, deg, sc in sorted(layers, key=lambda q: q[0]):
            paste(out, im, origin, pivot, where, deg, sc)
        return out


HELD = {"left": ("iron_sword", None), "dual": ("rusty_dagger", "rusty_dagger"), "staff": ("oak_staff", None)}


def show_holds():
    """Every hold the cut has, ready and strike: art/redo/layers/_holds.png."""
    f = Fight()
    names = ["ready", "strike"] + [n for n in HOLD_MOMENTS if n in f.m["moments"]]
    cell, canvas, off = 500, (2000, 1700), (240, 180)
    h = int(cell * 0.85)
    sheet = Image.new("RGB", (cell * 4, h * ((len(names) + 3) // 4)), (70, 80, 75))
    for i, name in enumerate(names):
        w, w2 = HELD.get(name.rpartition("_")[0], ("iron_sword", None))
        im = f.draw(name, None, size=canvas, offset=off, weapon=w, weapon2=w2).resize((cell, h), Image.LANCZOS)
        sheet.paste(im, ((i % 4) * cell, (i // 4) * h), im)
    sheet.save(hl.OUT / "_holds.png")
    print(hl.OUT / "_holds.png")


def show(args):
    f = Fight()
    rows = [("bare", None), ("set", SET)]
    cell, canvas, off = 500, (2000, 1700), (240, 180)
    src = "--src" in args
    sheet = Image.new("RGB", (cell * len(MOMENTS), int(cell * 0.85) * (len(rows) + src)), (70, 80, 75))
    h = int(cell * 0.85)
    for x, name in enumerate(MOMENTS):
        if src:
            im = Image.new("RGBA", canvas, (0, 0, 0, 0))
            im.alpha_composite(Image.fromarray(hr.load(POSE.get(name, f"up_{name}"))), off)
            im = im.resize((cell, h), Image.LANCZOS)
            sheet.paste(im, (x * cell, 0), im)
        for y, (_, gear) in enumerate(rows):
            im = f.draw(name, gear, size=canvas, offset=off).resize((cell, h), Image.LANCZOS)
            sheet.paste(im, (x * cell, (y + src) * h), im)
    sheet.save(hl.OUT / "_fight.png")
    print(hl.OUT / "_fight.png")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "show"
    if cmd == "cut":
        cut()
    elif cmd == "holds":
        show_holds()
    else:
        show(sys.argv[2:])
