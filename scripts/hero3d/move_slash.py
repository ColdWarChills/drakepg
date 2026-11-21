"""Write the overhead slash as a key file for rig_hero.py. Battle world: the hero faces +X, right side toward -Y.
python move_slash.py out.json
Beats (24 fps): ready, coil (sword back over the shoulder, weight back, chest turned to the camera), a short hold,
the cut (front foot steps, hips and chest whip round, the blade comes over the top), impact (deep lunge, arm long,
free arm thrown back), follow-through hold, recover to ready.
"""
import json
import sys

FR, BK = (0.24, -0.12, 0.149), (-0.20, 0.10, 0.149)   # front (right) and back (left) ankle at ready


def leg_poles(fr, bk):
    return {"pole_leg.R": [fr[0] + 0.6, fr[1] - 0.05, 0.55], "pole_leg.L": [bk[0] + 0.6, bk[1] + 0.05, 0.5]}


def key(f, root, hips, spine, chest, neck, head, hand_r, blade, hand_l, fr=FR, bk=BK, pole_r=(-0.3, -0.45, 0.9),
        pitch_r=0, pitch_l=0):
    k = {"f": f, "root": list(root), "hips": list(hips), "spine": list(spine), "chest": list(chest),
         "neck": list(neck), "head": list(head),
         "hand.R": {"p": list(hand_r), "blade": list(blade)}, "hand.L": {"p": list(hand_l)},
         "foot.R": list(fr) + [pitch_r], "foot.L": list(bk) + [pitch_l],
         "pole_arm.R": list(pole_r), "pole_arm.L": [-0.5, 0.45, 0.9]}
    k.update(leg_poles(fr, bk))
    return k


ready = dict(root=(0, 0, -0.07), hips=(0, -10, 0), spine=(3, 0, 0), chest=(4, -5, 0), neck=(0, 5, 0), head=(0, 8, 0),
             hand_r=(0.22, -0.24, 0.98), blade=(0.70, 0, 0.71), hand_l=(0.10, 0.02, 1.10))
coil = dict(root=(-0.10, 0, -0.13), hips=(0, -25, 0), spine=(-4, -12, 0), chest=(-8, -22, 0), neck=(0, 25, 0),
            head=(5, 30, 0), hand_r=(-0.14, -0.17, 1.55), blade=(-0.85, 0.05, -0.45), hand_l=(0.32, 0.0, 1.22),
            pole_r=(0.05, -0.45, 0.75))
coil2 = dict(coil, root=(-0.12, 0, -0.14), hand_r=(-0.17, -0.16, 1.53), blade=(-0.80, 0.05, -0.58))
over = dict(root=(0.08, 0, -0.17), hips=(0, 10, 0), spine=(5, 8, 0), chest=(8, 12, 0), neck=(0, -8, 0),
            head=(0, -10, 0), hand_r=(0.20, -0.18, 1.48), blade=(0.35, 0, 0.94), hand_l=(0.15, 0.10, 1.15),
            pole_r=(0.2, -0.45, 0.6), fr=(0.33, -0.12, 0.22), pitch_r=-15)
impact = dict(root=(0.22, 0, -0.22), hips=(0, 25, 0), spine=(8, 12, 0), chest=(14, 22, 0), neck=(-4, -15, 0),
              head=(-5, -22, 0), hand_r=(0.64, -0.12, 0.90), blade=(0.86, 0, -0.51), hand_l=(-0.28, 0.20, 1.12),
              pole_r=(0.35, -0.3, 0.25), fr=(0.44, -0.12, 0.149), bk=(-0.24, 0.10, 0.149))
follow = dict(impact, root=(0.24, 0, -0.24), hand_r=(0.58, -0.08, 0.74), blade=(0.70, 0.08, -0.71),
              hand_l=(-0.32, 0.22, 1.10), chest=(16, 26, 0), head=(-6, -24, 0))
settle = dict(follow, root=(0.23, 0, -0.225), hand_r=(0.58, -0.08, 0.76))
back = dict(ready, root=(0.08, 0, -0.10), fr=(0.34, -0.12, 0.20), pitch_r=-10)

if __name__ == "__main__":
    keys = [key(0, **ready), key(6, **coil), key(9, **coil2), key(11, **over), key(12, **impact), key(15, **follow),
            key(22, **settle), key(28, **back), key(32, **ready), key(36, **ready)]
    mv = {"name": "overhead_slash", "keys": keys, "cam_target": [0.2, 0, 0.82], "cam_ortho": 2.9}
    json.dump(mv, open(sys.argv[1], "w"), indent=1)
    print("keys", [k["f"] for k in keys])
