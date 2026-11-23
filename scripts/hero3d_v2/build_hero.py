"""Build the bare hero as a clean, rigged 3D model (Blender, headless). Every part is a loft of superellipse rings
whose sizes were read off the drawn orthographic views (art/redo/animway/cutB, 913 px = 1.75 m; numbers from
measure.py), subdivided, one flat colour per garment (sampled from art/raw/char_hero_bare.png by sample_colors.py),
the drawn face as the one small texture. Skin weights are given by construction: each part knows its bones and blends
smoothly across each joint. Writes <out>/hero.fbx, hero.glb (no outline hull) and hero.blend (with the hull).

blender -b --python scripts/hero3d_v2/build_hero.py -- <out dir> <face.png> [--head 1.0] [--fist 1.0] [--name hero]
The hero faces -Y (Blender front), his right side is -X. Rest pose: A-pose, arms 45 deg down."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import hero_lib as L  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
OUT, FACE = argv[0], argv[1]


def opt(k, d):
    return type(d)(argv[argv.index(k) + 1]) if k in argv else d


HEAD = opt("--head", 1.24)     # head (with hair and band) scale about the chin/neck point (1.0 = drawn proportions)
FIST = opt("--fist", 1.08)      # fist scale about the wrist (1.0 = drawn proportions)
NAME = opt("--name", "hero")
S = L.S

# ------------------------------------------------------------------ colours (lit, shade) from char_hero_bare.png
COL = {
    "Skin": ("#df9d4f", "#b06f2b"), "Hair": ("#2e1e0d", "#1a1107"), "Headband": ("#861a0f", "#4d0e05"),
    "Shirt": ("#debe85", "#b79764"), "Belt": ("#3a2209", "#201302"), "Buckle": ("#a08048", "#6a5030"),
    "Trousers": ("#1b312d", "#0e2320"), "Boots": ("#45290d", "#2b1a06"),
    "Steel": ("#c3bfb2", "#878377"), "Grip": ("#8b5a2b", "#5a3414"),       # the sword, after wpn_training_sword
    # inner lines (thin geometry lying on the surface): finger lines, shirt folds, the blade's fuller
    "LineSkin": ("#8a4c1c", "#8a4c1c"), "LineShirt": ("#a2834f", "#a2834f"), "LineSteel": ("#6f6b60", "#6f6b60"),
}

# ------------------------------------------------------------------ skeleton (rest, metres)
A45 = math.radians(45)
J = {"Hips": (0, 0, 0.93), "Spine": (0, 0, 1.03), "Chest": (0, 0, 1.15), "Neck": (0, -0.005, 1.38),
     "Head": (0, -0.03, 1.45), "HeadTop": (0, -0.03, 1.70)}
SIDES = {"Right": -1, "Left": 1}            # his right = -X
for side, s in SIDES.items():
    sho = Vector((s * 0.165, 0, 1.315))
    d = Vector((s * math.cos(A45), 0, -math.sin(A45)))
    J[side + "Clav"] = (s * 0.03, 0, 1.345)
    J[side + "Sho"] = tuple(sho)
    J[side + "Dir"] = tuple(d)
    J[side + "Elb"] = tuple(sho + d * 0.27)
    J[side + "Wri"] = tuple(sho + d * 0.43)
    J[side + "HandEnd"] = tuple(sho + d * 0.55)
    hip, ank = Vector((s * 0.095, 0, 0.86)), Vector((s * 0.13, 0, 0.10))
    J[side + "Hip"], J[side + "Ank"] = tuple(hip), tuple(ank)
    J[side + "Kne"] = tuple(hip + (ank - hip) * 0.5)
    J[side + "Ball"] = (s * 0.135, -0.12, 0.03)
    J[side + "Toe"] = (s * 0.135, -0.20, 0.03)
    n = (Vector((0, 0, 1)) - d * d.z).normalized()       # back of the hand (palm down in the rest pose)
    J[side + "N"] = tuple(n)
    wri = sho + d * 0.42                                  # the fist scales about the wrist; so does its grip point
    J[side + "Grip"] = tuple(wri + (sho + d * 0.49 - n * 0.01 - wri) * FIST)
FWD = Vector((0, -1, 0))

L.clear()
LIGHT = L.light_world()
face_img = bpy.data.images.load(FACE)
MATS = {k: L.toon_material(k, b, sh, LIGHT) for k, (b, sh) in COL.items()}
MATS["Face"] = L.toon_material("Face", *COL["Skin"], LIGHT, image=face_img)

# ------------------------------------------------------------------ part builder
OBJS = {}


def add(obj, mat, verts, faces, wfn, subdiv=1, uvfn=None, scale=None, mask=None, oline=None):
    """Subdivide one primitive, give it weights wfn(P) -> {bone: w}, append it to object `obj` with material `mat`.
    mask(P) -> 0/1 per vertex: forced shade (written to the colour attribute "shade", R channel)."""
    tmp = L.make_obj("tmp", verts, faces, subdiv)
    P, F = L.mesh_arrays(tmp)
    bpy.data.objects.remove(tmp)
    o = OBJS.setdefault(obj, {"P": [], "F": [], "M": [], "UV": [], "W": [], "mats": [], "K": [], "G": []})
    if mat not in o["mats"]:
        o["mats"].append(mat)
    off = sum(len(p) for p in o["P"])
    W = wfn(P)
    uv = uvfn(P) if uvfn else np.zeros((len(P), 2))
    o["K"].append((np.asarray(mask(P)) > 0.5).astype(float) if mask else np.zeros(len(P)))
    # outline width factor: 0 on inner lines and on hidden overlaps (oline(P) -> 0/1), else 1
    o["G"].append(np.zeros(len(P)) if mat.startswith("Line") else
                  ((np.asarray(oline(P)) > 0.5).astype(float) if oline else np.ones(len(P))))
    if scale is not None:
        piv, k = scale
        P = np.array(piv) + (P - np.array(piv)) * k
    o["P"].append(P)
    o["F"] += [tuple(i + off for i in f) for f in F]
    o["M"] += [o["mats"].index(mat)] * len(F)
    o["UV"].append(uv)
    o["W"].append((len(P), W))


def const(bone):
    return lambda P: {bone: np.ones(len(P))}


def limb(a_pt, axis, rows, p=2.2, n=16, cap0=0.5, cap1=0.5, u=None, v=None, p_front=None):
    """Rings along axis from a_pt. rows: (t, hu, hv_front, hv_back[, cv[, cu]]) -> loft arrays."""
    a, uu, vv = L.frame(axis)
    if u is not None:
        uu = Vector(u)
        uu = (uu - a * uu.dot(a)).normalized()
    if v is not None:
        vv = Vector(v)
        vv = (vv - a * vv.dot(a) - uu * vv.dot(uu)).normalized()
    rings = []
    for r in rows:
        t, hu, hf, hb = r[:4]
        cv = r[4] if len(r) > 4 else 0.0
        cu = r[5] if len(r) > 5 else 0.0
        c = Vector(a_pt) + a * t + vv * cv + uu * cu
        rings.append(L.ring(c, uu, vv, hu, hf, p, n, hv_neg=hb, p_front=p_front))
    return L.loft(rings, cap0, cap1)


sm = L.smooth
HEAD_PIV = (0, -0.03, 1.43)
HEADSCALE = (HEAD_PIV, HEAD) if HEAD != 1.0 else None

# ---- torso (shirt)
TORSO = [(0.985, 0.116, 0.086, 0.086), (1.015, 0.118, 0.088, 0.086), (1.06, 0.121, 0.09, 0.086),
         (1.12, 0.131, 0.097, 0.09), (1.20, 0.146, 0.104, 0.096), (1.27, 0.157, 0.104, 0.096),
         (1.31, 0.152, 0.095, 0.09), (1.34, 0.136, 0.086, 0.086), (1.37, 0.115, 0.073, 0.077),
         (1.392, 0.092, 0.06, 0.064), (1.405, 0.064, 0.05, 0.054)]          # V taper: lean waist, sloping shoulders


def w_torso(P):
    x, z = P[:, 0], P[:, 2]
    hips = 1 - sm(0.99, 1.07, z)
    chest = sm(1.12, 1.22, z)
    spine = 1 - hips - chest
    sh = sm(0.09, 0.17, np.abs(x)) * sm(1.20, 1.31, z) * 0.6
    # the armpit: the torso's sides follow the upper arm a little, so a raised arm stretches a shoulder/armpit shape
    ua = sm(0.105, 0.15, np.abs(x)) * sm(1.15, 1.22, z) * (1 - sm(1.29, 1.33, z)) * 0.3
    k = 1 - ua
    W = {"Hips": hips * (1 - sh) * k, "Spine": spine * (1 - sh) * k, "Chest": chest * (1 - sh) * k,
         "RightShoulder": np.where(x < 0, sh * k, 0), "LeftShoulder": np.where(x >= 0, sh * k, 0),
         "RightUpperArm": np.where(x < 0, ua, 0), "LeftUpperArm": np.where(x >= 0, ua, 0)}
    return W


add("Shirt", "Shirt", *limb((0, 0, 0), (0, 0, 1), TORSO, p=2.5, n=20, cap0=0.2, cap1=0.2), w_torso,
    mask=lambda P: (P[:, 2] > 1.012) & (P[:, 2] < 1.04))                  # the shirt bloused over the belt
COLLAR = [(1.372, 0.072, 0.066, 0.064, 0.004), (1.39, 0.075, 0.069, 0.066, 0.008), (1.412, 0.068, 0.062, 0.06, 0.012),
          (1.418, 0.062, 0.056, 0.055, 0.012)]
add("Shirt", "Shirt", *limb((0, 0, 0), (0, 0, 1), COLLAR, p=2.2, n=16, cap0=0.0, cap1=0.0),
    lambda P: {"Chest": np.full(len(P), 0.75), "Neck": np.full(len(P), 0.25)})


def front_at(z):
    """How far forward the shirt (torso or collar) reaches at height z."""
    tz = [r[0] for r in TORSO]
    tf = [r[2] + (r[4] if len(r) > 4 else 0) for r in TORSO]
    cz = [r[0] for r in COLLAR]
    cf = [r[2] + r[4] for r in COLLAR]
    f = np.interp(z, tz, tf)
    if cz[0] <= z <= cz[-1]:
        f = max(f, np.interp(z, cz, cf))
    return f


# the open V-neck: a thin skin triangle lying on the shirt front, collar down to mid chest
VNECK = [(z, 0.002 + 0.032 * (z - 1.33) / 0.075, 0.0016, 0.0016, front_at(z) + 0.003)
         for z in np.linspace(1.33, 1.405, 7)]
add("Shirt", "Skin", *limb((0, 0, 0), (0, 0, 1), VNECK, p=2.0, n=8, cap0=0.0, cap1=0.0, u=(1, 0, 0), v=(0, -1, 0)),
    lambda P: {"Chest": np.full(len(P), 0.85), "Neck": np.full(len(P), 0.15)}, subdiv=1)

# ---- neck + head (skin, face texture), ears, nose
NECK = [(1.36, 0.05, 0.045, 0.045, 0.0), (1.40, 0.048, 0.044, 0.044, 0.012), (1.44, 0.047, 0.043, 0.043, 0.025),
        (1.47, 0.045, 0.04, 0.04, 0.03)]


def w_neck(P):
    z = P[:, 2]
    nk = sm(1.36, 1.40, z)
    hd = sm(1.43, 1.47, z)
    return {"Chest": 1 - nk, "Neck": nk - hd, "Head": hd}


add("Head", "Skin", *limb((0, 0, 0), (0, 0, 1), NECK, p=2.0, n=12, cap0=0.2, cap1=0.2), w_neck, scale=None,
    mask=lambda P: np.ones(len(P)))                                        # the neck, under the chin
HEADR = [(1.432, 0.03, 0.024, 0.025, 0.11), (1.445, 0.047, 0.035, 0.06, 0.095), (1.47, 0.061, 0.046, 0.085, 0.08),
         (1.50, 0.075, 0.06, 0.10, 0.07), (1.535, 0.084, 0.08, 0.11, 0.06), (1.57, 0.087, 0.088, 0.115, 0.055),
         (1.61, 0.085, 0.085, 0.113, 0.05), (1.65, 0.075, 0.072, 0.10, 0.045), (1.685, 0.052, 0.05, 0.07, 0.045),
         (1.705, 0.025, 0.024, 0.034, 0.045)]
FX0, FX1 = (462 - 512) * S, (562 - 512) * S          # FACE_BOX of make_face.py in metres
FZ0, FZ1 = (968 - 228) * S, (968 - 128) * S


FACE_R0 = 0.09                                       # cylinder radius of the face mapping (m)
FACE_AXIS_Y = -0.05                                  # the head's vertical axis (y) for the face mapping


def uv_face(P):
    """Cylindrical (angle-based) mapping around the head's vertical axis, u = R0 * angle from the front: near the
    centre it equals the front projection, and toward the sides the eyes keep their size instead of smearing, so the
    near eye is still there in profile."""
    ang = np.arctan2(P[:, 0], -(P[:, 1] - FACE_AXIS_Y))
    return np.stack([(FACE_R0 * ang - FX0) / (FX1 - FX0), (P[:, 2] - FZ0) / (FZ1 - FZ0)], 1)


add("Head", "Face", *limb((0, 0, 0), (0, 0, 1), HEADR, p=2.2, n=24, cap0=0.3, cap1=0.4, p_front=2.5), const("Head"),
    uvfn=uv_face, scale=HEADSCALE,
    mask=lambda P: (P[:, 2] > 1.60) | ((P[:, 1] > 0.0) & (P[:, 2] < 1.52)))   # under the fringe; the nape
for side, s in SIDES.items():
    ear = [(-0.012, 0.012, 0.01, 0.01), (0.0, 0.024, 0.016, 0.016), (0.012, 0.027, 0.018, 0.018),
           (0.02, 0.018, 0.012, 0.012)]
    add("Head", "Skin", *limb((s * 0.078, 0.005, 1.545), (s, 0, 0), ear, p=2.0, n=12, u=(0, 0, 1), v=(0, -1, 0)),
        const("Head"), scale=HEADSCALE)
nose = [(0.0, 0.012, 0.014, 0.012), (0.014, 0.011, 0.011, 0.01), (0.024, 0.006, 0.006, 0.006)]
add("Head", "Skin", *limb((0, -0.128, 1.525), (0, -1, 0), nose, p=2.0, n=10, u=(1, 0, 0), v=(0, 0, 1)),
    const("Head"), scale=HEADSCALE)

# ---- hair: a cap squashed flat under the hairline plane, plus big flattened spikes
HC = Vector((0, -0.05, 1.575))                       # head-local origin; local F = forward (-Y world)


def hl(x, f, z):
    return Vector((x, HC.y - f, HC.z + z))


def hair_cap():
    rows = []
    for k in range(11):
        th = math.pi * (k / 10)
        zz = -math.cos(th) * 0.13
        r = math.sin(th)
        rows.append((zz, max(r, 0.08) * 0.108, max(r, 0.08) * 0.099, max(r, 0.08) * 0.135))
    verts, faces = limb(hl(0, -0.005, 0.025), (0, 0, 1), rows[1:-1], p=2.2, n=20, cap0=0.4, cap1=0.4)
    out = []
    for v in verts:
        v = Vector(v)
        f, z = -(v.y - HC.y), v.z - HC.z
        plane = 0.035 + 0.64 * (f - 0.08)                # hairline: forehead high, nape low
        if z < plane:
            c = Vector((0, HC.y + 0.01, v.z))
            v = Vector((v.x * 0.93, c.y + (v.y - c.y) * 0.93, HC.z + plane))
        out.append(tuple(v))
    return out, faces


add("Hair", "Hair", *hair_cap(), const("Head"), scale=HEADSCALE)

def sph(phi, el, k=1.0):
    """A point on the hair cap (head-local x, f, z) at azimuth phi (0 = forward, 90 = his left) and elevation el."""
    p, e = math.radians(phi), math.radians(el)
    rf = 0.099 if math.cos(p) > 0 else 0.135
    return (k * 0.108 * math.sin(p) * math.cos(e), k * rf * math.cos(p) * math.cos(e), 0.025 + k * 0.13 * math.sin(e))


def outward(phi, el, up=0.0, back=0.0, down=0.0):
    p, e = math.radians(phi), math.radians(el)
    n = Vector((math.sin(p) * math.cos(e), math.cos(p) * math.cos(e), math.sin(e)))
    return tuple(n + Vector((0, -back, up - down)))


SPIKES = []  # base (x, f, z) head-local, direction (x, forward, up), length, half width, half thickness, bend
# crown: locks swept back and out, distinct points (not straight up, or he wears a crown)
def lying(phi, el, out=0.45, back=1.0, side=0.0):
    """A lock lying back along the skull: mostly the surface tangent toward the back, a little outward."""
    p, e = math.radians(phi), math.radians(el)
    n = Vector((math.sin(p) * math.cos(e), math.cos(p) * math.cos(e), math.sin(e)))
    bk = Vector((0, -1, 0))
    bk = (bk - n * bk.dot(n)).normalized() if abs(bk.dot(n)) < 0.98 else Vector((0, 0, -1))
    sd = Vector((math.copysign(1, math.sin(p) or 1), 0, 0))
    return tuple(n * out + bk * back + sd * side)


# crown: short locks lying back, their tips lifting a little: a jagged mass, not a crown
for phi, el, ln in ((0, 70, 0.075), (50, 64, 0.07), (-50, 64, 0.072), (120, 66, 0.065), (-120, 66, 0.065),
                    (180, 72, 0.06), (25, 50, 0.07), (-25, 50, 0.07)):
    SPIKES.append((sph(phi, el, 0.9), lying(phi, el, 0.2, 1.0), ln, 0.042, 0.011, 0.15))
# upper ring: outward and back, tapering points all round the silhouette
for phi, ln in ((40, 0.065), (-40, 0.068), (80, 0.062), (-80, 0.066), (115, 0.06), (-115, 0.06), (150, 0.06),
                (-150, 0.06), (180, 0.058)):
    SPIKES.append((sph(phi, 35, 0.94), lying(phi, 35, 0.45, 0.9, 0.25), ln * 0.9, 0.036, 0.01, 0.12))
# back and lower sides: locks falling out and down
for phi, ln in ((125, 0.055), (-125, 0.055), (160, 0.06), (-160, 0.06), (95, 0.05), (-95, 0.05)):
    SPIKES.append((sph(phi, 8, 0.97), outward(phi, 8, down=0.6, back=0.3), ln, 0.03, 0.01, -0.15))
# fringe: bangs over the band onto the forehead
for x, ln, tilt in ((-0.04, 0.05, -0.3), (0.0, 0.055, 0.0), (0.04, 0.045, 0.3)):
    SPIKES.append(((x, 0.085, 0.085), (tilt, 0.55, -1.0), ln, 0.017, 0.007, 0.25))
# temple locks over the band down to the cheek, sideburns behind them
for s in (-1, 1):
    SPIKES.append(((s * 0.09, 0.045, 0.045), (s * 0.15, 0.05, -1), 0.06, 0.02, 0.008, 0.15))
    SPIKES.append(((s * 0.099, 0.0, 0.02), (s * 0.2, -0.15, -1), 0.07, 0.024, 0.009, 0.1))


def spike(base, dirn, ln, wb, tb, bend=0.0):
    """A flat tapered lock: width across the head surface, thin along the head normal, curling by `bend` (outward)."""
    B = hl(*base)
    D = Vector((dirn[0], -dirn[1], dirn[2])).normalized()       # (x, f, z) -> world
    nrm = (B - HC).normalized()
    nrm = (nrm - D * nrm.dot(D)).normalized()                    # thickness: the head's outward normal
    wdir = D.cross(nrm).normalized()
    prof = ((-0.03, 0.85, 0.9), (0.0, 1.0, 1.0), (0.3, 0.85, 0.85), (0.6, 0.5, 0.6), (0.85, 0.18, 0.3), (1.0, 0.02, 0.1))
    rings = []
    for t, kw, kt in prof:
        c = B + D * (t * ln) + nrm * (bend * ln * max(t, 0) ** 2)
        rings.append(L.ring(c, wdir, nrm, wb * kw, tb * kt, 2.0, 6))
    return L.loft(rings, 0.3, 0.0)


for sp in SPIKES:
    add("Hair", "Hair", *spike(*sp), const("Head"), scale=HEADSCALE, subdiv=1)

# ---- headband (a closed strip around the head), knot, two long flat ribbon tails with 3 bones each


def band():
    N, M = 40, 8
    verts, faces = [], []
    for i in range(N):
        ph = 2 * math.pi * i / N
        sn = math.sin(ph)
        x = 0.106 * math.cos(ph)
        f = sn * (0.096 if sn > 0 else 0.132)
        z = 0.03 + 0.022 * sn
        c = hl(x, f, z)
        out = Vector((math.cos(ph) / 0.106, -sn / (0.096 if sn > 0 else 0.132), 0)).normalized()
        up = Vector((0, 0, 1))
        for k in range(M):
            th = 2 * math.pi * k / M
            cs, s_ = math.cos(th), math.sin(th)
            a = math.copysign(abs(cs) ** 0.6, cs)
            b = math.copysign(abs(s_) ** 0.6, s_)
            verts.append(tuple(c + out * (0.007 * a) + up * (0.021 * b)))
    for i in range(N):
        for k in range(M):
            a, b = i * M + k, i * M + (k + 1) % M
            c_, d_ = ((i + 1) % N) * M + (k + 1) % M, ((i + 1) % N) * M + k
            faces.append((a, b, c_, d_))
    return verts, faces


add("Headband", "Headband", *band(), const("Head"), scale=HEADSCALE, subdiv=1)
KNOT = hl(-0.085, -0.09, 0.012)
knot = [(-0.016, 0.012, 0.012, 0.012), (0.0, 0.019, 0.017, 0.017), (0.016, 0.012, 0.012, 0.012)]
add("Headband", "Headband", *limb(KNOT, (-0.7, 0.7, 0), knot, p=2.0, n=10), const("Head"), scale=HEADSCALE, subdiv=1)


def ribbon_path(start, dirn, ln, wave, droop, K=13):
    """Centre line of a ribbon tail: straight out along dirn, drooping and with a soft S wave sideways."""
    D = Vector(dirn).normalized()
    side = D.cross(Vector((0, 0, 1))).normalized()
    pts = []
    for k in range(K):
        t = k / (K - 1)
        pts.append(Vector(start) + D * (ln * t) + Vector((0, 0, -droop * ln * t * t))
                   + side * (wave * ln * math.sin(math.pi * 1.5 * t) * t))
    return pts


def ribbon(pts, w0, w1, th=0.0035):
    """A long flat ribbon along pts: wide face hanging vertical (seen from the side), forked V end."""
    rings = []
    n_ = len(pts)
    for k, c in enumerate(pts):
        tan = (pts[min(k + 1, n_ - 1)] - pts[max(k - 1, 0)]).normalized()
        wd = Vector((0, 0, 1))
        wd = (wd - tan * wd.dot(tan)).normalized()
        td = tan.cross(wd).normalized()
        t = k / (n_ - 1)
        w = w0 + (w1 - w0) * min(t * 1.3, 1)
        rings.append(L.ring(c, wd, td, w, th, 3.0, 8))
    verts, faces = L.loft(rings, 0.2, 0.0)
    # forked end: pull the middle of the last ring back toward the ribbon
    last = rings[-1]
    c = sum(last, Vector()) / len(last)
    tan = (pts[-1] - pts[-2]).normalized()
    out = []
    for i, v in enumerate(verts):
        v = Vector(v)
        if len(rings[0]) * (n_ - 1) <= i < len(rings[0]) * n_:
            wd = (Vector((0, 0, 1)) - tan * tan.dot(Vector((0, 0, 1)))).normalized()
            off = abs((v - c).dot(wd)) / max(w1, 1e-4)
            v = v - tan * (0.035 * (1 - off))
        out.append(tuple(v))
    pole = len(verts) - 1
    out[pole] = tuple(Vector(out[pole]) - tan * 0.04)
    return out, faces


TAILS = {"A": dict(dirn=(-0.32, 1.0, -0.28), ln=0.23, wave=0.08, droop=0.35, w0=0.012, w1=0.026),
         "B": dict(dirn=(-0.6, 0.85, -0.55), ln=0.19, wave=-0.07, droop=0.3, w0=0.011, w1=0.024)}
TAIL_PTS = {}
for nm, tdef in TAILS.items():
    pts = ribbon_path(KNOT, tdef["dirn"], tdef["ln"], tdef["wave"], tdef["droop"])
    TAIL_PTS[nm] = [Vector(HEAD_PIV) + (p - Vector(HEAD_PIV)) * HEAD for p in pts]   # with the head scale, for bones
    seg = [0, 4, 8, 12]                                   # 3 bones: knot..pts[4], ..pts[8], ..pts[12]
    cum = np.cumsum([0] + [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)])
    b1, b2 = cum[4], cum[8]

    def w_tail(P, pts=pts, cum=cum, b1=b1, b2=b2, nm=nm):
        # arc length of each vertex = that of the nearest centre-line point
        C = np.array([tuple(p) for p in pts])
        idx = np.argmin(((P[:, None, :] - C[None]) ** 2).sum(-1), 1)
        s = cum[idx]
        a = sm(b1 - 0.02, b1 + 0.02, s)
        b = sm(b2 - 0.02, b2 + 0.02, s)
        h = 1 - sm(0.0, 0.025, s)
        return {"Head": h, f"Band{nm}_1": (1 - h) * (1 - a), f"Band{nm}_2": a * (1 - b), f"Band{nm}_3": b}

    add("Headband", "Headband", *ribbon(pts, tdef["w0"], tdef["w1"]), w_tail, scale=HEADSCALE, subdiv=1,
        mask=None)

# ---- arms: sleeve (shirt), forearm (skin), fist + thumb (skin)
SLEEVE = [(-0.07, 0.042, 0.045, 0.045), (-0.025, 0.056, 0.058, 0.058), (0.04, 0.063, 0.061, 0.061), (0.12, 0.058, 0.057, 0.057),
          (0.18, 0.058, 0.057, 0.057), (0.192, 0.064, 0.062, 0.062), (0.202, 0.069, 0.067, 0.067),
          (0.222, 0.069, 0.067, 0.067), (0.231, 0.064, 0.062, 0.062), (0.24, 0.07, 0.068, 0.068),
          (0.262, 0.07, 0.068, 0.068), (0.274, 0.063, 0.061, 0.061), (0.28, 0.048, 0.047, 0.047)]   # rolled cuff
FORE = [(0.15, 0.04, 0.038, 0.038), (0.27, 0.042, 0.04, 0.04), (0.31, 0.043, 0.04, 0.04),
        (0.36, 0.039, 0.036, 0.036), (0.415, 0.031, 0.028, 0.028), (0.45, 0.03, 0.027, 0.027)]
FISTR = [(0.42, 0.03, 0.034, 0.034, 0, 0), (0.44, 0.04, 0.05, 0.05, 0, -0.003), (0.465, 0.047, 0.058, 0.058, 0, -0.006),
         (0.50, 0.048, 0.06, 0.06, 0, -0.009), (0.525, 0.044, 0.056, 0.056, 0, -0.011),
         (0.542, 0.034, 0.044, 0.044, 0, -0.012)]   # (t, back-of-hand half, knuckle-line half, .., 0, palm-side shift)
for side, s in SIDES.items():
    sho, d, n = Vector(J[side + "Sho"]), Vector(J[side + "Dir"]), Vector(J[side + "N"])
    sh, up, lo, ha = side + "Shoulder", side + "UpperArm", side + "LowerArm", side + "Hand"

    def w_sleeve(P, sho=sho, d=d, sh=sh, up=up, lo=lo):
        t = L.along(P, sho, sho + d)
        u = sm(-0.05, 0.03, t)
        c = sm(0.235, 0.285, t) * 0.45               # the rolled cuff follows the forearm part way: no skin gap
        return {sh: 1 - u, up: u * (1 - c), lo: c}

    def w_fore(P, sho=sho, d=d, up=up, lo=lo, ha=ha):
        t = L.along(P, sho, sho + d)
        low = sm(0.245, 0.295, t)
        hand = sm(0.41, 0.44, t)
        return {up: 1 - low, lo: low * (1 - hand), ha: low * hand}

    def w_fist(P, sho=sho, d=d, lo=lo, ha=ha):
        t = L.along(P, sho, sho + d)
        hand = sm(0.40, 0.44, t)
        return {lo: 1 - hand, ha: hand}

    add("Shirt", "Shirt", *limb(sho, d, SLEEVE, p=2.0, n=16, cap0=0.3, cap1=0.15, u=n, v=FWD), w_sleeve)
    add("Arms", "Skin", *limb(sho, d, FORE, p=2.0, n=12, cap0=0.3, cap1=0.3, u=n, v=FWD), w_fore,
        mask=lambda P, sho=sho, d=d: L.along(P, sho, sho + d) < 0.302)    # inside the rolled cuff
    wri = sho + d * 0.42
    fscale = (tuple(wri), FIST) if FIST != 1.0 else None
    add("Hands", "Skin", *limb(sho, d, FISTR, p=3.0, n=16, cap0=0.3, cap1=0.35, u=n, v=FWD), w_fist, scale=fscale)
    t0 = sho + d * 0.45 + FWD * 0.05 - n * 0.022
    t1 = sho + d * 0.505 + FWD * 0.032 - n * 0.058
    L_ = (t1 - t0).length
    thumb = [(-0.008, 0.017, 0.016, 0.016), (0.0, 0.02, 0.019, 0.019), (L_ * 0.6, 0.019, 0.018, 0.018),
             (L_, 0.016, 0.015, 0.015)]
    add("Hands", "Skin", *limb(t0, t1 - t0, thumb, p=2.0, n=10, cap0=0.5, cap1=0.6, u=n, v=d), w_fist, scale=fscale)

# ---- inner lines: thin tubes snapped onto the surface of what is built so far (finger lines, shirt folds)
from mathutils.bvhtree import BVHTree  # noqa: E402


def surface_line(obj, pts, r=0.0028, lift=0.0012, n=4):
    """A thin tube through pts (world, rest) snapped onto object `obj`'s current surface, lifted along the normal."""
    o = OBJS[obj]
    P = np.concatenate(o["P"])
    tree = BVHTree.FromPolygons([tuple(p) for p in P], o["F"])
    snapped = []
    for p in pts:
        loc, nrm, _, _ = tree.find_nearest(Vector(p))
        snapped.append((loc + nrm * (lift + r * 0.5), nrm))
    rings = []
    for k, (c, nrm) in enumerate(snapped):
        tan = (snapped[min(k + 1, len(snapped) - 1)][0] - snapped[max(k - 1, 0)][0]).normalized()
        side = tan.cross(nrm).normalized()
        nn = side.cross(tan).normalized()
        rr = r * (0.35 if k in (0, len(snapped) - 1) else 1.0)
        rings.append(L.ring(c, side, nn, rr, rr * 0.6, 2.0, n))
    return L.loft(rings, 0.0, 0.0)


