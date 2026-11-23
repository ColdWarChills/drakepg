"""Auto-measure the near (picture-left) arm of cutting-pose tries, and the old hero for comparison.
<venv python> -I -B measure.py <try> ...   -> prints a table, writes art/redo/hero_joints/measure.json and an overlay
(_measure_overlay.png) with the axis, armpit, shoulder pivot and cuff marked.

The arm: figure pixels left of the torso (left of the belt's left end), its long axis by PCA of the sleeve pixels.
Along the axis from the fist inward, the cross-section width is steady on the sleeve and jumps at the armpit; the
shoulder pivot is put half a sleeve width past the armpit. The cuff = the last sleeve (cream) pixel on the axis before
the skin of the fist. U = top of the hair to the top of the belt (does not depend on the legs).
Old hero: upper arm 0.145 H + forearm 0.136 H = 0.281 H to the wrist (H = 1034, hero2d HOWTO), forearm width read
here on char_hero_bare's hanging near forearm."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

from jcommon import PURPLE, RAW, REDO, TESTS, fit, load


def masks(rgb, a=None):
    rgb = rgb.astype(np.float32)
    if a is None:
        fig = np.abs(rgb - np.array(PURPLE)).max(-1) > 70
    else:
        fig = a > 0.5
    n, lab, st, _ = cv2.connectedComponentsWithStats(fig.astype(np.uint8), 8)
    fig = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    belt = fig & (R < 100) & (R > 45) & (G < 70) & (G > 25) & (B < 45) & (R - B > 30)
    cream = fig & (R > 170) & (G > 150) & (B > 95) & (R - B < 110) & (R - B > 15)
    skin = fig & (R > 160) & (G > 95) & (G < 175) & (B < 110) & (R - B > 90)
    return fig, belt, cream, skin


def belt_box(belt, fig):
    rows = belt.sum(1).astype(float)
    yy = np.nonzero(fig.any(1))[0]
    lo, hi = yy.min() + int(0.3 * np.ptp(yy)), yy.min() + int(0.62 * np.ptp(yy))
    rows[:lo] = 0
    rows[hi:] = 0
    ys = np.nonzero(rows > 0.5 * rows.max())[0]
    # the belt is the widest brown band in the middle of the figure
    ytop = int(ys.min())
    band = belt[ys.min():ys.max() + 1]
    xs = np.nonzero(band.any(0))[0]
    return ytop, int(ys.max()), int(xs.min()), int(xs.max())


def section_width(fig, c, n, maxr=150):
    """Contiguous run of figure pixels through c along the normal n."""
    h, w = fig.shape
    out = 0
    for sgn in (1, -1):
        for r in range(0, maxr):
            x, y = int(round(c[0] + sgn * n[0] * r)), int(round(c[1] + sgn * n[1] * r))
            if not (0 <= x < w and 0 <= y < h) or not fig[y, x]:
                break
            out += 1
    return out - 1


def measure(name, rgb, a=None, draw=None):
    fig, belt, cream, skin = masks(rgb, a)
    ys, xs = np.nonzero(fig)
    top, bot = ys.min(), ys.max()
    H = bot - top + 1
    by0, by1, bx0, bx1 = belt_box(belt, fig)
    U = by0 - top
    # near arm candidates: sleeve + skin left of the belt's left end, above the belt bottom + 0.15 H
    reg = np.zeros_like(fig)
    reg[: by1 + int(0.15 * H), : bx0 - 4] = True
    arm = fig & reg
    n, lab, st, _ = cv2.connectedComponentsWithStats(arm.astype(np.uint8), 8)
    arm = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    sl = arm & cream
    py, px = np.nonzero(sl)
    P = np.stack([px, py], 1).astype(float)
    m = P.mean(0)
    _, _, vt = np.linalg.svd(P - m, full_matrices=False)
    d = vt[0]
    # point d from the shoulder toward the fist: the fist (skin) is further out (left/down)
    fy, fx = np.nonzero(arm & skin)
    F = np.array([fx.mean(), fy.mean()]) if len(fx) else m + d
    if np.dot(F - m, d) < 0:
        d = -d
    nrm = np.array([-d[1], d[0]])
    # walk the axis from the fist inward: cuff = last cream before skin; widths on the sleeve
    tf = np.dot(F - m, d)
    cuff = None
    for t in np.arange(tf, -400, -1.0):
        p = m + d * t
        x, y = int(round(p[0])), int(round(p[1]))
        if cream[y, x] and cuff is None:
            cuff = t
            break
    widths = []
    armpit = None
    for t in np.arange(cuff - 10, cuff - 600, -2.0):
        p = m + d * t
        w = section_width(fig, p, nrm)
        widths.append((t, w))
    ws = np.array([w for _, w in widths])
    steady = float(np.median(ws[: max(5, len(ws) // 4)]))
    for t, w in widths:
        if w > 1.6 * steady:
            armpit = t
            break
    shoulder_t = armpit - 0.5 * steady
    L = cuff - shoulder_t
    res = {"H": int(H), "U_hair_to_belt": int(U), "arm_len": round(float(L), 1), "sleeve_w": steady,
           "arm_over_H": round(float(L / H), 3), "arm_over_U": round(float(L / U), 3), "w_over_U": round(steady / U, 3),
           "axis_deg": round(float(np.degrees(np.arctan2(d[1], d[0]))), 1),
           "shoulder": [round(float(v), 1) for v in (m + d * shoulder_t)],
           "cuff": [round(float(v), 1) for v in (m + d * cuff)], "armpit": [round(float(v), 1) for v in (m + d * armpit)],
           "belt_top": int(by0), "fist_c": [round(float(v), 1) for v in F]}
    if draw is not None:
        dr = ImageDraw.Draw(draw)
        s0, c0 = m + d * shoulder_t, m + d * cuff
        dr.line([tuple(s0), tuple(c0)], fill=(0, 255, 0), width=3)
        for p, col in ((s0, (255, 0, 0)), (c0, (0, 0, 255)), (m + d * armpit, (255, 255, 0))):
            dr.ellipse([p[0] - 6, p[1] - 6, p[0] + 6, p[1] + 6], outline=col, width=3)
        dr.line([(0, by0), (draw.width, by0)], fill=(255, 128, 0), width=1)
        dr.line([(0, top), (draw.width, top)], fill=(255, 128, 0), width=1)
    return res


def old_hero():
    """The old hero: U and the near forearm's width (skin, hanging at the picture left)."""
    im = np.asarray(load(RAW / "char_hero_bare.png")).astype(np.float32)
    a = im[..., 3] / 255
    fig, belt, cream, skin = masks(im[..., :3], a)
    ys, xs = np.nonzero(fig)
    top = ys.min()
    by0, by1, bx0, bx1 = belt_box(belt, fig)
    # near forearm: skin left of the belt, between the sleeve end and the fist
    reg = np.zeros_like(fig)
    reg[:by1 + 120, :bx0 + 10] = True
    sk = skin & reg
    n, lab, st, _ = cv2.connectedComponentsWithStats(sk.astype(np.uint8), 8)
    sk = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    py, px = np.nonzero(sk)
    P = np.stack([px, py], 1).astype(float)
    mm = P.mean(0)
    _, _, vt = np.linalg.svd(P - mm, full_matrices=False)
    d = vt[0]
    nrm = np.array([-d[1], d[0]])
    proj = (P - mm) @ d
    ws = []
    for t in np.linspace(proj.min() + 0.25 * np.ptp(proj), proj.min() + 0.55 * np.ptp(proj), 9):
        ws.append(section_width(fig, mm + d * t, nrm))
    return {"H": int(ys.max() - top + 1), "U_hair_to_belt": int(by0 - top), "forearm_w_skin": float(np.median(ws)),
            "arm_len_0.281H": round(0.281 * (ys.max() - top + 1), 1)}


