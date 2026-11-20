#!/usr/bin/env python3
"""Pictures made for the bones (the designer, 2026-11-20: "can we try do one where we do textures for the bone?").

Every part picture rides one bone of the clean skeleton (scripts/skeleton.py), rigidly: turned by exactly the bone's
turn since standing, around the bone's joint, and carried along with it. Nothing is turned to follow the sword and no
picture is picked by an angle; the sword is fixed in the hand bone (skeleton.SWORD).

An arm set is a folder under unity/Bones/hero with parts.json ({part: [file, x, y]}, rig canvas px, standing): its
pieces replace those parts of the game's bare hero (rig.json "bare" + swaps.json "bare_v2"); an optional "_hold" is
the blade's turn from the hand bone for that set's fist (skeleton.SWORD's -112 otherwise). "today" is the game's.

  python scripts/bone_art.py stand <out.png>          the standing hero without a sword, on white, rig canvas size
  python scripts/bone_art.py clip <out.mp4> <set>...  the sword cut, one panel per arm set, normal then half speed
  python scripts/bone_art.py hard <out.png> <set>...  the cut's keys, the near arm close up, one row per arm set
  python scripts/bone_art.py take <try.png> <part> <set>   a generated try (an edit of `stand`) as that set's part
  python scripts/bone_art.py bends <out.png> <set>...  the near elbow close up, bent both ways, one row per set
  python scripts/bone_art.py poses <out.png> <set>     the cut's four keys, whole body, beside his drawn poses
  python scripts/bone_art.py elbow <set> <new set>     that set's arm with the rolled cuff above the elbow, a round elbow
  python scripts/bone_art.py fist <set> <new set>      that set's arm with the fist turned over (thumb at the guard)
  python scripts/bone_art.py fists <out.png> <set>...  the sword fist close up at the four keys, beside his drawn ones
"""
import json
import math
import shutil
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import skeleton as sk  # noqa: E402

BONES = sk.BONES
RIG = sk.RIG
SWAPS = json.loads((BONES / "swaps.json").read_text())
BONE_OF = {"torso": "spine", "n_cap": "n_upper"}   # every other part rides the bone of its own name
Z = {p["name"]: p["z"] for p in RIG["parts"]}
# the sleeve's round top (see `elbow`): with the sleeve, over the shirt, until the arm is raised; then under the
# shirt's armhole, so its lit top doesn't lie on the chest (a draw-order swap, as cut-out rigs do)
Z["n_cap"] = Z["n_upper"]
CAP_UNDER = (Z["torso"] - 1, 88)              # z, from how far the upper arm points away from straight down (deg)
Z_SWORD = RIG["weaponZ"]["n"]                 # over the forearm, under the fist and sleeve, as in the game
SWORD_PIC = RIG["weapons"]["iron_sword"]      # blade up in the picture, grip at (gx, gy), drawn 1:1 on the rig canvas
STAND0 = sk.pose_points({})
# the skeleton's cut, but the wind-up's upper arm goes straight up (-100, the shoulder's limit) instead of up across the face (-70): with art
# on, the sleeve covered his face; the forearm, wrist and sword stay as in skeleton.py
KEYS = dict(sk.KEYS, **{"wind-up": sk.near_arm(-100, -150, -20, spine=-8)})


# Whole-body keys (the designer, 2026-11-20: "the hand is upside down. elbow looks weird, animation is bad"): every
# bone has a say, not just the sword arm. Two limits are wider here than in skeleton.py (which stays as the designer
# saw it): the arm raised out to the side, as in his drawn wind-up, points up-back past the shoulder's -250 and, seen
# from the side, folds the forearm up behind the arm (elbow above 0). `wide()` turns them on; `excursions()` names
# every place a pose goes past skeleton.py's own limits.
WIDE = {"n_upper": [-290, 70], "n_fore": [-150, 100]}
LIMITS = {n: (list(b["limits"]) if b["limits"] else None) for n, b in sk.BONES_DEF.items()}


def wide(on=True):
    for n, lim in WIDE.items():
        sk.BONES_DEF[n]["limits"] = list(lim) if on else list(LIMITS[n])


def excursions(local):
    out = []
    for n in sk.ORDER:
        lim = LIMITS[n]
        v = local.get(n, sk.STAND[n])
        if lim and not lim[0] - 0.01 <= v <= lim[1] + 0.01:
            out.append(f"{n} {v:.0f} (skeleton.py {lim[0]}..{lim[1]})")
    return out