def curve(a, b, bulge=(0, 0, 0), k=7):
    a, b, bu = Vector(a), Vector(b), Vector(bulge)
    return [a + (b - a) * (i / (k - 1)) + bu * math.sin(math.pi * i / (k - 1)) for i in range(k)]


for side, s in SIDES.items():
    sho, d, n = Vector(J[side + "Sho"]), Vector(J[side + "Dir"]), Vector(J[side + "N"])
    wri = sho + d * 0.42
    ha, lo = side + "Hand", side + "LowerArm"
    for b in (-0.021, 0.0, 0.021):                    # three finger lines across the front of the fist
        pts = []
        for i in range(7):
            u = i / 6
            p = sho + d * (0.53 + 0.02 * math.sin(math.pi * u)) + n * (0.03 - 0.065 * u) + FWD * b
            pts.append(wri + (p - wri) * FIST)
        add("Hands", "LineSkin", *surface_line("Hands", pts, r=0.0024), const(ha), subdiv=0)
for a, b, bu in (((0.07, -0.12, 1.19), (0.045, -0.12, 1.045), (0.008, 0, 0)),         # long folds down to the belt
                 ((-0.075, -0.12, 1.2), (-0.05, -0.12, 1.05), (-0.008, 0, 0)),
                 ((-0.07, -0.12, 1.27), (-0.13, -0.1, 1.205), (0, 0, 0.012)),        # chest toward the armpits
                 ((0.07, -0.12, 1.27), (0.128, -0.1, 1.205), (0, 0, 0.012))):
    add("Shirt", "LineShirt", *surface_line("Shirt", curve(a, b, bu, 9), r=0.0024), w_torso, subdiv=0)

