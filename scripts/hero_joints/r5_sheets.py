"""Round 5 check sheets: the strike drawings (parts_r5) put together with rig_r2's painted arm and sword.
<venv python> -I -B r5_sheets.py
-> art/tests/hero_joints/r5/strike.png: game size, one scale, one feet line, on the Fen Road:
     (a) attempt 1's strike (round 2 parts, r4_keys frame 6, its whole-body tilt about the soles),
     (b) the new strike (parts_r5 + painted arm, fist, sword), (c) today's hero's strike drawing with its sword;
   under it 3x crops of (b): shoulder with the armhole, belt/waist seam, neck, fist with sword.
   art/tests/hero_joints/r5/flip.png: standing ready (round 2) | strike (round 5), same feet line, game size, and 2x
   crops of belt and neck in both.
Prints the numbers: arm pose, fist turn from its drawing (rho), tip, size checks (head, belt, boots, leg, torso)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import cv2
import numpy as np
from PIL import Image, ImageDraw

from jcommon import RAW, REDO, TESTS, fit

_argv, sys.argv = sys.argv, sys.argv[:1]
import rig_r2 as rig  # noqa: E402
import r2_stills  # noqa: E402
import swing_r2  # noqa: E402
sys.argv = _argv

OUT = TESTS / "r5"
OUT.mkdir(parents=True, exist_ok=True)
G5 = json.loads((REDO / "parts_r5.json").read_text())
G2 = json.loads((REDO / "parts_r2.json").read_text())
KK = 480 / 1028
FEET = 1283.0
WIN = (-300, -300, 2000, 1700)            # frame = canvas + 300
FIST_BLADE = -40.9                        # fist_r2's drawn blade angle
# ---- the new strike's arm (rig_r2 convention: y down, degrees clockwise; a1 = the upper arm's screen angle,
#      forearm = a1 - b, blade = forearm - turn). Solved so the tip lands on the feet line ahead of the front toe.
STRIKE = dict(a1=G5["a1_drawn"], b=0.0, blade=20.0, stretch=1.3)
STRIKE_ALT = dict(a1=G5["a1_drawn"], b=0.0, blade=25.0, stretch=1.0)   # bones at their own length (shown beside it)
# ---- attempt 1's strike: r4_keys frame 6 (contact). st = the whole body tilted about the soles (+ forward)
A1KEY = dict(a1=40.0, b=6.0, turn=-8.0, lean=22.0, head=-8.0, tail=6.0, dy=-1.0, far=9.0, fx=-20.0, fy=11.0,
             shoulder=(7.0, 3.0))
A1_ST, A1_HX = 8.0, 8.0
# ---- today's strike drawing: art/raw (edit px at the base's scale) onto hero 4's canvas, as r5_prep: K = 1029/1034,
#      its soles on the feet line; x by its figure's left edge onto r5_input's (310); game hand (1x px) * 2.154
K_OLD = 1029 / 1034
OLD_HAND = (331.7 * 1034 / 480, 388 * 1034 / 480)
OLD_BLADE = 115 - 90.0


class Parts5(rig.Parts):
    """Round 2's painted sleeve, shoulder, fist and sword; round 5's body pieces. The strike draws the far arm UNDER the
    tails (as drawn), so the two are passed in each other's slot: rig_r2 draws 'tails' first, then 'far_arm' (only
    valid with zero body moves, as here: both slots are then placed by the same plain offset)."""

    def __init__(self):
        super().__init__(REDO / "parts_r2")
        self.geo = dict(G5)
        d = REDO / "parts_r5"
        pc = G5["pieces"]
        slot = {"tails": pc["far_arm"], "far_arm": pc["tails"], "head": pc["head"], "torso": pc["torso"],
                "hips": pc["hips"], "armhole": pc["armhole"]}
        self.layers = {k: rig.crop_layer(rig.to_premul(Image.open(d / f"{v}.png"))) for k, v in slot.items()}
        self.cast_ratio = np.array(G5["cast_ratio"], np.float32)


_RIM = rig.rim_ring
_ARM_A = [None]


def rim_with_armhole(alpha):
    if _ARM_A[0] is not None:
        alpha = _ARM_A[0] + alpha * (1 - _ARM_A[0])
    return _RIM(alpha)


def strike_pose(k=None):
    k = k or STRIKE
    a1, b, st = k["a1"], k["b"], k["stretch"]
    _, F = rig.wrist_of(a1, b, st)
    return dict(a1=a1, b=b, turn=F - k["blade"], stretch=st)


def render_new(k=None):
    S_old, arm_old = rig.S0.copy(), rig.ARMHOLE
    rig.S0 = np.array(G5["S"], float)
    rig.ARMHOLE = True
    p = Parts5()
    fr = rig.Frame(p, WIN)
    pose = strike_pose(k)
    mats, _ = fr.body_mats(pose)
    _ARM_A[0] = rig.warp(*p.layers["armhole"], mats["armhole"], (fr.w, fr.h))[..., 3]
    rig.rim_ring = rim_with_armhole
    try:
        px, info = fr.render(pose)
    finally:
        rig.rim_ring, _ARM_A[0] = _RIM, None
        rig.S0, rig.ARMHOLE = S_old, arm_old
    return px, info, fr, pose


def render_old_attempt():
    p = rig.Parts()
    fr = rig.Frame(p, WIN)
    px, info = fr.render(dict(A1KEY))
    # the whole body tilted about the soles' middle (+ forward = clockwise on screen), then the hips' x shift
    c = (767 - fr.ox, FEET - fr.oy)
    M = cv2.getRotationMatrix2D(c, -A1_ST, 1.0)
    M[0, 2] += A1_HX
    px = cv2.warpAffine(px, M, (fr.w, fr.h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    return px, info, fr


def render_ready():
    p = rig.Parts()
    fr = rig.Frame(p, WIN)
    px, info = fr.render(r2_stills.pose_B(r2_stills.STILLS["ready"]))
    return px, info, fr


def render_today(fr, p):
    """Today's strike drawing (with its own rim) and the sword in its fist, premultiplied, on the frame."""
    im = Image.open(RAW / "char_hero_bare_sword_strike.png").convert("RGBA")
    a = np.asarray(im)[..., 3] >= 10
    ys, xs = np.nonzero(a)
    x0, y1 = xs.min(), ys.max()
    tx = 310.0 - x0 * K_OLD - fr.ox
    ty = FEET - y1 * K_OLD - fr.oy
    M = np.float32([[K_OLD, 0, tx], [0, K_OLD, ty]])
    body = cv2.warpAffine(rig.to_premul(im), M, (fr.w, fr.h), flags=cv2.INTER_AREA)
    grip = np.array([OLD_HAND[0] * K_OLD + tx, OLD_HAND[1] * K_OLD + ty])
    Ms = rig.rot_about(p.sword_grip, grip, OLD_BLADE + 90.0)
    sw = rig.warp(p.sword, (0, 0), Ms, (fr.w, fr.h))
    out = body.copy()
    rig.over(out, sw)
    # the fist back over the grip (skin within 50 px of the hand point)
    yy, xx = np.mgrid[0:fr.h, 0:fr.w].astype(np.float32)
    rgb = body[..., :3] / np.maximum(body[..., 3:4], 1e-4)
    skin = (rgb[..., 0] > 0.6) & (rgb[..., 1] > 0.35) & (rgb[..., 2] < 0.5) & (rgb[..., 0] - rgb[..., 2] > 0.3)
    near = (np.hypot(xx - grip[0], yy - grip[1]) < 50) & skin & (body[..., 3] > 0.5)
    out[near] = body[near]
    tip = grip + rig.vec(OLD_BLADE, p.tip_len)
    return out, grip + [fr.ox, fr.oy], tip + [fr.ox, fr.oy]


