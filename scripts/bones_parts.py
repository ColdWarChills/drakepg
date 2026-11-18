#!/usr/bin/env python3
"""The hero on bones: checks and fixes for the part pictures (burst 2b; unity/Bones/PARTS.md says how to use it).

Reads the tracked rig (unity/Bones/hero/rig.json) and the swap list (unity/Bones/hero/swaps.json), composes the puppet
the way the bones code does (each part turns on its joint, children follow, draw order by z), and draws proof sheets.

  python scripts/bones_parts.py wiggle [--set bare_v2] [--out name]   every joint through its range, one sheet
  python scripts/bones_parts.py clock  [--set bare_v2] [--out name]   the fists holding each weapon round the clock
  python scripts/bones_parts.py recut                                 bare_v2 parts from the rig picture (rules below)
  python scripts/bones_parts.py check                                 swaps.json loads, files exist, puppet composes

Never writes over a kept picture: new parts go to unity/Bones/hero/bare_v2/, swaps to bare/<variant>.png.
"""
import json
import math
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
BONES = ROOT / "unity" / "Bones" / "hero"
SHEETS = ROOT / "art" / "tests" / "sheets" / "bones" / "parts"
TRIES = ROOT / "art" / "redo" / "bones_tries"
GROUND = (118, 104, 138, 255)  # a flat mid-tone that no part has, so gaps and slivers show
N = 1536


def save(im, path, fmt=None, **kw):
    """Write a picture atomically (a temp name in the same folder, then os.replace). A PNG whose pixels are already
    the same is not written again, so a rerun leaves kept and untouched pictures byte-identical."""
    path = Path(path)
    fmt = fmt or {".png": "PNG", ".webp": "WEBP"}[path.suffix.lower()]
    if fmt == "PNG" and path.exists():
        old = Image.open(path)
        if old.mode == im.mode and old.size == im.size and np.array_equal(np.asarray(old), np.asarray(im)):
            return False
    tmp = path.with_name(path.name + ".tmp")
    im.save(tmp, fmt, **kw)
    os.replace(tmp, path)
    return True


save_pic = save  # for functions with a `save` argument


def save_text(path, text):
    path = Path(path)
    if path.exists() and path.read_text() == text:
        return False
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(text)
    os.replace(tmp, path)
    return True


def font(size=20):
    try:
        return ImageFont.truetype("arialbd.ttf", size)
    except OSError:
        return ImageFont.load_default()