# ---- trousers: pelvis + two legs; belt + buckle; boots (shaft + foot)
PELVIS = [(0.79, 0.05, 0.045, 0.045), (0.81, 0.095, 0.072, 0.078), (0.85, 0.127, 0.088, 0.1, -0.005),
          (0.90, 0.13, 0.093, 0.104, -0.005), (0.95, 0.123, 0.091, 0.097), (0.985, 0.116, 0.088, 0.088)]
add("Trousers", "Trousers", *limb((0, 0, 0), (0, 0, 1), PELVIS, p=2.4, n=20, cap0=0.3, cap1=0.2), const("Hips"),
    mask=lambda P: (P[:, 2] > 0.935) | (P[:, 2] < 0.83),                    # under the belt; the crotch
    oline=lambda P: P[:, 2] > 0.845)     # no hull on the pelvis's lower half: it pokes through a lifted thigh
LEG = [(-0.08, 0.074, 0.08, 0.084), (0.0, 0.077, 0.084, 0.088), (0.1, 0.071, 0.078, 0.08), (0.2, 0.063, 0.07, 0.07),
       (0.3, 0.056, 0.062, 0.06), (0.36, 0.055, 0.06, 0.058), (0.395, 0.06, 0.066, 0.064), (0.42, 0.062, 0.068, 0.066),
       (0.44, 0.056, 0.06, 0.058), (0.455, 0.048, 0.05, 0.05), (0.47, 0.04, 0.042, 0.042)]   # bloused, tucked in
