"""The far arm (his left) swung clear of the body, hero at 720 px (3x game size): upper arm angles x elbow folds.
<venv python> -I -B far_arm_test.py [kit json] -> art/tests/hero2d/r2/far_arm_test_3x.png"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, TESTS, Kit, draw_hero, joints, rest_pose, to_image  # noqa: E402

kit = Kit(Path(sys.argv[1]) if len(sys.argv) > 1 else KIT / "pieces.json")
OUTH = 720
ARMS = [(140, "back"), (99, "rest"), (60, "forward-down"), (20, "forward"), (-30, "forward and up")]
FLEX = [0, 60]
C = round(1100 * kit.height / 1090)
f = OUTH / kit.height
cell = round(C * f)
sheet = Image.new("RGB", (cell * len(ARMS), (cell + 34) * len(FLEX)), (236, 230, 216))
d = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arial.ttf", 18)
except OSError:
    font = ImageFont.load_default()
for j, fl in enumerate(FLEX):
    for i, (a, label) in enumerate(ARMS):
        p = rest_pose(kit)
        p["waist"] = (0.0, 0.0)
        sh = joints(kit, p)["f_shoulder"]
        p["waist"] = (C / 2 - sh[0], C / 2 - sh[1] + 40)
        p.update({"f_upper": a, "f_flex": fl, "sword": False})
        px, _ = draw_hero(kit, p, (C, C), only={"torso", "f_pad", "head", "hips", "f_upper", "f_fore", "f_cuff", "f_fist"})
        im = to_image(px, OUTH, kit.height)
        x, y = i * cell, j * (cell + 34)
        sheet.paste(im, (x, y + 34), im)
        d.rectangle([x, y, x + cell - 1, y + cell + 33], outline=(180, 170, 150))
        d.text((x + 6, y + 6), f"far upper {a} ({label}), elbow {fl}", fill=(40, 30, 20), font=font)
out = TESTS / ("far_arm_test_3x.png" if len(sys.argv) < 3 else sys.argv[2])
sheet.save(out)
print(out, sheet.size)
