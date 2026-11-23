"""Round 5 fix (the wind-up): today's wind-up drawing moved onto hero 4's canvas, the Qwen input. As r5_prep.py.
<venv python> -I -B r5w_prep.py
The raw edit art/redo/pose/sword_wind_s4243.png (base edit canvas; hero_poses.py picked seed 4243 for sword_wind) is
scaled by 1029/1034 about its feet and moved so its lowest pixel is on hero 4's feet line (1283), its x kept relative to
the canvas centre (as the game swaps it). -> art/redo/hero_joints/r5w_input.png (on s202's purple)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image

from jcommon import REDO, ROOT

SRC = ROOT / "art" / "redo" / "pose" / "sword_wind_s4243.png"
K = 1029 / 1034
FEET_Y = 1283.0
CX_SRC, CX_DST = 768.0, 767.0
BG = (152, 51, 201)


def transform():
    a = np.asarray(Image.open(SRC).convert("RGBA"))[..., 3] >= 10
    ys, _ = np.nonzero(a)
    y_feet = float(ys.max())
    return dict(k=K, tx=CX_DST - CX_SRC * K, ty=FEET_Y - y_feet * K, src=SRC.name, src_feet=y_feet)


def main():
    t = transform()
    im = Image.open(SRC).convert("RGBA")
    a = np.asarray(im).copy()
    a[a[..., 3] < 10] = 0
    im = Image.fromarray(a)
    W = round(im.width * t["k"])
    im = im.resize((W, W), Image.LANCZOS)
    out = Image.new("RGBA", (1536, 1536), BG + (255,))
    out.alpha_composite(im, (round(t["tx"]), round(t["ty"])))
    out.convert("RGB").save(REDO / "r5w_input.png")
    print(json.dumps(t))


if __name__ == "__main__":
    main()
