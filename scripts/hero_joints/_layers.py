import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from PIL import Image
from jcommon import REDO, TESTS, fit
box = (500, 380, 1000, 900)
ims = []
for k in ("torso", "hips", "near_sleeve"):
    im = Image.open(REDO / "parts" / f"{k}.png").convert("RGBA").crop(box)
    bg = Image.new("RGBA", im.size, (120, 200, 120, 255)); bg.alpha_composite(im); ims.append(bg.convert("RGB"))
W = sum(i.width for i in ims); sh = Image.new("RGB", (W + 20, ims[0].height), (255,255,255)); x = 0
for i in ims: sh.paste(i, (x, 0)); x += i.width + 10
fit(sh).save(TESTS / "_layers.png")
