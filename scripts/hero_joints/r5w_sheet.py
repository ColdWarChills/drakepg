"""Round 5 fix (the wind-up) check sheet: the coiled lower body (parts_r5/hips_wind) under round 4's turned torso, round
2's head and tails, with rig_r2's painted arm, fist and sword on bones at their own length (no stretch).
<venv python> -I -B r5w_sheet.py [sweep]
  sweep -> art/tests/hero_joints/r5/windup_sweep.png (forearm angle candidates at game size and fist 3x)
  (none) -> art/tests/hero_joints/r5/windup.png:
     top: (a) hero 4's last wind-up hold (frames_new take 8) | (b) the new wind-up still | (c) today's wind drawing with
          its sword (as the battle holds it before its tilt) | (d) today's hero in the battle (frames_old take 8);
          (b) and (c) are rendered at one scale and one feet line; (a) and (d) are the battle captures scaled by CAP_K
          (measured on hero 4's standing belt-to-sole).
     second row: (b) turned about the planted back heel by TILTS (as the battle tilts today's hero; the blade keeps its
          screen angle), (b) with the front toe planted instead, and (d) again.
     middle: 3x crops of (b): sword hand, elbow, shoulder + armhole, belt / waist seam.
     bottom: ready (round 2) | wind-up, same feet line, the planted foot marked, the boots' moves written.
  -> art/tests/hero_joints/r5/windup_numbers.json (the arm in r5_keys' two-bone convention = Unity's, rig_r2's
     curved equivalent, rho, the face check from r5_keys.geo, the waist offsets).
Arm convention (r5_keys / rig_r2): y down, degrees clockwise; a1 = the upper arm's screen angle, forearm F = a1 - b,
blade = F - turn; rho = blade - FIST_BLADE (-40.9) is the fist's turn from its drawing (limit 90)."""
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
import r5_keys  # noqa: E402  (geo() and the face box only; its main() writes keys.json under unity/ and is never called)
sys.argv = _argv

OUT = TESTS / "r5"
G2 = json.loads((REDO / "parts_r2.json").read_text())
G4 = json.loads((REDO / "parts_r4.json").read_text())
G5 = json.loads((REDO / "parts_r5.json").read_text())
GW = G5["wind"]
KK = 480 / 1028
FEET = 1283.0
WIN = (-300, -300, 2000, 1700)
FIST_BLADE = -40.9
WAIST2 = np.array(G2["waist"], float)
WAISTW = np.array(GW["waist"], float)
PLANT = "back_foot_planted"
OFF = np.array(GW["waist_offset_from_standing"][PLANT], float)     # the wind waist from the standing waist
# ---- the body moves (r5_keys WIND, without st/hx: the drawn legs carry the stance). dy 11: the torso sinks 11 px into
#      the belt, so the left half of the old buckle that parts_r4/torso carries under its belt line (x 797-815, from
#      y 700) stays under the thinner drawn belt (its top is at y ~710 there, standing frame)
BODY = dict(lean=-5.0, head=3.0, tail=-10.0, dy=11.0, far=26.0, fx=6.0, fy=-3.0)
TAG = ""
if len(sys.argv) > 2 and sys.argv[1] == "lean":          # r5w_sheet.py lean <deg>: a test of another torso lean
    BODY["lean"], TAG = float(sys.argv[2]), "_lean%s" % sys.argv[2]
# ---- the arm, Unity's two straight bones: a1 on the turned torso's drawn root (a1_drawn + lean), the forearm F, blade
A1_U = G4["a1_drawn"] + BODY["lean"]
F_U = -60.0
BLADE = -124.0                       # today's blade on screen at its wind-up hold in the battle (frames_old take 8)
SWEEP = [(-40.0, -124.0), (-60.0, -124.0), (-80.0, -124.0), (-60.0, -116.0)]
UE, L = r5_keys.UE, r5_keys.L
OLD_K = 1029 / 1034
OLD_HAND = (137.5 * 1034 / 480, 74.3 * 1034 / 480)   # classes.ts sword_wind right hand (1x px) on the raw art
OLD_BLADE = 0 - 90.0 - 14.0          # game angle 0 -> rig -90, then the battle's Cock(14) for drawn poses


