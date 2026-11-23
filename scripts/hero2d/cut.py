"""Cut the hero's cutting-pose drawing (a Qwen edit on flat purple) into clean pieces (step 1b).
<venv python> -I -B cut.py [src name in art/redo/hero2d/r2, default cut2_s21] [kit name, default pieces]
Writes art/redo/hero2d/r2/<kit>/*.png and art/redo/hero2d/r2/<kit>.json. An armor drawing (the same pose with other
clothes) is cut by the same zones and joint points: `cut.py armor_s31 scale_mail`.

Every joint point and zone line below was read by eye from cut2_s21 with look.py (a grid over a zoomed crop). All the
clean-up happens here, once: round joint ends (round_joint: half discs of the limb's own width and cross-section), a
round shoulder brought by the sleeve, the torso cut along a plain trunk line with its own outline, the far sleeve made
whole under the torso, hips and knees faded to one flat cloth colour, outlines smoothed near the joints."""
from __future__ import annotations

import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from cutlib import (extrude_cap, feather_cut, fill_rgb, flatten, grid, key_purple, line_on, poly_mask, round_joint,  # noqa: E402
                    save_piece, section, smooth_outline, unmix_purple)

ROOT = Path(__file__).resolve().parents[2]
KIT = ROOT / "art/redo/hero2d/r2"   # step 1b
SRC = sys.argv[1] if len(sys.argv) > 1 else "cut2_s21"
NAME = sys.argv[2] if len(sys.argv) > 2 else "pieces"
OUT = KIT / NAME
OUT.mkdir(parents=True, exist_ok=True)

raw = np.asarray(Image.open(KIT / f"{SRC}.png").convert("RGB"))
alpha = key_purple(raw)
rgb = unmix_purple(raw, alpha)
H, W = alpha.shape
# dark edge pixels mixed with the purple field look solid to the key: purple-tinted pixels (blue and red both well
# above green; none of his colours are like that) take the nearest clean colour and a softer alpha
near_edge = cv2.distanceTransform((alpha > 0.5).astype(np.uint8), cv2.DIST_L2, 5) <= 6   # only at the silhouette
spill = near_edge & (alpha > 0.05) & (rgb[..., 2] > rgb[..., 1] + 15) & (rgb[..., 0] > rgb[..., 1] + 8)
alpha = np.where(spill, np.minimum(alpha, 0.6), alpha)
_fill0 = np.concatenate([rgb, ((alpha > 0.5) & ~spill)[..., None] * 255.0], -1)
rgb = np.where(spill[..., None], fill_rgb(_fill0), rgb)
# well inside the figure everything is solid and keeps the drawn colour (greys with a lavender shine, like steel, read
# as half purple to the key and turned green when unmixed)
_solid = (alpha > 0.5).astype(np.uint8)
_open = cv2.morphologyEx(_solid, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))   # pinholes only, not gaps
interior = (cv2.distanceTransform(_open, cv2.DIST_L2, 5) > 4) & (alpha > 0.65) & ~spill   # no real hole, no spill (steel: 0.7+)
alpha = np.where(interior, 1.0, alpha)
rgb = np.where(interior[..., None], raw.astype(np.float32), rgb)
fig = np.concatenate([rgb, alpha[..., None] * 255], -1)  # float, alpha 0..255
fa = alpha > 0.5
XX, YY = grid(alpha.shape)

# ---- colour classes ----
r, g, b = rgb[..., 0] + 1, rgb[..., 1], rgb[..., 2]
skin0 = (g / r < 0.76) & (b / r < 0.45) & (r > 120)
shirt0 = (g / r >= 0.76) & (b / r >= 0.4) & (r > 120)
d_skin = cv2.distanceTransform((~skin0).astype(np.uint8), cv2.DIST_L2, 5)
d_shirt = cv2.distanceTransform((~shirt0).astype(np.uint8), cv2.DIST_L2, 5)
skin = skin0 | (~skin0 & ~shirt0 & (d_skin < d_shirt))
r_, g_, b_ = rgb[..., 0], rgb[..., 1], rgb[..., 2]
teal = (g_ > r_ + 4) & (b_ > r_)                              # trousers
brown = (r_ >= g_) & (g_ >= b_ - 6) & (r_ < 150) & ~skin0     # boots, belt, hair
red = (g / r < 0.42) & (r > 90)                               # headband

