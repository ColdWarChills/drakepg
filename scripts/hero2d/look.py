"""Look helper: a crop of a picture with a coordinate grid, zoomed, for reading joint points by eye.
<venv python> -I -B look.py <png> <out png> [x0 y0 x1 y1] [--zoom 2] [--step 50] [--pts x,y;x,y...]"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

a = sys.argv[1:]
src, out = Path(a[0]), Path(a[1])
zoom = float(a[a.index("--zoom") + 1]) if "--zoom" in a else 1.0
step = int(a[a.index("--step") + 1]) if "--step" in a else 50
pts = []
if "--pts" in a:
    pts = [tuple(float(v) for v in p.split(",")) for p in a[a.index("--pts") + 1].split(";") if p]
nums = [x for x in a[2:] if x.lstrip("-").isdigit()]
im = Image.open(src).convert("RGBA")
bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
bg.alpha_composite(im)
box = tuple(int(v) for v in nums[:4]) if len(nums) >= 4 else (0, 0) + im.size
crop = bg.crop(box).resize((int((box[2] - box[0]) * zoom), int((box[3] - box[1]) * zoom)), Image.LANCZOS)
d = ImageDraw.Draw(crop)
for x in range((box[0] // step + 1) * step, box[2], step):
    X = (x - box[0]) * zoom
    d.line([(X, 0), (X, crop.height)], fill=(0, 160, 255, 255) if x % (step * 2) == 0 else (0, 220, 255, 255), width=1)
    d.text((X + 2, 2), str(x), fill=(0, 0, 200, 255))
for y in range((box[1] // step + 1) * step, box[3], step):
    Y = (y - box[1]) * zoom
    d.line([(0, Y), (crop.width, Y)], fill=(0, 160, 255, 255) if y % (step * 2) == 0 else (0, 220, 255, 255), width=1)
    d.text((2, Y + 2), str(y), fill=(0, 0, 200, 255))
for i, (x, y) in enumerate(pts):
    X, Y = (x - box[0]) * zoom, (y - box[1]) * zoom
    d.ellipse([X - 5, Y - 5, X + 5, Y + 5], outline=(255, 0, 0, 255), width=2)
    d.text((X + 6, Y - 6), str(i), fill=(255, 0, 0, 255))
crop.convert("RGB").save(out)
