#!/usr/bin/env python3
"""The hero as a paper cut-out puppet: pieces DRAWN for bending, on the clean skeleton (PLANS "The hero's animation
plan", step 1; the designer: "Maybe we should stylize our characters").

Pieces come from generated parts sheets (local Qwen-Image 2.1, no removal edits): every piece is drawn whole, on its
own, straight, with round ends. `cut` splits a sheet into pieces by their outlines. A piece rides one bone of
scripts/skeleton.py rigidly: its own two joint anchors (drawn into its parts.json) are laid onto the bone's two ends
(turn + uniform scale, optionally widened across the bone by "wide"), so nothing is turned to follow anything else,
and nothing is mirrored (a left hand is a right fist picture mirrored once, as its own piece). The hand is a bone;
the sword is fixed to the hand bone at the fist picture's own grip: each fist was drawn gripping a magenta rod, the
rod's line gives the grip point ("rod") and the blade's turn from the hand bone ("hold", per fist), and the rod is
then taken out (where it lay over the fingers it becomes skin). The blade side is set by eye (thumb and first
finger); the guard sits against that side of the fist, under it. Draw order: forearm, sleeve, sword, fist.

  python scripts/puppet.py gen <sheet> <round> <seed>[,<seed>...] [--ref none|hero|<sheet.png>[+hero]]   tries -> art/redo/puppet/
  python scripts/puppet.py cut <try.png>                 its pieces -> art/redo/puppet/cut/<try>/ + a numbered sheet
  python scripts/puppet.py make [arm]                    puppet_arm from ARM_PICKS / FIST_PICKS (the kept tries)
  python scripts/puppet.py body [hero]                   puppet_hero: that arm + its far copy + BODY_PICKS
  python scripts/puppet.py bends <set> <out.png>         the sword arm at 3x: elbow 0-120 deg, each fist
  python scripts/puppet.py still <set> <out.png> [<close.png>]   game size on the Fen Road with the Bog Lurker

A set is unity/Bones/hero/puppet_<name>/parts.json: {piece: {"file", "bone", "a": [x, y], "b": [x, y], "z",
"wide"?, "scale"?}} with a and b the piece's joint at the bone's head and tail (piece px); "fists": {back|palm|open:
{..., "rod": [x, y] (grip point), "hold" (deg), "edge" (grip point to the fist's outline along the blade, piece px)}},
"fist_z", "sword_z". (Not bone_art.py's format: one "_hold" can't describe three fists.)
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import skeleton as sk  # noqa: E402

ROOT = sk.ROOT
TRIES = ROOT / "art" / "redo" / "puppet"
OUT = ROOT / "art" / "tests" / "sheets" / "puppet"
ART = ROOT / "src" / "assets" / "art"
SWORD = ART / "wpn_iron_sword@2x.webp"      # the same picture as unity/Bones/hero/iron_sword.png (1 px trim)
SWORD_GRIP = (72.0, 500.0)                  # the handle's middle, blade up, as rig.json's iron_sword
HOST = "http://127.0.0.1:8188"

RGBA_OPEN = "This is an RGBA format image with transparency."
RGBA_CLOSE = "The image has an alpha channel and a transparent background."
STYLE = ("Style: a flat paper cut-out puppet for a 2D game, like pieces cut from coloured construction paper: flat "
         "solid colours, crisp clean cut edges with a thin dark brown outline, details as a few simple paper layers "
         "and bold lines, no gradients, no painted light, no shading, no cast shadows, no paper texture noise.")
REF_LINE = ("The character is the young adventurer in <image1>: use <image1> only for his colours and clothes (the "
            "cream linen shirt, the tanned skin); do not draw him whole and do not copy his pose.")

SHEETS = {
    # the sword arm: sleeve and forearm, each whole and straight
    "arm": ("A parts sheet for a paper cut-out puppet of a young adventurer: the separate pieces of his right arm, "
            "laid out apart side by side with wide empty space between them, nothing touching or overlapping. "
            "Piece 1, the upper arm: a straight, upright sleeve of a cream linen shirt, wide and full, a big round "
            "cap at the shoulder end at the top, and at the elbow end at the bottom a rolled-up cuff, the cuff's "
            "bottom edge rounded. Piece 2, the forearm: a straight, upright bare forearm with tanned skin, shaped "
            "like a capsule with both ends fully rounded, a little wider at the elbow end, no hand on it, no sleeve "
            "on it. Both pieces drawn straight and unbent, seen from the side, same scale, the forearm a little "
            "longer than the upper arm."),
    # the fists: big, bold, simple; each grips a magenta rod that gives the sword's line
    # round 2: the arm in the kept fists' style and scale (the fist sheet is <image1>)
    "arm2": ("Draw the matching arm pieces for the same paper cut-out puppet as the hands in <image1>, in exactly the "
             "same art style: the same bold dark brown outline, the same flat colours with no shading, the same "
             "tanned skin colour. Draw no hands. The pieces of his right arm, laid out apart side by side with wide "
             "empty space between them, nothing touching or overlapping, both upright and straight. "
             "Piece 1, the upper arm: a sleeve of a cream linen shirt, a straight tube about two and a half times as "
             "long as it is wide, a round cap at the shoulder end at the top, and at the elbow end at the bottom a "
             "neat rolled-up cuff, its bottom edge rounded. Piece 2, the forearm: a bare forearm with tanned skin, "
             "a straight tube about three times as long as it is wide, both ends fully rounded like a capsule, a "
             "little wider at the elbow end at the top, its narrow end as wide as the wrists of the hands in "
             "<image1>. No hand on it, no sleeve on it. Both pieces at the same scale as the hands in <image1>."),
    # the rest of the hero, in the kept fists' style (<image1>); <image2> (when given) is the hero himself
    "body": ("Draw the other pieces of the same paper cut-out puppet as the hands in <image1>, in exactly the same "
             "art style: the same bold dark brown outline, the same flat colours with no shading, the same tanned "
             "skin. Draw no hands. PUPPET_WHO The pieces are laid out apart with wide empty space between them, "
             "nothing touching or overlapping, each piece whole: "
             "1. the head with the neck, turned to the right in three-quarter view: spiky dark brown hair, a red "
             "headband with its two knot tails flying at the back, a determined look, a small confident smile; "
             "2. the torso: a cream linen shirt with an open collar and a short placket, from the neck down to a "
             "brown leather belt with a square brass buckle, no arms, the shoulders rounded off; "
             "3. two thighs: straight upright dark teal trouser legs, rounded at both ends; "
             "4. two lower legs: a straight upright dark teal trouser leg tucked into a tall brown leather boot, the "
             "boot's foot pointing to the right. Same scale as the hands in <image1>."),
    "fists": ("A parts sheet for a paper cut-out puppet of a young adventurer: three separate right hands with "
              "tanned skin, laid out apart side by side with wide empty space between them, nothing touching. "
              "Each hand is cut off at the wrist with a fully rounded wrist end, wrist at the bottom. "
              "Hand 1: a right fist seen from the back of the hand, the big knuckles in a row, the fist tightly "
              "gripping a straight bright magenta rod that runs through the fist and sticks out on both sides; "
              "the thumb wraps around the rod next to the first finger. "
              "Hand 2: a right fist seen from the palm side, the four fingers curled around a straight bright "
              "magenta rod that runs through the fist and sticks out on both sides, the thumb closed over them. "
              "Hand 3: an open right hand, fingers together, seen from the back. Big, chunky, simple cartoon hands "
              "that read at a small size, all three the same size."),
}


def comfy():
    from qwen_batch import Comfy  # noqa: E402
    c = Comfy(HOST)
    c.check()
    return c


def hero_ref_png():
    import io
    art = Image.open(ART / "char_hero_bare@2x.webp").convert("RGBA")
    bg = Image.new("RGBA", art.size, (255, 255, 255, 255))
    bg.alpha_composite(art)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def prompt_of(sheet, ref):
    body = SHEETS[sheet]
    return f"{RGBA_OPEN} {body} {REF_LINE if ref else ''} {STYLE} {RGBA_CLOSE}".replace("  ", " ")


def sheet_ref_png(path):
    import io
    art = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", art.size, (255, 255, 255, 255))
    bg.alpha_composite(art)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def gen(sheet, rnd, seeds, ref):
    """ref: False (none), True (the hero, colours only) or a path (a kept sheet as <image1>, named in the prompt)."""
    TRIES.mkdir(parents=True, exist_ok=True)
    c = comfy()
    if isinstance(ref, str):
        sheet_png, _, who = ref.partition("+")
        refs = [c.upload("puppet_sheet_ref.png", sheet_ref_png(sheet_png))]
        if who == "hero":
            refs.append(c.upload("puppet_hero_ref.png", hero_ref_png()))
            w = ("The puppet is the young adventurer in <image2>: take his face, hair, headband and clothes from "
                 "<image2>, but draw him only as separate flat paper pieces in the style of <image1>.")
        else:
            w = ("The puppet is a young adventurer with tanned skin.")
        p = f"{RGBA_OPEN} {SHEETS[sheet].replace('PUPPET_WHO', w)} {STYLE} {RGBA_CLOSE}"
    else:
        refs = [c.upload("puppet_hero_ref.png", hero_ref_png())] if ref else []
        p = prompt_of(sheet, ref)
    print(p)
    for s in seeds:
        tag = ("sheethero" if ref.endswith("+hero") else "sheet") if isinstance(ref, str) else "ref" if ref else "noref"
        out = TRIES / f"{sheet}_r{rnd}_{tag}_s{s}.png"
        if out.exists():
            print("kept, not overwritten:", out.name)
            continue
        data = c.generate(p, refs, (1536, 1024), s, 30)
        out.write_bytes(data)
        print("wrote", out)


# ---------------------------------------------------------------- cutting a sheet into pieces
def alpha_of(img):
    a = np.asarray(img.convert("RGBA"))[..., 3].copy()
    a[a < 10] = 0                               # Qwen leaves 1-9 alpha on a third of the background
    return a


def components(mask, min_px=800):
    """Connected pieces of a mask (4-neighbour flood fill), biggest first: [(ys, xs)]."""
    from collections import deque
    h, w = mask.shape
    seen = np.zeros_like(mask, bool)
    out = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        q = deque([(y0, x0)])
        seen[y0, x0] = True
        ys, xs = [], []
        while q:
            y, x = q.popleft()
            ys.append(y)
            xs.append(x)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
        if len(ys) >= min_px:
            out.append((np.array(ys), np.array(xs)))
    return sorted(out, key=lambda c: -len(c[0]))


def cut(try_png):
    img = Image.open(try_png).convert("RGBA")
    a = alpha_of(img)
    parts = components(a > 0)
    d = TRIES / "cut" / Path(try_png).stem
    d.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGBA", img.size, (205, 205, 210, 255))
    arr = np.asarray(img).copy()
    arr[..., 3] = a
    sheet.alpha_composite(Image.fromarray(arr))
    dr = ImageDraw.Draw(sheet)
    for i, (ys, xs) in enumerate(parts):
        x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        piece = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
        piece[ys - y0, xs - x0] = arr[ys, xs]
        Image.fromarray(piece).save(d / f"p{i}.png")
        dr.rectangle([x0, y0, x1, y1], outline=(255, 0, 0), width=2)
        dr.text((x0 + 4, y0 + 2), f"p{i} {x1 - x0}x{y1 - y0}", fill=(255, 0, 0), font=sk.font(22))
        print(f"p{i}: box {x0},{y0} {x1 - x0}x{y1 - y0}, {len(ys)} px")
    sheet.convert("RGB").save(d / "_sheet.jpg", quality=90)
    print("pieces:", len(parts), "->", d)


def rod_of(piece_png, reach=70, line=5):
    """The magenta rod in a fist piece: its middle and direction (deg, image axes), and the piece with it taken out.
    Rod pixels with fist on both sides across the rod (where it passed in front of the fingers or thumb) become skin;
    the rest is cut away, and the fist's edge where the rod left it gets its outline back."""
    img = Image.open(piece_png).convert("RGBA")
    arr = np.asarray(img).astype(int)
    r, g, b, al = arr[..., 0], arr[..., 1], arr[..., 2], arr[..., 3]
    core = (al > 0) & (r > 150) & (b > 110) & (g < 0.7 * np.minimum(r, b))     # the rod's bright magenta
    ys, xs = np.nonzero(core)
    cx, cy = xs.mean(), ys.mean()
    cov = np.cov(np.vstack([xs - cx, ys - cy]))
    ev, evec = np.linalg.eigh(cov)
    vx, vy = evec[:, -1]
    px, py = -vy, vx                                       # across the rod
    hh, ww = al.shape
    yy, xx = np.mgrid[0:hh, 0:ww]
    across = np.abs((xx - cx) * px + (yy - cy) * py)
    half = np.percentile(np.abs((xs - cx) * px + (ys - cy) * py), 99)
    # the rod with its purple outline and soft edge (skin and ink have b < g), only in a band around its line
    mag = (al > 0) & (b > g + 8) & (r > g + 8) & (across <= half + 6)
    fist = (al > 0) & ~mag
    h, w = mag.shape

    def hits(y, x, sgn):
        for t in range(1, reach):
            yy, xx = int(round(y + sgn * py * t)), int(round(x + sgn * px * t))
            if not (0 <= yy < h and 0 <= xx < w) or al[yy, xx] == 0:
                return False
            if fist[yy, xx]:
                return True
        return False
    inside = np.zeros_like(mag)
    for y, x in zip(*np.nonzero(mag)):
        inside[y, x] = hits(y, x, 1) and hits(y, x, -1)
    skin = np.median(arr[fist & (r > 180) & (g > 120) & (b < g)][:, :3], axis=0)
    out = arr.copy()
    out[inside, :3] = skin
    # the soft seam round the filled part (pinkish pixels just outside the test) takes the skin too
    seam = np.asarray(Image.fromarray((inside * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 0
    seam &= ~inside & fist & (arr[..., :3].sum(axis=2) > 330)
    out[seam, :3] = skin
    gone = mag & ~inside
    out[gone, 3] = 0
    near = np.asarray(Image.fromarray((gone * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * line + 1))) > 0
    ring = near & ~gone & (out[..., 3] > 0)
    out[ring, :3] = OUTLINE
    out[ring, 3] = 255
    out = largest_only(out.astype(np.uint8))
    return (cx, cy), math.degrees(math.atan2(vy, vx)), Image.fromarray(out), int(mag.sum())


# ---------------------------------------------------------------- a set: pieces + their anchors
BONES = sk.BONES
OUTLINE = (78, 44, 22)          # the pieces' own dark brown outline (sampled off the kept fist sheet)


def largest_only(arr):
    """Only the biggest connected piece of an RGBA array stays (stray bits of a removed rod go)."""
    comps = components(arr[..., 3] > 0, min_px=1)
    keep = np.zeros(arr.shape[:2], bool)
    keep[comps[0]] = True
    arr[~keep, 3] = 0
    return arr


def trim_wrist(img, a, r, axis=(0.0, -1.0), line=6):
    """Cut a hand's wrist stub to a round end of radius r centred on the wrist joint a (the hand lies along axis
    from a), and draw the outline along the new edge (the whole round end and its sides, one even width)."""
    arr = np.asarray(img.convert("RGBA")).copy()
    h, w = arr.shape[:2]
    if r is None:                       # the stub's own half width at the joint, centred on it (no ledge at the turn)
        xs = np.nonzero(arr[int(a[1]), :, 3] > 0)[0]
        a = ((xs.min() + xs.max()) / 2, a[1])
        r = (xs.max() - xs.min() + 1) / 2
    yy, xx = np.mgrid[0:h, 0:w]
    along = (xx - a[0]) * axis[0] + (yy - a[1]) * axis[1]
    keep = (along >= 0) | ((xx - a[0]) ** 2 + (yy - a[1]) ** 2 <= r * r)
    new = (arr[..., 3] > 0) & keep
    inner = np.asarray(Image.fromarray((new * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(2 * line + 1))) > 0
    ring = new & ~inner & (along < line)
    arr[ring, :3] = OUTLINE
    arr[ring, 3] = 255
    arr[~new, 3] = 0
    return Image.fromarray(largest_only(arr))


def edge_along(img, c, u, step=0.5):
    """How far from c along u the piece's (alpha) outline is."""
    a = np.asarray(img)[..., 3]
    d = 0.0
    while True:
        x, y = int(round(c[0] + u[0] * d)), int(round(c[1] + u[1] * d))
        if not (0 <= y < a.shape[0] and 0 <= x < a.shape[1]) or a[y, x] < 128:
            return d
        d += step


LAYER_SHADOW = (3, 5, 4, 0.45)  # rig px offset x, y, blur, strength: each piece's shadow on the layers under it
SWORD_GUARD = 452.0             # the guard's lower edge, sword px


def make_fist(src_png, out_png, a, b, blade_side, r):
    """A fist piece from a cut sheet piece: the magenta rod measured and taken out, the wrist stub cut round.
    blade_side: +1 if the blade leaves the fist at the rod's +x end (set by eye: the thumb and first finger side).
    Returns its parts.json entry (rod = the grip point in piece px, hold = the blade's turn from the hand bone)."""
    (cx, cy), d, clean, n = rod_of(src_png)
    u = (math.cos(math.radians(d)), math.sin(math.radians(d)))
    if (u[0] > 0) != (blade_side > 0):
        u = (-u[0], -u[1])
    hand = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    axis = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
    piece = trim_wrist(clean, a, r, axis)
    # the grip point: on the rod's line, half way between the fist's two outline crossings
    t1, t2 = edge_along(piece, (cx, cy), u), edge_along(piece, (cx, cy), (-u[0], -u[1]))
    g = (cx + u[0] * (t1 - t2) / 2, cy + u[1] * (t1 - t2) / 2)
    edge = (t1 + t2) / 2
    piece.save(out_png)
    return {"file": Path(out_png).name, "bone": "n_hand", "a": list(a), "b": list(b),
            "rod": [round(g[0], 1), round(g[1], 1)],
            "hold": round(sk.norm(math.degrees(math.atan2(u[1], u[0])) - hand), 1), "edge": round(edge, 1), "rod_px": n}


def load_set(name):
    d = BONES / f"puppet_{name}"
    return d, json.loads((d / "parts.json").read_text())


_pics = {}


def pic(path):
    path = str(path)
    if path not in _pics:
        _pics[path] = Image.open(path).convert("RGBA").convert("RGBa")    # premultiplied: clean edges
    return _pics[path]


def piece_affine(piece, bone_pts):
    """Piece px -> rig px for a piece on its bone: a on the bone's head, a->b along the bone, uniform scale (or
    piece["scale"]), widened across the bone by piece["wide"]. Returns (o, ex, ey): rig = o + px*ex + py*ey."""
    h, t, w = bone_pts
    a, b = piece["a"], piece["b"]
    s = piece.get("scale") or math.dist(h, t) / math.dist(a, b)
    wide = piece.get("wide", 1.0)
    pa = math.atan2(b[1] - a[1], b[0] - a[0])
    ba = math.radians(w)
    al = (math.cos(pa), math.sin(pa))
    ac = (-al[1], al[0])
    bl = (math.cos(ba), math.sin(ba))
    bc = (-bl[1], bl[0])
    # px - a = A*al + C*ac  ->  rig = h + s*A*bl + s*wide*C*bc  (a turn and a scale: never mirrored)
    ex = (s * al[0] * bl[0] + s * wide * ac[0] * bc[0], s * al[0] * bl[1] + s * wide * ac[0] * bc[1])
    ey = (s * al[1] * bl[0] + s * wide * ac[1] * bc[0], s * al[1] * bl[1] + s * wide * ac[1] * bc[1])
    o = (h[0] - a[0] * ex[0] - a[1] * ey[0], h[1] - a[0] * ex[1] - a[1] * ey[1])
    return o, ex, ey


def draw_affine(canvas, src, o, ex, ey, k, off):
    """Draw src with rig = o + px*ex + py*ey onto canvas (out px = (rig - off) * k)."""
    A, B, C, D = k * ex[0], k * ey[0], k * ex[1], k * ey[1]
    det = A * D - B * C
    ia, ib, ic, id_ = D / det, -B / det, -C / det, A / det
    tx, ty = k * (o[0] - off[0]), k * (o[1] - off[1])
    data = (ia, ib, -(ia * tx + ib * ty), ic, id_, -(ic * tx + id_ * ty))
    layer = src.transform(canvas.size, Image.AFFINE, data, resample=Image.BILINEAR).convert("RGBA")
    if LAYER_SHADOW and canvas.getbbox():
        # the paper layer's soft shadow on the pieces under it (only where there is something under it)
        dx, dy, blur, al = LAYER_SHADOW
        a = layer.split()[3].filter(ImageFilter.GaussianBlur(max(0.5, blur * k)))
        under = canvas.split()[3]
        sh = Image.new("RGBA", canvas.size, (30, 22, 14, 0))
        from PIL import ImageChops
        sh.putalpha(ImageChops.multiply(a.point(lambda v: int(v * al)), under))
        moved = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        moved.alpha_composite(sh, (int(round(dx * k)), int(round(dy * k))))
        canvas.alpha_composite(moved)
    canvas.alpha_composite(layer)


def sword_affine(fist, fo, fex, fey):
    """The sword on the fist: its handle along the rod line, the guard against the fist's thumb-side outline (tucked
    a little under it), the blade out of the thumb side. Only the fist's own transform (the hand bone) moves it."""
    a, b = fist["a"], fist["b"]
    hand = math.atan2(b[1] - a[1], b[0] - a[0])
    bl = hand + math.radians(fist["hold"])
    u = (math.cos(bl), math.sin(bl))                     # blade direction, piece px
    g = fist["rod"]
    e = fist["edge"] - fist.get("tuck", 10)
    gp = (g[0] + u[0] * e, g[1] + u[1] * e)              # the guard's lower edge, piece px
    P = lambda p: (fo[0] + p[0] * fex[0] + p[1] * fey[0], fo[1] + p[0] * fex[1] + p[1] * fey[1])
    G, U = P(gp), P((gp[0] + u[0], gp[1] + u[1]))
    U = (U[0] - G[0], U[1] - G[1])
    n = math.hypot(*U)
    U = (U[0] / n, U[1] / n)                             # blade direction on the rig (the sword is drawn 1:1)
    ey = (-U[0], -U[1])                                  # sword px up -> U
    ex = (-U[1], U[0])                                   # sword px across -> U turned, so it is never mirrored
    o = (G[0] - SWORD_GRIP[0] * ex[0] - SWORD_GUARD * ey[0], G[1] - SWORD_GRIP[0] * ex[1] - SWORD_GUARD * ey[1])
    return o, ex, ey


def arm_items(parts, d, pts, fist_name, sword=True):
    """[(z, draw(canvas, k, off))] for the near arm: sleeve, forearm, fist, and the sword fixed in the fist."""
    out = []
    for name in ("n_upper", "n_fore"):
        pc = parts[name]
        o, ex, ey = piece_affine(pc, pts[pc["bone"]])
        out.append((pc["z"], lambda c, k, off, f=d / pc["file"], o=o, ex=ex, ey=ey: draw_affine(c, pic(f), o, ex, ey, k, off)))
    fist = parts["fists"][fist_name]
    o, ex, ey = piece_affine(fist, pts["n_hand"])
    out.append((parts["fist_z"], lambda c, k, off, f=d / fist["file"], o=o, ex=ex, ey=ey: draw_affine(c, pic(f), o, ex, ey, k, off)))
    if sword and fist.get("hold") is not None:
        so, sex, sey = sword_affine(fist, o, ex, ey)
        out.append((parts["sword_z"], lambda c, k, off, so=so, sex=sex, sey=sey: draw_affine(c, pic(SWORD), so, sex, sey, k, off)))
    return out


def draw_items(items, k, off, size, bg=None):
    img = Image.new("RGBA", size, bg or (0, 0, 0, 0))
    for _, f in sorted(items, key=lambda i: i[0]):
        f(img, k, off)
    return img


def arm_pose(upper, elbow, wrist, base=None):
    """Local angles for the near arm: the upper arm's world angle (deg, image axes, 90 = straight down), the elbow
    bend (0 straight, negative folds the forearm forward) and the wrist (local, 0 straight on from the forearm)."""
    base = dict(base or {})
    collar = sk.pose_points(base)["n_collar"][2]
    base.update({"n_upper": sk.norm(upper - collar), "n_fore": elbow, "n_hand": wrist})
    return base



# ---------------------------------------------------------------- the kept tries (picked by eye, art/redo/puppet)
CUT = TRIES / "cut"
ARM_PICKS = {
    # sleeve and forearm: round 2 (the arm drawn in the kept fists' style), seed 303
    "n_upper": (CUT / "arm2_r2_sheet_s303" / "p1.png", {"a": [86, 75], "b": [86, 530], "wide": 1.5, "z": 2}),
    "n_fore": (CUT / "arm2_r2_sheet_s303" / "p2.png", {"a": [52, 52], "b": [52, 455], "wide": 1.25, "z": 1}),
}
# fists: round 1, seed 201 (the hero as a colour reference): (piece, wrist a, knuckles b, blade side, wrist radius)
# back: a right fist, back of the hand to us, thumb wrapping the rod on the left -> the blade leaves on the left
# palm: a right fist, palm to us, thumb across the fingers on the right -> the blade leaves on the right
FIST_PICKS = {
    "back": (CUT / "fists_r1_ref_s201" / "p2.png", (245, 320), (238, 30), -1, None),
    "palm": (CUT / "fists_r1_ref_s201" / "p1.png", (207, 320), (213, 30), +1, None),
    "open": (CUT / "fists_r1_ref_s201" / "p0.png", (195, 440), (175, 30), 0, None),
}
FIST_SCALE = 0.37       # the fist across the rod ~110 rig px: the guard against the thumb, the pommel under the little finger


def make_arm(name="arm"):
    d = BONES / f"puppet_{name}"
    d.mkdir(parents=True, exist_ok=True)
    parts = {"_note": "puppet.py make: the sword arm drawn for bending (paper cut-out pieces, local Qwen). a/b: the "
                      "piece's joints at its bone's head/tail (piece px). A fist's rod: its grip point; hold: the "
                      "blade's turn from the hand bone (deg).", "fist_z": 4, "sword_z": 3, "fists": {}}
    for part, (src, entry) in ARM_PICKS.items():
        f = d / f"{part}.png"
        if not f.exists():
            Image.open(src).save(f)
        parts[part] = dict(entry, file=f.name, bone=part)
    for view, (src, a, b, side, r) in FIST_PICKS.items():
        f = d / f"fist_{view}.png"
        if side:
            e = make_fist(src, f, a, b, side, r)
        else:
            axis = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
            trim_wrist(Image.open(src), a, r, axis).save(f)
            e = {"file": f.name, "bone": "n_hand", "a": list(a), "b": list(b)}
        e["scale"] = FIST_SCALE
        parts["fists"][view] = e
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print(json.dumps(parts["fists"], indent=1))


# ---------------------------------------------------------------- check sheets
def bends(name, out):
    """The sword arm at 3x game size: the elbow 0-120 deg, each fist; the wrist turned in the last column."""
    d, parts = load_set(name)
    k = 3 * GAME_K
    cols = [(0, 0), (-30, 0), (-60, 0), (-90, 0), (-120, 0), (-90, -30), (-90, 40)]
    cell = (330, 380)
    rows = ["back", "palm", "open"]
    sheet = Image.new("RGB", (cell[0] * len(cols), cell[1] * len(rows) + 40), (232, 226, 210))
    dr = ImageDraw.Draw(sheet)
    for j, (el, wr) in enumerate(cols):
        dr.text((j * cell[0] + 10, 10), f"elbow {-el}, wrist {wr}", fill=(40, 30, 30), font=sk.font(20))
    for i, view in enumerate(rows):
        for j, (el, wr) in enumerate(cols):
            pts = sk.pose_points(arm_pose(100, el, wr))
            sh = pts["n_upper"][0]
            off = (sh[0] - cell[0] / k * 0.45, sh[1] - cell[1] / k * 0.12)
            img = draw_items(arm_items(parts, d, pts, view), k, off, cell, bg=(232, 226, 210, 255))
            ImageDraw.Draw(img).text((8, cell[1] - 28), view, fill=(90, 70, 60), font=sk.font(18))
            sheet.paste(img.convert("RGB"), (j * cell[0], 40 + i * cell[1]))
    OUT.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print("wrote", out)


GAME_K = 235 / 1130     # rig px -> game px (the hero ~235 px tall in a 1280x720 battle, right2/f000)


# ---------------------------------------------------------------- the whole hero
# round 3: the body drawn in the kept fists' style (no hero reference: with it Qwen copied his painted look), seed 411
BODY_SHEET = CUT / "body_r3_sheet_s411"
BODY_PICKS = {
    # piece: (cut piece, a, b, wide, z); a/b on the bone's head/tail
    "head": ("p0.png", [178, 250], [172, -12], 1.0, 9),       # a above the stub's end: the neck tucks into the collar
    "torso": ("p1.png", [108, 200], [108, 0], 0.68, 10),      # rides the spine (waist -> neck)
    "n_thigh": ("p4.png", [57, 57], [57, 294], 1.0, 8),
    "n_shin": ("p2.png", [45, 32], [48, 292], 1.0, 7),
    "f_thigh": ("p5.png", [57, 57], [57, 286], 1.0, 6),
    "f_shin": ("p3.png", [45, 32], [48, 292], 1.0, 5),
}
BONE_FOR = {"torso": "spine"}
FAR_SHADE = 0.8           # the far limbs a step darker, as cut-out puppets show depth


def shade(img, f):
    arr = np.asarray(img.convert("RGBA")).astype(float)
    arr[..., :3] *= f
    return Image.fromarray(arr.clip(0, 255).astype(np.uint8))


def make_body(name="hero", arm="arm"):
    """The whole bare hero in pieces: the arm set's near arm, the same pieces a step darker for his left (far) arm
    with the palm fist mirrored (a left fist), and the body sheet's head, torso and legs."""
    d = BONES / f"puppet_{name}"
    d.mkdir(parents=True, exist_ok=True)
    ad, ap = load_set(arm)
    parts = {"_note": "puppet.py body: the whole bare hero as paper cut-out pieces. Near arm = his right (the "
                      "sword arm), from puppet_" + arm + ". a/b: the piece's joints at its bone's head/tail.",
             "fist_z": 14, "sword_z": 13, "fists": {}}
    for part in ("n_upper", "n_fore"):
        f = d / ap[part]["file"]
        Image.open(ad / ap[part]["file"]).save(f)
        parts[part] = dict(ap[part], z=ap[part]["z"] + 10)
        far = "f_" + part[2:]
        shade(Image.open(ad / ap[part]["file"]), FAR_SHADE).save(d / f"{far}.png")
        parts[far] = dict(ap[part], file=f"{far}.png", bone=far, z=ap[part]["z"])
    for view, e in ap["fists"].items():
        Image.open(ad / e["file"]).save(d / e["file"])
        parts["fists"][view] = e
    # his left fist: the right palm-side fist mirrored (a left hand seen from the palm side), darker
    pf = ap["fists"]["palm"]
    src = Image.open(ad / pf["file"])
    W = src.width
    shade(src.transpose(Image.FLIP_LEFT_RIGHT), FAR_SHADE).save(d / "f_fist.png")
    parts["f_fist"] = {"file": "f_fist.png", "bone": "f_hand", "a": [W - 1 - pf["a"][0], pf["a"][1]],
                       "b": [W - 1 - pf["b"][0], pf["b"][1]], "scale": pf["scale"], "z": 3}
    for part, (f, a, b, wide, z) in BODY_PICKS.items():
        img = Image.open(BODY_SHEET / f)
        if part.endswith("_shin"):                       # the flat trouser top cut round about the knee
            axis = ((b[0] - a[0]) / math.dist(a, b), (b[1] - a[1]) / math.dist(a, b))
            img = trim_wrist(img, a, None, axis)
        if part.startswith("f_"):
            img = shade(img, FAR_SHADE)
        img.save(d / f"{part}.png")
        parts[part] = {"file": f"{part}.png", "bone": BONE_FOR.get(part, part), "a": a, "b": b, "wide": wide, "z": z}
    (d / "parts.json").write_text(json.dumps(parts, indent=1))
    print("wrote", d)


def hero_items(parts, d, pts, fist="back", sword=True):
    items = arm_items(parts, d, pts, fist, sword)
    for name, pc in parts.items():
        if name.startswith("_") or name in ("fists", "fist_z", "sword_z", "n_upper", "n_fore"):
            continue
        o, ex, ey = piece_affine(pc, pts[pc["bone"]])
        items.append((pc["z"], lambda c, k, off, f=d / pc["file"], o=o, ex=ex, ey=ey: draw_affine(c, pic(f), o, ex, ey, k, off)))
    return items


def ready_pose():
    """Standing ready: the base drawing's legs, body and raised left fist; his right arm down by his side, the elbow
    bent so the forearm points forward, the back of the fist to us, the blade up and forward."""
    legs = {"n_thigh": sk.STAND["n_thigh"] + 6, "n_shin": 4, "f_thigh": sk.STAND["f_thigh"] - 14, "f_shin": 16}
    return arm_pose(108, -80, 4, base=legs)


def paper_shadow(img, k, dx=5, dy=7, blur=4, alpha=0.28):
    """One soft shadow under the whole figure on the backdrop (the backdrop's paper layers have the same)."""
    a = img.split()[3].filter(ImageFilter.GaussianBlur(blur * k))
    sh = Image.new("RGBA", img.size, (30, 22, 14, 0))
    sh.putalpha(a.point(lambda v: int(v * alpha)))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(sh, (int(dx * k), int(dy * k)))
    out.alpha_composite(img)
    return out


def hero_render(name, pose, k, fist="back"):
    """The hero on a transparent canvas; returns (image, rig offset)."""
    d, parts = load_set(name)
    pts = sk.pose_points(pose)
    off = (150, 0)
    size = (int(1500 * k), int(1500 * k))
    img = draw_items(hero_items(parts, d, pts, fist), k, off, size)
    from PIL import ImageEnhance
    a = img.split()[3]
    img = ImageEnhance.Color(img.convert("RGB")).enhance(HERO_SAT).convert("RGBA")   # a step towards the backdrop's muted paper
    img.putalpha(a)
    return img, off, pts, parts


HERO_SAT = 0.85


def still(name, out, close_out=None):
    """At game size: the battle's own staging (BattleScreen.cs: the backdrop covers 1280x720 raised by BgRaise 90,
    GroundY 500, the hero at x 330 facing right, the monster at x 950 facing left, the hero ~235 px tall as in
    right2/f000), cut off above where the battle menu starts. Saved at 1x and 2x."""
    S = 2                                                                     # work at 2x (the @2x backdrop)
    W, H, RAISE, GROUND, CUT_H = 1280 * S, 720 * S, 90 * S, 500 * S, 600 * S
    bg = Image.open(ART / "bg_fen_road@2x.webp").convert("RGBA")
    sc = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * sc), round(bg.height * sc)), Image.LANCZOS)
    frame = Image.new("RGBA", (W, H), (20, 16, 28, 255))
    frame.alpha_composite(bg, ((W - bg.width) // 2, (H - bg.height) // 2 - RAISE))
    hero, off, pts, parts = hero_render(name, ready_pose(), 1.0)
    bb = hero.getbbox()
    k = 235 * S / (bb[3] - bb[1])
    hero = hero.crop(bb)
    hero = hero.resize((round(hero.width * k), round(hero.height * k)), Image.LANCZOS)
    hero = paper_shadow(hero, S / 2)
    feet = (330 * S, GROUND)
    mon = Image.open(ART / "mon_bog_lurker.webp").convert("RGBA").transpose(Image.FLIP_LEFT_RIGHT)  # faces left
    mw = 340 * S
    mon = mon.resize((mw, round(mon.height * mw / mon.width)), Image.LANCZOS)
    mb = mon.getbbox()
    ground = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(ground)
    gd.ellipse([feet[0] - 80 * S, GROUND - 9 * S, feet[0] + 80 * S, GROUND + 7 * S], fill=(30, 22, 14, 80))
    gd.ellipse([950 * S - 150 * S, GROUND - 10 * S, 950 * S + 160 * S, GROUND + 8 * S], fill=(30, 22, 14, 80))
    frame.alpha_composite(ground.filter(ImageFilter.GaussianBlur(6 * S)))
    frame.alpha_composite(mon, (950 * S - mon.width // 2, GROUND - mb[3]))
    frame.alpha_composite(hero, (feet[0] - hero.width // 2, feet[1] - hero.height))
    frame = frame.crop((0, 0, W, CUT_H)).convert("RGB")
    OUT.mkdir(parents=True, exist_ok=True)
    frame.resize((W // S, CUT_H // S), Image.LANCZOS).save(out)
    big = Path(out).with_name(Path(out).stem + "@2x.png")
    frame.save(big)
    print("wrote", out, big, "hero px", hero.size, "scale", round(k, 3))
    if close_out:
        close(name, close_out)


def close(name, out, k=3 * 235 / 1130 * 1.6):
    """The sword fist close up (about 5x game size), on the backdrop's ground colour."""
    hero, off, pts, parts = hero_render(name, ready_pose(), k)
    wrist = pts["n_hand"][0]
    c = ((wrist[0] - off[0]) * k, (wrist[1] - off[1]) * k)
    box = (int(c[0] - 260), int(c[1] - 330), int(c[0] + 300), int(c[1] + 230))
    tile = Image.new("RGBA", hero.size, (226, 218, 196, 255))
    tile.alpha_composite(paper_shadow(hero, k * 2))
    tile.crop(box).convert("RGB").save(out)
    print("wrote", out)



def main():
    a = sys.argv[1:]
    if not a:
        print(__doc__)
        return
    if a[0] == "gen":
        r = a[a.index("--ref") + 1] if "--ref" in a else "none"
        ref = True if r == "hero" else False if r == "none" else r
        gen(a[1], a[2], [int(s) for s in a[3].split(",")], ref)
    elif a[0] == "cut":
        cut(a[1])
    elif a[0] == "make":
        make_arm(a[1] if len(a) > 1 else "arm")
    elif a[0] == "body":
        make_body(a[1] if len(a) > 1 else "hero")
    elif a[0] == "still":
        still(a[1], a[2], a[3] if len(a) > 3 else None)
    elif a[0] == "bends":
        bends(a[1], a[2])
    elif a[0] == "rod":
        c, d, _, n = rod_of(a[1])
        print("rod middle", c, "dir", d, "px", n)
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