ys, xs = np.nonzero(fa)
TOP, BOT = int(ys.min()), int(ys.max())
HERO_H = BOT - TOP

# ---- joint points of cut2_s21 (1536x1536), read by eye; limb joints are then centred in the limb's section ----
J = {
    "neck": (765, 445), "waist": (777, 700),
    "n_shoulder": (668, 512), "n_elbow": (589, 636), "n_wrist": (500, 748),
    "f_shoulder": (866, 508), "f_elbow": (915, 640), "f_wrist": (998, 748),
    "n_hip": (728, 762), "n_knee": (660, 933), "n_hem": (628, 1051), "n_ankle": (553, 1233),
    "f_hip": (819, 762), "f_knee": (887, 933), "f_hem": (919, 1051), "f_ankle": (960, 1205),
}
POINTS = {"n_grip": (493, 808), "n_thumb": (545, 815), "f_grip": (1016, 794), "f_thumb": (990, 800)}
SHOULDER_R = 54          # the round shoulder the near sleeve brings (disc about the shoulder pivot)
CUFF_FROM = {"n": 108, "f": 95}   # the rolled band: sleeve past this distance from the shoulder pivot
WAIST_Y = 684            # shirt hem / belt top

# zone lines (cut2_s21)
N_SEAM = [(640, 440), (650, 468), (670, 476), (686, 492), (694, 515), (696, 540), (692, 565), (678, 586)]
N_TRUNK = [(700, 420), (712, 436), (707, 480), (700, 530), (690, 565), (678, 586)]   # plain trunk line under the arm
F_TRUNK = [(835, 440), (852, 476), (860, 510), (862, 560), (860, 625), (866, 690)]   # the torso's drawn far edge

# armor only: the outline of each shoulder plate, read by eye on that drawing (a plate reaches over the chest, past
# the round shoulder, and must turn with the arm whole). Near plate -> the near sleeve; far plate -> its own piece
# f_pad drawn over the torso with the far upper arm. Under both, the torso shows the dark padding (flat colour).
PADS = {
    "mail_s32": {
        "n": [(588, 537), (598, 507), (618, 467), (640, 446), (660, 432), (682, 430), (705, 436), (719, 452), (725, 483),
              (725, 512), (718, 525), (700, 535), (673, 542), (640, 543), (608, 541)],
        "f": [(838, 450), (860, 455), (882, 466), (898, 487), (908, 512), (913, 533), (919, 547), (914, 560), (898, 552),
              (878, 542), (864, 530), (856, 505), (848, 478)],
        "padding_rgb": (52, 40, 28),
    },
}
PAD = PADS.get(SRC)


def unit(a, b):
    d = np.subtract(b, a).astype(float)
    return d / np.hypot(*d)


def along(p, u):
    return (XX - p[0]) * u[0] + (YY - p[1]) * u[1]


def disc(c, rr):
    return np.hypot(XX - c[0], YY - c[1]) <= rr


def capsule(a, b, rr):
    u = unit(a, b)
    L = float(np.hypot(*np.subtract(b, a)))
    t = np.clip(along(a, u), 0, L)
    px_, py_ = a[0] + u[0] * t, a[1] + u[1] * t
    return np.hypot(XX - px_, YY - py_) <= rr


def centre(name, nxt, mask):
    """Move a joint point to the middle of the limb's cross-section there (across the bone)."""
    p = J[name]
    _, _, mid = section(mask, p, unit(p, J[nxt]))
    J[name] = (round(float(mid[0]), 1), round(float(mid[1]), 1))