def body(arm, far=(48.4, 48.4, 0), spine=0, head=0, legs=None, hips=0):
    """A whole-body pose. arm, far: the near and far arm as (upper, fore, wrist): world directions of the upper arm
    and forearm, and the wrist's bend from the forearm. spine, head, hips: turned from standing (deg; + leans forward).
    legs: world directions (n_thigh, n_shin, f_thigh, f_shin). The feet keep their standing angles; the hips ("_root")
    move so the lowest foot is on its own ground line (the near foot stands lower on screen) and the feet stay centred
    where they stood."""
    want = {"hips": STAND0["hips"][2] + hips, "spine": STAND0["spine"][2] + spine, "head": STAND0["head"][2] + head,
            "n_upper": arm[0], "n_fore": arm[1], "n_hand": arm[1] + arm[2],
            "f_upper": far[0], "f_fore": far[1], "f_hand": far[1] + far[2],
            "n_foot": STAND0["n_foot"][2], "f_foot": STAND0["f_foot"][2]}
    if legs:
        want.update(dict(zip(["n_thigh", "n_shin", "f_thigh", "f_shin"], legs)))
    local = sk.local_of(want)
    local["hips"] = want["hips"]
    pts = sk.pose_points(local)
    nt, ft = pts["n_foot"][1], pts["f_foot"][1]
    NT, FT = STAND0["n_foot"][1], STAND0["f_foot"][1]
    dy = max(nt[1] - NT[1], ft[1] - FT[1])
    dx = ((NT[0] + FT[0]) - (nt[0] + ft[0])) / 2
    w = STAND0["hips"][0]
    local["_root"] = (w[0] + dx, w[1] - dy)
    return local


def body_keys(hold=-112):
    """The cut's four keys for the whole body; the wind-up is his drawn one, the arm raised out to the side with the
    sword back over his head (needs `wide()`). hold: the set's blade turn from the hand (`hold_of`); the wrist turns
    with it so every blade keeps its direction (the flipped fist, -68: wrists 44 lower, ready's kept at -30)."""
    d = -112 - hold
    return {
        "ready": body((105, 30, max(10 + d, -30)), far=(60, -20, 10), spine=4, legs=(100, 118, 70, 92)),
        "wind-up": body((-130, -80, 27 + d), far=(66, -10, 10), spine=-12, head=-4, legs=(95, 125, 58, 74), hips=-6),
        "hit": body((70, 67, 50 + d), far=(-137, -137, 10), spine=24, head=10, legs=(128, 130, 25, 100), hips=8),
        "after": body((90, 88, 50 + d), far=(150, 125, 10), spine=20, head=8, legs=(124, 127, 32, 98), hips=6),
    }


# his drawn sword poses in the game (src/data/classes.ts HERO_POSES: the fist in 1x px, the weapon's turn)
DRAWN = {"ready": ("sword_ready", 244, 262, 55), "wind-up": ("sword_wind", 133.7, 68, -75),
         "hit": ("sword_strike", 331.7, 388, 115)}


def drawn_pose(key, k, cell):
    """His drawn pose for a key at the rig's size times k, feet on the cell's ground line, the sword in his fist."""
    art = sk.ROOT / "src" / "assets" / "art"
    pose, fx, fy, angle = DRAWN[key]
    im = Image.open(art / f"char_hero_bare_{pose}@2x.webp").convert("RGBA")
    base = Image.open(art / "char_hero_bare@2x.webp").convert("RGBA")
    rig_h = sk.P["n_toe"][1] - sk.P["head_top"][1]
    s = rig_h / (base.getbbox()[3] - base.getbbox()[1]) * k          # drawn 2x px -> sheet px
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    bb = im.getbbox()
    out = Image.new("RGBA", cell, sk.GROUND + (255,))
    ox, oy = round(cell[0] / 2 - (bb[0] + bb[2]) / 2), round(cell[1] - 30 - bb[3])
    out.alpha_composite(im, (ox, oy))
    sw = place(pic(SWORD_PIC["file"]), 0, 0, (SWORD_PIC["gx"], SWORD_PIC["gy"]), -90,
               ((fx * 2 * s + ox) / k, (fy * 2 * s + oy) / k), angle - 90, k, (0, 0), cell)
    out.alpha_composite(sw)
    return out


def body_cell(local, parts, k, cell):
    """A whole-body pose at k, feet on the cell's ground line, centred on the hips."""
    pts = sk.pose_points(local, local.get("_root"))
    ground = max(STAND0["n_foot"][1][1], STAND0["f_foot"][1][1])
    hx = pts["hips"][0][0]
    return render(local, parts, k, (cell[0] / 2 / k - hx, (cell[1] - 30) / k - ground), cell)


