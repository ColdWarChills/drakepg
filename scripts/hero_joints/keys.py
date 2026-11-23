"""The three key arm poses (from the layers_A3 drawings, as angles relative to the chest) and the swing's timeline.

Key poses are given in kind A's geometry (upper arm angle a1, bend b; forearm = a1 - b). Ready and strike take the A3
forearm direction F (ready 3.6, strike 45) and grip turn; the wind-up takes the A3 wind-up's shape (upper arm out to his
back and up, forearm up, elbow bent the other way) and its grip turn (96.8). The wrist spots come from these; every
kind then solves its own elbow from the same wrist (rig.ik), so the kinds differ only in the sleeve."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import math

import numpy as np

import rig

KEYS = {
    #           a1    b     turn   fist      lean  head  far   shoulder (dx, dy)
    "ready":  dict(a1=70.0, b=70.0, turn=44.5, fist="ready", lean=0.0, head=0.0, far=0.0, shoulder=(0.0, 0.0)),
    "wind":   dict(a1=-145.0, b=-60.0, turn=96.8, fist="wind", lean=-3.0, head=1.5, far=3.0, shoulder=(-4.0, -8.0)),
    "strike": dict(a1=52.0, b=7.0, turn=21.5, fist="strike", lean=5.0, head=-2.5, far=-4.0, shoulder=(10.0, 4.0)),
}
# the fist picture for a grip turn: swap at the midpoint between two fists' drawn turns
SWAPS = [(70.65, "wind"), (33.0, "ready"), (-999, "strike")]  # turn >= 70.65 wind, >= 33 ready, else strike


def fist_for(turn):
    for lim, name in SWAPS:
        if turn >= lim:
            return name
    return "strike"


def pose_for(kind, k, sign=None):
    """A pose dict for `kind` with kind A's wrist of key/pose k (dict with a1, b, ...)."""
    p = dict(k)
    if kind != "A":
        S = np.zeros(2)
        wA, _ = rig.wrist_of("A", k["a1"], k["b"])
        st = k.get("stretch", 1.0)
        target = wA * (1.0 if st == 1.0 else stretched_len(st) / rig.L)
        a1, b, _ = rig.ik(kind, target, np.sign(k["b"]) if sign is None else sign, st)
        p["a1"], p["b"] = a1, b
    else:
        p["stretch"] = 1.0
    return p


def stretched_len(st):
    return rig.STRETCH_LO + (rig.STRETCH_HI - rig.STRETCH_LO) * st + (rig.L - rig.STRETCH_HI)
