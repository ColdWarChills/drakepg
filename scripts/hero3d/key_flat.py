"""Cut a hero clip off a flat light backdrop: backdrop-coloured areas touching the frame edge go, and so do large
enclosed backdrop-coloured holes (between an arm and the body). The drawn outline stops the fill, so a light shirt
inside the outline stays. <venv python> -I key_flat.py <frames dir> <out dir> [--tol 16]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

a = sys.argv[1:]
src, out = Path(a[0]), Path(a[1])
tol = float(a[a.index("--tol") + 1]) if "--tol" in a else 16
out.mkdir(parents=True, exist_ok=True)
for p in sorted(src.glob("*.png")):
    im = np.array(Image.open(p).convert("RGB")).astype(np.int16)
    border = np.concatenate([im[:4].reshape(-1, 3), im[-4:].reshape(-1, 3), im[:, :4].reshape(-1, 3), im[:, -4:].reshape(-1, 3)])
    bg = np.median(border, 0)
    d = np.abs(im - bg).max(2)
    cand = d < tol
    lab, n = ndimage.label(cand)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    sizes = ndimage.sum(np.ones_like(lab), lab, range(n + 1))
    means = ndimage.mean(d, lab, range(n + 1))
    drop = np.zeros(n + 1, bool)
    for i in edge:
        drop[i] = True
    drop |= (sizes > 120) & (means < tol * 0.45)
    drop[0] = False
    gone = drop[lab]
    alpha = Image.fromarray(np.where(gone, 0, 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    rgba = Image.fromarray(im.astype(np.uint8)).convert("RGBA")
    rgba.putalpha(alpha)
    rgba.save(out / p.name)
print("keyed", len(list(out.glob("*.png"))))
