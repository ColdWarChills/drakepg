"""The three clean sword fists from layers_A3, as rigid pictures turning about the wrist.

Per fist: the A3 fist copy, its drawn rim taken off (warm pale pixels within 7 px of its outside), cut on a line across
the forearm a little before the wrist (the cuff covers that end), with the wrist point, the drawn forearm angle F and
the blade's screen angle. Angles: y down, 0 = screen right, + clockwise (the rig's convention). turn_ccw = the brief's
grip turn (T - F counter-clockwise, y up)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import cv2
import numpy as np

from jcommon import A3, load

# elbow and wrist of the sword arm on the A3 drawings (scripts/moments/grip.py ARMS; same canvas as layers_A3)
ARMS = {"ready": ((690, 735), (850, 745)), "wind": ((528, 590), (522, 455)), "strike": ((905, 915), (985, 995))}
CUT_BACK = 12.0  # px (A3 scale) behind the wrist where the fist picture ends; the cuff covers it


def strip_rim(px: np.ndarray) -> np.ndarray:
    """px: float premultiplied RGBA 0..1. Warm pale rim pixels within 7 px of the outside -> transparent."""
    a = px[..., 3]
    rgb = np.where(a[..., None] > 1e-3, px[..., :3] / np.maximum(a[..., None], 1e-3), 0) * 255
    inside = (a > 0.5).astype(np.uint8)
    dist = cv2.distanceTransform(inside, cv2.DIST_L2, 5)
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    rim = (R > 215) & (G > 185) & (B < 175) & (R - B > 50) & (dist < 7.5) & (a > 0.02)
    # grow into the soft outer edge pixels that touch a rim pixel
    rim = rim | ((cv2.dilate(rim.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0) & (a < 0.6))
    out = px.copy()
    out[rim] = 0
    # a soft new edge: 1 px feather where the rim was taken off
    keep = (out[..., 3] > 0.5).astype(np.uint8)
    d2 = cv2.distanceTransform(keep, cv2.DIST_L2, 3)
    edge = (keep > 0) & (d2 < 1.5) & (cv2.dilate(rim.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0)
    out[edge] *= 0.6
    return out


def load_fists():
    man = json.loads((A3 / "manifest.json").read_text())
    out = {}
    for m, (e, w) in ARMS.items():
        im = np.asarray(load(A3 / m / "fist.png")).astype(np.float32) / 255
        im[..., :3] *= im[..., 3:4]
        im = strip_rim(im)
        F = math.degrees(math.atan2(w[1] - e[1], w[0] - e[0]))
        f = np.array([math.cos(math.radians(F)), math.sin(math.radians(F))])
        H, W = im.shape[:2]
        yy, xx = np.mgrid[0:H, 0:W]
        t = (xx - w[0]) * f[0] + (yy - w[1]) * f[1]
        im[t < -CUT_BACK] = 0
        soft = np.clip((t + CUT_BACK) / 1.5, 0, 1)[..., None]
        im *= soft
        wd = man["moments"][m]["weapon"]
        blade = wd["angle"] - 90.0  # manifest: 0 = blade up, clockwise -> rig: 0 = right, clockwise
        turn_ccw = ((-(blade) - (-F)) + 180) % 360 - 180
        ys, xs = np.nonzero(im[..., 3] > 0.02)
        x0, y0, x1, y1 = xs.min() - 2, ys.min() - 2, xs.max() + 3, ys.max() + 3
        out[m] = {"img": im[y0:y1, x0:x1].copy(), "origin": (x0, y0), "wrist": w, "F": F, "blade": blade,
                  "grip": tuple(wd["grip_xy"]), "turn_ccw": turn_ccw, "weapon_scale": wd["scale_px_per_art_px"]}
    return out


if __name__ == "__main__":
    for m, d in load_fists().items():
        print(m, {k: (round(v, 1) if isinstance(v, float) else v) for k, v in d.items() if k != "img"},
              d["img"].shape)
