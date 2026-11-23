"""Place check of an armor edit against its bare moment: bring the edit into place (one shift + scale for the whole
picture, by ECC on the silhouette), then compare the parts the piece must not move, zone by zone (overlap IoU, centre
shift in px). Colour zones are found on each picture by its own colours, so a limb that moved on its own shows up.

<venv python> -I -B place.py <bare try> <edit try> [<edit try> ...]
"""
import sys
from pathlib import Path

import cv2
import numpy as np
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, REDO, load_rgba  # noqa: E402
import cut  # noqa: E402


def classes(a: np.ndarray) -> dict:
    r, g, b = (a[..., i].astype(float) for i in range(3))
    fa = a[..., 3] > 128
    r1 = r + 1
    skin = cut.skin_mask(a) & fa
    teal = fa & (g > r + 4) & (b > r - 2) & (r < 90) & ~cut.green_mask(a)
    cream = fa & (r > 170) & (g > 150) & (b > 100) & (r - b < 95) & ~skin
    red = fa & (g / r1 < 0.42) & (r > 90) & ~ndi.binary_dilation(cut.magenta_mask(a), iterations=3)  # not the marker
    brown = fa & (r >= g) & (g >= b - 6) & (r < 150) & (r > 35) & ~skin & ~red
    dark = fa & (r < 70) & (g < 60) & (b < 60)
    return {"fig": fa, "skin": skin, "teal": teal, "cream": cream, "red": red, "brown": brown, "dark": dark}


def red_blobs(c: dict) -> list:
    """Red blobs (headband, its tails, an open mouth) with what rings them within 16 px: dark (hair), skin (face)."""
    lab, n = ndi.label(c["red"])
    out = []
    for i in range(1, n + 1):
        b = lab == i
        if b.sum() < 150:
            continue
        ring = ndi.binary_dilation(b, iterations=16) & ~b
        k = max(1, ring.sum())
        ys, xs = np.nonzero(b)
        out.append({"mask": b, "x": xs.mean(), "y": ys.mean(), "dark": float((ring & c["dark"]).sum() / k),
                    "skin": float((ring & c["skin"]).sum() / k)})
    return out


def head_box(c: dict) -> np.ndarray:
    """Head zone: around the red headband, down to the chin. The band is the red blob with the largest area x share of
    dark hair in a wide ring round it (the tails fly in the air and can be bigger, e.g. at the strike; an open mouth or a
    sliver is red too, but small and only thinly outlined); band pieces next to it count too (hair can split it)."""
    blobs = red_blobs(c)
    if not blobs:
        return np.zeros_like(c["red"])
    best = max(blobs, key=lambda t: t["dark"] * t["mask"].sum())  # both cues: a big band, deep in the hair
    band = np.zeros_like(c["red"])
    for t in blobs:
        if t["dark"] > 0.15 and np.hypot(t["x"] - best["x"], t["y"] - best["y"]) < 0.08 * HERO_H:
            band |= t["mask"]
    ys, xs = np.nonzero(band)
    cx, cy = xs.mean(), ys.mean()
    H, W = c["red"].shape
    YY, XX = np.mgrid[0:H, 0:W]
    R = 0.11 * HERO_H
    return (np.abs(XX - cx) < R) & (YY > cy - R * 0.9) & (YY < cy + R * 1.25)


def zones(a: np.ndarray) -> dict:
    c = classes(a)
    head = head_box(c)
    ys = np.nonzero(c["fig"])[0]
    top, bot = ys.min(), ys.max()
    low = np.zeros_like(head)
    low[int(bot - 0.27 * (bot - top)):] = True
    return {
        "head (face, hair, band)": head & c["fig"],
        "skin off the head (arms, fists)": c["skin"] & ~head,
        "shirt": c["cream"] & ~head,
        "trousers": c["teal"],
        "boots": c["brown"] & low,
        "silhouette": c["fig"],
    }


def iou(a, b):
    return float((a & b).sum() / max(1, (a | b).sum()))


def gray(x: np.ndarray) -> np.ndarray:
    """Luminance over mid grey (so the outline and the cel shading drive the match, not only the silhouette)."""
    f = x.astype(np.float32) / 255
    al = f[..., 3:]
    rgb = f[..., :3] * al + 0.5 * (1 - al)
    return (rgb @ np.array([0.299, 0.587, 0.114], np.float32)).astype(np.float32)


def zone_shifts(bare: np.ndarray, edit: np.ndarray) -> dict:
    """Per bare zone: (dx, dy) of the edit's drawing against the bare one (phase correlation of the luminance)."""
    from scipy import ndimage as ndi_
    ga, gb = gray(bare), gray(edit)
    out = {}
    for k, m in bare_zones(bare).items():
        if not m.any():
            continue
        ys, xs = np.nonzero(ndi_.binary_dilation(m, iterations=12))
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        win = cv2.createHanningWindow((x1 - x0, y1 - y0), cv2.CV_32F)
        (dx, dy), _ = cv2.phaseCorrelate(ga[y0:y1, x0:x1], gb[y0:y1, x0:x1], win)
        out[k] = (float(dx), float(dy), (y0, y1, x0, x1))
    return out


def align(edit: np.ndarray, bare: np.ndarray):
    """-> (edit moved into place, {'dx','dy','scale'}): one shift for the whole picture, the median of the zones'
    shifts (the zone the piece changes cannot drag it). Qwen kept the scale in every edit seen (zones agree), so the
    scale stays 1."""
    sh = zone_shifts(bare, edit)
    dx = float(np.median([v[0] for v in sh.values()]))
    dy = float(np.median([v[1] for v in sh.values()]))
    if abs(dx) < 0.5 and abs(dy) < 0.5:
        return edit.copy(), {"dx": 0.0, "dy": 0.0, "scale": 1.0}
    w = np.array([[1, 0, -dx], [0, 1, -dy]], np.float32)
    pre = edit.astype(np.float32)
    pre[..., :3] *= pre[..., 3:] / 255
    out = cv2.warpAffine(pre, w, (edit.shape[1], edit.shape[0]), flags=cv2.INTER_LINEAR)
    al = out[..., 3:]
    out[..., :3] = np.where(al > 0, out[..., :3] * 255 / np.maximum(al, 1e-3), 0)
    return np.clip(out + 0.5, 0, 255).astype(np.uint8), {"dx": dx, "dy": dy, "scale": 1.0}


def bare_zones(bare: np.ndarray) -> dict:
    """Zones found once on the bare drawing (so a recoloured piece cannot fool them)."""
    z = zones(bare)
    return {k: v for k, v in z.items() if k != "silhouette"} | {"whole figure": z["silhouette"]}


def check(bare: np.ndarray, edit_aligned: np.ndarray) -> dict:
    """Per bare zone: overlap of the two silhouettes inside the zone's box, and how far the zone's drawing moved (px)."""
    res = {}
    for k, (dx, dy, (y0, y1, x0, x1)) in zone_shifts(bare, edit_aligned).items():
        A, B = bare[y0:y1, x0:x1, 3] > 128, edit_aligned[y0:y1, x0:x1, 3] > 128
        res[k] = (round(iou(A, B), 3), round(float(np.hypot(dx, dy)), 1))
    return res


if __name__ == "__main__":
    bare = load_rgba(REDO / f"{sys.argv[1]}.png")
    for n in sys.argv[2:]:
        e, t = align(load_rgba(REDO / f"{n}.png"), bare)
        print(n, {k: round(v, 3) for k, v in t.items()})
        for k, v in check(bare, e).items():
            print(f"   {k:34s} overlap {v[0]:.3f}  moved {v[1]:5.1f} px")
