"""Round 3: a 3x grid crop of the vest drawing's near shoulder (r2_vest_teal_s202) beside the torso part cut from it.
<venv python> -I -B r3_look_shoulder_src.py [drawing key] -> art/tests/hero_joints/r3/_shoulder_src[_key].png"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
from PIL import Image, ImageDraw

from jcommon import REDO, TESTS, fit

OUT = TESTS / "r3"
OUT.mkdir(exist_ok=True)
X0, Y0, X1, Y1 = 560, 440, 780, 690
K = 3


def tile(im):
    bg = Image.new("RGBA", im.size, (200, 200, 200, 255))
    bg.alpha_composite(im.convert("RGBA"))
    c = bg.crop((X0, Y0, X1, Y1)).resize(((X1 - X0) * K, (Y1 - Y0) * K), Image.NEAREST).convert("RGB")
    d = ImageDraw.Draw(c)
    for x in range(X0, X1, 20):
        d.line([((x - X0) * K, 0), ((x - X0) * K, c.height)], fill=(255, 0, 0) if x % 100 == 0 else (255, 160, 160))
        d.text(((x - X0) * K + 2, 2), str(x), fill=(255, 0, 0))
    for y in range(Y0, Y1, 20):
        d.line([(0, (y - Y0) * K), (c.width, (y - Y0) * K)], fill=(255, 0, 0) if y % 100 == 0 else (255, 160, 160))
        d.text((2, (y - Y0) * K + 2), str(y), fill=(255, 0, 0))
    d.ellipse([(660 - X0) * K - 5, (543 - Y0) * K - 5, (660 - X0) * K + 5, (543 - Y0) * K + 5], outline=(0, 0, 255),
              width=2)
    return c


SRC = sys.argv[1] if len(sys.argv) > 1 else "r2_vest_teal_s202"
a = tile(Image.open(REDO / f"{SRC}.png"))
if SRC == "r2_vest_teal_s202":
    b = tile(Image.open(REDO / "parts_r2" / "torso.png"))
else:  # the pixels the edit changed (>60 summed), white on the s202 drawing
    import numpy as np
    s0 = np.asarray(Image.open(REDO / "r2_vest_teal_s202.png").convert("RGB")).astype(int)
    s1 = np.asarray(Image.open(REDO / f"{SRC}.png").convert("RGB")).astype(int)
    ch = np.abs(s1 - s0).sum(2) > 60
    m = s0.copy()
    m[ch] = 255
    b = tile(Image.fromarray(m.astype(np.uint8)))
sheet = Image.new("RGB", (a.width * 2 + 8, a.height), (255, 255, 255))
sheet.paste(a, (0, 0))
sheet.paste(b, (a.width + 8, 0))
fit(sheet).save(OUT / ("_shoulder_src.png" if SRC == "r2_vest_teal_s202" else f"_shoulder_src_{SRC}.png"))
print(fit(sheet).size)
