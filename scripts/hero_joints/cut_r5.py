"""Round 5 cut: the strike drawings (Qwen edit r5_strike_s501 of today's strike drawing, dressed as hero 4) cut into
swap pieces for hero 4's rig. <venv python> -I -B cut_r5.py
Output: art/redo/hero_joints/parts_r5/ + parts_r5.json (same 1536 canvas and scale as parts_r2/parts_r4).
- The drawing is moved up DY px so its soles sit on s202's feet line (1283); no scale (head 1.02, belt 0.94 by r5_gate).
- The near (sword) arm is cut away; the body under it comes from r5_noarm_s511 (the same drawing with that arm removed,
  aligned by phase correlation), only inside the dilated arm mask.
- hips_lunge: belt + legs + boots, cut on the belt's own top edge (a fitted line). waist = the belt's centre (as round 2).
- torso_strike: the drawn vest + collar above the belt line, carried on under the belt (filled vest colour).
- armhole_strike: the drawn near sleeve's round top, feathered across the upper arm at U_CUT and tapered there onto the
  painted sleeve's width, drawn OVER the painted sleeve (as round 4).
- head_shout: the drawn head (shout, brows down) with its neck run on under the collar.
- tails_strike: the headband tails left of the knot (drawn in front of the far arm: strike layer order differs).
- far_arm_back: the raised far arm, with the part under the tails filled and a round root behind the torso.
Pivots are read on grid crops (gridlook.py r5/_grid_*.png) of r5_strike_s501 BEFORE the DY move; json values are after it."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image

from jcommon import REDO, ROOT

sys.path.insert(0, str(ROOT / "scripts" / "hero2d"))
import cutlib  # noqa: E402

_argv, sys.argv = sys.argv, sys.argv[:1]
import cut_r2  # noqa: E402  (figure(), shift_between(); its main() is not run)
import rig_r2  # noqa: E402
sys.argv = _argv

KEY, NOARM = "r5_strike_s501", "r5_noarm_s511"
OUT = REDO / "parts_r5"
OUT.mkdir(exist_ok=True)
P2 = json.loads((REDO / "parts_r2.json").read_text())
FEET = 1283.0
# ---- read on r5/_grid_s501*.png (drawing px, before DY)
ARM_SEED = (850, 870)            # a point on the near sleeve
FIST_SEED = (925, 1100)          # a point on the near fist
ARM_TOP = (793.4, 685.7)         # the top of the near sleeve's round end, on its axis
ARM_WRIST = (893.4, 1042.7)      # the drawn wrist (cuff's front edge, middle)
S_IN = 40.0                      # S: this far down the axis from ARM_TOP (the painted cap, r 33, sits inside the drawn top)
U_T0, U_CUT, CUT_SOFT = 45.0, 95.0, 6.0   # from ARM_TOP along the axis: taper starts, the drawn root ends (feathered)
ARM_DIL = 14                     # px: the arm mask grown over its outline before the no-arm body replaces it
NECK = (835.0, 642.0)            # the head's pivot: the neck's middle at the collar
KNOT = (806.0, 505.0)            # the headband's knot (the tails turn here)
TAIL_X = 800                     # tails = red left of this
FAR_SHOULDER = (752.0, 590.0)    # behind the vest, where the far sleeve goes in
HEAD_POLY = [(775, 395), (1045, 395), (1045, 700), (905, 700), (875, 676), (845, 668), (800, 648), (786, 596),
             (772, 545), (768, 470)]
NECK_POLY = [(800, 598), (872, 598), (880, 700), (796, 700)]   # the neck runs on under the collar (head piece)
BELT_SQ, BELT_EASE = 0.92, 40.0  # the belt band squeezed under its top line, eased into the trousers
HEAD_SCALE = 1.0                 # the head and tails about the neck pivot (0.97 tried: the uncovered rim
                                 # had to be filled on the torso and left stair-stepped teal at 3x; face is 1.03)
COLLAR_X, COLLAR_Y = 880, 572    # cream left of / below these (inside the head polygon) = the collar (torso)


def shifted(px, dx, dy):
    M = np.float32([[1, 0, dx], [0, 1, dy]])
    return cv2.warpAffine(px, M, (px.shape[1], px.shape[0]), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)


def comp_at(mask, pt, k_open=3):
    m = cv2.morphologyEx(mask.astype(np.uint8), cv2.MORPH_OPEN, np.ones((k_open, k_open), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    i = lab[int(pt[1]), int(pt[0])]
    if i == 0:
        raise SystemExit(f"seed {pt} is not on the mask")
    return lab == i


def classes(px):
    R, G, B, A = [px[..., i] for i in range(4)]
    f = A > 128
    return dict(
        cream=f & (R > 170) & (G > 140) & (B > 95) & (R - B < 110),
        skin=f & (R > 150) & (G > 90) & (G < 190) & (B < 125) & (R - B > 80),
        vest=f & (G > R + 8) & (G < 110) & (B < 110) & (G > 25),
        red=f & (R - G > 55) & (R - B > 30) & (G < 70) & (R < 200),
        brown=f & (R < 120) & (G < 85) & (B < 60) & (R - B > 15) & (R > G),
        fig=f,
    )


def belt_line(c):
    """The belt (brown component around the buckle, middle of the figure) and a line fitted to its top edge."""
    H, W = c["fig"].shape
    yy, xx = np.mgrid[0:H, 0:W]
    m = c["brown"] & (yy > 760) & (yy < 900) & (xx > 600) & (xx < 880)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
    n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    b = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    ys, xs = np.nonzero(b)
    cols = np.arange(xs.min() + 6, xs.max() - 5)
    tops = np.array([ys[xs == x].min() for x in cols])
    bots = np.array([ys[xs == x].max() for x in cols])
    a, k = np.polyfit(cols, tops, 1)
    a2, k2 = np.polyfit(cols, bots, 1)
    pts = np.column_stack([xs, ys]).astype(np.float32)
    (cx, cy), (w, h), ang = cv2.minAreaRect(pts)
    return b, dict(slope=float(a), icpt=float(k), slope_bot=float(a2), icpt_bot=float(k2), x0=int(xs.min()),
                   x1=int(xs.max()), centre=[float(cx), float(cy)], L=float(max(w, h)), T=float(min(w, h)))


def save(px, name):
    Image.fromarray(np.clip(px + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / f"{name}.png")


def main():
    geo = {"src": KEY, "noarm_src": NOARM}
    _, A, _, _ = cut_r2.figure(KEY)
    _, N, _, _ = cut_r2.figure(NOARM)
    rawA = np.asarray(Image.open(REDO / f"{KEY}.png").convert("RGB"))
    rawN = np.asarray(Image.open(REDO / f"{NOARM}.png").convert("RGB"))
    # ---- the no-arm drawing onto the strike drawing
    sh = {}
    for name, box in (("head", (790, 420, 1030, 660)), ("back_boot", (300, 1020, 570, 1290)),
                      ("belt", (640, 790, 840, 880)), ("far_fist", (660, 395, 780, 480))):
        sh[name] = [round(v, 2) for v in cut_r2.shift_between(rawA, rawN, box)]
    geo["noarm_shift"] = sh
    print("no-arm vs strike shifts (dx, dy, response):", sh)
    good = [v for v in sh.values() if v[2] > 0.3]
    ndx = float(np.median([v[0] for v in good])) if good else 0.0
    ndy = float(np.median([v[1] for v in good])) if good else 0.0
    N = shifted(N, -ndx, -ndy)
    geo["noarm_shift_used"] = [round(ndx, 2), round(ndy, 2)]
    # ---- soles onto the feet line
    ys = np.nonzero((A[..., 3] > 128).any(1))[0]
    DY = FEET - float(ys.max())
    geo["dy"] = DY
    A, N = shifted(A, 0, DY), shifted(N, 0, DY)
    mv = lambda p: (p[0], p[1] + DY)  # noqa: E731
    H, Wd = A.shape[:2]
    yy, xx = np.mgrid[0:H, 0:Wd].astype(np.float32)
    cA = classes(A)

    # ---- the near arm (sleeve + fist), and the body with it removed
    arm = comp_at(cA["cream"] & (yy > 672 + DY), mv(ARM_SEED)) | comp_at(cA["skin"] & (yy > 900 + DY), mv(FIST_SEED))
    arm = cv2.morphologyEx(arm.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8)) > 0
    arm_d = cv2.dilate(arm.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * ARM_DIL + 1,) * 2)) > 0
    # a soft edge (3 px) where the no-arm body takes over
    dist = cv2.distanceTransform((~arm_d).astype(np.uint8), cv2.DIST_L2, 5)
    w = np.clip(1 - dist / 3.0, 0, 1)[..., None]
    body = A * (1 - w) + N * w
    body[..., 3] = np.where(arm_d, N[..., 3], A[..., 3] * (1 - w[..., 0]) + N[..., 3] * w[..., 0])
    geo["arm_px"] = int(arm.sum())
    # pin holes the keying leaves inside the figure (stray purple pixels in the drawing): their neighbours' colour
    from scipy import ndimage
    f0 = body[..., 3] > 128
    holes = ndimage.binary_fill_holes(f0) & ~f0
    n, lab, st, _ = cv2.connectedComponentsWithStats(holes.astype(np.uint8), 8)
    pins = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] < 60])
    pins = cv2.dilate(pins.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    body[pins, :3] = cutlib.fill_rgb(body, f0 & ~pins & (body[..., 3] > 250))[pins]
    body[pins, 3] = 255
    geo["pin_holes_filled_px"] = int(pins.sum())
    c = classes(body)

    # ---- belt line: hips below, torso above
    belt, bl = belt_line(c)
    y_top = bl["slope"] * xx + bl["icpt"]
    geo["belt"] = bl
    waist = np.array(bl["centre"])
    xm = (bl["x0"] + bl["x1"]) / 2
    belt_top_mid = bl["slope"] * xm + bl["icpt"]
    print("belt: x %d..%d, top line y = %.3f x + %.1f (%.1f deg), centre %s, L %.1f T %.1f (s202: L 158 T 40)" % (
        bl["x0"], bl["x1"], bl["slope"], bl["icpt"], np.degrees(np.arctan(bl["slope"])), np.round(waist, 1), bl["L"],
        bl["T"]))
    fig = c["fig"]
    hips = fig & (yy >= y_top - 0.5)
    hips_px = body.copy()
    hips_px[~hips] = 0
    # a soft top edge on the belt (1 px), so the cut is not stair-stepped
    tt = np.clip(yy - (y_top - 1.0), 0, 1)
    hips_px[..., 3] *= np.where(hips, tt, 0)
    # the drawn belt is thicker than round 2's (edge to edge 1.12): the band under its top line squeezed by BELT_SQ,
    # the trousers' top BELT_EASE px stretched to take it up, everything lower untouched
    T = float(np.median(np.array([bl["slope_bot"] * x + bl["icpt_bot"] - (bl["slope"] * x + bl["icpt"])
                                  for x in range(bl["x0"] + 6, bl["x1"] - 5)])))
    t_out = yy - y_top
    T2 = T * BELT_SQ
    t_src = np.where(t_out < T2, t_out / BELT_SQ,
                     np.where(t_out < T + BELT_EASE, T + (t_out - T2) * BELT_EASE / (T + BELT_EASE - T2), t_out))
    t_src = np.where(t_out < 0, t_out, t_src)
    hips_px = cv2.remap(hips_px.astype(np.float32), xx, (y_top + t_src).astype(np.float32), cv2.INTER_LINEAR,
                        borderMode=cv2.BORDER_CONSTANT)
    geo["belt_squeeze"] = {"factor": BELT_SQ, "drawn_thickness_vertical": round(T, 1), "ease_px": BELT_EASE}
    waist = waist - np.array([0.0, T * (1 - BELT_SQ) / 2])      # the belt's centre after the squeeze
    save(hips_px, "hips_lunge")

    # ---- head (with the neck run on under the collar)
    head_reg = cutlib.poly_mask(fig.shape, [mv(p) for p in HEAD_POLY]) > 0
    collar = c["cream"] & (xx < COLLAR_X) & (yy > COLLAR_Y + DY)
    tails = (cv2.dilate(c["red"].astype(np.uint8), np.ones((5, 5), np.uint8)) > 0) & (xx < TAIL_X) & \
        (yy < 580 + DY) & fig
    farA = comp_at(c["cream"] & (yy < 640 + DY) & (xx < 800), (690, 520 + DY)) | \
        comp_at(c["skin"] & (yy < 500 + DY) & (xx < 800), (720, 430 + DY))
    farA = cv2.morphologyEx(farA.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
    far_d = cv2.dilate(farA.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    head = fig & head_reg & ~collar & ~c["vest"] & ~tails & ~far_d
    n, lab, st, _ = cv2.connectedComponentsWithStats(head.astype(np.uint8), 8)
    head = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    head_px = body.copy()
    head_px[~head] = 0
    neck = (cutlib.poly_mask(fig.shape, [mv(p) for p in NECK_POLY]) > 0) & ~head & fig
    skin_src = head & c["skin"] & (yy > 600 + DY) & (yy < 660 + DY) & (xx > 800) & (xx < 880)
    neck_rgb = np.median(body[..., :3][skin_src], 0)
    head_px[neck, :3] = neck_rgb
    head_px[neck, 3] = 255
    # the drawn head is a little big against round 2's (face 1.03, hair outline 1.10): scaled about the neck pivot;
    # the tails go with it (they hang on the knot), what the old outline covered is filled on the pieces behind it
    nk = np.array(mv(NECK))
    Mh = cv2.getRotationMatrix2D((float(nk[0]), float(nk[1])), 0.0, HEAD_SCALE)
    head_px = cv2.warpAffine(head_px, Mh, (Wd, H), flags=cv2.INTER_AREA, borderMode=cv2.BORDER_CONSTANT)
    save(head_px, "head_shout")
    geo["neck_rgb"] = neck_rgb.round(1).tolist()
    geo["head_scale_applied"] = HEAD_SCALE
    exposed = (head | neck) & ~(head_px[..., 3] > 128)

    # ---- tails (in front of the far arm), scaled with the head
    n, lab, st, _ = cv2.connectedComponentsWithStats((tails & ~head).astype(np.uint8), 8)
    tails = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= 300])
    # the tails run on 4 px under the head (drawn over them), so the two soft edges leave no seam
    tails_under = (cv2.dilate(tails.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0) & head
    tails_px = body.copy()
    tails_px[~(tails | tails_under)] = 0
    tails_px = cv2.warpAffine(tails_px, Mh, (Wd, H), flags=cv2.INTER_AREA, borderMode=cv2.BORDER_CONSTANT)
    save(tails_px, "tails_strike")
    exposed &= ~(tails_px[..., 3] > 128)

    # ---- far arm (behind tails, head and torso): its pixels under the tails filled with its own colours
    far = (far_d & fig & ~c["vest"]) | (tails & cv2.dilate(farA.astype(np.uint8), np.ones((41, 41), np.uint8)) > 0)
    far &= ~head
    far_px = body.copy()
    far_px[~far] = 0
    fill_from = farA & ~tails
    under = far & tails
    far_px[under, :3] = cutlib.fill_rgb(body, fill_from)[under]
    far_px[under, 3] = 255
    # keep only where the far sleeve really continues under the tails: inside the convex hull of the sleeve
    hull = cv2.convexHull(np.column_stack(np.nonzero(farA)[::-1]).astype(np.int32))
    hm = np.zeros(fig.shape, np.uint8)
    cv2.fillConvexPoly(hm, hull, 1)
    far_px[(hm == 0) & under] = 0
    fs = np.array(mv(FAR_SHOULDER))
    root = (np.hypot(xx - fs[0], yy - fs[1]) < 30) & ~far
    far_cloth = np.median(body[..., :3][farA & c["cream"] & (np.hypot(xx - fs[0], yy - fs[1]) < 110)], 0)
    far_px[root, :3] = far_cloth
    far_px[root, 3] = 255
    ex_far = exposed & (hm > 0) & ~far
    far_px[ex_far, :3] = cutlib.fill_rgb(body, fill_from)[ex_far]
    far_px[ex_far, 3] = 255
    geo["head_exposed_px_far_arm"] = int(ex_far.sum())
    save(far_px, "far_arm_back")

    # ---- torso: everything else above the belt line (vest, collar, outlines), carried on under the belt
    torso = fig & (yy < y_top + 0.5) & ~head & ~tails & ~far
    torso = cv2.morphologyEx(torso.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
    torso_px = body.copy()
    torso_px[~torso] = 0
    above = torso & c["vest"] & (yy < y_top - 3) & (yy > y_top - 40)
    fillc = cutlib.fill_rgb(torso_px, above)
    # under the belt: between the vest's sides just above the belt line, down 34 px
    sides = []
    for x in range(bl["x0"], bl["x1"] + 1):
        y = int(bl["slope"] * x + bl["icpt"]) - 6
        if torso[y, x]:
            sides.append(x)
    ux0, ux1 = min(sides), max(sides)
    under_b = (yy >= y_top - 3) & (yy < y_top + 34) & (xx >= ux0 + 2) & (xx <= ux1 - 2)
    torso_px[under_b, :3] = fillc[under_b]
    torso_px[under_b, 3] = 255
    # the first px under the line (seen through the hips' soft top edge) keep the drawing's own belt, so no green line
    seam = (yy >= y_top - 3) & (yy < y_top + 4) & fig & (xx >= bl["x0"] - 2) & (xx <= bl["x1"] + 2)
    torso_px[seam] = body[seam]
    torso_px[seam, 3] = 255
    # where the full-size head covered the vest (hair over the back of the collar): vest colour, inside the vest's hull
    th = cv2.convexHull(np.column_stack(np.nonzero(torso & (yy < y_top))[::-1]).astype(np.int32))
    tm = np.zeros(fig.shape, np.uint8)
    cv2.fillConvexPoly(tm, th, 1)
    ex_t = (exposed | (tails & ~(tails_px[..., 3] > 128))) & (tm > 0) & ~torso & ~(far_px[..., 3] > 128)
    vest_src = torso & (c["vest"] | c["cream"])
    torso_px[ex_t, :3] = cutlib.fill_rgb(body, vest_src)[ex_t]
    torso_px[ex_t, 3] = 255
    # small background pockets the drawing leaves between the tails, the hair and the vest's back (they would show the
    # background and the rim inside the figure): vest colour
    from scipy import ndimage
    pocket = ndimage.binary_fill_holes(fig) & ~fig & (yy > 480 + DY) & (yy < 620 + DY) & (xx > 730) & (xx < 840)
    pocket = cv2.dilate(pocket.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    pocket &= ~(far_px[..., 3] > 128)
    # on the back-most piece (the far arm), so the tails', head's and vest's own soft edges lie over it
    far_px[pocket, :3] = cutlib.fill_rgb(body, vest_src)[pocket]
    far_px[pocket, 3] = 255
    save(far_px, "far_arm_back")
    geo["pocket_px_filled"] = int(pocket.sum())
    geo["head_exposed_px_torso"] = int(ex_t.sum())
    save(torso_px, "torso_strike")
    geo["torso_under_belt_x"] = [ux0, ux1]

    # ---- armhole: the drawn near sleeve's top (from the strike drawing A), tapered onto the painted sleeve's width
    Rt = np.array(mv(ARM_TOP))
    Wr = np.array(mv(ARM_WRIST))
    d = (Wr - Rt) / np.linalg.norm(Wr - Rt)
    nrm = np.array([-d[1], d[0]])
    u = (xx - Rt[0]) * d[0] + (yy - Rt[1]) * d[1]
    v = (xx - Rt[0]) * nrm[0] + (yy - Rt[1]) * nrm[1]
    a1 = float(np.degrees(np.arctan2(d[1], d[0])))
    arm_line = cv2.dilate(arm.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0   # the sleeve with its outline
    us = np.arange(0, U_CUT + CUT_SOFT + 6, 5.0)
    vmin, vmax = [], []
    for uu in us:
        sel = arm_line & (np.abs(u - uu) < 0.7) & (np.abs(v) < 90) & (A[..., 3] > 128)
        vmin.append(float(v[sel].min()))
        vmax.append(float(v[sel].max()))
    vmin, vmax = np.array(vmin), np.array(vmax)
    geo["drawn_sleeve_extent_by_u"] = {int(a_): [round(b_), round(c_)] for a_, b_, c_ in zip(us, vmin, vmax)}
    print("drawn sleeve across (vmin, vmax) by u from its top:", geo["drawn_sleeve_extent_by_u"])
    t = np.clip((u - U_T0) / (U_CUT - U_T0), 0, 1)
    t = t * t * (3 - 2 * t)
    hwp = rig_r2.hw_of(np.maximum(u - S_IN, 0)) + 1.5        # the drawn root ends a hair wider than the painted tube
    lo_s, hi_s = np.interp(u, us, vmin), np.interp(u, us, vmax)
    lo_t, hi_t = (1 - t) * lo_s + t * -hwp, (1 - t) * hi_s + t * hwp
    v_src = np.where(t > 0, lo_s + (v - lo_t) * (hi_s - lo_s) / np.maximum(hi_t - lo_t, 1), v)
    mx = (Rt[0] + d[0] * u + nrm[0] * v_src).astype(np.float32)
    my = (Rt[1] + d[1] * u + nrm[1] * v_src).astype(np.float32)
    tapered = cv2.remap(A.astype(np.float32), mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    on = cv2.remap(arm_line.astype(np.float32), mx, my, cv2.INTER_LINEAR) > 0.5
    keep_u = np.clip((U_CUT - u) / CUT_SOFT + 0.5, 0, 1) * (u > -60)
    arm_px = tapered.copy()
    # the painted sleeve's hard shadow band (its back third: rig_r2 SHADE_SIDE +1 shows on v > 0 here) carried up
    # the tapered root, faded in from U_T0, so the band does not start at the cut
    c_t, hw_t = (lo_t + hi_t) / 2, (hi_t - lo_t) / 2
    band = ((v - c_t) > hw_t / 3).astype(np.float32)
    band = cv2.GaussianBlur(band, (0, 0), 0.7)
    sb = np.clip((u - U_T0) / (U_CUT - 10 - U_T0), 0, 1)
    sb = sb * sb * (3 - 2 * sb)
    Rr, Gg, Bb = [arm_px[..., i] for i in range(3)]
    cream_t = (arm_px[..., 3] > 20) & (Rr > 170) & (Gg > 140) & (Bb > 95) & (Rr - Bb < 110)
    ratio = (rig_r2.CREAM_SH / rig_r2.CREAM).astype(np.float32)
    k_b = (band * sb * cream_t)[..., None]
    arm_px[..., :3] *= 1 - (1 - ratio) * k_b
    arm_px[..., 3] *= keep_u * on
    arm_px[arm_px[..., 3] <= 0.5] = 0
    save(arm_px, "armhole_strike")
    geo["taper"] = {"u_from": U_T0, "u_to": U_CUT, "painted_width_at_cut": round(float(2 * rig_r2.hw_of(U_CUT - S_IN)), 1),
                    "drawn_width_at_cut": round(float(np.interp(U_CUT, us, vmax - vmin)), 1),
                    "drawn_width_at_top_40": round(float(np.interp(40, us, vmax - vmin)), 1)}
    print("taper", geo["taper"])

    # figure pixels no piece took (thin bits the masks' openings dropped): each goes to the piece nearest to it
    order = ["far_arm_back", "tails_strike", "head_shout", "torso_strike", "hips_lunge"]
    pcs = {k: np.asarray(Image.open(OUT / f"{k}.png")).astype(np.float32) for k in order}
    labm = np.zeros(fig.shape, np.int32)
    for i, k in enumerate(order, 1):
        labm[pcs[k][..., 3] > 128] = i
    left = fig & (labm == 0) & ~cv2.dilate((~fig).astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    _, idx = ndimage.distance_transform_edt(labm == 0, return_indices=True)
    near = labm[idx[0], idx[1]]
    geo["leftover_px"] = {}
    for i, k in enumerate(order, 1):
        m = left & (near == i)
        if m.any():
            pcs[k][m, :3] = body[m, :3]
            pcs[k][m, 3] = 255
            save(pcs[k], k)
            geo["leftover_px"][k] = int(m.sum())
    print("leftover figure px given to pieces:", geo["leftover_px"])

    S = Rt + d * S_IN
    geo.update({
        "S": S.round(1).tolist(), "a1_drawn": round(a1, 1), "arm_top": Rt.round(1).tolist(),
        "drawn_wrist": Wr.round(1).tolist(), "drawn_arm_len": round(float(np.linalg.norm(Wr - S)), 1),
        "u_cut": U_CUT, "neck": list(mv(NECK)), "waist": waist.round(1).tolist(),
        "knot": (nk + HEAD_SCALE * (np.array(mv(KNOT)) - nk)).round(1).tolist(),
        "far_shoulder": fs.round(1).tolist(), "belt_x": [bl["x0"], bl["x1"]], "belt_top_mid": round(float(belt_top_mid), 1),
        "belt_top_slope_deg": round(float(np.degrees(np.arctan(bl["slope"]))), 2),
        "waist_offset_from_standing": [round(float(waist[0] - P2["waist"][0]), 1), round(float(waist[1] - P2["waist"][1]), 1)],
        "cast_ratio": P2["cast_ratio"], "vest_lit": P2["vest_lit"], "vest_shadow": P2["vest_shadow"],
        "pieces": {"hips": "hips_lunge", "torso": "torso_strike", "armhole": "armhole_strike", "head": "head_shout",
                   "tails": "tails_strike", "far_arm": "far_arm_back"},
        "layer_order_back_to_front": ["far_arm", "tails", "head", "torso", "hips", "painted round shoulder", "sword",
                                      "fist", "painted sleeve", "armhole"],
        # the same keys as parts_r4.json: the drawn arm is straight, so its elbow is on the S..wrist line at UE/L
        "arm_root": Rt.round(1).tolist(),
        "arm_elbow": (S + (Wr - S) * 134.0 / 258.5).round(1).tolist(),
        "valid_for": ("the strike only: painted upper arm angle (screen) = a1_drawn (74.4) with no torso turn; the drawn "
                      "arm is 330.7 px from S to wrist against the painted 258.5, so the bones are stretched x1.23 "
                      "(rig stretch 1.3) for the fist to reach the drawn wrist; at bone length the fist sits ~57 px higher"),
    })
    (REDO / "parts_r5.json").write_text(json.dumps(geo, indent=1))
    print("S", geo["S"], "a1_drawn", geo["a1_drawn"], "drawn arm len S..wrist", geo["drawn_arm_len"], "(painted L 258.5)")
    print("waist", geo["waist"], "offset from standing", geo["waist_offset_from_standing"], "neck", geo["neck"])
    print({k: int((np.asarray(Image.open(OUT / f"{k}.png"))[..., 3] > 128).sum()) for k in geo["pieces"].values()})


if __name__ == "__main__":
    main()
