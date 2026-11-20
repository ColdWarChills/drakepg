#!/usr/bin/env python3
"""Arm pieces made for bending (the designer, 2026-11-20: "redraw the arm pieces for bending").

Today's near arm was cut out of one painting: the sleeve piece keeps a ball of forearm skin in its cuff (the recut's
"keep" circle), and the forearm piece keeps the cuff on top and a stub of fist at the wrist. Bend the elbow and the
ball stays put while the real forearm comes out beside it. Here the pieces are rebuilt from their own pixels, for the
same joints (rig.json and swaps.json are not touched), the way cut-out rigs do elbows: the forearm turns under a
closed sleeve end, so nothing at the joint changes shape as it bends:

- sleeve (n_upper, from bare_v2): the cuff's opening (the forearm skin in it and the dark inside round it) filled with
  the cuff's own lit cloth, carried down from just above the opening and a little darker towards the end; the end is a
  gentle curve across the arm just past where the cuff's sides end, with a darker rolled edge.
- forearm (n_fore, from bare): bare skin only; its top a round end on the elbow pivot (always under the closed cuff),
  joined to the forearm by straight sides, its other end round on the wrist pivot (under the fist); both painted by
  carrying the forearm's own cross-section along it.

Writes unity/Bones/hero/bare_v3/<part>.png and bare_v3/parts.json ({part: [file, x, y]}), never over a kept picture
(--force replaces this script's own output). The clip: scripts/arm_pieces_clip.py.

  python scripts/arm_pieces.py make
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
BONES = ROOT / "unity" / "Bones" / "hero"
OUT = BONES / "bare_v3"
PAD = 40
SS = 4                                   # supersampling for painted outlines
RIG = json.loads((BONES / "rig.json").read_text())
SW = json.loads((BONES / "swaps.json").read_text())
J = {k: tuple(v) for k, v in RIG["joints"].items()}


def load(e):
    """A part picture padded by PAD on every side, as float RGBA, and its new top-left in the rig canvas."""
    im = np.asarray(Image.open(BONES / e["file"]).convert("RGBA")).astype(np.float32)
    a = np.zeros((im.shape[0] + 2 * PAD, im.shape[1] + 2 * PAD, 4), np.float32)
    a[PAD:-PAD, PAD:-PAD] = im
    return a, (e["x"] - PAD, e["y"] - PAD)


def unit(p0, p1):
    d = np.array([p1[0] - p0[0], p1[1] - p0[1]], np.float32)
    return d / float(np.hypot(*d))


def frame(a, origin, p0, p1):
    """Along (from p0 towards p1, px) and across (signed, px) for every pixel; p0, p1 in rig canvas coordinates."""
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(np.float32)
    x0, y0 = p0[0] - origin[0], p0[1] - origin[1]
    d = unit(p0, p1)
    dx, dy = xx - x0, yy - y0
    return dx * d[0] + dy * d[1], -dx * d[1] + dy * d[0], float(np.hypot(p1[0] - p0[0], p1[1] - p0[1]))


def to_px(origin, p0, d, al, ac):
    """Picture pixel of an (along, across) point."""
    return (p0[0] - origin[0] + al * d[0] - ac * d[1], p0[1] - origin[1] + al * d[1] + ac * d[0])


def skin_of(a):
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    return (al > 40) & (r - g > 40) & (r - b > 90)


def profile(a, keep, al, ac, lo, hi):
    """The cross-section between along lo..hi: per whole px across, the median colour of the kept pixels there."""
    band = keep & (al >= lo) & (al < hi)
    cols = np.round(ac[band]).astype(int)
    rgb = a[..., :3][band]
    prof = {}
    for c in np.unique(cols):
        sel = cols == c
        if sel.sum() >= 3:
            prof[int(c)] = np.median(rgb[sel], 0)
    lo_c, hi_c = min(prof), max(prof)
    for c in range(lo_c, hi_c + 1):          # no holes across
        if c not in prof:
            prof[c] = prof[min(prof, key=lambda k: abs(k - c))]
    return prof, lo_c, hi_c


def lut_of(prof, lo_c, hi_c):
    return np.array([prof[c] for c in range(lo_c, hi_c + 1)], np.float32)


def coverage(shape, polys):
    """Antialiased coverage (0..1) of polygons given in picture px."""
    m = Image.new("L", (shape[1] * SS, shape[0] * SS), 0)
    dr = ImageDraw.Draw(m)
    for poly in polys:
        dr.polygon([(x * SS, y * SS) for x, y in poly], fill=255)
    m = m.resize((shape[1], shape[0]), Image.BOX)
    return np.asarray(m).astype(np.float32) / 255


def hull(points):
    """Convex hull (monotone chain) of 2D points."""
    pts = sorted(set(points))

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def round_end(a, org, p0, d, prof, lo_c, hi_c, al, ac, a0, a1, centre, radius):
    """Paint the forearm between along a0..a1 as the hull of its cross-section and a disc on `centre` (along)."""
    pts = [(t, c) for t in (a0, a1) for c in (lo_c - 0.5, hi_c + 0.5)]
    pts += [(centre + radius * np.cos(th), radius * np.sin(th)) for th in np.linspace(0, 2 * np.pi, 96, endpoint=False)]
    poly = [to_px(org, p0, d, t, c) for t, c in hull([(round(t, 3), round(c, 3)) for t, c in pts])]
    cov = coverage(a.shape, [poly])
    lut = lut_of(prof, lo_c, hi_c)
    acr = np.clip(np.round(ac), lo_c, hi_c).astype(int) - lo_c
    m = cov > 0
    near = m & (((al - a0) * (a1 - al) >= 0) | (np.hypot(al - centre, ac) <= radius + 1))
    a[..., :3][near] = lut[acr[near]]
    a[..., 3][near] = np.maximum(a[..., 3][near], cov[near] * 255)


def grow(m, n):
    """The mask grown by n px (square steps)."""
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]
        g[:-1] |= m[1:]
        g[:, 1:] |= m[:, :-1]
        g[:, :-1] |= m[:, 1:]
        m = g
    return m


def make_upper():
    e = SW["sets"]["bare_v2"]["n_upper"]
    a, org = load(e)
    al, ac, L = frame(a, org, J["n_shoulder"], J["n_elbow"])
    skin = skin_of(a) & (al > L - 60)
    lum = a[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32)
    cloth = (a[..., 3] > 200) & ~skin
    dark = cloth & (lum < 0.72 * np.median(lum[cloth & (al > L - 40)]))
    hole = skin | (dark & grow(skin, 10))                     # the opening and the dark inside of the cuff round it
    hole |= grow(hole, 3) & cloth & (lum < 0.85 * np.median(lum[cloth & (al > L - 40)]))   # and its ragged rim
    out = a.copy()
    # closed: the opening filled with the cuff's own front, the lit cloth just above the opening carried down each
    # column (smoothed across so no streaks), turning a little darker towards the roll's lower edge
    side = cloth & ~hole
    top = float(al[hole].min())
    front = side & (al >= top - 14) & (al < top - 2) & (lum > np.median(lum[cloth & (al > L - 40)]) * 0.9)
    prof, lo_c, hi_c = profile(a, front, al, ac, top - 14, top - 2)
    lut = lut_of(prof, lo_c, hi_c)
    k = 9
    padded = np.pad(lut, ((k // 2, k // 2), (0, 0)), mode="edge")
    lut = np.stack([np.convolve(padded[:, i], np.ones(k) / k, mode="valid") for i in range(3)], 1)
    acr = np.clip(np.round(ac), lo_c, hi_c).astype(int) - lo_c
    depth = np.clip((al - top) / 44, 0, 1)
    fill = lut[acr] * (1 - 0.18 * depth)[..., None]
    out[..., :3][hole] = fill[hole]
    out[..., 3][hole] = 255
    seam = grow(hole, 2) & ~hole & cloth                      # a soft join to the drawn cloth round it
    out[..., :3][seam] = 0.5 * out[..., :3][seam] + 0.5 * fill[seam]
    # the end: a gentle curve across the arm just past where the cuff's sides end, with a darker rolled edge
    sides = ac[side & (al > L + 30)]
    half = float(max(-sides.min(), sides.max()))
    centre_end = float(al[side & (np.abs(ac) < 10)].max()) if (side & (np.abs(ac) < 10) & (al > L)).any() else L + 42
    centre_end = max(centre_end, L + 40)
    end = centre_end - 6 * (ac / half) ** 2
    past = al - end
    out[..., 3][(al > L) & (past > 0)] = 0
    edge = (al > L) & (past <= 0) & (past > -3) & (out[..., 3] > 0)
    out[..., :3][edge] *= 0.8
    soft = (al > L) & (past <= 0) & (past > -1.2)
    out[..., 3][soft] = np.minimum(out[..., 3][soft], np.clip(-past[soft] + 0.3, 0, 1) * 255)
    return out, org


def make_fore():
    e = RIG["sets"]["bare"]["n_fore"]
    a, org = load(e)
    al, ac, L = frame(a, org, J["n_elbow"], J["n_wrist"])
    d = unit(J["n_elbow"], J["n_wrist"])
    skin = skin_of(a)
    prof_top, lo_t, hi_t = profile(a, skin, al, ac, 62, 80)
    prof_bot, lo_b, hi_b = profile(a, skin, al, ac, L - 58, L - 42)
    out = a.copy()
    out[..., 3][(al < 40) | (al > L - 42)] = 0          # the cuff on top and the fist stub at the wrist go
    out[..., 3][~skin & (al < 70)] = 0
    radius = max(-lo_t, hi_t, (hi_t - lo_t) / 2)         # wide enough for the forearm's top, centred on the pivot
    out[..., 3][al < 62] = 0
    round_end(out, org, J["n_elbow"], d, prof_top, lo_t, hi_t, al, ac, 0, 68, 0, radius)
    out[..., 3][al > L - 46] = 0
    round_end(out, org, J["n_elbow"], d, prof_bot, lo_b, hi_b, al, ac, L, L - 52, L, (hi_b - lo_b) / 2)
    return out, org


def save(part, arr, org, table):
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{part}.png"
    if path.exists() and "--force" not in sys.argv:
        raise SystemExit(f"{path} exists (a kept picture is never overwritten; use a new name)")
    im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    bb = im.getbbox()
    im = im.crop(bb)
    im.save(path)
    table[part] = [f"bare_v3/{part}.png", org[0] + bb[0], org[1] + bb[1]]
    print(part, path.relative_to(ROOT), table[part])


def main():
    if sys.argv[1:2] == ["make"]:
        table = {}
        up, org = make_upper()
        save("n_upper", up, org, table)
        fore, org = make_fore()
        save("n_fore", fore, org, table)
        (OUT / "parts.json").write_text(json.dumps(table, indent=1))
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
