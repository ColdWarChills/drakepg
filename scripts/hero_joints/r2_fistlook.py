"""Round 2: the A3 fists (as round 1 loads them) beside the new hero's own fists on cutj3_s123 and the approved hands."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from PIL import Image, ImageDraw
import fists as fistmod
from jcommon import ROOT, TESTS, REDO, fit
F = fistmod.load_fists()
tiles = []
for m in ("ready", "wind", "strike"):
    px = F[m]["img"]
    a = px[..., 3:4]
    rgb = px[..., :3] + np.array([0.6, 0.6, 0.6]) * (1 - a)
    im = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))
    print(m, im.size, F[m]["origin"], F[m]["wrist"], round(F[m]["F"], 1), round(F[m]["blade"], 1), F[m]["grip"])
    tiles.append(im.resize((im.width * 3, im.height * 3), Image.NEAREST))
s = Image.open(REDO / "cutj3_s123.png").convert("RGB")
near = s.crop((540, 760, 660, 895)); far = s.crop((900, 380, 1030, 520))
tiles += [near.resize((near.width * 3, near.height * 3), Image.NEAREST), far.resize((far.width * 3, far.height * 3), Image.NEAREST)]
ref = Image.open(ROOT / "art/tests/sheets/layered_hero_hands.webp").convert("RGB")
r1 = ref.crop((303, 718, 443, 858)).resize((420, 420), Image.NEAREST)
tiles.append(r1)
W = sum(t.width for t in tiles) + 10 * len(tiles)
H = max(t.height for t in tiles)
sheet = Image.new("RGB", (W, H), (255, 255, 255))
x = 0
for t in tiles:
    sheet.paste(t, (x, 0)); x += t.width + 10
fit(sheet).save(TESTS / "r2" / "_fistlook.png")
print(sheet.size)
