"""Round 2: where are the headband tails on cutj3_s123? Red pixel columns left of the face, and a crop sheet."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from PIL import Image, ImageDraw
from jcommon import REDO, TESTS
s = np.asarray(Image.open(REDO / "cutj3_s123.png").convert("RGB")).astype(int)
R, G, B = s[..., 0], s[..., 1], s[..., 2]
red = (R > 100) & (R - G > 60) & (R - B > 50) & (G < 90)
ys, xs = np.nonzero(red)
print("red bbox", xs.min(), xs.max(), ys.min(), ys.max())
for x in range(xs.min(), xs.max(), 20):
    col = ys[(xs >= x) & (xs < x + 20)]
    print(x, col.min() if len(col) else None, col.max() if len(col) else None, len(col))
im = Image.open(REDO / "cutj3_s123.png").convert("RGB").crop((520, 260, 900, 520))
im = im.resize((im.width * 2, im.height * 2), Image.LANCZOS)
d = ImageDraw.Draw(im)
for x in range(0, 380, 20):
    d.line([(2 * x, 0), (2 * x, 10 if x % 100 else 24)], fill=(255, 255, 255))
    if x % 100 == 0: d.text((2 * x + 3, 12), str(520 + x), fill=(255, 255, 255))
for y in range(0, 260, 20):
    d.line([(0, 2 * y), (10 if y % 100 else 24, 2 * y)], fill=(255, 255, 255))
    if y % 100 == 0: d.text((26, 2 * y - 5), str(260 + y), fill=(255, 255, 255))
im.save(TESTS / "r2" / "_tails.png")