# ---- pieces ------------------------------------------------------------------------------------------------------------
class PartsW(rig.Parts):
    """Round 4's turned torso, armhole and far arm, round 2's head and tails (parts_r4 carries both), and the drawn
    coiled hips, moved so their waist sits on the standing waist (the frame is then moved by the waist offset)."""

    def __init__(self):
        super().__init__(REDO / "parts_r4")
        px, org = rig.crop_layer(rig.to_premul(Image.open(REDO / "parts_r5" / "hips_wind.png")))
        d = WAIST2 - WAISTW
        self.layers["hips"] = (px, (org[0] + d[0], org[1] + d[1]))


_RIM = rig.rim_ring
_ARM_A = [None]


def rim_with_armhole(alpha):
    if _ARM_A[0] is not None:
        alpha = _ARM_A[0] + alpha * (1 - _ARM_A[0])
    return _RIM(alpha)


def two_bone(a1, F):
    """Unity's arm: shoulder-relative elbow and wrist of two straight bones."""
    E = rig.vec(a1, UE)
    return E, E + rig.vec(F, L - UE)


def wind_pose(F=F_U, blade=BLADE):
    """rig_r2's pose with Unity's own a1 and b (the upper sleeve on the upper bone, so on the drawn armhole, as Sprite
    Skin draws it); rig_r2's rounded elbow puts its wrist ~16 px from the two bones' (reported), same blade."""
    b_u = A1_U - F
    p = dict(BODY)
    p["a1"], p["b"] = A1_U, b_u
    p["stretch"] = 1.0
    _, Fr = rig.wrist_of(p["a1"], p["b"], 1.0)
    p["turn"] = Fr - blade
    return p, dict(a1=A1_U, b=b_u, F=F, blade=blade, turn=F - blade)


def frame_for(p, off=None):
    """The frame moved so the picture lands at the wind waist offset (canvas px = image px + WIN origin)."""
    off = OFF if off is None else off
    return rig.Frame(p, (WIN[0] - off[0], WIN[1] - off[1], WIN[2], WIN[3]))


def render_wind(F=F_U, blade=BLADE, off=None):
    S_old = rig.S0.copy()
    rig.S0 = np.array(G4["S"], float)
    rig.ARMHOLE = True
    rig.SHADE_SIDE = -1.0      # the band under the raised arm, as the drawn root has it (r4_turned; Unity: sleeve "under")
    p = PartsW()
    fr = frame_for(p, off)
    pose, unity = wind_pose(F, blade)
    mats, _ = fr.body_mats(pose)
    _ARM_A[0] = rig.warp(*p.layers["armhole"], mats["armhole"], (fr.w, fr.h))[..., 3]
    rig.rim_ring = rim_with_armhole
    try:
        px, info = fr.render(pose)
    finally:
        rig.rim_ring, _ARM_A[0] = _RIM, None
        rig.S0, rig.ARMHOLE, rig.SHADE_SIDE = S_old, False, 1.0
    return px, info, fr, pose, unity


def render_ready():
    p = rig.Parts()
    fr = rig.Frame(p, WIN)
    return fr.render(r2_stills.pose_B(r2_stills.STILLS["ready"]))[0], fr


