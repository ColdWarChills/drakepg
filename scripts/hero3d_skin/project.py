"""Register a drawing to one projection view (skin_blend.py view) part by part, and project it into the atlas.
<venv python> -I project.py <view dir> <drawing png (RGBA, or on white with --white)> <out layer.npz> [--white]
  [--facing 0.25] [--band 5]

1. The drawing is scaled/moved so its figure height and feet centre match the render's.
2. Every drawing pixel gets a colour class (skin, dark browns, headband, shirt, buckle, trousers, steel) and goes to the
   nearest render part (object id mask) that can carry that class: a part = one garment object.
3. Per part, DIS optical flow between the signed distance fields of the render part and the drawing part gives a dense
   warp (render pixel -> drawing pixel); inner lines inside a part (folds) are kept, the drawing's outline/rim at a
   part's edge and the render's part/depth edges are skipped (a band of --band px).
4. Every atlas texel the view sees (depth-tested against the rendered position pass, facing the camera by more than
   --facing) takes the warped drawing's colour. Out: colour (sRGB u8), ok (painted), seen (visible+facing).
Also writes <out>_warp.png (warped drawing on the render) and prints per-part IoU before/after."""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

a = sys.argv[1:]
V, SRC, OUT = Path(a[0]), Path(a[1]), Path(a[2])
WHITE = "--white" in a
FACING = float(a[a.index("--facing") + 1]) if "--facing" in a else 0.25
BAND = int(a[a.index("--band") + 1]) if "--band" in a else 5
SMOOTH = float(a[a.index("--smooth") + 1]) if "--smooth" in a else 10.0
P = json.loads((V / "proj.json").read_text())
IDS = P["ids"]
NAME = {v: k for k, v in IDS.items()}


def hx(h):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


CLASSES = {  # colour class: (sRGB samples, parts that can carry it)
    "skin": (["#df9d4f", "#b06f2b", "#e8b070", "#c98a45"], ["Head", "Arms", "Hands", "Shirt"]),
    "dark": (["#2e1e0d", "#1a1107", "#2a1a12", "#3a2209", "#201302", "#45290d", "#2b1a06", "#5a3a1a"],
             ["Hair", "Belt", "Boots", "Mail", "Pauldrons"]),
    "band": (["#861a0f", "#4d0e05", "#a52a18"], ["Headband"]),
    "shirt": (["#debe85", "#b79764", "#a2834f", "#efd9a8", "#d2b06a"], ["Shirt"]),
    "buckle": (["#a08048", "#c8a868"], ["Belt"]),
    "trousers": (["#1b312d", "#0e2320", "#26443e"], ["Trousers"]),
    "steel": (["#c3bfb2", "#878377", "#6f6b60", "#9a9a9a", "#5a5a60", "#e0e0e0", "#3c3c42"], ["Mail", "Pauldrons"]),
}

pid = np.load(V / "pix_id.npy")
ppos = np.load(V / "pix_pos.npy")
H, W = pid.shape
ralpha = pid > 0

# ---- 1. global fit of the drawing
src = Image.open(SRC).convert("RGBA")
s = np.asarray(src).astype(np.float32)
if WHITE:                                         # a picture on white: alpha from the distance to white
    d = np.abs(s[..., :3] - 255).max(2)
    s[..., 3] = np.clip((d - 8) * 12, 0, 255)
dal = s[..., 3] > 128
FIT = Path(a[a.index("--fit-from") + 1]) if "--fit-from" in a else None   # same-canvas picture to take the fit from


def fig_box(m):
    ys, xs = np.nonzero(m)
    y0, y1 = ys.min(), ys.max()
    h = y1 - y0 + 1
    low = m[y1 - int(h * 0.04):y1 + 1].any(0)
    fx = np.nonzero(low)[0]
    return y0, y1, (fx.min() + fx.max()) / 2


