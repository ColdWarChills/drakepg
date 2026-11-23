"""Every piece at master size (hero 960 px), pivot red, tip blue, named points green. -> art/tests/hero2d/pieces_sheet.png"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, TESTS, ROOT, Kit  # noqa: E402

kit = Kit()
f = 960 / kit.height
try:
    font = ImageFont.truetype("arial.ttf", 16)
except OSError:
    font = ImageFont.load_default()
tiles = []
for k, v in kit.p.items():
    im = Image.open(KIT / v["file"]).convert("RGBA")
    im = im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)
    t = Image.new("RGBA", (im.width + 20, im.height + 46), (236, 230, 216, 255))
    t.alpha_composite(im, (10, 36))
    d = ImageDraw.Draw(t)
    def mark(p, col):
        x, y = p[0] * f + 10, p[1] * f + 36
        d.ellipse([x - 5, y - 5, x + 5, y + 5], outline=col, width=2)
        d.line([x - 8, y, x + 8, y], fill=col)
        d.line([x, y - 8, x, y + 8], fill=col)
    mark(v["pivot"], (220, 0, 0))
    mark(v["tip"], (0, 60, 220))
    for p in v["points"].values():
        mark(p, (0, 150, 0))
    d.text((6, 6), k, fill=(40, 30, 20), font=font)
    tiles.append(t)
W = 1900
x = y = rowh = 0
pos = []
for t in tiles:
    if x + t.width > W:
        x, y, rowh = 0, y + rowh + 10, 0
    pos.append((x, y))
    x += t.width + 10
    rowh = max(rowh, t.height)
sheet = Image.new("RGB", (W, y + rowh + 50), (250, 247, 240))
for t, p in zip(tiles, pos):
    sheet.paste(t.convert("RGB"), p)
ImageDraw.Draw(sheet).text((10, y + rowh + 20), "red = pivot (joint to the parent), blue = tip (next joint / blade "
                           "direction), green = named points (shoulders, hips, neck, grip, thumb)", fill=(40, 30, 20), font=font)
sheet.save(TESTS / "pieces_sheet.png")