def render_today(fr, p):
    """Today's wind drawing (its own rim) with the sword in its fist, at hero 4's scale and feet line; x: its figure's
    left edge on r5w_input's (as r5_sheets places the strike)."""
    im = Image.open(RAW / "char_hero_bare_sword_wind.png").convert("RGBA")
    a = np.asarray(im)[..., 3] >= 10
    ys, xs = np.nonzero(a)
    x0, y1 = xs.min(), ys.max()
    inp = np.asarray(Image.open(REDO / "r5w_input.png").convert("RGB")).astype(np.int32)
    fig = np.abs(inp - np.array([152, 51, 201])).max(-1) > 40
    ix0 = np.nonzero(fig.any(0))[0].min()
    tx = ix0 - x0 * OLD_K - WIN[0]
    ty = FEET - y1 * OLD_K - WIN[1]
    M = np.float32([[OLD_K, 0, tx], [0, OLD_K, ty]])
    body = cv2.warpAffine(rig.to_premul(im), M, (fr.w, fr.h), flags=cv2.INTER_AREA)
    grip = np.array([OLD_HAND[0] * OLD_K + tx, OLD_HAND[1] * OLD_K + ty])
    sw = rig.warp(p.sword, (0, 0), rig.rot_about(p.sword_grip, grip, OLD_BLADE + 90.0), (fr.w, fr.h))
    out = body.copy()
    rig.over(sw, np.zeros_like(sw))
    out = sw + body * (1 - sw[..., 3:4])
    yy, xx = np.mgrid[0:fr.h, 0:fr.w].astype(np.float32)
    rgb = body[..., :3] / np.maximum(body[..., 3:4], 1e-4)
    skin = (rgb[..., 0] > 0.6) & (rgb[..., 1] > 0.35) & (rgb[..., 2] < 0.5) & (rgb[..., 0] - rgb[..., 2] > 0.3)
    near = (np.hypot(xx - grip[0], yy - grip[1]) < 50) & skin & (body[..., 3] > 0.5)
    out[near] = body[near]
    return out


# ---- sheet helpers -------------------------------------------------------------------------------------------------
def on_bg(px, bg):
    return Image.fromarray((np.clip(px[..., :3] + bg * (1 - px[..., 3:4]), 0, 1) * 255 + 0.5).astype(np.uint8))


def game(im, x0=120, x1=1460, y0=-20, y1=1330):
    """Canvas box -> game size (image px = canvas - WIN origin)."""
    c = im.crop((x0 - WIN[0], y0 - WIN[1], x1 - WIN[0], y1 - WIN[1]))
    return c.resize((round(c.width * KK), round(c.height * KK)), Image.LANCZOS)


def zoom(im, centre, half=70, k=3):
    x, y = centre[0] - WIN[0], centre[1] - WIN[1]
    return im.crop((int(x - half), int(y - half), int(x + half), int(y + half))).resize((2 * half * k,) * 2, Image.NEAREST)


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 10 + 6 * len(text), 18], fill=(255, 255, 255))
    d.text((5, 3), text, fill=(0, 0, 0))
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


def capture(name):
    """frames_*.png take 8 (third tile, top row, whole), scaled by CAP_K: hero 4's standing belt-to-sole there reads 257
    px (frames_new take 8: belt y ~588, soles ~845) against 269 at this sheet's game size ((1283 - 707) * KK)."""
    im = Image.open(OUT / name)
    w = im.width / 7
    t = im.crop((int(2 * w), 0, int(3 * w), int(im.height / 2)))
    k = CAP_K
    return t.resize((round(t.width * k), round(t.height * k)), Image.LANCZOS)


CAP_K = 269.0 / 257.0
TILTS = (-8.0, -12.0)           # whole-figure tilts about the planted back heel, as the battle tilts today's hero
ST = -12.0                      # the deliverable: front foot lift at game size matches today's battle hold (take 8)
HEEL = np.array([489.0, 1283.0])


def tilted(px, st):
    """The rendered hero turned by st (r5_keys convention: + clockwise on screen) about the planted heel."""
    c = (float(HEEL[0] - WIN[0]), float(HEEL[1] - WIN[1]))
    M = cv2.getRotationMatrix2D(c, -st, 1.0)
    return cv2.warpAffine(px, M, (px.shape[1], px.shape[0]), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)