SHAFT = [(0.418, 0.058, 0.06, 0.06), (0.423, 0.067, 0.069, 0.069), (0.438, 0.069, 0.071, 0.071),
         (0.462, 0.067, 0.069, 0.069), (0.47, 0.056, 0.058, 0.058), (0.49, 0.054, 0.056, 0.056),
         (0.56, 0.05, 0.053, 0.053), (0.64, 0.045, 0.049, 0.049), (0.71, 0.043, 0.05, 0.049),
         (0.75, 0.041, 0.048, 0.047)]                                                      # folded top
FOOT = [(-0.07, 0.03, 0.028, 0.028, 0.062), (-0.055, 0.042, 0.05, 0.04, 0.064), (-0.02, 0.047, 0.058, 0.048, 0.064),
        (0.03, 0.049, 0.04, 0.048, 0.05), (0.08, 0.05, 0.03, 0.04, 0.04), (0.125, 0.047, 0.026, 0.035, 0.035),
        (0.155, 0.038, 0.021, 0.031, 0.031), (0.17, 0.022, 0.013, 0.025, 0.028)]           # lean boot, low toe
HEEL = [(0.0, 0.038, 0.032, 0.03), (0.008, 0.04, 0.034, 0.032), (0.03, 0.04, 0.034, 0.032), (0.036, 0.036, 0.03, 0.028)]
for side, s in SIDES.items():
    hip, ank = Vector(J[side + "Hip"]), Vector(J[side + "Ank"])
    ul, ll, ft, to = side + "UpperLeg", side + "LowerLeg", side + "Foot", side + "Toes"
    LL = (ank - hip).length
    axis = (ank - hip).normalized()

    def w_leg(P, hip=hip, axis=axis, ul=ul, ll=ll):
        t = L.along(P, hip, hip + axis)
        u = sm(-0.04, 0.06, t)
        k = sm(0.33, 0.43, t)
        return {"Hips": 1 - u, ul: u * (1 - k), ll: u * k}

    def w_shaft(P, hip=hip, axis=axis, ll=ll, ft=ft):
        t = L.along(P, hip, hip + axis)
        f = sm(0.72, 0.78, t)
        return {ll: 1 - f, ft: f}

    def w_foot(P, ank=ank, ll=ll, ft=ft, to=to):
        sf = -(P[:, 1] - ank.y)
        z = P[:, 2]
        toe = sm(0.08, 0.13, sf)
        low = sm(0.10, 0.16, z) * 0.7 * (1 - toe)
        return {ll: low, ft: (1 - toe) * (1 - low), to: toe}

    def m_leg(P, hip=hip, axis=axis, s=s):
        t = L.along(P, hip, hip + axis)
        lat = s * (P[:, 0] - (hip.x + axis.x * t))
        return ((lat < -0.035) & (t < 0.36)) | (P[:, 2] > 0.935) | (t > 0.437)   # inner thigh, belt, the tuck

    def m_shaft(P, hip=hip, axis=axis):
        t = L.along(P, hip, hip + axis)
        return (t > 0.466) & (t < 0.5)                                    # under the folded top

    add("Trousers", "Trousers", *limb(hip, axis, LEG, p=2.2, n=16, cap0=0.3, cap1=0.2), w_leg, mask=m_leg,
        oline=lambda P, hip=hip, axis=axis: L.along(P, hip, hip + axis) > 0.02)   # no hull on the top inside the pelvis
    add("Boots", "Boots", *limb(hip, axis, SHAFT, p=2.2, n=16, cap0=0.1, cap1=0.2), w_shaft, mask=m_shaft)
    add("Boots", "Boots", *limb((ank.x, ank.y, 0), (0, -1, 0), FOOT, p=3.0, n=16, cap0=0.3, cap1=0.4,
                                 u=(1, 0, 0), v=(0, 0, 1)), w_foot)
    add("Boots", "Boots", *limb((ank.x, ank.y + 0.04, 0), (0, 0, 1), HEEL, p=4.0, n=16, cap0=0.1, cap1=0.1),
        const(ft), subdiv=1)                                                           # the heel block
