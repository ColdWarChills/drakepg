"""Round 4 cut: the wind-up torso (Qwen edit r4_windup_s<seed>) fitted onto hero 4's round-2 parts.
<venv python> -I -B cut_r4.py [drawing key, default r4_windup_s402] [fit]
  fit: only write parts_r4/_fitted.png (the drawing moved onto s202's belt and leaned) and print the numbers.

Output: art/redo/hero_joints/parts_r4/ + parts_r4.json (same canvas and scale as parts_r2).
- hips, head, tails: copied from parts_r2 unchanged (the swap cannot jump at the belt or change the face).
- the drawing is moved so its belt lies on s202's belt, then leaned back by a shear about the belt line so its collar
  lands where s202's collar is (the old head sits on it, a head tilt is a turn about the neck pivot); in the last
  FIT_H px above the belt its sides are eased onto the round-2 torso's sides (no step at the waist).
- torso: the leaned vest, collar and neck V (below s202's collar line), without the near arm and the far arm, carried on
  under the belt like round 2.
- armhole: the drawn near shoulder and the root of the raised sleeve up to a feathered cut across the upper arm,
  drawn OVER the painted sleeve (rig_r2's ARMHOLE layer), so the painted arm comes out of a drawn raised shoulder.
- far_arm: the drawn far arm (reaching forward) with a round root behind the torso.
Pivots: S inside the drawn sleeve root on its axis; a1_drawn = the drawn upper arm's angle (the turned torso is only
valid with the arm near it); neck, waist, knot as round 2; far_shoulder the new drawing's."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import shutil

import cv2
import numpy as np
from PIL import Image

from jcommon import REDO, ROOT

sys.path.insert(0, str(ROOT / "scripts" / "hero2d"))
import cutlib  # noqa: E402

KEY = sys.argv[1] if len(sys.argv) > 1 else "r4_windup_s402"
ONLY_FIT = len(sys.argv) > 2 and sys.argv[2] == "fit"
_argv, sys.argv = sys.argv, sys.argv[:1]
import cut_r2  # noqa: E402  (figure(), shift_between(); its main() is not run)
sys.argv = _argv

OUT = REDO / "parts_r4"
OUT.mkdir(exist_ok=True)
P1 = json.loads((REDO / "parts.json").read_text())
P2 = json.loads((REDO / "parts_r2.json").read_text())
FIT_H = 70.0           # px above the belt over which the sides are eased onto round 2's
# ---- read on _fitted grid crops (gridlook.py parts_r4/_fitted:...) for r4_windup_s402
ARM_ROOT = (637.0, 498.0)     # the drawn sleeve's root centre where it leaves the vest
ARM_ELBOW = (490.0, 428.0)    # the drawn elbow centre
U_CUT = 55.0                  # px from ARM_ROOT along the upper arm: the drawn root ends here (feathered)
CUT_SOFT = 6.0                # feather length
S_IN = 15.0                   # S sits this far from ARM_ROOT toward the elbow (inside the drawn root)
ROOT_R = 95.0                 # the armhole piece: drawn pixels within this of ARM_ROOT (and u < U_CUT)
NECK_FADE = (-16.0, -2.0)     # px from the collar line: the drawing's neck skin fades in over the old neck's bottom
BAND_LUM = 573.0              # R+G+B between the drawn sleeve's lit (613) and shadow (533) cream
DRAWN_OVER = 1.5             # px
U_T0 = 15.0                   # the taper starts here
ARM_X_MAX = 664               # the near arm lies left of this (the vest's near side)
FAR_SHOULDER = (828.0, 548.0)  # inside the vest, level with the middle of the far sleeve's root (x ~838, y 497..600)
FAR_ROW0 = 490                # from this row down, right of the vest's far edge = the far arm (above: the collar)


def belt_box_shift(src, new):
    """The drawing's belt against s202's: centre of the belt's x range and its median top (as r4_gate)."""
    import r4_gate
    rb = r4_gate.belt(src, r4_gate.fig_mask(src))
    nb = r4_gate.belt(new, r4_gate.fig_mask(new))
    return (nb["x0"] + nb["x1"]) / 2 - (rb["x0"] + rb["x1"]) / 2, nb["top"] - rb["top"], rb, nb


