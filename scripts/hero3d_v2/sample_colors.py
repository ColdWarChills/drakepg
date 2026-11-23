"""Dominant colours (lit and shade modes) of each garment in the drawn hero, char_hero_bare.png.
<venv python> -I scripts/hero3d_v2/sample_colors.py <char_hero_bare.png>"""
import sys
from collections import Counter

import numpy as np
from PIL import Image

im = Image.open(sys.argv[1])
a = np.asarray(im.convert("RGBA")).astype(int)
print("alpha corner", a[5, 5], "alpha min/max", a[..., 3].min(), a[..., 3].max())
BOX = {  # x0, y0, x1, y1 boxes inside each garment (1024x1536 image)
    "hair": (360, 290, 520, 340), "band": (360, 330, 520, 375), "skin_face": (420, 390, 500, 480),
    "skin_arm": (220, 700, 290, 880), "skin_fist": (610, 520, 690, 600), "shirt": (260, 500, 560, 720),
    "belt": (360, 730, 500, 760), "buckle": (452, 728, 488, 765), "trousers": (240, 780, 620, 1060),
    "boots": (150, 1060, 700, 1300)}
for k, (x0, y0, x1, y1) in BOX.items():
    px = a[y0:y1, x0:x1, :3].reshape(-1, 3)
    q = (px // 12) * 12 + 6
    c = Counter(map(tuple, q))
    top = c.most_common(6)
    tot = len(px)
    out = []
    for col, n in top:
        m = np.all(q == col, axis=1)
        mean = px[m].mean(0).astype(int)
        out.append(f"#{mean[0]:02x}{mean[1]:02x}{mean[2]:02x} {100 * n / tot:.0f}%")
    print(f"{k:10s}", "  ".join(out))