pieces: dict = {}
masks: dict = {}


def piece(name, px, pivot, tip, points=None, note=""):
    """px: float RGBA (alpha 0..255) on the full canvas. Small stray blobs are dropped."""
    m = (px[..., 3] > 128).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    if n > 2:
        keep = np.zeros(n, bool)
        keep[1:] = st[1:, cv2.CC_STAT_AREA] >= 0.03 * st[1:, cv2.CC_STAT_AREA].max()
        grown = cv2.dilate(keep[lab].astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
        px = px * grown[..., None]
    x0, y0 = save_piece(px, OUT / f"{name}.png")
    sh = lambda p: [round(float(p[0]) - x0, 1), round(float(p[1]) - y0, 1)]  # noqa: E731
    pieces[name] = {"file": f"{NAME}/{name}.png", "pivot": sh(pivot), "tip": sh(tip),
                    "points": {k: sh(v) for k, v in (points or {}).items()}, "note": note, "src_offset": [x0, y0]}
    masks[name] = px[..., 3] > 128


def side_mask(line, keep_left=True, top=0, bottom=None):
    """Pixels left (or right) of a polyline running top to bottom (extended straight up and down)."""
    bottom = H if bottom is None else bottom
    pts = [(line[0][0], top)] + list(line) + [(line[-1][0], bottom)]
    edge = [(0, bottom), (0, top)] if keep_left else [(W, bottom), (W, top)]
    return poly_mask(alpha.shape, pts + edge)


head_zone = poly_mask(alpha.shape, [(540, 170), (1010, 170), (1010, 438), (830, 438), (812, 452), (760, 452),
                                    (712, 434), (540, 434)])
# the collar under the chin belongs to the torso: shirt there, or (armor) anything that is not skin, hair or band
under_chin = head_zone & (YY > 410) & (shirt0 | ((PAD is not None) & (YY > 415) & (XX > 705) & ~skin & ~red))
# (grown a few px so each plate takes its whole drawn outline; a leftover outline sliver would stick out when the
# sleeve turns further than the plate)
_grow = lambda m: cv2.dilate(m.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11))) > 0  # noqa: E731
pad_n = _grow(poly_mask(alpha.shape, PAD["n"])) & fa if PAD else np.zeros_like(fa)
pad_f = _grow(poly_mask(alpha.shape, PAD["f"])) & fa if PAD else np.zeros_like(fa)

# ================= arms =================
left_n_trunk = side_mask(N_TRUNK, True, 0, 700)
left_n_seam = side_mask(N_SEAM, True, 0, 700)
n_zone = (left_n_trunk & (YY < 700)) | ((XX < 600) & (YY >= 700) & (YY < 900))
n_zone &= ~red & fa & ~head_zone
right_f = ~side_mask(F_TRUNK, True, 0, 700)
f_zone = ((right_f & (YY < 700)) | ((XX > 940) & (YY >= 700) & (YY < 900))) & fa & ~red & ~head_zone

centre("n_elbow", "n_wrist", n_zone)
centre("n_wrist", "n_elbow", n_zone & skin)
centre("f_elbow", "f_wrist", f_zone)
centre("f_wrist", "f_elbow", f_zone & skin)

