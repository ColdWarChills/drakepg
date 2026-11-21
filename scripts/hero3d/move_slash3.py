"""Overhead slash, take 3: take 2's body, with the sword hand keyed so the blade always leaves the fist by the thumb
and first finger. Each sword-hand key gives the grip point, "blade" (only the side and the swing plane) and "th": the
blade's angle from square to the forearm, toward the forearm's own direction (+ = the wrist bent toward the little
finger, - = cocked back toward the thumb). rig_hero.py keeps th within what a wrist and a grip can make (-25..60).
On screen (hero facing right) the blade stays counter-clockwise of the forearm the whole cut: thumb back in the wind-up,
thumb up and forward at the hit. Take 2 hit with the forearm level and the blade below it, so the thumb faced down
and the fist read as inverted. Here the arm comes down steeper than the blade.
python move_slash3.py out.json
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from move_slash import key, ready  # noqa: E402
from move_slash2 import coil, coil2, over, impact, follow, back  # noqa: E402


def k(f, th=None, **p):
    out = key(f, **p)
    if th is not None:
        out["hand.R"]["th"] = th
    return out


coil = dict(coil, blade=(-0.95, 0.05, -0.30))
coil2 = dict(coil2, blade=(-0.95, 0.05, -0.32))
over = dict(over, blade=(-0.85, 0, 0.55))                         # the blade lags behind the hand, pointing back-up
whip = dict(over, root=(0.22, 0, -0.30), hips=(8, 20, 0), spine=(9, 9, 0), chest=(12, 17, 0), neck=(-3, -12, 0),
            head=(-6, -16, 0), hand_r=(0.74, -0.16, 1.30), blade=(0.15, 0, 1.0), hand_l=(0.04, 0.18, 1.06),
            pole_r=(0.3, -0.45, 0.5), fr=(0.60, -0.14, 0.18), pitch_r=-6)
impact = dict(impact, hand_r=(0.82, -0.14, 0.60), blade=(1.0, 0, -0.05), pole_r=(0.30, -0.50, 1.10))
follow = dict(follow, hand_r=(0.76, -0.10, 0.47), blade=(0.95, 0.05, -0.30), pole_r=(0.30, -0.50, 1.00))
settle = dict(follow, root=(0.31, 0, -0.38), hand_r=(0.76, -0.10, 0.49))

if __name__ == "__main__":
    keys = [k(0, **ready), k(5, -25, **coil), k(9, -25, **coil2), k(11, 15, **over), k(12, 35, **whip),
            k(13, 45, **impact), k(16, 45, **follow), k(24, 45, **settle), k(30, **back), k(35, **ready),
            k(38, **ready)]
    mv = {"name": "overhead_slash_3", "keys": keys, "cam_target": [0.25, 0, 0.78], "cam_ortho": 3.0}
    json.dump(mv, open(sys.argv[1], "w"), indent=1)
    print("keys", [x["f"] for x in keys])