def on_bg(px, bg):
    out = px[..., :3] + bg * (1 - px[..., 3:4])
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))


def game(im, fr, x0=280, x1=1640, y0=300, y1=1330):
    """Canvas box -> game size."""
    c = im.crop((x0 - fr.ox, y0 - fr.oy, x1 - fr.ox, y1 - fr.oy))
    return c.resize((round(c.width * KK), round(c.height * KK)), Image.LANCZOS)


def zoom(im, fr, centre, half=70, k=3):
    x, y = centre[0] - fr.ox, centre[1] - fr.oy
    return im.crop((int(x - half), int(y - half), int(x + half), int(y + half))).resize((2 * half * k, 2 * half * k),
                                                                                     Image.NEAREST)


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 10 + 7 * len(text), 20], fill=(255, 255, 255))
    d.text((5, 4), text, fill=(0, 0, 0))
    return im


def feet_line(im, y):
    d = ImageDraw.Draw(im)
    for x in range(0, im.width, 12):
        d.line([(x, y), (x + 6, y)], fill=(255, 255, 0), width=1)
    return im


def row(tiles, gap=6):
    s = Image.new("RGB", (sum(t.width for t in tiles) + gap * (len(tiles) - 1), max(t.height for t in tiles)),
                  (255, 255, 255))
    x = 0
    for t in tiles:
        s.paste(t, (x, 0))
        x += t.width + gap
    return s


