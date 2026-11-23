"""Judging pictures for the drawn-skin test, from skin_blend.py show renders (4x game size: 548.6 px/m).
<venv python> -I sheets.py <renders dir> <out dir> <repo root>
Renders expected: b_stance b_wind b_strike (bare, sword), m_stance m_wind m_strike (scale mail, sword), t0..t7
(turnaround), meta.json. Writes skin_2x.png, judge_3x.png, game_1280.png, turnaround.png."""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

D, OUT, ROOT = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
META = json.loads((D / "meta.json").read_text())
FONT = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 22)
HERO_SPOT = (231, 586)                         # the hero's feet in the 1280x720 frame (as hero3d_v2/sheets.py)
GAME_PPM = 240 / 1.75
RIM = (210, 176, 106)
DRAWN_FIG_H = None


def rim(im, width):
    a = np.asarray(im)[..., 3].astype(float) / 255
    d = ndi.distance_transform_edt(a < 0.5)
    ring = np.clip(width + 0.5 - d, 0, 1)
    base = np.zeros(a.shape + (4,), np.uint8)
    base[..., :3] = RIM
    base[..., 3] = (ring * 255).astype(np.uint8)
    out = Image.fromarray(base)
    out.alpha_composite(im)
    return out


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, im.width, 32), fill=(250, 248, 244))
    d.text((8, 3), text, font=FONT, fill=(30, 26, 22))
    return im


def hero_at(name, scale):
    """A render scaled to `scale` x game size, with the pale edge (0.8 game px), and its ground pixel."""
    m = META[name]
    k = GAME_PPM * scale / m["ppm"]
    im = Image.open(D / f"{name}.png").convert("RGBA")
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    return rim(im, 0.8 * scale), (m["ground"][0] * k, m["ground"][1] * k), k


def backdrop(scale):
    bg = Image.open(ROOT / "art/raw/bg_fen_road.png").convert("RGBA")
    return bg.resize((1280 * scale, 720 * scale), Image.LANCZOS)


def scene(name, scale, cell):
    """The hero on the backdrop at scale x game size, cropped to a cell around the hero spot."""
    bg = backdrop(scale)
    h, g, _ = hero_at(name, scale)
    x, y = round(HERO_SPOT[0] * scale - g[0]), round(HERO_SPOT[1] * scale - g[1])
    bg.alpha_composite(h, (max(x, 0), max(y, 0)), (max(-x, 0), max(-y, 0)))
    cx, fy = HERO_SPOT[0] * scale + 40 * scale, HERO_SPOT[1] * scale
    w, hh = cell
    return bg.crop((round(cx - w / 2), round(fy - hh + 20 * scale), round(cx + w / 2), round(fy + 20 * scale)))


def drawn(key, scale, cell):
    """A drawn picture at scale x game size (char_hero_bare's figure = 240 game px; poses share its pixel size), feet
    on the hero spot, on the backdrop."""
    base = np.asarray(Image.open(ROOT / "art/raw/char_hero_bare.png").convert("RGBA"))[..., 3] > 40
    ys = np.nonzero(base.any(1))[0]
    k = 240 * scale / (ys.max() - ys.min())
    im = Image.open(ROOT / f"art/raw/{key}.png").convert("RGBA")
    if key == "char_hero_scale_mail":          # the raw file is on a purple field: cut it out
        a = np.asarray(im).astype(int)
        m = np.abs(a[..., :3] - np.array([163, 55, 211])).max(2) > 60
        lab, _ = ndi.label(ndi.binary_opening(m, iterations=2))
        m = ndi.binary_fill_holes(lab == np.argmax(np.bincount(lab.ravel())[1:]) + 1)
        a[..., 3] = m * 255
        im = Image.fromarray(a.astype(np.uint8))
    al = np.asarray(im)[..., 3] > 40
    ys, xs = np.nonzero(al)
    y1 = ys.max()
    low = np.nonzero(al[y1 - 30:y1 + 1].any(0))[0]
    gx = (low.min() + low.max()) / 2
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    bg = backdrop(scale)
    x, y = round(HERO_SPOT[0] * scale - gx * k), round(HERO_SPOT[1] * scale - y1 * k)
    bg.alpha_composite(im, (max(x, 0), max(y, 0)), (max(-x, 0), max(-y, 0)))
    cx, fy = HERO_SPOT[0] * scale + 40 * scale, HERO_SPOT[1] * scale
    w, hh = cell
    return bg.crop((round(cx - w / 2), round(fy - hh + 20 * scale), round(cx + w / 2), round(fy + 20 * scale)))


