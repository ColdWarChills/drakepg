"""Our hero on the Dragon Crashers knight's skeleton (2026-11-25, job 1): every knight drawing repainted as today's hero.

python -I -B hero_paint.py [src dir] [out dir]
  src: the knight's Sprite Library drawings, one PNG per label (DcHero.Export(lib asset, dir): art/tests/dc/hero/lib/<Category>__<Label>.png)
  out: the repaints, same names and sizes (art/tests/dc/hero/paint/)

The outline never changes: alpha is the knight's own, untouched (except the tabard strip under the hips, made clear so
the legs behind it show). Inside it, each pixel takes the hero colour of the nearest of the knight's own flat colours
for that drawing (its swatches below), as a hard flat fill with a 3x3 majority vote (`flat`), so no knight shading or
halo lines carry over; only the eyes and mouth are matched softly (`repaint`, SOFT). The hero colours are
one palette sampled from today's hero (char_hero_bare.png), two or three flat tones per material, so every part is
painted from the same colours. A few details are drawn on top: the headband, the V of the shirt, a square buckle.
Colours past the outline are bled from the nearest painted pixel, so the edge never samples the knight's colours.
Nothing here is the knight's art; the output is (the knight's outlines), so it stays under art/tests (not in git).
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/tests/dc/hero/lib"
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "art/tests/dc/hero/paint"

# today's hero, sampled from char_hero_bare.png (light, shadow, deep)
HERO = {
    "skin": [(228, 162, 86), (198, 130, 66), (156, 96, 50)],
    "shirt": [(228, 196, 138), (190, 152, 94), (146, 110, 64)],
    "shirt_hi": [(240, 212, 160)],
    "pants": [(36, 64, 57), (22, 44, 38), (13, 28, 24)],
    "pants_hi": [(48, 82, 73)],
    "boots": [(86, 52, 21), (58, 35, 13), (36, 22, 7)],
    "hair": [(58, 37, 15), (40, 25, 8), (26, 16, 5)],
    "hair_hi": [(98, 63, 30)],
    "band": [(166, 36, 27), (112, 23, 14)],
    "belt": [(68, 41, 12), (44, 26, 6)],
    "brass": [(196, 162, 102), (132, 102, 60)],
    "eye": [(36, 23, 12)],
    "white": [(244, 236, 224)],
    "lips": [(84, 40, 20)],
    "tongue": [(176, 64, 62)],
}


def C(mat, tone=0):
    return HERO[mat][tone]


# per drawing: (knight swatch, hero colour). Far-side parts take the shadow tones.
SPECS = {
    "Arm_R__Arm_R": [((56, 96, 24), C("shirt")), ((48, 16, 32), C("shirt", 1)), ((72, 56, 80), C("skin")),
                     ((80, 88, 104), C("skin")), ((228, 196, 80), C("skin")),
                     ((48, 32, 64), C("skin", 1))],
    "Arm_L__Arm_L": [((40, 80, 32), C("shirt", 1)), ((48, 16, 32), C("shirt", 2)), ((48, 32, 64), C("skin", 1)),
                     ((80, 64, 104), C("skin", 1)), ((32, 16, 48), C("skin", 2))],
    "Hand_R_Bottom__Hand_R_Bottom": [((48, 32, 64), C("skin", 1))],
    "Hand_L_Top__Hand_L_Top": [((112, 104, 128), C("skin", 1)), ((72, 56, 80), C("skin", 2))],
    "Coat_Front__Coat_Front": [((72, 56, 80), C("shirt")), ((152, 152, 128), C("shirt", 1))],
    "Coat_Back__Coat_Back": [((48, 32, 64), C("shirt", 1))],
    "Shoulder_Plate_R__Shoulder_Plate_R": [((120, 136, 144), C("shirt")), ((152, 168, 176), C("shirt_hi"))],
    "Shoulder_Plate_L__Shoulder_Plate_L": [((88, 104, 120), C("shirt", 1)), ((120, 136, 144), C("shirt"))],
    "Neck_Element__Neck_Element": [((56, 80, 136), C("shirt"))],
    "Neck__Neck": [((168, 96, 112), C("skin", 1)), ((224, 128, 136), C("skin"))],
    "Head__Head": [((144, 56, 8), C("hair")), ((248, 176, 160), C("skin")), ((224, 128, 136), C("skin", 1)),
                   ((168, 96, 112), C("skin", 2))],
    "Hair_Front__Hair_Front": [((88, 48, 16), C("hair")), ((184, 96, 32), C("hair_hi"))],
    "Hair_Back__Hair_Back": [((16, 16, 8), C("band", 1))],
    "Eye_L__Open": [((0, 0, 0), C("eye")), ((232, 216, 224), C("white")), ((248, 176, 160), C("skin"))],
    "Eye_R__Open": [((0, 0, 0), C("eye")), ((232, 216, 224), C("white")), ((248, 176, 160), C("skin"))],
    "Eye_L__Closed": [((0, 0, 0), C("eye")), ((248, 176, 160), C("skin"))],
    "Eye_R__Closed": [((0, 0, 0), C("eye")), ((248, 176, 160), C("skin"))],
    "Mouth__Normal": [((64, 40, 0), C("lips")), ((224, 128, 136), C("skin", 1))],
    "Mouth__Open": [((0, 0, 0), (52, 20, 12)), ((224, 64, 96), C("tongue")), ((240, 240, 240), C("white")),
                    ((224, 128, 136), C("skin", 1))],
    "Mouth__Teeth": [((0, 0, 0), (52, 20, 12)), ((240, 240, 240), C("white"))],
}
LEG_NEAR = [((104, 112, 112), C("pants")), ((80, 96, 88), C("pants", 1)), ((152, 152, 128), C("boots")),
            ((56, 80, 136), C("boots", 1))]
SPECS["Leg_R__Leg_R"] = LEG_NEAR
SPECS["Leg_L__Leg_L"] = [(s, tuple(int(v * 0.86) for v in t)) for s, t in LEG_NEAR]


def lum(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def repaint(rgb, swatches, sigma=22.0):
    """Soft match to the swatches; each pixel = blend of the hero colours, times its lightness against the swatch."""
    src = np.array([s for s, _ in swatches], float)
    dst = np.array([t for _, t in swatches], float)
    d2 = ((rgb[..., None, :] - src) ** 2).sum(-1)
    d2 -= d2.min(-1, keepdims=True)
    w = np.exp(-d2 / (2 * sigma * sigma))
    w /= w.sum(-1, keepdims=True)
    base = (w[..., None] * dst).sum(-2)
    ref = (w * lum(src)).sum(-1)
    ratio = np.clip((lum(rgb) + 6) / (ref + 6), 0.72, 1.22)
    return np.clip(base * ratio[..., None], 0, 255), w


def flat(rgb, alpha, swatches, majority=True):
    """Flat fills: each interior pixel (alpha > 200) takes its nearest swatch's hero colour, a 3x3 majority vote
    removes the one-pixel halos between two knight colours, and the soft edge takes the nearest interior label."""
    src = np.array([s for s, _ in swatches], float)
    dst = np.array([t for _, t in swatches], float)
    lab = ((rgb[..., None, :] - src) ** 2).sum(-1).argmin(-1)
    inner = alpha > 200
    if majority:
        k = len(swatches)
        votes = np.stack([ndimage.uniform_filter((lab == i).astype(float), 3) for i in range(k)])
        lab = np.where(inner, votes.argmax(0), lab)
    if inner.any():
        _, (iy, ix) = ndimage.distance_transform_edt(~inner, return_indices=True)
        lab = lab[iy, ix]
    return dst[lab], lab


def bleed(rgb, alpha):
    """Every pixel outside the outline takes the colour of the nearest painted one."""
    inside = alpha > 8
    if not inside.any():
        return rgb
    _, (iy, ix) = ndimage.distance_transform_edt(~inside, return_indices=True)
    return rgb[iy, ix]


def near_swatch(rgb, colour, tol=30):
    return np.sqrt(((rgb - np.array(colour, float)) ** 2).sum(-1)) < tol


def above_mask(h, w, b0):
    return (np.arange(h)[:, None] < b0).repeat(w, 1)


def torso(rgb, alpha):
    """Hood -> shirt with the chest showing in its V, grey shirt -> cream with its folds as shadow, belt with a square
    buckle, hips -> trousers; everything that hangs below the trouser hips (the tabard strip) made clear."""
    h, w = alpha.shape
    op = alpha > 200
    ys = np.arange(h)[:, None].repeat(w, 1)
    purple = near_swatch(rgb, (72, 56, 80), 22) & op
    rows = purple.sum(1)
    belt_rows = np.where((rows > w * 0.25) & (np.arange(h) > h * 0.45))[0]
    b0, b1 = int(belt_rows.min()), int(belt_rows.max())
    above, below = ys < b0, ys > b1
    shirt = [((56, 80, 136), C("shirt")), ((56, 72, 120), C("shirt")), ((120, 136, 144), C("shirt")),
             ((56, 64, 96), C("shirt", 1)), ((72, 56, 80), C("shirt", 1)), ((16, 56, 88), C("skin", 1)),
             ((152, 152, 128), C("shirt")), ((224, 176, 56), C("shirt"))]
    up, ulab = flat(rgb, alpha, shirt)
    # the knight's thin chest stripes read as muscles on a shirt: only big shadow shapes stay, small ones go light
    shade = (ulab == 3) | (ulab == 4)
    comp, n = ndimage.label(shade & above_mask(h, w, b0))
    sizes = ndimage.sum(np.ones_like(comp), comp, range(1, n + 1))
    small = np.isin(comp, 1 + np.where(sizes < 0.04 * (alpha > 200).sum())[0]) & (comp > 0)
    up[small] = C("shirt")
    pants = [((120, 136, 144), C("pants")), ((56, 80, 136), C("pants", 1)), ((72, 56, 80), C("pants", 1)),
             ((224, 176, 56), C("pants")), ((104, 112, 112), C("pants", 1))]
    lo, plab = flat(rgb, alpha, pants)
    beltp = [((72, 56, 80), C("belt")), ((224, 176, 56), C("belt")), ((120, 136, 144), C("belt", 1)),
             ((56, 80, 136), C("belt", 1))]
    bt, _ = flat(rgb, alpha, beltp)
    out = np.where(above[..., None], up, np.where(below[..., None], lo, bt))
    # below the belt: clear everything under each column's lowest trouser-grey pixel (the tabard strip and its edge)
    hips = (plab == 0) & below & op
    new_alpha = alpha.copy()
    hb = np.full(w, -1)
    for x in range(w):
        yy = np.where(hips[:, x])[0]
        if len(yy):
            hb[x] = yy.max()
    have = hb >= 0
    if have.any():
        xs = np.arange(w)
        hb = np.interp(xs, xs[have], hb[have])
        hb = ndimage.maximum_filter1d(hb, 5)
        cut = ys > hb[None, :] + 1
        new_alpha[cut & below] = 0
    # a square buckle over the knight's round one
    gold = near_swatch(rgb, (224, 176, 56), 60) & op
    gy, gx = np.where(gold)
    if len(gy):
        cy, cx = (gy.min() + gy.max()) / 2, (gx.min() + gx.max()) / 2
        sz = (b1 - b0) * 0.62
        img = Image.fromarray(out.astype(np.uint8))
        d = ImageDraw.Draw(img)
        x0, y0, x1, y1 = cx - sz * 0.8, cy - sz, cx + sz * 0.8, cy + sz
        d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=C("belt", 1))
        d.rectangle([x0, y0, x1, y1], fill=C("brass"))
        d.rectangle([x0 + 4, y0 + 4, x1 - 4, y1 - 4], fill=C("belt"))
        d.line([(cx, y0 + 4), (cx, y1 - 4)], fill=C("brass", 1), width=3)
        out = np.asarray(img).astype(float)
    return out, new_alpha, dict(belt=(b0, b1), cleared=int(((alpha > 0) & (new_alpha == 0)).sum()))


def legs(rgb, alpha, far):
    """Trousers down to where the leg wraps start; from there down, boot: the wraps are its shaft, the shoe its foot,
    a darker cuff at the top. The light band just above the wraps is the trousers' blousing."""
    k = 0.86 if far else 1.0
    def K(c): return tuple(v * k for v in c)
    op = alpha > 200
    wraps = near_swatch(rgb, (152, 152, 128), 34) & op
    wy = np.where(wraps.any(1))[0]
    top = int(wy.min()) if len(wy) else alpha.shape[0]
    ys = np.arange(alpha.shape[0])[:, None]
    trousers = [((104, 112, 112), K(C("pants"))), ((80, 96, 88), K(C("pants", 1))), ((120, 136, 144), K(C("pants_hi"))),
                ((152, 152, 128), K(C("pants"))), ((56, 80, 136), K(C("pants")))]
    boot = [((152, 152, 128), K(C("boots"))), ((120, 136, 144), K(C("boots", 1))), ((104, 112, 112), K(C("boots", 1))),
            ((80, 96, 88), K(C("boots", 1))), ((56, 80, 136), K(C("boots", 1)))]
    a, _ = flat(rgb, alpha, trousers)
    b, _ = flat(rgb, alpha, boot)
    out = np.where((ys < top)[..., None], a, b)
    out[np.broadcast_to((ys >= top) & (ys < top + 7), alpha.shape)] = K(C("boots", 2))
    return out, dict(boot_top=top)