def col(tiles, gap=6):
    s = Image.new("RGB", (max(t.width for t in tiles), sum(t.height for t in tiles) + gap * (len(tiles) - 1)),
                  (255, 255, 255))
    y = 0
    for t in tiles:
        s.paste(t, (0, y))
        y += t.height + gap
    return s


# ---- size checks: the same measures on round 2's pieces and round 5's ----------------------------------------------
def piece(dirname, name):
    return np.asarray(Image.open(REDO / dirname / f"{name}.png").convert("RGBA")).astype(np.int32)


def brown(px):
    R, G, B, A = [px[..., i] for i in range(4)]
    return (A > 128) & (R < 120) & (G < 85) & (B < 60) & (R - B > 15) & (R > G)


def comps(mask, min_area=500):
    n, lab, st, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    return [lab == i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= min_area]


def belt_of(hips):
    """The belt: the brown component highest in the hips piece. Top-edge length (between its ends, along its slope)
    and its min-area box."""
    m = brown(hips)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
    cs = sorted(comps(m), key=lambda c: np.nonzero(c)[0].mean())
    b = cs[0]
    ys, xs = np.nonzero(b)
    cols = np.arange(xs.min() + 4, xs.max() - 3)
    tops = np.array([ys[xs == x].min() for x in cols])
    bots = np.array([ys[xs == x].max() for x in cols])
    a, _ = np.polyfit(cols, tops, 1)
    top_len = (xs.max() - xs.min()) * math.sqrt(1 + a * a)
    # thickness between the two edges (median over columns, square to the slope; the buckle's overhang is outvoted)
    thick = float(np.median(bots - tops)) / math.sqrt(1 + a * a)
    (_, _), (w, h), _ = cv2.minAreaRect(np.column_stack([xs, ys]).astype(np.float32))
    return dict(top_len=round(float(top_len), 1), thick=round(thick, 1), L=round(float(max(w, h)), 1), T=round(float(min(w, h)), 1),
                slope_deg=round(float(np.degrees(np.arctan(a))), 1), x=[int(xs.min()), int(xs.max())])


def boots_of(hips):
    """The two boots: brown components below y 1000; each one's sole length (its lowest 12 px rows' x extent) and
    its area."""
    m = brown(hips)
    m[:1000] = False
    out = []
    for c in sorted(comps(m, 3000), key=lambda c: np.nonzero(c)[1].min())[:2]:
        ys, xs = np.nonzero(c)
        low = ys >= ys.max() - 12
        out.append(dict(area=int(c.sum()), sole=int(xs[low].max() - xs[low].min()), width=int(xs.max() - xs.min()),
                        x=[int(xs.min()), int(xs.max())],
                        bottom=int(ys.max())))
    return out


