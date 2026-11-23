"""Look at the references once: char_hero_bare and the three A3 moments, same height, with a pixel grid."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
from PIL import Image, ImageDraw

from jcommon import RAW, TESTS, a3_moment, fit, load, on_bg

tiles = []
hb = load(RAW / "char_hero_bare.png")
tiles.append(("char_hero_bare", hb.crop(hb.getbbox())))
for m in ("ready", "wind", "strike"):
    im = a3_moment(m)
    tiles.append((m, im.crop(im.getbbox())))
H = 900
row = []
for name, t in tiles:
    k = H / t.height if name == "char_hero_bare" else H / 1100
    t2 = t.resize((round(t.width * k), round(t.height * k)), Image.LANCZOS)
    row.append((name, on_bg(t2)))
W = sum(r.width for _, r in row) + 20 * len(row)
sheet = Image.new("RGB", (W, max(r.height for _, r in row) + 30), (255, 255, 255))
d = ImageDraw.Draw(sheet)
x = 0
for name, r in row:
    sheet.paste(r, (x, 30))
    d.text((x + 5, 5), name, fill=(0, 0, 0))
    x += r.width + 20
fit(sheet).save(TESTS / "_refs.png")
print(sheet.size)
