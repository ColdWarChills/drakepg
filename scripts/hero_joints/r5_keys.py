"""Attempt 2 (round 5): the key poses of hero 4's sword_cut with the strike drawings, frame by frame (30 fps), the single
source of the Unity clip. <venv python> -I -B r5_keys.py [front|back]

Writes unity/Assets/Test2D/Art/keys.json in r4_keys' form (a1/b/turn in rig_r2's convention: y down, degrees clockwise,
a1 = the upper arm's SCREEN angle, forearm F = a1 - b, blade = F - turn), plus per frame the body drawing set
(front | windup | strike) and the waist offset of that set (ox, oy: canvas px from the standing waist; the strike is the
lunge moved so the chosen foot stays planted). Every frame is a key (the clip is stepped: held key poses, few
in-betweens). Prints per frame: rho (the fist's turn from its drawing, before the battle's tilt, which hero 4 no longer
takes), the wrist bend from the drawing, and whether wrist, grip, pommel, forearm or upper arm enter the face box.

Timing: clip frame 0 = the frame before the battle starts the blow; take frame = clip frame + 5 (attempt 1's takes:
press at take 6, today's hero in his wind pose 6-9, step 10, contact 11, strike pose to 15, rest at 16 and back home
by 21)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import numpy as np

import r4_keys as k4
from jcommon import REDO

G2, G4 = k4.G2, k4.G4
G5 = json.loads((REDO / "parts_r5.json").read_text())
FIST_BLADE = k4.FIST_BLADE            # -40.9: the drawn blade angle of the fist
FIST_TURN = 44.48                     # rig.json fist_turn_ccw: forearm minus blade as the fist is drawn
UE, L = k4.UE, k4.L
WAIST2 = np.array(G2["waist"], float)
WAIST5 = np.array(G5["waist"], float)
PLANT = {"front": (-16.3, 99.0), "back": (143.7, 99.0), "drawn": (-36.3, 99.0)}
plant = next((a for a in sys.argv[1:] if a in PLANT), "back")
OX, OY = PLANT[plant]
POMMEL = 170.0                        # grip to the pommel's end (sword.png: 893 px tall, grip at 0.79)
SLEEVE_HALF = 30.0                    # half the sleeve's width, for the face check

# Face boxes (canvas px, standing frame): the standing head (head_front: eye at about (806, 376)) and the shouting head
# as drawn in the lunge (head_shout), each with 10 px room. The strike box moves with the lunge's offset.
FACE_STAND = (755, 330, 870, 470)
FACE_STRIKE_DRAWN = (850, 505, 980, 660)

ZERO = dict(st=0.0, lean=0.0, head=0.0, tail=0.0, dy=0.0, hx=0.0, far=0.0, fx=0.0, fy=0.0, sx=0.0, sy=0.0)
# The wind-up: no whole-body tilt (st turned about the root sank the back sole 9 px under the ground) and no hips shift
# (hx: the hips piece carries the legs, so it slid the planted feet 4 px); the lean is the torso's.
WIND = dict(st=0.0, lean=-5.0, head=3.0, tail=-10.0, dy=4.0, hx=0.0, far=26.0, fx=6.0, fy=-3.0)
WIND_HOLD = dict(WIND, head=3.5, tail=-12.0, far=28.0)
# Fix round (2026-11-24): the coiled wind-up, body set "wind" (hips_wind: wide stance, knees bent + round 4's turned
# torso/armhole/far arm + round 2's head/tails). The whole body is tilted back 12 degrees by the stance bone (st, about
# the root: standing waist x, soles); ox/oy place hips_wind's own waist so that the planted back heel stays at canvas
# (489, 1283) after that tilt (windup_numbers.json). dy 11 sinks the r4 torso so its half of the old buckle stays
# under the new belt. Screen blade = blade key + st = -124; the fist's turn on screen -83.1 (limit 90).
# ox/oy: the still's 48.1/6.3 put the rendered back boot 3 px behind the ready's and 1 px high (Unity studio, rear
# edge on the sole rows 481 vs 484, sole 1282 vs 1283); +2.7/+1.6 before the tilt = (+3, +1) on screen.
WIND5 = dict(a1=-154.5, rel=True, b=-99.5, blade=-112.0, body="wind", sleeve="under", ox=50.8, oy=7.9,
             st=-12.0, lean=-5.0, head=3.0, tail=-10.0, dy=11.0, hx=0.0, far=26.0, fx=6.0, fy=-3.0)
READY = dict(a1=70.0, b=70.0, blade=-44.5)

# One entry per clip frame: arm (a1 screen, b, blade screen; a1 of wind-up frames relative to the torso: rel), body
# drawing set, sleeve shading, smear, body moves (missing = 0).
FRAMES = [
    dict(**READY, body="front"),                                                               # 0 READY
    *[dict(WIND5) for _ in range(4)],
    #   1 WIND-UP (swap) and 2-4 held: the lift is dropped (its straight arm read as a long tube across the chest); like
    #   today's hero, who is in his wind pose from the press on, hero 4 swaps straight from the ready into the coiled
    #   wind-up. The elbow changes side on this swap frame (a stepped swap of drawings and solver side, never a blend).
    dict(a1=68.0, b=0.0, blade=-5.0, body="strike", smear=True),                                # 5 CUT (swap, smear)
    #   (the arm within ~6 degrees of the strike drawing's: at 40 the drawn armhole kinks against the sleeve at 3x; the
    #   overhead arc is the smear's)
    dict(a1=G5["a1_drawn"], b=0.0, blade=25.0, body="strike", smear=True),                      # 6 STRIKE (contact)
    *[dict(a1=G5["a1_drawn"], b=0.0, blade=25.0, body="strike") for _ in range(4)],              # 7-10 STRIKE held
    dict(a1=75.0, b=40.0, blade=0.0, body="front", lean=2.0, head=-1.0, tail=4.0),              # 11 RECOVER (swap)
    dict(a1=72.0, b=60.0, blade=-30.0, body="front", lean=1.0, head=-0.5, tail=2.0),            # 12
    dict(a1=70.0, b=68.0, blade=-42.0, body="front", lean=0.3, tail=1.0),                        # 13
    dict(**READY, body="front"),                                                               # 14 READY
    dict(**READY, body="front"),                                                               # 15
    dict(**READY, body="front"),                                                               # 16
]


def frames():
    out = []
    for f, k in enumerate(FRAMES):
        p = dict(ZERO)
        p.update({x: v for x, v in k.items() if x in ZERO})
        p["a1"] = k["a1"] + (p["lean"] if k.get("rel") else 0.0)
        p["b"] = k["b"]
        p["turn"] = p["a1"] - p["b"] - k["blade"]
        p.update(f=f, body=k["body"], sleeve=k.get("sleeve", "lit"), smear=k.get("smear", False))
        p["ox"], p["oy"] = (OX, OY) if p["body"] == "strike" else (k.get("ox", 0.0), k.get("oy", 0.0))
        out.append(p)
    for p in out:
        p["straight"] = abs(p["b"]) < 1e-6
        p["flip"] = p["b"] > 0
    for i, p in enumerate(out):      # a straight frame takes the side of the first bent frame after it
        if p["straight"]:
            nxt = next((q for q in out[i + 1:] if not q["straight"]), None)
            p["flip"] = nxt["b"] > 0 if nxt else p["flip"]
    return out


def rot(deg, v):
    return k4.rot(deg, v)


def vec(a, l=1.0):
    return k4.vec(a, l)


def body_points(body):
    """(waist, S) of a drawing set on its own canvas."""
    if body == "strike":
        return WAIST5, np.array(G5["S"], float)
    if body in ("windup", "wind"):
        return WAIST2, np.array(G4["S"], float)
    return WAIST2, np.array(G2["S"], float)


def geo(p):
    """Canvas px (standing frame, y down) of the arm and sword as Unity places them: the hips pivot at the standing
    waist + (hx + ox, oy), the torso turned by the lean about it (+ dy), the shoulder in the torso, the wrist at the end
    of two straight bones, then everything turned about the root (standing waist x, soles) by st."""
    wb, Sb = body_points(p["body"])
    pivot = WAIST2 + np.array([p["hx"] + p.get("ox", 0.0), p["dy"] + p.get("oy", 0.0)])
    S = pivot + rot(p["lean"], Sb - wb + np.array([p["sx"], p["sy"]]))
    E = S + vec(p["a1"], UE)
    W = S + vec(p["a1"], L) if abs(p["b"]) < 1e-6 else E + vec(p["a1"] - p["b"], L - UE)
    blade = p["a1"] - p["b"] - p["turn"]
    rho = blade - FIST_BLADE
    grip = W + rot(rho, k4.GRIP0 - k4.WRIST0)
    out = {"grip": grip, "blade": blade, "tip": grip + vec(blade, k4.TIP_LEN), "S": S, "W": W, "E": E,
           "pommel": grip - vec(blade, POMMEL)}
    root = np.array([WAIST2[0], 1283.0])
    for key in ("grip", "tip", "S", "W", "E", "pommel"):
        out[key] = root + rot(p["st"], out[key] - root)
    out["blade"] = blade + p["st"]
    return out


def face_box(p):
    if p["body"] == "strike":
        x0, y0, x1, y1 = FACE_STRIKE_DRAWN
        dx = p["ox"] - PLANT["drawn"][0]
        return (x0 + dx, y0, x1 + dx, y1)
    if p["body"] == "wind":
        # the standing head box carried as the torso carries the head: waist offset + sink, lean, then the tilt about
        # the root (= windup_numbers.json's face box at st -12)
        x0, y0, x1, y1 = FACE_STAND
        piv = WAIST2 + np.array([p["hx"] + p["ox"], p["dy"] + p["oy"]])
        root = np.array([WAIST2[0], 1283.0])
        pts = np.array([root + rot(p["st"], piv + rot(p["lean"], np.array(c, float) - WAIST2) - root)
                        for c in ((x0, y0), (x1, y0), (x0, y1), (x1, y1))])
        return (*pts.min(0), *pts.max(0))
    return FACE_STAND


def seg_hits(a, b, box, pad=0.0):
    x0, y0, x1, y1 = box[0] - pad, box[1] - pad, box[2] + pad, box[3] + pad
    for t in np.linspace(0, 1, 40):
        x, y = a + (b - a) * t
        if x0 <= x <= x1 and y0 <= y <= y1:
            return True
    return False


def numbers(fs):
    flips, bad = [], []
    for i, p in enumerate(fs):
        g = geo(p)
        blade = p["a1"] - p["b"] - p["turn"]
        rho = (blade - FIST_BLADE + 180) % 360 - 180
        bend = (p["turn"] - FIST_TURN + 180) % 360 - 180
        p["rho"], p["blade"], p["wrist_bend"] = rho, blade, bend
        p["rho_screen"] = rho + p["st"]
        box = face_box(p)
        hits = [n for n, pt in (("wrist", g["W"]), ("grip", g["grip"]), ("pommel", g["pommel"]))
                if seg_hits(pt, pt, box)]
        if seg_hits(g["grip"], g["pommel"], box):
            hits.append("hilt")
        if seg_hits(g["E"], g["W"], box, SLEEVE_HALF):
            hits.append("forearm")
        if seg_hits(g["S"], g["E"], box, SLEEVE_HALF):
            hits.append("upper")
        if seg_hits(g["grip"], g["tip"], box):
            hits.append("blade")
        p["face_hits"] = hits
        if i and fs[i - 1]["flip"] != p["flip"]:
            flips.append(p["f"])
        if hits and not p["smear"]:
            bad.append((p["f"], hits))
        print("f%02d %-6s %-5s a1 %7.1f b %6.1f F %7.1f blade %7.1f rho %+6.1f (screen %+6.1f) wrist bend %+6.1f%s%s%s%s" % (
            p["f"], p["body"], p["sleeve"], p["a1"], p["b"], p["a1"] - p["b"], blade, rho, p["rho_screen"], bend,
            " STRAIGHT" if p["straight"] else "", " SMEAR" if p["smear"] else "",
            " |rho|>90!" if abs(p["rho_screen"]) >= 90 else "", (" FACE: " + ",".join(hits)) if hits else ""))
    print("flip changes at frames", flips, "(each must be a straight frame or a drawing-set swap)")
    print("plant:", plant, (OX, OY), "| face hits on non-smear frames:", bad or "none")
    return flips, bad


def main():
    fs = frames()
    flips, bad = numbers(fs)
    for f in flips:
        assert fs[f]["straight"] or fs[f]["body"] != fs[f - 1]["body"], f"flip at f{f} is not on a straight or swap frame"
    k4.geo = geo                       # r4_keys' smear drawer, on this clip's geometry (strike shoulder and offset)
    k4.FIELDS = k4.FIELDS + ["ox", "oy"]   # its in-between poses carry the offset too (the shoulder's path into the lunge)
    sm = k4.smears(fs)
    k4.OUT.write_text(json.dumps({"fps": k4.FPS, "fist_blade": FIST_BLADE, "plant": plant, "waist_offset": [OX, OY],
                                  "frames": fs, "smears": sm}, indent=1, default=float))
    print("wrote", k4.OUT, len(fs), "frames")


if __name__ == "__main__":
    main()