BELT = [(0.963, 0.138, 0.108, 0.108), (0.967, 0.15, 0.119, 0.12), (1.012, 0.15, 0.119, 0.12), (1.016, 0.138, 0.108, 0.108)]
add("Belt", "Belt", *limb((0, 0, 0), (0, 0, 1), BELT, p=2.5, n=24, cap0=0.0, cap1=0.0),
    lambda P: {"Hips": np.full(len(P), 0.7), "Spine": np.full(len(P), 0.3)})
BUCKLE = [(0.0, 0.026, 0.022, 0.022), (0.008, 0.028, 0.024, 0.024), (0.013, 0.022, 0.018, 0.018)]
add("Belt", "Buckle", *limb((0, -0.112, 0.9895), (0, -1, 0), BUCKLE, p=4.0, n=16, cap0=0.0, cap1=0.2, u=(1, 0, 0),
                            v=(0, 0, 1)), lambda P: {"Hips": np.full(len(P), 0.7), "Spine": np.full(len(P), 0.3)})

# ------------------------------------------------------------------ armature
arm_data = bpy.data.armatures.new("HeroRig")
ARM = bpy.data.objects.new("HeroRig", arm_data)
bpy.context.scene.collection.objects.link(ARM)
bpy.context.view_layer.objects.active = ARM
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None, connect=False, deform=True, roll_to=(0, -1, 0)):
    b = eb.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.align_roll(Vector(roll_to))
    if parent:
        b.parent = eb[parent]
        b.use_connect = connect
    b.use_deform = deform
    return b