def rot(v, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def ik(a, target, l1, l2, bend):
    """Two bones from a to target (same as hero_layers_rig.ik): world directions of both, bend +1 clockwise."""
    dx, dy = target[0] - a[0], target[1] - a[1]
    d = max(abs(l1 - l2) + 0.5, min(math.hypot(dx, dy), l1 + l2 - 0.5))
    base = math.atan2(dy, dx)
    inner = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
    d1 = base + bend * inner
    mid = (a[0] + l1 * math.cos(d1), a[1] + l1 * math.sin(d1))
    end = (a[0] + d * math.cos(base), a[1] + d * math.sin(base))
    return math.degrees(d1), math.degrees(math.atan2(end[1] - mid[1], end[0] - mid[0]))


class Puppet:
    """The rig with a set of part pictures. sets: names layered over bare (rig.json sets, then swaps.json sets).
    swaps: {part: swap name in swaps.json} pictures that replace a part (with their own joints)."""

    def __init__(self, sets=(), swaps=None, parts=None):
        """parts: {part: (file, x, y)} pictures from anywhere (tries), over the sets."""
        self.rig = json.loads((BONES / "rig.json").read_text())
        sw = BONES / "swaps.json"
        self.sw = json.loads(sw.read_text()) if sw.exists() else {}
        self.j = {k: tuple(v) for k, v in self.rig["joints"].items()}
        self.tree = {p["name"]: (p["parent"], p["joint"], p["z"]) for p in self.rig["parts"]}
        self.pics = {}
        for part in self.tree:
            e = self.rig["sets"]["bare"][part]
            self.pics[part] = dict(file=e["file"], x=e["x"], y=e["y"], joints={})
        for s in sets:
            src = self.rig["sets"].get(s) or self.sw.get("sets", {}).get(s, {})
            for part, e in src.items():
                self.pics[part] = dict(file=e["file"], x=e["x"], y=e["y"], joints={})
        for part, (file, x, y) in (parts or {}).items():
            self.pics[part] = dict(file=str(file), x=x, y=y, joints={})
        self.grips = {k: dict(v) for k, v in self.rig["grips"].items()}
        for part, name in (swaps or {}).items():
            self.swap(part, name)
        self._img = {}

    def swap(self, part, name):
        e = self.sw["swaps"][name] if isinstance(name, str) else name
        self.pics[part] = dict(file=e["file"], x=e["x"], y=e["y"], joints={k: tuple(v) for k, v in e["joints"].items()},
                               z=e.get("z"))
        if "grip" in e:
            self.grips[part[0]] = dict(e["grip"])

    def J(self, part, joint):
        """Where `joint` sits in the rest layout according to `part`'s picture."""
        return self.pics[part]["joints"].get(joint, self.j[joint])

    def image(self, file):
        if file not in self._img:
            self._img[file] = Image.open(Path(file) if Path(file).is_absolute() else BONES / file).convert("RGBa")
        return self._img[file]

    def solve(self, local, root=(0, 0)):
        """World turn and pivot of every part from local turns (degrees, clockwise, 0 = the rest layout)."""
        turn, at = {}, {}
        for part, (parent, joint, _) in self.tree.items():
            if parent is None:
                turn[part] = local.get(part, 0)
                at[part] = add(self.J(part, joint), root)
            else:
                turn[part] = turn[parent] + local.get(part, 0)
                pj = self.tree[parent][1]
                at[part] = add(at[parent], rot(sub(self.J(parent, joint), self.J(parent, pj)), turn[parent]))
        return turn, at

    def layers(self, local, root=(0, 0), weapons=None, order=None):
        """[(z, image, origin, pivot, where, deg)] for every part and weapon. weapons: {"n": id, "f": id}."""
        turn, at = self.solve(local, root)
        out = []
        for part, (_, joint, z) in self.tree.items():
            pic = self.pics[part]
            z = (order or {}).get(part, pic.get("z") or z)
            out.append((z, self.image(pic["file"]), (pic["x"], pic["y"]), self.J(part, joint), at[part], turn[part]))
        for s, wid in (weapons or {}).items():
            if not wid:
                continue
            w = self.rig["weapons"][wid]
            hand, g = f"{s}_hand", self.grips[s]
            hold = add(at[hand], rot(sub(tuple(g["at"]), self.J(hand, f"{s}_wrist")), turn[hand]))
            z = (order or {}).get(f"{s}_weapon", self.rig["weaponZ"][s])
            out.append((z, self.image(w["file"]), (0, 0), (w["gx"], w["gy"]), hold, turn[hand] + g["angle"] + 90))
        return out, turn, at

    def draw(self, local=None, root=(0, 0), weapons=None, size=(N, N), offset=(0, 0), order=None, ground=None):
        lay, turn, at = self.layers(local or {}, root, weapons, order)
        out = Image.new("RGBA", size, ground or (0, 0, 0, 0))
        for z, im, origin, pivot, where, deg in sorted(lay, key=lambda v: v[0]):
            c, s_ = math.cos(math.radians(-deg)), math.sin(math.radians(-deg))
            px, py = pivot[0] - origin[0], pivot[1] - origin[1]
            wx, wy = where[0] + offset[0], where[1] + offset[1]
            data = (c, -s_, px - c * wx + s_ * wy, s_, c, py - s_ * wx - c * wy)
            out.alpha_composite(im.transform(size, Image.AFFINE, data, Image.BILINEAR).convert("RGBA"))
        return out, turn, at


    def from_pose(self, p):
        """A poses.json pose (world turns, legs by IK to the ankle targets) as (local turns, root, weapons, order)."""
        j = self.j
        world = {k: p.get(k, 0) for k in self.tree}
        waist = add(j["waist"], tuple(p["root"]))
        for s in "nf":
            hip = add(waist, rot(sub(j[f"{s}_hip"], j["waist"]), world["hips"]))
            ankle = add(j[f"{s}_ankle"], tuple(p[f"{s}_ankle"]))
            l1, l2 = math.dist(j[f"{s}_hip"], j[f"{s}_knee"]), math.dist(j[f"{s}_knee"], j[f"{s}_ankle"])
            d1, d2 = ik(hip, ankle, l1, l2, -1)
            r1, r2 = ik(j[f"{s}_hip"], j[f"{s}_ankle"], l1, l2, -1)
            world[f"{s}_thigh"], world[f"{s}_shin"] = d1 - r1, d2 - r2
            world[f"{s}_foot"] = p.get(f"{s}_foot", 0)
        local = {}
        for part, (parent, _, _) in self.tree.items():
            local[part] = world[part] - (world[parent] if parent else 0)
        weapons = dict(zip("nf", p.get("weapon") or []))
        order = dict(self.rig["farFront"]) if p.get("far_front") else None
        if order and "f_weapon" in order:
            order = {k: v for k, v in order.items()}
        return local, tuple(p["root"]), weapons, order

    def draw_pose(self, p, **kw):
        local, root, weapons, order = self.from_pose(p)
        return self.draw(local, root=root, weapons=weapons, order=order, **kw)


# ------------------------------------------------------------------------------------------------ recut
# The rules (geometric, so the same masks cut every gear picture of the rig pose, as hero_layers_rig's masks do):
# - CAPS: a child at a shoulder or hip gets a round cap: every drawn pixel within `cap` px of the pivot (its own and
#   its parent's), and gives its pixels behind the pivot line and outside the cap to the parent, so no straight cut
#   edge sweeps over the body. The parent keeps the child's pixels only within `keep` px (a smaller ball behind a far
#   limb, so no sleeve stub is left standing when the limb turns away).
# - The hips lose the belt (the torso brings it): under the belt they get the trousers carried up from just below
#   it, so a turning torso shows trousers, not a second belt. The carried-up part is rounded about the waist (each
#   side out to where the trousers end at the belt's lower edge), so its top corners can't stand out past the belt
#   end when the torso tilts.
# - The torso gets the neck under the jaw and the collar's back (copies of what the head has), so a turning head
#   opens no notch.
CAPS = {"n_shoulder": (62, 62), "f_shoulder": (40, 40), "n_hip": (62, 62), "f_hip": (62, 62)}
NEXT = {"n_shoulder": "n_elbow", "f_shoulder": "f_elbow", "n_hip": "n_knee", "f_hip": "f_knee"}
HIPS_FILL = (640, 706)  # rows: the trousers just below the belt (row 706) are carried up to row 640
HIPS_SIDE = 16  # rows under the belt whose trouser sides are carried on up (their slant)
NECK_FILL = ((792, 414), 24)  # a round neck under the jaw, above the collar (centre, radius)
COLLAR = [(700, 425), (712, 404), (745, 410), (790, 418), (800, 425)]  # the collar's back above the neck line


def own_territories(hr, alpha):
    """hero_layers_rig.territories before the parents take their joint circles (copied, not changed)."""
    np_ = np
    near, far = hr.poly(hr.NEAR_ARM), hr.poly(hr.FAR_ARM)
    for s, arm in (("n", near), ("f", far)):
        a, b = hr.J[f"{s}_shoulder"], hr.J[f"{s}_elbow"]
        arm &= ~hr.loose(alpha, arm, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
    t = {}
    for s, arm in (("n", near), ("f", far)):
        e, w = hr.J[f"{s}_elbow"], hr.J[f"{s}_wrist"]
        past_e = hr.beyond(e, w)
        past_w = hr.beyond(w, (2 * w[0] - e[0], 2 * w[1] - e[1]))
        t[f"{s}_upper"], t[f"{s}_fore"], t[f"{s}_hand"] = arm & ~past_e, arm & past_e & ~past_w, arm & past_w
    arms = near | far
    head = (hr.yy < hr.NECK_Y) & ~arms
    body = ~arms & ~head
    t["torso"] = body & (hr.yy < hr.BELT_BOTTOM)
    legs = np_.zeros((N, N), bool)
    for s in "nf":
        h, k, a = hr.J[f"{s}_hip"], hr.J[f"{s}_knee"], hr.J[f"{s}_ankle"]
        side = (hr.xx < hr.LEG_SPLIT) if s == "n" else (hr.xx >= hr.LEG_SPLIT)
        leg = body & side & hr.beyond(h, k) & (hr.yy > hr.BELT_BOTTOM)
        past_k = hr.beyond(k, a)
        past_a = hr.beyond(a, (2 * a[0] - k[0], 2 * a[1] - k[1]))
        t[f"{s}_thigh"], t[f"{s}_shin"], t[f"{s}_foot"] = leg & ~past_k, leg & past_k & ~past_a, leg & past_a
        legs |= leg
    t["hips"] = body & (hr.yy >= hr.BELT_TOP) & ~legs
    t["head"] = head
    return t


def recut_masks(hr, img, rig_alpha, caps=None, neck=True):
    """The territories come from the rig picture (as hero_layers_rig.cut does for every set); the drawn pixels
    (caps, fills) from `img`, the rig or a gear edit of it."""
    caps = CAPS if caps is None else caps
    fig = img[..., 3] > 128
    own = own_territories(hr, rig_alpha)
    m = {k: v.copy() for k, v in own.items()}
    m["head"] = own["head"] | (hr.disc(hr.J["neck"], hr.RADIUS["neck"]) & own["torso"])  # as before
    for part, (parent, joint, z) in hr.TREE.items():
        if not parent or part in ("head", "torso"):
            continue
        pv = hr.J[joint]
        if joint in caps:
            rc, rk = caps[joint]
            past = hr.beyond(pv, hr.J[NEXT[joint]])
            far_behind = own[part] & ~past & ~hr.disc(pv, rc)
            cap = hr.disc(pv, rc) & fig & (own[part] | own[parent])
            m[part] = (m[part] & ~far_behind) | cap
            m[parent] |= far_behind | (hr.disc(pv, rk) & own[part])
        else:
            m[parent] |= hr.disc(pv, hr.RADIUS[joint]) & own[part]
    if neck:
        m["torso"] |= (hr.disc(*NECK_FILL) | hr.poly(COLLAR)) & fig & (hr.yy < hr.NECK_Y)
    return m


def hips_fill(img, mask, waist):
    """The hips picture with the belt replaced by the trousers carried up from just below it, rounded about the
    waist: on each side only within the distance from the waist to that side's last trouser pixel on row `src`."""
    top, src = HIPS_FILL
    out = img.copy()
    has = (img[src, :, 3] > 200) & mask[src]
    new = mask.copy()
    new[:src] = False
    new[top:src] = has[None, :]  # bounded below by the round top and the side lines (not the rig's notched outline)
    xs = np.nonzero(has)[0]
    wx, wy = waist
    yy, xx = np.mgrid[top:src, 0:img.shape[1]]
    d2 = (xx - wx) ** 2 + (yy - wy) ** 2
    rl2, rr2 = (xs.min() - wx) ** 2 + (src - wy) ** 2, (xs.max() - wx) ** 2 + (src - wy) ** 2
    new[top:src] &= np.where(xx < wx, d2 <= rl2, d2 <= rr2)
    # and inside the trouser sides carried on up along their slant (fitted on the rows just under the belt), so a
    # lifted belt end shows the trouser side going on, not a straight column with the belt's notch in it
    rows = np.arange(src, src + HIPS_SIDE)
    for pick, keep in ((np.min, lambda x, edge: x >= edge), (np.max, lambda x, edge: x <= edge)):
        edge = [pick(np.nonzero((img[r, :, 3] > 200) & mask[r])[0]) for r in rows]
        b, a = np.polyfit(rows - src, edge, 1)
        new[top:src] &= keep(xx, a + b * (yy - src))
    out[top:src] = np.where(new[top:src, :, None], img[src][None], img[top:src])
    return out, new


SHOULDER_CLOTH = ("n_shoulder", "f_shoulder")


def cap_cloth(img, taken):
    """A shoulder cap takes the torso's pixels round the joint; what of them is not shirt (the neck's skin and the
    collar's outline) is painted over with the shirt next to it, so a raised arm shows no skin fleck on its cap."""
    rgb = img[..., :3].astype(int)
    mx, mn = rgb.max(-1), rgb.min(-1)
    shirt = (mx > 110) & ((mx - mn) < 0.5 * mx) & (img[..., 3] > 128)
    solid = img[..., 3] > 128
    skin = taken & solid & ~shirt & (mx > 110)
    if not skin.any():
        return img

    def grow(m, r):
        return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * r + 1))) > 0
    inner = ~grow(~solid, 2)  # the figure's own outline stays
    bad = skin | (grow(skin, 2) & taken & inner & ~shirt)  # the skin and the collar line beside it
    if not bad.any():
        return img
    out = img.copy()
    ys, xs = np.nonzero(bad)
    y0, x0, y1, x1 = ys.min() - 2, xs.min() - 2, ys.max() + 3, xs.max() + 3
    col = out[y0:y1, x0:x1, :3].astype(float)
    known = (shirt & ~bad)[y0:y1, x0:x1]
    todo = bad[y0:y1, x0:x1].copy()
    while todo.any():  # grow the shirt in from the edge of the fleck, a ring at a time
        acc, cnt = np.zeros_like(col), np.zeros(known.shape)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                k = np.roll(np.roll(known, dy, 0), dx, 1)
                acc += np.roll(np.roll(col, dy, 0), dx, 1) * k[..., None]
                cnt += k
        ring = todo & (cnt > 0)
        if not ring.any():
            break
        col[ring] = acc[ring] / cnt[ring][:, None]
        known |= ring
        todo &= ~ring
    out[y0:y1, x0:x1, :3] = np.round(col).astype(out.dtype)
    return out


