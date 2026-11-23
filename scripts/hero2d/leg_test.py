"""Bend test of the legs at 3x game size (hero 720 px): standing, the bent front leg of a deep lunge, the stretched back
leg; near leg (his right) and far leg, each with the hips and the other leg at rest. <venv python> -I -B leg_test.py -> art/tests/hero2d/leg_test_3x.png"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import TESTS, ROOT, Kit, draw_hero, rest_pose, to_image  # noqa: E402

kit = Kit()
rp = rest_pose(kit)
OUTH = 720
f = OUTH / kit.height
CASES = [("standing", None, None), ("the stance's own bend", "stance", None), ("front leg of a deep lunge", 35.0, 85.0),
         ("stretched back leg", 128.0, 4.0)]
STANCE = {"n": (112.0, 14.0), "f": (68.0, 22.0)}   # replaced by the fitted stance once it exists (stance.py)
try:
    import json as _j
    STANCE.update({k: tuple(v) for k, v in _j.loads((TESTS / "stance_legs.json").read_text()).items()})
except (OSError, ValueError):
    pass
C = (round(900 * kit.height / 1090),) * 2
cw, ch = round(C[0] * f), round(C[1] * f)
sheet = Image.new("RGB", (cw * 4, (ch + 40) * 2), (236, 230, 216))
d = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arial.ttf", 18)
except OSError:
    font = ImageFont.load_default()
for r, s in enumerate(("n", "f")):
    for c, (name, th, kn) in enumerate(CASES):
        p = dict(rp)
        p["waist"] = (450.0, 200.0)
        if th == "stance":
            th, kn = STANCE[s]
        if th is not None:
            p[f"{s}_thigh"], p[f"{s}_knee"] = th, kn
            # sole flat on the ground
            p[f"{s}_foot"] = -((th + kn) - (rp[f"{s}_thigh"] + rp[f"{s}_knee"]))
        px, _ = draw_hero(kit, p, C, only={"hips", "n_thigh", "n_shin", "n_boot", "f_thigh", "f_shin", "f_boot"})
        im = to_image(px, OUTH, kit.height)
        x, y = c * cw, r * (ch + 40)
        sheet.paste(im, (x, y + 40), im)
        d.rectangle([x, y, x + cw - 1, y + ch + 39], outline=(180, 170, 150))
        th_, kn_ = p[f"{s}_thigh"], p[f"{s}_knee"]
        d.text((x + 6, y + 6), f"{'near' if s == 'n' else 'far'} leg, {name}: thigh {th_:.0f}, knee {kn_:.0f}",
               fill=(40, 30, 20), font=font)
sheet.save(TESTS / "leg_test_3x.png")
