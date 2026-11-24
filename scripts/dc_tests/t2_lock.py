"""Dragon Crashers test 2, step 2: lock each Qwen repaint to the knight torso's own outline, and measure how far Qwen strayed.
<venv python> -I -B t2_lock.py [candidate ...]   (candidates: names in art/tests/dc/t2/qwen without .png; default all)
For each candidate, on the 4x canvas (no alignment, raw numbers):
  IoU    = Qwen's silhouette (pixels not the green backdrop) against the original torso alpha (> 50%), upscaled 4x
  leak   = share of the original's opaque pixels where Qwen left backdrop green (inside the outline)
  spill  = Qwen's non-green pixels outside the original outline, as a share of the original's opaque area
Then the lock: every green pixel (leaks and the backdrop) takes the colour of the nearest Qwen paint, so no green can be
sampled at the edges; downscaled 4x by area to 153x298; alpha = the original alpha, untouched.
Writes art/tests/dc/t2/locked/<candidate>.png and art/tests/dc/t2/locked/metrics.json."""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
from t2_qwen import CANVAS, OFFSET, SCALE  # noqa: E402

T2 = ROOT / "art/tests/dc/t2"
QW = T2 / "qwen"
OUT = T2 / "locked"


def is_green(rgb: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (g - np.maximum(r, b) > 12) & (g > 1.25 * r) & (g > 1.12 * b)


def lock(name: str) -> dict:
    orig = np.asarray(Image.open(T2 / "torso_orig.png").convert("RGBA"))
    h, w = orig.shape[:2]
    q = np.asarray(Image.open(QW / f"{name}.png").convert("RGB")).astype(np.int32)
    assert q.shape[1] == CANVAS[0] and q.shape[0] == CANVAS[1], q.shape
    x0, y0 = OFFSET
    region = q[y0:y0 + h * SCALE, x0:x0 + w * SCALE]
    a4 = np.asarray(Image.fromarray(orig[..., 3]).resize((w * SCALE, h * SCALE), Image.BICUBIC)) > 127
    paint = ~is_green(region)
    # Qwen's silhouette on the whole canvas, to also catch paint far outside the region
    paint_all = ~is_green(q)
    inside_canvas = np.zeros(paint_all.shape, bool)
    inside_canvas[y0:y0 + h * SCALE, x0:x0 + w * SCALE] = a4
    inter = (paint_all & inside_canvas).sum()
    union = (paint_all | inside_canvas).sum()
    m = {
        "iou": round(float(inter / union), 4),
        "leak_inside": round(float((~paint & a4).sum() / a4.sum()), 4),
        "spill_outside": round(float((paint_all & ~inside_canvas).sum() / a4.sum()), 4),
    }
    # lock: nearest Qwen paint into every green pixel, then area-downscale, alpha = original
    _, (iy, ix) = ndimage.distance_transform_edt(~paint, return_indices=True)
    filled = region[iy, ix].astype(np.uint8)
    small = np.asarray(Image.fromarray(filled).resize((w, h), Image.BOX))
    out = np.dstack([small, orig[..., 3]])
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, "RGBA").save(OUT / f"{name}.png")
    return m


if __name__ == "__main__":
    names = sys.argv[1:] or sorted(p.stem for p in QW.glob("*_v*_s*.png"))
    path = OUT / "metrics.json"
    allm = json.loads(path.read_text()) if path.exists() else {}
    for n in names:
        allm[n] = lock(n)
        print(n, allm[n])
    path.write_text(json.dumps(allm, indent=1))
