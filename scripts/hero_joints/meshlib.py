"""A textured triangle-strip renderer (what a small Unity strip mesh does) and the three arm kinds.

Images are float32 premultiplied RGBA in 0..1, shape (H, W, 4). Points are (x, y), y down. Angles in degrees, y down,
0 = screen right, 90 = down (so + turns clockwise on screen).

A limb is a straight drawn tube in a SOURCE picture: source axis from P0 (the joint it hangs from) along unit d, with
normal n = d turned +90 (clockwise on screen). A point of the limb is (u, v): u along the axis from P0, v along n.
The mesh samples the source picture directly (one resample).
"""
from __future__ import annotations

import math

import numpy as np


def vec(a, l=1.0):
    r = math.radians(a)
    return np.array([math.cos(r) * l, math.sin(r) * l])


def ang(v):
    return math.degrees(math.atan2(v[1], v[0]))


def bilinear(src: np.ndarray, x: np.ndarray, y: np.ndarray) -> np.ndarray:
    h, w = src.shape[:2]
    x = np.clip(x, 0, w - 1.001)
    y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(np.int32)
    y0 = np.floor(y).astype(np.int32)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    a = src[y0, x0]
    b = src[y0, x0 + 1]
    c = src[y0 + 1, x0]
    d = src[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def render_mesh(src: np.ndarray, uv_src: np.ndarray, xy_dst: np.ndarray, tris: np.ndarray, size) -> np.ndarray:
    """Draw the mesh into a new layer of `size` (w, h). Each output pixel takes the sample of the LAST triangle that
    covers its centre (overwrite, not blend: no seams along shared edges; on a fold the later triangle wins).
    uv_src: source picture coords per vertex; xy_dst: output coords per vertex."""
    W, H = size
    out = np.zeros((H, W, 4), np.float32)
    flipped = 0
    for t in tris:
        p = xy_dst[t]
        q = uv_src[t]
        x0 = max(int(math.floor(p[:, 0].min())), 0)
        x1 = min(int(math.ceil(p[:, 0].max())) + 1, W)
        y0 = max(int(math.floor(p[:, 1].min())), 0)
        y1 = min(int(math.ceil(p[:, 1].max())) + 1, H)
        if x1 <= x0 or y1 <= y0:
            continue
        (ax, ay), (bx, by), (cx, cy) = p
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            continue
        # winding of the source triangle vs the output triangle: a flip means the mesh folded over itself here
        (sa, sb), (sc, sd), (se, sf) = q
        sden = (sd - sf) * (sa - se) + (se - sc) * (sb - sf)
        if sden * den < 0:
            flipped += 1
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        px, py = xx + 0.5, yy + 0.5
        l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / den
        l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / den
        l3 = 1 - l1 - l2
        e = -1e-4
        inside = (l1 >= e) & (l2 >= e) & (l3 >= e)
        if not inside.any():
            continue
        sx = l1 * q[0, 0] + l2 * q[1, 0] + l3 * q[2, 0] - 0.5
        sy = l1 * q[0, 1] + l2 * q[1, 1] + l3 * q[2, 1] - 0.5
        samp = bilinear(src, sx[inside], sy[inside])
        region = out[y0:y1, x0:x1]
        region[inside] = samp
    render_mesh.flipped = flipped
    return out


def over(dst: np.ndarray, layer: np.ndarray, at=(0, 0)) -> None:
    """dst = layer over dst (premultiplied), layer placed with its top-left at `at` (ints)."""
    x, y = at
    h, w = layer.shape[:2]
    H, W = dst.shape[:2]
    xa, ya, xb, yb = max(x, 0), max(y, 0), min(x + w, W), min(y + h, H)
    if xb <= xa or yb <= ya:
        return
    L = layer[ya - y:yb - y, xa - x:xb - x]
    D = dst[ya:yb, xa:xb]
    D[:] = L + D * (1 - L[..., 3:4])


# ---- the limb strip -------------------------------------------------------------------------------------------------

class Limb:
    """A straight drawn tube in a source picture. P0: the joint it hangs from (shoulder), d_src: unit axis toward the
    wrist (source), u_end: where the mesh ends past the cuff (the fist is not in the source layer), u_start: how far
    the mesh starts behind P0 (negative; covers the round shoulder cap), half: mesh half width (covers the silhouette
    and its soft edge; keep it under the bend radius)."""

    def __init__(self, src: np.ndarray, P0, d_src, u_start: float, u_end: float, half: float, u_elbow: float,
                 u_wrist: float, n_seg: int = 160):
        self.src = src
        self.P0 = np.asarray(P0, float)
        self.d = np.asarray(d_src, float) / np.linalg.norm(d_src)
        self.n = np.array([-self.d[1], self.d[0]])  # +90 on screen (clockwise, y down)
        self.u0, self.u1, self.half = u_start, u_end, half
        self.ue, self.uw = u_elbow, u_wrist
        self.us = np.linspace(u_start, u_end, n_seg + 1)
        # always put mesh rows exactly on the elbow and wrist
        self.us = np.unique(np.concatenate([self.us, [u_elbow, u_wrist]]))
        n = len(self.us)
        self.tris = np.array([[2 * i, 2 * i + 1, 2 * i + 2] for i in range(n - 1)] +
                             [[2 * i + 1, 2 * i + 3, 2 * i + 2] for i in range(n - 1)])
        # interleave so later segments (toward the wrist) draw last
        order = np.argsort(np.concatenate([np.arange(n - 1), np.arange(n - 1)]), kind="stable")
        self.tris = self.tris[order]

    def uv_src(self):
        pts = []
        for u in self.us:
            c = self.P0 + self.d * u
            pts.append(c - self.n * self.half)
            pts.append(c + self.n * self.half)
        return np.array(pts)


def centreline(us: np.ndarray, a_upper: float, bend: float, ue: float, radius: float, stretch=1.0,
               s_lo=None, s_hi=None):
    """Arc-length param centreline of a bent limb: straight at angle a_upper, then a circular arc of the given radius
    turning by -bend (degrees; + bend folds the forearm counter-clockwise on screen, i.e. toward his front for the near
    arm hanging down), centred on u = ue, then straight. Band length = radius * |bend| (0 when straight).
    stretch: the stretch of the part between s_lo and s_hi (u values) - the cap and the cuff keep their length.
    Returns (points (n,2) relative to P0 = origin, tangent angles (n,))."""
    # u -> arc length s (mid-sleeve stretch)
    if stretch != 1.0 and s_lo is not None:
        s = np.where(us < s_lo, us, np.where(us > s_hi, s_lo + (s_hi - s_lo) * stretch + (us - s_hi),
                                             s_lo + (us - s_lo) * stretch))
        se = s_lo + (ue - s_lo) * stretch if s_lo <= ue <= s_hi else ue
    else:
        s, se = us, ue
    th = math.radians(abs(bend))
    band = radius * th
    sa, sb = se - band / 2, se + band / 2
    sign = -1.0 if bend > 0 else 1.0
    # tangent angle
    frac = np.clip((s - sa) / max(band, 1e-9), 0, 1) if band > 1e-9 else (s >= se).astype(float)
    phi = a_upper + sign * abs(bend) * frac
    # integrate positions analytically per part
    pts = np.zeros((len(s), 2))
    d0 = vec(a_upper)
    for i, si in enumerate(s):
        if si <= sa or band <= 1e-9:
            if band <= 1e-9 and si > se:
                pts[i] = d0 * se + vec(phi[i]) * (si - se)
            else:
                pts[i] = d0 * si
        else:
            # point at the arc start
            A = d0 * sa
            # circle centre: from A along the normal toward the turn side
            t = min(si, sb) - sa
            # rotation of the tangent so far
            dphi = sign * math.degrees(t / radius)
            nrm_ang = a_upper + sign * 90.0
            C = A + vec(nrm_ang, radius)
            # A relative to C, rotated by dphi
            rA = A - C
            r = math.radians(dphi)
            rot = np.array([[math.cos(r), -math.sin(r)], [math.sin(r), math.cos(r)]])
            P = C + rot @ rA
            if si > sb:
                P = P + vec(a_upper + sign * abs(bend)) * (si - sb)
            pts[i] = P
    return pts, phi


def rigid_two(us: np.ndarray, a_upper: float, bend: float, ue: float):
    """Kind A centreline: two straight parts meeting at the elbow at u = ue (a sharp corner)."""
    d0 = vec(a_upper)
    a2 = a_upper - bend
    d1 = vec(a2)
    pts = np.where(us[:, None] <= ue, d0[None] * us[:, None], d0[None] * ue + d1[None] * (us[:, None] - ue))
    phi = np.where(us <= ue, a_upper, a2)
    return pts, phi