def head_scale(h2, h5):
    """Round 2's head piece (hair, face, headband; no neck below y 470) matched onto round 5's over scale and turn."""
    def gray(px):
        a = px[..., 3:4] / 255.0
        return (px[..., :3] * a + 200 * (1 - a)).mean(-1).astype(np.float32)
    g2, g5 = gray(h2), gray(h5)
    tpl = g2[300:440, 680:880]          # the face (eyes, nose, mouth, ear); the hair outline is a hairstyle
    best = (-1, 0, 0, None)
    for ang in range(-30, 31, 2):
        M = cv2.getRotationMatrix2D((tpl.shape[1] / 2, tpl.shape[0] / 2), ang, 1.0)
        tr = cv2.warpAffine(tpl, M, tpl.shape[::-1], borderValue=200)
        for k in np.arange(0.85, 1.201, 0.01):
            t = cv2.resize(tr, None, fx=k, fy=k, interpolation=cv2.INTER_AREA)
            r = cv2.matchTemplate(g5, t, cv2.TM_CCOEFF_NORMED)
            _, mx, _, loc = cv2.minMaxLoc(r)
            if mx > best[0]:
                best = (mx, k, ang, loc)
    return dict(score=round(float(best[0]), 3), scale=round(float(best[1]), 3), turn=best[2])


def sizes():
    h2, h5 = piece("parts_r2", "hips"), piece("parts_r5", G5["pieces"]["hips"])
    b2, b5 = belt_of(h2), belt_of(h5)
    bo2, bo5 = boots_of(h2), boots_of(h5)
    hd = head_scale(piece("parts_r2", "head"), piece("parts_r5", G5["pieces"]["head"]))
    w2, w5 = np.array(G2["waist"]), np.array(G5["waist"])
    n2, n5 = np.array(G2["neck"]), np.array(G5["neck"])
    # leg: the belt's centre to the far end of the back boot's sole (the stretched back leg in the strike)
    back2 = np.array([bo2[0]["x"][0], FEET])
    back5 = np.array([bo5[0]["x"][0], FEET])
    res = {
        "head_scale_r5_vs_r2": hd,
        "belt_r2": b2, "belt_r5": b5,
        "belt_top_len_ratio": round(b5["top_len"] / b2["top_len"], 3),
        "belt_thickness_ratio": round(b5["thick"] / b2["thick"], 3),
        "boots_r2": bo2, "boots_r5": bo5,
        "boot_sole_ratio_back_front": [round(bo5[i]["sole"] / bo2[i]["sole"], 3) for i in range(2)],
        "boot_width_ratio_back_front": [round(bo5[i]["width"] / bo2[i]["width"], 3) for i in range(2)],
        "boot_size_ratio_back_front": [round(math.sqrt(bo5[i]["area"] / bo2[i]["area"]), 3) for i in range(2)],
        "torso_neck_to_waist": [round(float(np.linalg.norm(n2 - w2)), 1), round(float(np.linalg.norm(n5 - w5)), 1)],
        "back_leg_waist_to_back_sole": [round(float(np.linalg.norm(back2 - w2)), 1),
                                        round(float(np.linalg.norm(back5 - w5)), 1)],
    }
    res["torso_ratio"] = round(res["torso_neck_to_waist"][1] / res["torso_neck_to_waist"][0], 3)
    res["back_leg_ratio"] = round(res["back_leg_waist_to_back_sole"][1] / res["back_leg_waist_to_back_sole"][0], 3)
    # standing height proxy: head top to neck + neck to waist + waist to sole along the back leg
    top2 = np.nonzero((piece("parts_r2", "head")[..., 3] > 128).any(1))[0].min()
    top5 = np.nonzero((piece("parts_r5", G5["pieces"]["head"])[..., 3] > 128).any(1))[0].min()
    hn2, hn5 = n2[1] - top2, (n5[1] - top5)
    res["height_proxy_r2_r5"] = [round(float(hn2 + res["torso_neck_to_waist"][0] + res["back_leg_waist_to_back_sole"][0]), 1),
                                 round(float(hn5 + res["torso_neck_to_waist"][1] + res["back_leg_waist_to_back_sole"][1]), 1)]
    res["height_proxy_ratio"] = round(res["height_proxy_r2_r5"][1] / res["height_proxy_r2_r5"][0], 3)
    # where the hips land against the standing hips, by anchor: as drawn (figure centred, as the game swaps a pose),
    # the front boot's toe kept where it stands, the back boot's rear end kept where the standing back heel is
    off = w5 - w2
    toe = bo2[1]["x"][1] - bo5[1]["x"][1]
    back = bo2[0]["x"][0] - bo5[0]["x"][0]
    res["waist_offset"] = {"as_drawn": [round(float(off[0]), 1), round(float(off[1]), 1)],
                           "front_toe_planted": [round(float(off[0] + toe), 1), round(float(off[1]), 1)],
                           "back_foot_planted": [round(float(off[0] + back), 1), round(float(off[1]), 1)],
                           "front_toe_x_r2_r5": [bo2[1]["x"][1], bo5[1]["x"][1]],
                           "back_boot_rear_x_r2_r5": [bo2[0]["x"][0], bo5[0]["x"][0]]}
    return res


