"""Round 3 rig (round 2 + a vest cap over the sleeve root, the upper arm shorter while raised, a darker wider cast
shadow, the torso's baked sleeve sliver painted out). Round 2 text follows. Round 2 rig (kind B only): the near sleeve PAINTED by code as one straight tube (taper, hard shadow band, edge lines,
a cuff) and bent at the elbow on a fixed-radius arc; a round shoulder; ONE fist rigid with the sword, turning about the
wrist under the cuff; the body as rigid pieces moved by an upper-body transform (lean about the waist, sink, squash),
the head countering, the far arm behind the torso, the headband tails as their own piece; the arm's hard cel shadow
on the torso and hips; a flat smear under the sword.

Angles: y down, 0 = screen right, + clockwise (degrees). Bend b: forearm = upper - b. turn = counter-clockwise from the
forearm to the blade: blade = forearm - turn. Sizes: the drawing's own pixels (cutj3_s123 canvas, hero 1028 tall)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import cv2
import numpy as np
from PIL import Image, ImageDraw

import fist_r2
from jcommon import RAW, REDO
from meshlib import Limb, ang, centreline, render_mesh, vec

P1 = json.loads((REDO / "parts.json").read_text())          # round 1 geometry (read only)
PARTS2 = REDO / "parts_r2"
P2 = json.loads((REDO / "parts_r2.json").read_text()) if (REDO / "parts_r2.json").exists() else {}
S0 = np.array(P1["S"])
L = float(P1["L"])            # pivot to wrist 258.5: the same arm as the old hero (r2_armlen.py overlay)
UE = float(P1["UE"])          # elbow at 0.52 L
# painted sleeve (px)
HW0, HW1 = 33.0, 28.0         # half width at the shoulder, above the cuff (66 -> 56 wide)
UC0, UC1 = L - 16.0, L + 6.0  # the cuff: from the seam to its front edge
HWC = 31.5                    # the cuff's half width (63 wide)
CUFF_CURVE = 2.5              # px: the cuff's front edge and seam bow toward the hand at the middle
HALF = 36.0                   # mesh half width
R_B = 40.0                    # the elbow's bend radius (>= HALF: the inner edge never crosses itself)
SHADE_SIDE = +1.0             # the shadow band's side (v > 0: under the forearm at ready, under the arm at strike)
SHOULDER_R = 38.0             # the round shoulder under the sleeve
ARMHOLE = False               # a vest strip over the sleeve root: tried (_shoulder_try3.png), worse, off
STRETCH_LO, STRETCH_HI = 60.0, UC0 - 8   # r3: the stretch starts below the cap's hem shadow
CREAM = np.array([237, 215, 166], np.float32) / 255
CREAM_SH = np.array([217, 189, 140], np.float32) / 255
LINE_DARK = 0.74              # edge lines: the local colour x this
SWORD_SCALE = 0.5814
RIM_RGB = np.array([248, 217, 122], np.float32) / 255
RIM_W = 4.5
CAST_OFF = (22, 25)
CAST_K = 0.6           # px: the arm's cel shadow on the torso and hips (light from his upper back)
SMEAR_RGB = np.array([255, 247, 226], np.float32) / 255
SRC_S = np.array([60.0, 60.0])  # the painted sleeve's pivot on its own canvas; axis straight down
SRC_SIZE = (120, int(60 + UC1 + 20))


def to_premul(im: Image.Image) -> np.ndarray:
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    a[..., :3] *= a[..., 3:4]
    return a


def crop_layer(px):
    ys, xs = np.nonzero(px[..., 3] > 0.002)
    x0, y0, x1, y1 = xs.min() - 2, ys.min() - 2, xs.max() + 3, ys.max() + 3
    return px[y0:y1, x0:x1].copy(), (int(x0), int(y0))


def hw_of(u):
    return np.where(u < 0, HW0, HW0 + (HW1 - HW0) * np.clip(u / UC0, 0, 1))


def paint_sleeve():
    """The straight sleeve on its own canvas (premultiplied float): pivot at SRC_S, axis straight down (+y).
    u = along the axis, v = across (v > 0 toward -x, the Limb's normal)."""
    W, H = SRC_SIZE
    ss = 4  # supersample for anti-aliased hard edges
    yy, xx = np.mgrid[0:H * ss, 0:W * ss].astype(np.float32)
    u = (yy + 0.5) / ss - SRC_S[1]
    v = -((xx + 0.5) / ss - SRC_S[0])
    hw = hw_of(u)
    vn = np.clip(np.abs(v) / HWC, 0, 1)
    seam = UC0 + CUFF_CURVE * (1 - vn ** 2)
    front = UC1 + CUFF_CURVE * (1 - vn ** 2)
    tube = (u >= 0) & (u < seam) & (np.abs(v) <= hw)
    cap = (u < 0) & (np.hypot(u, v) <= HW0)
    cuff = (u >= seam) & (u <= front) & (np.abs(v) <= HWC)
    shape = tube | cap | cuff
    hw_all = np.where(cuff, HWC, hw)
    band = (SHADE_SIDE * v > hw_all / 3) & shape
    rgb = np.where(band[..., None], CREAM_SH, CREAM)
    m = shape.astype(np.uint8)
    # supersampled -> 1x: coverage and mean colour
    def down(a):
        return a.reshape(H, ss, W, ss, *a.shape[2:]).mean((1, 3))
    cov = down(m.astype(np.float32))
    col = down(rgb * m[..., None]) / np.maximum(cov[..., None], 1e-4)
    # edge lines (just inside the outline) and the cuff's seam line, darker
    inside = (cov > 0.5).astype(np.uint8)
    d_in = cv2.distanceTransform(inside, cv2.DIST_L2, 5)
    line = np.clip(2.6 - d_in, 0, 1) * (cov > 0.02)
    u1 = (np.arange(H)[:, None] + 0.5) - SRC_S[1]
    v1 = -((np.arange(W)[None, :] + 0.5) - SRC_S[0])
    vn1 = np.clip(np.abs(v1) / HWC, 0, 1)
    seam1 = UC0 + CUFF_CURVE * (1 - vn1 ** 2)
    seam_line = np.clip(1.3 - np.abs(u1 - seam1 - 0.6), 0, 1) * (np.abs(v1) < HWC)
    # the cap's edge stays plain where the round shoulder sits behind it (only the outer edge shows a line)
    dark = np.maximum(line, seam_line)
    col = col * (1 - (1 - LINE_DARK) * dark[..., None])
    px = np.concatenate([col * cov[..., None], cov[..., None]], -1).astype(np.float32)
    return px


CAP_REST = float(__import__("os").environ.get("HJ_CAP_REST", "0"))  # r3 try 2: the Qwen cap's turn at rest (deg)
CAP_SHADOW = (5, 6)           # px: the cap's hard shadow on the sleeve (light from his upper back)
CAP_R = HW0 + 7.0             # r3 cap: the round top (covers the sleeve's own round top, HW0, at every angle)
CAP_PAD = 3.0                 # the cap is this much wider than the sleeve on each side
HEM0, HEM_SLANT, HEM_BOW = 44.0, 5.0, 3.0   # the hem: u at the middle, longer on the band side, bowed to the hand
HEM_SH, HEM_SH_K = 8.0, 0.93  # the hem's hard shadow on the sleeve: depth px, colour = sleeve shadow x this


def hem_u(v):
    vn = np.clip(np.asarray(v, float) / HW0, -1, 1)
    return HEM0 + HEM_SLANT * SHADE_SIDE * vn + HEM_BOW * (1 - vn ** 2)


def paint_cap(lit, shadow):
    """The vest's cap over the sleeve root, on its own canvas: pivot at (c, c), axis straight down, rigid with the upper
    arm. The vest's two tones (band on the sleeve's band side); no lines here (drawn per frame where it borders
    anything but the vest)."""
    n = int(2 * (HEM0 + HEM_SLANT + HEM_BOW + 10))
    ss = 4
    yy, xx = np.mgrid[0:n * ss, 0:n * ss].astype(np.float32)
    c = n / 2
    u = (yy + 0.5) / ss - c
    v = -((xx + 0.5) / ss - c)
    hw = hw_of(np.maximum(u, 0)) + CAP_PAD
    shape = (np.hypot(u, v) <= CAP_R) | ((u >= 0) & (u <= hem_u(v)) & (np.abs(v) <= hw))
    band = (SHADE_SIDE * v > hw / 3) & shape
    rgb = np.where(band[..., None], shadow, lit)
    cov = shape.reshape(n, ss, n, ss).mean((1, 3)).astype(np.float32)
    col = (rgb * shape[..., None]).reshape(n, ss, n, ss, 3).mean((1, 3)) / np.maximum(cov[..., None], 1e-4)
    return np.concatenate([col * cov[..., None], cov[..., None]], -1).astype(np.float32), (c, c)


def patch_torso(px, lit):
    """The torso part still carries a cream sliver of the old sleeve top (x 655-700, y 495-700): paint it in the vest's
    lit tone and redraw the vest's edge line along the outline there (premultiplied, canvas coordinates)."""
    x0, y0, x1, y1 = 650, 492, 706, 705
    reg = px[y0:y1, x0:x1]
    a = reg[..., 3]
    rgb = reg[..., :3] / np.maximum(a[..., None], 1e-4)
    warm = ((rgb[..., 0] > rgb[..., 1] + 0.02) & (a > 0.01)).astype(np.uint8)
    warm = cv2.morphologyEx(warm, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    warm = cv2.dilate(warm, np.ones((3, 3), np.uint8)).astype(bool) & (a > 0.01)
    rgb[warm] = lit
    inside = (a > 0.5).astype(np.uint8)
    d_in = cv2.distanceTransform(np.pad(inside, 3, constant_values=1), cv2.DIST_L2, 5)[3:-3, 3:-3]
    line = np.clip(2.6 - d_in, 0, 1) * (a > 0.02)
    rgb = rgb * (1 - (1 - LINE_DARK) * line[..., None])
    reg[..., :3] = rgb * a[..., None]
    return int(warm.sum())


def paint_shoulder():
    """The round shoulder: a cream disc with an edge line and the shadow band's side darker (rotated with the arm)."""
    r = SHOULDER_R
    n = int(2 * r + 8)
    ss = 4
    yy, xx = np.mgrid[0:n * ss, 0:n * ss].astype(np.float32)
    c = n / 2
    x = (xx + 0.5) / ss - c
    y = (yy + 0.5) / ss - c
    disc = np.hypot(x, y) <= r
    v = -x  # same frame as the sleeve source (axis +y, v toward -x)
    band = (SHADE_SIDE * v > r / 3) & disc
    rgb = np.where(band[..., None], CREAM_SH, CREAM)
    cov = disc.reshape(n, ss, n, ss).mean((1, 3)).astype(np.float32)
    col = (rgb * disc[..., None]).reshape(n, ss, n, ss, 3).mean((1, 3)) / np.maximum(cov[..., None], 1e-4)
    d_in = cv2.distanceTransform((cov > 0.5).astype(np.uint8), cv2.DIST_L2, 5)
    line = np.clip(2.6 - d_in, 0, 1)
    col = col * (1 - (1 - LINE_DARK) * line[..., None])
    return np.concatenate([col * cov[..., None], cov[..., None]], -1).astype(np.float32), (c, c)


class Parts:
    def __init__(self, parts_dir=None):
        d = pathlib.Path(parts_dir) if parts_dir else PARTS2
        self.geo = json.loads((d.parent / f"{d.name}.json").read_text())
        self.layers = {}
        self.vest_lit = np.array(self.geo["vest_lit"], np.float32) / 255
        self.vest_shadow = np.array(self.geo["vest_shadow"], np.float32) / 255
        for k in ("tails", "far_arm", "head", "torso", "hips", "armhole"):
            full = to_premul(Image.open(d / f"{k}.png"))
            if k == "torso":
                self.patched = patch_torso(full, self.vest_lit)
            self.layers[k] = crop_layer(full)
        self.sleeve = paint_sleeve()
        g3 = json.loads((REDO / "parts_r3.json").read_text())
        self.hinge = np.array(g3["hinge"], float)
        self.outline = np.array(g3["outline"], np.float32) / 255
        cap = to_premul(Image.open(REDO / "parts_r3" / "cap.png"))
        seam = np.asarray(Image.open(REDO / "parts_r3" / "cap_seam.png")).astype(np.float32)[..., None] / 255
        ys, xs = np.nonzero(cap[..., 3] > 0.002)
        x0, y0, x1, y1 = xs.min() - 3, ys.min() - 3, xs.max() + 4, ys.max() + 4
        self.cap = (cap[y0:y1, x0:x1].copy(), (int(x0), int(y0)))
        self.cap_seam = (np.ascontiguousarray(seam[y0:y1, x0:x1, 0]), (int(x0), int(y0)))
        self.fist = fist_r2.load_fist()
        sw = Image.open(RAW / "wpn_training_sword.png").convert("RGBA")
        k = SWORD_SCALE
        sw = sw.convert("RGBa").resize((round(sw.width * k), round(sw.height * k)), Image.LANCZOS).convert("RGBA")
        self.sword = to_premul(sw)
        self.sword_grip = (sw.width / 2, 0.79 * sw.height)
        self.tip_len = self.sword_grip[1] - 50 * SWORD_SCALE
        self.cast_ratio = np.full(3, CAST_K, np.float32)   # r3: 0.6 of the lit tone (round 2's ratio was faint)


# ---- the arm ---------------------------------------------------------------------------------------------------------

SH_LO = 60.0                  # r3: the upper arm shortens between here and the elbow (the cap and its hem keep size)
UA_MIN = 0.70                 # the upper arm's length while raised (share of UE), eased in by the lift
LIFT_PX = 10.0                # the shoulder rises this much more while the arm is raised


def lift_of(a1):
    """0 with the upper arm hanging down, 1 straight up; periodic in a1 (no seam over the full circle)."""
    e = (1 - math.cos(math.radians(a1 - 90.0))) / 2
    t = min(max((e - 0.3) / 0.6, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def ua_of(a1):
    return 1.0 - (1.0 - UA_MIN) * lift_of(a1)


def smap(us, stretch=1.0, k=1.0):
    """u along the drawn sleeve -> arc length on screen: the upper arm shortened to k of its length between SH_LO
    and the elbow; the mid sleeve stretched between STRETCH_LO and STRETCH_HI (cap and cuff keep their length)."""
    us = np.asarray(us, float)
    d_sh = (1.0 - k) * UE
    seg = UE - SH_LO
    def sh(u):
        return np.where(u < SH_LO, u, np.where(u < UE, SH_LO + (u - SH_LO) * (seg - d_sh) / seg, u - d_sh))
    s = sh(us)
    if stretch != 1.0:
        lo, hi = float(sh(np.array(STRETCH_LO))), float(sh(np.array(STRETCH_HI)))
        s = s + (stretch - 1.0) * (np.clip(s, lo, hi) - lo)
    return s


def centre(us, a1, b, stretch=1.0, k=1.0):
    s = smap(us, stretch, k)
    se = float(smap(np.array([UE]), stretch, k)[0])
    return centreline(s, a1, b, se, R_B)


def wrist_of(a1, b, stretch=1.0, k=1.0):
    pts, phi = centre(np.array([L]), a1, b, stretch, k)
    return pts[0], float(phi[0])


def chord(b, stretch=1.0, k=1.0):
    p, _ = wrist_of(0.0, b, stretch, k)
    return float(np.hypot(*p)), ang(p)


def ik(target, sign, stretch=1.0, k=1.0):
    """(a1, b) that put the wrist on `target` (relative to the shoulder), bend sign given."""
    D = float(np.hypot(*target))
    al = ang(target)
    lo, hi = 0.0, 175.0
    if chord(0.0, stretch, k)[0] <= D:
        b = 0.0
    else:
        for _ in range(50):
            mid = (lo + hi) / 2
            if chord(mid, stretch, k)[0] > D:
                lo = mid
            else:
                hi = mid
        b = (lo + hi) / 2
    b *= 1 if sign >= 0 else -1
    _, off = chord(b, stretch, k)
    return al - off, b


def arm_len(stretch=1.0, k=1.0):
    return float(smap(np.array([L]), stretch, k)[0])


def wrist_A(a1, b, k=1.0):
    """Kind A's wrist for keys given as two straight parts (round 1's key poses); upper arm k of its length."""
    return vec(a1, UE * k) + vec(a1 - b, L - UE)


# ---- placing pictures -------------------------------------------------------------------------------------------------

def M3(M):
    return np.vstack([M, [0, 0, 1]])


def rot_about(pivot_src, pivot_dst, deg, scale=1.0):
    r = math.radians(deg)
    c, s = math.cos(r) * scale, math.sin(r) * scale
    R = np.array([[c, -s], [s, c]])
    t = np.asarray(pivot_dst, float) - R @ np.asarray(pivot_src, float)
    return np.hstack([R, t[:, None]])


def apply(M, p):
    return M[:, :2] @ np.asarray(p, float) + M[:, 2]


def warp(px, origin, M, size):
    T = np.array([[1, 0, origin[0]], [0, 1, origin[1]], [0, 0, 1]], np.float64)
    M = (M3(M) @ T)[:2]
    W, H = size
    return cv2.warpAffine(px, M, (W, H), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)


def over(dst, lay):
    dst[:] = lay + dst * (1 - lay[..., 3:4])


def rim_ring(alpha):
    inside = (alpha > 0.5).astype(np.uint8)
    dist = cv2.distanceTransform(1 - inside, cv2.DIST_L2, 5)
    ab = cv2.GaussianBlur(alpha, (0, 0), 3.0)
    gx, gy = cv2.Sobel(ab, cv2.CV_32F, 1, 0), cv2.Sobel(ab, cv2.CV_32F, 0, 1)
    nrm = np.hypot(gx, gy) + 1e-6
    face = np.degrees(np.arctan2(gy / nrm, -gx / nrm))
    g = np.clip(0.55 + 0.6 * np.cos(np.radians(face - 140)), 0, 1)
    return np.clip(RIM_W * g + 0.5 - dist, 0, 1) * (1 - alpha) * (nrm > 0.02)


def fill_polys(shapes, size, ss=3):
    """[(points (n,2) frame px, alpha)] -> a premultiplied smear layer, flat colour, anti-aliased hard edges."""
    W, H = size
    out = np.zeros((H, W, 4), np.float32)
    for pts, a in shapes:
        pts = np.asarray(pts, float)
        if len(pts) < 3 or a <= 0:
            continue
        x0 = max(int(pts[:, 0].min()) - 2, 0)
        y0 = max(int(pts[:, 1].min()) - 2, 0)
        x1 = min(int(pts[:, 0].max()) + 3, W)
        y1 = min(int(pts[:, 1].max()) + 3, H)
        if x1 <= x0 or y1 <= y0:
            continue
        im = Image.new("L", ((x1 - x0) * ss, (y1 - y0) * ss), 0)
        ImageDraw.Draw(im).polygon([((x - x0) * ss, (y - y0) * ss) for x, y in pts], fill=255)
        cov = np.asarray(im.resize((x1 - x0, y1 - y0), Image.BOX)).astype(np.float32) / 255 * a
        reg = out[y0:y1, x0:x1]
        lay = np.concatenate([SMEAR_RGB * cov[..., None], cov[..., None]], -1)
        reg[:] = lay + reg * (1 - lay[..., 3:4])
    return out


class Frame:
    """One pose -> one picture. Window on the drawing canvas: x from ox, y from oy, size (w, h)."""

    def __init__(self, parts: Parts, window=(-150, -110, 1800, 1000)):
        self.p = parts
        self.ox, self.oy, self.w, self.h = window
        n = int(UC1 + 10 + 36)
        self.limb = Limb(parts.sleeve, P0=SRC_S, d_src=(0.0, 1.0), u_start=-(HW0 + 3), u_end=UC1 + 4, half=HALF,
                         u_elbow=UE, u_wrist=L, n_seg=n)
        self.limb.src_c = (np.stack([np.zeros_like(self.limb.us), self.limb.us], 1), np.full(len(self.limb.us), 90.0))

    def body_mats(self, pose):
        """Matrices (drawing canvas -> frame) per body piece."""
        g = self.p.geo
        off = np.array([-self.ox, -self.oy], float)
        waist = np.array(g["waist"], float)
        lean, dy, sq = pose.get("lean", 0.0), pose.get("dy", 0.0), pose.get("sq", 1.0)
        Tm = np.array([[1, 0, -waist[0]], [0, 1, -waist[1]], [0, 0, 1]])
        Sm = np.diag([1.0, sq, 1.0])
        r = math.radians(lean)
        Rm = np.array([[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]])
        Tb = np.array([[1, 0, waist[0] + off[0]], [0, 1, waist[1] + dy + off[1]], [0, 0, 1]])
        Mub = Tb @ Rm @ Sm @ Tm
        neck = np.array(g["neck"], float)
        Mhead = Mub @ M3(rot_about(neck, neck, pose.get("head", 0.0)))
        knot = np.array(g["knot"], float)
        Mtails = Mhead @ M3(rot_about(knot, knot, pose.get("tail", 0.0)))
        fs = np.array(g["far_shoulder"], float)
        Mfar = Mub @ np.array([[1, 0, pose.get("fx", 0.0)], [0, 1, pose.get("fy", 0.0)], [0, 0, 1]]) @ \
            M3(rot_about(fs, fs, pose.get("far", 0.0)))
        Mhips = np.array([[1, 0, off[0]], [0, 1, off[1] + pose.get("hdy", 0.0)], [0, 0, 1]])
        # r3: the shrug turns the vest cap about its collar end (the hinge); the arm's pivot rides in the cap's frame
        sx, sy = pose.get("shoulder", (0.0, 0.0))
        H = self.p.hinge
        phi = -math.degrees(sy / float(np.hypot(*(S0 - H))))
        S = apply(Mub[:2], apply(rot_about(H, H, phi), S0)) + np.array([sx, 0.0])
        Mcap = Mub @ M3(rot_about(H, H, CAP_REST + phi))
        return {"hips": Mhips[:2], "torso": Mub[:2], "armhole": Mub[:2], "head": Mhead[:2], "tails": Mtails[:2],
                "far_arm": Mfar[:2], "ub": Mub[:2], "cap": Mcap[:2], "cap_phi": phi}, S

    def arm(self, pose, S):
        """Wrist, forearm angle at the wrist, the sleeve layer."""
        a1, b, st, k = pose["a1"], pose["b"], pose.get("stretch", 1.0), pose.get("ua", 1.0)
        pts, phi = centre(self.limb.us, a1, b, st, k)
        nrm = np.stack([np.cos(np.radians(phi + 90)), np.sin(np.radians(phi + 90))], 1)
        dst = np.empty((2 * len(pts), 2))
        dst[0::2] = S + pts - nrm * HALF
        dst[1::2] = S + pts + nrm * HALF
        sp, sphi = self.limb.src_c
        snrm = np.stack([np.cos(np.radians(sphi + 90)), np.sin(np.radians(sphi + 90))], 1)
        uv = np.empty_like(dst)
        uv[0::2] = SRC_S + sp - snrm * HALF
        uv[1::2] = SRC_S + sp + snrm * HALF
        lay = render_mesh(self.limb.src, uv, dst, self.limb.tris, (self.w, self.h))
        flipped = render_mesh.flipped
        wpt, wphi = wrist_of(a1, b, st, k)
        el, _ = centre(np.array([UE]), a1, b, st, k)
        return S + wpt, wphi, lay, flipped, S + el[0]

    def cap_uncovered(self, pose, S, ca=None):
        """Sleeve pixels with u < 10 (its round top and the first 10 px) not under the cap (alpha < 0.5)."""
        if ca is None:
            mats, _ = self.body_mats(pose)
            ca = warp(self.p.cap[0][..., 3:4].copy(), self.p.cap[1], mats["cap"], (self.w, self.h))
        uu, vv = np.mgrid[-HW0:10.01:1.0, -HW0:HW0 + 0.01:1.0]
        ok = np.where(uu < 0, np.hypot(uu, vv) <= HW0, np.abs(vv) <= hw_of(np.maximum(uu, 0)))
        uu, vv = uu[ok], vv[ok]
        a1 = pose["a1"]
        x = S[0] + np.cos(np.radians(a1)) * uu + np.cos(np.radians(a1 + 90)) * vv
        y = S[1] + np.sin(np.radians(a1)) * uu + np.sin(np.radians(a1 + 90)) * vv
        xi, yi = np.round(x).astype(int), np.round(y).astype(int)
        inb = (xi >= 0) & (xi < self.w) & (yi >= 0) & (yi < self.h)
        cov = np.zeros(len(xi))
        cov[inb] = ca[yi[inb], xi[inb]]
        return int((cov < 0.5).sum())

    def render(self, pose, rim=True, smear=None, sword=True, body=True, cast=True):
        p = self.p
        size = (self.w, self.h)
        frame = np.zeros((self.h, self.w, 4), np.float32)
        mats, S = self.body_mats(pose)
        info = {"S": S}
        lays = {}
        if body:
            for k in ("tails", "far_arm", "head", "torso", "hips"):
                lays[k] = warp(*p.layers[k], mats[k], size)
        Wd, F, sleeve, flipped, elbow = self.arm(pose, S)
        info.update(W=Wd, F=F, flipped=flipped, elbow=elbow, a1=pose["a1"], b=pose["b"])
        # r3: the vest cap (cut from the Qwen edit), torso-rigid, turned about its collar end by the shrug
        shoulder = warp(*p.cap, mats["cap"], size)
        # fist + sword: one rigid unit about the wrist
        f = p.fist
        turn = pose["turn"]
        blade = F - turn
        rho = blade - f["blade"]
        Mf = rot_about(f["wrist"], Wd, rho)
        fist = warp(f["img"], f["origin"], Mf, size)
        grip = apply(Mf, f["grip"])
        info.update(turn=turn, blade=blade, grip=grip, wrist_bend=turn - f["turn_ccw"],
                    tip=grip + vec(blade, p.tip_len))
        swl = None
        if sword:
            Ms = rot_about(p.sword_grip, grip, blade + 90.0)
            swl = warp(p.sword, (0, 0), Ms, size)
        # the cuff's small hard shadow on the fist: fist pixels just past the cuff's front edge
        fd = vec(F)
        fn = np.array([-fd[1], fd[0]])
        cuff_front = Wd + fd * (UC1 - L)
        yy, xx = np.mgrid[0:self.h, 0:self.w].astype(np.float32)
        x0, x1 = int(max(Wd[0] - 90, 0)), int(min(Wd[0] + 90, self.w))
        y0, y1 = int(max(Wd[1] - 90, 0)), int(min(Wd[1] + 90, self.h))
        if x1 > x0 and y1 > y0:
            sx, sy = xx[y0:y1, x0:x1] - cuff_front[0], yy[y0:y1, x0:x1] - cuff_front[1]
            t = sx * fd[0] + sy * fd[1]
            vv = sx * fn[0] + sy * fn[1]
            vn = np.clip(np.abs(vv) / HWC, 0, 1)
            t0 = CUFF_CURVE * (1 - vn ** 2)
            shade = np.clip(t - t0 + 0.5, 0, 1) * np.clip(t0 + 6.0 - t + 0.5, 0, 1) * (np.abs(vv) < HWC - 1)
            ratio = fist_r2.SKIN_SHADOW / fist_r2.SKIN_LIT
            reg = fist[y0:y1, x0:x1]
            reg[..., :3] *= 1 - (1 - ratio) * shade[..., None]
        arm_layers = [fist, sleeve]
        # rim: outside everything but the sword
        hero_a = np.zeros((self.h, self.w), np.float32)
        for k in ("tails", "far_arm", "head", "torso", "hips"):
            if k in lays:
                a = lays[k][..., 3]
                hero_a = a + hero_a * (1 - a)
        for lay in [shoulder] + arm_layers:
            a = lay[..., 3]
            hero_a = a + hero_a * (1 - a)
        if rim:
            ring = rim_ring(hero_a)
            frame[..., :3] = RIM_RGB * ring[..., None]
            frame[..., 3] = ring
        for k in ("tails", "far_arm", "head", "torso", "hips"):
            if k in lays:
                over(frame, lays[k])
        # the arm's cel shadow (sleeve, fist and the sword's hilt) on the torso and hips, hard edged
        if cast and body:
            arm_a = np.zeros((self.h, self.w), np.float32)
            for lay in arm_layers:
                a = lay[..., 3]
                arm_a = a + arm_a * (1 - a)
            if swl is not None:
                hilt = (np.hypot(xx - grip[0], yy - grip[1]) < 95).astype(np.float32)
                a = swl[..., 3] * hilt
                arm_a = a + arm_a * (1 - a)
            dx, dy = CAST_OFF
            sh = np.zeros_like(arm_a)
            sh[dy:, dx:] = arm_a[:-dy, :-dx]
            sh = np.clip((sh - 0.5) * 2.5 + 0.5, 0, 1)  # hard edge
            tb = lays["torso"][..., 3]
            tb = lays["hips"][..., 3] + tb * (1 - lays["hips"][..., 3])
            sh = sh * tb
            frame[..., :3] *= 1 - (1 - p.cast_ratio) * sh[..., None]
            info["cast_px"] = float(sh.sum())
        if smear:
            over(frame, fill_polys(smear, size))
        if swl is not None:
            over(frame, swl)
        for lay in arm_layers:
            over(frame, lay)
        # r3: the cap's hard shadow on the sleeve just outside its edge, then the cap; the seam's line only where the
        # cap borders anything but the visible vest (the sleeve coming out from under it)
        ca = shoulder[..., 3]
        sleeve_a = sleeve[..., 3]
        ddx, ddy = CAP_SHADOW
        csh = np.zeros_like(ca)
        csh[ddy:, ddx:] = ca[:-ddy, :-ddx]
        csh = np.clip((csh - 0.5) * 3 + 0.5, 0, 1) * sleeve_a * (1 - ca)
        frame[..., :3] *= 1 - (1 - CREAM_SH / CREAM * HEM_SH_K) * csh[..., None]
        cap = shoulder.copy()
        if body:
            seam = cv2.warpAffine(p.cap_seam[0], (M3(mats["cap"]) @ np.array(
                [[1, 0, p.cap_seam[1][0]], [0, 1, p.cap_seam[1][1]], [0, 0, 1]], np.float64))[:2], size,
                flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
            arm_now = np.zeros_like(ca)
            for lay in arm_layers + ([swl] if swl is not None else []):
                a = lay[..., 3]
                arm_now = a + arm_now * (1 - a)
            vis_vest = lays["torso"][..., 3] * (1 - arm_now) * (1 - ca)
            near = cv2.dilate(vis_vest, np.ones((9, 9), np.uint8))
            line = seam * (1 - np.clip((near - 0.3) / 0.5, 0, 1))
            cap[..., :3] = cap[..., :3] * (1 - line[..., None]) + p.outline * ca[..., None] * line[..., None]
            info["seam_line_px"] = float(line.sum())
        over(frame, cap)
        info["cap_uncovered"] = self.cap_uncovered(pose, S, ca)
        if body and ARMHOLE:  # the vest's armhole edge over the sleeve's root (tried, off: see HOWTO round 2)
            over(frame, warp(*p.layers["armhole"], mats["armhole"], size))
        return frame, info


def to_image(px, bg=None):
    a = px[..., 3:4]
    if bg is not None:
        out = px[..., :3] + bg * (1 - a)
        return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    rgb = np.where(a > 1e-4, px[..., :3] / np.maximum(a, 1e-4), 0)
    return Image.fromarray((np.concatenate([np.clip(rgb, 0, 1), a], -1) * 255 + 0.5).astype(np.uint8), "RGBA")