def tilt_keys(st, unity):
    """Unity keys for a tilt st about r5_keys.geo's root (standing waist x, soles) that keep the back heel planted: the
    extra (ox, oy) and the key blade that keeps today's screen blade (geo adds st to it)."""
    root = np.array([WAIST2[0], FEET])
    want = root + r5_keys.rot(-st, HEEL - root)          # where the heel must be before the turn
    d = want - HEEL
    k = dict(unity)
    k["blade"] = unity["blade"] - st
    k["turn"] = unity["F"] - k["blade"]
    p = dict(r5_keys.ZERO, **{kk: v for kk, v in BODY.items() if kk in r5_keys.ZERO})
    p.update(body="windup", ox=float(OFF[0] + d[0]), oy=float(OFF[1] + d[1]), a1=k["a1"], b=k["b"], turn=k["turn"],
             st=st)
    g = r5_keys.geo(p)
    pivot = WAIST2 + [p["ox"], p["oy"] + BODY["dy"]]
    x0, y0, x1, y1 = r5_keys.FACE_STAND
    cs = np.array([root + r5_keys.rot(st, pivot + r5_keys.rot(BODY["lean"], np.array(c, float) - WAIST2) - root)
                   for c in ((x0, y0), (x1, y0), (x0, y1), (x1, y1))])
    box = (cs[:, 0].min(), cs[:, 1].min(), cs[:, 0].max(), cs[:, 1].max())
    hits = [n for n, a, b, pad in (("wrist", g["W"], g["W"], 0), ("grip", g["grip"], g["grip"], 0),
                                   ("pommel", g["pommel"], g["pommel"], 0), ("hilt", g["grip"], g["pommel"], 0),
                                   ("blade", g["grip"], g["tip"], 0), ("forearm", g["E"], g["W"], r5_keys.SLEEVE_HALF),
                                   ("upper", g["S"], g["E"], r5_keys.SLEEVE_HALF)) if r5_keys.seg_hits(a, b, box, pad)]
    rho_screen = (k["blade"] - FIST_BLADE + 180) % 360 - 180 + st
    heel_after = root + r5_keys.rot(st, HEEL + d - root)
    return dict(st=st, ox=round(p["ox"], 1), oy=round(p["oy"], 1), a1=k["a1"], b=k["b"], blade_key=round(k["blade"], 1),
                turn=round(k["turn"], 1), blade_screen=round(float(g["blade"]), 1), rho_screen=round(rho_screen, 1),
                face_hits=hits, heel_check=np.round(heel_after, 1).tolist(),
                face_box=[round(float(v), 1) for v in box],
                **{k: np.round(g[k], 1).tolist() for k in ("S", "E", "W", "grip", "pommel", "tip")})


def to_canvas(fr, pt):
    """Frame px -> the sheet's canvas px (the picture is drawn moved by OFF, so this is the standing canvas + OFF)."""
    return np.asarray(pt, float) + [WIN[0], WIN[1]]


# ---- numbers ---------------------------------------------------------------------------------------------------------
def unity_check(unity):
    """r5_keys.geo on this wind-up (Unity's two bones), the face box moved with the upper body, hits listed."""
    p = dict(r5_keys.ZERO, **{k: v for k, v in BODY.items() if k in r5_keys.ZERO})
    p.update(body="windup", ox=float(OFF[0]), oy=float(OFF[1]), a1=unity["a1"], b=unity["b"], turn=unity["turn"])
    g = r5_keys.geo(p)
    pivot = WAIST2 + OFF + [0, BODY["dy"]]
    x0, y0, x1, y1 = r5_keys.FACE_STAND
    cs = [pivot + r5_keys.rot(BODY["lean"], np.array(c, float) - WAIST2) for c in ((x0, y0), (x1, y0), (x0, y1), (x1, y1))]
    cs = np.array(cs)
    box = (cs[:, 0].min(), cs[:, 1].min(), cs[:, 0].max(), cs[:, 1].max())
    hits = [n for n, a, b, pad in (("wrist", g["W"], g["W"], 0), ("grip", g["grip"], g["grip"], 0),
                                   ("pommel", g["pommel"], g["pommel"], 0), ("hilt", g["grip"], g["pommel"], 0),
                                   ("blade", g["grip"], g["tip"], 0), ("forearm", g["E"], g["W"], r5_keys.SLEEVE_HALF),
                                   ("upper", g["S"], g["E"], r5_keys.SLEEVE_HALF))
            if r5_keys.seg_hits(a, b, box, pad)]
    return g, box, hits


