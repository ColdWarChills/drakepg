"""Bend test of the sword arm: six upper-arm angles x elbow folds 0/45/90/120, hero at 720 px (3x game size).
<venv python> -I -B arm_test.py [kit json] [out name] [elbow folds, e.g. 45,120]  ->  art/tests/hero2d/r2/arm_test_3x.png"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, TESTS, Canvas, Kit, ROOT, to_image  # noqa: E402
from rig import draw_hero, joints, rest_pose  # noqa: E402

kit = Kit(Path(sys.argv[1]) if len(sys.argv) > 1 else KIT / "pieces.json")
OUTH = 720
ARMS = [(-135, "raised up and back (wind-up)"), (-90, "straight up"), (-40, "forward and up"), (0, "straight forward"),
        (45, "forward-down (after a cut)"), (100, "hanging (rest)")]
FLEX = [int(v) for v in sys.argv[3].split(",")] if len(sys.argv) > 3 else [0, 45, 90, 120]
WRIST = {-135: 95, -90: 95, -40: 95, 0: 95, 45: 60, 100: 95}
C = round(1300 * kit.height / 1090)  # cell size in kit px, scaled so a cell is as many sheet px as in step 1
f = OUTH / kit.height
cell = round(C * f)
sheet = Image.new("RGB", (cell * len(FLEX), (cell + 40) * len(ARMS)), (236, 230, 216))
d = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arial.ttf", 18)
except OSError:
    font = ImageFont.load_default()
for i, (a, label) in enumerate(ARMS):
    for j, fl in enumerate(FLEX):
        p = rest_pose(kit)
        p["waist"] = (0.0, 0.0)
        shw = joints(kit, p)["n_shoulder"]
        p["waist"] = (C / 2 - shw[0], C / 2 - shw[1])
        p.update({"n_upper": a, "n_flex": fl, "n_wrist": WRIST[a]})
        px, inf = draw_hero(kit, p, (C, C), only={"torso", "head", "n_upper", "n_pad", "n_fore", "n_cuff", "sword", "n_fist"})
        info = inf["arm"]
        sh = (C / 2, C / 2)
        im = to_image(px, OUTH, kit.height)
        x, y = j * cell, i * (cell + 40)
        sheet.paste(im, (x, y + 40), im)
        d.rectangle([x, y, x + cell - 1, y + cell + 39], outline=(180, 170, 150))
        d.text((x + 6, y + 4), f"upper {a} ({label}), elbow {fl}, wrist {info['wrist_deg']:.0f} (blade {info['a_blade']:.0f})",
               fill=(40, 30, 20), font=font)
        sx, sy = x + sh[0] * f, y + 40 + sh[1] * f
        d.ellipse([sx - 3, sy - 3, sx + 3, sy + 3], outline=(200, 0, 0))
out = TESTS / (sys.argv[2] if len(sys.argv) > 2 else "arm_test_3x.png")
sheet.save(out)
print(out, sheet.size)
