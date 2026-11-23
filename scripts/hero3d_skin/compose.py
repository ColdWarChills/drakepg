"""Merge projection layers (project.py) into one atlas, hard priority: the first layer that painted a texel wins; never a
blend of two drawings. Texels next to painted ones (skipped bands, seams) take the nearest painted colour of the same
object; the rest stay unpainted (alpha 0 in <out>_holes.png, shown magenta by skin_blend.py show --magenta) and get the
old flat look (fallback.npy) in <out>.png. Optional --snap: colours snapped to the nearest colour of the drawings'
palette (k-means of the painted texels, 48 colours).
<venv python> -I compose.py <out png> <tex_id.npy> <fallback.npy> <layer.npz> [<layer.npz> ...] [--grow 24] [--snap]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

a = [x for x in sys.argv[1:]]
GROW = int(a[a.index("--grow") + 1]) if "--grow" in a else 24
SNAP = "--snap" in a
a = [x for i, x in enumerate(a) if not x.startswith("--") and not (i > 0 and a[i - 1] == "--grow")]
OUT, TID, FB, LAYERS = Path(a[0]), np.load(a[1]), np.load(a[2]), a[3:]
H, W = TID.shape
col = np.zeros((H, W, 3), np.uint8)
done = np.zeros((H, W), bool)
src = np.zeros((H, W), np.uint8)
for i, lp in enumerate(LAYERS, 1):
    L = np.load(lp)
    take = L["ok"] & ~done
    col[take] = L["colour"][take]
    src[take] = i
    done |= take
    print(f"LAYER {i} {Path(lp).name}: {take.sum()} texels")
used = TID > 0
print(f"painted {done.sum()} of {used.sum()} ({done.sum() / used.sum():.2f})")
for k in np.unique(TID[used]):
    obj = TID == k
    have = done & obj
    if not have.any():
        continue
    d, (iy, ix) = ndi.distance_transform_edt(~have, return_indices=True)
    grow = obj & ~done & (d <= GROW)
    col[grow] = col[iy[grow], ix[grow]]
    done |= grow
print(f"after grow {done.sum()} ({done.sum() / used.sum():.2f})")
if SNAP:
    import cv2
    px = col[done].astype(np.float32)
    sample = px[np.random.default_rng(1).choice(len(px), min(200000, len(px)), replace=False)]
    _, _, centers = cv2.kmeans(sample, 48, None, (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 20, 0.5), 2,
                               cv2.KMEANS_PP_CENTERS)
    lab = np.argmin(((px[:, None] - centers[None]) ** 2).sum(-1), 1)
    col[done] = centers[lab].astype(np.uint8)
fb = np.clip(FB[..., :3], 0, 1)
fb = np.where(fb <= 0.0031308, fb * 12.92, 1.055 * fb ** (1 / 2.4) - 0.055)
fbu = (fb * 255).round().astype(np.uint8)
holes = np.dstack([col, (done * 255).astype(np.uint8)])
full = col.copy()
full[used & ~done] = fbu[used & ~done]
# grow every island outward a few texels (bilinear sampling at island edges)
m = used.copy()
d, (iy, ix) = ndi.distance_transform_edt(~m, return_indices=True)
edge = (~m) & (d <= 4)
full[edge] = full[iy[edge], ix[edge]]
holes[edge] = holes[iy[edge], ix[edge]]
Image.fromarray(holes).save(str(OUT.with_suffix("")) + "_holes.png")
Image.fromarray(full).save(OUT)
Image.fromarray(src * 60).save(str(OUT.with_suffix("")) + "_src.png")
print("wrote", OUT)
