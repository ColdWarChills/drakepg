#!/usr/bin/env python3
"""Today's hero drawn as pieces for bending (PLANS "The hero's animation plan", step 1, try 2): the paper puppet's
mechanics (scripts/puppet.py, imported, never edited) with today's painted look.

The pieces are cut from today's art itself (char_hero_bare@2x: its near arm already hangs straight and clear), not
drawn as a flat sheet. Each piece keeps the painting; the ends a neighbour hides are completed as round overlapping
ends (a forearm's top under the cuff: its own painted rows reflected; a wrist: the fist's own skin), and the cuff's
dark inside is its own layer under the forearm, so a bend never shows a flat cut or a ball of skin. The skeleton is
built in memory from the picture's own joint points (skeleton.py's bones, its file untouched), so every piece rides
its bone at scale 1. The fists come from local Qwen edits of the art's own hands gripping a magenta rod (the rod
trick): the rod gives the grip, then is taken out; the sword is fixed to the hand bone at that grip, sized as the game
sizes it (weapon size x the hero picture's height). Only the sword arm is made for bending so far: the head, torso,
legs and far arm are cut by nearest bone in their drawn pose.

  python scripts/pieces.py fist back|palm|open <round> <seed>[,..]   Qwen edit of a hand crop (back/palm grip the rod)
  python scripts/pieces.py fistsheet <round> <seed>[,..]    Qwen: three right hands, style from the hand crop only
  python scripts/pieces.py make <set>                       the near arm from today's art + FIST_PICKS -> pieces_<set>/
  python scripts/pieces.py body base <set>                  the rest of the hero cut from today's art into the set
  python scripts/pieces.py bends <set> <out.png> [nobase] [fore_over]   the sword arm at 3x, elbow 0-120 deg, each fist
  python scripts/pieces.py still <set> <out.png> [<close.png>]   game size on the Fen Road (try 1's staging) + close-up
  python scripts/pieces.py cut base <try_set>               just the cut (a try set: art/redo/pieces/sets/<try_set>/)

Kept sets: unity/Bones/hero/pieces_<set>/; sets named try_* and all Qwen tries (with their prompt and seed in a .txt)
go to art/redo/pieces/ (git-ignored). Nothing kept is overwritten.
"""
import io
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import skeleton as sk  # noqa: E402
import puppet as pp  # noqa: E402

ROOT = sk.ROOT
ART = ROOT / "src" / "assets" / "art"
BASE = ART / "char_hero_bare@2x.webp"
TRIES = ROOT / "art" / "redo" / "pieces"
OUT = ROOT / "art" / "tests" / "sheets" / "pieces"
BONES = sk.BONES
HOST = "http://127.0.0.1:8188"

# ---------------------------------------------------------------- puppet.py, patched in memory only
pp.OUT = OUT
pp.LAYER_SHADOW = None          # today's look is painted: no paper layer shadows
pp.HERO_SAT = 1.0
WEAPON_SIZE = 0.62              # src/data/items.ts iron_sword size: the sword's height = size x the hero picture's height
HERO_PIC_H = 960                # char_hero_bare@2x height (the game scales the weapon by displayHeight)
SW = WEAPON_SIZE * HERO_PIC_H / 640.0     # sword px -> picture px


def set_dir(name):
    """A kept set: unity/Bones/hero/pieces_<name>/; a try (name try_*): art/redo/pieces/sets/<name>/ (git-ignored)."""
    return TRIES / "sets" / name if name.startswith("try_") else BONES / f"pieces_{name}"


def load_set(name):
    d = set_dir(name)
    return d, json.loads((d / "parts.json").read_text())


pp.load_set = load_set


def sword_affine(fist, fo, fex, fey):
    """puppet.sword_affine, with the sword at the game's size: the handle along the rod line, the guard against the
    fist's thumb-side outline, the blade out of the thumb side; only the fist's transform (the hand bone) moves it."""
    a, b = fist["a"], fist["b"]
    hand = math.atan2(b[1] - a[1], b[0] - a[0])
    bl = hand + math.radians(fist["hold"])
    u = (math.cos(bl), math.sin(bl))
    g = fist["rod"]
    e = fist["edge"] - fist.get("tuck", 3)
    gp = (g[0] + u[0] * e, g[1] + u[1] * e)
    P = lambda p: (fo[0] + p[0] * fex[0] + p[1] * fey[0], fo[1] + p[0] * fex[1] + p[1] * fey[1])
    G, U = P(gp), P((gp[0] + u[0], gp[1] + u[1]))
    U = (U[0] - G[0], U[1] - G[1])
    n = math.hypot(*U)
    U = (U[0] / n, U[1] / n)
    ey = (-U[0] * SW, -U[1] * SW)
    ex = (-U[1] * SW, U[0] * SW)
    o = (G[0] - pp.SWORD_GRIP[0] * ex[0] - pp.SWORD_GUARD * ey[0], G[1] - pp.SWORD_GRIP[0] * ex[1] - pp.SWORD_GUARD * ey[1])
    return o, ex, ey


pp.sword_affine = sword_affine
_arm_items0 = pp.arm_items