bone("Hips", J["Hips"], J["Spine"])
bone("Spine", J["Spine"], J["Chest"], "Hips", True)
bone("Chest", J["Chest"], (0, 0, 1.38), "Spine", True)
bone("Neck", J["Neck"], J["Head"], "Chest")
bone("Head", J["Head"], J["HeadTop"], "Neck", True)
for nm, pts in TAIL_PTS.items():                     # headband tails: 3 bones each, for sway later
    for i, (a_, b_) in enumerate(((0, 4), (4, 8), (8, 12))):
        bone(f"Band{nm}_{i + 1}", pts[a_], pts[b_], "Head" if i == 0 else f"Band{nm}_{i}", i > 0, roll_to=(0, 0, 1))
for side, s in SIDES.items():
    bone(side + "Shoulder", J[side + "Clav"], J[side + "Sho"], "Chest")
    bone(side + "UpperArm", J[side + "Sho"], J[side + "Elb"], side + "Shoulder", True)
    bone(side + "LowerArm", J[side + "Elb"], J[side + "Wri"], side + "UpperArm", True)
    bone(side + "Hand", J[side + "Wri"], J[side + "HandEnd"], side + "LowerArm", True)
    bone(side + "UpperLeg", J[side + "Hip"], J[side + "Kne"], "Hips")
    bone(side + "LowerLeg", J[side + "Kne"], J[side + "Ank"], side + "UpperLeg", True)
    bone(side + "Foot", J[side + "Ank"], J[side + "Ball"], side + "LowerLeg", True, roll_to=(0, 0, 1))
    bone(side + "Toes", J[side + "Ball"], J[side + "Toe"], side + "Foot", True, roll_to=(0, 0, 1))
    g = Vector(J[side + "Grip"])
    # socket: head = grip point in the fist, +Y (bone direction) = the blade's way out of the fist (thumb side),
    # +Z = the back of the hand
    # socket_hand_R deforms: the sword mesh is weighted 100% to it
    bone("socket_hand_" + side[0], g, g + FWD * 0.1, side + "Hand", deform=(side == "Right"), roll_to=J[side + "N"])