def poses(out, new_set):
    """The cut's four keys: his drawn poses in the game, the rejected cut (sword arm only, today's keys, the first arm
    made for the bones) and the new whole-body keys with new_set's arm."""
    wide()
    new = body_keys(hold_of(new_set))
    k, cell = 0.36, (500, 540)
    rows = [("his drawn poses (sword as in game)", None), ("before: only the sword arm moved", "old"),
            ("now: the whole body", "new")]
    sheet = Image.new("RGB", (cell[0] * 4 + 6 * 3, (cell[1] + 6) * len(rows)), (20, 20, 24))
    old_parts, new_parts = parts_of("bonearm_a"), parts_of(new_set)
    for r, (title, which) in enumerate(rows):
        for c, key in enumerate(new):
            if which is None:
                img = drawn_pose(key, k, cell) if key in DRAWN else Image.new("RGBA", cell, (34, 32, 40, 255))
            elif which == "old":
                use_hold("bonearm_a")
                img = body_cell(KEYS[key], old_parts, k, cell)
            else:
                use_hold(new_set)
                img = body_cell(new[key], new_parts, k, cell)
            label(img, f"{key}  |  {title}" if c == 0 else key)
            sheet.paste(img.convert("RGB"), (c * (cell[0] + 6), r * (cell[1] + 6)))
    sheet.save(out)
    use_hold(new_set)
    for key, local in new.items():
        sw = sk.sword_points(sk.pose_points(local, local["_root"]))
        print(key, f"blade {sk.norm(sw['angle']):.0f}", "; ".join(excursions(local)) or "inside skeleton.py's limits")
    print(out, sheet.size)


def bends(out, sets):
    """The near elbow close up (rig 1:1) at standing and bent -45, -90, -130 (forward) and +50, +90 (the arm turned
    out to the side, as in the wind-up), one row per arm set."""
    wide()
    cell, bends_ = 360, [sk.STAND["n_fore"], -45, -90, -130, 50, 90]
    sheet = Image.new("RGB", ((cell + 4) * len(bends_), (cell + 26) * len(sets)), (20, 20, 24))
    d = ImageDraw.Draw(sheet)
    for r, s in enumerate(sets):
        parts = parts_of(s)
        for c, b in enumerate(bends_):
            loc = dict(sk.STAND)
            loc["n_fore"] = b
            e = sk.pose_points(loc)["n_fore"][0]
            img = render(loc, parts, 1.0, (-(e[0] - cell / 2), -(e[1] - cell / 2)), (cell, cell), sword=False)
            sheet.paste(img.convert("RGB"), (c * (cell + 4), r * (cell + 26)))
            d.text((c * (cell + 4) + 6, r * (cell + 26) + cell + 4), f"{NAMES.get(s, s)}: elbow {b:.0f}",
                   fill=(235, 235, 235), font=sk.font(14))
    sheet.save(out)
    print(out, sheet.size)


def blend(a, b, t):
    """skeleton.blend, and the hips ("_root") carried along."""
    out = sk.blend(a, b, t)
    if "_root" in a or "_root" in b:
        ra, rb = a.get("_root", STAND0["hips"][0]), b.get("_root", STAND0["hips"][0])
        out["_root"] = (ra[0] + (rb[0] - ra[0]) * t, ra[1] + (rb[1] - ra[1]) * t)
    return out


def cut_frames():
    frames, prev = [], KEYS["ready"]
    for key, n in sk.CUT:
        for i in range(1, n + 1):
            frames.append((sk.blend(prev, KEYS[key], sk.ease(i / n)), key))
        prev = KEYS[key]
    return frames
_cache = {}


def pic(file):
    if file not in _cache:
        _cache[file] = Image.open(BONES / file).convert("RGBA").convert("RGBa")   # premultiplied: clean edges
    return _cache[file]


def parts_of(arm_set):
    parts = {k: (v["file"], v["x"], v["y"]) for k, v in RIG["sets"]["bare"].items()}
    parts.update({k: (v["file"], v["x"], v["y"]) for k, v in SWAPS["sets"]["bare_v2"].items()})
    if arm_set != "today":
        parts.update({k: tuple(v) for k, v in json.loads((BONES / arm_set / "parts.json").read_text()).items()
                      if not k.startswith("_")})
    parts["_hold"] = hold_of(arm_set)                    # render() holds the sword at the set's own turn
    return parts


def hold_of(arm_set):
    """The blade's turn from the hand bone for a set ("_hold" in its parts.json; skeleton.py's -112 otherwise)."""
    f = BONES / arm_set / "parts.json"
    return json.loads(f.read_text()).get("_hold", -112) if arm_set != "today" and f.exists() else -112


def use_hold(arm_set):
    sk.SWORD["angle"] = hold_of(arm_set)


def place(src, sx, sy, h0, w0, h, w, k, off, size):
    """The picture (top-left (sx, sy) when standing) turned by w - w0 about joint h0 and moved to h; output px =
    (canvas + off) * k."""
    d = math.radians(w - w0)
    c, s = math.cos(d), math.sin(d)
    ox, oy = -off[0] - h[0], -off[1] - h[1]
    data = (c / k, s / k, c * ox + s * oy + h0[0] - sx,
            -s / k, c / k, -s * ox + c * oy + h0[1] - sy)
    return src.transform(size, Image.AFFINE, data, resample=Image.BILINEAR).convert("RGBA")   # bicubic overshoots: light seams