if __name__ == "__main__":
    old = old_hero()
    old["arm_over_U"] = round(old["arm_len_0.281H"] / old["U_hair_to_belt"], 3)
    old["w_over_U"] = round(old["forearm_w_skin"] / old["U_hair_to_belt"], 3)
    out = {"old": old}
    print("old", old)
    sheets = []
    for n in sys.argv[1:]:
        im = Image.open(REDO / f"{n}.png").convert("RGB")
        dr = im.copy()
        r = measure(n, np.asarray(im), draw=dr)
        r["arm_vs_old"] = round(r["arm_over_U"] / old["arm_over_U"], 2)
        r["w_vs_old"] = round(r["w_over_U"] / old["w_over_U"], 2)
        out[n] = r
        print(n, r)
        sheets.append(dr.crop((150, 100, 1386, 1500)))
    prev = json.loads((REDO / "measure.json").read_text()) if (REDO / "measure.json").exists() else {}
    prev.update(out)
    (REDO / "measure.json").write_text(json.dumps(prev, indent=1))
    if sheets:
        W = sum(s.width for s in sheets)
        sh = Image.new("RGB", (W, sheets[0].height), (255, 255, 255))
        x = 0
        for s in sheets:
            sh.paste(s, (x, 0))
            x += s.width
        fit(sh).save(TESTS / "_measure_overlay.png")
