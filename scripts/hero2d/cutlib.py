"""Cutting helpers for the cutout hero: key the purple field, find the drawn line, cut a piece by a zone polygon and a
colour filter, and close a joint end by extruding the last cross-section into a round cap."""
from __future__ import annotations

import sys

import cv2
import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
PURPLE = np.array([163, 55, 211], np.float32)


def key_purple(rgb: np.ndarray, soft=(40, 90)) -> np.ndarray:
    """Alpha from the distance to the purple field (soft edge), largest blob kept, holes filled."""
    d = np.abs(rgb.astype(np.float32) - PURPLE).max(-1)
    a = np.clip((d - soft[0]) / (soft[1] - soft[0]), 0, 1)
    m = (a > 0.5).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    keep = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    big = (lab == keep).astype(np.uint8)
    # fill holes
    ff = big.copy()
    h, w = ff.shape
    mask = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(ff, mask, (0, 0), 1)
    holes = (ff == 0)
    big = np.maximum(big, holes.astype(np.uint8))
    grown = cv2.dilate(big, np.ones((5, 5), np.uint8))
    return a * grown


def unmix_purple(rgb: np.ndarray, a: np.ndarray) -> np.ndarray:
    """Remove the purple that the soft edge mixed in: c = (obs - (1-a) * purple) / a."""
    out = rgb.astype(np.float32).copy()
    aa = np.clip(a, 1e-3, 1)[..., None]
    out = (out - (1 - aa) * PURPLE) / aa
    return np.clip(out, 0, 255)


def lum(rgb):
    return (0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]) / 255


def poly_mask(shape, pts) -> np.ndarray:
    m = np.zeros(shape[:2], np.uint8)
    cv2.fillPoly(m, [np.round(np.asarray(pts)).astype(np.int32)], 1)
    return m.astype(bool)


def extrude_cap(rgba: np.ndarray, base: tuple, pivot: tuple, r: float, inset: float = 3.0,
                line_ring: float = 0.0, line_rgb=(42, 26, 18)) -> np.ndarray:
    """Close a joint end. `base` is the middle of the piece's cut end (where its real pixels stop), `pivot` the joint
    beyond it; the stadium from base to pivot plus a disc of radius r at the pivot is filled UNDER the piece with the
    piece's cross-section taken `inset` px inside the base (so its shading bands and side lines run on).
    line_ring > 0 draws that many px of line along the cap's outer arc (a seam)."""
    h, w = rgba.shape[:2]
    base = np.asarray(base, float)
    pivot = np.asarray(pivot, float)
    u = pivot - base
    L = float(np.hypot(*u))
    if L < 1e-3:
        # pivot at the base: axis from the piece's centre of mass toward the base
        ys, xs = np.nonzero(rgba[..., 3] > 0.5)
        u = base - np.array([xs.mean(), ys.mean()])
        L = 0.0
    u = u / max(np.hypot(*u), 1e-6)
    v = np.array([-u[1], u[0]])
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    px, py = xx - base[0], yy - base[1]
    along = px * u[0] + py * u[1]
    lat = px * v[0] + py * v[1]
    stadium = (along >= -0.5) & (along <= L) & (np.abs(lat) <= r)
    dpiv = np.hypot(xx - pivot[0], yy - pivot[1])
    region = (stadium | (dpiv <= r)) & (along >= -0.5)
    # sample the cross-section
    sx = base[0] - u[0] * inset + lat * v[0]
    sy = base[1] - u[1] * inset + lat * v[1]
    samp = cv2.remap(rgba.astype(np.float32), sx.astype(np.float32), sy.astype(np.float32), cv2.INTER_LINEAR,
                     borderMode=cv2.BORDER_CONSTANT)
    # where the cross-section is empty (beyond the limb's width) the cap stays empty, except inside the disc where the
    # nearest lateral sample is used
    cap = np.zeros_like(rgba, np.float32)
    cap[region] = samp[region]
    cap[..., 3] = np.where(region, np.maximum(cap[..., 3], 0), 0)
    # soft round edge on the disc end
    edge = np.clip(r - dpiv + 0.5, 0, 1)
    beyond = along > L
    cap[..., 3] = np.where(beyond, cap[..., 3] * edge, cap[..., 3])
    if line_ring > 0:
        ring = beyond & (dpiv > r - line_ring) & (dpiv <= r + 0.5)
        cap[ring, :3] = line_rgb
        cap[ring, 3] = np.maximum(cap[ring, 3], edge[ring])
    # under the piece
    a = rgba[..., 3:4] / 255.0 if rgba[..., 3].max() > 1.01 else rgba[..., 3:4]
    scale = 255.0 if rgba[..., 3].max() > 1.01 else 1.0
    ca = cap[..., 3:4] / scale
    out_a = a + ca * (1 - a)
    out_rgb = (rgba[..., :3] * a + cap[..., :3] * ca * (1 - a)) / np.maximum(out_a, 1e-6)
    return np.concatenate([out_rgb, out_a * scale], -1)


