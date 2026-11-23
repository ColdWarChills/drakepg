"""Outline width where the line sits between the background and a light fill; fist crops with a 10%-of-height grid."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

W = Path(sys.argv[1]); SCR = Path(sys.argv[2])
for name, path in (("drawn", W / "art/raw/char_hero_bare.png"), ("3D", W / "art/redo/hero3d_v2/tmp/final/look_bare.png")):
    img = Image.open(path).convert("RGBA")
    im = np.asarray(img).astype(float)
    a = im[..., 3] > 128
    ys, xs = np.nonzero(a)
    y0, y1 = ys.min(), ys.max(); H = y1 - y0 + 1
    lum = im[..., :3] @ [0.3, 0.59, 0.11]
    runs = []
    for y in range(y0, y1):
        row = np.nonzero(a[y])[0]
        if len(row) < 4:
            continue
        for x0, st in ((row.min(), 1), (row.max(), -1)):
            n, x = 0, x0
            for _ in range(8):
                if lum[y, x] > 140 and a[y, x]: x += st
            while a[y, x] and lum[y, x] < 90 and n < 30:
                n += 1; x += st
            if 0 < n < 30 and lum[y, x] > 150:          # line then a light fill (skin, shirt)
                runs.append(n)
    runs = np.array(runs)
    print(f"{name}: H {H}px, light-fill edge rows {len(runs)}, dark line median {np.median(runs):.1f}px "
          f"(p25 {np.percentile(runs, 25):.1f}, p75 {np.percentile(runs, 75):.1f}) = {np.median(runs) / H * 1000:.2f}/1000 H")
    # fist crops scaled to a common 1000 px height
    k = 1000 / H
    s = img.crop((xs.min(), y0, xs.max() + 1, y1 + 1)).resize((round((xs.max() - xs.min() + 1) * k), 1000))
    d = ImageDraw.Draw(s)
    for gx in range(0, s.width, 20):
        d.line((gx, 0, gx, s.height), fill=(0, 160, 255, 90) if gx % 100 else (255, 0, 0, 160))
    s.save(SCR / f"grid_{name}.png")