hc = Vector(HEAD_PIV) + (Vector((0, -0.05, 1.575)) - Vector(HEAD_PIV)) * HEAD     # head centre, with the head scale
bone("socket_head", hc, hc + Vector((0, 0, 0.1)), "Head", deform=False)
bpy.ops.object.mode_set(mode='OBJECT')
BONES = [b.name for b in arm_data.bones]

# ------------------------------------------------------------------ the sword (own object, 100% socket_hand_R)
# Built in the socket's frame: +Y = blade out of the fist, +Z = back of the hand (the blade's flat faces it),
# +X = the edge line (the guard crosses along it). Sizes after art/raw/wpn_training_sword.png (0.9 m overall).
SOCK = arm_data.bones["socket_hand_R"].matrix_local.copy()


def in_socket(verts_faces):
    verts, faces = verts_faces
    return [tuple(SOCK @ Vector(v)) for v in verts], faces


SX, SY, SZ = (1, 0, 0), (0, 1, 0), (0, 0, 1)
BLADE = [(0.07, 0.019, 0.006, 0.006), (0.085, 0.023, 0.0065, 0.0065), (0.40, 0.022, 0.006, 0.006),
         (0.60, 0.021, 0.0055, 0.0055), (0.66, 0.015, 0.004, 0.004), (0.70, 0.003, 0.0015, 0.0015)]
GUARD = [(-0.098, 0.013, 0.011, 0.011), (-0.09, 0.012, 0.01, 0.01), (-0.06, 0.008, 0.009, 0.009), (0.0, 0.009, 0.011, 0.011),
         (0.06, 0.008, 0.009, 0.009), (0.09, 0.012, 0.01, 0.01), (0.098, 0.013, 0.011, 0.011)]
GRIPR = [(-0.088, 0.013, 0.013, 0.013), (-0.06, 0.0145, 0.0145, 0.0145), (0.0, 0.015, 0.015, 0.015),
         (0.05, 0.0135, 0.0135, 0.0135), (0.056, 0.012, 0.012, 0.012)]
POMMEL = [(-0.122, 0.011, 0.011, 0.011), (-0.116, 0.021, 0.021, 0.021), (-0.098, 0.021, 0.021, 0.021),
          (-0.09, 0.012, 0.012, 0.012)]
SW = const("socket_hand_R")
add("Sword", "Steel", *in_socket(limb((0, 0, 0), SY, BLADE, p=1.25, n=8, cap0=0.0, cap1=0.2, u=SX, v=SZ)), SW, subdiv=1)
add("Sword", "Steel", *in_socket(limb((0, 0.064, 0), SX, GUARD, p=3.0, n=12, cap0=0.2, cap1=0.2, u=SY, v=SZ)), SW,
    subdiv=1)
add("Sword", "Grip", *in_socket(limb((0, 0, 0), SY, GRIPR, p=2.0, n=10, cap0=0.2, cap1=0.2, u=SX, v=SZ)), SW, subdiv=1)
add("Sword", "Steel", *in_socket(limb((0, 0, 0), SY, POMMEL, p=2.0, n=6, cap0=0.3, cap1=0.2, u=SX, v=SZ)), SW, subdiv=0)
for zf in (1, -1):                                    # the fuller: a dark line down the middle of each flat
    add("Sword", "LineSteel", *surface_line("Sword", [SOCK @ Vector((0, y, zf * 0.007)) for y in np.linspace(0.09, 0.56, 6)],
                                            r=0.0026, lift=0.0004), SW, subdiv=0)

# ------------------------------------------------------------------ meshes
for name, o in OBJS.items():
    P = np.concatenate(o["P"])
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(p) for p in P], [], o["F"])
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for m in o["mats"]:
        me.materials.append(MATS[m])
    me.polygons.foreach_set("material_index", o["M"])
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    K = np.concatenate(o["K"])
    G = np.concatenate(o["G"])
    # colour attribute "shade": R = forced shade (0/1), G = outline width factor (0 on inner lines, else 1)
    ca = me.color_attributes.new("shade", 'BYTE_COLOR', 'POINT')
    ca.data.foreach_set("color", np.stack([K, G, K * 0, np.ones_like(K)], 1).ravel())
    og = ob.vertex_groups.new(name="outline")         # the same factor for Blender's hull (solidify vertex group)
    for val in (0.0, 1.0):
        ids = [int(i) for i in np.nonzero(G == val)[0]]
        if ids:
            og.add(ids, val, 'REPLACE')
    uv = np.concatenate(o["UV"])
    if "Face" in o["mats"]:
        lay = me.uv_layers.new(name="UVMap")
        li = np.zeros(len(me.loops), dtype=int)
        me.loops.foreach_get("vertex_index", li)
        lay.data.foreach_set("uv", uv[li].ravel())
    # weights: per-vertex dicts, keep the 3 largest, normalise
    nv = len(P)
    bones_used = sorted({b for _, W in o["W"] for b in W})
    Wm = np.zeros((nv, len(bones_used)))
    off = 0
    for cnt, W in o["W"]:
        for b, w in W.items():
            Wm[off:off + cnt, bones_used.index(b)] += np.clip(w, 0, None)
        off += cnt
    if Wm.shape[1] > 3:                                  # at most 3 bones per vertex
        idx = np.argsort(-Wm, axis=1)[:, 3:]
        np.put_along_axis(Wm, idx, 0, axis=1)
    Wm /= Wm.sum(1, keepdims=True)
    for j, b in enumerate(bones_used):
        assert b in BONES, b
        vg = ob.vertex_groups.new(name=b)
        for i in np.nonzero(Wm[:, j] > 1e-4)[0]:
            vg.add([int(i)], float(Wm[i, j]), 'REPLACE')
    ob.parent = ARM
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = ARM
    mod.use_deform_preserve_volume = False
    print(f"MESH {name}: {nv} verts, {len(me.polygons)} faces, materials {o['mats']}")

os.makedirs(OUT, exist_ok=True)
h = max(v.co.z for ob in bpy.data.objects if ob.type == 'MESH' for v in ob.data.vertices)
print(f"HEIGHT {h:.3f} m")

