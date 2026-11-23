"""Cutting and stacking for the hero in moments (no rig: every picture is a whole drawing, code only cuts layers).

- read_rod: the green rod in the sword fist -> fist centre, blade direction (the declared thumb side), angle.
- remove_rod: the rod's pixels taken out (transparent where the ground was behind it, filled from the drawing where his
  body was behind it).
- weapon_layer: the weapon picture placed with its grip point on the fist centre at the rod's angle.
- fist_mask: the sword fist (skin and its outline around the rod gap), drawn above the weapon.
"""
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, RAW  # noqa: E402

SWORD = {"art": "wpn_training_sword", "grip": 0.79, "size": 0.6}
# The game draws a weapon `size` x the bare art's canvas height; that canvas is 1536 px for a hero 1032 px tall.
BASE_CANVAS_PER_HERO = 1536 / 1032


def green_mask(a: np.ndarray) -> np.ndarray:
    r, g, b = (a[..., i].astype(int) for i in range(3))
    return (a[..., 3] > 60) & (g > 60) & (g > r + 35) & (g > b + 35)


def magenta_mask(a: np.ndarray) -> np.ndarray:
    """The marker's pommel stub (pure bright magenta): nothing on the hero is this colour."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    return (a[..., 3] > 60) & (r > 120) & (b > 100) & (r > g + 70) & (b > g + 50)


def marker_mask(a: np.ndarray) -> np.ndarray:
    return green_mask(a) | magenta_mask(a)


def _blobs(m: np.ndarray, min_px: int) -> np.ndarray:
    lab, n = ndi.label(m)
    if not n:
        return m
    sizes = ndi.sum(m, lab, range(1, n + 1))
    return np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > min_px])


def read_marker(a: np.ndarray) -> dict:
    """The two-colour marker (green blade part, magenta pommel stub) -> the same dict as read_rod, with the blade end
    read from the picture: d points from the fist toward the green part. Raises ValueError when the picture does not
    say it clearly (a colour missing, the stub longer than the blade part, the two parts not on one line)."""
    gm = _blobs(green_mask(a), 150)
    mm = _blobs(magenta_mask(a), 40)
    ng, nm = int(gm.sum()), int(mm.sum())
    if ng < 300 or nm < 60:
        raise ValueError(f"marker unclear: green {ng} px, magenta {nm} px")
    pg = np.stack(np.nonzero(gm)[::-1], 1).astype(float)
    pm = np.stack(np.nonzero(mm)[::-1], 1).astype(float)
    p = np.concatenate([pg, pm])
    mu = p.mean(0)
    w, v = np.linalg.eigh(np.cov((p - mu).T))
    d = v[:, np.argmax(w)]
    if (pg.mean(0) - pm.mean(0)) @ d < 0:
        d = -d  # d: from the magenta stub toward the green blade part
    perp = np.array([-d[1], d[0]])
    tg, tm = (pg - mu) @ d, (pm - mu) @ d
    width = float(np.percentile(np.abs((p - mu) @ perp), 95) * 2)
    # the two parts on one line: the magenta stub's centre lies on the green part's axis
    wg, vg = np.linalg.eigh(np.cov((pg - pg.mean(0)).T))
    dg = vg[:, np.argmax(wg)]
    off = abs((pm.mean(0) - pg.mean(0)) @ np.array([-dg[1], dg[0]]))
    len_g, len_m = float(tg.max() - tg.min()), float(tm.max() - tm.min())
    # each part one thin straight stick: its spread across its own axis stays a stick's width (a cross or a second
    # stick spreads it); the green part as thin as the magenta stub
    wid = lambda q, dv: float(np.percentile(np.abs((q - q.mean(0)) @ np.array([-dv[1], dv[0]])), 95) * 2)  # noqa
    wm_, vm_ = np.linalg.eigh(np.cov((pm - pm.mean(0)).T))
    w_g, w_m = wid(pg, dg), wid(pm, vm_[:, np.argmax(wm_)])
    if w_g > 1.6 * w_m + 8 or width > 1.6 * w_m + 8:
        raise ValueError(f"marker is not one straight stick (spread: green {w_g:.0f}, both {width:.0f}, stub "
                         f"{w_m:.0f} px)")
    if off > 2.5 * width:
        raise ValueError(f"marker parts not on one line (stub {off:.0f} px off the blade axis, width {width:.0f})")
    if len_m >= len_g:
        raise ValueError(f"magenta stub ({len_m:.0f} px) not shorter than the green part ({len_g:.0f} px)")
    t1, t2 = float(tm.max()), float(tg.min())  # inner ends: the fist sits between them
    if t2 < t1:  # the colours overlap along the axis (no fist between): unclear
        raise ValueError("marker colours overlap: no fist between the stub and the blade part")
    c = mu + d * (t1 + t2) / 2
    angle = math.degrees(math.atan2(d[0], -d[1]))
    return {"center": c, "d": d, "angle": angle, "gap": t2 - t1, "width": width, "segments": 2,
            "len_blade_side": float(tg.max() - t2), "len_pommel_side": float(t1 - tm.min()), "mask": gm | mm,
            "source": "marker", "stub_off": float(off)}


LOPSIDED = 2.0  # the long end at least this many times the stub


def read_long_end(a: np.ndarray) -> dict:
    """The one-colour lopsided stick (marker v3): blade = the long end, read from the picture. Raises ValueError when
    the stick is not one thin straight stick with both ends showing, or not clearly lopsided."""
    r = read_rod(a, (1, 0))
    if r["segments"] != 2:
        raise ValueError("stick: only one end shows, so the fist's place on it is unknown")
    if r["width"] > 45:
        raise ValueError(f"stick: not one thin straight stick (spread {r['width']:.0f} px)")
    if r["len_pommel_side"] > r["len_blade_side"]:
        r = read_rod(a, (-1, 0))
    lb, lp = r["len_blade_side"], r["len_pommel_side"]
    if lp < 12 or lb < LOPSIDED * lp:
        raise ValueError(f"stick: not clearly lopsided (long {lb:.0f} px, stub {lp:.0f} px)")
    r["source"] = "long end"
    return r


def read_rod(a: np.ndarray, thumb: tuple) -> dict:
    gm = green_mask(a)
    lab, n = ndi.label(gm)
    sizes = ndi.sum(gm, lab, range(1, n + 1))
    keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 150])
    ys, xs = np.nonzero(keep)
    if len(xs) < 300:
        raise ValueError(f"no rod found ({len(xs)} green px)")
    p = np.stack([xs, ys], 1).astype(float)
    mu = p.mean(0)
    w, v = np.linalg.eigh(np.cov((p - mu).T))
    d = v[:, np.argmax(w)]
    if d @ np.array(thumb, float) < 0:
        d = -d
    t = (p - mu) @ d
    perp = (p - mu) @ np.array([-d[1], d[0]])
    ts = np.sort(t)
    gaps = np.diff(ts)
    i = int(np.argmax(gaps))
    width = float(np.percentile(np.abs(perp), 95) * 2)
    if gaps[i] < width:  # one visible segment only: the fist sits at its end nearer the pommel side
        t1 = t2 = ts[0]
        segs = 1
    else:
        t1, t2 = ts[i], ts[i + 1]
        segs = 2
    c = mu + d * (t1 + t2) / 2
    angle = math.degrees(math.atan2(d[0], -d[1]))  # 0 = blade up, clockwise positive (Phaser/Unity-style)
    return {"center": c, "d": d, "angle": angle, "gap": float(t2 - t1), "width": width, "segments": segs,
            "len_blade_side": float(ts[-1] - t2), "len_pommel_side": float(t1 - ts[0]), "mask": keep}


def remove_rod(a: np.ndarray, rod_mask: np.ndarray, grow: int = 3) -> np.ndarray:
    """Rod out: alpha inpainted (ground behind -> 0), colour inpainted from the drawing around it."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    # green- or magenta-tinted edge pixels next to the marker
    tint = (a[..., 3] > 0) & (g > r + 12) & (g > b + 12)
    pink = (a[..., 3] > 0) & (b > g + 12) & (r > g + 12)  # magenta blended into an edge (skin and cloth: b < g)
    m = ndi.binary_dilation(rod_mask, iterations=grow) | (tint & ndi.binary_dilation(rod_mask, iterations=10)) | (
        pink & ndi.binary_dilation(rod_mask, iterations=24))
    m = ndi.binary_dilation(m, iterations=1)
    out = a.copy()
    # colour of the nearest solid pixel everywhere, so inpainting never pulls in the black ground
    solid = (a[..., 3] > 200) & ~m
    _, (iy, ix) = ndi.distance_transform_edt(~solid, return_indices=True)
    rgb = a[..., :3][iy, ix]
    mk = m.astype(np.uint8) * 255
    rgb = cv2.inpaint(np.ascontiguousarray(rgb), mk, 5, cv2.INPAINT_TELEA)
    al = cv2.inpaint(np.ascontiguousarray(a[..., 3]), mk, 5, cv2.INPAINT_TELEA)
    al = np.where(al < 150, 0, np.where(al > 230, 255, al))
    out[m, :3] = rgb[m]
    out[m, 3] = al[m]
    return out