def arm_items(parts, d, pts, fist_name, sword=True):
    """puppet.arm_items plus the cuff's inside (its own layer on the upper arm's bone, under the forearm)."""
    items = _arm_items0(parts, d, pts, fist_name, sword)
    pc = parts.get("n_cuff")
    if pc:
        o, ex, ey = pp.piece_affine(pc, pts[pc["bone"]])
        items.append((pc["z"], lambda c, k, off, f=d / pc["file"], o=o, ex=ex, ey=ey: pp.draw_affine(c, pp.pic(f), o, ex, ey, k, off)))
    return items


pp.arm_items = arm_items

# ---------------------------------------------------------------- the skeleton on a picture's own joints
_LIST0 = list(sk.BONE_LIST)


def use_joints(J, hero_h):
    """Rebuild skeleton.py's bones (in memory) on the picture's joints; no limits (the picture is the rest pose)."""
    P = {k: tuple(v) for k, v in J.items()}
    sk.P = P
    sk.BONE_LIST = [(n, p, at, a, b, None) for n, p, at, a, b, _ in _LIST0]
    sk.BONES_DEF, sk.STAND = sk.build()
    pp.GAME_K = 235 / hero_h          # picture px -> game px (the hero ~235 px tall in a 1280x720 battle)


# ---------------------------------------------------------------- sources: a picture + its joints (picture px)
SOURCES = {
    # today's art (2x): the near arm hangs by his side; read off a 25 px grid
    "base": {
        "file": str(BASE), "hero_h": 954,
        "joints": {"waist": (262, 430), "mid_hip": (265, 470), "neck": (262, 200), "head_top": (270, 20),
                   "n_shoulder": (160, 268), "n_elbow": (107, 370), "n_wrist": (96, 505), "n_knuckles": (98, 572),
                   "f_shoulder": (330, 262), "f_elbow": (392, 395), "f_wrist": (448, 318), "f_knuckles": (474, 245),
                   "n_hip": (212, 482), "n_knee": (172, 700), "n_ankle": (110, 872), "n_toe": (40, 925),
                   "f_hip": (312, 482), "f_knee": (398, 690), "f_ankle": (392, 880), "f_toe": (512, 922)},
        # half widths: sleeve (and the shoulder cap's radius), forearm, fist
        "radii": {"n": (52, 34, 46)},
        # the cuff's lower edge, px along the forearm from the elbow joint (the joint sits deep enough in the sleeve
        # that the forearm's round end never pokes out below the cuff when bent)
        "cuff": {"n": 37},
    },
}


def source(name):
    if name in SOURCES:
        return SOURCES[name]
    j = TRIES / f"{name}.json"
    return json.loads(j.read_text())


# ---------------------------------------------------------------- cutting
def skin_of(arr):
    r, g, b, a = [arr[..., i].astype(float) for i in range(4)]
    return (a > 40) & (r > 90) & (g < 0.79 * r) & (b < 0.55 * r)


def seg(xx, yy, p, q):
    """Distance to the segment p-q, and the unclamped position along it (px from p)."""
    L = math.dist(p, q)
    ux, uy = (q[0] - p[0]) / L, (q[1] - p[1]) / L
    t = (xx - p[0]) * ux + (yy - p[1]) * uy
    tc = np.clip(t, 0, L)
    d = np.hypot(xx - (p[0] + tc * ux), yy - (p[1] + tc * uy))
    return d, t, L, (ux, uy)


