"""The grip check: the sword is in his right hand (the near arm) and held the right way round.

Angles on screen: 0 = screen-right (his front), 90 = up, 180 = his back, 270 = down (picture y grows down).
  F = the forearm, elbow -> wrist (ARMS: points read by hand on the drawing; `auto F` is a cross-check from the skin).
  T = the direction the blade leaves the fist (from the two-colour marker; BLADE_BY_EYE for drawings made before it).
  turn = T - F, wrapped to (-180, 180]; + is counter-clockwise.
A right hand in a normal grip: back of the hand drawn -> turn +25..+120; palm side drawn -> turn -25..-120.
The eye notes (EYE) are written from the raw crop before the numbers are run: the side of the hand drawn, the screen
direction the thumb end of the fist points, and whether the sword arm is the near arm. Code then compares them with the
numbers; any disagreement fails, with no override.

<venv python> -I -B grip.py test            the four step-A drawings: must say ready pass, strike pass, wind fail, follow fail
<venv python> -I -B grip.py try <try> ...   the code side only for new tries (T, auto F, sword tip), before eye notes
<venv python> -I -B grip.py table           art/tests/moments/hands_table.md for the kept moments (moments.json)
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CANVAS, REDO, TESTS, load_rgba  # noqa: E402
import bare  # noqa: E402
import cut  # noqa: E402
import place  # noqa: E402

MIN_TURN, MAX_TURN = 25, 120
LIMIT_TURN = 20  # 20..25: "at the wrist's limit"; accepted only for a pre-marker drawing checked by eye (step-A strike)

# Elbow and wrist of the sword arm, read by hand on the 1536 px drawing named (canvas px). F is a direction, so the
# norm_ copy of a try (scaled about the soles) has the same F.
ARMS = {
    "plain_short_ready_s112": {"elbow": (690, 735), "wrist": (850, 745), "on": "norm"},
    "plain_short_wind_s212": {"elbow": (528, 590), "wrist": (522, 455), "on": "norm"},
    "plain_short_strike_s311": {"elbow": (905, 915), "wrist": (985, 995), "on": "norm"},
    "plain_short_follow_s411": {"elbow": (705, 1010), "wrist": (765, 1110), "on": "norm"},
    "grip_followb_s611": {"elbow": (735, 775), "wrist": (775, 905), "on": "raw"},
    # the hand pass kept step A's wind-up forearm untouched: its points (read on norm_plain_short_wind_s212)
    "handmwind_s707": {"elbow": (528, 590), "wrist": (522, 455), "on": "norm of plain_short_wind_s212"},
    "grip4_followb3_s721": {"elbow": (740, 800), "wrist": (800, 945), "on": "raw"},
}

# Eye notes, written from the raw crop (the marker still in) before running the numbers.
#   side: "back" (flat back of the hand and the knuckle row; fingertips hidden) or "palm" (curled fingers toward us)
#   thumb: screen angle the thumb end of the fist points to; near: the sword arm is the near arm (in front of the torso)
EYE = {
    "plain_short_ready_s112": {"side": "back", "thumb": 45, "near": True,
                               "note": "knuckle row on top, fingertips hidden; thumb at the upper right, by the blade"},
    "plain_short_strike_s311": {"side": "back", "thumb": -20, "near": True,
                                "note": "back of the hand and knuckles toward us; thumb at the front end, by the blade"},
    "plain_short_wind_s212": {"side": "palm", "thumb": 0, "near": True,
                              "note": "curled fingers toward us; thumb on top at the right end, toward his front"},
    "plain_short_follow_s411": {"side": "back", "thumb": 30, "near": True,
                                "note": "back of the hand and knuckles toward us; thumb at the upper right end"},
    "grip_followb_s611": {"side": "back", "thumb": -30, "near": True,
                          "note": "flat back of the hand and the knuckle row toward us, fingertips hidden; the thumb tip "
                                  "shows at the lower right end of the fist, where the green leaves; the arm hangs in "
                                  "front of his legs"},
    # written from the raw crops; the code's T for these two had already been printed once by `grip.py try`
    "handmwind_s707": {"side": "back", "thumb": 180, "near": True,
                       "note": "the flat back of the hand and the knuckle row along the top of the fist face us, the "
                               "fingers fold away over the stick and their tips are hidden; the thumb tip curls over "
                               "the stick at the left end of the fist, toward his back, where the green leaves; the "
                               "raised arm is his right arm, its sleeve over the near edge of his torso"},
    "grip4_followb3_s721": {"side": "back", "thumb": -20, "near": True,
                            "note": "the flat back of the hand and the knuckles face us, fingertips hidden under the "
                                    "grip; the thumb curls round the grip at the front (right) end of the fist, where "
                                    "the green leaves; the arm hangs in front of his trousers"},
}


def ang(v) -> float:
    return math.degrees(math.atan2(-v[1], v[0]))


def wrap(d: float) -> float:
    d = (d + 180) % 360 - 180
    return 180.0 if d == -180 else d


def auto_F(a: np.ndarray, rod: dict):
    """F found from the arm: skin of the sword arm (not the head) in a ring 50-170 px round the fist, its long axis,
    pointed toward the fist. A cross-check of the hand-read F only."""
    c = place.classes(a)
    skin = c["skin"] & ~place.head_box(c)
    H, W = skin.shape
    cx, cy = rod["center"]
    YY, XX = np.mgrid[0:H, 0:W]
    r2 = (XX - cx) ** 2 + (YY - cy) ** 2
    fist = cut.fist_mask(a, rod)
    lab, _ = ndi.label(skin & (r2 < 170 ** 2))
    ids = set(np.unique(lab[ndi.binary_dilation(fist, iterations=3)])) - {0}
    ring = np.isin(lab, list(ids)) & (r2 > 50 ** 2) & ~ndi.binary_dilation(cut.marker_mask(a), iterations=6)
    ys, xs = np.nonzero(ring)
    if len(xs) < 200:
        return None
    p = np.stack([xs, ys], 1).astype(float)
    mu = p.mean(0)
    w, v = np.linalg.eigh(np.cov((p - mu).T))
    d = v[:, np.argmax(w)]
    if (np.array([cx, cy]) - mu) @ d < 0:
        d = -d
    return ang(d)


def soles_y(a: np.ndarray) -> int:
    """His soles: the lowest row of the figure (marker and weapon not in it) that is at least 12 px wide."""
    m = (a[..., 3] > 128) & ~ndi.binary_dilation(cut.marker_mask(a), iterations=4)
    rows = np.nonzero(m.sum(1) >= 12)[0]
    return int(rows.max())


def measure(name: str, moment: str | None = None) -> dict:
    a = load_rgba(REDO / f"{name}.png")
    m = moment or bare.moment_of(name)
    p = bare.build(a, m, name)
    rod = p["rod"]
    key = bare.drawing_key(name)
    T = ang(rod["d"])
    out = {"name": name, "key": key, "moment": m, "source": rod.get("source", "?"), "T": T, "autoF": auto_F(a, rod),
           "center": [round(float(v)) for v in rod["center"]], "order": p["order"]}
    wy, wx = np.nonzero(p["weapon"][..., 3] > 40)
    sy = soles_y(p["body"])
    out["soles_y"] = sy
    out["tip_low_y"] = int(wy.max())
    out["tip_ok"] = bool(wy.max() <= sy + TIP_BELOW_OK)  # 3/4 view: up to TIP_BELOW_OK below the soles rests on the road
    out["off_canvas"] = bool(wx.min() <= 2 or wy.min() <= 2 or wx.max() >= CANVAS - 3)
    if key in ARMS:
        e, w = ARMS[key]["elbow"], ARMS[key]["wrist"]
        out["F"] = ang((w[0] - e[0], w[1] - e[1]))
        out["turn"] = wrap(T - out["F"])
    return out


TIP_BELOW_OK = 60  # px of the 1536 drawing: a tip this far below the soles line can still rest on the road (3/4 view)


def reach(name: str) -> dict:
    """Room for a steep blade on a raw try, after head normalisation (as it would be kept): the fist's height above
    the soles, T, auto F, and the steepest blade whose real-sword tip stays within TIP_BELOW_OK of the soles line."""
    a = load_rgba(REDO / f"{name}.png")
    if not name.startswith("norm_"):
        a = bare.normalize(a, json.loads((REDO / "moments.json").read_text())["head_ref"])[0]
    rod = bare.read_blade(a, name)
    w = cut.weapon_layer((1200, 400), {**rod, "center": np.array([200.0, 100.0]), "angle": 180.0})  # blade down
    L = float(np.nonzero(w[..., 3] > 40)[0].max() - 100.0)  # its reach below the grip
    sy = soles_y(cut.remove_rod(a, rod["mask"]))
    h = sy - float(rod["center"][1])
    s = (h + TIP_BELOW_OK) / L
    tmax = -math.degrees(math.asin(min(1.0, max(-1.0, s))))
    T = ang(rod["d"])
    tip_below = float(rod["center"][1] - sy + L * math.sin(math.radians(-T)))
    return {"fist_above_soles": round(h), "T": round(T, 1), "autoF": round(auto_F(a, rod) or 0, 1),
            "steepest_T_tip_ok": round(tmax, 1), "tip_below_soles": round(tip_below)}


def judge(r: dict) -> dict:
    """Adds must / drawn / thumb_ok / near / verdict to a measured row (needs ARMS and EYE for its drawing)."""
    t = r.get("turn")
    eye = EYE.get(r["key"])
    if t is None or eye is None:
        r["verdict"] = "not judged (no ARMS or EYE entry)"
        return r
    at = abs(t)
    must = "back" if t > 0 else "palm"
    r["must"] = must if at >= LIMIT_TURN and at <= MAX_TURN else ("none: too straight" if at < LIMIT_TURN else
                                                                  "none: bent too far")
    r["limit"] = LIMIT_TURN <= at < MIN_TURN
    r["drawn"] = eye["side"]
    r["thumb_off"] = abs(wrap(eye["thumb"] - r["T"]))
    r["thumb_ok"] = r["thumb_off"] <= 45
    r["near"] = eye["near"]
    fails = []
    if r["must"] != r["drawn"]:
        fails.append(f"{r['drawn']} drawn, {r['must']} must show")
    if not r["thumb_ok"]:
        fails.append(f"thumb {eye['thumb']} vs blade {r['T']:.0f}")
    if not r["near"]:
        fails.append("not the near arm")
    if r["limit"] and r["source"] != "eye":
        fails.append(f"turn {t:.0f} under {MIN_TURN}")
    if not r["tip_ok"]:
        fails.append("sword tip below the soles")
    r["fails"] = fails
    r["verdict"] = "FAIL: " + "; ".join(fails) if fails else ("pass, at the wrist's limit" if r["limit"] else "pass")
    r["note"] = eye["note"]
    return r


def line(r: dict) -> str:
    f = lambda v: "-" if v is None else f"{v:.0f}"  # noqa: E731
    return (f"{r['moment']:7s} {r['name']:34s} F {f(r.get('F'))} (auto {f(r.get('autoF'))})  T {f(r['T'])}  "
            f"turn {f(r.get('turn'))}  must {r.get('must', '-')}  drawn {r.get('drawn', '-')}  thumb "
            f"{'ok' if r.get('thumb_ok') else 'NO'} ({f(r.get('thumb_off'))})  near {r.get('near', '-')}  tip "
            f"{'ok' if r['tip_ok'] else 'LOW'} (low {r['tip_low_y']}, soles {r['soles_y']}{', off canvas' if r['off_canvas'] else ''})  -> {r['verdict']}")


STEP_A = {"ready": "norm_plain_short_ready_s112", "wind": "norm_plain_short_wind_s212",
          "strike": "norm_plain_short_strike_s311", "follow": "norm_plain_short_follow_s411"}
EXPECT = {"ready": "pass", "wind": "FAIL", "strike": "pass", "follow": "FAIL"}


def test():
    ok = True
    for m, n in STEP_A.items():
        r = judge(measure(n, m))
        r["tip_ok"] = True  # step A's sword tip is a known, separate fault (part 2 of the soles); not the grip
        r = judge(r)
        good = r["verdict"].startswith(EXPECT[m])
        ok &= good
        print(("OK  " if good else "BAD ") + line(r))
    print("grip check test:", "as expected" if ok else "NOT as expected")
    return ok


TABLE_HEAD = ("| moment | drawing | F | T | turn | must show | drawn | thumb at the blade end | near arm | sword tip | "
              "clean drawing | clean fist: overlap, centre moved, marker left | verdict |\n"
              "|---|---|---|---|---|---|---|---|---|---|---|---|---|\n")
BEGIN, END = "<!-- grip.py table: begin (rewritten by `grip.py table`) -->", "<!-- grip.py table: end -->"


def tip_words(r: dict) -> str:
    d = r["tip_low_y"] - r["soles_y"]  # + = below the soles line
    if not r["tip_ok"]:
        return f"TOO LOW or off the picture ({d} px below the soles)"
    return f"{-d} px above the soles" if d <= 0 else f"{d} px below the soles, on the road"


def table():
    """Rewrites only the block between BEGIN and END in hands_table.md; everything outside it (the hand-written eye
    notes on the clean drawings and the look files) is kept as it is."""
    p = json.loads((REDO / "moments.json").read_text())
    cc = json.loads((REDO / "clean_checks.json").read_text()) if (REDO / "clean_checks.json").exists() else {}
    rows = []
    for m in ["ready", "wind", "strike", "follow"]:
        r = judge(measure(p[m], m))
        print(line(r))
        rows.append(r)
    txt = [BEGIN, "\n\n",
           "Angles on screen: 0 = his front (screen-right), 90 = up, 180 = his back, 270 = down. F = forearm, elbow to "
           "wrist (points read by hand on the 1536 px drawing; `auto` = found from the arm's skin, a cross-check). T = "
           "where the blade leaves the fist (from the two-colour marker; ready and strike, drawn before the marker, "
           "from their blade end checked by eye). turn = T - F, + = counter-clockwise. Back of the hand drawn needs "
           "+25..+120, palm side drawn needs -25..-120. Side drawn, thumb and near arm are eye notes written from the "
           "raw crop (`EYE` in grip.py); the thumb column compares the thumb end seen with T (within 45). Sword tip: "
           f"the real training sword's lowest pixel against the soles line (up to {TIP_BELOW_OK} px below still rests "
           "on the road in this 3/4 view). Clean fist (clean.py, clean_checks.json): skin overlap of the sword fist, "
           "marker drawing vs clean drawing (IoU, needs >= 0.85), its centre moved (px, needs <= 3), marker colour or "
           "fringe left (px, needs 0).\n\n", TABLE_HEAD]
    for r in rows:
        cn = p.get("clean", {}).get(r["moment"])
        c = cc.get(cn, {}) if cn else {}
        cl = (f"{c['fist_iou']:.2f}, {c['fist_centre_moved_px']:.1f} px, {c['marker_left_px']} px" if c else "-")
        txt.append(f"| {r['moment']} | {r['name']} | {r['F']:.0f} (auto {r['autoF']:.0f}) | {r['T']:.0f} | "
                   f"{r['turn']:+.0f} | {r['must']} | {r['drawn']} | {'yes' if r['thumb_ok'] else 'NO'} (thumb seen "
                   f"at {EYE[r['key']]['thumb']}) | {'yes' if r['near'] else 'NO'} | "
                   f"{tip_words(r)} | "
                   f"{cn or '-'} | {cl} | {r['verdict']}{'' if not c or c.get('pass') else '; clean fist FAILS'} |\n")
    txt.append("\nWhat the eye saw in each sword hand (raw crop, written before or beside the numbers):\n\n")
    for r in rows:
        txt.append(f"- {r['moment']}: {r['note']}.\n")
    txt.append("\n" + END)
    f = TESTS / "hands_table.md"
    old = f.read_text(encoding="utf-8") if f.exists() else ""
    if BEGIN in old and END in old:
        head, rest = old.split(BEGIN, 1)
        tail = rest.split(END, 1)[1]
    else:  # first run on step A2's file: its text was all made by the old table(); start fresh, keep a title
        head, tail = "# The sword hand in the four moments (grip check grip.py, clean fist clean.py)\n\n", "\n"
    f.write_text(head + "".join(txt) + tail, encoding="utf-8")
    print(f)


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "test":
        test()
    elif cmd == "try":
        for n in sys.argv[2:]:
            try:
                print(line(judge(measure(n))))
                print("   ", reach(n))
            except ValueError as e:
                print(n, "->", e)
    elif cmd == "table":
        table()
