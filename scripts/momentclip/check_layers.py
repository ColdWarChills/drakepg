"""Checks of the layer path: the placed sword against the folder's own weapon.png (where it is not cut off), the feet
and anchors found, and the outfit stills (ready and strike in two mixes) -> art/tests/momentclip/v2/layers_check.png.

<venv python> -I -B check_layers.py [layers folder]
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from common import MIXES, OUT, FONTS, SNAPSHOT, caption
from layers import PAD, Hero

folder = sys.argv[1] if len(sys.argv) > 1 else str(SNAPSHOT)
OUT.mkdir(parents=True, exist_ok=True)
hero = Hero(folder)
print("feet:", {m: {s: (round(v[0]), v[1]) for s, v in f.items()} for m, f in hero.feet.items()})
print("anchors:", {m: round(v, 1) for m, v in hero.anchor.items()})

# 1. the sword maths against weapon.png
for m in hero.moments:
    ref = np.asarray(Image.open(hero.folder / m / "weapon.png").convert("RGBA"))[..., 3].astype(float)
    w = hero.moments[m]["weapon"]
    mine = np.asarray(hero.weapon(m).placed(w["grip_xy"], w["angle"], w["scale_px_per_art_px"], hero.canvas, PAD))
    mine = mine[PAD:PAD + hero.canvas, PAD:PAD + hero.canvas, 3].astype(float)
    a, b = ref > 128, mine > 128
    iou = (a & b).sum() / max(1, (a | b).sum())
    print(f"sword {m}: IoU with weapon.png {iou:.3f} (ref {a.sum()} px, mine {b.sum()} px)")

# 2. outfit stills
font = ImageFont.truetype(str(FONTS / "AlegreyaSans-Bold.ttf"), 30)
tiles = []
for mix in ("mix1", "mix2"):
    row = []
    for m in ("ready", "strike"):
        full = hero.full(m, MIXES[mix])
        bg = Image.new("RGBA", full.size, (226, 220, 205, 255))
        bg.alpha_composite(full)
        crop = bg.crop((PAD - 200, PAD + 250, PAD + hero.canvas + 350, PAD + hero.canvas - 60)).convert("RGB")
        row.append(crop)
    tiles.append((mix, row))
cw, ch = tiles[0][1][0].size
sheet = Image.new("RGB", (cw * 2, (ch + 50) * 2), (245, 240, 230))
d = ImageDraw.Draw(sheet)
for r, (mix, row) in enumerate(tiles):
    y = r * (ch + 50)
    d.text((10, y + 8), f"{mix}: {caption(MIXES[mix])}   (left: ready, right: strike)", fill=(30, 20, 10), font=font)
    for c, t in enumerate(row):
        sheet.paste(t, (c * cw, y + 50))
sheet.thumbnail((1210, 1210))
while sheet.width * sheet.height > 1_100_000:
    sheet = sheet.resize((int(sheet.width * 0.97), int(sheet.height * 0.97)), Image.LANCZOS)
sheet.save(OUT / "layers_check.png")
print(OUT / "layers_check.png", sheet.size)
