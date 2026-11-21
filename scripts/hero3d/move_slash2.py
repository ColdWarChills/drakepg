"""Overhead slash, take 2: keyed against the strongest AI-video frames (deep wide lunge, hips low, chest far forward,
free arm thrown back for a long silhouette). Battle world: the hero faces +X, his right side toward -Y.
python move_slash2.py out.json [--stepped]
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from move_slash import key, ready  # noqa: E402

coil = dict(root=(-0.14, 0, -0.20), hips=(-4, -30, 0), spine=(-6, -14, 0), chest=(-10, -26, 0), neck=(0, 28, 0),
            head=(6, 32, 0), hand_r=(-0.16, -0.16, 1.50), blade=(-0.80, 0.08, -0.60), hand_l=(0.30, 0.0, 1.10),
            pole_r=(0.05, -0.45, 0.75), fr=(0.26, -0.13, 0.149), bk=(-0.30, 0.10, 0.149))
coil2 = dict(coil, root=(-0.16, 0, -0.22), hand_r=(-0.19, -0.15, 1.47), blade=(-0.74, 0.08, -0.67))
over = dict(root=(0.10, 0, -0.24), hips=(4, 12, 0), spine=(6, 8, 0), chest=(8, 12, 0), neck=(0, -8, 0),
            head=(0, -10, 0), hand_r=(0.22, -0.18, 1.40), blade=(0.30, 0, 0.95), hand_l=(0.18, 0.10, 1.05),
            pole_r=(0.2, -0.45, 0.6), fr=(0.46, -0.13, 0.25), bk=(-0.32, 0.11, 0.149), pitch_r=-15)
impact = dict(root=(0.30, 0, -0.36), hips=(12, 28, 0), spine=(12, 10, 0), chest=(16, 22, 0), neck=(-6, -16, 0),
              head=(-12, -22, 0), hand_r=(0.92, -0.14, 0.70), blade=(0.92, 0.0, -0.38), hand_l=(-0.10, 0.26, 1.08),
              pole_r=(0.45, -0.35, 0.30), fr=(0.68, -0.14, 0.149), bk=(-0.36, 0.12, 0.149))
follow = dict(impact, root=(0.32, 0, -0.39), hand_r=(0.88, -0.10, 0.52), blade=(0.70, 0.08, -0.71),
              hand_l=(-0.12, 0.27, 1.04), chest=(19, 26, 0), head=(-14, -25, 0))
settle = dict(follow, root=(0.31, 0, -0.38), hand_r=(0.87, -0.10, 0.54))
back = dict(ready, root=(0.10, 0, -0.12), fr=(0.36, -0.12, 0.20), pitch_r=-10)

if __name__ == "__main__":
    keys = [key(0, **ready), key(5, **coil), key(9, **coil2), key(11, **over), key(13, **impact), key(16, **follow),
            key(24, **settle), key(30, **back), key(35, **ready), key(38, **ready)]
    mv = {"name": "overhead_slash_2", "keys": keys, "cam_target": [0.25, 0, 0.78], "cam_ortho": 3.0}
    if "--stepped" in sys.argv:
        mv["interp"] = "CONSTANT"
    json.dump(mv, open(sys.argv[1], "w"), indent=1)
    print("keys", [k["f"] for k in keys])
