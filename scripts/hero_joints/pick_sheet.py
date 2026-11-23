"""character_pick.png: all Qwen tries on one sheet, same scale, with the measured arm and the verdict."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image, ImageDraw

from jcommon import PURPLE, RAW, REDO, TESTS, fit

M = json.loads((REDO / "measure.json").read_text())
V = {
    "cutj_s101": "A pose, but faces us; taller, slimmer",
    "cutj_s102": "faces us; tall",
    "cutj_s103": "faces us; very tall, small head",
    "cutj_s104": "faces us; long thin arms",
    "cutj2_s111": "still faces us, tall",
    "cutj2_s112": "faces us; sleeve folds",
    "cutj2_s113": "faces us, tall",
    "cutj3_s121": "his own pose; drawn rim, shading stripes on sleeve",
    "cutj3_s122": "his own pose; sleeve kinks at the elbow",
    "cutj3_s123": "PICKED: plainest tube sleeve, cuff, collar, no rim",
}
tiles = []
r = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
bg = Image.new("RGBA", r.size, PURPLE + (255,))
bg.alpha_composite(r)
tiles.append(("char_hero_bare (old, in the game)", bg.convert("RGB"), ""))
for n in V:
    tiles.append((n, Image.open(REDO / f"{n}.png").convert("RGB"), V[n]))
crops = []
for n, im, v in tiles:
    a = np.asarray(im).astype(int)
    ys, xs = np.nonzero(np.abs(a - np.array(PURPLE)).max(-1) > 60)
    c = im.crop((xs.min() - 8, ys.min() - 8, xs.max() + 8, ys.max() + 8))
    # one scale for all: 0.45 of the drawing (they are drawn on the same 1536 canvas)
    c = c.resize((round(c.width * 0.45), round(c.height * 0.45)), Image.LANCZOS)
    crops.append((n, c, v))
cols = 6
tw = max(c.width for _, c, _ in crops)
th = max(c.height for _, c, _ in crops)
rows = (len(crops) + cols - 1) // cols
sheet = Image.new("RGB", (cols * (tw + 10), rows * (th + 64) + 10), (255, 255, 255))
d = ImageDraw.Draw(sheet)
for i, (n, c, v) in enumerate(crops):
    x, y = (i % cols) * (tw + 10), (i // cols) * (th + 64)
    sheet.paste(c, (x, y + th - c.height))
    d.text((x + 2, y + th + 4), n, fill=(0, 0, 0))
    m = M.get(n)
    if m:
        d.text((x + 2, y + th + 18), f"arm {m['arm_vs_old']:.2f}x old, sleeve {m['w_vs_old']:.2f}x old forearm",
               fill=(60, 60, 60))
    d.text((x + 2, y + th + 32), v, fill=(150, 0, 0) if "PICKED" in v else (60, 60, 60))
    if "PICKED" in v:
        d.rectangle([x, y, x + tw, y + th + 60], outline=(220, 0, 0), width=3)
fit(sheet).save(TESTS / "character_pick.png")
print(TESTS / "character_pick.png", sheet.size)
