"""Contact sheet of Qwen tries: <venv python> -I -B tries_sheet.py <out png> <try> <try> ... [--ref]
Each try cropped to its figure, same scale, label under it. --ref puts char_hero_bare first at the same scale."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

import numpy as np
from PIL import Image, ImageDraw

from jcommon import PURPLE, RAW, REDO, TESTS, fit

args = [a for a in sys.argv[1:] if not a.startswith("--")]
out, names = args[0], args[1:]
labels = dict(a.split("=", 1) for a in sys.argv[1:] if a.startswith("--label:"))  # unused hook


def fig_box(im):
    a = np.asarray(im.convert("RGB")).astype(int)
    d = np.abs(a - np.array(PURPLE)).max(-1) > 60
    ys, xs = np.nonzero(d)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


tiles = []
if "--ref" in sys.argv:
    r = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
    bg = Image.new("RGBA", r.size, PURPLE + (255,))
    bg.alpha_composite(r)
    tiles.append(("char_hero_bare (old)", bg.convert("RGB")))
for n in names:
    tiles.append((n, Image.open(REDO / f"{n}.png").convert("RGB")))
crops = []
for n, im in tiles:
    x0, y0, x1, y1 = fig_box(im)
    crops.append((n, im.crop((max(0, x0 - 10), max(0, y0 - 10), x1 + 10, y1 + 10))))
H = max(c.height for _, c in crops)
W = sum(c.width for _, c in crops) + 10 * len(crops)
sheet = Image.new("RGB", (W, H + 40), (255, 255, 255))
d = ImageDraw.Draw(sheet)
x = 0
for n, c in crops:
    sheet.paste(c, (x, H - c.height))
    d.text((x + 4, H + 10), n, fill=(0, 0, 0))
    x += c.width + 10
fit(sheet).save(TESTS / out)
print(TESTS / out, sheet.size)