def main():
    px_b, inf_b, fr, pose = render_new()
    bg = swing_r2.background(fr)
    rho = (inf_b["blade"] - FIST_BLADE + 180) % 360 - 180
    num = {"strike_pose": {k: round(float(v), 2) for k, v in pose.items()},
           "blade": round(float(inf_b["blade"]), 1), "rho_fist_turn_from_drawing": round(float(rho), 1),
           "wrist_bend_from_drawing": round(float(inf_b["wrist_bend"]), 1),
           "wrist": (inf_b["W"] + [fr.ox, fr.oy]).round(1).tolist(), "grip": (inf_b["grip"] + [fr.ox, fr.oy]).round(1).tolist(),
           "tip": (inf_b["tip"] + [fr.ox, fr.oy]).round(1).tolist(), "flipped_triangles": int(inf_b["flipped"])}
    print("strike arm:", json.dumps(num))
    px_a, inf_a, _ = render_old_attempt()
    p2 = rig.Parts()
    px_c, grip_c, tip_c = render_today(fr, p2)
    print("today's drawing: grip", grip_c.round(1), "tip", tip_c.round(1))
    im_a, im_b, im_c = on_bg(px_a, bg), on_bg(px_b, bg), on_bg(px_c, bg)
    fy = (FEET - 300) * KK
    top = row([feet_line(label(game(im_a, fr), "(a) attempt 1's strike"), fy),
               feet_line(label(game(im_b, fr), "(b) new strike   fist turned %.1f deg from rig_r2 (limit 90)   blade %.1f" % (rho, inf_b["blade"])), fy),
               feet_line(label(game(im_c, fr), "(c) today's hero"), fy)])
    S = np.array(G5["S"])
    waist, neck = np.array(G5["waist"]), np.array(G5["neck"])
    wrist = np.array(num["wrist"])
    d_arm = rig.vec(pose["a1"])
    cut_pt = S + d_arm * (G5["u_cut"] - (np.linalg.norm(np.array(G5["S"]) - np.array(G5["arm_top"]))))
    zs = row([label(zoom(im_b, fr, cut_pt + [-10, -25], 80), "(b) shoulder + armhole cut 3x"),
              label(zoom(im_b, fr, waist + [0, -15]), "(b) belt / waist 3x"),
              label(zoom(im_b, fr, neck + [0, -10]), "(b) neck 3x"),
              label(zoom(im_b, fr, wrist + [10, 20]), "(b) fist + sword 3x")])
    zs = zs.resize((top.width, round(zs.height * top.width / zs.width)), Image.LANCZOS) if zs.width > top.width else zs
    fit(col([top, zs]), 2000, 3.2).save(OUT / "strike.png")
    # the arm at the bones' own length (STRIKE_ALT) beside the stretched one and today's hero
    px_b1, inf_b1, _, pose1 = render_new(STRIKE_ALT)
    im_b1 = on_bg(px_b1, bg)
    print("alt arm (stretch %.2f): blade %.1f rho %.1f wrist %s tip %s" % (
        pose1["stretch"], inf_b1["blade"], (inf_b1["blade"] - FIST_BLADE + 180) % 360 - 180,
        (inf_b1["W"] + [fr.ox, fr.oy]).round(1), (inf_b1["tip"] + [fr.ox, fr.oy]).round(1)))
    fit(row([feet_line(label(game(im_b1, fr), "arm at bone length (L 258.5)"), fy),
             feet_line(label(game(im_b, fr), "arm stretched x%.2f" % (rig.stretched_len(pose["stretch"]) / rig.L)), fy),
             feet_line(label(game(im_c, fr), "today's hero"), fy)]), 2000, 2.0).save(OUT / "strike_arm_options.png")
    # 1x full-resolution copy of (b) for close looks
    im_b.crop((250 - fr.ox, 350 - fr.oy, 1650 - fr.ox, 1330 - fr.oy)).save(OUT / "strike_b_1x.png")

    # ---- flip: ready (round 2) | strike (round 5), same place
    px_r, inf_r, frr = render_ready()
    im_r = on_bg(px_r, bg)
    w2, n2 = np.array(G2["waist"]), np.array(G2["neck"])
    flip = col([row([feet_line(label(game(im_r, frr), "standing ready (round 2)"), fy),
                     feet_line(label(game(im_b, fr), "strike (round 5)   fist turn %.1f deg (limit 90)" % rho), fy)]),
                row([label(zoom(im_r, frr, w2, 110, 2), "ready belt 2x"), label(zoom(im_b, fr, waist, 110, 2), "strike belt 2x")]),
                row([label(zoom(im_r, frr, n2 + [0, -30], 110, 2), "ready neck 2x"),
                     label(zoom(im_b, fr, neck + [0, -30], 110, 2), "strike neck 2x")])])
    sz = sizes()
    d = ImageDraw.Draw(flip)
    lines = ["head %.2f (turn %d)   belt top %.2f   belt thick %.2f   boots sole %s   boots size %s" % (
        sz["head_scale_r5_vs_r2"]["scale"], sz["head_scale_r5_vs_r2"]["turn"], sz["belt_top_len_ratio"],
        sz["belt_thickness_ratio"], sz["boot_sole_ratio_back_front"], sz["boot_size_ratio_back_front"]),
        "height proxy %.2f   torso neck-waist %.2f   back leg %.2f   boots width %s   arm (painted, stretched) / L %.2f" % (
        sz["height_proxy_ratio"], sz["torso_ratio"], sz["back_leg_ratio"], sz["boot_width_ratio_back_front"],
        rig.stretched_len(pose["stretch"]) / rig.L),
        "waist offset from standing: as drawn %s | front toe planted %s | back foot planted %s" % (
        sz["waist_offset"]["as_drawn"], sz["waist_offset"]["front_toe_planted"], sz["waist_offset"]["back_foot_planted"])]
    d.rectangle([0, flip.height - 62, flip.width, flip.height], fill=(255, 255, 255))
    for i, ln in enumerate(lines):
        d.text((6, flip.height - 58 + 18 * i), ln, fill=(0, 0, 0))
    fit(flip, 1800, 3.0).save(OUT / "flip.png")
    print("sizes:", json.dumps(sz, indent=1))
    (OUT / "numbers.json").write_text(json.dumps({"arm": num, "sizes": sz}, indent=1))
    print("saved", OUT / "strike.png", OUT / "flip.png")


if __name__ == "__main__":
    main()