for s, zone in (("n", n_zone), ("f", f_zone)):
    P, E, Wr = J[f"{s}_shoulder"], J[f"{s}_elbow"], J[f"{s}_wrist"]
    uu, uf = unit(P, E), unit(E, Wr)
    beyond_wrist = along(Wr, uf) > 0
    sleeve_m = zone & ~skin & ~beyond_wrist
    if s == "n":
        # the sleeve brings the whole shoulder: the drawn sleeve left of the seam plus a round top about the pivot
        sleeve_m &= left_n_seam | disc(P, SHOULDER_R)
        # a whole disc of the drawing about the pivot (it covers the same place at every angle, so no wedge opens
        # between the sleeve top and the trunk line when the arm turns)
        sleeve_m |= fa & disc(P, SHOULDER_R) & ~head_zone
        # armor: the plate is its own piece (n_pad); under it the sleeve keeps only its round top, in the dark padding
        # colour (the plate's far-reaching corners would stick out past the plate when the arm turns)
        sleeve_m &= ~(pad_n & ~disc(P, SHOULDER_R))
        if PAD:   # and no thin leftover strips of the sleeve's edge beside the plate
            sleeve_m = cv2.morphologyEx(sleeve_m.astype(np.uint8), cv2.MORPH_OPEN,
                                        cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (13, 13))) > 0
        sl = fig * sleeve_m[..., None]
        if PAD:
            sl[..., :3] = np.where(pad_n[..., None], np.asarray(PAD["padding_rgb"], np.float32), sl[..., :3])
        # the round top where it lies over the chest gets the drawing's own line (a shoulder seam)
        if not PAD:   # (armor: the plate covers that seam)
            sl = line_on(sl, disc(P, SHOULDER_R + 3) & ~left_n_seam & (YY < P[1] + 40), width=1.8)
    else:
        # the far sleeve is made whole under the torso (a tube of the near sleeve's width), so it can swing clear
        tube = capsule(P, E, 34) & ~right_f & (along(P, uu) > -40)
        sl = fig * sleeve_m[..., None]
        fill = np.concatenate([fill_rgb(sl), np.full((H, W, 1), 255.0, np.float32)], -1)
        sl = np.where((tube & ~sleeve_m)[..., None], fill, sl)
        sl = line_on(sl, tube & ~sleeve_m, width=2.4)
        if PAD:   # under the plate (its own piece f_pad) the dark padding, and no thin strips left beside it
            sl[..., :3] = np.where(pad_f[..., None] & (sl[..., 3:] > 0), np.asarray(PAD["padding_rgb"], np.float32),
                                   sl[..., :3])
            keep = cv2.morphologyEx((sl[..., 3] > 128).astype(np.uint8), cv2.MORPH_OPEN,
                                    cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (13, 13))) > 0
            sl = sl * cv2.dilate(keep.astype(np.uint8), np.ones((3, 3), np.uint8))[..., None]
    cuff_m = (sl[..., 3] > 128) & (along(P, uu) >= CUFF_FROM[s])
    piece(f"{s}_upper", sl, P, E, note="sleeve with the rolled band" + (", round shoulder" if s == "n" else ""))
    piece(f"{s}_cuff", sl * cuff_m[..., None], P, E, note="the rolled band again, drawn over the forearm's root")
    # forearm: skin up to the wrist; its root runs on under the band to the elbow (a cap of its own section)
    fo = fig * (zone & skin & ~beyond_wrist)[..., None]
    base = np.asarray(E) + uf * 34
    _, _, mid = section(fo[..., 3] > 128, base, uf)
    l0, l1, _ = section(fo[..., 3] > 128, mid, uf)
    fo = extrude_cap(fo, tuple(mid), E, (l1 - l0) / 2, inset=6)
    fo = round_joint(fo, Wr, -uf, inset=8, drop=False)   # runs on round under the fist (no seam at the wrist)
    piece(f"{s}_fore", fo, E, Wr, note="forearm; its root runs on under the rolled band to the elbow")
    fi = fig * (zone & beyond_wrist & (skin | ~shirt0))[..., None]
    fi = feather_cut(fi, fa, 3.0)
    g_, t_ = POINTS[f"{s}_grip"], POINTS[f"{s}_thumb"]
    piece(f"{s}_fist", fi, Wr, g_, points={"grip": g_, "thumb": t_},
          note="fist, back of the hand to us" + ("; turns with the sword about the wrist" if s == "n" else ""))

# ================= head, torso, hips =================
head = fa & head_zone & ~under_chin
piece("head", fig * head[..., None], J["neck"], (800, 300), note="head with hair, headband and tails")