def grow_labels(lab, todo, n=40):
    """Unlabelled pixels in todo take the label of a labelled neighbour, ring by ring (nearest wins)."""
    lab = lab.copy()
    for _ in range(n):
        left = todo & (lab == 0)
        if not left.any():
            break
        for v in sorted(set(np.unique(lab)) - {0}):
            m = Image.fromarray(((lab == v) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
            grab = left & (np.asarray(m) > 0) & (lab == 0)
            lab[grab] = v
    return lab


def piece_from(arr, mask, pad=3):
    ys, xs = np.nonzero(mask)
    x0, y0, x1, y1 = xs.min() - pad, ys.min() - pad, xs.max() + 1 + pad, ys.max() + 1 + pad
    x0, y0 = int(max(0, x0)), int(max(0, y0))
    x1, y1 = int(x1), int(y1)
    out = arr[y0:y1, x0:x1].copy()
    out[~mask[y0:y1, x0:x1], 3] = 0
    return out, (x0, y0)


def complete_end(arr, have, J0, u, r, t_from, t_to):
    """Fill the round end of a limb piece: pixels within r of the axis through J0 (direction u, into the piece),
    from t_from (<= -r, a disc there) to t_to, that the piece lacks, take the piece's own pixel reflected across
    where it starts along u (its painted rows mirrored)."""
    h, w = have.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    t = (xx - J0[0]) * u[0] + (yy - J0[1]) * u[1]
    s = (xx - J0[0]) * -u[1] + (yy - J0[1]) * u[0]
    region = ((np.abs(s) <= r) & (t >= 0) & (t <= t_to)) | ((t < 0) & (np.hypot(xx - J0[0], yy - J0[1]) <= r))
    need = region & ~have
    out = arr.copy()
    ys, xs = np.nonzero(need)
    for y, x in zip(ys, xs):
        for k in range(1, int(3 * r + t_to)):
            yy1, xx1 = int(round(y + u[1] * k)), int(round(x + u[0] * k))
            if not (0 <= yy1 < h and 0 <= xx1 < w):
                break
            if have[yy1, xx1]:
                # mirror: as far again past the start
                y2, x2 = int(round(y + u[1] * (2 * k + 8))), int(round(x + u[0] * (2 * k + 8)))   # past the cuff's shadow
                if not (0 <= y2 < h and 0 <= x2 < w and have[y2, x2]):
                    y2, x2 = yy1, xx1
                out[y, x, :3] = arr[y2, x2, :3]
                out[y, x, 3] = 255
                break
    return out


def feather(arr, cut_mask, px=2):
    """Soften the alpha only along edges that were cut (not the painting's own outline)."""
    a = arr[..., 3].astype(float)
    sm = np.asarray(Image.fromarray(arr[..., 3]).filter(ImageFilter.GaussianBlur(px))).astype(float)
    a = np.where(cut_mask, np.minimum(a, sm), a)
    arr[..., 3] = a.astype(np.uint8)
    return arr


def picture(src):
    arr = np.asarray(Image.open(src["file"]).convert("RGBA")).copy()
    arr[arr[..., 3] < 10, 3] = 0
    return arr


def arm_labels(src, arr, side="n"):
    """Each pixel of the near arm: 1 sleeve, 2 forearm, 3 fist (0: not the arm). Also returns the forearm's
    (distance, position along it, length, direction) and the cuff's position."""
    J = {k: tuple(v) for k, v in src["joints"].items()}
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    S, E, W, K = (J[f"{side}_{n}"] for n in ("shoulder", "elbow", "wrist", "knuckles"))
    Rs, Rf, Rh = src["radii"][side]
    d1, t1, L1, u1 = seg(xx, yy, S, E)
    d2, t2, L2, u2 = seg(xx, yy, E, W)
    d3, t3, L3, u3 = seg(xx, yy, W, K)
    op = arr[..., 3] > 0
    arm = op & ((d1 <= Rs) | (d2 <= Rf + 14) | (d3 <= Rh))
    skin = skin_of(arr) & arm
    beyond = t2 > L2                                    # past the wrist line
    rim = arm & ~skin & (arr[..., 0] > 228) & (arr[..., 1] > 0.88 * arr[..., 0])
    cuff = src.get("cuff", {}).get(side, 25)           # the cuff's lower edge, px along the forearm from the elbow
    lab = np.zeros((h, w), np.uint8)
    lab[arm & ~skin & ~rim & ~beyond & (t2 < cuff)] = 1  # sleeve (nothing past the cuff: that is the forearm's rim)
    lab[skin & ~beyond] = 2                             # forearm
    lab[skin & beyond] = 3                              # fist
    return grow_labels(lab, arm), (d2, t2, L2, u2), cuff


def cut_arm(src_name, set_name, side="n"):
    src = source(src_name)
    J = {k: tuple(v) for k, v in src["joints"].items()}
    arr = picture(src)
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    S, E, W, K = (J[f"{side}_{n}"] for n in ("shoulder", "elbow", "wrist", "knuckles"))
    Rs, Rf, Rh = src["radii"][side]
    lab, (d2, t2, L2, u2), cuff = arm_labels(src, arr, side)
    d = set_dir(set_name)
    d.mkdir(parents=True, exist_ok=True)
    parts = {"_note": f"pieces.py cut {src_name}: today's look cut into pieces for bending; a/b = the piece's joints "
                      "at its bone's head/tail (piece px, scale 1 on the picture's own skeleton).",
             "source": src_name, "fist_z": 4, "sword_z": 3, "fists": {}}
    # sleeve: as drawn (its shoulder end is the round cap of the arm region)
    # the cuff's opening (where the forearm went in, enclosed by the sleeve on both sides across the forearm) takes
    # the sleeve's own darkest shade, its inside: a bend shows the open cuff, never a notch or a ball of skin
    sl = lab == 1
    across = (-u2[1], u2[0])
    enc = np.zeros_like(sl)
    zone = ~sl & (np.abs(t2 - cuff / 2) <= cuff / 2 + 2) & (d2 <= Rf + 8)
    for sgn in (1, -1):
        hit = np.zeros_like(sl)
        for k in range(1, int(Rf * 2.2)):
            sh_y, sh_x = int(round(-sgn * across[1] * k)), int(round(-sgn * across[0] * k))
            hit |= np.roll(np.roll(sl, sh_y, 0), sh_x, 1)
        enc = hit if sgn == 1 else enc & hit
    inside = zone & enc
    lum = arr[..., :3].astype(float).sum(axis=2)
    dark = np.percentile(lum[sl], 6)
    shade = np.median(arr[sl & (lum <= dark)][:, :3], axis=0) * 0.85
    # the inside is its own layer on the upper arm's bone, under the forearm: at rest the painted forearm shows in the
    # opening as in today's art; bent, the dark inside does
    arr_in = arr.copy()
    arr_in[inside, :3] = shade.astype(np.uint8)
    arr_in[inside, 3] = 255
    cin, o = piece_from(arr_in, inside, pad=2)
    save_piece(d, f"{side}_cuff", feather(cin, np.ones(cin.shape[:2], bool), 1), o, S, E, z=0, parts=parts,
               bone=f"{side}_upper")
    up, o = piece_from(arr, sl)
    up = np.asarray(open_alpha(Image.fromarray(up), 5)).copy()
    save_piece(d, f"{side}_upper", up, o, S, E, z=2, parts=parts)
    # forearm: its top completed as a round end under the cuff, its wrist end taking the fist's skin in a disc;
    # nothing of it above the cuff's edge wider than that round end (a straight arm showed a scrap there)
    fore_m = (lab == 2) | ((lab == 3) & (np.hypot(xx - W[0], yy - W[1]) <= Rf))
    fore_m &= ~((t2 < cuff) & (d2 > Rf))
    fore, o = piece_from(arr, fore_m | ((d2 <= Rf) & (t2 > -Rf) & (t2 < 60)), pad=Rf + 4)
    have = fore[..., 3] > 0
    have &= fore_m[o[1]:o[1] + fore.shape[0], o[0]:o[0] + fore.shape[1]]
    fore[~have, 3] = 0
    E0 = (E[0] - o[0], E[1] - o[1])
    top = t2[fore_m].min() if fore_m.any() else 0
    fore = complete_end(fore, have, E0, u2, Rf, -Rf, max(0.0, top + 4))
    fore = np.asarray(open_alpha(Image.fromarray(feather(fore, ~have & (fore[..., 3] > 0), 1)), 7)).copy()
    save_piece(d, f"{side}_fore", fore, o, E, W, z=1, parts=parts)
    # the drawn fist (no grip): its wrist end a disc of the forearm's skin, softened where cut
    fist_m = (lab == 3) | ((lab == 2) & (np.hypot(xx - W[0], yy - W[1]) <= Rf * 0.95))
    fist, o = piece_from(arr, fist_m)
    cutm = ((lab == 2) & fist_m)[o[1]:o[1] + fist.shape[0], o[0]:o[0] + fist.shape[1]]
    cutm = np.asarray(Image.fromarray((cutm * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
    fist = pp.largest_only(feather(fist, cutm, 1.5))
    Image.fromarray(fist).save(d / "fist_drawn.png")
    parts["fists"]["drawn"] = {"file": "fist_drawn.png", "bone": f"{side}_hand",
                               "a": [float(W[0] - o[0]), float(W[1] - o[1])],
                               "b": [float(K[0] - o[0]), float(K[1] - o[1])]}
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print("wrote", d)


def save_piece(d, name, arr, o, a, b, z, parts, bone=None):
    Image.fromarray(arr).save(d / f"{name}.png")
    parts[name] = {"file": f"{name}.png", "bone": bone or name, "a": [float(a[0] - o[0]), float(a[1] - o[1])],
                   "b": [float(b[0] - o[0]), float(b[1] - o[1])], "z": z}


# ---------------------------------------------------------------- Qwen (local ComfyUI): the fists in today's look
UP = 6                              # crops are edited at 6x (as grip_hands.py)
# crops of today's art (2x px). near: his right (near) fist, hanging by his side, the back of the hand to us.
# far_mirrored: his LEFT (far) raised fist, seen from the palm side, mirrored: a right fist seen from the palm side.
CROPS = {"near": ((10, 430, 190, 610), False), "far_mirrored": ((380, 190, 540, 350), True)}
FIST_PROMPTS = {
    "back": ("near", "Edit <image1>. It shows a cartoon hero's right fist and the lower end of his forearm, hanging down "
             "by his side, the back of the hand toward us, on a transparent background. Change only the hand: make the "
             "fist tightly grip a straight, round rod of flat, solid bright magenta color (pure #FF00FF, no shading). "
             "The rod is level and runs straight through the fist from left to right, square to the forearm: on the "
             "right it comes out of the fist between the thumb and the index finger, on the left right under the "
             "little finger, and it reaches past the left and the right edge of the picture. The four fingers curl "
             "around the rod and cover it; the thumb wraps around it over the index finger. The rod is very thick, "
             "half as tall as the whole fist, like a thick wooden staff. Keep the hand the same size and at the same "
             "place, with the same skin color, the same soft shading and the same painting style, with no dark "
             "outlines. Keep the forearm exactly the same."),
    "palm": ("far_mirrored", "Edit <image1>. It shows a cartoon hero's raised right fist and forearm seen from the "
             "palm side, on a transparent background. Change only the hand: make the fist tightly grip a straight, "
             "round rod of flat, solid bright magenta color (pure #FF00FF, no shading). The rod is held upright and "
             "runs straight through the fist, square to the curled fingers: it comes out above the fist between the "
             "thumb and the index finger and below the fist right under the little finger, and it reaches past the "
             "top and the bottom of the picture. The four fingers curl around the rod and cover it; the thumb closes "
             "over the index finger. The rod is very thick, half as wide as the whole fist, like a thick wooden staff. "
             "Keep the hand the same size and at the same place, with the same skin color, the same soft shading and "
             "the same painting style. Keep the forearm exactly the same."),
    "open": ("near", "Edit <image1>. It shows a cartoon hero's right fist and the lower end of his forearm, hanging "
             "down by his side, the back of the hand toward us, on a transparent background. Change only the hand: "
             "open it into a relaxed open hand pointing straight down, the four fingers straight and together, the "
             "thumb along the front of the hand on the right, the back of the hand toward us. Keep the hand the same "
             "size and at the same place, with the same skin color, the same soft shading and the same painting "
             "style. Keep the forearm exactly the same."),
}
SHEET_PROMPT = ("Three separate right hands of the young hero whose right hand is shown in <image1>, in exactly the same "
                "soft painted cartoon style, skin color and shading as <image1>, laid out apart side by side with wide "
                "empty space between them, nothing touching, each cut off at the wrist with a short round wrist end at "
                "the bottom, all three the same size. Hand 1: a right fist seen from the back of the hand, the "
                "knuckles in a row, tightly gripping a straight bright magenta rod (pure #FF00FF, no shading) that "
                "runs through the fist and sticks out on both sides; the thumb wraps around the rod over the index "
                "finger. Hand 2: a right fist seen from the palm side, the four fingers curled around a straight "
                "bright magenta rod that runs through the fist and sticks out on both sides, the thumb closed over "
                "them. Hand 3: an open right hand, fingers together, seen from the back. Use <image1> only for the "
                "style and the skin; draw no arm, no sleeve, no body.")


def crop_png(which):
    box, mirror = CROPS[which]
    body = Image.open(BASE).convert("RGBA")
    c = Image.fromarray(pp.largest_only(np.asarray(body.crop(box)).copy()))     # the hand only (no trousers)
    if mirror:
        c = c.transpose(Image.FLIP_LEFT_RIGHT)
    return c.resize((c.width * UP, c.height * UP), Image.LANCZOS)


def png_bytes(img, on_white=False):
    if on_white:
        bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
        bg.alpha_composite(img)
        img = bg.convert("RGB")
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


def comfy():
    from qwen_batch import Comfy  # noqa: E402
    c = Comfy(HOST)
    c.check()
    return c


def write_try(out, data, prompt, seed, note=""):
    im = Image.open(io.BytesIO(data)).convert("RGBA")
    im.putalpha(im.getchannel("A").point(lambda v: 0 if v < 10 else v))
    im.save(out)
    out.with_suffix(".txt").write_text(f"{prompt}\nseed {seed}, steps 20 {note}\n", encoding="utf-8")
    print("wrote", out, im.size)


def gen_fist(view, rnd, seeds):
    from qwen_batch import RGBA_CLOSE, RGBA_OPEN  # noqa: E402
    TRIES.mkdir(parents=True, exist_ok=True)
    which, body = FIST_PROMPTS[view]
    c = comfy()
    ref = c.upload(f"pieces_{which}.png", png_bytes(crop_png(which)))
    p = f"{RGBA_OPEN} {body} {RGBA_CLOSE}"
    for s in seeds:
        out = TRIES / f"fist_{view}_r{rnd}_s{s}.png"
        if out.exists():
            print("kept, not overwritten:", out.name)
            continue
        write_try(out, c.generate(p, [ref], None, s, 20), p, s, f"(edit of the {which} crop at {UP}x)")


def gen_sheet(rnd, seeds):
    from qwen_batch import RGBA_CLOSE, RGBA_OPEN  # noqa: E402
    TRIES.mkdir(parents=True, exist_ok=True)
    c = comfy()
    ref = c.upload("pieces_hand_ref.png", png_bytes(crop_png("near"), on_white=True))
    p = f"{RGBA_OPEN} {SHEET_PROMPT} {RGBA_CLOSE}"
    for s in seeds:
        out = TRIES / f"fistsheet_r{rnd}_s{s}.png"
        if out.exists():
            print("kept, not overwritten:", out.name)
            continue
        write_try(out, c.generate(p, [ref], (1536, 1024), s, 20), p, s, "(the near hand crop on white as style ref)")


pp.OUTLINE = (150, 92, 48)      # where the rod is taken out, the fingers' edge gets a darker skin shade (no ink)


def wrist_round(img, a, r, axis, soft=6):
    """Cut a hand's forearm off at the wrist joint a as a round end of radius r (the hand lies along axis from a);
    the cut edge is softened, not inked (today's art has no dark outlines; the fist lies over the same skin)."""
    arr = np.asarray(img.convert("RGBA")).copy()
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    along = (xx - a[0]) * axis[0] + (yy - a[1]) * axis[1]
    keep = (along >= 0) | ((xx - a[0]) ** 2 + (yy - a[1]) ** 2 <= r * r)
    blur = np.asarray(Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(soft))) / 255.0
    k = np.where(keep, np.clip((blur - 0.5) / 0.45 + 0.3, 0, 1), 0)      # ~0.3 at the cut, 1 two blurs inside
    arr[..., 3] = (arr[..., 3] * k).astype(np.uint8)
    return Image.fromarray(pp.largest_only(arr))


def open_alpha(img, size=21):
    """Morphological opening of the alpha: thin slivers left between the rod's way and the fist's edge go."""
    arr = np.asarray(img.convert("RGBA")).copy()
    m = Image.fromarray(((arr[..., 3] > 0) * 255).astype(np.uint8))
    m = np.asarray(m.filter(ImageFilter.MinFilter(size)).filter(ImageFilter.MaxFilter(size))) > 0
    arr[~m, 3] = 0
    return Image.fromarray(pp.largest_only(arr))


def fist_piece(try_name, view, a, b, blade_side, r=None, set_name=None):
    """A fist from a rod try (6x): the rod measured and taken out (puppet.rod_of), the wrist cut round, then scaled
    back to picture px. a, b: the wrist joint and the knuckles in the try's px. blade_side: +1 if the blade leaves
    at the rod's +x end (the thumb and index finger side, set by eye). Returns (piece image, parts entry)."""
    path = TRIES / f"{try_name}.png"
    which = FIST_PROMPTS[view][0] if view in FIST_PROMPTS else "near"
    crop_w = CROPS[which][0][2] - CROPS[which][0][0]
    f = crop_w / Image.open(path).width            # try px -> picture px (the edit comes back rounded to 64 px)
    axis = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
    if r is None:
        r = 0.5 * math.dist(a, b) * 0.55
    entry = {"bone": "n_hand"}
    if blade_side:
        (cx, cy), d, clean, n = pp.rod_of(path, reach=int(70 * UP / 6), line=4)
        u = (math.cos(math.radians(d)), math.sin(math.radians(d)))
        if (u[0] > 0) != (blade_side > 0):
            u = (-u[0], -u[1])
        big = wrist_round(open_alpha(clean), a, r, axis)
        t1, t2 = pp.edge_along(big, (cx, cy), u), pp.edge_along(big, (cx, cy), (-u[0], -u[1]))
        g = (cx + u[0] * (t1 - t2) / 2, cy + u[1] * (t1 - t2) / 2)
        hand = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
        entry.update({"rod": [round(g[0] * f, 1), round(g[1] * f, 1)],
                      "hold": round(sk.norm(math.degrees(math.atan2(u[1], u[0])) - hand), 1),
                      "edge": round((t1 + t2) / 2 * f, 1), "rod_px": n})
    else:
        big = wrist_round(Image.open(path), a, r, axis)
    pre = big.convert("RGBa").resize((round(big.width * f), round(big.height * f)), Image.LANCZOS).convert("RGBA")
    entry.update({"a": [round(a[0] * f, 1), round(a[1] * f, 1)], "b": [round(b[0] * f, 1), round(b[1] * f, 1)],
                  "from": try_name, "scale": 1.0})      # picture px, as drawn: never stretched to the bone
    return pre, entry


def add_fist(set_name, view, try_name, a, b, blade_side, r=None):
    d, parts = load_set(set_name)
    img, e = fist_piece(try_name, view, a, b, blade_side, r)
    e["file"] = f"fist_{view}.png"
    img.save(d / e["file"])
    parts["fists"][view] = e
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print(view, json.dumps(e))


# ---------------------------------------------------------------- the kept arm: today's near arm + the round 1 fists
# (try px of art/redo/pieces/<try>.png: wrist joint, knuckles, blade side, wrist end radius)
FIST_PICKS = {
    # back of the hand to us, hanging; thumb on the right (front): the blade leaves on the right, pommel under the
    # little finger on the left
    "back": ("fist_back_r1_s101", (445, 420), (470, 860), +1, 120),
    # his left raised fist mirrored and redrawn gripping the rod: a right fist from the palm side, thumb at the top
    # right: the blade leaves at the top
    "palm": ("fist_palm_r1_s101", (700, 690), (360, 240), +1, 140),
    "open": ("fist_open_r1_s101", (450, 420), (450, 860), 0, 120),
}


def make_arm(set_name="arm", src_name="base"):
    d = set_dir(set_name)
    if (d / "parts.json").exists():
        sys.exit(f"kept, not overwritten: {d}")
    cut_arm(src_name, set_name)
    _, parts = load_set(set_name)
    (d / "fist_drawn.png").unlink()
    parts["fists"] = {}
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    for view, (t, a, b, side, r) in FIST_PICKS.items():
        add_fist(set_name, view, t, a, b, side, r)


# ---------------------------------------------------------------- the rest of the hero, cut from the same picture
# piece: (bone, head joint, tail joint, width for the nearest-bone split, z). Pixels go to the nearest bone (distance
# over width); each piece also keeps the picture's own pixels in a disc at its head joint (its round overlapping end).
BODY = {
    "f_upper": ("f_upper", "f_shoulder", "f_elbow", 48, 1),
    "f_fore": ("f_fore", "f_elbow", "f_wrist", 34, 2),
    "f_fist": ("f_hand", "f_wrist", "f_knuckles", 44, 3),
    "f_thigh": ("f_thigh", "f_hip", "f_knee", 70, 4),
    "f_shin": ("f_shin", "f_knee", "f_ankle", 58, 3),
    "f_foot": ("f_foot", "f_ankle", "f_toe", 52, 3),
    "n_thigh": ("n_thigh", "n_hip", "n_knee", 70, 6),
    "n_shin": ("n_shin", "n_knee", "n_ankle", 58, 5),
    "n_foot": ("n_foot", "n_ankle", "n_toe", 52, 5),
    "head": ("head", "neck", "head_top", 105, 7),
    "torso": ("spine", "waist", "neck", 95, 8),
}
END_R = {"f_fore": 30, "f_fist": 26, "f_shin": 52, "n_shin": 52, "f_foot": 40, "n_foot": 40, "head": 30,
         "f_thigh": 60, "n_thigh": 60, "f_upper": 44}
NEAR_Z = 10                     # the near (sword) arm above the whole body


def cut_body(src_name, set_name):
    """Add the rest of the hero to a set that holds the near arm: everything else in the picture, piece by bone."""
    src = source(src_name)
    J = {k: tuple(v) for k, v in src["joints"].items()}
    arr = picture(src)
    h, w = arr.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    lab, _, _ = arm_labels(src, arr)
    rest = (arr[..., 3] > 0) & (lab == 0)
    names = list(BODY)
    score = np.stack([seg(xx, yy, J[BODY[n][1]], J[BODY[n][2]])[0] / BODY[n][3] for n in names])
    owner = np.argmin(score, axis=0)
    d, parts = load_set(set_name)
    for i, n in enumerate(names):
        bone, a, b, _, z = BODY[n]
        m = rest & (owner == i)
        if n in END_R:
            m |= rest & (np.hypot(xx - J[a][0], yy - J[a][1]) <= END_R[n])
        if n == "torso":            # the shoulder under the sleeve's cap stays on the torso
            Rs = src["radii"]["n"][0]
            m |= (lab == 1) & (np.hypot(xx - J["n_shoulder"][0], yy - J["n_shoulder"][1]) <= Rs)
        m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0
        m &= arr[..., 3] > 0
        pc, o = piece_from(arr, m)
        pc = pp.largest_only(pc)
        save_piece(d, n, pc, o, J[a], J[b], z, parts, bone=bone)
    for n in ("n_upper", "n_fore", "n_cuff"):
        parts[n]["z"] = parts[n]["z"] + NEAR_Z
    parts["fist_z"], parts["sword_z"] = 4 + NEAR_Z, 3 + NEAR_Z
    parts["_note"] += " Body: pieces.py body (nearest bone, round ends from the picture's own pixels)."
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print("wrote", d)


def hero_items(parts, d, pts, fist="back", sword=True):
    items = pp.arm_items(parts, d, pts, fist, sword)
    for name, pc in parts.items():
        if name in BODY:
            o, ex, ey = pp.piece_affine(pc, pts[pc["bone"]])
            items.append((pc["z"], lambda c, k, off, f=d / pc["file"], o=o, ex=ex, ey=ey: pp.draw_affine(c, pp.pic(f), o, ex, ey, k, off)))
    return items


# the sword arm standing ready: the upper arm a little forward of hanging, the forearm forward, the back of the
# fist to us, the blade up and forward; everything else as drawn
READY = {"upper": 108, "elbow": -75, "wrist": 35, "fist": "back"}   # the blade forward, under his raised left arm (clear of his left fist)


def hero_render(set_name, k, pose=None):
    d, parts = load_set(set_name)
    src = source(parts["source"])
    use_joints(src["joints"], src["hero_h"])
    p = dict(READY, **(pose or {}))
    pts = sk.pose_points(pp.arm_pose(p["upper"], p["elbow"], p["wrist"]))
    off = (-250, -100)
    size = (int(1100 * k), int(1200 * k))
    return pp.draw_items(hero_items(parts, d, pts, p["fist"]), k, off, size), off, pts


def kept(out):
    out = Path(out)
    if out.exists():
        sys.exit(f"kept, not overwritten: {out}")
    out.parent.mkdir(parents=True, exist_ok=True)
    return out


def still(set_name, out, close_out=None):
    """puppet.still's staging (BattleScreen.cs: the backdrop over 1280x720 raised by 90, ground 500, the hero at x
    330 facing right ~235 px tall, the Bog Lurker at x 950 facing left), cut off above the battle menu; 1x and 2x."""
    out = kept(out)
    big_out = kept(out.with_name(out.stem + "@2x.png"))
    S = 2
    W, H, RAISE, GROUND, CUT_H = 1280 * S, 720 * S, 90 * S, 500 * S, 600 * S
    bg = Image.open(ART / "bg_fen_road@2x.webp").convert("RGBA")
    sc = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * sc), round(bg.height * sc)), Image.LANCZOS)
    frame = Image.new("RGBA", (W, H), (20, 16, 28, 255))
    frame.alpha_composite(bg, ((W - bg.width) // 2, (H - bg.height) // 2 - RAISE))
    src = source(load_set(set_name)[1]["source"])
    # the hero picture's own height maps to 235 px (as today's hero in right2/f000)
    k = 235 * S / src["hero_h"]
    hero, off, pts = hero_render(set_name, 2.0)
    hero = hero.resize((round(hero.width * k / 2.0), round(hero.height * k / 2.0)), Image.LANCZOS)
    feet_pic = max(src["joints"]["n_toe"][1], src["joints"]["f_toe"][1]) + 12      # the boot soles, picture px
    feet = (330 * S, GROUND)
    body_x = src["joints"]["waist"][0]
    hx = feet[0] - (body_x - off[0]) * k
    hy = GROUND - (feet_pic - off[1]) * k
    mon = Image.open(ART / "mon_bog_lurker.webp").convert("RGBA").transpose(Image.FLIP_LEFT_RIGHT)
    mw = 340 * S
    mon = mon.resize((mw, round(mon.height * mw / mon.width)), Image.LANCZOS)
    mb = mon.getbbox()
    ground = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(ground)
    gd.ellipse([feet[0] - 80 * S, GROUND - 9 * S, feet[0] + 80 * S, GROUND + 7 * S], fill=(30, 22, 14, 80))
    gd.ellipse([950 * S - 150 * S, GROUND - 10 * S, 950 * S + 160 * S, GROUND + 8 * S], fill=(30, 22, 14, 80))
    frame.alpha_composite(ground.filter(ImageFilter.GaussianBlur(6 * S)))
    frame.alpha_composite(mon, (950 * S - mon.width // 2, GROUND - mb[3]))
    frame.alpha_composite(hero, (round(hx), round(hy)))
    frame = frame.crop((0, 0, W, CUT_H)).convert("RGB")
    frame.resize((W // S, CUT_H // S), Image.LANCZOS).save(out)
    frame.save(big_out)
    print("wrote", out, big_out)
    if close_out:
        close(set_name, close_out)


def close(set_name, out, k=None):
    """The sword fist close up (about 5x game size), on the backdrop's ground colour."""
    out = kept(out)
    src = source(load_set(set_name)[1]["source"])
    k = k or 3 * 235 / src["hero_h"] * 1.6
    hero, off, pts = hero_render(set_name, k)
    wrist = pts["n_hand"][0]
    c = ((wrist[0] - off[0]) * k, (wrist[1] - off[1]) * k)
    box = (int(c[0] - 300), int(c[1] - 330), int(c[0] + 300), int(c[1] + 230))
    tile = Image.new("RGBA", hero.size, (226, 218, 196, 255))
    tile.alpha_composite(hero)
    tile.crop(box).convert("RGB").save(out)
    print("wrote", out)


# ---------------------------------------------------------------- check sheets
def bends(name, out, fore_over=False, base_crop=True):
    """The sword arm at 3x game size: the elbow 0-120 deg (and the wrist turned), one row per fist; a first column
    with today's own arm at the same size (for judging; not for the designer)."""
    d, parts = load_set(name)
    src = source(parts["source"])
    use_joints(src["joints"], src["hero_h"])
    if fore_over:
        parts = dict(parts, n_fore=dict(parts["n_fore"], z=3), n_upper=dict(parts["n_upper"], z=2))
        parts["fist_z"], parts["sword_z"] = 5, 4
    k = 3 * pp.GAME_K
    rest = (round(sk.STAND["n_fore"]), round(sk.STAND["n_hand"]))       # the picture's own elbow and wrist
    cols = ([rest] if base_crop else []) + [(0, 0), (-30, 0), (-60, 0), (-90, 0), (-120, 0), (-90, -30), (-90, 40)]
    cell = (330, 380)
    rows = list(parts["fists"])
    extra = 1 if base_crop else 0
    bgc = (226, 222, 210)
    sheet = Image.new("RGB", (cell[0] * (len(cols) + extra), cell[1] * len(rows) + 40), bgc)
    dr = ImageDraw.Draw(sheet)
    if base_crop:
        J = src["joints"]
        pic = Image.open(src["file"]).convert("RGBA")
        S = J["n_shoulder"]
        box = (int(S[0] - cell[0] / k * 0.45), int(S[1] - cell[1] / k * 0.12))
        crop = pic.crop((box[0], box[1], box[0] + int(cell[0] / k), box[1] + int(cell[1] / k)))
        crop = crop.resize(cell, Image.LANCZOS)
        tile = Image.new("RGBA", cell, bgc + (255,))
        tile.alpha_composite(crop)
        sheet.paste(tile.convert("RGB"), (0, 40))
        dr.text((10, 10), "the picture's own arm", fill=(40, 30, 30), font=sk.font(18))
    for j, (el, wr) in enumerate(cols):
        dr.text(((j + extra) * cell[0] + 10, 10), f"elbow {-el}, wrist {wr}", fill=(40, 30, 30), font=sk.font(20))
    upper0 = sk.pose_points({})["n_upper"][2]
    for i, view in enumerate(rows):
        for j, (el, wr) in enumerate(cols):
            pts = sk.pose_points(pp.arm_pose(upper0, el, wr))
            sh = pts["n_upper"][0]
            off = (sh[0] - cell[0] / k * 0.45, sh[1] - cell[1] / k * 0.12)
            img = pp.draw_items(pp.arm_items(parts, d, pts, view), k, off, cell, bg=bgc + (255,))
            ImageDraw.Draw(img).text((8, cell[1] - 28), view, fill=(90, 70, 60), font=sk.font(18))
            sheet.paste(img.convert("RGB"), ((j + extra) * cell[0], 40 + i * cell[1]))
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    if Path(out).exists() and OUT in Path(out).resolve().parents:
        sys.exit(f"kept, not overwritten: {out}")
    sheet.save(out)
    print("wrote", out)


def main():
    a = sys.argv[1:]
    if not a:
        print(__doc__)
        return
    if a[0] == "cut":
        cut_arm(a[1], a[2])
    elif a[0] == "bends":
        bends(a[1], a[2], fore_over="fore_over" in a[3:], base_crop="nobase" not in a[3:])
    elif a[0] == "make":
        make_arm(a[1] if len(a) > 1 else "arm")
    elif a[0] == "body":
        cut_body(a[1], a[2])
    elif a[0] == "still":
        still(a[1], a[2], a[3] if len(a) > 3 else None)
    elif a[0] == "close":
        close(a[1], a[2])
    elif a[0] == "fist":
        gen_fist(a[1], a[2], [int(x) for x in a[3].split(",")])
    elif a[0] == "fistsheet":
        gen_sheet(a[1], [int(x) for x in a[2].split(",")])
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