def save_piece(rgba: np.ndarray, path, pad=8):
    """Crop to the alpha box (+pad) and save; returns the crop offset (x0, y0)."""
    a = rgba[..., 3]
    ys, xs = np.nonzero(a > 2)
    x0, y0 = max(0, xs.min() - pad), max(0, ys.min() - pad)
    x1, y1 = min(rgba.shape[1], xs.max() + pad + 1), min(rgba.shape[0], ys.max() + pad + 1)
    Image.fromarray(np.clip(rgba[y0:y1, x0:x1] + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path)
    return int(x0), int(y0)


# ---- step 1b helpers: every one of them runs once, when the pieces are made ----
LINE_RGB = np.array([92, 58, 26], np.float32)   # the drawing's own outline brown (sampled from cut2_s21)


def grid(shape):
    yy, xx = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float32)
    return xx, yy


def fill_rgb(px: np.ndarray, src_mask: np.ndarray | None = None) -> np.ndarray:
    """RGB where every pixel takes the colour of the nearest solid pixel (alpha > 200, or src_mask)."""
    from scipy import ndimage
    m = px[..., 3] > 200 if src_mask is None else src_mask
    idx = ndimage.distance_transform_edt(~m, return_distances=False, return_indices=True)
    return px[..., :3][idx[0], idx[1]]


def section(mask: np.ndarray, at, u, maxr=200):
    """The cross-section of `mask` through `at`, perpendicular to unit vector u: (left, right) extents along
    v = (-u_y, u_x) and the section's middle point."""
    v = np.array([-u[1], u[0]], float)
    ext = []
    for sgn in (-1, 1):
        t = 0
        while t < maxr:
            x, y = np.round(np.asarray(at) + sgn * (t + 1) * v).astype(int)
            if not (0 <= y < mask.shape[0] and 0 <= x < mask.shape[1]) or not mask[y, x]:
                break
            t += 1
        ext.append(sgn * t)
    mid = np.asarray(at, float) + v * (ext[0] + ext[1]) / 2
    return ext[0], ext[1], mid