def boots_on(hips_canvas):
    """Boot extents of a hips piece as placed on the canvas (premultiplied float, frame px)."""
    px = hips_canvas.copy()
    rgb = px[..., :3] / np.maximum(px[..., 3:4], 1e-4) * 255
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = (px[..., 3] > 0.5) & (R < 120) & (G < 85) & (B < 60) & (R - B > 15) & (R > G)
    m[: int(1000 - WIN[1])] = False
    n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    cs = [lab == i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= 3000]
    out = []
    for c in sorted(cs, key=lambda c: np.nonzero(c)[1].min())[:2]:
        ys, xs = np.nonzero(c)
        heel = xs <= xs.min() + 15
        toe = xs >= xs.max() - 15
        out.append(dict(rear=int(xs.min() + WIN[0]), toe=int(xs.max() + WIN[0]), sole=int(ys.max() + WIN[1]),
                        heel_y=int(ys[heel].max() + WIN[1]), toe_y=int(ys[toe].max() + WIN[1])))
    return out


def sweep():
    tiles, zs = [], []
    for F, bl in SWEEP:
        px, info, fr, pose, unity = render_wind(F, bl)
        bg = swing_r2.background(rig.Frame(rig.Parts(), WIN))
        im = on_bg(px, bg)
        rho = bl - FIST_BLADE
        tiles.append(label(game(im), "F %.0f blade %.0f rho %.1f b %.1f" % (F, bl, rho, unity["b"])))
        zs.append(label(zoom(im, to_canvas(fr, info["W"]) + [0, -20], 90, 2), "fist 2x"))
    fit(col([row(tiles), row(zs)]), 2400, 3.0).save(OUT / "windup_sweep.png")
    print("saved", OUT / "windup_sweep.png")


def rot_pt(pt, st):
    """A canvas point of the untilted still turned with the figure about the planted heel."""
    return HEEL + r5_keys.rot(st, np.asarray(pt, float) - HEEL)