torso_zone = fa & ~left_n_seam & ~right_f & (YY < 700) & (~head_zone | under_chin)
hem_shirt = cv2.dilate(shirt0.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
torso_m = torso_zone & ~(masks["n_upper"] & left_n_trunk) & ((YY < WAIST_Y - 8) | (hem_shirt & (YY < 694)))
torso_m &= ~((YY > WAIST_Y - 8) & (XX > 786) & (XX < 836))   # the buckle (pale like the shirt) stays on the belt
to = fig * torso_m[..., None]
if PAD:
    # under the plates the torso shows the dark padding (the plates turn away with the arms)
    under = (pad_n | pad_f) & torso_m
    to[..., :3] = np.where(under[..., None], np.asarray(PAD["padding_rgb"], np.float32), to[..., :3])
# the plain trunk line under the near arm gets the drawing's outline (a cut, but meant to be seen)
to = line_on(to, side_mask([(x + 8, y) for x, y in N_TRUNK], True, 0, 640) & (YY > 440), width=2.4)
piece("torso", to, J["waist"], J["neck"],
      points={"neck": J["neck"], "n_shoulder": J["n_shoulder"], "f_shoulder": J["f_shoulder"]},
      note="shirt, waist up; plain trunk line under each arm, the sleeves bring the shoulders")

TROUSER = np.median(rgb[teal & fa & (YY > 760) & (YY < 900)], 0)
pelvis = poly_mask(alpha.shape, [(676, WAIST_Y - 10), (876, WAIST_Y - 10), (878, 760), (866, 792), (822, 818),
                                 (778, 834), (734, 818), (688, 792), (676, 760)])
hips_m = fa & pelvis & ~torso_m & ~n_zone & ~f_zone
hp = fig * hips_m[..., None]
# the lower part fades to one flat cloth colour (the thigh tops under it are flat there too: no step)
t = np.clip((YY - 738) / 40, 0, 1)[..., None]
hp[..., :3] = hp[..., :3] * (1 - t) + TROUSER * t
piece("hips", hp, J["waist"], (777, 830), points={"n_hip": J["n_hip"], "f_hip": J["f_hip"]},
      note="belt and the top of the trousers; round lower edge in flat cloth colour")

# ================= legs =================
legs = fa & (YY > WAIST_Y + 20) & ~n_zone & ~f_zone
bt = brown & (YY > 1010)
tl = teal & ~bt
d_b = cv2.distanceTransform((~bt).astype(np.uint8), cv2.DIST_L2, 5)
d_t = cv2.distanceTransform((~tl).astype(np.uint8), cv2.DIST_L2, 5)
boot_all = legs & (bt | (~bt & ~tl & (d_b < d_t) & (YY > 1000)))
trousers_all = legs & ~boot_all
for s, split in (("n", XX < 777), ("f", XX >= 777)):
    leg = trousers_all & split
    centre(f"{s}_knee", f"{s}_hip", leg)
    hip, knee, hem, ankle = J[f"{s}_hip"], J[f"{s}_knee"], J[f"{s}_hem"], J[f"{s}_ankle"]
    ut, us = unit(hip, knee), unit(knee, hem)
    # thigh: the drawn trouser leg, completed to a whole tube near the hip (the split between the legs is a cut),
    # round at both ends, flat cloth colour at the hip and knee
    th_m = leg & (along(knee, ut) < 0)
    mid_t = np.asarray(hip) + ut * 0.55 * np.hypot(*np.subtract(knee, hip))
    l0, l1, midp = section(th_m, mid_t, ut)
    tube = capsule(hip, knee, (l1 - l0) / 2) & (along(knee, ut) < 0) & (YY > WAIST_Y)
    th = fig * th_m[..., None]
    fill = np.concatenate([fill_rgb(th), np.full((H, W, 1), 255.0, np.float32)], -1)
    th = np.where((tube & ~th_m)[..., None], fill, th)
    # the drawing's soft edge pixels that are now inside the piece carry purple: give them the nearest solid colour
    solid = th_m & (alpha > 0.97)
    soft_in = (th[..., 3] > 1) & ~solid
    th[..., :3] = np.where(soft_in[..., None], fill_rgb(th, solid), th[..., :3])
    th = round_joint(th, hip, ut, inset=8)
    th = round_joint(th, knee, -ut, inset=8, drop=False)
    th = flatten(th, hip, 34, 90, TROUSER)
    th = flatten(th, knee, 26, 70, TROUSER)
    th = smooth_outline(th, hip, 90, sigma=6)
    th = smooth_outline(th, knee, 70, sigma=5)
    piece(f"{s}_thigh", th, hip, knee, note="thigh; round top under the hips, round knee end, flat near both")
    sh_m = leg & (along(knee, ut) >= -2)
    shn = fig * sh_m[..., None]
    shn = round_joint(shn, knee, us, inset=10)
    shn = flatten(shn, knee, 26, 70, TROUSER)
    shn = smooth_outline(shn, knee, 75, sigma=6)
    piece(f"{s}_shin", shn, knee, hem, note="lower trouser leg; round top (flat colour) under the thigh's knee end")
    bo = fig * (boot_all & split)[..., None]
    ub = unit(hem, ankle)
    _, _, bmid = section(bo[..., 3] > 128, np.asarray(hem) + ub * 45, ub)
    b0, b1, _ = section(bo[..., 3] > 128, bmid, ub)
    bo = extrude_cap(bo, tuple(bmid), hem, 0.8 * (b1 - b0) / 2, inset=4)
    piece(f"{s}_boot", bo, hem, ankle, note="boot; its top closed round under the trouser hem")

if PAD:
    for s, pm in (("n", pad_n), ("f", pad_f)):
        piece(f"{s}_pad", fig * pm[..., None], J[f"{s}_shoulder"], J[f"{s}_elbow"],
              note="shoulder plate; turns part of the way with the upper arm (rig PAD_FOLLOW)")

# ---- the sword (game art; grip from items.ts grip 0.79, size 0.6 of hero height) ----
sw = np.asarray(Image.open(ROOT / "art/raw/wpn_training_sword.png").convert("RGBA")).astype(np.float32)
sw[..., 3] = np.where(sw[..., 3] < 10, 0, sw[..., 3])
f = 0.6 * HERO_H / sw.shape[0]
swi = Image.fromarray(sw.astype(np.uint8), "RGBA")
swi = swi.resize((round(swi.width * f), round(swi.height * f)), Image.LANCZOS)
sw = np.asarray(swi).astype(np.float32)
x0, y0 = save_piece(sw, OUT / "sword.png")
grip = (432 * f - x0, 0.79 * 1536 * f - y0)
pieces["sword"] = {"file": f"{NAME}/sword.png", "pivot": [round(grip[0], 1), round(grip[1], 1)],
                   "tip": [round(grip[0], 1), round(grip[1] - 100, 1)], "points": {}, "src_offset": [x0, y0],
                   "note": "wpn_training_sword; pivot = the middle of the handle, tip = up the blade"}

LAYERS = ["f_boot", "f_shin", "f_thigh", "n_boot", "n_shin", "n_thigh", "hips", "f_upper", "f_fore", "f_cuff",
          "f_fist", "torso"] + (["f_pad"] if PAD else []) + ["head", "n_upper"] + (["n_pad"] if PAD else []) + ["n_fore", "n_cuff", "sword", "n_fist"]
meta = {"layers_back_to_front": LAYERS,
        "variants": {"n_fist": [], "f_fist": [], "note": "none drawn: the one fist per hand is the cutting-pose fist"},
        "src": f"{SRC}.png", "hero_height_px": HERO_H, "top": TOP, "bottom": BOT, "joints": J, "pieces": pieces}
(KIT / f"{NAME}.json").write_text(json.dumps(meta, indent=1))
print("hero height", HERO_H, "joints", {k: v for k, v in J.items()})