# ------------------------------------------------------------------ export (no outline hull), then the hull + blend
import poses as PZ  # noqa: E402

PZ.init(ARM)
sc = bpy.context.scene
sc.render.fps = 24
sc.frame_start, sc.frame_end = 1, 24
CLIPS = {"Stance": PZ.make_action("Stance", [(1, PZ.pose_stance), (24, PZ.pose_stance)])}
PZ.reset()
PZ.use_action("Stance", 1)
bpy.ops.object.select_all(action='DESELECT')
FBX = dict(use_selection=False, object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=False, mesh_smooth_type='FACE',
           add_leaf_bones=False, use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_bones=True,
           bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
           bake_anim_step=1.0, bake_anim_simplify_factor=0.0, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
           apply_scale_options='FBX_SCALE_ALL', global_scale=1.0, path_mode='COPY', embed_textures=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), **FBX)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, NAME + ".glb"), export_format='GLB', export_skins=True,
                          export_apply=False, export_animations=True, export_animation_mode='ACTIONS')
# The FBX/GLB carry no usable look (the exporters only read Principled BSDF): write it down for the Unity shader.
import json  # noqa: E402
OUTLINE = {"Hair": 1.0, "Headband": 0.8, "Head": 1.0, "Hands": 1.0, "Sword": 0.6}     # per-object hull width factor
RIM = {"colour": "#d2b06a", "width_px_720p": 0.8}       # keep in step with sheets.py RIM
lv = Vector(L.LIGHT_CAM).normalized()
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
look = {
    "version": 2,
    # ---- v1 fields (kept valid)
    "shader": "d = dot(N, light); lit = d > cut and shade_mask < 0.5; colour = lit ? (hi and d > hi_cut ? hi : albedo) "
              ": albedo * shade/base; unlit; albedo = base colour or the face texture",
    "light_dir_blender_world": [round(c, 4) for c in LIGHT],
    "light_note": "fixed relative to the screen (not the model): from the upper right front; dir_view is in camera "
                  "space: x right, y up, z toward the viewer. Blender world value is for the camera looking along +Y.",
    "cut": L.CUT,
    "materials": {**{k: {"base": b, "shade": s, **({"kind": "inner line (flat, base = shade)"} if k.startswith("Line")
                                                    else {})} for k, (b, s) in COL.items()},
                  "Face": {"base": COL["Skin"][0], "shade": COL["Skin"][1], "texture": os.path.basename(FACE),
                           "kind": "albedo = the texture; shaded = texture * shade/base"}},
    "face": {"material": "Face", "texture": os.path.basename(FACE), "colours_as": "Skin",
             "how": "the drawn face as one texture (flat colours on the exact skin), cylindrical mapping around the "
                    f"head's vertical axis (x = 0, y = {FACE_AXIS_Y} m in the rest pose, before the head scale): "
                    f"u = (R0 * atan2(x, -(y - axis_y)) - {FX0:.5f}) / {FX1 - FX0:.5f}, R0 = {FACE_R0}; "
                    f"v = (z - {FZ0:.5f}) / {FZ1 - FZ0:.5f}; baked into the UVMap of object Head (no shader work)."},
    "outline": {"colour": "#2a1a12", "width_m": L.OUTLINE_M,
                "how": "inverted hull: push vertices out along the normal by width_m * object scale * vertex factor, "
                       "draw front faces culled",
                "object_scale": {o.name: OUTLINE.get(o.name, 1.0) for o in meshes},
                "per_vertex_scale": "G channel of the colour attribute 'shade' (0 on inner-line geometry, else 1)"},
    "proportions": {"head": HEAD, "fist": FIST, "note": "head scale about the chin/neck point; fist scale relative "
                    "to round 1's fist tables, about the wrist"},
    "units": "1 unit = 1 m",
    # ---- v2
    "light": {"dir_view": [round(c, 4) for c in lv], "cut": L.CUT, "hi_cut": L.HI_CUT,
              "note": "one light fixed to the screen; hard step on N.L; materials with a 'hi' colour use it above "
                      "hi_cut (none in this version)"},
    "shadow_mask": {"layer": "shade", "kind": "colour attribute per vertex (FBX: vertex colour)", "channel": "R",
                    "threshold": 0.5, "values": "0 or 1 only (immune to sRGB/linear conversion)",
                    "where": "neck, under the fringe and the nape, inside the rolled cuffs, the shirt bloused over "
                             "the belt, the trousers under the belt, the crotch and inner thighs, the trousers tuck, "
                             "under the boots' folded tops"},
    "rim": {"colour": RIM["colour"], "width_px_720p": RIM["width_px_720p"],
            "how": "2D pass on the hero's finished picture: dilate the alpha by width (in 1280x720 pixels, hero 240 px "
                   "tall), fill the new ring with the colour, under the picture; outer silhouette only",
            "measured": "char_hero_bare.png: about 3 px at 1033 px tall (0.7-0.8 px at game size), warm tan "
                        "#b49c5f..#d7b16a; the monsters checked (bramble bandit, bog lurker, ash imp) have none"},
    "parts": {"body": ["Head", "Hair", "Headband", "Shirt", "Arms", "Hands", "Belt", "Trousers", "Boots"],
              "note": "one object per garment; armor may hide any of them"},
    "gear": [{"id": "training_sword", "slot": "weapon", "objects": ["Sword"], "bone": "socket_hand_R",
              "skin": "100% socket_hand_R (a deforming bone)", "hides": [],
              "after": "art/raw/wpn_training_sword.png (0.9 m overall)"}],
    "clips": {"actions": list(CLIPS), "fbx_takes_expected": [f"{ARM.name}|{c}" for c in CLIPS], "fps": 24,
              "Stance": "the battle stance, held: the same key on frames 1 and 24"},
    "game_view": {"yaw": 40, "pitch": 5, "ortho": True, "px_per_m_720p": round(240 / 1.75, 3),
                  "facing": "he faces screen right, chest toward the viewer; his right (the sword hand) is the near side"},
    "bones": BONES,
    "sockets": {"socket_hand_R": "grip point in his right fist; +Y blade out by the thumb, +Z back of the hand",
                "socket_hand_L": "the same for the left fist", "socket_head": "head centre, +Y up"},
    "stats": {"vertices": {o.name: len(o.data.vertices) for o in meshes},
              "vertices_total": sum(len(o.data.vertices) for o in meshes),
              "vertices_body_without_sword": sum(len(o.data.vertices) for o in meshes if o.name != "Sword"),
              "max_bone_influences": 3, "height_m": round(h, 3)},
}
with open(os.path.join(OUT, NAME + "_look.json"), "w") as f:
    json.dump(look, f, indent=1)
for ob in meshes:
    L.add_outline(ob, L.OUTLINE_M)
    ob["outline_scale"] = OUTLINE.get(ob.name, 1.0)
bpy.context.scene["light"] = list(LIGHT)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
print("SAVED", OUT, NAME)