def weapon_layer(shape, rod: dict, hero_h: float = HERO_H, weapon: dict = SWORD) -> np.ndarray:
    art = np.array(Image.open(RAW / f"{weapon['art']}.png").convert("RGBA")).astype(np.float32)
    art[..., 3] = np.where(art[..., 3] < 10, 0, art[..., 3])
    s = weapon["size"] * hero_h * BASE_CANVAS_PER_HERO / art.shape[0]
    pre = art.copy()
    pre[..., :3] *= pre[..., 3:] / 255.0
    small = cv2.resize(pre, (round(art.shape[1] * s), round(art.shape[0] * s)), interpolation=cv2.INTER_AREA)
    gx, gy = small.shape[1] / 2, weapon["grip"] * small.shape[0]
    th = math.radians(rod["angle"])
    cs, sn = math.cos(th), math.sin(th)
    cx, cy = rod["center"]
    M = np.array([[cs, -sn, cx - (cs * gx - sn * gy)], [sn, cs, cy - (sn * gx + cs * gy)]], np.float32)
    out = cv2.warpAffine(small, M, (shape[1], shape[0]), flags=cv2.INTER_CUBIC, borderValue=0)
    out = np.clip(out, 0, 255)
    al = out[..., 3:]
    out[..., :3] = np.where(al > 0, out[..., :3] * 255.0 / np.maximum(al, 1e-3), 0)
    return np.clip(out, 0, 255).astype(np.uint8)


