"""Check 2 (elbow): each kind at bends 0, 45, 90 and 130 degrees, the sleeve alone, at 3x of game size (the drawing is
2.14x game size at 1440p, so 1.4x of the drawing), plus numbers: the sleeve's width across the centre line through
the bend (min and max against the straight width), flipped mesh triangles, and holes (background inside the sleeve).
<venv python> -I -B elbows.py [out png]"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

import rig
from jcommon import TESTS, fit
from meshlib import vec

out = sys.argv[1] if len(sys.argv) > 1 else "elbows_3x.png"
parts = rig.Parts()
fr = rig.Frame(parts, window=(0, 0, 700, 600))
BENDS = [0, 45, 90, 130]
GAME = 480 / 1028  # the hero is 480 px tall in the game at 1440p
Z = 3 * GAME
BG = np.array([0.80, 0.80, 0.82], np.float32)


def widths(alpha, S, kind, a1, b):
    """Width of the sleeve's alpha (> 0.5) across the centre line at u steps through the elbow region."""
    us = np.arange(rig.UE - 70, rig.UE + 71, 4.0)
    pts, phi = rig.centre(kind, us, a1, b)
    res = []
    m = alpha > 0.5
    for p, f in zip(pts, phi):
        c = S + p
        nv = vec(f + 90)
        w = 0
        for sgn in (1, -1):
            r = 0
            while r < 120:
                x, y = np.round(c + sgn * nv * (r + 1)).astype(int)
                if not (0 <= y < m.shape[0] and 0 <= x < m.shape[1]) or not m[y, x]:
                    break
                r += 1
            w += r
        res.append(w + 1)
    return np.array(res)


rows = []
nums = {}
straight_w = None
for kind in ("A", "B", "C"):
    tiles = []
    for b in BENDS:
        a1 = 90.0 - 0  # upper arm straight down; the forearm folds toward his front (screen right)
        pose = dict(a1=a1, b=float(b), lean=0, head=0, far=0, fist=None, stretch=1.0)
        S = np.array([300.0, 130.0]) - (rig.S0 - np.array([0, 0]))  # shoulder lands at (300,130) in the window
        fr.ox, fr.oy = -int(S[0]), -int(S[1])
        px, info = fr.render(kind, pose, rim=False)
        Sw = info["S"]
        ws = widths(px[..., 3], Sw, kind, a1, float(b))
        if straight_w is None:
            straight_w = float(np.median(widths(px[..., 3], Sw, kind, a1, 0.0)[:5]))
        # holes: background pixels enclosed by the sleeve
        m = (px[..., 3] > 0.5).astype(np.uint8)
        ff = m.copy()
        cv2.floodFill(ff, np.zeros((m.shape[0] + 2, m.shape[1] + 2), np.uint8), (0, 0), 1)
        holes = int((ff == 0).sum())
        nums[f"{kind}{b}"] = {"w_min": round(float(ws.min() / straight_w), 3), "w_max": round(float(ws.max() / straight_w), 3),
                             "flipped": info["flipped"], "holes_px": holes}
        el = Sw + vec(a1, rig.UE)
        im = rig.to_image(px, BG)
        c = 150
        crop = im.crop((int(el[0] - c), int(el[1] - c), int(el[0] + c), int(el[1] + c)))
        crop = crop.resize((round(crop.width * Z), round(crop.height * Z)), Image.LANCZOS)
        d = ImageDraw.Draw(crop)
        n = nums[f"{kind}{b}"]
        d.text((4, 4), f"{rig.KINDS[kind]} {b} deg", fill=(0, 0, 0))
        d.text((4, crop.height - 14), f"width {n['w_min']:.2f}-{n['w_max']:.2f}  flips {n['flipped']}  holes {holes}",
               fill=(0, 0, 0))
        tiles.append(crop)
    rows.append(tiles)
tw, th = rows[0][0].size
sheet = Image.new("RGB", (4 * (tw + 6), 3 * (th + 6)), (255, 255, 255))
for r, tiles in enumerate(rows):
    for c, t in enumerate(tiles):
        sheet.paste(t, (c * (tw + 6), r * (th + 6)))
fit(sheet).save(TESTS / out)
print(json.dumps(nums), "straight width px", straight_w)
(TESTS / "_elbow_numbers.json").write_text(json.dumps({"straight_w": straight_w, **nums}, indent=1))