def main():
    # the deliverable pose: tilted ST about the planted back heel; rendered with the key blade (BLADE - ST) so the
    # screen blade stays BLADE after the turn
    px0, info, fr, pose, unity0 = render_wind(F_U, BLADE - ST)
    px = tilted(px0, ST)
    tk = tilt_keys(ST, unity0 | dict(blade=BLADE, turn=F_U - BLADE))
    fr0 = rig.Frame(rig.Parts(), WIN)
    bg = swing_r2.background(fr0)
    im_b = on_bg(px, bg)
    p2 = rig.Parts()
    im_c = on_bg(render_today(fr0, p2), bg)
    px_r, frr = render_ready()
    im_r = on_bg(px_r, bg)
    W_rig, E_rig, S_rig = [rot_pt(to_canvas(fr, info[k]), ST) for k in ("W", "elbow", "S")]
    grip_rig = rot_pt(to_canvas(fr, info["grip"]), ST)
    rho = (info["blade"] - FIST_BLADE + 180) % 360 - 180 + ST          # the fist's turn from its drawing, on screen
    fy = (FEET + 20) * KK
    gb = game(im_b)
    cap_new, cap_old = capture("frames_new.png"), capture("frames_old.png")
    top = row([label(cap_new, "(a) hero 4 wind-up hold now (battle, take 8)"),
               feet_line(label(gb, "(b) new wind-up: coiled legs, turned torso, tilted %+.0f about the back heel" % ST), fy),
               feet_line(label(game(im_c), "(c) today's wind drawing + sword (before the battle's tilt)"), fy),
               label(cap_old, "(d) today's hero in the battle (take 8)")])
    # ---- the other options, for comparison: untilted, the other tilt, front toe planted
    others, tilt_num = [], []
    px_u, _, _, _, unity_u = render_wind(F_U, BLADE)
    others.append(feet_line(label(game(on_bg(px_u, bg)), "(b) untilted (st 0)"), fy))
    for st in TILTS:
        tilt_num.append(tilt_keys(st, unity_u))
        if st == ST:
            continue
        px_t, _, _, _, _ = render_wind(F_U, BLADE - st)
        others.append(feet_line(label(game(on_bg(tilted(px_t, st), bg)), "(b) tilted %+.0f" % st), fy))
    off_f = np.array(GW["waist_offset_from_standing"]["front_toe_planted"], float)
    px_f, _, _, _, _ = render_wind(F_U, BLADE, off_f)
    others.append(feet_line(label(game(on_bg(px_f, bg)), "(b) untilted, front toe planted (%+.0f, %+.0f)" % tuple(off_f)), fy))
    trow = row(others)
    belt_c = rot_pt(WAIST2 + OFF + [0, BODY["dy"] - 10], ST)
    pommel = np.array(tk["pommel"])
    zs = row([label(zoom(im_b, W_rig + [-5, -15], 80), "(b) sword hand 3x  rho %.1f on screen (limit 90)" % rho),
              label(zoom(im_b, E_rig, 80), "(b) elbow 3x"),
              label(zoom(im_b, (S_rig + pommel) / 2, 95), "(b) shoulder + armhole + pommel 3x"),
              label(zoom(im_b, belt_c, 112), "(b) belt / waist seam 3x (both ends)")])
    zs = zs.resize((top.width, round(zs.height * top.width / zs.width)), Image.LANCZOS) if zs.width > top.width else zs
    # ---- ready | wind-up, the planted foot marked
    # the soles: each boot's sole (its lowest 12 rows' x extent, r5w_cut) as two points, heel end and toe end; the
    # wind-up's moved by the waist offset and turned with the figure about the planted heel
    shift = WAIST2 - WAISTW + OFF
    b_r, b_w, moves = [], [], {}
    for i, side in enumerate(("back", "front")):
        r = GW["boots_r2"][i]
        w = GW["boots"][i]
        rp = [np.array([r["sole"][0], r["bottom"]], float), np.array([r["sole"][1], r["bottom"]], float)]
        wp = [rot_pt(np.array([w["sole"][0], w["bottom"]], float) + shift, ST),
              rot_pt(np.array([w["sole"][1], w["bottom"]], float) + shift, ST)]
        b_r.append({"heel": rp[0].round(1).tolist(), "toe": rp[1].round(1).tolist()})
        b_w.append({"heel": wp[0].round(1).tolist(), "toe": wp[1].round(1).tolist()})
        moves[side] = {"heel_dx": round(float(wp[0][0] - rp[0][0]), 1), "heel_up": round(float(rp[0][1] - wp[0][1]), 1),
                       "toe_dx": round(float(wp[1][0] - rp[1][0]), 1), "toe_up": round(float(rp[1][1] - wp[1][1]), 1),
                       "toe_above_ground": round(float(FEET - wp[1][1]), 1)}
    g_r, g_w = game(im_r), game(im_b)
    for gi in (g_r, g_w):
        d = ImageDraw.Draw(gi)
        x = (HEEL[0] - 120) * KK
        d.line([(x, fy - 120), (x, fy + 8)], fill=(255, 40, 40), width=2)
        d.text((x + 4, fy - 118), "back heel x %d" % HEEL[0], fill=(255, 40, 40))
        feet_line(gi, fy)
    flip = row([label(g_r, "ready (round 2)"),
                label(g_w, "wind-up: back heel planted (%+.0f px); back toe up %.0f; front toe %+.0f px, %.0f off the ground" % (
                    moves["back"]["heel_dx"], FEET - b_w[0]["toe"][1], moves["front"]["toe_dx"], moves["front"]["toe_above_ground"]))])
    sheet = col([top, trow, zs, flip])
    d = ImageDraw.Draw(sheet)
    txt = ("Unity keys (r5_keys form): a1 %.1f (rel torso %.1f) b %.1f blade(key) %.1f st %.0f ox %.1f oy %.1f | screen blade %.1f rho %.1f | "
           "rig still wrist %.0f px from Unity's | face hits: %s" % (
               tk["a1"], tk["a1"] - BODY["lean"], tk["b"], tk["blade_key"], ST, tk["ox"], tk["oy"], tk["blade_screen"],
               tk["rho_screen"], float(np.linalg.norm(W_rig - np.array(tk["W"]))), tk["face_hits"] or "none"))
    d.rectangle([0, sheet.height - 20, sheet.width, sheet.height], fill=(255, 255, 255))
    d.text((6, sheet.height - 17), txt, fill=(0, 0, 0))
    fit(sheet, 2400, 3.4).save(OUT / f"windup{TAG}.png")
    im_b.crop((250 - WIN[0], 0 - WIN[1], 1500 - WIN[0], 1330 - WIN[1])).save(OUT / f"windup_b_1x{TAG}.png")
    num = {
        "pose": "wind-up, tilted st %.0f about the planted back heel (489, 1283)" % ST,
        "unity_keys_r5_keys_form": {"body": "wind (new set: hips_wind + parts_r4 torso/armhole/far arm + round 2 head/tails)",
                                    "a1_rel_torso": round(tk["a1"] - BODY["lean"], 2), "a1_screen": tk["a1"],
                                    "b": tk["b"], "blade_key": tk["blade_key"], "turn": tk["turn"], "st": ST,
                                    "ox": tk["ox"], "oy": tk["oy"], "sleeve": "under", **{k: v for k, v in BODY.items()}},
        "screen": {"blade": tk["blade_screen"], "rho_fist_turn_from_drawing": tk["rho_screen"]},
        "armhole_drawn_a1": G4["a1_drawn"],
        "rig_still": {"a1": round(float(pose["a1"]), 2), "b": round(float(pose["b"]), 2), "turn": round(float(pose["turn"]), 2),
                      "rho_screen": round(float(rho), 1), "wrist_bend_from_drawing": round(float(info["wrist_bend"]), 1),
                      "S": S_rig.round(1).tolist(), "elbow": E_rig.round(1).tolist(), "wrist": W_rig.round(1).tolist(),
                      "grip": grip_rig.round(1).tolist(), "flipped_triangles": int(info["flipped"])},
        "unity_geo": {k: tk[k] for k in ("S", "E", "W", "grip", "pommel", "tip")},
        "wrist_rig_vs_unity_px": round(float(np.linalg.norm(W_rig - np.array(tk["W"]))), 1),
        "face_box": tk["face_box"], "face_hits": tk["face_hits"],
        "waist_offset_from_standing_untilted": GW["waist_offset_from_standing"], "plant_used": PLANT,
        "boots_ready": b_r, "boots_wind": b_w, "boot_moves": moves,
        "all_tilts_back_heel_planted": tilt_num,
        "layer_order_back_to_front": ["tails", "far_arm", "head", "torso", "hips", "painted round shoulder", "sword",
                                      "fist", "painted sleeve", "armhole"],
    }
    (OUT / f"windup_numbers{TAG}.json").write_text(json.dumps(num, indent=1, default=float))
    print(json.dumps(num, indent=1, default=float))
    print("saved", OUT / "windup.png")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "sweep":
        sweep()
    else:
        main()
