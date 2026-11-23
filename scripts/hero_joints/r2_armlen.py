"""Round 2: is the new hero's near arm shorter than the old one? cutj3_input (char_hero_bare on the same canvas) and
cutj3_s123 side by side and blended, near arm crop, with a 20 px grid."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from PIL import Image, ImageDraw
from jcommon import REDO, TESTS, fit
a = Image.open(REDO / "cutj3_input.png").convert("RGB")
b = Image.open(REDO / "cutj3_s123.png").convert("RGB")
box = (470, 440, 780, 900)
ca, cb = a.crop(box), b.crop(box)
bl = Image.blend(ca, cb, 0.5)
tiles = []
for t in (ca, cb, bl):
    t = t.copy(); d = ImageDraw.Draw(t)
    for y in range(0, t.height, 20):
        d.line([(0, y), (6 if y % 100 else 20, y)], fill=(255, 255, 255))
        if y % 100 == 0: d.text((22, y - 5), str(box[1] + y), fill=(255, 255, 255))
    for x in range(0, t.width, 20):
        d.line([(x, 0), (x, 6 if x % 100 else 20)], fill=(255, 255, 255))
    tiles.append(t)
sheet = Image.new("RGB", (3 * 310 + 20, 460), (0, 0, 0))
for i, t in enumerate(tiles):
    sheet.paste(t, (i * 320, 0))
sheet = sheet.resize((sheet.width * 3 // 2, sheet.height * 3 // 2), Image.LANCZOS)
fit(sheet).save(TESTS / "r2" / "_armlen.png")
