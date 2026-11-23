"""Measure the pale rim on char_hero_bare: its colour and its depth (px at the drawing's size), by edge direction.
Rim pixels: warm pale (R > 200, G > 170, B < 170, R-B > 60) within 10 px of the outside. Depth per silhouette pixel =
distance from the outside to the first non-rim pixel along the inward normal."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np

from jcommon import RAW, REDO, load

im = np.asarray(load(RAW / "char_hero_bare.png")).astype(np.float32)
a = im[..., 3] / 255
rgb = im[..., :3]
inside = (a > 0.5).astype(np.uint8)
dist_in = cv2.distanceTransform(inside, cv2.DIST_L2, 5)  # distance to the outside, for inside pixels
R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
rimlike = (R > 200) & (G > 165) & (B < 175) & (R - B > 55) & (a > 0.3)
ab = cv2.GaussianBlur(a, (0, 0), 3)
gx, gy = cv2.Sobel(ab, cv2.CV_32F, 1, 0), cv2.Sobel(ab, cv2.CV_32F, 0, 1)
n = np.hypot(gx, gy) + 1e-6
nx, ny = gx / n, gy / n  # points inward (alpha grows inward)
edge = (inside > 0) & (dist_in <= 1.5)
ys, xs = np.nonzero(edge)
depths, facing, cols = [], [], []
for y, x in zip(ys[::2], xs[::2]):
    dx, dy = nx[y, x], ny[y, x]
    d = 0
    for s in range(0, 14):
        yy, xx = int(round(y + dy * s)), int(round(x + dx * s))
        if not (0 <= yy < a.shape[0] and 0 <= xx < a.shape[1]) or not rimlike[yy, xx]:
            break
        d = s + 1
        cols.append(rgb[yy, xx])
    depths.append(d)
    facing.append(-dy)  # +1: the inward normal points up = the edge faces down
depths, facing = np.array(depths), np.array(facing)
out = {"height_px": int(np.ptp(np.nonzero(inside.any(1))[0]) + 1)}
for name, sel in (("top (edge faces up)", facing < -0.5), ("sides", np.abs(facing) <= 0.5),
                  ("bottom (edge faces down)", facing > 0.5)):
    d = depths[sel]
    out[name] = {"n": int(sel.sum()), "share_with_rim": round(float((d > 0).mean()), 3),
                 "median_depth_where_rim": float(np.median(d[d > 0])) if (d > 0).any() else 0,
                 "p90": float(np.percentile(d[d > 0], 90)) if (d > 0).any() else 0}
c = np.array(cols)
out["rim_rgb_median"] = [int(v) for v in np.median(c, 0)]
# the outermost pixel's alpha where there is rim: how soft the outer edge is
print(json.dumps(out, indent=1))
(REDO / "rim_measure.json").write_text(json.dumps(out, indent=1))
# split by which way the edge faces, 8 sectors (0 = edge faces screen-right, 90 = up)
fx = []
for y, x in zip(ys[::2], xs[::2]):
    fx.append(-nx[y, x])
fx = np.array(fx)
ang = (np.degrees(np.arctan2(-facing, fx)) + 360) % 360  # edge facing direction, 0 right, 90 up
sect = {}
for k in range(8):
    lo = k * 45 - 22.5
    sel = ((ang - lo) % 360) < 45
    d = depths[sel]
    sect[f"{k*45:3d}"] = [int(sel.sum()), round(float((d > 0).mean()), 2), float(np.median(d[d > 0])) if (d > 0).any() else 0]
print("sector: n, share, median depth", sect)
out["sectors_facing_deg_n_share_depth"] = sect
(REDO / "rim_measure.json").write_text(json.dumps(out, indent=1))