def skin_mask(a: np.ndarray) -> np.ndarray:
    r, g, b = (a[..., i].astype(float) for i in range(3))
    r1 = r + 1
    return (a[..., 3] > 100) & (g / r1 < 0.80) & (b / r1 < 0.53) & (r > 150) & (r - b > 70)


def fist_mask(a: np.ndarray, rod: dict, hand: np.ndarray | None = None) -> np.ndarray:
    """The sword fist: the hand-coloured blob that sits on the rod gap (plus its dark outline), cut to a disc around
    the gap so the forearm is cut off at the wrist. `hand` = colour mask of the hand (skin by default, a glove's colours
    for a gloved edit)."""
    hand = skin_mask(a) if hand is None else hand
    H, W = hand.shape
    cx, cy = rod["center"]
    rad = max(rod["gap"], rod["width"] * 3) * 0.85
    YY, XX = np.mgrid[0:H, 0:W]
    disc = (XX - cx) ** 2 + (YY - cy) ** 2 < rad ** 2
    lab, _ = ndi.label(hand & disc)
    near = np.unique(lab[(XX - cx) ** 2 + (YY - cy) ** 2 < (rod["width"] * 1.5) ** 2])
    near = near[near > 0]
    if not len(near):
        return np.zeros_like(hand)
    m = np.isin(lab, near)
    m = ndi.binary_closing(m, iterations=2)
    # the drawn outline round the fist, but never the shirt or trousers behind it
    r, g, b = (a[..., i].astype(int) for i in range(3))
    line = (r + g + b < 300) & (a[..., 3] > 0) & ~(g > r)  # dark outline (not the teal trousers)
    m = ((ndi.binary_dilation(m, iterations=6) & line) | m) & disc
    return m


def pale_rim(a: np.ndarray) -> np.ndarray:
    """The drawing's warm pale rim light (and the lit skin next to it): bright, warm, not grey."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    return (a[..., 3] > 0) & (r > 200) & (g > 165) & (r - b > 25) & (r >= g)


def hand_outline(a: np.ndarray) -> np.ndarray:
    """The hand's own dark line: warm dark brown (never the teal trousers, never grey steel)."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    return (a[..., 3] > 0) & (r + g + b < 360) & (r > g) & (g >= b - 5) & (r - b > 20)


def own_arm(skin: np.ndarray, rod: dict, forearm: float | None) -> np.ndarray:
    """The skin of the sword arm only. Skin of another limb can touch the sword fist (ready: his far forearm under it);
    the touching skin is split at its narrowest neck by a watershed on the skin's thickness, seeded by the fist core
    and by the skin far from the fist that does not lie along the sword forearm (forearm = F in screen degrees, 0 =
    right, 90 = up; elbow -> wrist)."""
    H, W = skin.shape
    cx, cy = rod["center"]
    YY, XX = np.mgrid[0:H, 0:W]
    r = np.hypot(XX - cx, YY - cy)
    rad = max(rod["gap"], rod["width"] * 3)
    lab, _ = ndi.label(skin)
    core = r < rod["width"] * 1.5
    ids = set(np.unique(lab[core & skin])) - {0}
    blob = np.isin(lab, list(ids))
    if forearm is None:
        return blob
    back = math.radians(forearm + 180)  # from the fist toward the elbow
    bx, by = math.cos(back), -math.sin(back)
    cosang = ((XX - cx) * bx + (YY - cy) * by) / np.maximum(r, 1e-6)
    other = blob & (r > 1.25 * rad) & (cosang < math.cos(math.radians(55)))
    if not other.any():
        return blob
    # erode until the fist core and the far skin fall apart, then grow both back inside the skin at the same pace
    for k in range(1, 16):
        er = ndi.binary_erosion(blob, iterations=k)
        lab_e, _ = ndi.label(er)
        a_ids = set(np.unique(lab_e[core & er])) - {0}
        b_ids = set(np.unique(lab_e[other & er])) - {0}
        if a_ids and not (a_ids & b_ids):
            break
    else:
        return blob
    A = np.isin(lab_e, list(a_ids))
    B = er & ~A
    for _ in range(k + 4):
        A2 = ndi.binary_dilation(A) & blob & ~B
        B = ndi.binary_dilation(B) & blob & ~A
        A = A2
    return A