def grid(rows, gap=6, bgc=(40, 36, 32)):
    W = max(sum(i.width for i in r) + gap * (len(r) - 1) for r in rows)
    H = sum(max(i.height for i in r) for r in rows) + gap * (len(rows) - 1)
    out = Image.new("RGB", (W, H), bgc)
    y = 0
    for r in rows:
        x = 0
        for i in r:
            out.paste(i.convert("RGB"), (x, y))
            x += i.width + gap
        y += max(i.height for i in r) + gap
    return out


OUT.mkdir(parents=True, exist_ok=True)
POSES = (("stance", "stance", ""), ("wind", "wind-up", "_sword_wind"), ("strike", "strike", "_sword_strike"))
# skin_2x: the new result only, two rows (bare, scale mail)
cell2 = (760, 620)
rows = []
for pre, outfit in (("b", "bare"), ("m", "scale mail")):
    rows.append([label(scene(f"{pre}_{p}", 2, cell2), f"{outfit}: {lab}") for p, lab, _ in POSES])
grid(rows).save(OUT / "skin_2x.png")
# judge_3x: each beside its drawn picture
cell3 = (1000, 900)
rows = []
for pre, outfit, key in (("b", "bare", "char_hero_bare"), ("m", "scale mail", "char_hero_scale_mail")):
    for p, lab, suf in POSES:
        rows.append([label(scene(f"{pre}_{p}", 3, cell3), f"3D drawn skin, {outfit}, {lab}"),
                     label(drawn(key + suf, 3, cell3), f"drawn: {key + suf}")])
crops = []
for name, part, half in (("b_stance", "head", 110), ("b_stance", "head", 110), ("b_stance", "hand_R", 90),
                         ("b_strike", "elbow_R", 90), ("b_strike", "knee_L", 100), ("b_stance", "foot_R", 100),
                         ("b_wind", "elbow_R", 90), ("b_wind", "head", 110)):
    m = META[name]
    h, g, k = hero_at(name, 3)
    x, y = m["lm"][part][0] * k, m["lm"][part][1] * k
    if part == "head" and len(crops) == 1:
        y -= 70                                 # the hair, above the face
    tile = Image.new("RGBA", h.size, (226, 222, 212, 255))
    tile.alpha_composite(h)
    c = tile.crop((round(x - half), round(y - half), round(x + half), round(y + half))).resize((300, 300), Image.LANCZOS)
    crops.append(label(c, f"{name[2:]}: {['face', 'hair'][len(crops)] if part == 'head' and len(crops) < 2 else part}"))
rows.append(crops[:4])
rows.append(crops[4:])
grid(rows).save(OUT / "judge_3x.png")
# game_1280
bg = backdrop(1)
h, g, _ = hero_at("b_stance", 1)
bg.alpha_composite(h, (round(HERO_SPOT[0] - g[0]), round(HERO_SPOT[1] - g[1])))
bg.convert("RGB").save(OUT / "game_1280.png")
# turnaround
ts = sorted(k for k in META if k.startswith("t") and k[1:].isdigit())
tiles = []
for t in ts:
    im = Image.open(D / f"{t}.png").convert("RGBA")
    im = im.resize((im.width // 2, im.height // 2), Image.LANCZOS)
    tile = Image.new("RGBA", im.size, (226, 222, 212, 255))
    tile.alpha_composite(im)
    tiles.append(label(tile, f"yaw {META[t]['yaw']:.0f}"))
grid([tiles[:4], tiles[4:]]).save(OUT / "turnaround.png")
print("wrote", OUT)
