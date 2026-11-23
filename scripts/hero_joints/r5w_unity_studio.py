"""Wind-up fix round: numbers and feet from a Unity studio render (Test2DRun.Studio(dir, w, h, ox, oy)).
<venv python> -I -B r5w_unity_studio.py <studio dir> <ox> <oy>

Per clip frame: the sprite set, the joints (canvas px: hero-root px + (772, 1283)) against windup_numbers.json's
unity_geo on the wind frames, and the back boot measured on the rendered silhouette the same way on every frame: its rear
edge (leftmost hero pixel in the sole rows, canvas y 1262-1290, x < 700) and the lowest hero pixel there (the sole)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image

from jcommon import TESTS

D = pathlib.Path(sys.argv[1])
OX, OY = float(sys.argv[2]), float(sys.argv[3])
ROOT = np.array([772.0, 1283.0])
BG = np.array([0.55, 0.62, 0.58]) * 255
WN = json.loads((TESTS / "r5" / "windup_numbers.json").read_text())["unity_geo"]


def hero_mask(im):
    a = np.asarray(im.convert("RGB")).astype(float)
    return np.abs(a - BG).max(-1) > 12


def back_boot(m):
    y0, y1 = int(1262 - OY), int(1290 - OY)
    xs_lim = int(700 - OX)
    rows = m[y0:y1, :xs_lim]
    ys, xs = np.nonzero(rows)
    if not len(xs):
        return None
    rear = xs.min() + OX
    # the sole: lowest hero pixel in the back boot's columns (rear .. rear + 120)
    cols = m[:, int(rear - OX):int(rear - OX) + 120]
    yy = np.nonzero(cols.any(1))[0]
    return float(rear), float(yy.max() + OY)


def main():
    js = json.loads((D / "studio.json").read_text())
    out = []
    for j in js:
        f = j["f"]
        m = hero_mask(Image.open(D / f"s{f:03d}.png"))
        bb = back_boot(m)
        c = {k: (np.array(j[k]) + ROOT).round(1).tolist() for k in ("S", "elbow", "wrist", "waist", "neck")}
        line = f"f{f:02d} {j['hips_pic']:<10} {j['torso_pic']:<13} {j['head_pic']:<11} flip {j['flip']} " \
               f"upper {j['upper']:7.1f} fore {j['fore']:7.1f} hand {j['hand']:7.1f} miss {j['reach_miss']:.1f} " \
               f"back boot rear/sole {bb}"
        if j["hips_pic"].startswith("hips_wind"):
            d = {k: round(float(np.hypot(*(np.array(c[k2]) - np.array(WN[k])))), 1)
                 for k, k2 in (("S", "S"), ("E", "elbow"), ("W", "wrist"))}
            line += f"  vs still S/E/W {d}"
        print(line)
        out.append({"f": f, "back_boot": bb, **c, "hips": j["hips_pic"]})
    (D / "feet.json").write_text(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
