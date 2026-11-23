"""Round 2: colour clusters on cutj3_s123 (shirt cream lit/shadow/line, skin lit/shadow/line) and on the A3 ready fist."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import cv2
import numpy as np
from PIL import Image
import fists as fistmod
from jcommon import REDO

def km(px, k):
    px = px.astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 0.5)
    _, lab, c = cv2.kmeans(px, k, None, crit, 4, cv2.KMEANS_PP_CENTERS)
    n = np.bincount(lab.ravel(), minlength=k)
    o = np.argsort(-c.sum(1))
    return [(c[i].round().astype(int).tolist(), int(n[i])) for i in o]

s = np.asarray(Image.open(REDO / "cutj3_s123.png").convert("RGB")).astype(np.float32)
R, G, B = s[..., 0], s[..., 1], s[..., 2]
cream = (R > 150) & (G > 120) & (B > 80) & (R - B > 20) & (R - B < 120)
yy, xx = np.mgrid[0:s.shape[0], 0:s.shape[1]]
torso = cream & (yy > 480) & (yy < 700) & (xx > 690) & (xx < 850)
print("torso cream", km(s[torso], 4))
sleeve = cream & (yy > 560) & (yy < 780) & (xx > 560) & (xx < 680)
print("sleeve cream", km(s[sleeve], 4))
skin = (R > 120) & (G > 60) & (B < 120) & (R - B > 70) & (yy > 790) & (yy < 890) & (xx > 540) & (xx < 660)
print("near fist skin", km(s[skin], 4))
F = fistmod.load_fists()["ready"]["img"]
a = F[..., 3] > 0.9
rgb = F[..., :3][a] / F[..., 3:4][a] * 255
print("A3 ready fist", km(rgb, 4))
tr = (R < 70) & (G > 40) & (B > 40) & (yy > 720) & (yy < 900)
print("trousers", km(s[tr], 3))
