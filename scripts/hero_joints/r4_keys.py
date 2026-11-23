"""Round 4, step 2: the key poses of hero 4's sword_cut, frame by frame (30 fps), the single source of the Unity clip.
<venv python> -I -B r4_keys.py [preview]

Writes unity/Assets/Test2D/Art/keys.json: one pose per clip frame (a1/b/turn in rig_r2's convention: y down, degrees
clockwise, a1 = the upper arm's SCREEN angle, forearm = a1 - b, blade = forearm - turn), body moves, which drawings
show (body front|windup, sleeve lit|under), the solver's flip (b > 0) and the straight frames where flip changes.
Prints per frame: a1 relative to the torso, b, fist rotation from its drawing (rho), wrist. 'preview' renders every frame
with rig_r2 (the Python rig, as ground truth for pose design) into art/tests/hero_joints/r4/keys_preview.png.

Timing: frame 0 = the frame the battle shows the blow start (today's hero: wind pose on 0-3, step 4, contact 5)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math

import numpy as np

from jcommon import REDO, ROOT, TESTS

OUT = ROOT / "unity" / "Assets" / "Test2D" / "Art" / "keys.json"
G4 = json.loads((REDO / "parts_r4.json").read_text())
FIST_BLADE = -40.9           # fist_r2: the drawn blade angle (the fist's own orientation)
A1_DRAWN = G4["a1_drawn"]    # the turned torso's drawn upper arm, relative to the torso
FPS = 30
UE, L = 134.0, 258.5

# st = the whole body tilted about the soles (degrees, + = forward), on top of the battle's own tilt.
# Key poses. f = clip frame; ease into this key: 'io' in-out, 'i' accelerate, 'o' decelerate, 'l' linear, 'h' hold
# (the same values as the key before). a1 for the wind-up keys is given RELATIVE to the torso (rel=True).
BASE = dict(st=0.0, lean=0.0, head=0.0, tail=0.0, dy=0.0, hx=0.0, far=0.0, fx=0.0, fy=0.0, sx=0.0, sy=0.0)
KEYS = [
    dict(f=0, a1=70.0, b=70.0, turn=44.5, body="front", sleeve="lit"),                          # READY
    dict(f=1, a1=10.0, b=62.0, turn=44.0, st=-0.5, lean=-2.0, head=1.0, tail=-4.0, dy=1.0, ease="i"),    # lift
    dict(f=2, a1=-84.0, b=0.0, turn=36.0, st=-1.5, lean=-4.0, head=2.0, tail=-8.0, dy=2.5, hx=-2.0, ease="l"),  # straight up
    dict(f=3, a1=-157.0, rel=True, b=-74.0, turn=33.0, st=-3.0, lean=-5.0, head=3.0, tail=-10.0, dy=4.0, hx=-4.0, far=26.0,
         fx=6.0, fy=-3.0, body="windup", sleeve="under", ease="o"),                               # WIND-UP arrives (swap)
    dict(f=4, a1=-154.5, rel=True, b=-72.0, turn=33.0, st=-3.0, lean=-5.0, head=3.5, tail=-12.0, dy=4.0, hx=-4.0, far=28.0,
         fx=6.0, fy=-3.0, ease="o"),                                                              # WIND-UP held
    dict(f=5, a1=-48.0, b=0.0, turn=58.0, st=2.0, lean=6.0, head=-1.0, tail=-4.0, dy=0.0, hx=0.0, far=4.0, fx=-8.0, fy=4.0,
         body="front", sleeve="lit", ease="i", smear=True),                                       # the cut (straight)
    dict(f=6, a1=40.0, b=6.0, turn=-8.0, st=8.0, lean=22.0, head=-8.0, tail=6.0, dy=-1.0, hx=8.0, far=9.0, fx=-20.0, fy=11.0,
         sx=7.0, sy=3.0, ease="l", smear=True),                                                   # STRIKE (contact)
    dict(f=7, a1=38.0, b=5.0, turn=-6.0, st=7.5, lean=21.0, head=-7.0, tail=10.0, dy=-1.0, hx=8.0, far=8.0, fx=-18.0, fy=10.0,
         sx=6.0, sy=3.0, ease="o"),
    dict(f=10, a1=40.0, b=5.0, turn=-4.0, st=7.0, lean=20.0, head=-6.5, tail=6.0, dy=-0.5, hx=7.0, far=8.0, fx=-17.0, fy=9.5,
         sx=5.0, sy=3.0, ease="io"),                                                              # STRIKE held
    dict(f=14, a1=68.0, b=66.0, turn=43.0, lean=-0.5, head=0.5, tail=-2.0, hx=0.0, ease="io"),   # recover
    dict(f=16, a1=70.0, b=70.0, turn=44.5, ease="o"),                                             # READY
]
FIELDS = ["a1", "b", "turn", "st", "lean", "head", "tail", "dy", "hx", "far", "fx", "fy", "sx", "sy"]


def ease(kind, s):
    if kind == "i":
        return s * s
    if kind == "o":
        return 1 - (1 - s) ** 2
    if kind == "io":
        return s * s * (3 - 2 * s)
    if kind == "h":
        return 0.0
    return s


def full_keys():
    out, prev = [], None
    for k in KEYS:
        d = dict(BASE) if prev is None else {f: prev[f] for f in FIELDS}
        d.update(body=prev["body"] if prev else "front", sleeve=prev["sleeve"] if prev else "lit")
        d.update({f: v for f, v in k.items() if f in FIELDS or f in ("body", "sleeve")})
        if prev is not None:   # unset body fields go back to BASE unless given
            for f in FIELDS[3:]:
                if f not in k:
                    d[f] = BASE[f]
        if k.get("rel"):
            d["a1"] = k["a1"] + d["lean"]
        d["f"], d["ease"], d["smear"] = k["f"], k.get("ease", "l"), k.get("smear", False)
        out.append(d)
        prev = d
    return out


def frames():
    ks = full_keys()
    res = []
    for i in range(len(ks) - 1):
        k0, k1 = ks[i], ks[i + 1]
        for f in range(k0["f"], k1["f"]):
            s = ease(k1["ease"], (f - k0["f"]) / (k1["f"] - k0["f"]))
            p = {fl: k0[fl] + (k1[fl] - k0[fl]) * s for fl in FIELDS}
            p.update(f=f, body=k0["body"], sleeve=k0["sleeve"], smear=k0["smear"] and f == k0["f"])
            res.append(p)
    last = ks[-1]
    res.append({**{fl: last[fl] for fl in FIELDS}, "f": last["f"], "body": last["body"], "sleeve": last["sleeve"],
                "smear": False})
    for p in res:
        p["straight"] = abs(p["b"]) < 1e-6
        p["flip"] = p["b"] > 0       # Limb Solver 2D's flip: on = the elbow bent to b > 0 (measured in Unity)
    # the flip value on a straight frame = the side of the frames after it (the change shows nothing: arm straight)
    for i, p in enumerate(res):
        if p["straight"] and i + 1 < len(res):
            p["flip"] = res[i + 1]["b"] > 0
    return res


def vec(a, l=1.0):
    return np.array([math.cos(math.radians(a)), math.sin(math.radians(a))]) * l


def numbers(fs):
    flips = []
    for i, p in enumerate(fs):
        F = p["a1"] - p["b"]
        blade = F - p["turn"]
        rho = (blade - FIST_BLADE + 180) % 360 - 180
        rel = p["a1"] - p["lean"]
        p["rho"], p["blade"] = rho, blade
        if i and fs[i - 1]["flip"] != p["flip"]:
            flips.append(p["f"])
        print("f%02d %-6s %-5s a1 %7.1f (rel torso %7.1f%s) b %6.1f F %7.1f turn %5.1f blade %7.1f rho %+6.1f%s%s%s" % (
            p["f"], p["body"], p["sleeve"], p["a1"], rel,
            " FIT %+.1f" % (((rel - A1_DRAWN + 180) % 360) - 180) if p["body"] == "windup" else "",
            p["b"], F, p["turn"], blade, rho, " STRAIGHT" if p["straight"] else "", " SMEAR" if p["smear"] else "",
            " |rho|>90!" if abs(rho) > 90 else ""))
    print("flip changes at frames", flips, "(each must be a straight frame)")
    return flips


G2 = json.loads((REDO / "parts_r2.json").read_text())
WAIST = np.array(G2["waist"], float)
WRIST0, GRIP0 = np.array([850.0, 745.0]), np.array([876.7, 760.3])   # fist_r2: the drawn wrist and grip
TIP_LEN = 676.4                     # r4_export: grip to the blade's tip (px)
SMEAR_MIN = 250.0                   # px of tip travel in a frame for its sweep to leave a smear
SMEAR_ALPHA = (0.45, 0.2)           # this frame's sweep, the one before
SMEAR_INNER = 0.45                  # the band's inner edge on the blade (share of the tip's radius) at the newest end
SMEAR_RGB = (255, 247, 226)


def rot(deg, v):
    c, s_ = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return np.array([c * v[0] - s_ * v[1], s_ * v[0] + c * v[1]])


def geo(p):
    """Canvas px (y down) of the sword as Unity places it: hips at the waist + hx, torso turned by the lean about it
    (+ dy), the shoulder in the torso, the wrist at the end of two straight bones (straight frames: full length)."""
    wind = p["body"] == "windup"
    S_c = np.array(G4["S"] if wind else G2["S"], float)
    torso = WAIST + np.array([p["hx"], p["dy"]])
    S = torso + rot(p["lean"], S_c - WAIST + np.array([p["sx"], p["sy"]]))
    if abs(p["b"]) < 1e-6:
        W = S + vec(p["a1"], L)
    else:
        W = S + vec(p["a1"], UE) + vec(p["a1"] - p["b"], L - UE)
    blade = p["a1"] - p["b"] - p["turn"]
    rho = blade - FIST_BLADE
    grip = W + rot(rho, GRIP0 - WRIST0)
    out = {"grip": grip, "blade": blade, "tip": grip + vec(blade, TIP_LEN), "S": S, "W": W}
    # the stance: everything turned about the root (waist x, soles) by st
    root = np.array([G2["waist"][0], 1283.0])
    for k in ("grip", "tip", "S", "W"):
        out[k] = root + rot(p["st"], out[k] - root)
    out["blade"] = blade + p["st"]
    return out


def lerp_pose(a, b, s):
    q = {fl: a[fl] + (b[fl] - a[fl]) * s for fl in FIELDS}
    q["body"] = a["body"] if s < 0.5 else b["body"]
    return q


def smears(fs):
    """For each frame whose tip travels far: flat bands over the blade's sweep from the frame before (sampled in joint
    space between the two frames, so the band follows the arc), and the older sweep fainter. One PNG per such frame,
    drawn on the drawing canvas, pivot at the hero root (waist x, soles)."""
    from PIL import Image, ImageDraw
    art = OUT.parent
    for f in art.glob("smear_f*.png"):
        f.unlink()
    out = []
    for i in range(1, len(fs)):
        travel = float(np.hypot(*(geo(fs[i])["tip"] - geo(fs[i - 1])["tip"])))
        fs[i]["tip_travel"] = travel
        if travel < SMEAR_MIN or not fs[i]["smear"]:   # only the strike's frames (keys with smear=True)
            fs[i]["smear"] = False
            continue
        bands = []
        for age in (1, 0):
            j = i - age
            if j < 1:
                continue
            if not fs[j]["smear"] or float(np.hypot(*(geo(fs[j])["tip"] - geo(fs[j - 1])["tip"]))) < SMEAR_MIN:
                continue
            n = 10
            gs = [geo(lerp_pose(fs[j - 1], fs[j], k / n)) for k in range(n + 1)]
            outer, inner = [], []
            for k, g in enumerate(gs):
                pos = (1 - age + k / n) / 2.0                 # 0 = the trail's old end, 1 = the blade now
                r_in = TIP_LEN * (1 - (1 - SMEAR_INNER) * pos)
                outer.append(g["grip"] + vec(g["blade"], TIP_LEN))
                inner.append(g["grip"] + vec(g["blade"], r_in))
            bands.append((np.array(outer + inner[::-1]), SMEAR_ALPHA[age]))
        allp = np.concatenate([b[0] for b in bands])
        x0, y0 = np.floor(allp.min(0)).astype(int) - 4
        x1, y1 = np.ceil(allp.max(0)).astype(int) + 4
        ss = 3
        acc = np.zeros((y1 - y0, x1 - x0), np.float32)
        for pts, a in bands:
            im = Image.new("L", ((x1 - x0) * ss, (y1 - y0) * ss), 0)
            ImageDraw.Draw(im).polygon([((x - x0) * ss, (y - y0) * ss) for x, y in pts], fill=255)
            cov = np.asarray(im.resize((x1 - x0, y1 - y0), Image.BOX)).astype(np.float32) / 255 * a
            acc = cov + acc * (1 - cov)
        rgba = np.zeros(acc.shape + (4,), np.uint8)
        rgba[..., :3] = SMEAR_RGB
        rgba[..., 3] = (acc * 255 + 0.5).astype(np.uint8)
        name = f"smear_f{fs[i]['f']:02d}.png"
        Image.fromarray(rgba, "RGBA").save(art / name)
        root = (G2["waist"][0], 1283.0)
        out.append({"f": fs[i]["f"], "file": name, "pivot": [root[0] - x0, root[1] - y0], "size": [x1 - x0, y1 - y0]})
        fs[i]["smear"] = True
        print("smear at f%d: tip travel %.0f px, %d bands, %s" % (fs[i]["f"], travel, len(bands), name))
    return out


def preview(fs):
    import rig_r2 as rig
    import r4_turned as rt
    import swing_r2 as sw
    from PIL import Image
    p2 = rig.Parts()
    p4 = rig.Parts(REDO / "parts_r4")
    fr2, fr4 = rig.Frame(p2), rig.Frame(p4)
    bg = sw.background(fr2)
    tiles = []
    for p in fs:
        pose = dict(a1=p["a1"], b=p["b"], turn=p["turn"], lean=p["lean"], head=p["head"], tail=p["tail"], dy=p["dy"],
                    far=p["far"], fx=p["fx"], fy=p["fy"], shoulder=(p["sx"], p["sy"]))
        target = rig.wrist_A(p["a1"], p["b"])
        sign = -1.0 if p["b"] < 0 else 1.0
        pose["a1"], pose["b"] = rig.ik(target, sign)
        wind = p["body"] == "windup"
        rig.SHADE_SIDE = -1.0 if p["sleeve"] == "under" else 1.0
        if wind:
            S_old = rig.S0.copy()
            rig.S0 = np.array(G4["S"], float)
            rig.ARMHOLE = True
            px, _ = rt.render4(fr4, p4, pose)
            rig.S0, rig.ARMHOLE = S_old, False
        else:
            px, _ = fr2.render(pose)
        rig.SHADE_SIDE = 1.0
        im = rt.on_bg(px, bg)
        im = rt.game(im, fr2)
        tiles.append(rt.label(im, "f%d %s" % (p["f"], p["body"])))
    per = 9
    rows = [rt.row(tiles[i:i + per]) for i in range(0, len(tiles), per)]
    out = TESTS / "r4" / "keys_preview.png"
    rt.col(rows).save(out)
    print("saved", out)


def main():
    fs = frames()
    flips = numbers(fs)
    for f in flips:
        assert next(p for p in fs if p["f"] == f)["straight"], f"flip at f{f} is not on a straight frame"
    sm = smears(fs)
    OUT.write_text(json.dumps({"fps": FPS, "fist_blade": FIST_BLADE, "frames": fs, "smears": sm}, indent=1,
                              default=float))
    print("wrote", OUT, len(fs), "frames")
    if "preview" in sys.argv:
        preview(fs)


if __name__ == "__main__":
    main()