def head(out, alpha):
    """The headband across the brow and round the back of the head, over hair and skin alike."""
    h, w = alpha.shape
    img = Image.fromarray(out.astype(np.uint8))
    d = ImageDraw.Draw(img)
    top, bot = [], []
    for i in range(21):
        t = i / 20
        yc = h * (0.47 - 0.07 * t) + h * 0.03 * np.sin(np.pi * t)
        top.append((w * t, yc - h * 0.06))
        bot.append((w * t, yc + h * 0.06))
    d.polygon(top + bot[::-1], fill=C("band"))
    d.line(bot, fill=C("band", 1), width=3)
    return np.asarray(img).astype(float)


def hair_back(alpha):
    """The wedge behind the head becomes the headband's tail: red, with a darker lower fold."""
    h, w = alpha.shape
    ys = np.arange(h)[:, None].repeat(w, 1)
    out = np.zeros((h, w, 3))
    out[ys < h * 0.45] = C("band")
    out[ys >= h * 0.45] = C("band", 1)
    return out


SOFT = ("Eye_", "Mouth_")   # small drawings: soft blends keep their tiny shapes


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    report = []
    for f in sorted(SRC.glob("*.png")):
        name = f.stem
        a = np.asarray(Image.open(f).convert("RGBA")).astype(float)
        rgb, alpha = a[..., :3], a[..., 3].astype(np.uint8)
        info = {}
        if name == "Torso__Torso":
            out, alpha, info = torso(rgb, alpha)
        elif name.startswith("Leg_"):
            out, info = legs(rgb, alpha, far=name.startswith("Leg_L"))
        elif name == "Hair_Back__Hair_Back":
            out = hair_back(alpha)
        else:
            spec = SPECS.get(name)
            if spec is None:
                report.append(f"{name}: no spec, left as the knight's")
                continue
            out = repaint(rgb, spec)[0] if name.startswith(SOFT) else flat(rgb, alpha, spec)[0]
            if name == "Head__Head":
                out = head(out, alpha)
        out = bleed(np.clip(out, 0, 255), alpha)
        res = np.dstack([out.astype(np.uint8), alpha])
        Image.fromarray(res, "RGBA").save(OUT / f.name)
        report.append(f"{name}: {a.shape[1]}x{a.shape[0]} {info}")
    print("\n".join(report))


if __name__ == "__main__":
    main()
