"""Cut Qwen hero views (white background) into square views on black at ONE shared scale (Pixal3D multi-view input).
python cut_views.py out_dir front=path right=path back=path
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
views = dict(a.split("=", 1) for a in sys.argv[2:])
SIZE, PAD = 1024, 1.12


def mask_of(rgb):
    a = rgb.astype(np.int16)
    light = a.min(axis=2) > 225
    grey = (a.max(axis=2) - a.min(axis=2)) < 16
    fg = ~(light & grey)
    fg = ndimage.binary_opening(fg, iterations=2)
    lab, n = ndimage.label(fg)
    if n > 1:
        sizes = ndimage.sum(fg, lab, range(1, n + 1))
        keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 0.002 * fg.sum()])
        fg = keep
    return ndimage.binary_fill_holes(fg)


data = {}
for v, p in views.items():
    rgb = np.array(Image.open(p).convert("RGB"))
    m = mask_of(rgb)
    ys, xs = np.nonzero(m)
    data[v] = (rgb, m, (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
side = max(max(b[2] - b[0], b[3] - b[1]) for _, _, b in data.values()) * PAD
info = {}
for v, (rgb, m, b) in data.items():
    k = (SIZE / PAD) / (b[3] - b[1])   # every view: the same body height
    rgba = np.dstack([rgb, (m * 255).astype(np.uint8)])
    im = Image.fromarray(rgba, "RGBA").crop(b)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    can = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 255))
    # feet on one line for every view: bottom of the box at the same height
    x = (SIZE - im.width) // 2
    y = SIZE - round((SIZE - SIZE / PAD) / 2) - im.height
    can.alpha_composite(im, (x, y))
    can.convert("RGB").save(out / f"{v}.png")
    info[v] = {"box": [int(t) for t in b], "scale": k}
json.dump(info, open(out / "views.json", "w"), indent=1)
print("cut", list(views), "shared side", round(side))
