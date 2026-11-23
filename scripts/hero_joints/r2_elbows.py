"""Round 2 elbow numbers (no picture: cuff_4x shows the elbows at the swing's poses): the painted, tapered sleeve alone
at bends 0, 45, 90, 130 (both sides), its alpha width across the centre line through the elbow against the painted
width at that point, flipped triangles and holes. -> printed and art/tests/hero_joints/r2/elbow_numbers.json"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np

import rig_r2 as rig
from jcommon import TESTS
from meshlib import vec

parts = rig.Parts()
fr = rig.Frame(parts, window=(0, 0, 700, 700))
us = np.arange(rig.UE - 70, rig.UE + 71, 4.0)
out = {}
for b in (0, 45, 90, 130, -45, -90, -130):
    a1 = 90.0
    S = np.array([350.0, 120.0])
    Wd, F, lay, flipped, el = fr.arm(dict(a1=a1, b=float(b)), S)
    m = lay[..., 3] > 0.5
    pts, phi = rig.centre(us, a1, float(b))
    ratios = []
    for u, p, f in zip(us, pts, phi):
        c = S + p
        nv = vec(f + 90)
        w = 0
        for sgn in (1, -1):
            r = 0
            while r < 120:
                x, y = np.round(c + sgn * nv * (r + 1)).astype(int)
                if not m[y, x]:
                    break
                r += 1
            w += r
        ratios.append((w + 1) / (2 * float(rig.hw_of(np.array([u]))[0])))
    ff = m.astype(np.uint8).copy()
    cv2.floodFill(ff, np.zeros((m.shape[0] + 2, m.shape[1] + 2), np.uint8), (0, 0), 1)
    out[b] = {"w_min": round(min(ratios), 3), "w_max": round(max(ratios), 3), "flipped": flipped,
              "holes_px": int((ff == 0).sum())}
    print(b, out[b])
(TESTS / "r2" / "elbow_numbers.json").write_text(json.dumps(out, indent=1))
