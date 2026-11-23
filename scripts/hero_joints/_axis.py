import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np, json
from PIL import Image
from jcommon import REDO, ROOT
sys.path.insert(0, str(ROOT / "scripts" / "hero2d")); import cutlib
src = np.asarray(Image.open(REDO / "cutj3_s123.png").convert("RGB")).astype(np.float32)
a = np.clip((np.abs(src - cutlib.PURPLE).max(-1) - 40) / 50, 0, 1)
R,G,B = src[...,0],src[...,1],src[...,2]
skin = (R > 160) & (G > 95) & (G < 185) & (B < 120) & (R - B > 85)
fig = (a > 0.5) & ~skin
S = np.array([652., 540.]); W = np.array([601., 795.])
d = (W - S) / np.linalg.norm(W - S); n = np.array([-d[1], d[0]])
for uu in range(0, 271, 15):
    c = S + d * uu
    l0, l1, mid = cutlib.section(fig, c, d)
    print(uu, l0, l1, "width", l1 - l0, "centre off", (l0 + l1) / 2, "mid", np.round(mid, 1))
