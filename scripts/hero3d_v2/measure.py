"""Print the silhouette runs of the drawn orthographic views (front / right), row by row, with each run's mean colour.
Used to read widths and depths for build_hero.py (numbers are typed into its PARTS table by hand).
<venv python> -I scripts/hero3d_v2/measure.py <cutB dir> [step]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

D = Path(sys.argv[1])
STEP = int(sys.argv[2]) if len(sys.argv) > 2 else 12
for view in ("front", "right"):
    a = np.asarray(Image.open(D / f"{view}.png").convert("RGB")).astype(int)
    m = a.sum(2) > 60
    ys = np.where(m.any(1))[0]
    top, bot = ys.min(), ys.max()
    print(f"== {view}: top {top} bottom {bot} height {bot - top}")
    for y in range(top, bot + 1, STEP):
        row = m[y]
        runs, x = [], 0
        while x < len(row):
            if row[x]:
                s = x
                while x < len(row) and row[x]:
                    x += 1
                if x - s > 3:
                    c = a[y, s:x].mean(0).astype(int)
                    runs.append(f"{s}-{x} #{c[0]:02x}{c[1]:02x}{c[2]:02x}")
            x += 1
        print(f"{y:4d} ({(bot - y) / (bot - top):.3f}): " + "  ".join(runs))
