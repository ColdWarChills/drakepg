"""Round 5 fix (the wind-up): the coiled lower body cut from the picked Qwen try, for hero 4's rig.
<venv python> -I -B r5w_cut.py [key]          (default: the pick below; run AFTER cut_r5.py, which rewrites parts_r5.json)
Output: art/redo/hero_joints/parts_r5/hips_wind.png (belt down: belt, both legs, boots; same 1536 canvas and scale as
parts_r2/r4/r5) and a "wind" block added to parts_r5.json (every existing key kept as it was; asserted).
- The drawing is moved up/down DY px so its soles sit on s202's feet line (1283); no scale.
- hips_wind: cut on the belt's own top edge (a fitted line), the figure's biggest piece under it (the far fist, which
  hangs near the belt line, is left out); the belt band scaled to round 2's thickness (as cut_r5 squeezes the strike's;
  here the drawn belt is thinner, so it is stretched), the trousers' top 40 px eased.
- The upper body is round 4's turned torso (parts_r4: torso, armhole, far arm) with round 2's head and tails; only the
  legs are new. Joints written: waist (the belt's centre after the squeeze), the two boots' soles, and the waist offsets
  from the standing waist (as drawn / back foot planted / front toe planted)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image

from jcommon import REDO

_argv, sys.argv = sys.argv, sys.argv[:1]
import cut_r2  # noqa: E402  (figure(); its main() is not run)
sys.argv = _argv

KEY = sys.argv[1] if len(sys.argv) > 1 else "r5w_wind_s522"
OUT = REDO / "parts_r5"
JSON = REDO / "parts_r5.json"
P2 = json.loads((REDO / "parts_r2.json").read_text())
P4 = json.loads((REDO / "parts_r4.json").read_text())
FEET = 1283.0
BELT_BOX = (560, 640, 900, 820)   # (x0, y0, x1, y1) drawing px before DY: where the belt is looked for
BELT_T_R2 = None                  # filled from parts_r2's hips (measured the same way)


def shifted(px, dx, dy):
    M = np.float32([[1, 0, dx], [0, 1, dy]])
    return cv2.warpAffine(px, M, (px.shape[1], px.shape[0]), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)


def brown(px):
    R, G, B, A = [px[..., i] for i in range(4)]
    return (A > 128) & (R < 120) & (G < 85) & (B < 60) & (R - B > 15) & (R > G)


def belt_line(px, box):
    """The belt (biggest brown component in the box, closed across the buckle) and lines fitted to its top and
    bottom edges."""
    H, W = px.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    x0, y0, x1, y1 = box
    m = brown(px) & (yy > y0) & (yy < y1) & (xx > x0) & (xx < x1)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
    n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    b = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    ys, xs = np.nonzero(b)
    cols = np.arange(xs.min() + 6, xs.max() - 5)
    tops = np.array([ys[xs == x].min() for x in cols])
    bots = np.array([ys[xs == x].max() for x in cols])
    a, k = np.polyfit(cols, tops, 1)
    a2, k2 = np.polyfit(cols, bots, 1)
    T = float(np.median(bots - tops))
    pts = np.column_stack([xs, ys]).astype(np.float32)
    (cx, cy), (w, h), _ = cv2.minAreaRect(pts)
    return dict(slope=float(a), icpt=float(k), slope_bot=float(a2), icpt_bot=float(k2), x0=int(xs.min()),
                x1=int(xs.max()), centre=[float(cx), float(cy)], L=float(max(w, h)), T_box=float(min(w, h)), T=T)


def boots(px):
    """The two boots: brown components below y 1000; x extent, sole x extent (lowest 12 rows), bottom."""
    m = brown(px)
    m[:1000] = False
    n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    cs = [lab == i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= 3000]
    out = []
    for c in sorted(cs, key=lambda c: np.nonzero(c)[1].min())[:2]:
        ys, xs = np.nonzero(c)
        low = ys >= ys.max() - 12
        out.append(dict(x=[int(xs.min()), int(xs.max())], sole=[int(xs[low].min()), int(xs[low].max())],
                        bottom=int(ys.max()), area=int(c.sum())))
    return out


def main():
    _, A, _, _ = cut_r2.figure(KEY)
    ys = np.nonzero((A[..., 3] > 128).any(1))[0]
    DY = FEET - float(ys.max())
    A = shifted(A, 0, DY)
    H, W = A.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    box = (BELT_BOX[0], BELT_BOX[1] + DY, BELT_BOX[2], BELT_BOX[3] + DY)
    bl = belt_line(A, box)
    y_top = bl["slope"] * xx + bl["icpt"]
    fig = A[..., 3] > 128
    below = fig & (yy >= y_top - 0.5)
    n, lab, st, _ = cv2.connectedComponentsWithStats(below.astype(np.uint8), 8)
    hips = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    hips = cv2.dilate(hips.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & below   # keep the soft edge
    hp = A.copy()
    hp[~hips] = 0
    tt = np.clip(yy - (y_top - 1.0), 0, 1)
    hp[..., 3] *= np.where(hips, tt, 0)
    # the belt's thickness against round 2's (measured the same way on parts_r2/hips): squeeze the band under its top
    # line onto round 2's, the trousers' top 40 px eased (as cut_r5)
    h2 = np.asarray(Image.open(REDO / "parts_r2" / "hips.png").convert("RGBA")).astype(np.float32)
    b2 = belt_line(h2, (600, 650, 900, 800))
    # (the drawn belt here is thinner than round 2's: 0.84 by r5_sheets' measure; it is stretched the same way the
    # strike's thicker belt was squeezed, the buckle with it)
    sq = float(np.clip(b2["T"] / bl["T"], 0.85, 1.2))
    T, EASE = bl["T"], 40.0
    if abs(sq - 1.0) > 0.005:
        t_out = yy - y_top
        T2 = T * sq
        t_src = np.where(t_out < T2, t_out / sq, np.where(t_out < T + EASE, T + (t_out - T2) * EASE / (T + EASE - T2), t_out))
        t_src = np.where(t_out < 0, t_out, t_src)
        hp = cv2.remap(hp.astype(np.float32), xx, (y_top + t_src).astype(np.float32), cv2.INTER_LINEAR,
                       borderMode=cv2.BORDER_CONSTANT)
    Image.fromarray(np.clip(hp + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT / "hips_wind.png")
    waist = np.array(bl["centre"]) - np.array([0.0, T * (1 - sq) / 2])
    bw = boots(hp)
    b_r2 = boots(np.asarray(Image.open(REDO / "parts_r2" / "hips.png").convert("RGBA")).astype(np.float32))
    w2 = np.array(P2["waist"], float)
    off = waist - w2
    back = b_r2[0]["x"][0] - bw[0]["x"][0]          # keep the back boot's rear end where it stands
    toe = b_r2[1]["x"][1] - bw[1]["x"][1]           # keep the front boot's toe where it stands
    xm = (bl["x0"] + bl["x1"]) / 2
    wind = {
        "src": KEY, "dy": DY, "piece": "hips_wind",
        "belt": {k: round(v, 3) if isinstance(v, float) else v for k, v in bl.items()},
        "belt_r2": {"T": round(b2["T"], 1), "L": round(b2["L"], 1), "x": [b2["x0"], b2["x1"]],
                    "slope_deg": round(float(np.degrees(np.arctan(b2["slope"]))), 2)},
        "belt_squeeze": round(sq, 3),
        "belt_top_mid": round(float(bl["slope"] * xm + bl["icpt"]), 1),
        "belt_top_slope_deg": round(float(np.degrees(np.arctan(bl["slope"]))), 2),
        "belt_x": [bl["x0"], bl["x1"]],
        "waist": waist.round(1).tolist(),
        "boots": bw, "boots_r2": b_r2,
        "waist_offset_from_standing": {"as_drawn": off.round(1).tolist(),
                                       "back_foot_planted": [round(float(off[0] + back), 1), round(float(off[1]), 1)],
                                       "front_toe_planted": [round(float(off[0] + toe), 1), round(float(off[1]), 1)]},
        "upper_body": {"torso": "parts_r4/torso", "armhole": "parts_r4/armhole", "far_arm": "parts_r4/far_arm",
                       "head": "parts_r2/head", "tails": "parts_r2/tails"},
        # the upper body's joints (parts_r4, standing canvas: they move with the waist offset, the torso's dy and lean)
        "upper_body_joints_standing_frame": {k: P4[k] for k in ("waist", "neck", "S", "arm_root", "a1_drawn",
                                                                "far_shoulder", "knot")},
        "torso_dy_needed": 11.0,
        "torso_dy_why": ("parts_r4/torso carries the left half of the old buckle under its belt line (x 797-815 from y "
                         "700, standing frame); this belt is thinner at its top, so the torso sinks 11 px to keep it "
                         "under the belt (checked at lean -5)"),
        "sleeve_shading": "under",
    }
    old = json.loads(JSON.read_text())
    before = json.dumps(old, sort_keys=True)
    old.pop("wind", None)
    assert json.dumps(old, sort_keys=True) == json.dumps({k: v for k, v in json.loads(before).items() if k != "wind"},
                                                          sort_keys=True)
    new = dict(old)
    new["wind"] = wind
    for k, v in old.items():
        assert new[k] == v, k
    JSON.write_text(json.dumps(new, indent=1))
    print(json.dumps(wind, indent=1))


if __name__ == "__main__":
    main()
