#!/usr/bin/env python3
"""Pictures made for the bones (the designer, 2026-11-20: "can we try do one where we do textures for the bone?").

Every part picture rides one bone of the clean skeleton (scripts/skeleton.py), rigidly: turned by exactly the bone's
turn since standing, around the bone's joint, and carried along with it. Nothing is turned to follow the sword and no
picture is picked by an angle; the sword is fixed in the hand bone (skeleton.SWORD).

An arm set is a folder under unity/Bones/hero with parts.json ({part: [file, x, y]}, rig canvas px, standing): its
pieces replace those parts of the game's bare hero (rig.json "bare" + swaps.json "bare_v2"). "today" is the game's.

  python scripts/bone_art.py stand <out.png>          the standing hero without a sword, on white, rig canvas size
  python scripts/bone_art.py clip <out.mp4> <set>...  the sword cut, one panel per arm set, normal then half speed
  python scripts/bone_art.py hard <out.png> <set>...  the cut's keys, the near arm close up, one row per arm set
  python scripts/bone_art.py take <try.png> <part> <set>   a generated try (an edit of `stand`) as that set's part
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
BONE_OF = {"torso": "spine"}                  # every other part rides the bone of its own name
Z = {p["name"]: p["z"] for p in RIG["parts"]}
Z_SWORD = RIG["weaponZ"]["n"]                 # over the forearm, under the fist and sleeve, as in the game
SWORD_PIC = RIG["weapons"]["iron_sword"]      # blade up in the picture, grip at (gx, gy), drawn 1:1 on the rig canvas
STAND0 = sk.pose_points({})
# the skeleton's cut, but the wind-up's upper arm goes straight up (-100, the shoulder's limit) instead of up across the face (-70): with art
# on, the sleeve covered his face; the forearm, wrist and sword stay as in skeleton.py
KEYS = dict(sk.KEYS, **{"wind-up": sk.near_arm(-100, -150, -20, spine=-8)})


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
        parts.update({k: tuple(v) for k, v in json.loads((BONES / arm_set / "parts.json").read_text()).items()})
    return parts


def place(src, sx, sy, h0, w0, h, w, k, off, size):
    """The picture (top-left (sx, sy) when standing) turned by w - w0 about joint h0 and moved to h; output px =
    (canvas + off) * k."""
    d = math.radians(w - w0)
    c, s = math.cos(d), math.sin(d)
    ox, oy = -off[0] - h[0], -off[1] - h[1]
    data = (c / k, s / k, c * ox + s * oy + h0[0] - sx,
            -s / k, c / k, -s * ox + c * oy + h0[1] - sy)
    return src.transform(size, Image.AFFINE, data, resample=Image.BICUBIC).convert("RGBA")


def render(local, parts, k=0.5, off=(0, 0), size=(768, 768), sword=True, bg=sk.GROUND):
    pts = sk.pose_points(local)
    img = Image.new("RGBA", size, bg + (255,))
    items = sorted([(Z[n], n) for n in parts] + ([(Z_SWORD, "sword")] if sword else []))
    for _, n in items:
        if n == "sword":
            sw = sk.sword_points(pts)
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


NAMES = {"today": "today's arm pieces", "bare_v3": "the redrawn elbow (bare_v3)", "bonearm_a": "arm made for the bones"}


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


def main():
    cmd = sys.argv[1:2]
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