def only_big(px, min_area, drop_right_of=None):
    """Drop a piece's stray bits: components smaller than min_area (and any centred right of drop_right_of)."""
    a = (px[..., 3] > 0).astype(np.uint8)
    n, lab, st, cen = cv2.connectedComponentsWithStats(a, 8)
    out = px.copy()
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] < min_area or (drop_right_of is not None and cen[i][0] > drop_right_of):
            out[lab == i] = 0
    return out


def edges(alpha, y, thr=128):
    xs = np.nonzero(alpha[int(round(y))] > thr)[0]
    return float(xs.min()), float(xs.max())


def vest_alpha(px):
    """255 where a piece shows the dark teal vest (its sides are what must not jump at the waist)."""
    R, G, B, A = [px[..., i].astype(np.float32) for i in range(4)]
    m = (A > 128) & (G > R + 8) & (G < 110) & (B < 110) & (G > 30)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    return m.astype(np.float32) * 255


def main():
    src = np.asarray(Image.open(REDO / "r2_vest_teal_s202.png").convert("RGB"))
    _, px, fig, rgb = cut_r2.figure(KEY)
    new = np.asarray(Image.open(REDO / f"{KEY}.png").convert("RGB"))
    H, Wd = fig.shape
    yy, xx = np.mgrid[0:H, 0:Wd].astype(np.float32)
    geo = {"src": KEY}

    # ---- 1. belt onto s202's belt (integer box estimate, then phase correlation in the shifted box)
    dx0, dy0, rb, nb = belt_box_shift(src, new)
    M = np.float32([[1, 0, -dx0], [0, 1, -dy0]])
    moved = cv2.warpAffine(new, M, (Wd, H), borderMode=cv2.BORDER_REPLICATE)
    rdx, rdy, resp = cut_r2.shift_between(src, moved, (660, 690, 880, 760))
    dx, dy = dx0 + rdx, dy0 + rdy
    geo["belt_shift"] = [round(dx, 2), round(dy, 2), round(resp, 3)]
    print("belt shift (drawing -> s202)", geo["belt_shift"], "belt widths", rb["w"], nb["w"])

    # ---- 2. the collar: where it lands after the belt move, against s202's (phase correlation on the collar box)
    M = np.float32([[1, 0, -dx], [0, 1, -dy]])
    moved = cv2.warpAffine(new, M, (Wd, H), borderMode=cv2.BORDER_REPLICATE)
    cbox = (660, 430, 840, 520)
    cdx, cdy, cresp = cut_r2.shift_between(src, moved, cbox)
    hdx, hdy, hresp = cut_r2.shift_between(src, moved, (650, 280, 900, 470))
    yb = float(P2["belt_top_mid"])
    y_neck = float(P2["neck"][1])
    # the collar's own correlation is weak when the collar is redrawn: then the head's shift stands for the neck's
    ndx = cdx if cresp >= 0.3 else hdx
    geo["neck_shift_used"] = round(ndx, 2)
    k = -ndx / (y_neck - yb)          # x_out = x + k (y - yb): moves the neck by -ndx
    geo["collar_shift_before_lean"] = [round(cdx, 2), round(cdy, 2), round(cresp, 3)]
    geo["head_shift_before_lean"] = [round(hdx, 2), round(hdy, 2), round(hresp, 3)]
    geo["lean_shear"] = round(k, 4)
    geo["lean_deg"] = round(float(np.degrees(np.arctan(k))), 2)
    print("collar shift", geo["collar_shift_before_lean"], "head shift", geo["head_shift_before_lean"],
          "-> shear k %.4f (%.1f deg, top to the %s)" % (k, geo["lean_deg"], "left" if k > 0 else "right"))

    # ---- 3. one inverse map: belt move + shear, then the sides eased onto round 2's torso near the belt
    t2 = vest_alpha(np.asarray(Image.open(REDO / "parts_r2" / "torso.png")))
    y_ref = yb - 10
    oL, oR = edges(t2, y_ref)
    # the drawing's sides at the same row after move + shear (alpha of the keyed figure)
    a_new = vest_alpha(px)
    xs_map = xx - k * (yy - yb) + dx
    # and the neck's height: above the belt the drawing is pulled up so its collar sits at s202's height (hdy at the
    # neck, 0 at the belt; a vertical squash of the torso by hdy / (belt - neck))
    ndy = hdy if hresp >= 0.3 else 0.0
    geo["neck_dy_used"] = round(ndy, 2)
    ys_map = yy + dy + ndy * np.clip((yb - yy) / (yb - y_neck), 0, None)
    a_lean = cv2.remap(a_new.astype(np.float32), xs_map, ys_map, cv2.INTER_LINEAR)
    nL, nR = edges(a_lean, y_ref)
    w = np.clip((yy - (yb - FIT_H)) / FIT_H, 0, 1) ** 1.5 * (yy <= yb + 40) + (yy > yb + 40)
    w = w * (yy > yb - FIT_H)
    co, so, cn, sn = (oL + oR) / 2, oR - oL, (nL + nR) / 2, nR - nL
    ct = cn + w * (co - cn)
    st = sn + w * (so - sn)
    x_lean = cn + (xx - ct) * sn / st        # output x -> x in the leaned picture
    xs_map = x_lean - k * (yy - yb) + dx
    fitted = cv2.remap(px.astype(np.float32), xs_map.astype(np.float32), ys_map.astype(np.float32), cv2.INTER_LINEAR,
                       borderMode=cv2.BORDER_CONSTANT)
    fa = fitted[..., 3]
    fv = vest_alpha(fitted)
    geo["sides_above_belt_r2"] = [oL, oR]
    geo["sides_above_belt_new_before_ease"] = [nL, nR]
    print("vest sides at belt-10: round 2 %.0f..%.0f, new before ease %.0f..%.0f" % (oL, oR, nL, nR))
    # after: per row, the new figure's sides vs round 2's torso (where both exist), rows belt-40..belt-2
    rows = {}
    for y in (yb - 40, yb - 25, yb - 10, yb - 4):
        rows[int(y)] = [edges(t2, y), edges(fv, y)]
    geo["sides_rows"] = rows
    print("vest sides (r2 torso | fitted):", {k_: (np.round(v[0]).tolist(), np.round(v[1]).tolist()) for k_, v in rows.items()})
    Image.fromarray(np.clip(fitted + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "_fitted.png")
    if ONLY_FIT:
        (REDO / "parts_r4.json").write_text(json.dumps(geo, indent=1))
        return

    frgb = fitted[..., :3]
    R, G, B = frgb[..., 0], frgb[..., 1], frgb[..., 2]
    ffig = fa > 128
    cream = ffig & (R > 170) & (G > 140) & (B > 95) & (R - B < 110)
    skin = ffig & (R > 160) & (G > 95) & (G < 185) & (B < 120) & (R - B > 85)
    # ---- the collar line (s202's, round 1 numbers): above = head (the old head is used)
    (x0, y0), (x1, y1) = P1["collar_line"]
    line_y = y0 + (xx - x0) * (y1 - y0) / (x1 - x0)
    belt_top = yb

    # ---- the near arm: axis from the root to the elbow
    Rt, El = np.array(ARM_ROOT), np.array(ARM_ELBOW)
    d = (El - Rt) / np.linalg.norm(El - Rt)
    nrm = np.array([-d[1], d[0]])
    u = (xx - Rt[0]) * d[0] + (yy - Rt[1]) * d[1]
    v = (xx - Rt[0]) * nrm[0] + (yy - Rt[1]) * nrm[1]
    a1 = float(np.degrees(np.arctan2(d[1], d[0])))
    def arm_mask(img):
        R_, G_, B_, A_ = [img[..., i] for i in range(4)]
        f_ = A_ > 128
        cr = f_ & (R_ > 170) & (G_ > 140) & (B_ > 95) & (R_ - B_ < 110)
        sk = f_ & (R_ > 160) & (G_ > 95) & (G_ < 185) & (B_ < 120) & (R_ - B_ > 85)
        m_ = (cr | sk) & (xx < ARM_X_MAX) & (yy < belt_top)
        n_, lab_, st_, _ = cv2.connectedComponentsWithStats(m_.astype(np.uint8), 8)
        m_ = lab_ == (1 + int(np.argmax(st_[1:, cv2.CC_STAT_AREA])))
        return cv2.dilate(m_.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0, cr, sk
    near_arm, _, _ = arm_mask(fitted)
    # the drawn sleeve's extent across its axis per u (upper arm only: the raised forearm lies beyond U_CUT + 20)
    us = np.arange(0, U_CUT + CUT_SOFT + 6, 5.0)
    vmin, vmax, vband = [], [], []
    lum = frgb.sum(-1)
    for uu in us:
        sel = near_arm & cream & (np.abs(u - uu) < 0.7) & (np.abs(v) < 90)
        vmin.append(float(v[sel].min()))
        vmax.append(float(v[sel].max()))
        # the drawn shadow band (on the v < 0 side, under the arm): from vmin up while the cream is in shadow
        vb = np.round(v[sel]).astype(int)
        lb = lum[sel]
        b_ = vb.min()
        while b_ < vb.max() and (lb[vb == b_ + 1].mean() if (vb == b_ + 1).any() else 0) < BAND_LUM:
            b_ += 1
        vband.append(float(b_ + 0.5))
    vmin, vmax, vband = np.array(vmin), np.array(vmax), np.array(vband)
    geo["drawn_band_edge_by_u"] = {int(a_): round(b_, 1) for a_, b_ in zip(us, vband)}
    print("drawn shadow band edge by u:", geo["drawn_band_edge_by_u"])
    geo["drawn_sleeve_extent_by_u"] = {int(a_): [round(b_), round(c_)] for a_, b_, c_ in zip(us, vmin, vmax)}
    print("drawn sleeve across (vmin, vmax) by u:", geo["drawn_sleeve_extent_by_u"])
    # taper: from U_T0 to U_CUT the drawn sleeve is eased (across only) onto the painted sleeve's width and axis, so
    # the painted tube comes out of it without a step
    sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
    import rig_r2
    t = np.clip((u - U_T0) / (U_CUT - U_T0), 0, 1)
    t = t * t * (3 - 2 * t) * (u < U_CUT + CUT_SOFT + 8) * (u > 0)
    t = np.where(u >= U_CUT, (u < U_CUT + CUT_SOFT + 8).astype(np.float32), t)
    hwp = rig_r2.hw_of(np.maximum(u - S_IN, 0))
    lo_s, hi_s = np.interp(u, us, vmin), np.interp(u, us, vmax)
    mb_s = np.interp(u, us, vband)
    hwd = hwp + DRAWN_OVER     # the drawn root ends a hair wider than the painted tube: its edge stays the outer one
    lo_t, hi_t = (1 - t) * lo_s + t * -hwd, (1 - t) * hi_s + t * hwd
    mb_t = (1 - t) * mb_s + t * (-hwp / 3)        # the painted band's edge (rig_r2: |v| > hw / 3 on the shadow side)
    v_lo = lo_s + (v - lo_t) * (mb_s - lo_s) / np.maximum(mb_t - lo_t, 1)
    v_hi = mb_s + (v - mb_t) * (hi_s - mb_s) / np.maximum(hi_t - mb_t, 1)
    v_src = np.where(t > 0, np.where(v < mb_t, v_lo, v_hi), v)
    mx = (Rt[0] + d[0] * u + nrm[0] * v_src).astype(np.float32)
    my = (Rt[1] + d[1] * u + nrm[1] * v_src).astype(np.float32)
    tapered = cv2.remap(fitted.astype(np.float32), mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    geo["taper"] = {"u_from": U_T0, "u_to": U_CUT, "painted_half_width_at_cut": round(float(rig_r2.hw_of(U_CUT - S_IN)), 1),
                    "drawn_width_at_cut": round(float(np.interp(U_CUT, us, vmax - vmin)), 1)}
    print("taper", geo["taper"])

    # ---- torso: leaned vest + collar + neck V, below the collar line, no near arm, no far arm
    # the neck skin stays the old head's (its neck runs on under the collar): no new skin on the torso
    vest_c = ffig & (G > R + 8) & (G < 110) & (B < 110) & (G > 30)
    # the far side: everything right of the vest's far edge (row by row, below the collar) is the far arm, its outline
    # lines included, never the torso
    far_edge = np.full(H, np.inf, np.float32)
    for y_ in range(FAR_ROW0, int(belt_top)):
        xs_ = np.nonzero(vest_c[y_, 760:920])[0]
        if len(xs_):
            far_edge[y_] = 760 + xs_.max()
    right_of_vest = (xx > far_edge[:, None] + 1) & (yy >= FAR_ROW0)
    torso = ffig & (yy >= line_y) & (yy < belt_top + 2) & ~near_arm & ~right_of_vest & ~(skin & (yy < line_y + 70))
    # the raised near shoulder rises above s202's collar line left of the neck: keep its vest and cream there (they
    # cover the old head's cut through the hair), not the drawing's own hair, headband or skin
    shoulder_up = ffig & (xx < 700) & (yy < line_y) & (yy > line_y - 45) & (vest_c | cream) & ~skin
    shoulder_up = cv2.morphologyEx(shoulder_up.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
    torso = torso | (shoulder_up & ~near_arm)
    # the collar's back rises above that line too: its cream stays whole (no flat cut along the line)
    collar_up = ffig & cream & (xx >= 690) & (xx < 830) & (yy < line_y) & (yy > line_y - 25)
    collar_up = cv2.morphologyEx(collar_up.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
    torso = torso | collar_up
    geo["shoulder_above_collar_line_px"] = int(shoulder_up.sum())
    torso_px = fitted.copy()
    torso_px[~torso] = 0
    # the neck inside the collar's V: the drawing's own neck skin (the V opens lower than s202's), faded in from just
    # under the collar line so the old head's drawn neck runs into it without a line
    neck_v = skin & (yy >= line_y + NECK_FADE[0]) & (yy < line_y + 70) & (xx > 700) & (xx < 830) & ffig
    ramp = np.clip((yy - line_y - NECK_FADE[0]) / (NECK_FADE[1] - NECK_FADE[0]), 0, 1)
    torso_px[neck_v] = fitted[neck_v]
    torso_px[..., 3] = np.where(neck_v, fitted[..., 3] * ramp, torso_px[..., 3])
    # the old head piece carries s202's collar top above the collar line (hidden under nothing in the ready frame, it is
    # that frame's collar); here the drawing's own neck and collar cover it fully
    hp = np.asarray(Image.open(REDO / "parts_r2" / "head.png")).astype(np.float32)
    hR, hG, hB, hA = [hp[..., i] for i in range(4)]
    sliver = (hA > 20) & (hR > 170) & (hG > 140) & (hB > 95) & (hR - hB < 110) & (xx > 690) & (xx < 840) & \
        (yy > line_y - 30) & (yy < line_y + 2)
    sliver = cv2.dilate(sliver.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    cover = sliver & ffig & (skin | cream)
    torso_px[cover] = fitted[cover]
    geo["old_collar_sliver_px"] = [int(sliver.sum()), int(cover.sum())]
    above = torso & (yy < belt_top - 3) & (yy > belt_top - 40)
    fillc = cutlib.fill_rgb(torso_px, above)
    xs_t = np.nonzero(torso[int(belt_top) - 8])[0]
    under = (yy >= belt_top - 3) & (yy < belt_top + 34) & (xx >= xs_t.min() + 2) & (xx <= xs_t.max() - 2)
    torso_px[under, :3] = fillc[under]
    torso_px[under, 3] = 255
    torso_px = only_big(torso_px, 200)
    Image.fromarray(np.clip(torso_px + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "torso.png")

    # ---- armhole: the drawn shoulder + sleeve root, drawn over the painted sleeve; feathered across the upper arm
    keep_u = np.clip((U_CUT - u) / CUT_SOFT + 0.5, 0, 1)
    region = (np.hypot(xx - Rt[0], yy - Rt[1]) < ROOT_R) & (yy >= line_y - 45) & (yy < belt_top)
    near_t, cream_t, skin_t = arm_mask(tapered)
    arm_px = tapered.copy()
    # (no skin test on the arm: the sleeve's tan outline reads as skin; the fist lies past U_CUT, where keep_u drops it)
    on_arm = near_t & (cream_t | (yy >= line_y))
    a = arm_px[..., 3] * region * np.where(on_arm, keep_u, 1.0)
    # vest and collar within the region stay whole (they lie over the painted sleeve's round end)
    # the drawn dark outline between the sleeve root and the vest is neither cream nor vest: close the gap (not onto
    # the drawing's hair or headband) so the old tails behind do not show through it
    keep = cv2.morphologyEx((on_arm | torso).astype(np.uint8), cv2.MORPH_CLOSE,
                            cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))) > 0
    redish = (R - G > 40) & (R - B > 30)
    hairish = (R < 95) & (G < 75) & (R > G + 4)
    keep = (keep & ffig & ~redish & ~hairish & (~skin_t | near_t)) | on_arm | torso
    arm_px[..., 3] = a * keep.astype(np.float32)
    arm_px[arm_px[..., 3] <= 0.5] = 0
    Image.fromarray(np.clip(arm_px + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "armhole.png")
    # sleeve colours of the drawing at the seam (lit and shadow clusters), for the painted sleeve
    seam = cream_t & near_t & (np.abs(u - U_CUT + 6) < 6)
    cl = tapered[..., :3][seam].astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 0.5)
    _, labs, cen = cv2.kmeans(cl, 2, None, crit, 4, cv2.KMEANS_PP_CENTERS)
    order = np.argsort(cen.sum(1))[::-1]
    geo["seam_cream_lit"], geo["seam_cream_shadow"] = cen[order[0]].tolist(), cen[order[1]].tolist()
    geo["seam_side_of_shadow"] = float(np.median(v[seam][labs.ravel() == order[1]]))
    print("seam cream lit", np.round(cen[order[0]]), "shadow", np.round(cen[order[1]]),
          "shadow side v", round(geo["seam_side_of_shadow"], 1))

    # ---- far arm: the drawn far sleeve + fist right of the vest's far edge, with a round root behind the torso
    far = ffig & right_of_vest & (yy < belt_top) & (xx > 790)
    n, lab, st, _ = cv2.connectedComponentsWithStats(far.astype(np.uint8), 8)
    far = (lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))) & ~torso
    far_px = fitted.copy()
    far_px[~far] = 0
    fs = np.array(FAR_SHOULDER)
    far_cloth = np.median(frgb[far & cream & (np.hypot(xx - fs[0], yy - fs[1]) < 80)], 0)
    root = (np.hypot(xx - fs[0], yy - fs[1]) < 34) & ~far & torso
    far_px[root, :3] = far_cloth
    far_px[root, 3] = 255
    Image.fromarray(np.clip(far_px + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "far_arm.png")

    # ---- unchanged round-2 pieces
    for k_ in ("hips", "tails"):
        shutil.copyfile(REDO / "parts_r2" / f"{k_}.png", OUT / f"{k_}.png")
    # the head as round 2's, less two stray bits round 2's piece carries (the old far fist's top at ~(996, 502), hidden
    # by the raised far fist in round 2's frames, and a 10 px speck): the face and hair pixels are unchanged
    hp = np.asarray(Image.open(REDO / "parts_r2" / "head.png")).astype(np.float32)
    hp2 = only_big(hp, 300, drop_right_of=900)
    geo["head_px_removed_vs_r2"] = int(((hp[..., 3] > 0) & (hp2[..., 3] == 0)).sum())
    Image.fromarray(np.clip(hp2 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "head.png")

    S = Rt + d * S_IN
    geo.update({
        "S": S.round(1).tolist(), "a1_drawn": round(a1, 1), "arm_root": list(ARM_ROOT), "arm_elbow": list(ARM_ELBOW),
        "u_cut": U_CUT, "neck": P2["neck"], "waist": P2["waist"], "knot": P2["knot"], "far_shoulder": list(FAR_SHOULDER),
        "belt_x": P2["belt_x"], "belt_top_mid": P2["belt_top_mid"], "cast_ratio": P2["cast_ratio"],
        "vest_lit": P2["vest_lit"], "vest_shadow": P2["vest_shadow"],
        "valid_for": ("the wind-up only: painted upper arm angle (screen) = a1_drawn + the torso's turn (lean); "
                      "tested: +-0 clean, +7.6 deg slight kink, -15.5 and +19.5 deg clear steps -> keep within ~5 deg"),
    })
    (REDO / "parts_r4.json").write_text(json.dumps(geo, indent=1))
    print("S", geo["S"], "a1_drawn", geo["a1_drawn"])
    print({k_: int((np.asarray(Image.open(OUT / f"{k_}.png"))[..., 3] > 128).sum())
           for k_ in ("torso", "armhole", "far_arm", "hips", "head", "tails")})


if __name__ == "__main__":
    main()
