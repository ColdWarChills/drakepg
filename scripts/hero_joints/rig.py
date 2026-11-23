"""The jointed hero's rig for the arm test: body parts as rigid pictures, the near sleeve as a strip mesh in three
kinds (A stiff parts, B bends at the elbow, C soft arm), the A3 fists turning about the wrist with the sword.

Angles: y down, 0 = screen right, + clockwise (degrees). Bend b: forearm = upper - b (b > 0 folds the forearm
counter-clockwise on screen). turn = the brief's grip turn, counter-clockwise from the forearm to the blade:
blade = forearm - turn. All sizes are the drawing's own pixels (no scaling of the hero)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import cv2
import numpy as np
from PIL import Image

import fists as fistmod
from jcommon import RAW, REDO
from meshlib import Limb, ang, centreline, render_mesh, rigid_two, vec

PARTS = REDO / "parts"
P = json.loads((REDO / "parts.json").read_text())
S0 = np.array(P["S"])
W0 = np.array(P["W"])
E0 = np.array(P["E"])
L = float(P["L"])
UE = float(P["UE"])     # elbow along the sleeve: 0.52 L (old hero: upper 0.145 H, forearm 0.136 H)
A_SRC, B_SRC = float(P["a_src"]), float(P["b_src"])   # the drawn sleeve: upper part angle, its gentle drawn bend
L1, L2 = UE, L - UE
SW = P["sleeve_w"]
HALF = SW / 2 + 6       # mesh half width: the sleeve and its soft edge
R_B = 0.6 * SW          # kind B: fixed bend radius (>= HALF, so the inner edge never crosses itself)
BAND_C = 0.5 * L        # kind C: the bend spread over half the arm (radius = band / bend)
STRETCH_LO, STRETCH_HI = SW / 2 + 4, P["u_cuff_end"] - 24   # only the mid sleeve stretches (cap and cuff keep size)
FIST_SCALE = 1.0        # the A3 drawings were normalised to char_hero_bare's head; cutj3_s123 kept that size
SWORD_SCALE = 0.5814    # layers_A3 manifest
RIM_RGB = np.array([248, 217, 122], np.float32) / 255   # rim_measure.py on char_hero_bare
RIM_W = 4.5             # px at ~1030 px tall; faces up and to his back (upper left); fades to none facing down/right
SHADOW_OFF = (5, 7)     # px: the arm's shadow on the body (hard edged, like the drawing's cel shadows)
SHADOW_DARK = 0.2
KINDS = {"A": "stiff parts", "B": "bends at the elbow", "C": "soft arm"}


def to_premul(im: Image.Image) -> np.ndarray:
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    a[..., :3] *= a[..., 3:4]
    return a


def crop_layer(px):
    ys, xs = np.nonzero(px[..., 3] > 0.002)
    x0, y0, x1, y1 = xs.min() - 2, ys.min() - 2, xs.max() + 3, ys.max() + 3
    return px[y0:y1, x0:x1].copy(), (int(x0), int(y0))


class Parts:
    def __init__(self):
        self.layers = {}
        for k in ("head", "torso", "far_arm", "hips"):
            self.layers[k] = crop_layer(to_premul(Image.open(PARTS / f"{k}.png")))
        self.sleeve = to_premul(Image.open(PARTS / "near_sleeve.png"))   # full canvas, sampled by the mesh
        self.fists = fistmod.load_fists()
        sw = Image.open(RAW / "wpn_training_sword.png").convert("RGBA")
        k = SWORD_SCALE
        sw = sw.convert("RGBa").resize((round(sw.width * k), round(sw.height * k)), Image.LANCZOS).convert("RGBA")
        self.sword = to_premul(sw)
        self.sword_grip = (sw.width / 2, 0.79 * sw.height)
        self._kindA_src()

    def _kindA_src(self):
        """Kind A: the sleeve cut at the elbow into two pictures, each with a round end (a disc of the flat cloth
        colour, ringed with the sleeve's edge colour) about the elbow."""
        src = self.sleeve
        H, W = src.shape[:2]
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        d1, d2 = vec(A_SRC), vec(A_SRC - B_SRC)
        bis = (d1 + d2) / np.linalg.norm(d1 + d2)
        n = np.array([-bis[1], bis[0]])
        u = UE + (xx - E0[0]) * bis[0] + (yy - E0[1]) * bis[1]
        v = (xx - E0[0]) * n[0] + (yy - E0[1]) * n[1]
        a = src[..., 3]
        # the sleeve's own width at the elbow (from its alpha), and the edge colour (just inside its outline)
        row = (np.abs(u - UE) < 1.0) & (a > 0.5)
        vs = v[row]
        r = (vs.max() - vs.min()) / 2
        vc = (vs.max() + vs.min()) / 2
        self.A_radius = float(r)
        inside = (a > 0.5).astype(np.uint8)
        dist = cv2.distanceTransform(inside, cv2.DIST_L2, 3)
        edge = (dist > 0.5) & (dist < 2.5) & (np.abs(u - UE) < 60)
        edge_rgb = np.median(src[..., :3][edge] / np.maximum(src[..., 3:4][edge], 1e-3), 0)
        cloth = np.array(P["cloth_rgb"], np.float32) / 255
        ce = E0 + n * vc
        dd = np.hypot(xx - ce[0], yy - ce[1])
        disc_a = np.clip(r - dd + 0.5, 0, 1)
        ring = np.zeros_like(disc_a)  # no drawn ring: inside the sleeve it read as a line across the joint
        disc_rgb = cloth[None, None] * (1 - ring[..., None]) + edge_rgb[None, None] * ring[..., None]
        disc = np.concatenate([disc_rgb * disc_a[..., None], disc_a[..., None]], -1)
        self.A_src = {}
        for part, keep in (("upper", u <= UE), ("lower", u >= UE)):
            px = src * keep[..., None]
            beyond = (~keep) & (disc_a > 0)
            # the round end goes under the cut piece (so the drawn sleeve wins where both exist)
            out = px.copy()
            out[beyond] = disc[beyond] * (1 - px[beyond][..., 3:4]) + px[beyond]
            self.A_src[part] = out


# ---- kinds: forward kinematics and the elbow from the wrist ---------------------------------------------------------

def stretch_s(u, stretch):
    if stretch == 1.0:
        return u
    lo, hi = STRETCH_LO, STRETCH_HI
    return np.where(u < lo, u, np.where(u > hi, lo + (hi - lo) * stretch + (u - hi), lo + (u - lo) * stretch))


def centre(kind, us, a1, b, stretch=1.0):
    """Centreline points (relative to the shoulder) and tangent angles for the sleeve's u values."""
    if kind == "A":
        return rigid_two(us, a1, b, UE)
    th = math.radians(abs(b))
    if kind == "B":
        radius = R_B
    else:
        radius = BAND_C / th if th > 1e-6 else 1e9
    return centreline(np.asarray(us, float), a1, b, UE, radius, stretch, STRETCH_LO, STRETCH_HI)


def wrist_of(kind, a1, b, stretch=1.0):
    pts, phi = centre(kind, np.array([L]), a1, b, stretch)
    return pts[0], float(phi[0])


def chord(kind, b, stretch=1.0):
    p, _ = wrist_of(kind, 0.0, b, stretch)
    return float(np.hypot(*p)), ang(p)


def ik(kind, target, sign, stretch=1.0):
    """(a1, b) that put the wrist on `target` (relative to the shoulder); the bend's sign is given (continuity).
    Out of reach -> straight. Returns (a1, b, reached)."""
    D = float(np.hypot(*target))
    al = ang(target)
    lo, hi = 0.0, 175.0
    if chord(kind, 0.0, stretch)[0] <= D:
        b = 0.0
        reached = False
    else:
        for _ in range(50):
            mid = (lo + hi) / 2
            if chord(kind, mid, stretch)[0] > D:
                lo = mid
            else:
                hi = mid
        b = (lo + hi) / 2
        reached = True
    b *= 1 if sign >= 0 else -1
    _, off = chord(kind, b, stretch)
    return al - off, b, reached


# ---- drawing --------------------------------------------------------------------------------------------------------

def warp_into(frame, px, origin, M):
    """Rigid picture `px` (top-left at `origin` on the drawing canvas) placed by M (2x3, drawing canvas -> frame)."""
    T = np.array([[1, 0, origin[0]], [0, 1, origin[1]], [0, 0, 1]], np.float64)
    M3 = np.vstack([M, [0, 0, 1]]) @ T
    H, W = frame.shape[:2]
    lay = cv2.warpAffine(px, M3[:2], (W, H), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    frame[:] = lay + frame * (1 - lay[..., 3:4])
    return lay


def rot_about(pivot_src, pivot_dst, deg, scale=1.0):
    r = math.radians(deg)
    c, s = math.cos(r) * scale, math.sin(r) * scale
    R = np.array([[c, -s], [s, c]])
    t = np.asarray(pivot_dst) - R @ np.asarray(pivot_src)
    return np.hstack([R, t[:, None]])


def apply(M, p):
    return M[:, :2] @ np.asarray(p, float) + M[:, 2]


def rim_ring(alpha):
    """The pale rim outside the figure's alpha: about RIM_W px on edges facing up and to his back (upper left),
    narrowing to nothing on edges facing down or to his front (measured on char_hero_bare by rim_measure.py)."""
    inside = (alpha > 0.5).astype(np.uint8)
    dist = cv2.distanceTransform(1 - inside, cv2.DIST_L2, 5)
    ab = cv2.GaussianBlur(alpha, (0, 0), 3.0)
    gx, gy = cv2.Sobel(ab, cv2.CV_32F, 1, 0), cv2.Sobel(ab, cv2.CV_32F, 0, 1)
    nrm = np.hypot(gx, gy) + 1e-6
    # the direction the edge faces = minus the alpha gradient; angle with y up: 0 right, 90 up
    face = np.degrees(np.arctan2(gy / nrm, -gx / nrm))
    g = np.clip(0.55 + 0.6 * np.cos(np.radians(face - 140)), 0, 1)
    wmap = RIM_W * g
    ring = np.clip(wmap + 0.5 - dist, 0, 1) * (1 - alpha) * (nrm > 0.02)
    return ring


class Frame:
    """One pose -> one picture. Window on the drawing canvas: x from ox, y from oy, size (w, h)."""

    def __init__(self, parts: Parts, window=(-150, 190, 1800, 700)):
        self.p = parts
        self.ox, self.oy, self.w, self.h = window

    def render(self, kind, pose, rim=True, with_body=True, sword=True):
        p = self.p
        off = np.array([-self.ox, -self.oy], float)
        frame = np.zeros((self.h, self.w, 4), np.float32)
        info = {}
        waist = np.array(P["waist"])
        Mb = rot_about(waist, waist + off, pose["lean"])
        body = self._body(pose, off) if with_body else np.zeros_like(frame)
        # near shoulder (drawing canvas -> frame), with its shift
        S = apply(Mb, S0) + np.array(pose.get("shoulder", (0.0, 0.0)))
        a1, b, st = pose["a1"], pose["b"], pose.get("stretch", 1.0)
        info.update(S=S, a1=a1, b=b, stretch=st)
        # sleeve mesh
        limb_args = dict(P0=S0, d_src=vec(A_SRC), half=HALF, u_elbow=UE, u_wrist=L)
        u0, u1 = -(SW / 2 + 6), P["u_cuff_end"] + 6
        sleeve_layers = []
        self.flipped = 0
        if kind == "A":
            for part, (ua, ub) in (("upper", (u0, UE + p.A_radius + 3)), ("lower", (UE - p.A_radius - 3, u1))):
                limb = Limb(p.A_src[part], u_start=ua, u_end=ub, n_seg=60, **limb_args)
                limb.src_c = rigid_two(limb.us, A_SRC, 0.0 if part == "upper" else B_SRC, UE)
                if part == "lower":  # the lower picture: straight along the drawn forearm, from the drawn elbow
                    limb.src_c = (vec(A_SRC, UE)[None] + vec(A_SRC - B_SRC)[None] * (limb.us - UE)[:, None],
                                  np.full(len(limb.us), A_SRC - B_SRC))
                if part == "upper":
                    pts, phi = rigid_two(limb.us, a1, 0.0, UE)  # rigid: straight past the elbow
                else:
                    el = vec(a1, UE)
                    pts = el[None] + vec(a1 - b)[None] * (limb.us - UE)[:, None]
                    phi = np.full(len(limb.us), a1 - b)
                sleeve_layers.append(self._mesh(limb, pts, phi, S + 0, kind))
        else:
            limb = Limb(p.sleeve, u_start=u0, u_end=u1, n_seg=180, **limb_args)
            limb.src_c = centreline(limb.us, A_SRC, B_SRC, UE, R_B)
            pts, phi = centre(kind, limb.us, a1, b, st)
            sleeve_layers.append(self._mesh(limb, pts, phi, S, kind))
        wpt, wphi = wrist_of(kind, a1, b, st)
        Wd = S + wpt
        info.update(W=Wd, F=wphi)
        # fist + sword: one rigid unit about the wrist
        fm = pose.get("fist")
        if fm is None:  # the sleeve alone (elbow sheets)
            info["flipped"] = self.flipped
            out = np.zeros_like(frame)
            for lay in sleeve_layers:
                out[:] = lay + out * (1 - lay[..., 3:4])
            return out, info
        f = p.fists[fm]
        turn = pose["turn"]
        blade = wphi - turn
        rho = blade - f["blade"]
        Mfist = rot_about(f["wrist"], Wd, rho, FIST_SCALE)
        grip = apply(Mfist, f["grip"])
        info.update(turn=turn, blade=blade, fist=fm, delta=turn - f["turn_ccw"], grip=grip)
        if sword:
            sw_ang = blade + 90.0  # sword art: blade up = -90 in rig terms
            Ms = rot_about(p.sword_grip, grip, sw_ang)
            swl = warp_into(np.zeros_like(frame), p.sword, (0, 0), Ms)
            tip_len = (p.sword_grip[1] - 50 * SWORD_SCALE)
            info["tip"] = grip + vec(blade, tip_len)
        else:
            swl = None
        # stack: rim (under everything), body, sword, fist, sleeve; the rim is found on everything but the sword
        fist_lay = warp_into(np.zeros_like(frame), f["img"], f["origin"], Mfist)
        hero = body.copy()
        for lay in [fist_lay] + sleeve_layers:
            hero[:] = lay + hero * (1 - lay[..., 3:4])
        if rim:
            ring = rim_ring(hero[..., 3])
            frame[..., :3] = RIM_RGB[None, None] * ring[..., None]
            frame[..., 3] = ring
        frame[:] = body + frame * (1 - body[..., 3:4])
        # the arm's hard cel shadow on the body (light from his upper back): the sleeve and fist shifted, on body only
        arm_a = np.zeros(frame.shape[:2], np.float32)
        for lay in [fist_lay] + sleeve_layers:
            arm_a = lay[..., 3] + arm_a * (1 - lay[..., 3])
        sh = np.zeros_like(arm_a)
        dx, dy = SHADOW_OFF
        sh[dy:, dx:] = arm_a[:-dy, :-dx]
        sh = sh * body[..., 3] * (1 - arm_a)
        frame[..., :3] *= 1 - SHADOW_DARK * sh[..., None]
        if swl is not None:
            frame[:] = swl + frame * (1 - swl[..., 3:4])
        for lay in [fist_lay] + sleeve_layers:
            frame[:] = lay + frame * (1 - lay[..., 3:4])
        info["flipped"] = self.flipped
        return frame, info

    def _body(self, pose, off):
        p = self.p
        out = np.zeros((self.h, self.w, 4), np.float32)
        waist = np.array(P["waist"])
        Mb = rot_about(waist, waist + off, pose["lean"])
        warp_into(out, *p.layers["hips"], rot_about(waist, waist + off, 0.0))
        neck = np.array(P["neck"])
        warp_into(out, *p.layers["head"], rot_about(neck, apply(Mb, neck), pose["lean"] + pose["head"]))
        warp_into(out, *p.layers["torso"], Mb)
        fs = np.array(P["far_shoulder"])
        warp_into(out, *p.layers["far_arm"], rot_about(fs, apply(Mb, fs), pose["lean"] + pose["far"]))
        return out

    def _mesh(self, limb, pts, phi, S, kind):
        nrm = np.stack([np.cos(np.radians(phi + 90)), np.sin(np.radians(phi + 90))], 1)
        dst = []
        for c, nv in zip(pts, nrm):
            cc = S + c
            dst.append(cc - nv * limb.half)
            dst.append(cc + nv * limb.half)
        dst = np.array(dst)
        sp, sphi = limb.src_c
        snrm = np.stack([np.cos(np.radians(sphi + 90)), np.sin(np.radians(sphi + 90))], 1)
        uv = []
        for c, nv in zip(sp, snrm):
            uv.append(S0 + c - nv * limb.half)
            uv.append(S0 + c + nv * limb.half)
        out = render_mesh(limb.src, np.array(uv), dst, limb.tris, (self.w, self.h))
        self.flipped += render_mesh.flipped
        return out


def to_image(px, bg=None):
    a = px[..., 3:4]
    if bg is not None:
        out = px[..., :3] + bg * (1 - a)
        return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    rgb = np.where(a > 1e-4, px[..., :3] / np.maximum(a, 1e-4), 0)
    return Image.fromarray((np.concatenate([np.clip(rgb, 0, 1), a], -1) * 255 + 0.5).astype(np.uint8), "RGBA")