def round_joint(px: np.ndarray, pivot, inward, inset=6.0, r=None, ring=0.0, ring_rgb=LINE_RGB, drop=True):
    """Close a piece's joint end with a half disc centred on `pivot`. inward: vector from the pivot into the piece.
    The piece's pixels past the joint line (through the pivot, across the bone) are dropped (drop=True); the half disc
    beyond is filled UNDER the piece with the cross-section `inset` px inside, so shading and side lines run on and the
    end is round. r defaults to the half-width of that cross-section (a cap narrower than the limb leaves corners).
    ring > 0 draws an outline arc of that width on the cap's round edge."""
    h, w = px.shape[:2]
    u = np.asarray(inward, float)
    u /= np.hypot(*u)
    v = np.array([-u[1], u[0]])
    xx, yy = grid((h, w))
    dx, dy = xx - pivot[0], yy - pivot[1]
    along = dx * u[0] + dy * u[1]
    lat = dx * v[0] + dy * v[1]
    out = px.copy()
    if drop:
        out[along < 0] = 0
    sm = out[..., 3] > 128
    l0, l1, _ = section(sm, np.asarray(pivot, float) + u * inset, u)
    if r is None:
        r = max(-l0, l1)
    sx = (pivot[0] + u[0] * inset + lat * v[0]).astype(np.float32)
    sy = (pivot[1] + u[1] * inset + lat * v[1]).astype(np.float32)
    samp = cv2.remap(out.astype(np.float32), sx, sy, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    # outside the section's width, use the nearest edge of the section (so the disc is whole)
    latc = np.clip(lat, l0 + 5, l1 - 5)
    sx2 = (pivot[0] + u[0] * inset + latc * v[0]).astype(np.float32)
    sy2 = (pivot[1] + u[1] * inset + latc * v[1]).astype(np.float32)
    samp2 = cv2.remap(out.astype(np.float32), sx2, sy2, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    d = np.hypot(dx, dy)
    disc = (along < 0.5) & (d <= r + 0.5)
    cap = np.where(((lat > l0) & (lat < l1))[..., None], samp, samp2)
    edge = np.clip(r - d + 0.5, 0, 1)
    cap[..., 3] = np.where(disc, 255.0 * edge, 0)
    cap[..., :3] = np.where(disc[..., None], cap[..., :3], 0)
    if ring > 0:
        rr = disc & (d > r - ring) & (along < -0.5)
        cap[rr, :3] = ring_rgb
    a = out[..., 3:4] / 255
    ca = cap[..., 3:4] / 255
    oa = a + ca * (1 - a)
    rgb = (out[..., :3] * a + cap[..., :3] * ca * (1 - a)) / np.maximum(oa, 1e-6)
    return np.concatenate([rgb, oa * 255], -1)


def smooth_outline(px: np.ndarray, centre, R: float, sigma=5.0, fade=0.35):
    """Round off the piece's outline within R of `centre` (blur the alpha and cut it again): notches and zigzags of
    baggy folds near a joint become one smooth curve. New pixels take the nearest colour."""
    xx, yy = grid(px.shape)
    d = np.hypot(xx - centre[0], yy - centre[1])
    wgt = np.clip((R - d) / (fade * R), 0, 1)
    m = (px[..., 3] > 128).astype(np.float32)
    b = cv2.GaussianBlur(m, (0, 0), sigma)
    sm = np.clip((b - 0.5) * 14 + 0.5, 0, 1)
    rgb = fill_rgb(px)
    a = px[..., 3] / 255 * (1 - wgt) + sm * wgt
    keep = (px[..., 3] > 200) | (wgt <= 0)
    return np.concatenate([np.where(keep[..., None], px[..., :3], rgb), (a * 255)[..., None]], -1)


def flatten(px: np.ndarray, centre, r_flat: float, r_keep: float, rgb_flat) -> np.ndarray:
    """Fade the colours to one flat colour near a joint (fully flat within r_flat, untouched past r_keep): two
    overlapping pieces of the same cloth then show no step where they meet."""
    xx, yy = grid(px.shape)
    d = np.hypot(xx - centre[0], yy - centre[1])
    t = np.clip((d - r_flat) / max(r_keep - r_flat, 1e-3), 0, 1)[..., None]
    out = px.copy()
    out[..., :3] = px[..., :3] * t + np.asarray(rgb_flat, np.float32) * (1 - t)
    return out


def line_on(px: np.ndarray, region: np.ndarray, width=2.6, rgb=None, dark=0.52) -> np.ndarray:
    """Draw the outline on the piece's own edge inside `region` (an edge made by a cut, meant to be seen). By default
    the line is the local colour darkened (a fold line on cloth, a dark edge on steel), not one fixed brown."""
    a = (px[..., 3] > 128).astype(np.uint8)
    din = cv2.distanceTransform(a, cv2.DIST_L2, 5)
    band = np.clip(width + 0.5 - din, 0, 1) * (a > 0) * region
    out = px.copy()
    if rgb is None:
        local = cv2.blur(px[..., :3] * (px[..., 3:4] > 128), (9, 9)) / np.maximum(
            cv2.blur((px[..., 3] > 128).astype(np.float32), (9, 9))[..., None], 1e-3)
        col = local * dark
    else:
        col = np.broadcast_to(np.asarray(rgb, np.float32), px[..., :3].shape)
    out[..., :3] = out[..., :3] * (1 - band[..., None]) + col * band[..., None]
    return out


def feather_cut(px: np.ndarray, figure: np.ndarray, width=3.0) -> np.ndarray:
    """Soften a piece's cut edges (where the drawing goes on past the piece) over `width` px, so it blends into the
    neighbour drawn under it; the drawing's own silhouette edges stay as they are."""
    m = (px[..., 3] > 128).astype(np.uint8)
    cut_side = ((1 - m) & figure.astype(np.uint8)).astype(np.uint8)
    d = cv2.distanceTransform(1 - cut_side, cv2.DIST_L2, 5)
    out = px.copy()
    out[..., 3] *= np.clip((d - 0.5) / width, 0, 1)
    return out