def recut(source="rig", out_dir=None, caps=None, neck=True, parts=None):
    """Cut the rig picture (or a gear edit of it: a name in hero_layers.PICKS) by the rules above. Returns
    {part: (file, x, y)} for the parts that differ from the kept cut."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_layers_rig as hr  # only here: the other commands run from tracked files alone
    img = hr.load(source)
    rig = hr.load("rig")
    hr.refine(rig[..., 3])
    old = hr.territories(rig[..., 3])
    m = recut_masks(hr, img, rig[..., 3], caps, neck)
    out_dir = Path(out_dir or TRIES / "recut" / source)
    out_dir.mkdir(parents=True, exist_ok=True)
    done = {}
    own = own_territories(hr, rig[..., 3])
    for part in parts or hr.TREE:
        pic = img
        if part == "hips":
            pic, m[part] = hips_fill(img, m[part], hr.J["waist"])
        joint = hr.TREE[part][1]
        if joint in SHOULDER_CLOTH and joint in (CAPS if caps is None else caps):
            pic = cap_cloth(img, m[part] & own[hr.TREE[part][0]] & hr.disc(hr.J[joint], (caps or CAPS)[joint][0]))
        if (m[part] == old[part]).all() and pic is img:
            continue
        im, box = hr.crop(pic, m[part])
        path = out_dir / f"{part}.png"
        save(im, path)
        done[part] = (path, box[0], box[1])
    return done


def grip_hands(out_dir=None, source="grip"):
    """The fists from the grip picture (art/redo/layers/grip_s6100.png: both fists closed round a green rod), with the
    rod taken out: fingers drawn round a handle instead of the rig's empty fists. rig.json `grips` were measured from
    this same picture, so the weapon's handle runs where the rod ran. Same masks as the kept hands."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_layers_rig as hr
    rig = hr.load("rig")
    hr.refine(rig[..., 3])
    t = hr.territories(rig[..., 3])
    img = hr.load(source).copy()
    r, g, b, a = (img[..., i].astype(int) for i in range(4))
    green = (a > 128) & (g > 150) & (g - r > 70) & (g - b > 70)
    near = np.asarray(Image.fromarray((green * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 0
    spill = near & (g > r - 10) & (g > b + 20)  # the rod's anti-aliased fringe and its green glow on the skin
    img[green | spill, 3] = 0
    out_dir = Path(out_dir or TRIES / "hands")
    out_dir.mkdir(parents=True, exist_ok=True)
    done = {}
    for part in ("n_hand", "f_hand"):
        mask = t[part] & ~(green | spill)
        im, box = hr.crop(img, mask)
        path = out_dir / f"{part}.png"
        im.save(path)
        done[part] = (path, box[0], box[1])
    return done


# The blade angle round the clock (screen degrees, 0 = pointing right, 90 = down) and the wrist the fist keeps:
# the angle from the forearm to the blade, as hero_layers_rig.Rig.pose (-125 to -40 is a wrist that doesn't break).
CLOCK = [-90, -45, 0, 45, 90, 135, 180, -135]
# Proposed weapon grip points (rig.json weapons gy, px from the picture's top; rig.json is not changed here): the
# dagger's 276 puts the fist on its pommel, so the whole handle hides; the sword's 500 sits tight under the guard.
GRIP_Y = {"rusty_dagger": 264}
WRIST = {"n": -85, "f": -75}


def clock_pose(p: Puppet, s, blade, wrist=None, bend=30):
    """Local turns that put side s's blade at `blade` degrees: the forearm at blade - wrist, the upper arm bent."""
    w = WRIST[s] if wrist is None else wrist
    j = p.j
    r1 = math.degrees(math.atan2(*reversed(sub(j[f"{s}_elbow"], j[f"{s}_shoulder"]))))
    r2 = math.degrees(math.atan2(*reversed(sub(j[f"{s}_wrist"], j[f"{s}_elbow"]))))
    d2 = blade - w
    d1 = d2 + (bend if s == "n" else -bend)
    hand = blade - p.grips[s]["angle"]
    up, fore = d1 - r1, d2 - r2
    return {f"{s}_upper": up, f"{s}_fore": fore - up, f"{s}_hand": hand - fore}


def clock_sheet(rows, out, title, view=230):
    """rows: [(label, puppet, side, weapon)]: the fist round the clock, 1:1 crops centred on the grip."""
    W = 150 + len(CLOCK) * (view + 4)
    s = Image.new("RGB", (W, 56 + len(rows) * (view + 4)), (28, 26, 32))
    d = ImageDraw.Draw(s)
    d.text((8, 8), title, fill=(240, 232, 214), font=font(20))
    d.text((8, 32), "Blade direction in screen degrees (0 = right, 90 = down); the forearm keeps one wrist angle; "
                    "crops at full size round the grip.", fill=(190, 184, 170), font=font(13))
    y = 56
    for label, p, side, wid in rows:
        d.text((8, y + view // 2 - 18), label, fill=(240, 232, 214), font=font(14))
        d.text((8, y + view // 2 + 2), wid, fill=(190, 184, 170), font=font(13))
        for i, blade in enumerate(CLOCK):
            im, turn, at = p.draw(clock_pose(p, side, blade), weapons={side: wid})
            hand = f"{side}_hand"
            g = p.grips[side]
            c = add(at[hand], rot(sub(tuple(g["at"]), p.J(hand, f"{side}_wrist")), turn[hand]))
            fig = im.crop((round(c[0] - view / 2), round(c[1] - view / 2), round(c[0] + view / 2), round(c[1] + view / 2)))
            t = Image.new("RGBA", fig.size, GROUND)
            t.alpha_composite(fig)
            x = 150 + i * (view + 4)
            s.paste(t.convert("RGB"), (x, y))
            d.text((x + 4, y + 3), f"{blade:+d}", fill=(255, 255, 255), font=font(13))
        y += view + 4
    out.parent.mkdir(parents=True, exist_ok=True)
    save(s, out, quality=85, method=6)
    print(out, s.size, out.stat().st_size // 1024, "KB")


def face_cut(seed, out_dir=None):
    """A face edit cut the way hero_layers_fight cuts face_shout / face_hurt: what the edit changed on the rig's head,
    the face only (no hair), as a picture laid over the head (z 61, turns with the head)."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_layers_fight as hf
    import hero_layers_rig as hr
    import hero_poses as hp
    rig = hr.load("rig")
    f = hr.derim(np.asarray(hp.cleaned(TRIES / "qwen" / f"face_grit_s{seed}.png")))
    d = np.abs(f[..., :3].astype(int) - rig[..., :3].astype(int)).sum(-1)
    yy = hr.yy
    ch = (d > 55) & (yy < hr.NECK_Y + 30) & (f[..., 3] > 128)
    ch = hf.grow(hf.shrink(ch, 1), 6) & (f[..., 3] > 128)
    ch &= hf.grow(hf.shrink(hf.skin_of(rig) & (yy < hr.NECK_Y + 30), 6), 13)
    ys, xs = np.nonzero(ch)
    box = hr.disc((np.median(xs), np.median(ys)), 150)
    kept = hf.solid(ch & box)
    im, b = hr.crop(f, kept)
    if seed in FACE_FIX:  # after the crop, so the picture keeps its size and place
        im = far_eye_fix(im, f, rig, b, **FACE_FIX[seed])
    out_dir = Path(out_dir or TRIES / "face")
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f"face_grit_s{seed}.png"
    save(im, path)
    return path, b[0], b[1]


# The grit edit's far eye (his left, on screen right), in the cut picture's pixels: the cut left a hole at the eye's
# outer edge (the calm eye showed through, pupil and highlight), and the edit drew a grey smear under its outer corner.
# hole: the box where what the edit drew is filled in; smear: rows of the smear and the hair lock's edge beside it
# (x = x0 + slope * (y - y0)): cheek skin up to that edge, nothing past it (the head's own hair shows there).
FACE_FIX = {7100: dict(hole=(105, 15, 130, 50), smear=(113, 48, 61), edge=(126, 48, -0.55))}


def far_eye_fix(im, f, rig, b, hole, smear, edge):
    g = np.array(im.convert("RGBA")).astype(int)
    h, w = g.shape[:2]
    S = f[b[1]:b[1] + h, b[0]:b[0] + w].astype(int)
    R = rig[b[1]:b[1] + h, b[0]:b[0] + w].astype(int)
    yy, xx = np.mgrid[:h, :w]
    x0, y0, x1, y1 = hole
    d = np.abs(S[..., :3] - R[..., :3]).sum(-1)
    fill = (xx >= x0) & (xx < x1) & (yy >= y0) & (yy <= y1) & (g[..., 3] < 128) & (d > 55) & (S[..., 3] > 128) \
        & (R[..., 3] > 128)
    g[fill, :3], g[fill, 3] = S[fill, :3], 255
    ex, ey, slope = edge
    line = ex + slope * (yy - ey)
    mx, mn = g[..., :3].max(-1), g[..., :3].min(-1)
    skin = (g[..., 0] > 180) & (g[..., 1] > 110) & (g[..., 2] < 140) & (g[..., 3] > 200)
    sx, sy0, sy1 = smear
    col = np.median(g[(yy >= sy1 - 6) & (yy <= sy1 + 5) & (xx >= sx - 8) & (xx <= sx + 5) & skin][:, :3], 0)
    hair_edge = (S[..., 0] > S[..., 2] + 15) & (S[..., :3].max(-1) < 130) & (S[..., :3].max(-1) >= 40) \
        & (np.abs(xx - line) < 1.6)
    zone = (xx >= sx) & (yy >= sy0) & (yy <= sy1) & (g[..., 3] > 0) & ~skin & (mx > 35) & (xx < line) & ~hair_edge
    g[zone, :3], g[zone, 3] = col.astype(int), 255
    g[(xx >= line + 1.6) & (yy >= sy0 - 8) & (yy <= sy1 + 1), 3] = 0
    return Image.fromarray(g.astype(np.uint8))


def faces_sheet(grit, out, title, extra=()):
    """The bare head with each face laid on it at its place (calm, shout, hurt, grit and any extra tries)."""
    fj = json.loads((BONES / "fight.json").read_text())["faces"]
    e = json.loads((BONES / "rig.json").read_text())["sets"]["bare"]["head"]
    head = Image.open(BONES / e["file"]).convert("RGBA")
    faces = [("calm (the head as it is)", None), ("shout (kept)", (BONES / fj["shout"]["file"], fj["shout"]["x"], fj["shout"]["y"])),
             ("hurt (kept)", (BONES / fj["hurt"]["file"], fj["hurt"]["x"], fj["hurt"]["y"])), ("grit (new)", grit)] + list(extra)
    box = (e["x"] + 40, e["y"] + 20, e["x"] + 300, e["y"] + 280)
    cell = 390
    s = Image.new("RGB", (len(faces) * (cell + 6) + 6, cell + 60), (28, 26, 32))
    d = ImageDraw.Draw(s)
    d.text((8, 6), title, fill=(240, 232, 214), font=font(18))
    for i, (label, f) in enumerate(faces):
        c = Image.new("RGBA", (N, N), GROUND)
        c.alpha_composite(head, (e["x"], e["y"]))
        if f:
            c.alpha_composite(Image.open(f[0]).convert("RGBA"), (f[1], f[2]))
        x = 6 + i * (cell + 6)
        s.paste(c.crop(box).resize((cell, cell), Image.LANCZOS).convert("RGB"), (x, 52))
        d.text((x + 4, 32), label, fill=(255, 220, 140) if "new" in label else (220, 214, 200), font=font(14))
    out.parent.mkdir(parents=True, exist_ok=True)
    save(s, out, quality=88, method=6)
    print(out, s.size)


# ------------------------------------------------------------------------------------------------ Qwen jobs
# Edits of the rig picture (art/redo/layers/rig_s6100.png) on the same 1536 canvas, so the parts cut from them sit
# where the rig's parts sit. Tries go to art/redo/bones_tries/qwen/<name>_s<seed>.png.
STYLE = ("the same flat cartoon art style with hard-edged cel shadows, thin dark outlines and a warm golden rim light "
         "along his edges")
KEEP = ("Keep everything else of <image1> exactly the same: the same young hero with messy dark brown hair and the red "
        f"headband, the same face, the same cream shirt with rolled-up sleeves, the same belt, trousers and boots, "
        f"the same size and colours, {STYLE}.")
STILL = ("His head, his belt, his hips, both legs and both feet stay exactly where they are, at exactly the same size. "
         "Both arms stay perfectly straight, held out away from his body pointing down and outward like the letter A, "
         "with a clear gap between each arm and his body, both hands closed into empty fists; the arms now hang from "
         "his turned shoulders.")
NOTHING = "No weapon anywhere. No ground, no shadow, no text."
QWEN = {
    # Chest open to the viewer (the brief tied this to the wind-up; the drawn stances show it at the strike).
    "torso_toward": ("Edit <image1>. Change only how his upper body is turned: his chest and shoulders now turn to face "
                     "the viewer almost squarely, a near front view of the torso, while his hips and legs stay in the "
                     "three-quarter view facing right. His right shoulder, on the left of the picture, is pulled back, "
                     "so both shoulders look about equally wide; the opening of his shirt collar sits in the middle of "
                     f"his chest; his chest looks broad and open. {STILL} {KEEP} {NOTHING}"),
    # Chest turned away, back and near side showing (the brief: strike; the drawn stances: the wind-up).
    "torso_away": ("Edit <image1>. Change only how his upper body is turned: his chest and shoulders now turn away from "
                   "the viewer, toward the right, nearly into a side view, so we see his right side and a little of his "
                   "upper back. His right shoulder, on the left of the picture, is driven forward toward the right; his "
                   "left shoulder is mostly hidden behind his chest; the opening of his shirt collar is near the right "
                   f"edge of his chest; his upper back is slightly rounded. {STILL} {KEEP} {NOTHING}"),
    # Gritted teeth, brows down: the effort of a wind-up (made like face_shout / face_hurt in hero_layers.py).
    "face_grit": ("Edit <image1>. Change only the expression on his face: straining with effort, his teeth tightly "
                  "clenched and bared in a grimace with the corners of his mouth pulled wide, his eyebrows drawn down "
                  "hard and low, his eyes narrowed and fixed straight ahead. His head stays in exactly the same place at "
                  "the same size and angle; his hair, headband and neck are unchanged. "
                  "Keep everything else of <image1> exactly the same: the same young hero with messy dark brown hair "
                  "and the red headband, the same face, the same body and exactly the same pose with every limb in "
                  f"exactly the same place, both fists in exactly the same place, {STYLE}. {NOTHING}"),
}
# Second references (drawn stance bodies, art/redo/layers/parts/fight/<moment>/body.png) for the "+ref" variants.
REF = {"torso_toward": "wind", "torso_away": "strike"}
REF_LINE = {"torso_toward": " Turn his chest and shoulders the way the upper body in <image2> is turned, but keep "
                            "his arms straight in the A shape: only the turn of the chest and shoulders comes from "
                            "<image2>.",
            "torso_away": " Turn his chest and shoulders the way the upper body in <image2> is turned, but keep his "
                          "arms straight in the A shape and his back upright: only the turn of the chest and shoulders "
                          "comes from <image2>."}


ARMLESS = ("Edit <image1>. It shows the upper body of a cartoon hero, without his head. Remove both of his arms "
           "completely, from the shoulders down: no upper arms, no forearms, no hands, no fists. Keep his torso "
           "exactly as it is: the same cream shirt turned exactly the same way, the same collar and neck opening, the "
           "same belt and buckle, at exactly the same size and place, with the same colours and the same thin dark "
           "outlines. Where an arm covered the shirt, show the shirt behind it. Each shoulder ends in a smooth, rounded "
           f"shoulder of the shirt, drawn with the same thin dark outline. {STYLE}. No arms, no head, no weapon, no "
           "ground, no shadow, no text.")
TWIST = ("Edit <image1>. Change his upper body: he twists at the waist {how} His arms are held straight out to the sides "
         "and a little down, well clear of his body, both hands closed into empty fists. His hips, belt, legs and feet "
         "stay the same and in the same place; his head turns with his chest. " + KEEP + " " + NOTHING)
QWEN.update({
    # Cut-and-repaint: the drawn stance bodies with the arms taken off.
    "toward_armless": ("fight:wind", ARMLESS),
    "away_armless": ("fight:strike", ARMLESS),
    # Free twist of the whole rig picture (arms out of the way), the torso cut out and fitted to waist and neck after.
    "toward_free": ("rig", TWIST.format(how="so that his chest and both shoulders face the viewer squarely: a front "
                                          "view of the chest, both shoulders equally wide, the shirt's collar "
                                          "opening in the middle of the chest, his right shoulder pulled back.")),
    "away_free": ("rig", TWIST.format(how="so that his chest turns away from the viewer toward the right, nearly a "
                                        "side view with a little of his upper back showing: his right shoulder, on the "
                                        "left of the picture, comes forward across his body, his left shoulder is "
                                        "hidden behind his chest, the collar opening is at the right edge of the "
                                        "chest, and he leans forward a little.")),
})


ARMS_A = ("Edit <image1>. Change only his two arms: both arms are now perfectly straight and held out away from his body, "
          "pointing down and outward at 45 degrees like the letter A, hanging from his shoulders exactly where they are "
          "now, with a wide clear gap between each arm and his body, both hands closed into empty fists. No rod, no "
          "weapon. His head, his face, his chest, shoulders and shirt stay exactly as they are, turned exactly the same "
          "way, at the same size and place; his belt, legs and feet are unchanged. " + KEEP + " No ground, no shadow, "
          "no text.")
QWEN.update({
    # The drawn stances (with head, so the collar and neck survive), arms moved out of the way: the torso keeps its turn.
    "wind_arms": ("up:up_wind_s6111", ARMS_A),
    "strike_arms": ("up:up_strike_s6100", ARMS_A),
    # The wind-up's body shows the back of the shirt: say so, or the edit turns the chest back to the front.
    "wind_back_arms": ("up:up_wind_s6111", ARMS_A.replace(
        "His head, his face, his chest,", "His upper body stays turned away from the viewer exactly as it is now: we "
        "see the back of his shirt and his shoulder blades, with no collar opening, no buttons and no chest visible. "
        "His head, his face,")),
})


def qwen_source(src):
    """The picture an edit starts from, on the 1536 canvas: the rig, or a drawn stance body at its place."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_poses as hp
    if src == "rig":
        return hp.cleaned(ROOT / "art" / "redo" / "layers" / "rig_s6100.png")
    if src.startswith("up:"):
        return hp.cleaned(ROOT / "art" / "redo" / "layers" / f"{src[3:]}.png")
    moment = src.split(":")[1]
    e = json.loads((BONES / "fight.json").read_text())["moments"][moment]["files"]["body"]
    c = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    c.alpha_composite(Image.open(ROOT / "art" / "redo" / "layers" / "parts" / e["file"]).convert("RGBA"), (e["x"], e["y"]))
    return c


def qwen(name, seeds, ref=False):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_poses as hp
    from art_tests import write_notes
    from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy
    out = TRIES / "qwen"
    out.mkdir(parents=True, exist_ok=True)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    entry = QWEN[name]
    src, body = entry if isinstance(entry, tuple) else ("rig", entry)
    refs = [comfy.upload(f"bones_{src.replace(':', '_')}.png", hp.on_white(qwen_source(src)))]
    tag = name
    if ref:
        b = Image.open(ROOT / "art" / "redo" / "layers" / "parts" / "fight" / REF[name] / "body.png").convert("RGBA")
        refs.append(comfy.upload(f"bones_ref_{REF[name]}.png", hp.on_white(b)))
        body += REF_LINE[name]
        tag = name + "_ref"
    prompt = f"{RGBA_OPEN} {body} {RGBA_CLOSE}"
    for seed in seeds:
        path = out / f"{tag}_s{seed}.png"
        if path.exists():
            continue
        print(path.relative_to(ROOT), flush=True)
        path.write_bytes(comfy.generate(prompt, refs, None, seed, 30))
        write_notes(path.with_suffix(".txt"), comfy.last_graph)


# ------------------------------------------------------------------------------------------------ wiggle
# joint: (part that turns on it, angles). Angles are local turns, clockwise on screen. Elbows bend the hand forward
# (to the right, where he faces), knees bend the shin back.
WIGGLE = [
    ("n_shoulder", "n_upper", [-160, -120, -80, -40, 0, 40, 80, 120]),
    ("n_elbow", "n_fore", [-150, -120, -90, -60, -30, 0, 20]),
    ("n_wrist", "n_hand", [-90, -60, -30, 0, 30, 60, 90]),
    ("f_shoulder", "f_upper", [-160, -120, -80, -40, 0, 40, 80, 120]),
    ("f_elbow", "f_fore", [-150, -120, -90, -60, -30, 0, 20]),
    ("f_wrist", "f_hand", [-90, -60, -30, 0, 30, 60, 90]),
    ("neck", "head", [-35, -20, -10, 0, 10, 20, 35]),
    ("waist", "torso", [-35, -20, -10, 0, 10, 20, 35]),
    ("n_hip", "n_thigh", [-60, -40, -20, 0, 20, 40, 60]),
    ("n_knee", "n_shin", [-20, 0, 30, 60, 90, 110, 130]),
    ("n_ankle", "n_foot", [-35, -20, 0, 20, 35]),
    ("f_hip", "f_thigh", [-60, -40, -20, 0, 20, 40, 60]),
    ("f_knee", "f_shin", [-20, 0, 30, 60, 90, 110, 130]),
    ("f_ankle", "f_foot", [-35, -20, 0, 20, 35]),
]


def wiggle_cells(p: Puppet, cell=240, view=340, rows=None):
    """[(joint, [(angle, tile, cover)])]: the joint at the tile's centre, `view` canvas px across."""
    spec, rows = rows or WIGGLE, []
    for joint, part, angles in spec:
        tiles = []
        for a in angles:
            im, turn, at = p.draw({part: a}, ground=None)
            c = at[part]
            box = (round(c[0] - view / 2), round(c[1] - view / 2), round(c[0] + view / 2), round(c[1] + view / 2))
            fig = im.crop(box)
            al = np.asarray(fig.getchannel("A")) > 128
            yy, xx = np.mgrid[:view, :view]
            r = p.rig["radius"].get(joint, 30) * 0.8
            disc = (xx - view / 2) ** 2 + (yy - view / 2) ** 2 <= r * r
            cover = al[disc].mean()
            tile = Image.new("RGBA", fig.size, GROUND)
            tile.alpha_composite(fig)
            tiles.append((a, tile.convert("RGB").resize((cell, cell), Image.LANCZOS), cover))
        rows.append((joint, tiles))
    return rows


def wiggle_sheet(rows_by_set, title, out, cell=200):
    """rows_by_set: [(label, rows)]; with two sets each joint gets a 'before' row and an 'after' row."""
    cols = max(len(t) for _, rows in rows_by_set for _, t in rows)
    lw = 150
    nrows = sum(len(rows) for _, rows in rows_by_set)
    W, H = lw + cols * (cell + 4), 60 + nrows * (cell + 26)
    sheet = Image.new("RGB", (W, H), (28, 26, 32))
    d = ImageDraw.Draw(sheet)
    d.text((10, 10), title, fill=(240, 232, 214), font=font(22))
    d.text((10, 36), "Each tile: one joint turned (angle in degrees, + = clockwise), the rest still; the joint at the centre. "
                     "Ground is flat so gaps show.", fill=(190, 184, 170), font=font(14))
    y = 60
    order = []
    joints = [j for j, _ in rows_by_set[0][1]]
    for j in joints:
        for label, rows in rows_by_set:
            order.append((label, dict(rows)[j], j))
    for label, tiles, j in order:
        d.text((8, y + 30), j, fill=(240, 232, 214), font=font(18))
        if len(rows_by_set) > 1:
            d.text((8, y + 56), label, fill=(255, 200, 90) if label != rows_by_set[0][0] else (170, 170, 170),
                   font=font(16))
        for i, (a, tile, cover) in enumerate(tiles):
            x = lw + i * (cell + 4)
            sheet.paste(tile.resize((cell, cell), Image.LANCZOS), (x, y + 22))
            d.text((x + 4, y + 2), f"{a:+d}", fill=(220, 214, 200), font=font(15))
        y += cell + 26
    out.parent.mkdir(parents=True, exist_ok=True)
    save(sheet, out, quality=82, method=6)
    print(out, sheet.size, out.stat().st_size // 1024, "KB")


CLOSE = [("n_shoulder", "n_upper", [-160, -120, -40, 40, 100]), ("f_shoulder", "f_upper", [-140, -90, 40, 80, 120]),
         ("neck", "head", [-20, -12, 12, 20, 35]), ("waist", "torso", [-26, -10, 10, 26]),
         ("n_hip", "n_thigh", [-60, -20, 40, 60]), ("f_hip", "f_thigh", [-60, 20, 40, 60])]


def closeup_sheet(rows, out, title, specs=CLOSE, view=200):
    """1:1 crops (no scaling) round each joint for each puppet in rows [(label, puppet)], one block per joint."""
    cols = max(len(a) for _, _, a in specs)
    W = 120 + cols * (view + 4)
    H = 50 + sum(len(rows) * (view + 4) + 24 for _ in specs)
    s = Image.new("RGB", (W, H), (28, 26, 32))
    d = ImageDraw.Draw(s)
    d.text((8, 8), title, fill=(240, 232, 214), font=font(20))
    d.text((8, 32), "Crops at full size (1 canvas px = 1 px), the joint at the centre.", fill=(190, 184, 170),
           font=font(13))
    y = 50
    for joint, part, angles in specs:
        d.text((8, y + 2), joint, fill=(255, 220, 140), font=font(16))
        y += 24
        for label, p in rows:
            d.text((8, y + view // 2 - 8), label, fill=(240, 232, 214), font=font(14))
            for i, a in enumerate(angles):
                im, turn, at = p.draw({part: a})
                c = at[part]
                if joint == "neck":
                    c = (c[0] - 10, c[1] - 20)
                fig = im.crop((round(c[0] - view / 2), round(c[1] - view / 2), round(c[0] + view / 2),
                               round(c[1] + view / 2)))
                t = Image.new("RGBA", fig.size, GROUND)
                t.alpha_composite(fig)
                x = 120 + i * (view + 4)
                s.paste(t.convert("RGB"), (x, y))
                d.text((x + 4, y + 3), f"{a:+d}", fill=(255, 255, 255), font=font(13))
            y += view + 4
    out.parent.mkdir(parents=True, exist_ok=True)
    save(s, out, quality=85, method=6)
    print(out, s.size, out.stat().st_size // 1024, "KB")


def poses_sheet(rows, names, out, title, cell=360, box=(300, 150, 1340, 1430)):
    """Whole puppets in poses.json poses: rows [(label, puppet)], one column per pose."""
    poses = json.loads((BONES / "poses.json").read_text())
    bw, bh = box[2] - box[0], box[3] - box[1]
    ch = round(cell * bh / bw)
    s = Image.new("RGB", (130 + len(names) * (cell + 4), 50 + len(rows) * (ch + 4)), (28, 26, 32))
    d = ImageDraw.Draw(s)
    d.text((8, 8), title, fill=(240, 232, 214), font=font(18))
    for i, n in enumerate(names):
        d.text((130 + i * (cell + 4) + 4, 32), n, fill=(220, 214, 200), font=font(14))
    for r, (label, p) in enumerate(rows):
        y = 50 + r * (ch + 4)
        d.text((8, y + ch // 2 - 10), label, fill=(240, 232, 214), font=font(14))
        for i, n in enumerate(names):
            im, _, _ = p.draw_pose(poses[n], size=(N + 300, N), offset=(150, 0))
            t = Image.new("RGBA", im.size, GROUND)
            t.alpha_composite(im)
            t = t.crop((box[0] + 150, box[1], box[2] + 150, box[3]))
            s.paste(t.convert("RGB").resize((cell, ch), Image.LANCZOS), (130 + i * (cell + 4), y))
    out.parent.mkdir(parents=True, exist_ok=True)
    save(s, out, quality=85, method=6)
    print(out, s.size, out.stat().st_size // 1024, "KB")


def args_set(args):
    sets = []
    while "--set" in args:
        i = args.index("--set")
        sets.append(args[i + 1])
        del args[i:i + 2]
    return sets


# ------------------------------------------------------------------------------------------------ swap torsos
# A swap torso is cut from a picture whose chest is already turned (an edit of a drawn stance with the arms moved out
# of the way), brought into the rest layout: the belt's middle onto the rig's waist, the belt level (fight.json gives
# each stance's belt middle `pivot` and tilt `lean`). Its joints are measured on the aligned picture.
TORSO_SRC = {
    # neck/n_shoulder/f_shoulder: joints measured on the aligned picture (the waist is the rig's by alignment).
    # n_seam/f_seam: where the sleeve meets the body (shoulder top, armpit); the arm side of it goes, except the
    # shoulder ball (CAPS keep radius, or n_keep/f_keep) so the base upper arm's round cap lands on cloth; the far
    # ball is dropped on both (it stood out as a round stub past the turned chest). belt: the belt's lower edge.
    "torso_toward": dict(src="strike_arms_s7300", moment="strike", neck=(870, 400), n_shoulder=(684, 478),
                         f_shoulder=(912, 468), n_seam=((700, 418), (680, 572)), f_seam=((932, 380), (908, 532)),
                         belt=720, f_keep=0),
    # s7401 over s7400: its arms hang lower, so the shoulders slope instead of standing square, and no rim fleck
    "torso_away": dict(src="wind_back_arms_s7401", moment="wind", neck=(830, 374), n_shoulder=(668, 458),
                       f_shoulder=(868, 446), n_seam=((652, 392), (668, 600)), f_seam=((858, 382), (888, 506)),
                       belt=716, f_keep=0),
}


def _side(xx, yy, a, b):
    """Sign of each pixel against the line a->b (+ = right of the direction a->b, on screen)."""
    return (b[0] - a[0]) * (yy - a[1]) - (b[1] - a[1]) * (xx - a[0])


def _joined(mask, seed):
    """The part of `mask` 4-connected to the seed pixel (y, x): grown a step at a time inside the mask."""
    ys, xs = np.nonzero(mask)
    y0, x0, y1, x1 = ys.min(), xs.min(), ys.max() + 1, xs.max() + 1
    m = mask[y0:y1, x0:x1]
    r = np.zeros_like(m)
    r[seed[0] - y0, seed[1] - x0] = True
    while True:
        g = r.copy()
        g[1:] |= r[:-1]
        g[:-1] |= r[1:]
        g[:, 1:] |= r[:, :-1]
        g[:, :-1] |= r[:, 1:]
        g &= m
        if (g == r).all():
            break
        r = g
    out = np.zeros_like(mask)
    out[y0:y1, x0:x1] = r
    return out


def torso_cut(name, spec=None):
    """The swap torso: the aligned try minus the head (above the neck line), minus the arms past the seams (the
    shoulder ball kept), down to the belt's lower edge; plus the neck disc under the jaw, like bare_v2's torso.
    Returns (picture cropped, x, y, joints)."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_layers_rig as hr
    import hero_poses as hp
    s = dict(TORSO_SRC[name], **(spec or {}))
    im = hp.cleaned(TRIES / "qwen" / f"{s['src']}.png")
    im = Image.fromarray(hr.derim(np.array(im)))
    al, _ = align(im, s["moment"])
    a = np.array(al)
    fig = a[..., 3] > 0
    yy, xx = np.mgrid[:N, :N]
    nx, ny = s["neck"]
    # the head: what is above the neck line and not shirt (skin, hair, headband; the shirt is the low-saturation
    # light colour), so the collar's back and the shoulders keep their own tops
    rgb = a[..., :3].astype(int)
    mx, mn = rgb.max(-1), rgb.min(-1)
    shirt = (mx > 110) & ((mx - mn) < 0.5 * mx)
    head = (yy < ny + 11) & (~shirt | (yy < ny - 60))
    trousers = (rgb[..., 1] > rgb[..., 0] + 5) & (mx < 100)  # dark green under the belt: the hips bring it
    keep = fig & ~head & ~trousers & (yy <= s["belt"]) & (mn <= 210)  # and no white rim-light flecks
    # nor the cream rim light on the figure's outline in the belt's last rows (it stood as a white speck at the belt end)
    inner = np.asarray(Image.fromarray((fig * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(7))) > 0
    keep &= ~((yy > s["belt"] - 6) & fig & ~inner & (mx > 140) & (mn > 100))
    for side, sgn in (("n", 1), ("f", -1)):
        p, q = s[f"{side}_seam"]
        # the near arm is left of its seam (drawn top->bottom), the far arm right of it
        arm = (_side(xx, yy, p, q) * sgn) > 0
        sx, sy = s[f"{side}_shoulder"]
        rk = s.get(f"{side}_keep", CAPS[f"{side}_shoulder"][1])
        ball = (xx - sx) ** 2 + (yy - sy) ** 2 <= rk * rk
        keep &= ~arm | ball
    keep = _joined(keep, (s["belt"] - 30, 780))  # no specks of teeth or hair left floating above the shirt
    (cx, cy), r = (nx, ny), NECK_FILL[1]
    keep |= fig & ((xx - cx) ** 2 + (yy - cy) ** 2 <= r * r)
    a[..., 3] = np.where(keep, a[..., 3], 0)
    out = Image.fromarray(a)
    x0, y0, x1, y1 = out.getbbox()
    rj = json.loads((BONES / "rig.json").read_text())["joints"]
    joints = {"waist": tuple(rj["waist"]), "neck": s["neck"], "n_shoulder": s["n_shoulder"],
              "f_shoulder": s["f_shoulder"]}
    return out.crop((x0, y0, x1, y1)), x0, y0, joints


def torso_puppet(name, spec=None, sets=("bare_v2",), save=None):
    """A puppet on bare_v2 with the swap torso; the picture is written to `save` (or a try file)."""
    pic, x, y, joints = torso_cut(name, spec)
    path = Path(save) if save else TRIES / "torso" / f"{name}.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    save_pic(pic, path)
    p = Puppet(sets=list(sets))
    p.swap("torso", dict(file=str(path.resolve()), x=x, y=y, joints={k: list(v) for k, v in joints.items()}))
    return p, (str(path), x, y, joints)


def align(img: Image.Image, moment):
    """The picture turned and moved so the stance's belt lies level with its middle on the rig's waist."""
    m = json.loads((BONES / "fight.json").read_text())["moments"][moment]
    (px, py), lean = m["pivot"], m["lean"]
    wx, wy = json.loads((BONES / "rig.json").read_text())["joints"]["waist"]
    c, s_ = math.cos(math.radians(lean)), math.sin(math.radians(lean))
    # output point q -> source point: R(lean)(q - waist) + pivot (turning the picture back by -lean)
    data = (c, -s_, px - c * wx + s_ * wy, s_, c, py - s_ * wx - c * wy)
    pre = img.convert("RGBa").transform(img.size, Image.AFFINE, data, Image.BILINEAR)
    return pre.convert("RGBA"), (m["neck"], m["pivot"], lean)


def measure_sheet(img: Image.Image, out, marks=None, box=(560, 330, 1010, 760), zoom=2):
    """A 1:1 (x zoom) crop with a 20 px grid and the rig's joints, to read joint places off."""
    t = Image.new("RGBA", img.size, GROUND)
    t.alpha_composite(img)
    d = ImageDraw.Draw(t)
    for x in range(0, N, 20):
        d.line((x, 0, x, N), fill=(255, 255, 255, 40 if x % 100 else 110))
    for y in range(0, N, 20):
        d.line((0, y, N, y), fill=(255, 255, 255, 40 if y % 100 else 110))
    rj = json.loads((BONES / "rig.json").read_text())["joints"]
    for k in ("waist", "neck", "n_shoulder", "f_shoulder"):
        x, y = rj[k]
        d.ellipse((x - 5, y - 5, x + 5, y + 5), outline=(255, 0, 0, 255), width=2)
    for k, (x, y) in (marks or {}).items():
        d.ellipse((x - 6, y - 6, x + 6, y + 6), outline=(0, 255, 255, 255), width=2)
        d.text((x + 8, y - 8), k, fill=(0, 255, 255, 255))
    c = t.crop(box)
    c = c.resize((c.width * zoom, c.height * zoom), Image.NEAREST)
    d = ImageDraw.Draw(c)
    for x in range(box[0] - box[0] % 100 + 100, box[2], 100):
        d.text(((x - box[0]) * zoom + 2, 2), str(x), fill=(255, 255, 0))
    for y in range(box[1] - box[1] % 100 + 100, box[3], 100):
        d.text((2, (y - box[1]) * zoom + 2), str(y), fill=(255, 255, 0))
    c.convert("RGB").save(out)
    print(out)


def load_swaps():
    p = BONES / "swaps.json"
    return json.loads(p.read_text()) if p.exists() else {"version": 1}


def save_swaps(sw):
    order = ["version", "sets", "radius", "swaps", "faces", "cost"]
    sw = {k: sw[k] for k in order if sw.get(k)} | {k: v for k, v in sw.items() if k not in order}
    save_text(BONES / "swaps.json", json.dumps(sw, indent=1) + "\n")


def write_set(name="bare_v2", source="rig", force=False):
    """The recut parts into unity/Bones/hero/<name>/ (only the parts that changed) and swaps.json `sets`."""
    out = BONES / name
    if out.exists() and not force:
        sys.exit(f"{out} exists: a kept set is never overwritten (--force only before it is committed)")
    parts = recut(source, out)
    sw = load_swaps()
    sw.setdefault("sets", {})[name] = {p: {"file": f"{name}/{p}.png", "x": int(x), "y": int(y)}
                                       for p, (_, x, y) in sorted(parts.items())}
    save_swaps(sw)
    print(name, sorted(parts))


TORSO_NOTES = {
    "torso_toward": "Chest open to the viewer, collar opening in the middle, shoulders wide (cut from an edit of the "
                    "drawn strike stance, arms moved out). The base arms hang from its own shoulders; z as the torso.",
    "torso_away": "Chest turned away: the back of the shirt and the shoulder blades, no collar (cut from an edit of "
                  "the drawn wind-up stance, arms moved out). The base head stays 3/4 front, so he looks over his "
                  "shoulder; z as the torso.",
}
FACE_SEED = 7100
COST = {  # gear pictures each new picture asks for (counted, not drawn)
    "torso_toward": {"gear": "body armor", "pictures": 11},  # the body armors that swap the outfit (11 of 12)
    "torso_away": {"gear": "body armor", "pictures": 11},
    "grit": {"gear": "helm", "pictures": 9},  # cut under each helm's mask from the one edit, as shout and hurt
}


def write_swaps(force=False):
    """The kept torsos and the grit face into unity/Bones/hero (new names only) and swaps.json."""
    sw = load_swaps()
    sw.setdefault("swaps", {})
    for name in TORSO_SRC:
        path = BONES / "bare" / f"{name}.png"
        if path.exists() and not force:
            sys.exit(f"{path} exists: a kept picture is never overwritten")
        _, (_, x, y, joints) = torso_puppet(name, save=path)
        sw["swaps"][name] = {"part": "torso", "file": f"bare/{name}.png", "x": int(x), "y": int(y),
                             "joints": {k: [round(v[0], 1), round(v[1], 1)] for k, v in joints.items()},
                             "note": TORSO_NOTES[name]}
    face = BONES / "fight" / "face_grit.png"
    if face.exists() and not force:
        sys.exit(f"{face} exists: a kept picture is never overwritten")
    p, x, y = face_cut(FACE_SEED, out_dir=TRIES / "face")
    save(Image.open(p), face)
    sw["faces"] = {"grit": {"file": "fight/face_grit.png", "x": int(x), "y": int(y)}}
    sw["cost"] = COST
    save_swaps(sw)
    print(json.dumps({k: v for k, v in sw.items() if k != "sets"}, indent=1)[:1500])


def kept_puppet(name, sets=("bare_v2",)):
    """A puppet on bare_v2 with a kept swap from swaps.json."""
    p = Puppet(sets=list(sets))
    p.swap("torso", name)
    return p


def torso_sheet(out):
    """The torso pictures alone in the rest layout beside the drawn stance bodies they came from."""
    box = (540, 320, 1080, 780)
    rj = json.loads((BONES / "rig.json").read_text())
    tiles = []
    base = Puppet(sets=["bare_v2"])
    for label, p in (("base torso (bare_v2)", base), ("torso_toward (new)", kept_puppet("torso_toward")),
                     ("torso_away (new)", kept_puppet("torso_away"))):
        c = Image.new("RGBA", (N, N), GROUND)
        e = p.pics["torso"]
        c.alpha_composite(Image.open(Path(e["file"]) if Path(e["file"]).is_absolute() else BONES / e["file"])
                          .convert("RGBA"), (e["x"], e["y"]))
        d = ImageDraw.Draw(c)
        for k in ("waist", "neck", "n_shoulder", "f_shoulder"):
            x, y = p.J("torso", k)
            d.ellipse((x - 6, y - 6, x + 6, y + 6), outline=(0, 255, 255, 255), width=2)
        tiles.append((label, c.crop(box)))
    for label, m in (("drawn strike stance (source of toward)", "strike"), ("drawn wind-up stance (source of away)", "wind")):
        e = json.loads((BONES / "fight.json").read_text())["moments"][m]["files"]["body"]
        c = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        c.alpha_composite(Image.open(ROOT / "art" / "redo" / "layers" / "parts" / e["file"]).convert("RGBA"),
                          (e["x"], e["y"]))
        al, _ = align(c, m)
        t = Image.new("RGBA", (N, N), GROUND)
        t.alpha_composite(al)
        tiles.append((label, t.crop(box)))
    cw, ch = 340, round(340 * (box[3] - box[1]) / (box[2] - box[0]))
    s = Image.new("RGB", (len(tiles) * (cw + 6) + 6, ch + 84), (28, 26, 32))
    d = ImageDraw.Draw(s)
    d.text((8, 6), "Swap torsos alone, in the rest layout (rings: waist, neck, shoulders where the base parts attach)",
           fill=(240, 232, 214), font=font(16))
    d.text((8, 28), "Right: the drawn stance bodies they were edited from, turned level on the same waist.",
           fill=(190, 184, 170), font=font(13))
    for i, (label, t) in enumerate(tiles):
        x = 6 + i * (cw + 6)
        s.paste(t.resize((cw, ch), Image.LANCZOS).convert("RGB"), (x, 78))
        d.text((x + 2, 56), label, fill=(255, 220, 140) if "new" in label else (220, 214, 200), font=font(13))
    out.parent.mkdir(parents=True, exist_ok=True)
    save(s, out, quality=86, method=6)
    print(out, s.size, out.stat().st_size // 1024, "KB")


def sheets234():
    base = Puppet(sets=["bare_v2"])
    tw, ta = kept_puppet("torso_toward"), kept_puppet("torso_away")
    torso_sheet(SHEETS / "torsos.webp")
    poses_sheet([("bare_v2", base), ("torso_toward", tw), ("torso_away", ta)],
                ["rest", "sword_ready", "sword_wind", "sword_strike", "staff_strike"], SHEETS / "torsos_poses.webp",
                "The swap torsos on the puppet (base arms, legs and head) in the test poses, base torso on top",
                cell=330, box=(380, 230, 1220, 1060))
    closeup_sheet([("bare_v2", base), ("torso_toward", tw), ("torso_away", ta)], SHEETS / "torsos_closeup.webp",
                  "Swap torsos: the joints the base parts hang from, at full size (base torso on top)",
                  specs=[s for s in CLOSE if s[0] in ("n_shoulder", "f_shoulder", "neck", "waist")], view=180)
    # the grip clock: kept data above the proposed dagger grip; the far fist drawn in front (rig farFront)
    old, moved = Puppet(), Puppet()
    for wid, gy in GRIP_Y.items():
        moved.rig["weapons"][wid]["gy"] = gy
    ff = dict(moved.rig["farFront"])

    class Front(Puppet):
        def layers(self, local, root=(0, 0), weapons=None, order=None):
            return super().layers(local, root, weapons, ff)
    front = Front()
    for wid, gy in GRIP_Y.items():
        front.rig["weapons"][wid]["gy"] = gy
    rows = [("sword, kept", old, "n", "iron_sword"), ("staff, kept", old, "n", "oak_staff"),
            ("dagger, kept", old, "n", "rusty_dagger"), (f"dagger, grip {GRIP_Y['rusty_dagger']}", moved, "n", "rusty_dagger"),
            ("far, kept", old, "f", "rusty_dagger"), (f"far {GRIP_Y['rusty_dagger']}, in front", front, "f", "rusty_dagger")]
    clock_sheet(rows, SHEETS / "grip_clock.webp", "The fist on the handle round the clock: kept grip data, then the "
                "proposed dagger grip point (rig.json not changed); last row: far fist in front (farFront)", view=224)
    e = load_swaps()["faces"]["grit"]
    faces_sheet((BONES / e["file"], e["x"], e["y"]), SHEETS / "faces.webp",
                "Faces on the bare head: the new grit beside calm, shout and hurt")


def check():
    """swaps.json loads, every file exists, every swap composes in the rest pose and keeps the figure in one piece."""
    sw = load_swaps()
    bad = []
    for name, st in sw.get("sets", {}).items():
        bad += [f"sets.{name}.{p}" for p, e in st.items() if not (BONES / e["file"]).exists()]
    for name, e in sw.get("swaps", {}).items():
        if not (BONES / e["file"]).exists():
            bad.append(f"swaps.{name}")
    for name, e in sw.get("faces", {}).items():
        if not (BONES / e["file"]).exists():
            bad.append(f"faces.{name}")
    print("files:", "pass" if not bad else f"missing {bad}")
    poses = json.loads((BONES / "poses.json").read_text())
    for name, e in sw.get("swaps", {}).items():
        p = Puppet(sets=list(sw.get("sets", {})))
        p.swap(e["part"], name)
        for pn in ("rest", "sword_wind", "sword_strike"):
            im, _, _ = p.draw_pose(poses[pn])
            a = np.asarray(im.getchannel("A")) > 128
            # one piece: the figure's biggest blob holds nearly all its pixels (4-neighbour flood by rows)
            from collections import deque
            seen = np.zeros_like(a)
            sizes = []
            for y0, x0 in zip(*np.nonzero(a[::7, ::7])):
                y0, x0 = y0 * 7, x0 * 7
                if seen[y0, x0]:
                    continue
                q, n = deque([(y0, x0)]), 0
                seen[y0, x0] = True
                while q:
                    y, x = q.popleft()
                    n += 1
                    for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                        if 0 <= yy < N and 0 <= xx < N and a[yy, xx] and not seen[yy, xx]:
                            seen[yy, xx] = True
                            q.append((yy, xx))
                sizes.append(n)
            share = max(sizes) / a.sum()
            print(f"{name} {pn}: one piece {share:.1%}", "pass" if share > 0.98 else "FAIL")


# ------------------------------------------------------------------------------------------------ the sword hand
# The near hand gripping in line with the forearm (back of the hand to the viewer), for the "extended" relation the
# kept sword moves hold at rest and at every hit. Its pose already exists in the drawn strike stance
# (art/redo/layers/arm_strike_s6110.png: the near fist on a green rod); the edit only takes the rod out of the closed
# fist, so the handle can pass behind the hand. Tries: art/redo/hand/qwen/hand_<kind>_s<seed>.png.
HAND = ROOT / "art" / "redo" / "hand"
HAND_SRC = ROOT / "art" / "redo" / "layers" / "arm_strike_s6110.png"
HAND_BOX = (930, 640, 1250, 960)  # the near forearm's end, the fist and the rod on the 1536 canvas
HAND_UP = 3
HAND_QWEN = {
    "closed": ("Edit <image1>. It shows the right forearm and closed right fist of a cartoon hero, seen from the back of "
               "the hand, holding a bright green rod. Remove the green rod completely. The fist stays tightly closed, "
               "exactly as it is, at the same place and the same size, the fingers curled shut and the thumb over them: "
               "where the rod covered the fist, show the curled fingers and knuckles of the closed fist, drawn the same "
               "way. Keep the forearm, the skin colour, the thin dark outlines, the hard-edged cel shadows and the warm "
               "rim light exactly the same. No rod, no weapon, nothing in the hand. No ground, no shadow, no text."),
}


def hand_qwen(kind, seeds):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import hero_poses as hp
    from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy
    out = HAND / "qwen"
    out.mkdir(parents=True, exist_ok=True)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    src = hp.cleaned(HAND_SRC).crop(HAND_BOX)
    src = src.resize((src.width * HAND_UP, src.height * HAND_UP), Image.LANCZOS)
    ref = comfy.upload("bones_hand_strike.png", hp.on_white(src))
    prompt = f"{RGBA_OPEN} {HAND_QWEN[kind]} {RGBA_CLOSE}"
    for seed in seeds:
        path = out / f"hand_{kind}_s{seed}.png"
        if path.exists():
            continue
        print(path.relative_to(ROOT), flush=True)
        path.write_bytes(comfy.generate(prompt, [ref], None, seed, 30))
        (path.with_suffix(".txt")).write_text(f"{prompt}\nseed {seed}, steps 30\n", encoding="utf-8")


HAND_ROD = ((1211.56, 880.26), 30.03)  # the green rod's axis in the strike stance (centre, deg): the handle's line
HAND_GUARD = 44  # rig px from the guard's lower edge to the sword's grip point (iron_sword.png rows 456 -> 500)
RIG_WRIST_W = 44  # the rig forearm's width 15-60 px above the wrist joint (bare/n_fore.png)


def hand_axis(a, wrist, guess):
    """The forearm's axis through `wrist` (canvas px) on alpha mask `a`: midpoints of cross-sections 20-90 px up the
    arm from the wrist, a line fitted through them. Returns (direction deg, from the elbow toward the hand; width px)."""
    d = np.array([math.cos(math.radians(guess)), math.sin(math.radians(guess))])
    n = np.array([-d[1], d[0]])
    mids, widths = [], []
    for back in range(20, 91, 10):
        c = np.array(wrist) - d * back
        on = [s for s in np.arange(-80, 80, 0.5)
              if a[int(round(c[1] + n[1] * s)), int(round(c[0] + n[0] * s))]]
        if on:
            mids.append(c + n * (min(on) + max(on)) / 2)
            widths.append(max(on) - min(on))
    m = np.array(mids)
    u = np.linalg.svd(m - m.mean(0))[2][0]
    if u @ d < 0:
        u = -u
    return math.degrees(math.atan2(u[1], u[0])), float(np.median(widths)), m.mean(0)


def hand_cut(seed, kind="closed", wrist=(1041, 777), name="n_hand_grip"):
    """Cut the grip hand from an edit: the fist and a round wrist end (a disc about the wrist joint, the forearm's
    half width) so it covers the forearm's end at any turn; scaled so its wrist is as wide as the rig's; written to
    art/redo/hand/cut/<name>_s<seed>.png with its swaps.json entry (placed with its wrist on the rig's n_wrist)."""
    src = Image.open(HAND / "qwen" / f"hand_{kind}_s{seed}.png").convert("RGBA")
    src = src.resize((src.width // HAND_UP, src.height // HAND_UP), Image.LANCZOS)
    canvas = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    canvas.alpha_composite(src, HAND_BOX[:2])
    arr = np.asarray(canvas).copy()
    a = arr[..., 3] > 128
    axis, width, mid = hand_axis(a, wrist, 39.5)
    # the wrist joint: on the axis, at the given point's place along it
    d = np.array([math.cos(math.radians(axis)), math.sin(math.radians(axis))])
    w = mid + d * ((np.array(wrist) - mid) @ d)
    (rx, ry), rang = HAND_ROD
    r = np.array([math.cos(math.radians(rang)), math.sin(math.radians(rang))])
    # the fingers' front edge on the handle's line: walk in from far out until the hand starts
    t = 200.0
    while t > -100 and not a[int(round(ry + r[1] * t)), int(round(rx + r[0] * t))]:
        t -= 0.5
    front = np.array([rx, ry]) + r * t
    scale = RIG_WRIST_W / width
    grip = front - r * (HAND_GUARD / scale)
    yy, xx = np.mgrid[0:N, 0:N]
    rel = np.stack([xx - w[0], yy - w[1]], -1)
    keep = ((rel @ d) > 0) | (np.hypot(rel[..., 0], rel[..., 1]) < width / 2 * 1.08)
    arr[..., 3] = np.where(keep, arr[..., 3], 0)
    cut = Image.fromarray(arr)
    box = cut.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    pic = cut.crop(box)
    pic = pic.resize((max(1, round(pic.width * scale)), max(1, round(pic.height * scale))), Image.LANCZOS)
    # picture coords -> rig canvas: the wrist lands on the rig's n_wrist
    rig_w = np.array(json.loads((BONES / "rig.json").read_text())["joints"]["n_wrist"])
    wpic = (w - np.array(box[:2])) * scale
    corner = rig_w - wpic
    gpic = (grip - np.array(box[:2])) * scale
    out = HAND / "cut"
    out.mkdir(parents=True, exist_ok=True)
    path = out / f"{name}_s{seed}.png"
    pic.save(path)
    rel_angle = (axis - rang + 180) % 360 - 180
    entry = {"part": "n_hand", "file": f"bare/{name}.png", "x": round(float(corner[0]), 1), "y": round(float(corner[1]), 1),
             "joints": {"n_wrist": [round(float(rig_w[0]), 1), round(float(rig_w[1]), 1)]},
             "grip": {"at": [round(float(corner[0] + gpic[0]), 1), round(float(corner[1] + gpic[1]), 1)], "angle": round(rang, 1)},
             "relation": round(rel_angle, 1), "weaponZ": 69,
             "note": ""}
    print(path, pic.size, "forearm axis", round(axis, 1), "width", round(width, 1), "scale", round(scale, 3),
          "wrist", w.round(1), "front", front.round(1), "grip", grip.round(1),
          "wrist->grip rig px", round(float(np.hypot(*(gpic - wpic))), 1))
    print(json.dumps(entry))
    return path, entry


def main():
    args = sys.argv[1:]
    cmd = args.pop(0)
    if cmd == "handcut":  # handcut <seed> [kind]: cut the grip hand from an edit, print its swaps.json entry
        hand_cut(int(args[0]), *(args[1:2] or ["closed"]))
        return
    if cmd == "handqwen":  # handqwen <kind> <seed>..: the sword hand edits (HAND_QWEN)
        hand_qwen(args[0], [int(a) for a in args[1:]])
        return
    if cmd == "writeswaps":  # the kept torsos and grit face (new names only) and swaps.json swaps/faces/cost
        write_swaps(force="--force" in args)
    elif cmd == "sheets234":  # proof sheets of jobs 2-4 into art/tests/sheets/bones/parts
        sheets234()
    elif cmd == "check":
        check()
    elif cmd == "writeset":  # writeset [--force]: bare_v2 from the rig picture
        write_set(force="--force" in args)
    elif cmd == "sheets1":  # the proof sheets of job 1 (joints) into art/tests/sheets/bones/parts
        old, new = Puppet(), Puppet(["bare_v2"])
        arms = [w for w in WIGGLE if w[0][:2] in ("n_", "f_") and w[0][2:] in ("shoulder", "elbow", "wrist")]
        rest = [w for w in WIGGLE if w not in arms]
        for label, rows in (("arms", arms), ("body", rest)):
            g = [("kept", wiggle_cells(old, rows=rows)), ("bare_v2", wiggle_cells(new, rows=rows))]
            wiggle_sheet(g, f"Wiggle test, {label}: kept parts (grey) above bare_v2 (gold), each joint alone",
                         SHEETS / f"wiggle_{label}.webp", cell=180)
        closeup_sheet([("kept", old), ("bare_v2", new)], SHEETS / "joints_closeup.webp",
                      "Joint close-ups at full size: kept parts above bare_v2 (the faults the attacks hit)")
        poses_sheet([("kept", old), ("bare_v2", new)], ["sword_ready", "sword_wind", "sword_strike", "staff_ready",
                                                        "dagger_strike"],
                    SHEETS / "poses_bare_v2.webp", "The test poses (poses.json): kept parts above bare_v2")
    elif cmd == "qwen":  # qwen <name> <seed> [<seed>..] [--ref]
        ref = "--ref" in args
        qwen(args[0], [int(a) for a in args[1:] if not a.startswith("-")], ref)
    elif cmd == "qsheet":  # qsheet <try stem>..: the tries beside the rig picture, lines at the neck and waist
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import hero_poses as hp
        names = ["rig"] + args
        cell = 600
        s = Image.new("RGB", (len(names) * cell, cell), (30, 28, 34))
        for i, n in enumerate(names):
            path = ROOT / "art/redo/layers/rig_s6100.png" if n == "rig" else TRIES / "qwen" / f"{n}.png"
            im = hp.cleaned(path)
            t = Image.new("RGBA", im.size, (214, 205, 182, 255))
            ImageDraw.Draw(t).rectangle((768, 0, 1536, 1536), fill=(58, 70, 52, 255))
            for yline in (425, 495, 690):
                ImageDraw.Draw(t).line((0, yline, 1536, yline), fill=(200, 40, 40, 255), width=2)
            t.alpha_composite(im)
            s.paste(t.convert("RGB").crop((420, 250, 1140, 970)).resize((cell, cell), Image.LANCZOS), (i * cell, 0))
            ImageDraw.Draw(s).text((i * cell + 6, 4), n, fill=(255, 255, 255), font=font(16))
        s.save(TRIES / "_qsheet.png")
        print(TRIES / "_qsheet.png")
    elif cmd == "tryface":  # tryface <seed>
        grit = face_cut(int(args[0]))
        print(grit)
        faces_sheet(grit, TRIES / f"faces_s{args[0]}.webp", f"Faces on the bare head: grit try s{args[0]}")
    elif cmd == "tryhands":
        hands = grip_hands()
        old, new = Puppet(), Puppet(parts=hands)
        rows = []
        for wid in ("iron_sword", "rusty_dagger", "oak_staff"):
            rows += [("kept fist", old, "n", wid), ("grip fist", new, "n", wid)]
        rows += [("kept far fist", old, "f", "rusty_dagger"), ("grip far fist", new, "f", "rusty_dagger")]
        moved = Puppet()
        for wid, gy in GRIP_Y.items():
            moved.rig["weapons"][wid]["gy"] = gy
        rows += [(f"grip at {GRIP_Y['rusty_dagger']}", moved, "n", "rusty_dagger"),
                 (f"far, grip at {GRIP_Y['rusty_dagger']}", moved, "f", "rusty_dagger")]
        clock_sheet(rows, TRIES / "clock_hands.webp", "Near and far fist round the clock: kept fist vs fist from the grip picture")
    elif cmd == "trycut":  # recut into the tries folder and wiggle it beside the kept parts
        src = args[0] if args and not args[0].startswith("-") else "rig"
        parts = recut(src)
        print({k: (str(v[0].name), v[1], v[2]) for k, v in parts.items()})
        sets = [src] if src != "rig" else []
        closeup_sheet([("kept", Puppet(sets)), ("recut", Puppet(sets, parts=parts))], TRIES / f"close_recut_{src}.webp",
                      f"Joint close-ups: kept cut above recut of {src}")
        if "--close" in args:
            return
        groups = [("kept", wiggle_cells(Puppet(sets))), ("recut", wiggle_cells(Puppet(sets, parts=parts)))]
        wiggle_sheet(groups, f"Wiggle: kept cut (grey) above recut of {src} (gold)", TRIES / f"wiggle_recut_{src}.webp")
    elif cmd == "wiggle":
        sets = args_set(args)
        out = args[args.index("--out") + 1] if "--out" in args else "wiggle_before"
        groups = [("bare", wiggle_cells(Puppet()))]
        if sets:
            groups.append(("+".join(sets), wiggle_cells(Puppet(sets))))
        title = "Wiggle test: bare parts" if not sets else f"Wiggle test: bare (grey label) above {'+'.join(sets)} (gold label)"
        wiggle_sheet(groups, title, (TRIES if "--try" in args else SHEETS) / f"{out}.webp")
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
