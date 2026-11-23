"""Round 2: ONE sword fist for the whole swing (the A3 ready fist), cleaned and recoloured to the new hero's skin.

- recolour: per channel, piecewise linear through the four skin clusters (lit, mid, shadow, line) of the A3 fist ->
  the same clusters on cutj3_s123's own near fist (r2_colors.py);
- clean edge: the alpha is cut 1.5 px inside the old soft edge (hard, anti-aliased), and the outer 3 px take the colour
  of the nearest pixel further in (no purple or cream fringe);
- wrist end: the drawn forearm is cut at the wrist point and replaced by a round skin stub (a disc about the wrist
  point), so the end under the cuff is the same shape at every wrist turn.
Angles: rig convention (y down, 0 = screen right, + clockwise). turn = blade counter-clockwise from the forearm."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import cv2
import numpy as np

from jcommon import A3, load

ELBOW, WRIST = (690, 735), (850, 745)  # ready arm on the A3 drawing (scripts/moments/grip.py ARMS)
STUB_R = 24.0      # px: the round wrist stub's radius (hidden under the cuff)
# skin clusters (lit, mid, shadow, line), RGB: A3 ready fist -> cutj3_s123 near fist
SRC_CL = np.array([[232, 160, 72], [216, 142, 55], [198, 124, 42], [158, 89, 21]], np.float32)
DST_CL = np.array([[227, 157, 85], [204, 136, 69], [183, 119, 59], [151, 94, 49]], np.float32)
SKIN_LIT = DST_CL[0] / 255
SKIN_SHADOW = DST_CL[2] / 255


def recolour(rgb):
    """rgb 0..255 float (H, W, 3) -> the new hero's skin, per channel through the cluster pairs (extrapolated)."""
    out = np.empty_like(rgb)
    for c in range(3):
        xs, ys = SRC_CL[:, c], DST_CL[:, c]
        o = np.argsort(xs)
        xs, ys = xs[o], ys[o]
        v = rgb[..., c]
        lo = ys[0] + (v - xs[0]) * (ys[1] - ys[0]) / (xs[1] - xs[0])
        hi = ys[-1] + (v - xs[-1]) * (ys[-1] - ys[-2]) / (xs[-1] - xs[-2])
        mid = np.interp(v, xs, ys)
        out[..., c] = np.where(v < xs[0], lo, np.where(v > xs[-1], hi, mid))
    return np.clip(out, 0, 255)


def load_fist():
    man = json.loads((A3 / "manifest.json").read_text())
    im = np.asarray(load(A3 / "ready" / "fist.png")).astype(np.float32)
    a = im[..., 3] / 255
    rgb = im[..., :3]
    H, W = a.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    F = math.degrees(math.atan2(WRIST[1] - ELBOW[1], WRIST[0] - ELBOW[0]))
    f = np.array([math.cos(math.radians(F)), math.sin(math.radians(F))])
    t = (xx - WRIST[0]) * f[0] + (yy - WRIST[1]) * f[1]
    # the hand past the wrist (+1 px overlap with the stub)
    solid = (a > 0.5) & (t > -1.0)
    # drop the drawn rim: warm pale pixels near the outside
    inside = solid.astype(np.uint8)
    d_in = cv2.distanceTransform(inside, cv2.DIST_L2, 5)
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    rim = (R > 215) & (G > 185) & (B < 175) & (R - B > 50) & (d_in < 7.5)
    solid &= ~rim
    # keep the biggest piece, fill pin holes
    n, lab, st, _ = cv2.connectedComponentsWithStats(solid.astype(np.uint8), 8)
    solid = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    solid = cv2.morphologyEx(solid.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)) > 0
    # round stub behind the wrist: a disc about the wrist point
    stub = (np.hypot(xx - WRIST[0], yy - WRIST[1]) < STUB_R) & (t <= 0)
    shape = solid | stub
    # a smooth outline (the rim cut and the soft drawn edge leave notches): open, then blur and threshold
    shape = cv2.morphologyEx(shape.astype(np.uint8), cv2.MORPH_OPEN,
                             cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))) > 0
    shape = cv2.GaussianBlur(shape.astype(np.float32), (0, 0), 2.2) > 0.5
    # a hard anti-aliased alpha 1.5 px inside the old edge: signed distance to the shape's outline
    d_in = cv2.distanceTransform(shape.astype(np.uint8), cv2.DIST_L2, 5)
    d_out = cv2.distanceTransform((~shape).astype(np.uint8), cv2.DIST_L2, 5)
    sd = np.where(shape, d_in, -d_out)
    alpha = np.clip(sd - 1.0 + 0.5, 0, 1)
    # colours: recoloured; the outer 3 px (and the stub) from the nearest pixel further in
    rgb2 = recolour(rgb)
    core = solid & (d_in > 4.5) & (a > 0.98)
    _, lab2 = cv2.distanceTransformWithLabels((~core).astype(np.uint8), cv2.DIST_L2, 5,
                                              labelType=cv2.DIST_LABEL_PIXEL)
    ys, xs = np.nonzero(core)
    lut = np.zeros((lab2.max() + 1, 3), np.float32)
    # label index -> the core pixel's colour (labels are numbered over the zero pixels in scan order)
    order = np.zeros(lab2.max() + 1, np.int64)
    zero_lab = lab2[core]
    lut[zero_lab] = rgb2[ys, xs]
    near_rgb = lut[lab2]
    edge = (~core) | (d_in < 3.0)
    # the edge ring: the nearest core colour, then smoothed along the ring (no radial streaks)
    cw = cv2.GaussianBlur(core.astype(np.float32), (0, 0), 2.0)
    smooth = cv2.GaussianBlur(rgb2 * core[..., None], (0, 0), 2.0) / np.maximum(cw[..., None], 1e-3)
    near_rgb = np.where((cw > 0.05)[..., None], smooth, near_rgb)
    rgb3 = np.where(edge[..., None], near_rgb, rgb2)
    # the stub: flat lit skin with its shadow half toward the palm side (below the forearm line)
    v = -(xx - WRIST[0]) * f[1] + (yy - WRIST[1]) * f[0]
    stub_only = stub & ~solid
    rgb3[stub_only] = np.where((v[stub_only] > 8)[:, None], DST_CL[2], DST_CL[0])
    px = np.concatenate([rgb3 / 255 * alpha[..., None], alpha[..., None]], -1).astype(np.float32)
    ysa, xsa = np.nonzero(alpha > 0.002)
    x0, y0, x1, y1 = xsa.min() - 2, ysa.min() - 2, xsa.max() + 3, ysa.max() + 3
    wd = man["moments"]["ready"]["weapon"]
    blade = wd["angle"] - 90.0
    turn = ((-blade - (-F)) + 180) % 360 - 180
    return {"img": px[y0:y1, x0:x1].copy(), "origin": (int(x0), int(y0)), "wrist": WRIST, "F": F, "blade": blade,
            "grip": tuple(wd["grip_xy"]), "turn_ccw": turn}


if __name__ == "__main__":
    d = load_fist()
    print({k: (round(v, 1) if isinstance(v, float) else v) for k, v in d.items() if k != "img"}, d["img"].shape)