def render(local, parts, k=0.5, off=(0, 0), size=(768, 768), sword=True, bg=sk.GROUND):
    pts = sk.pose_points(local, local.get("_root"))     # "_root": where the hips are (whole-body poses move them)
    img = Image.new("RGBA", size, bg + (255,))
    z = dict(Z)
    if abs(sk.norm(pts["n_upper"][2] - 90)) > CAP_UNDER[1]:            # the arm raised above level, front or back
        z["n_cap"] = CAP_UNDER[0]
    items = sorted([(z[n], n) for n in parts if not n.startswith("_")] + ([(Z_SWORD, "sword")] if sword else []))
    for _, n in items:
        if n == "sword":
            was = sk.SWORD["angle"]
            sk.SWORD["angle"] = parts.get("_hold", was)
            sw = sk.sword_points(pts)
            sk.SWORD["angle"] = was
            layer = place(pic(SWORD_PIC["file"]), 0, 0, (SWORD_PIC["gx"], SWORD_PIC["gy"]), -90,
                          sw["grip"], sw["angle"], k, off, size)
        else:
            f, x, y = parts[n]
            b = BONE_OF.get(n, n)
            h0, _, w0 = STAND0[b]
            h, _, w = pts[b]
            layer = place(pic(f), x, y, h0, w0, h, w, k, off, size)
        img.alpha_composite(layer)
    return img


def frame_box(frames, pad=40):
    """Canvas box holding the body and every sword and hand position of the frames."""
    xs, ys = [350, 1170], [170, 1350]
    for local, _ in frames:
        pts = sk.pose_points(local)
        sw = sk.sword_points(pts)
        for p in (sw["tip"], sw["pommel"], pts["n_hand"][1], pts["n_fore"][0]):
            xs.append(p[0])
            ys.append(p[1])
    return min(xs) - pad, min(ys) - pad, max(xs) + pad, max(ys) + pad


def label(img, text):
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, img.width, 34], fill=(30, 28, 36, 255))
    d.text((10, 6), text, fill=(245, 245, 245), font=sk.font(20))
    return img


NAMES = {"today": "today's pieces, new skeleton", "bare_v3": "the redrawn elbow (bare_v3)", "bonearm_a": "arm made for the bones",
         "bonearm_c": "bare elbow, cuff higher", "bonearm_d": "bare elbow, fist turned over"}


def clip(out, sets):
    frames = cut_frames()
    x0, y0, x1, y1 = frame_box(frames)
    k = 560 / (x1 - x0)
    size = (int((x1 - x0) * k) // 2 * 2, int((y1 - y0) * k) // 2 * 2)
    tmp = Path(tempfile.mkdtemp(prefix="bone_art_"))
    parts = {s: parts_of(s) for s in sets}
    order = [(i, "normal speed") for i in range(len(frames))] + [(i, "half speed") for i in range(len(frames)) for _ in (0, 1)]
    rendered = {}
    for j, (i, speed) in enumerate(order):
        if i not in rendered:
            panels = [label(render(frames[i][0], parts[s], k, (-x0, -y0), size), f"{NAMES.get(s, s)}  |  {speed}")
                      for s in sets]
            row = Image.new("RGB", (size[0] * len(sets) + 8 * (len(sets) - 1), size[1]), (20, 20, 24))
            for n, p in enumerate(panels):
                row.paste(p.convert("RGB"), (n * (size[0] + 8), 0))
            rendered[i] = row
        row = rendered[i].copy()
        if speed == "half speed":
            for n in range(len(sets)):
                ImageDraw.Draw(row).rectangle([n * (size[0] + 8), 0, n * (size[0] + 8) + size[0], 34], fill=(30, 28, 36))
                ImageDraw.Draw(row).text((n * (size[0] + 8) + 10, 6), f"{NAMES.get(sets[n], sets[n])}  |  {speed}",
                                         fill=(245, 245, 245), font=sk.font(20))
        row.save(tmp / f"f{j:04d}.png")
    import bones_sheets as bs
    bs.run_ffmpeg(["-framerate", "24", "-i", str(tmp / "f%04d.png"), "-pix_fmt", "yuv420p", "-c:v", "libx264",
                   "-crf", "20", str(out)])
    shutil.rmtree(tmp, ignore_errors=True)
    print(out, len(order), "frames", size)


def hard(out, sets):
    """The four keys of the cut, the near arm close up (rig canvas 1:1), one row per set; under each, how far each arm
    piece is turned from how it was painted."""
    keys = list(KEYS)
    cell = 520
    sheet = Image.new("RGB", (cell * len(keys), (cell + 40) * len(sets) + 40), (20, 20, 24))
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "The cut's four keys, the near arm close up (rig 1:1); numbers: how far each piece is turned from how"
                    " it was painted", fill=(240, 240, 240), font=sk.font(18))
    for r, s in enumerate(sets):
        parts = parts_of(s)
        for c, key in enumerate(keys):
            local = KEYS[key]
            pts = sk.pose_points(local)
            ps = [pts["n_upper"][0], pts["n_fore"][0], pts["n_hand"][0], pts["n_hand"][1]]   # shoulder to knuckles
            cx, cy = sum(p[0] for p in ps) / 4, sum(p[1] for p in ps) / 4
            img = render(local, parts, 1.0, (-(cx - cell / 2), -(cy - cell / 2)), (cell, cell))
            sheet.paste(img.convert("RGB"), (c * cell, 40 + r * (cell + 40)))
            turns = {b: round(sk.norm(pts[b][2] - STAND0[b][2])) for b in ("n_upper", "n_fore", "n_hand")}
            elbow = round(local["n_fore"])
            d.text((c * cell + 8, 40 + r * (cell + 40) + cell + 4),
                   f"{NAMES.get(s, s)}, {key}: sleeve {turns['n_upper']}, forearm {turns['n_fore']}, fist "
                   f"{turns['n_hand']}; elbow {elbow}", fill=(230, 230, 230), font=sk.font(14))
    sheet.save(out)
    print(out, sheet.size)


