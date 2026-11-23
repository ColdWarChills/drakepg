"""Round 2 cut: the body pieces for the rig (kind B), in art/redo/hero_joints/parts_r2/ + parts_r2.json.
<venv python> -I -B cut_r2.py [torso drawing, default cutj3_s123]

From cutj3_s123 (round 1's cut, same points): head (minus the headband tails), tails (own piece, turning about the
knot), far arm (with a round root behind the torso), hips (cut on the belt's own top edge, so the belt stays on the
hips). From the torso drawing (the vest pick): the torso, carried on under the belt (the belt is drawn over it, so a
lean opens no gap). Round 1's parts/ and parts.json are not touched."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image

from jcommon import REDO, ROOT

sys.path.insert(0, str(ROOT / "scripts" / "hero2d"))
import cutlib  # noqa: E402

TORSO_SRC = sys.argv[1] if len(sys.argv) > 1 else "cutj3_s123"
OUT = REDO / "parts_r2"
OUT.mkdir(exist_ok=True)
P1 = json.loads((REDO / "parts.json").read_text())
KNOT = (693.0, 368.0)   # the headband's knot (read on _tails.png)
TAIL_CUT_X = 689        # the tails piece: left of this, the knot stays on the head
ARMHOLE_W, ARMHOLE_R = 11.0, 70.0   # the vest strip over the sleeve root: width, reach from the shoulder pivot


def poly(shape, pts):
    m = np.zeros(shape, np.uint8)
    cv2.fillPoly(m, [np.round(np.array(pts)).astype(np.int32)], 1)
    return m > 0


def figure(name):
    src = np.asarray(Image.open(REDO / f"{name}.png").convert("RGB"))
    dpu = np.abs(src.astype(np.float32) - np.array(cutlib.PURPLE)).max(-1)
    a = np.clip((dpu - 40) / 50, 0, 1)
    n, lab, st, _ = cv2.connectedComponentsWithStats((a > 0.5).astype(np.uint8), 8)
    big = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    a = a * (cv2.dilate(big.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0)
    # shaded purple (the gap between the drawn arm and the body is darker purple): background too
    s = src.astype(np.float32)
    purple_hue = (s[..., 2] > s[..., 1] + 60) & (s[..., 0] > s[..., 1] + 30) & (s[..., 2] > 90)
    a = a * ~purple_hue
    rgb = cutlib.unmix_purple(src, a)
    # left-over purple: dark shaded background in closed gaps -> transparent; purple-tinged edge -> recoloured below
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    bg_like = (B > 0.95 * R) & (B > G + 30)
    fringe = (B > G + 15) & (B > 0.62 * R) & (R > G + 5) & ~bg_like
    a = a * ~bg_like
    px = np.concatenate([rgb, (a * 255)[..., None]], -1).astype(np.float32)
    good = (a > 0.98) & ~fringe
    px[..., :3] = np.where(fringe[..., None], cutlib.fill_rgb(px, good), px[..., :3])
    fig = a > 0.5
    rgb = px[..., :3]
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    dist = cv2.distanceTransform(fig.astype(np.uint8), cv2.DIST_L2, 5)
    rim = (R > 230) & (G > 195) & (R - B > 100) & (dist < 7.5) & (a > 0.02)
    px[rim, 3] = 0
    solid = px[..., 3] > 245
    px[..., :3] = np.where(solid[..., None], px[..., :3], cutlib.fill_rgb(px, solid))
    return src, px, fig, rgb


def shift_between(a, b, box):
    """Phase-correlation shift of b against a inside box (x0, y0, x1, y1)."""
    x0, y0, x1, y1 = box
    ga = cv2.cvtColor(a[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY).astype(np.float32)
    gb = cv2.cvtColor(b[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY).astype(np.float32)
    (dx, dy), resp = cv2.phaseCorrelate(ga, gb)
    return float(dx), float(dy), float(resp)


def main():
    src, px, fig, rgb = figure("cutj3_s123")
    H, Wd = fig.shape
    yy, xx = np.mgrid[0:H, 0:Wd].astype(np.float32)
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = (R > 160) & (G > 95) & (G < 185) & (B < 120) & (R - B > 85)
    out = {}
    geo = {k: P1[k] for k in ("S", "far_shoulder", "neck", "waist")}
    geo["knot"] = KNOT
    geo["torso_src"] = TORSO_SRC

    # ---- the near arm's region (as round 1: corridor along the drawn sleeve + the shoulder disc)
    S = np.array(P1["S"])
    a_src, b_src, UE = P1["a_src"], P1["b_src"], P1["UE"]
    d1 = np.array([np.cos(np.radians(a_src)), np.sin(np.radians(a_src))])
    d2 = np.array([np.cos(np.radians(a_src - b_src)), np.sin(np.radians(a_src - b_src))])
    E = S + d1 * UE
    bis = (d1 + d2) / np.linalg.norm(d1 + d2)
    lower_side = ((xx - E[0]) * bis[0] + (yy - E[1]) * bis[1]) > 0
    u = np.where(lower_side, UE + (xx - E[0]) * d2[0] + (yy - E[1]) * d2[1], (xx - S[0]) * d1[0] + (yy - S[1]) * d1[1])
    v = np.where(lower_side, (xx - E[0]) * -d2[1] + (yy - E[1]) * d2[0], (xx - S[0]) * -d1[1] + (yy - S[1]) * d1[0])
    torso_side = ~poly((H, Wd), [(0, 0)] + P1["torso_left"] + [(682, 790), (0, 790)])
    near_region = ((np.abs(v) < P1["sleeve_w"] / 2 + 8) & (u > 0) & (u < P1["u_cuff_end"] + 4)) | \
        (np.hypot(xx - S[0], yy - S[1]) < P1["sleeve_w"] / 2 + 4)
    arm_only = near_region & ~torso_side
    drawn_fist = poly((H, Wd), [(548, 768), (652, 768), (652, 888), (548, 888)])
    farm = poly((H, Wd), P1["far_poly"])

    # ---- belt: its top edge per column (dark brown band at the waist)
    belt = fig & (R < 115) & (G < 80) & (B < 60) & (R - B > 15) & (yy > 685) & (yy < 760) & (xx > 660) & (xx < 880)
    cols = np.arange(Wd)
    top = np.full(Wd, np.nan)
    for x in range(660, 880):
        ys = np.nonzero(belt[:, x])[0]
        if len(ys) > 3:
            top[x] = ys.min()
    ok = ~np.isnan(top)
    xs_ok = cols[ok]
    top = np.interp(cols, xs_ok, top[ok])
    k = np.ones(9) / 9
    top = np.convolve(np.pad(top, 4, mode="edge"), k, mode="valid")
    geo["belt_x"] = [int(xs_ok.min()), int(xs_ok.max())]
    geo["belt_top_mid"] = float(top[(xs_ok.min() + xs_ok.max()) // 2])
    belt_top = top[None, :]
    print("belt x", geo["belt_x"], "top at ends/mid", round(top[xs_ok.min()], 1), round(top[xs_ok.max()], 1),
          round(geo["belt_top_mid"], 1))

    # ---- hips: from the belt's top edge down (anti-aliased cut), without the near arm, the far arm, the drawn fist
    cut_a = np.clip(yy - (belt_top - 0.5), 0, 1)
    hips = fig & (yy > belt_top - 2) & ~(near_region & ~torso_side) & ~farm & ~drawn_fist & ~(near_region & (u > 150))
    hips_px = px.copy()
    hips_px[~hips] = 0
    hips_px[..., 3] *= cut_a
    out["hips"] = hips_px

    # ---- head (above the collar line) and the tails
    (x0, y0), (x1, y1) = P1["collar_line"]
    line_y = y0 + (xx - x0) * (y1 - y0) / (x1 - x0)
    head = fig & (yy < line_y) & ~arm_only
    redish = (R - G > 40) & (R - B > 30) & (R > 60)
    zone = (yy > 325) & (yy < 475) & (xx < TAIL_CUT_X)
    # the tails: the red cloth left of the knot as one smooth shape (closed, opened, blurred), not the hair or ear
    m = (zone & redish & fig).astype(np.uint8)
    ell = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, ell)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    m = (lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))).astype(np.float32)
    soft = cv2.GaussianBlur(m, (0, 0), 1.2)
    tails = (soft > 0.5) & zone
    tails_px = px.copy()
    tails_px[..., 3] *= np.clip((soft - 0.3) / 0.4, 0, 1) * zone
    tails_px[tails_px[..., 3] <= 0] = 0
    # a soft cut at the knot (the tails tuck under the knot on the head)
    tails_px[..., 3] *= np.clip((TAIL_CUT_X + 1 - xx) / 3, 0, 1)
    out["tails"] = tails_px
    head_px = px.copy()
    head_px[~head] = 0
    # behind the tails: hair colour where the hair ran under them, transparent elsewhere
    hair = fig & (R < 95) & (G < 75) & ~redish & (yy < 470)
    near_hair = cv2.dilate(hair.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15))) > 0
    near_hair = cv2.GaussianBlur(near_hair.astype(np.float32), (0, 0), 2.0) > 0.5
    hole = tails & head
    hair_rgb = np.median(rgb[hair & (yy > 380)], 0)
    head_px[hole & near_hair, :3] = hair_rgb
    head_px[hole & ~near_hair] = 0
    neck = poly((H, Wd), [(728, 452), (796, 452), (796, 505), (728, 505)]) & ~head
    neck_rgb = np.median(rgb[(yy > 440) & (yy < 455) & (xx > 740) & (xx < 785) & skin], 0)
    head_px[neck, :3] = neck_rgb
    head_px[neck, 3] = 255
    out["head"] = head_px

    # ---- far arm, with a round root of its own sleeve colour behind the torso
    far = fig & farm
    far_px = px.copy()
    far_px[~far] = 0
    fs = np.array(P1["far_shoulder"])
    far_cloth = np.median(rgb[far & (np.hypot(xx - fs[0], yy - fs[1]) < 60) & (R > 180)], 0)

    # ---- torso from the torso drawing (aligned to s123), carried on under the belt
    tsrc, tpx, tfig, trgb = figure(TORSO_SRC) if TORSO_SRC != "cutj3_s123" else (src, px, fig, rgb)
    if TORSO_SRC != "cutj3_s123":
        geo["align_head"] = shift_between(src, tsrc, (650, 280, 900, 470))
        geo["align_belt"] = shift_between(src, tsrc, (660, 690, 880, 760))
        geo["align_sleeve"] = shift_between(src, tsrc, (540, 560, 700, 800))
        print("alignment (dx, dy, response): head", geo["align_head"], "belt", geo["align_belt"], "sleeve",
              geo["align_sleeve"])
    tR, tG, tB = trgb[..., 0], trgb[..., 1], trgb[..., 2]
    tskin = (tR > 160) & (tG > 95) & (tG < 185) & (tB < 120) & (tR - tB > 85)
    torso = tfig & torso_side & (yy >= line_y) & (yy < belt_top + 2) & ~arm_only & ~farm & ~(tskin & (xx < 650))
    torso_px = tpx.copy()
    torso_px[~torso] = 0
    # under the belt: the torso's own colour from just above the belt, down 34 px (hidden by the hips piece)
    above = torso & (yy < belt_top - 3) & (yy > belt_top - 40)
    fillc = cutlib.fill_rgb(tpx, above)
    xs_t = np.nonzero(torso[int(geo["belt_top_mid"]) - 8])[0]
    under = (yy >= belt_top - 3) & (yy < belt_top + 34) & (xx >= xs_t.min() + 2) & (xx <= xs_t.max() - 2)
    torso_px[under, :3] = fillc[under]
    torso_px[under, 3] = 255
    under_far = poly((H, Wd), [(848, 510), (868, 530), (874, 700), (856, 712), (850, 600)]) & ~torso & \
        (yy < belt_top + 30)
    torso_px[under_far, :3] = fillc[under_far]
    torso_px[under_far, 3] = 255
    region = (~torso_side | poly((H, Wd), [(640, 470), (690, 470), (690, 610), (640, 610)])).astype(np.float32)
    region *= (yy > 485) & (yy < 600)
    torso_px = cutlib.line_on(torso_px, region, width=2.4)
    out["torso"] = torso_px

    # the vest's own drawn shadow tone (for the arm's cast shadow): its two darkest-to-lit cluster ratio
    # the armhole: a thin strip of the vest along its edge round the near shoulder, drawn OVER the sleeve's root
    # (the sleeve comes out from under the vest instead of a round end lying on it)
    ta = (torso_px[..., 3] > 128).astype(np.uint8)
    d_edge = cv2.distanceTransform(ta, cv2.DIST_L2, 5)
    Sx, Sy = P1["S"]
    rS = np.hypot(xx - Sx, yy - Sy)
    strip = (d_edge > 0) & (d_edge < ARMHOLE_W) & (rS < ARMHOLE_R) & (xx < Sx + 40) & (yy > Sy - 75)
    soft = cv2.GaussianBlur(strip.astype(np.float32), (0, 0), 1.0)
    arm_px = torso_px.copy()
    arm_px[..., 3] *= np.clip(np.minimum(np.clip((ARMHOLE_W + 0.5 - d_edge), 0, 1), np.clip(ARMHOLE_R - rS, 0, 1)
                                         ) * (strip | (soft > 0.3)), 0, 1)
    arm_px[arm_px[..., 3] <= 0] = 0
    out["armhole"] = arm_px
    # the far arm's round root, behind the torso and only where the torso hides it
    root = (np.hypot(xx - fs[0], yy - fs[1]) < 34) & ~far & (torso_px[..., 3] > 128)
    far_px[root, :3] = far_cloth
    far_px[root, 3] = 255
    out["far_arm"] = far_px
    vest = torso & (yy > 520) & (yy < belt_top - 6) & (xx > 700) & (xx < 840)
    cl = trgb[vest].astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 0.5)
    _, labs, cen = cv2.kmeans(cl, 3, None, crit, 4, cv2.KMEANS_PP_CENTERS)
    cnt = np.bincount(labs.ravel(), minlength=3) / len(cl)
    lit = cen[int(np.argmax(cnt))]
    darker = [i for i in range(3) if cen[i].sum() < lit.sum() - 25 and cnt[i] > 0.03]
    sh = cen[max(darker, key=lambda i: cen[i].sum())] if darker else lit * np.array([22, 44, 42]) / np.array([35, 58, 55])
    print("vest clusters", cen.round(), cnt.round(3))
    geo["vest_lit"], geo["vest_shadow"] = lit.tolist(), sh.tolist()
    geo["cast_ratio"] = np.clip(sh / np.maximum(lit, 1), 0.3, 1).tolist()
    print("vest lit", lit.round(), "shadow", sh.round(), "ratio", np.round(geo["cast_ratio"], 3))

    for k, im in out.items():
        Image.fromarray(np.clip(im + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / f"{k}.png")
    (REDO / "parts_r2.json").write_text(json.dumps(geo, indent=1))
    print({k: int((v[..., 3] > 128).sum()) for k, v in out.items()})


if __name__ == "__main__":
    main()
