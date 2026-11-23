"""Check 1 (hand): each fist on the new sleeve at its key pose, 4x, beside the approved hands (layered_hero_hands).
<venv python> -I -B fistcheck.py [kind] [out]  -> art/tests/hero_joints/fists_4x.png"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

import numpy as np
from PIL import Image, ImageDraw

import keys
import rig
from jcommon import ROOT, TESTS, fit

kind = sys.argv[1] if len(sys.argv) > 1 else "B"
out = sys.argv[2] if len(sys.argv) > 2 else "fists_4x.png"
parts = rig.Parts()
fr = rig.Frame(parts)
BG = np.array([0.82, 0.80, 0.76], np.float32)
tiles = []
for m in ("ready", "wind", "strike"):
    pose = keys.pose_for(kind, keys.KEYS[m])
    px, info = fr.render(kind, pose)
    im = rig.to_image(px, BG)
    g = info["W"]
    # the same zoom as the approved sheet: its hero is ~319 px tall (ours 1028), then both 4x
    k = 319 / 1028
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    c = 70
    gx, gy = g[0] * k, g[1] * k
    box = (int(gx - c), int(gy - c), int(gx + c), int(gy + c))
    crop = im.crop(box).resize((4 * 2 * c, 4 * 2 * c), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    d.text((6, 6), f"{m} ({kind}): turn {info['turn']:.0f}, delta {info['delta']:+.0f}, forearm {info['F']:.0f}",
           fill=(0, 0, 0))
    tiles.append(crop)
    print(m, {k: (np.round(v, 1).tolist() if isinstance(v, np.ndarray) else (round(v, 1) if isinstance(v, float) else v))
              for k, v in info.items()})
# the approved hands: ready and strike sword hands of the layered hero (row 2 and 3, first column), 4x of game size
ref = Image.open(ROOT / "art/tests/sheets/layered_hero_hands.webp").convert("RGB")
r1 = ref.crop((303, 718, 443, 858)).resize((560, 560), Image.NEAREST)
r2 = ref.crop((403, 1166, 543, 1306)).resize((560, 560), Image.NEAREST)
W = sum(t.width for t in tiles) + 2 * 560 + 50
sheet = Image.new("RGB", (W, 600), (255, 255, 255))
x = 0
for t in tiles:
    sheet.paste(t, (x, 30))
    x += t.width + 10
ImageDraw.Draw(sheet).text((x + 5, 8), "approved (layered_hero_hands): ready, strike", fill=(0, 0, 0))
sheet.paste(r1, (x, 30))
sheet.paste(r2, (x + 570, 30))
ImageDraw.Draw(sheet).text((5, 8), "new sleeve + A3 fists, 4x (nearest)", fill=(0, 0, 0))
fit(sheet, 1500, 1.05).save(TESTS / out)
print(TESTS / out)