def biggest_blob(m):
    """The largest 4-connected piece of a small boolean mask."""
    import numpy as np
    seen = np.zeros(m.shape, bool)
    best = []
    for y0, x0 in zip(*np.nonzero(m)):
        if seen[y0, x0]:
            continue
        stack, blob = [(y0, x0)], []
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            blob.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < m.shape[0] and 0 <= xx < m.shape[1] and m[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    stack.append((yy, xx))
        if len(blob) > len(best):
            best = blob
    out = np.zeros(m.shape, bool)
    if best:
        ys, xs = zip(*best)
        out[list(ys), list(xs)] = True
    return out


def similar(img, scale, turn, centre, shift, resample=Image.BICUBIC):
    """img scaled and turned (deg, clockwise on screen) about centre, then moved by shift; same size."""
    d = math.radians(turn)
    c, s = math.cos(d), math.sin(d)
    # output p -> source: centre + R(-turn) (p - centre - shift) / scale
    a, b = c / scale, s / scale
    dd, e = -s / scale, c / scale
    ox, oy = -centre[0] - shift[0], -centre[1] - shift[1]
    data = (a, b, a * ox + b * oy + centre[0], dd, e, dd * ox + e * oy + centre[1])
    return img.transform(img.size, Image.AFFINE, data, resample=resample)


def take(try_png, part, arm_set):
    """A generated try (an edit of `stand`, so on the rig canvas, but edits drift and resize) as a part of an arm set:
    faint alpha dropped, only its biggest piece kept, then scaled, turned and moved onto today's part where their
    shapes overlap best (a search on a quarter-size mask), cropped. Prints how much it had to be moved."""
    import numpy as np
    q = 4
    a = Image.open(try_png).convert("RGBA")
    al = np.asarray(a)[..., 3].copy()
    al[al < 10] = 0
    small = np.asarray(Image.fromarray(al).resize((a.width // q, a.height // q), Image.BOX)) > 128
    keep = np.asarray(Image.fromarray((biggest_blob(small) * 255).astype(np.uint8)).resize(a.size, Image.NEAREST)) > 0
    keep = np.asarray(Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))) > 0
    al[~keep] = 0
    a.putalpha(Image.fromarray(al))
    f, x, y = parts_of("today")[part]
    old = Image.new("L", a.size, 0)
    old.paste(Image.open(BONES / f).convert("RGBA").split()[3], (x, y))
    O = np.asarray(old.resize((a.width // q, a.height // q), Image.BOX)).astype(np.float32) / 255
    N0 = Image.fromarray(al).resize((a.width // q, a.height // q), Image.BOX)
    ys, xs = np.nonzero(np.asarray(N0) > 128)
    cen = (float(xs.mean()), float(ys.mean()))
    FO = np.fft.rfft2(O)
    best = None
    for sc in np.arange(0.70, 1.31, 0.05):
        for tr in range(-20, 21, 4):
            N = np.asarray(similar(N0, sc, tr, cen, (0, 0), Image.BILINEAR)).astype(np.float32) / 255
            corr = np.fft.irfft2(FO * np.conj(np.fft.rfft2(N)), O.shape)
            iy, ix = np.unravel_index(int(np.argmax(corr)), corr.shape)
            inter = float(corr[iy, ix])
            iou = inter / (O.sum() + N.sum() - inter)
            dy = iy if iy < O.shape[0] // 2 else iy - O.shape[0]
            dx = ix if ix < O.shape[1] // 2 else ix - O.shape[1]
            if best is None or iou > best[0]:
                best = (iou, sc, tr, dx, dy)
    iou, sc, tr, dx, dy = best
    img = similar(a.convert("RGBa"), sc, tr, (cen[0] * q, cen[1] * q), (dx * q, dy * q)).convert("RGBA")
    bb = img.getbbox()
    out_dir = BONES / arm_set
    out_dir.mkdir(exist_ok=True)
    path = out_dir / f"{part}.png"
    if path.exists():
        raise SystemExit(f"{path} exists (a kept picture is never overwritten; use a new set name)")
    img.crop(bb).save(path)
    table_path = out_dir / "parts.json"
    table = json.loads(table_path.read_text()) if table_path.exists() else {}
    table[part] = [f"{arm_set}/{part}.png", bb[0], bb[1]]
    table_path.write_text(json.dumps(table, indent=1))
    print(part, "->", path.relative_to(sk.ROOT), table[part], f"scaled {sc:.2f}, turned {tr}, moved {dx * q},{dy * q};"
          f" overlap with today's {iou:.2f}")


def frame_of(part, arm_set, p0, p1, pad=80):
    """A part picture padded, as float RGBA, its top-left on the rig canvas, and per pixel: along (from joint p0
    towards p1), across, and the distance to p0 (rig px)."""
    import numpy as np
    f, x0, y0 = parts_of(arm_set)[part]
    im = np.asarray(Image.open(BONES / f).convert("RGBA")).astype(np.float32)
    a = np.zeros((im.shape[0] + 2 * pad, im.shape[1] + 2 * pad, 4), np.float32)
    a[pad:-pad, pad:-pad] = im
    ox, oy = x0 - pad, y0 - pad
    u = np.array([p1[0] - p0[0], p1[1] - p0[1]], np.float32)
    u /= np.hypot(*u)
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(np.float32)
    dx, dy = xx + ox - p0[0], yy + oy - p0[1]
    return a, (ox, oy), dx * u[0] + dy * u[1], -dx * u[1] + dy * u[0], np.hypot(dx, dy), u


def carried(a, al, ac, lo_al, hi_al, smooth=7):
    """The cross-section between along lo_al..hi_al (median colour per px across, smoothed): (lut, first across)."""
    import numpy as np
    band = (a[..., 3] > 200) & (al >= lo_al) & (al < hi_al)
    cols = np.round(ac[band]).astype(int)
    lo, hi = int(cols.min()), int(cols.max())
    lut = np.zeros((hi - lo + 1, 3), np.float32)
    have = np.zeros(hi - lo + 1, bool)
    for c in range(lo, hi + 1):
        sel = cols == c
        if sel.sum() >= 2:
            lut[c - lo], have[c - lo] = np.median(a[..., :3][band][sel], 0), True
    idx = np.nonzero(have)[0]
    for i in range(len(lut)):
        if not have[i]:
            lut[i] = lut[idx[np.argmin(np.abs(idx - i))]]
    pd = np.pad(lut, ((smooth // 2, smooth // 2), (0, 0)), mode="edge")
    lut = np.stack([np.convolve(pd[:, i], np.ones(smooth) / smooth, mode="valid") for i in range(3)], 1)
    return lut, lo


def clean_edges(arr, n=3):
    """Soft edge pixels take the colour of the solid picture next to them (the pieces were cut from a render on white,
    so their edges carry a light fringe that shows as a dotted light line over darker parts)."""
    import numpy as np
    rgb, al = arr[..., :3].copy(), arr[..., 3]
    solid = al >= 250
    for _ in range(n):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(al.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            s_ = np.roll(np.roll(solid, dy, 0), dx, 1)
            c_ = np.roll(np.roll(rgb, dy, 0), dx, 1)
            acc += c_ * s_[..., None]
            cnt += s_
        grow = ~solid & (cnt > 0) & (al > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        solid = solid | grow
    out = arr.copy()
    out[..., :3] = rgb
    return out


def elbow(src_set, new_set, lift=40, roll=25, shoulder=62):
    """The arm of src_set with a bare elbow (the designer, 2026-11-20: "elbow looks weird": the elbow turned inside
    the wide cuff, so the forearm came out of the cuff's side). The rolled cuff (the sleeve's last `roll` px before the
    elbow line and everything past it) moves `lift` px up the arm, over the sleeve cut short under it; the forearm
    gets a round top on the elbow (a disc as wide as the forearm, painted by carrying its own cross-section), always
    under the cuff, so the elbow is skin that looks the same at every bend. The sleeve's top is cut to a disc on the
    shoulder joint, so it stays put as the arm turns. Writes unity/Bones/hero/<new_set>/."""
    import numpy as np
    S, E, W = sk.P["n_shoulder"], sk.P["n_elbow"], sk.P["n_wrist"]
    # the sleeve
    a, org, al, ac, dist, u = frame_of("n_upper", src_set, E, (2 * E[0] - S[0], 2 * E[1] - S[1]))
    cuffp = a.copy()
    cuffp[..., 3] *= np.clip((al + roll) / 4, 0, 1)          # the roll: from `roll` px before the line on
    sh = Image.fromarray(np.clip(cuffp, 0, 255).astype(np.uint8)).convert("RGBa")
    sh = sh.transform(sh.size, Image.AFFINE, (1, 0, lift * u[0], 0, 1, lift * u[1]), resample=Image.BILINEAR)
    sh = np.asarray(sh.convert("RGBA")).astype(np.float32)
    # the roll is wider than the sleeve higher up: its top 20 px keep inside the sleeve's own outline (no corner
    # poking onto the shirt)
    top = al < -(lift + roll) + 20
    sh[..., 3][top] *= a[..., 3][top] / 255
    up = a.copy()
    up[..., 3] *= np.clip((-(al + lift) - roll + 8) / 4, 0, 1)   # the sleeve, cut 8 px under the moved roll's top
    out_up = up.copy()
    t = sh[..., 3:4] / 255
    out_up[..., :3] = sh[..., :3] * t + up[..., :3] * (up[..., 3:4] / 255) * (1 - t)
    out_up[..., 3] = sh[..., 3] + up[..., 3] * (1 - t[..., 0])
    nz = out_up[..., 3] > 0
    out_up[..., :3][nz] /= (out_up[..., 3][nz] / 255)[:, None]
    # the shoulder: past the shoulder line, only a disc on the shoulder joint as big as the round cut in the shirt
    # (the torso ends `shoulder` px from it), so the sleeve's top no longer swings onto the chest when the arm turns
    als = (al + float(np.hypot(E[0] - S[0], E[1] - S[1])))       # along from the shoulder joint
    ds = np.hypot(al + float(np.hypot(E[0] - S[0], E[1] - S[1])), ac)
    out_up[..., 3] *= np.where(als >= 0, 1.0, np.clip(shoulder + 0.5 - ds, 0, 1))
    # that disc goes under the shirt (n_cap), so the shirt's armhole edge lies over it like a seam and the sleeve's
    # lit top never lies on the chest when the arm is raised; the rest of the sleeve stays over the shirt
    inside = np.clip(shoulder + 0.5 - ds, 0, 1)
    cap = out_up.copy()
    cap[..., 3] *= inside
    out_up[..., 3] *= 1 - np.clip(shoulder - 2 + 0.5 - ds, 0, 1)    # 2 px over the cap: no seam when both are on top
    up_org = org
    # the forearm: a round top on the elbow
    f, org2, al2, ac2, dist2, u2 = frame_of("n_fore", src_set, E, W)
    lut, lo = carried(f, al2, ac2, 12, 30)
    r = (len(lut) - 1) / 2 + 0.5
    centre = lo + (len(lut) - 1) / 2                # the forearm's middle across (the disc sits on it, at the elbow)
    cov = np.clip(r + 0.5 - np.hypot(al2, ac2 - centre), 0, 1)
    band = np.clip(r + 0.5 - np.abs(ac2 - centre), 0, 1) * (al2 >= 0) * (al2 < 14)
    top = np.maximum(cov * (al2 < 14), band)
    acr = np.clip(np.round(ac2), lo, lo + len(lut) - 1).astype(int) - lo
    paint = (top > 0) & (al2 < 14)
    fore = f.copy()
    fore[..., 3][al2 < 14] = 0
    fore[..., :3][paint] = lut[acr][paint]
    fore[..., 3][paint] = top[paint] * 255
    out = BONES / new_set
    out.mkdir(exist_ok=True)
    table = {}
    for part, arr, o in (("n_upper", out_up, up_org), ("n_cap", cap, up_org), ("n_fore", fore, org2)):
        arr = clean_edges(arr)
        path = out / f"{part}.png"
        if path.exists():
            raise SystemExit(f"{path} exists (a kept picture is never overwritten; use a new set name)")
        img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
        bb = img.getbbox()
        img.crop(bb).save(path)
        table[part] = [f"{new_set}/{part}.png", o[0] + bb[0], o[1] + bb[1]]
    (out / "parts.json").write_text(json.dumps(table, indent=1))
    print(new_set, table, f"elbow radius {r:.0f}, cuff lifted {lift}")


def fist(src_set, new_set):
    """The fist turned over (the designer, 2026-11-21, twice: "the hand is upside down", "still inverted"). Today's fist
    shows the curled fingers with the thumb lying across them, and the sword left its thumb at the pommel end. Here the
    fist is mirrored across its own bone (the wrist stays on the forearm), so the thumb sits at the guard; the blade
    comes out of that side: "_hold" = -(180 + the old hold). Writes <new_set>/n_hand.png and a parts.json that keeps
    src_set's arm pieces."""
    import numpy as np
    out = BONES / new_set
    out.mkdir(parents=True, exist_ok=True)
    path = out / "n_hand.png"
    if path.exists():
        raise SystemExit(f"{path} exists (a kept picture is never overwritten; use a new set name)")
    f, x, y = parts_of(src_set)["n_hand"]
    W = 1400
    can = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    can.alpha_composite(Image.open(BONES / f).convert("RGBA"), (x, y))
    (wx, wy), _, th = STAND0["n_hand"]
    c2, s2 = math.cos(math.radians(2 * th)), math.sin(math.radians(2 * th))
    m = can.convert("RGBa").transform((W, W), Image.AFFINE, (c2, s2, wx - c2 * wx - s2 * wy, s2, -c2, wy - s2 * wx + c2 * wy),
                                      resample=Image.BILINEAR).convert("RGBA")   # premultiplied, bilinear: no light seams
    arr = clean_edges(np.asarray(m).astype(np.float32))
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    bb = img.getbbox()
    img.crop(bb).save(path)
    table = {k: v for k, v in json.loads((BONES / src_set / "parts.json").read_text()).items() if not k.startswith("_")}
    table["n_hand"] = [f"{new_set}/n_hand.png", bb[0], bb[1]]
    table["_hold"] = -(180 + hold_of(src_set))
    (out / "parts.json").write_text(json.dumps(table, indent=1))
    print(new_set, table)


def fists(out, sets):
    """The sword fist close up (x4) at the cut's four keys: his drawn fists on top (sword as in game), then one row per
    set, each with its own hold."""
    wide()
    art = sk.ROOT / "src" / "assets" / "art"
    cell, zoom, kd = 560, 90, 1.18
    label_of = {"bonearm_c": "before (thumb at the pommel)", "bonearm_d": "now (thumb at the guard)"}

    def crop(img):
        c = cell / 2
        return img.crop((round(c - zoom), round(c - zoom), round(c + zoom), round(c + zoom))).resize(
            (zoom * 4, zoom * 4), Image.LANCZOS)

    rows = [[]]
    for key in ("ready", "wind-up", "hit", "after"):
        if key not in DRAWN:
            rows[0].append((Image.new("RGBA", (zoom * 4, zoom * 4), (34, 32, 40, 255)), ""))
            continue
        pose, fx, fy, angle = DRAWN[key]
        im = Image.open(art / f"char_hero_bare_{pose}@2x.webp").convert("RGBA")
        im = im.resize((round(im.width * kd), round(im.height * kd)), Image.LANCZOS)
        can = Image.new("RGBA", (cell, cell), sk.GROUND + (255,))
        ox, oy = cell / 2 - fx * 2 * kd, cell / 2 - fy * 2 * kd
        can.alpha_composite(im, (round(ox), round(oy)))
        can.alpha_composite(place(pic(SWORD_PIC["file"]), 0, 0, (SWORD_PIC["gx"], SWORD_PIC["gy"]), -90,
                                  (cell / 2, cell / 2), angle - 90, 1.0, (0, 0), (cell, cell)))
        rows[0].append((crop(can), f"his {key}"))
    for st in sets:
        parts, r = parts_of(st), []
        for key, local in body_keys(parts["_hold"]).items():
            (hx, hy), (tx, ty), _ = sk.pose_points(local, local["_root"])["n_hand"]
            cx, cy = (hx + tx) / 2, (hy + ty) / 2
            r.append((crop(render(local, parts, 1.0, (cell / 2 - cx, cell / 2 - cy), (cell, cell))),
                      f"{label_of.get(st, NAMES.get(st, st))}: {key}"))
        rows.append(r)
    w = zoom * 4
    sheet = Image.new("RGB", ((w + 6) * 4, (w + 26) * len(rows)), (20, 20, 24))
    d = ImageDraw.Draw(sheet)
    for j, r in enumerate(rows):
        for i, (c, t) in enumerate(r):
            x, y = i * (w + 6), j * (w + 26)
            sheet.paste(c.convert("RGB"), (x, y))
            d.text((x + 6, y + w + 4), t, fill=(235, 235, 235), font=sk.font(15))
    sheet.save(out)
    print(out, sheet.size)


def main():
    cmd = sys.argv[1:2]
    if cmd == ["bends"]:
        bends(Path(sys.argv[2]), sys.argv[3:])
        return
    if cmd == ["poses"]:
        poses(Path(sys.argv[2]), sys.argv[3])
        return
    if cmd == ["fists"]:
        fists(Path(sys.argv[2]), sys.argv[3:])
        return
    if cmd == ["fist"]:
        fist(sys.argv[2], sys.argv[3])
        return
    if cmd == ["elbow"]:
        elbow(sys.argv[2], sys.argv[3], *[int(v) for v in sys.argv[4:6]])
        return
    if cmd == ["take"]:
        take(sys.argv[2], sys.argv[3], sys.argv[4])
        return
    if cmd == ["stand"]:
        img = render({}, parts_of("today"), 1.0, (0, 0), (1536, 1536), sword=False, bg=(255, 255, 255))
        img.convert("RGB").save(sys.argv[2])
        print(sys.argv[2], img.size)
    elif cmd == ["clip"]:
        clip(Path(sys.argv[2]), sys.argv[3:])
    elif cmd == ["hard"]:
        hard(Path(sys.argv[2]), sys.argv[3:])
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
