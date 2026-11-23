"""Gate 1 sheet: per moment the whole hero with the sword, the sword hand, the far hand and the near arm (shoulder to
wrist) at 3x game size (him 720 px tall), plus the head height (hair top to chin) of each moment.

<venv python> -I -B gate1.py <out name> ready=<try> wind=<try> strike=<try> follow=<try>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, TESTS, load_rgba  # noqa: E402
import bare  # noqa: E402
import layers  # noqa: E402
import place  # noqa: E402
import sheets  # noqa: E402

CELL = 360


def head_height(a):
    c = place.classes(a)
    hb = place.head_box(c)
    face = c["skin"] & hb
    lab, n = ndi.label(face)
    if not n:
        return None
    sizes = ndi.sum(face, lab, range(1, n + 1))
    f = lab == 1 + int(np.argmax(sizes))
    ys, xs = np.nonzero(f)
    chin = ys.max()
    cols = slice(xs.min() - 60, xs.max() + 60)
    fig = c["fig"][:, cols]
    top = np.nonzero(fig.any(1) & (np.arange(fig.shape[0]) < chin))[0].min()
    return int(chin - top)


def arm_boxes(a, parts):
    terr = layers.territories(a, parts["rod"])
    c = place.classes(a)
    arms = c["skin"] & ~place.head_box(c)
    near = arms & terr["near_side"]
    far = arms & ~terr["near_side"] & ~ndi.binary_dilation(near, iterations=8)
    ys = np.nonzero(c["fig"])[0]
    far[int(ys.min() + 0.62 * (ys.max() - ys.min())):] = False  # a free hand never hangs below his hips here
    lab, n = ndi.label(far)
    if n:
        sizes = ndi.sum(far, lab, range(1, n + 1))
        far = lab == 1 + int(np.argmax(sizes))
    return near, far


def tip(mask, away_from):
    """The point of mask farthest from a point (the fist end of an arm)."""
    ys, xs = np.nonzero(mask)
    d = (xs - away_from[0]) ** 2 + (ys - away_from[1]) ** 2
    i = int(np.argmax(d))
    return xs[i], ys[i]


def main():
    out = sys.argv[1]
    picks = dict(kv.split("=") for kv in sys.argv[2:])
    rows, cells = [], {}
    cols = ["whole", "sword hand 3x", "far hand 3x", "near arm 3x"]
    for m in bare.MOMENTS:
        if m not in picks:
            continue
        a = load_rgba(REDO / f"{picks[m]}.png")
        p = bare.build(a, m, picks[m])
        comp = bare.compose(p)
        hh = head_height(a)
        r = f"{bare.LABEL[m] if hasattr(bare, 'LABEL') else m}\nhead {hh}px"
        rows.append(r)
        x0, y0, x1, y1 = sheets.bbox(comp, 20)
        w = Image.new("RGBA", (x1 - x0, y1 - y0), sheets.PAPER + (255,))
        w.alpha_composite(Image.fromarray(comp).crop((x0, y0, x1, y1)))
        w.thumbnail((CELL, CELL))
        c = Image.new("RGBA", (CELL, CELL), sheets.PAPER + (255,))
        c.paste(w, ((CELL - w.width) // 2, (CELL - w.height) // 2))
        cells[(r, "whole")] = c
        cells[(r, "sword hand 3x")] = sheets.crop3x(comp, p["rod"]["center"], CELL / 2 / 0.72, CELL)
        near, far = arm_boxes(a, p)
        ys, xs = np.nonzero(a[..., 3] > 0)
        body_c = (xs.mean(), ys.mean())
        if far.any():
            cells[(r, "far hand 3x")] = sheets.crop3x(comp, tip(far, body_c), CELL / 2 / 0.72, CELL)
        if near.any():
            # the elbow: the near-arm skin point midway between the fist and the far end of the arm (true 3x)
            fist_c = p["rod"]["center"]
            shoulder = tip(near, fist_c)
            ys, xs = np.nonzero(near)
            mid = ((fist_c[0] + shoulder[0]) / 2, (fist_c[1] + shoulder[1]) / 2)
            i = int(np.argmin((xs - mid[0]) ** 2 + (ys - mid[1]) ** 2))
            cells[(r, "near arm 3x")] = sheets.crop3x(comp, (xs[i], ys[i]), CELL / 2 / 0.72, CELL)
        print(m, picks[m], "head", hh, "angle", round(p["rod"]["angle"]), flush=True)
    s = sheets.grid(rows, cols, cells, f"Gate 1: {out}", CELL)
    s.save(TESTS / f"{out}.png")
    print(TESTS / f"{out}.png", s.size)


if __name__ == "__main__":
    main()
