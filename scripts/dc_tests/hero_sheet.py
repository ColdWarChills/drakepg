"""Contact sheet of the knight's Sprite Library drawings (art/tests/dc/hero/lib/*.png, or a given folder), 3x, a grid line
every 20 source px, names under each: for planning the hero repaint.  python -I -B hero_sheet.py [src dir] [out png] [scale]"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
src = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/tests/dc/hero/lib"
out = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "art/tests/dc/hero/lib_sheet.png"
S = int(sys.argv[3]) if len(sys.argv) > 3 else 3
font = ImageFont.truetype("arial.ttf", 16)
files = sorted(src.glob("*.png"))
tiles = []
for f in files:
    a = Image.open(f).convert("RGBA")
    w, h = a.size
    t = Image.new("RGBA", (w * S, h * S + 22), (120, 130, 140, 255))
    big = a.resize((w * S, h * S), Image.NEAREST)
    t.alpha_composite(big, (0, 0))
    d = ImageDraw.Draw(t)
    for x in range(0, w, 20):
        d.line([(x * S, 0), (x * S, h * S)], fill=(255, 255, 255, 60))
    for y in range(0, h, 20):
        d.line([(0, y * S), (w * S, y * S)], fill=(255, 255, 255, 60))
    d.text((2, h * S + 2), f.stem, font=font, fill=(255, 255, 255, 255))
    tiles.append(t)
W = 2400
x = y = rowh = 0
pos = []
for t in tiles:
    if x + t.width > W:
        x, y, rowh = 0, y + rowh + 8, 0
    pos.append((x, y))
    x += t.width + 8
    rowh = max(rowh, t.height)
sheet = Image.new("RGB", (W, y + rowh), (60, 64, 70))
for t, p in zip(tiles, pos):
    sheet.paste(t, p, t)
sheet.save(out)
print(out, sheet.size)