def fist_copy(clean: np.ndarray, rod: dict, weapon: np.ndarray, forearm: float | None = None) -> tuple:
    """The sword hand copied from the clean drawing, to lie over the weapon -> (RGBA layer, mask).

    By positive colour only: the skin of the sword hand (skin blobs in the fist disc that touch the fist centre), closed
    and hole-filled, plus the hand's own rim and dark line within 2 px of that skin, plus the drawing's soft outer
    edge (pixels of the outer contour, alpha < 250, within 3 px). Where the weapon would pass under the forearm beyond
    the disc, the forearm skin there comes with it (so the pommel goes under the wrist, not over it). The copy ends
    at the disc across the wrist; under it the body sprite holds the very same pixels, so that end leaves no seam.
    Its edge: the drawing's own alpha on the outer contour, a 1 px feather inside the figure."""
    H, W = clean.shape[:2]
    cx, cy = rod["center"]
    rad = max(rod["gap"], rod["width"] * 3) * 0.95
    YY, XX = np.mgrid[0:H, 0:W]
    r2 = (XX - cx) ** 2 + (YY - cy) ** 2
    skin = own_arm(skin_mask(clean), rod, forearm)
    lab, _ = ndi.label(skin & (r2 < rad ** 2))
    ids = set(np.unique(lab[r2 < (rod["width"] * 1.5) ** 2])) - {0}
    hand = np.isin(lab, list(ids))
    # thin skin-coloured strips touching the hand (a boot's bright rim) are not the hand
    hand &= ndi.binary_dilation(ndi.binary_opening(hand, iterations=2), iterations=3)
    # the forearm under which the weapon passes (beyond the disc): skin of the same arm that the weapon covers
    lab2, _ = ndi.label(skin)
    arm = np.isin(lab2, list(set(np.unique(lab2[hand])) - {0}))
    wpn = weapon[..., 3] > 30
    under = arm & ndi.binary_dilation(wpn, iterations=10) & (r2 < (2.2 * rad) ** 2)
    if under.any():
        hand |= arm & ndi.binary_dilation(under, iterations=8)
    hand = ndi.binary_fill_holes(ndi.binary_closing(hand, iterations=2))
    al = clean[..., 3]
    edge = ndi.binary_dilation(hand, iterations=2) & (pale_rim(clean) | hand_outline(clean))
    outer = ndi.binary_dilation(hand, iterations=3) & (al > 0) & (al < 250)
    # the rim and the soft edge only as a band along the hand: thin spurs of other parts' rims (the boot's) drop out
    band = (edge | outer) & ndi.binary_opening(hand | edge | outer, structure=np.ones((3, 3), bool))
    m = ndi.binary_fill_holes(hand | band)
    # cool pixels at its edge are the shirt or trousers beside the hand (a teal speck over the weapon): never the
    # hand, its warm line or its pale rim, all redder than green; the body sprite under the copy keeps them
    cr, cg = clean[..., 0].astype(int), clean[..., 1].astype(int)
    m &= ~(~hand & (cg >= cr - 5))
    # soft edge: the drawing's own alpha where the hand meets the empty ground, a 1 px feather inside the figure
    rim = m & ~ndi.binary_erosion(m, iterations=1)  # the copy's last pixel row
    f = np.where(m, 1.0, 0.0).astype(np.float32)
    f[rim & (al >= 250)] = 0.5  # inside the figure: half, so the edge over the weapon is not a staircase
    out = clean.copy()
    out[..., 3] = np.clip(al.astype(np.float32) * f + 0.5, 0, 255).astype(np.uint8)
    out[out[..., 3] == 0, :3] = 0
    return out, m


def over(dst: np.ndarray, src: np.ndarray) -> np.ndarray:
    """src over dst, both RGBA uint8."""
    d = dst.astype(np.float32) / 255
    s = src.astype(np.float32) / 255
    sa, da = s[..., 3:], d[..., 3:]
    oa = sa + da * (1 - sa)
    oc = (s[..., :3] * sa + d[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
    return (np.concatenate([oc, oa], -1) * 255 + 0.5).astype(np.uint8)


def masked(a: np.ndarray, m: np.ndarray) -> np.ndarray:
    out = a.copy()
    out[..., 3] = np.where(m, out[..., 3], 0)
    return out