ry0, ry1, rfx = fig_box(ralpha)
dy0, dy1, dfx = fig_box(np.asarray(Image.open(FIT).convert("RGBA"))[..., 3] > 128 if FIT else dal)
k = (ry1 - ry0) / (dy1 - dy0)
M = np.float32([[k, 0, rfx - dfx * k], [0, k, ry1 - dy1 * k]])
D = cv2.warpAffine(s, M, (W, H), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
Dal = D[..., 3] > 128

# ---- 2. classes -> parts
names, cols = [], []
for c, (hs, _) in CLASSES.items():
    for h in hs:
        names.append(c)
        cols.append(hx(h))
cols = np.array(cols, np.float32)
rgb = D[..., :3][Dal]
cls = np.full((H, W), "", dtype=object)
cls[Dal] = np.array(names, dtype=object)[np.argmin(((rgb[:, None, :] - cols[None]) ** 2).sum(-1), 1)]
parts = sorted(set(int(v) for v in np.unique(pid)) - {0})
pdist = {p: ndi.distance_transform_edt(pid != p) for p in parts}
assign = np.zeros((H, W), np.int32)
best = np.full((H, W), np.inf)
for c, (_, cand) in CLASSES.items():
    sel = Dal & (cls == c)
    for nm in cand:
        p = IDS.get(nm)
        if p not in pdist:
            continue
        dd = np.where(sel, pdist[p], np.inf)
        better = dd < best
        assign[better] = p
        best[better] = dd[better]
assign[best > 90] = 0
# features enclosed by a part (eyes, brows and mouth in the face, folds in the shirt) belong to that part
for p in sorted(parts, key=lambda q: (assign == q).sum()):
    m = assign == p
    if m.sum() < 50:
        continue
    filled = ndi.binary_fill_holes(ndi.binary_closing(m, iterations=3)) & Dal
    assign[filled & ~m & (ndi.distance_transform_edt(~m) < 40)] = p

# ---- 3. per-part warp
mapx = np.zeros((H, W), np.float32)
mapy = np.zeros((H, W), np.float32)
dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
dis.setFinestScale(1)
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
CLIP = 90.0


def sdf_img(m):
    sd = ndi.distance_transform_edt(m) - ndi.distance_transform_edt(~m)
    return np.clip((sd + CLIP) / (2 * CLIP) * 255, 0, 255).astype(np.uint8)


dpart_interior = np.zeros((H, W), bool)
report = []
# The head is one group: a similarity transform from the render's head (face+hair+band, no band tails) to the drawn
# one; the face takes it alone (its eyes, brows and mouth have no silhouette to follow), hair and band add a flow.
HEAD = [IDS[n] for n in ("Head", "Hair") if n in IDS]
NOFLOW = {IDS["Head"]}


def head_box(face, band):
    """Anchors: the headband's middle row over the face (y0), the lowest face/neck row (y1), the face's middle x."""
    ys, xs = np.nonzero(face)
    cx = np.median(xs)
    by, bx = np.nonzero(band)
    near = np.abs(bx - cx) < (xs.max() - xs.min()) * 0.5
    return np.median(by[near]), ys.max(), cx


r0, r1, rcx = head_box(pid == IDS["Head"], pid == IDS["Headband"])
d0, d1, dcx = head_box(assign == IDS["Head"], assign == IDS["Headband"])
print(f"HEAD anchors render band {r0:.0f} chin {r1:.0f}  drawing band {d0:.0f} chin {d1:.0f}")
gk = (d1 - d0) / (r1 - r0)                        # render px -> drawing px
GX = lambda x: dcx + (x - rcx) * gk               # noqa: E731
GY = lambda y: d0 + (y - r0) * gk                 # noqa: E731
GIx = (xx - dcx) / gk + rcx                       # drawing coords of the render grid, inverse map for warping
for p in parts:
    R = pid == p
    Dp = assign == p
    if Dp.sum() < 50:
        report.append(f"{NAME[p]}: no drawing pixels")
        continue
    Dp = ndi.binary_closing(Dp, iterations=4) & Dal | Dp
    iou0 = (R & Dp).sum() / (R | Dp).sum()
    grouped = p in HEAD or NAME[p] == "Headband"
    if grouped:                                   # the drawn part moved into render space by the head transform
        Dp = cv2.remap(Dp.astype(np.uint8), GX(xx).astype(np.float32), GY(yy).astype(np.float32),
                       cv2.INTER_NEAREST) > 0
    if p in NOFLOW:
        f = np.zeros((H, W, 2), np.float32)
    else:
        f = dis.calc(sdf_img(R), sdf_img(Dp), None)
        wgt = ndi.gaussian_filter(R.astype(np.float32), SMOOTH) + 1e-6   # smooth inside the part: no wobbly lines
        f = np.stack([ndi.gaussian_filter(f[..., i] * R, SMOOTH) / wgt for i in (0, 1)], -1)
    mx, my = xx + f[..., 0], yy + f[..., 1]
    if grouped:
        mx, my = GX(mx).astype(np.float32), GY(my).astype(np.float32)
        Dp = assign == p
    mapx[R], mapy[R] = mx[R], my[R]
    warped = cv2.remap(Dp.astype(np.uint8), mx, my, cv2.INTER_NEAREST) > 0
    iou1 = (R & warped).sum() / (R | warped).sum()
    inner = ndi.distance_transform_edt(Dal if p in NOFLOW else Dp) > BAND * 0.6   # the face: any drawn pixel
    dpart_interior |= (cv2.remap(inner.astype(np.uint8), mx, my, cv2.INTER_NEAREST) > 0) & R
    report.append(f"{NAME[p]}: IoU fitted {iou0:.2f} -> warped {iou1:.2f}")
print("PARTS", " | ".join(report))
Wimg = cv2.remap(D, mapx, mapy, cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
# render-side band: part edges and depth jumps
edge = np.zeros((H, W), bool)
edge[:, 1:] |= pid[:, 1:] != pid[:, :-1]
edge[1:] |= pid[1:] != pid[:-1]
pp = np.nan_to_num(ppos, nan=99)
jump = np.zeros((H, W), bool)
jump[:, 1:] |= np.linalg.norm(pp[:, 1:] - pp[:, :-1], axis=-1) > 0.02
jump[1:] |= np.linalg.norm(pp[1:] - pp[:-1], axis=-1) > 0.02
far = ndi.distance_transform_edt(~(edge | jump)) > BAND
dal_in = ndi.distance_transform_edt(Dal) > BAND + 1           # the drawing's rim light along its silhouette
dal_w = cv2.remap(dal_in.astype(np.uint8), mapx, mapy, cv2.INTER_NEAREST) > 0
valid = ralpha & far & dpart_interior & dal_w & (Wimg[..., 3] > 200)
print(f"VALID render px {valid.sum()} of {ralpha.sum()} ({valid.sum() / ralpha.sum():.2f})")

dbg = np.asarray(Image.open(V / "flat.png").convert("RGBA")).copy()
dbg[valid] = Wimg[valid].astype(np.uint8)
dbg[ralpha & ~valid, :3] = (dbg[ralpha & ~valid, :3] * 0.4 + np.array([255, 0, 255]) * 0.6).astype(np.uint8)
Image.fromarray(dbg).save(str(OUT.with_suffix("")) + "_warp.png")

# ---- 4. texels
tp = np.load(V / "tex_pos.npy")
tn = np.load(V / "tex_nrm.npy")
tid = np.load(V / "tex_id.npy")
A, b = np.array(P["A"]), np.array(P["b"])
pix = tp @ A.T + b
px, py = pix[..., 0].astype(np.float32), pix[..., 1].astype(np.float32)
ix = np.clip(np.rint(px).astype(int), 0, W - 1)
iy = np.clip(np.rint(py).astype(int), 0, H - 1)
inside = (tid > 0) & (px >= 0) & (px < W) & (py >= 0) & (py < H)
vis = inside & (np.linalg.norm(np.nan_to_num(ppos[iy, ix], nan=99) - tp, axis=-1) < 0.012)
facing = (tn @ np.array(P["view_dir"])) > FACING
seen = vis & facing
col = cv2.remap(Wimg, px, py, cv2.INTER_LINEAR)
ok = seen & valid[iy, ix]
print(f"TEXELS used {tid.astype(bool).sum()}  seen {seen.sum()}  painted {ok.sum()}")
np.savez_compressed(OUT, colour=np.clip(col[..., :3], 0, 255).astype(np.uint8), ok=ok, seen=seen,
                    facing=(tn @ np.array(P["view_dir"])).astype(np.float16))
