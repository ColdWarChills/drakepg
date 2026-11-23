"""Cut the picked drawing (cutj3_s123) into the parts the arm test needs.
<venv python> -I -B cut.py [drawing]  -> art/redo/hero_joints/parts/*.png (full 1536 canvas each) + parts.json

Points read by eye on grid crops of cutj3_s123 (gridlook.py; _s123_upper.png, _arm3.png):
  near shoulder pivot S (652,540) (shoulder top at y ~505, half a sleeve width above), near wrist W (601,795) inside the
  cuff (cuff bottom on the axis at u ~267), sleeve width 72 (measure.py), far shoulder (855,525), neck (770,478),
  waist (772,722). Head cut: the collar top, a line (690,458)-(815,478). Torso's left edge where the near arm met it:
  (700,478)-(672,505)-(667,545)-(676,600)-(680,705). Far arm: right of the drawn line (856..866, 536..700).
"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image

from jcommon import REDO, ROOT

sys.path.insert(0, str(ROOT / "scripts" / "hero2d"))
import cutlib  # noqa: E402  (read-only use of the hero2d helpers)

NAME = sys.argv[1] if len(sys.argv) > 1 else "cutj3_s123"
OUT = REDO / "parts"
OUT.mkdir(exist_ok=True)

P = {
    "S": (660.0, 543.0), "a_src": 110.8, "b_src": 13.0, "UE": 134.0, "L": 258.5, "sleeve_w": 72.0, "u_cuff_end": 269.0,
    "far_shoulder": (855.0, 525.0), "neck": (770.0, 478.0), "waist": (772.0, 722.0),
    "collar_line": [(690, 458), (815, 478)],
    "torso_left": [(700, 478), (672, 505), (667, 545), (676, 600), (680, 705)],
    "far_poly": [(845, 505), (990, 430), (1180, 430), (1180, 780), (866, 780), (866, 700), (862, 640), (858, 560),
                 (850, 530)],
    "belt_top": 706, "torso_bottom": 728, "hips_top": 703,
}


def poly(shape, pts):
    m = np.zeros(shape, np.uint8)
    cv2.fillPoly(m, [np.round(np.array(pts)).astype(np.int32)], 1)
    return m > 0


def main():
    src = np.asarray(Image.open(REDO / f"{NAME}.png").convert("RGB"))
    # alpha from the distance to the purple field (soft edge); no hole filling: the gap between the near arm and the
    # body is enclosed and must stay background
    dpu = np.abs(src.astype(np.float32) - np.array(cutlib.PURPLE)).max(-1)
    a = np.clip((dpu - 40) / 50, 0, 1)
    nlab, lab, st, _ = cv2.connectedComponentsWithStats((a > 0.5).astype(np.uint8), 8)
    big = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    a = a * (cv2.dilate(big.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0)
    rgb = cutlib.unmix_purple(src, a)
    A = a * 255
    px = np.concatenate([rgb, A[..., None]], -1).astype(np.float32)
    H, Wd = a.shape
    fig = a > 0.5
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # drawn rim (warm, saturated pale) within 7 px of the outside: taken off (code adds its own rim)
    dist = cv2.distanceTransform(fig.astype(np.uint8), cv2.DIST_L2, 5)
    rim = (R > 230) & (G > 195) & (R - B > 100) & (dist < 7.5) & (a > 0.02)
    px[rim, 3] = 0
    # soft edge pixels take the colour of the nearest solid pixel (no purple fringe; the alpha stays soft)
    solid = px[..., 3] > 245
    px[..., :3] = np.where(solid[..., None], px[..., :3], cutlib.fill_rgb(px, solid))
    print("rim px taken off", int(rim.sum()))
    skin = (R > 160) & (G > 95) & (G < 185) & (B < 120) & (R - B > 85)
    xx, yy = cutlib.grid((H, Wd))

    # ---- near sleeve: the drawn sleeve's own centre line (_axis.py: section middles) is two straight parts with a
    # gentle drawn bend: from S at a_src, bending by b_src at u = UE, to the wrist W at u = L
    S = np.array(P["S"])
    a_src, b_src, UE, L = P["a_src"], P["b_src"], P["UE"], P["L"]
    d1 = np.array([np.cos(np.radians(a_src)), np.sin(np.radians(a_src))])
    d2 = np.array([np.cos(np.radians(a_src - b_src)), np.sin(np.radians(a_src - b_src))])
    E = S + d1 * UE
    P["E"] = E.tolist()
    P["W"] = (E + d2 * (L - UE)).tolist()
    bis = (d1 + d2) / np.linalg.norm(d1 + d2)
    lower_side = ((xx - E[0]) * bis[0] + (yy - E[1]) * bis[1]) > 0
    u_up = (xx - S[0]) * d1[0] + (yy - S[1]) * d1[1]
    v_up = (xx - S[0]) * -d1[1] + (yy - S[1]) * d1[0]
    u_lo = UE + (xx - E[0]) * d2[0] + (yy - E[1]) * d2[1]
    v_lo = (xx - E[0]) * -d2[1] + (yy - E[1]) * d2[0]
    u = np.where(lower_side, u_lo, u_up)
    v = np.where(lower_side, v_lo, v_up)
    w = P["sleeve_w"]
    corridor = (np.abs(v) < w / 2 + 8) & (u > 0) & (u < P["u_cuff_end"])
    disc = np.hypot(xx - S[0], yy - S[1]) < w / 2 + 4
    torso_side = ~poly((H, Wd), [(0, 0)] + P["torso_left"] + [(682, 790), (0, 790)])  # right of the torso's left edge
    # the inner (torso-side, v < 0) edge: above the armpit the drawing runs on into the torso, so there the piece is
    # cut on the free part's inner edge line, with a drawn line on it
    ins = []
    for uu in np.arange(110, 230, 5.0):
        dd = d1 if uu <= UE else d2
        c = (S + d1 * uu) if uu <= UE else (E + d2 * (uu - UE))
        l0, l1, _ = cutlib.section(fig & ~skin, c, dd)
        ins.append(l0)  # extent along -n (v < 0 side), negative
    v_in = float(np.median(ins))
    P["v_inner"] = v_in
    near = fig & (corridor | disc) & ~(skin & (u > 200)) & (v > v_in - 1.5)
    near_px = px.copy()
    near_px[~near] = 0
    near_px[..., 3] *= np.clip((v - v_in + 1.5) / 1.5, 0, 1)
    cutline = ((v < v_in + 3) & (u < 105)).astype(np.float32)
    near_px = cutlib.line_on(near_px, cutline, width=2.0)
    # A clean tube: the drawn sleeve steps out on its outer edge at the elbow (a notch once straightened or bent).
    # Its two edges are measured along u (sections across the centre line), smoothed (the sleeve narrows smoothly
    # toward the cuff), and the piece is re-cut between them with the drawn colours; the edge line goes back on.
    us_m = np.arange(40.0, P["u_cuff_end"] - 4, 4.0)
    vo, vi = [], []
    for uu in us_m:
        dd = d1 if uu <= UE else d2
        c = (S + d1 * uu) if uu <= UE else (E + d2 * (uu - UE))
        l0, l1, _ = cutlib.section(fig & ~skin, c, dd)
        vo.append(l1)
        vi.append(l0)
    vo, vi = np.array(vo, float), np.array(vi, float)
    vi = np.maximum(vi, v_in)  # the straight cut above the armpit
    # a straight line fit per edge (least squares) is the plainest tube; the drawn sleeve narrows ~8 px to the cuff
    co = np.polyfit(us_m, vo, 1)
    ci = np.polyfit(us_m[us_m > 110], vi[us_m > 110], 1)
    P["edge_fit_outer"], P["edge_fit_inner"] = [float(c) for c in co], [float(c) for c in ci]
    P["edge_drawn_max_dev"] = [float(np.abs(np.polyval(co, us_m) - vo).max()),
                               float(np.abs(np.polyval(ci, us_m) - vi)[us_m > 110].max())]
    vout = np.polyval(co, u)
    vinn = np.maximum(np.polyval(ci, u), v_in)
    tube = (u > 0) & (u < P["u_cuff_end"])
    a_t = np.clip(vout - v + 0.5, 0, 1) * np.clip(v - vinn + 0.5, 0, 1)
    keep_top = near & (u <= 0)  # the drawn shoulder cap stays as drawn
    new_a = np.where(tube, a_t * 255, np.where(keep_top, near_px[..., 3], 0))
    # the tube's cuff end stays as drawn (its own outline)
    cuffz = (u > P["u_cuff_end"] - 14) & near
    new_a = np.where(cuffz, np.minimum(new_a, px[..., 3]), new_a)
    solid_n = near & (px[..., 3] > 245) & (np.abs(v - (vout + vinn) / 2) < (vout - vinn) / 2 - 4)
    rgbn = cutlib.fill_rgb(px, solid_n)
    near_px = np.concatenate([np.where(solid_n[..., None], px[..., :3], rgbn), new_a[..., None]], -1)
    lit0 = near & (u > 60) & (u < 240) & (np.abs(v) < w / 4)
    cloth = np.median(rgb[lit0], 0)
    near_px = cutlib.flatten(near_px, E, 30.0, 85.0, cloth)
    # the edge line, drawn on the smooth edges themselves (anti-aliased): the local colour darkened, 1.8 px
    lw = 2.2
    band = (np.clip(lw + 0.5 - (vout - v), 0, 1) + np.clip(lw + 0.5 - (v - vinn), 0, 1)).clip(0, 1)
    band *= tube & (u < P["u_cuff_end"] - 10)
    near_px[..., :3] = near_px[..., :3] * (1 - 0.42 * band[..., None])
    near = near_px[..., 3] > 128
    # pixels of the near piece that lie on the torso side and above the gap stay in the torso too (same cloth)
    arm_only = near & ~torso_side

    # ---- head: above the collar line
    (x0, y0), (x1, y1) = P["collar_line"]
    line_y = y0 + (xx - x0) * (y1 - y0) / (x1 - x0)
    head = fig & (yy < line_y) & ~near
    head_px = px.copy()
    head_px[~head] = 0
    # neck under the collar: skin colour, so a small turn of the head opens no gap
    neck = poly((H, Wd), [(728, 452), (796, 452), (796, 505), (728, 505)]) & ~head
    neck_rgb = np.median(rgb[(yy > 440) & (yy < 455) & (xx > 740) & (xx < 785) & skin], 0)
    head_px[neck, :3] = neck_rgb
    head_px[neck, 3] = 255

    # ---- far arm
    farm = fig & poly((H, Wd), P["far_poly"])
    far_px = px.copy()
    far_px[~farm] = 0

    # ---- torso: below the collar line, above the belt bottom; near-arm-only and far arm out
    torso = fig & torso_side & (yy >= line_y) & (yy < P["torso_bottom"]) & ~arm_only & ~farm & ~(skin & (xx < 650))
    torso_px = px.copy()
    torso_px[~torso] = 0
    # the torso under the belt and under the far arm's inner edge: cream (nearest torso cloth colour)
    cloth = torso & ~((R < 110) & (G < 80)) & (yy < P["belt_top"] - 4)
    fillc = cutlib.fill_rgb(px, cloth)
    under_belt = poly((H, Wd), [(688, P["belt_top"] - 6), (856, P["belt_top"] - 6), (856, P["torso_bottom"]),
                                (688, P["torso_bottom"])]) & fig
    torso_px[under_belt, :3] = fillc[under_belt]
    torso_px[under_belt, 3] = 255
    under_far = poly((H, Wd), [(848, 510), (868, 530), (874, 700), (856, 712), (850, 600)]) & ~torso & (yy < 712)
    torso_px[under_far, :3] = fillc[under_far]
    torso_px[under_far, 3] = 255
    # where the near arm was cut away from the shoulder: a drawn edge line on the torso
    region = (~torso_side | poly((H, Wd), [(640, 470), (690, 470), (690, 610), (640, 610)])).astype(np.float32)
    region *= (yy > 485) & (yy < 600)
    torso_px = cutlib.line_on(torso_px, region, width=2.4)

    # ---- hips: the belt and below, not the near arm or fist
    drawn_fist = poly((H, Wd), [(548, 768), (652, 768), (652, 888), (548, 888)])  # the drawing's own near fist
    hips = fig & (yy >= P["hips_top"]) & ~near & ~farm & ~corridor & ~drawn_fist
    hips_px = px.copy()
    hips_px[~hips] = 0

    parts = {"near_sleeve": near_px, "head": head_px, "far_arm": far_px, "torso": torso_px, "hips": hips_px}
    for k, im in parts.items():
        Image.fromarray(np.clip(im + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / f"{k}.png")
    # flat cloth colour of the sleeve (for kind A's round ends): the median lit sleeve colour
    lit = near & (u > 60) & (u < 240) & (np.abs(v) < w / 4)
    P["cloth_rgb"] = [float(c) for c in np.median(rgb[lit], 0)]
    P["drawing"] = NAME
    (REDO / "parts.json").write_text(json.dumps(P, indent=1))
    print({k: int((v[..., 3] > 128).sum()) for k, v in parts.items()}, "v_inner", v_in, "cloth", P["cloth_rgb"])


if __name__ == "__main__":
    main()
