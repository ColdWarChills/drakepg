"""Joint positions of an A-pose hero mesh (feet on z 0, facing -Y, height 1.75, right side at -X), measured from its
vertices. Writes joints.json and joints_check.png (front and side projections with the joints).
python landmarks.py <dir with verts.npz>
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

D = Path(sys.argv[1])
V = np.load(D / "verts.npz")["V"]
H = V[:, 2].max()
X, Y, Z = V[:, 0], V[:, 1], V[:, 2]


def slab(z0, z1, cond=None):
    m = (Z >= z0) & (Z < z1)
    if cond is not None:
        m &= cond
    return V[m]


def centroid(pts):
    return pts.mean(0) if len(pts) else None


J = {}
# crotch: lowest z where there is geometry near x = 0 (between the legs it is empty)
crotch = None
for z in np.arange(0.30 * H, 0.65 * H, 0.004):
    s = slab(z, z + 0.006, np.abs(X) < 0.012)
    if len(s) > 5:
        crotch = z
        break
J["crotch_z"] = float(crotch)
for side, sgn in (("R", -1), ("L", 1)):
    leg = (X * sgn) > 0.01
    def legc(z, w=0.012):
        return centroid(slab(z - w, z + w, leg & (np.abs(X) < 0.25 * H)))
    hip = legc(crotch - 0.03 * H)
    ank = legc(0.085 * H)
    knee_z = (crotch + 0.085 * H) / 2 + 0.01 * H
    knee = legc(knee_z)
    J[f"hip.{side}"] = [float(hip[0]), float(hip[1]), float(crotch + 0.045 * H)]
    J[f"knee.{side}"] = knee.tolist()
    J[f"ankle.{side}"] = ank.tolist()
    feet = slab(0, 0.04 * H, leg)
    toe = feet[feet[:, 1].argmin()]
    J[f"toe.{side}"] = [float(ank[0]), float(toe[1]), 0.02]

    # arms: the hand is the farthest-out vertex cluster between 0.35H and 0.75H
    arm_zone = slab(0.35 * H, 0.75 * H, (X * sgn) > 0)
    far = arm_zone[np.argmax(arm_zone[:, 0] * sgn)]
    fist = arm_zone[np.linalg.norm(arm_zone - far, axis=1) < 0.06 * H]
    fist_c = fist.mean(0)
    # armpit: going down from 0.8H, the first z where the outer slice has a gap (arm apart from torso)
    armpit = None
    for z in np.arange(0.71 * H, 0.45 * H, -0.004):
        s = slab(z, z + 0.006, (X * sgn) > 0)
        xs = np.sort(s[:, 0] * sgn)
        if len(xs) < 10:
            continue
        gaps = np.diff(xs)
        i = np.argmax(gaps)
        if gaps[i] > 0.02 * H and xs[i] > 0.05 * H:
            armpit = z
            torso_edge = xs[i]
            break
    sh_z = armpit + 0.045 * H
    top = slab(sh_z - 0.01, sh_z + 0.01, (X * sgn) > 0)
    sh_x = (top[:, 0] * sgn).max() - 0.045 * H
    sh = centroid(slab(sh_z - 0.03, sh_z + 0.03, ((X * sgn) > sh_x - 0.03) & ((X * sgn) < sh_x + 0.03)))
    shoulder = np.array([sh_x * sgn, sh[1], sh_z])
    d = fist_c - shoulder
    wrist = fist_c - d / np.linalg.norm(d) * 0.045 * H
    # elbow: centroid of the arm slab across the shoulder->wrist line at t = 0.52
    u = (wrist - shoulder) / np.linalg.norm(wrist - shoulder)
    t = (V - shoulder) @ u
    L = np.linalg.norm(wrist - shoulder)
    near = (np.abs(t - 0.52 * L) < 0.015) & (np.linalg.norm((V - shoulder) - np.outer(t, u), axis=1) < 0.06 * H)
    elbow = V[near].mean(0)
    J[f"shoulder.{side}"] = shoulder.tolist()
    J[f"elbow.{side}"] = elbow.tolist()
    J[f"wrist.{side}"] = wrist.tolist()
    J[f"fist.{side}"] = fist_c.tolist()
    J[f"armpit_z.{side}"] = float(armpit)

shz = (J["shoulder.R"][2] + J["shoulder.L"][2]) / 2
def body_y(z):
    q = slab(z - 0.02, z + 0.02, np.abs(X) < 0.10)[:, 1]
    return float((np.percentile(q, 3) + np.percentile(q, 97)) / 2)
pz = crotch + 0.05 * H
nz, hz = shz + 0.035 * H, shz + 0.085 * H
J["pelvis"] = [0.0, float(body_y(pz)), float(pz)]
J["spine"] = [0.0, float(body_y(pz + 0.30 * (nz - pz))), float(pz + 0.30 * (nz - pz))]
J["chest"] = [0.0, float(body_y(pz + 0.62 * (nz - pz))), float(pz + 0.62 * (nz - pz))]
J["neck"] = [0.0, float(body_y(nz)), float(nz)]
J["head"] = [0.0, float(body_y(hz)), float(hz)]
J["head_top"] = [0.0, float(body_y(shz + 0.07 * H)), float(H)]
json.dump(J, open(D / "joints.json", "w"), indent=1)

# check image: front (x,z) and side (y,z)
S, W = 400, 820
im = Image.new("RGB", (W, S + 20), (240, 240, 235))
dr = ImageDraw.Draw(im)
sel = V[np.random.default_rng(0).choice(len(V), min(len(V), 40000), replace=False)]
def px(a, b, ox):
    return ox + S / 2 + a / H * S * 0.95, S + 10 - b / H * S * 0.95
for p in sel:
    dr.point(px(p[0], p[2], 0), (150, 150, 150))
    dr.point(px(p[1], p[2], 410), (150, 150, 150))
for k, v in J.items():
    if not isinstance(v, list):
        continue
    for (a, b, ox) in ((v[0], v[2], 0), (v[1], v[2], 410)):
        x, y = px(a, b, ox)
        col = (220, 30, 30) if ".R" in k else (30, 60, 220) if ".L" in k else (20, 150, 20)
        dr.ellipse((x - 3, y - 3, x + 3, y + 3), fill=col)
im.save(D / "joints_check.png")
print(json.dumps({k: [round(x, 3) for x in v] if isinstance(v, list) else round(v, 3) for k, v in J.items()}))
