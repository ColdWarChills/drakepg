"""Round 2 swing (kind B): one sword cut at 60 fps, played twice with idles, the body swinging with it.
<venv python> -I -B swing_r2.py dry      -> per-frame numbers only (no pictures): tip/wrist/elbow steps, grip turns
<venv python> -I -B swing_r2.py render   -> frames in $HJ_TMP/r2 (default art/redo/hero_joints/_frames_r2), then
                                            art/tests/hero_joints/r2/arm.mp4, arm.webp (half size), swing_r2.json

Channels are keyed per swing frame and joined by a monotone cubic (PCHIP: no overshoot between keys; every overshoot is
a key). The arm's keys are round 1's (two straight parts, a1 and b) and give the wrist; kind B solves its own elbow.
On top of the keys, everywhere: a slow breath (its period divides the clip, so the loop has no seam). The head counters
the chest by half its angle. The headband tails follow the head on a damped spring (lag, overshoot)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import math
import os
import subprocess

import numpy as np
from PIL import Image
from scipy.interpolate import PchipInterpolator

from jcommon import FFMPEG, RAW, REDO, TESTS

FPS = 60
CH = ["a1", "b", "turn", "lean", "dy", "sq", "sx", "sy", "far", "fx", "fy"]
# swing frame: a1, b, turn, lean, dy (upper body sinks +), sq (torso height), shoulder sx, sy, far arm angle, fx, fy
K = [
    (0, 70.0, 70.0, 44.5, 0.0, 0.0, 1.000, 0.0, 0.0, 0.0, 0.0, 0.0),
    (5, 72.0, 73.0, 43.5, 0.8, 1.5, 0.996, 0.0, 1.0, 0.5, 0.0, 0.0),       # a small dip before the raise
    (10, 102.0, 42.0, 50.0, -1.0, 2.5, 0.993, 0.0, -1.0, -1.5, 1.5, -0.5),   # backswing: the arm swings down...
    (15, 150.0, 6.0, 55.0, -3.5, 3.5, 0.988, -1.0, -5.0, -4.0, 4.0, -2.0),   # ...back past the hip, nearly straight
    (20, 190.0, -26.0, 60.0, -6.0, 4.2, 0.982, -1.0, -9.0, -7.0, 7.0, -3.5),  # ...and up behind the head
    (26, 208.0, -66.0, 65.0, -7.8, 4.5, 0.978, -1.0, -12.0, -9.0, 9.0, -5.0),  # wind-up overshoot
    (31, 215.0, -60.0, 62.0, -7.0, 4.0, 0.980, 0.0, -10.0, -8.0, 8.0, -4.0),   # WIND-UP held
    (38, 217.0, -58.0, 61.0, -6.6, 3.6, 0.982, 0.0, -9.5, -7.5, 7.5, -3.5),
    (44, 210.0, -63.0, 64.0, -7.6, 4.6, 0.976, -1.0, -11.0, -9.0, 9.0, -5.0),  # cock back
    (46, 208.0, -64.0, 65.0, -7.8, 4.8, 0.975, -1.0, -11.5, -9.0, 9.0, -5.0),  # the cut starts
    (47, 244.0, -38.0, 74.0, -4.5, 3.0, 0.990, 0.0, -8.0, -5.0, 4.0, -2.0),    # the blade lags (turn opens)
    (48, 284.0, -4.0, 78.0, 0.5, 0.5, 1.010, 0.0, -4.0, 0.0, -3.0, 2.0),
    (49, 326.0, 7.0, 58.0, 5.5, -2.0, 1.030, 0.0, 0.0, 4.0, -9.0, 5.0),
    (50, 372.0, 9.0, 30.0, 9.5, -3.0, 1.030, -2.0, 3.0, 7.0, -16.0, 9.0),
    (51, 424.0, 9.0, 10.0, 12.0, -2.0, 1.020, -3.0, 4.0, 10.0, -22.0, 12.0),     # strike overshoot (the blade whips)
    (57, 420.0, 8.0, 13.0, 11.5, 0.0, 1.005, -3.0, 4.0, 9.5, -21.0, 11.5),
    (61, 410.0, 6.0, 21.5, 9.5, 1.0, 1.000, -3.0, 3.0, 8.0, -18.0, 10.0),       # settle back under
    (65, 412.0, 7.0, 21.5, 10.0, 0.5, 1.000, -3.0, 3.0, 8.0, -18.0, 10.0),      # STRIKE held
    (73, 413.5, 7.5, 22.5, 10.4, 0.8, 1.000, -2.5, 3.3, 8.3, -18.5, 10.3),
    (81, 411.0, 6.5, 21.0, 9.8, 0.3, 1.000, -3.2, 2.8, 7.8, -17.5, 9.7),
    (87, 411.5, 7.0, 21.5, 10.0, 0.5, 1.000, -3.0, 3.0, 8.0, -18.0, 10.0),
    (98, 424.0, 42.0, 36.0, 3.0, 0.5, 1.000, -1.0, 1.0, 2.5, -6.0, 3.0),        # back to ready, eased
    (107, 431.5, 72.0, 45.5, -0.4, -0.3, 1.000, -0.3, -0.3, -0.4, 0.8, -0.4),  # a touch past ready
    (111, 430.0, 70.0, 44.5, 0.0, 0.0, 1.000, 0.0, 0.0, 0.0, 0.0, 0.0),
]
# segments re-timed along the blade tip's own path: (first key frame, last key frame, new first, new last, profile)
# the keys in between only shape the path; the frames get a smooth speed profile (tip steps rise and fall)
RETIME = [
    (5, 26, 5, 26, "sine"),   # the backswing
    (46, 51, 46, 54, "cut"),
    (87, 107, 87, 107, "sine"),
]
CUT_W = [0.025, 0.075, 0.16, 0.235, 0.235, 0.16, 0.075, 0.035]   # share of the cut's tip travel per frame
SWING_LEN = 111
CUT = (46, 56)                 # swing frames counted as "the cut" for the step report
STRETCH = {49: 1.12, 50: 1.16}  # the two fastest frames: the mid sleeve longer (cap and cuff keep their size)
IDLE_A, IDLE_MID, IDLE_B = 12, 30, 18
N_CLIP = IDLE_A + 2 * (SWING_LEN + 1) + IDLE_MID + IDLE_B
BREATH_T = N_CLIP / 2          # frames per breath: two breaths per clip, so the loop is seamless
SMEAR_MIN = 300.0              # px of tip travel in a frame for that frame's sweep to leave a smear
SMEAR_ALPHA = (0.42, 0.22, 0.10)
SMEAR_INNER = 0.5   # the smear's inner edge at the blade (share of the tip's radius); 1 at the tail


def channels(geo=None):
    """PCHIP channels over the keys; with geo, the RETIME segments are replaced by one key per frame, spaced so that
    the blade tip's steps follow a smooth profile."""
    keys = [list(k) for k in K]
    if geo is not None:
        for k0, k1, n0, n1, prof in RETIME:
            seg = [k for k in keys if k0 <= k[0] <= k1]
            vals = np.array([k[1:] for k in seg], float)
            # path parameter: the keys spaced by the tip's travel between them (fine-sampled)
            tk = np.array([k[0] for k in seg], float)
            pch = [PchipInterpolator(tk, vals[:, i]) for i in range(vals.shape[1])]
            ts = np.linspace(tk[0], tk[-1], 600)
            tips, wrs, els = [], [], []
            for t in ts:
                d = dict(zip(CH, [float(c(t)) for c in pch]))
                g = geo(pose_B(d, np.sign(d["b"]) or 1.0))
                tips.append(g["tip"]); wrs.append(g["W"]); els.append(g["E"])
            # travel = the tip's + twice the wrist's + twice the elbow's (so none of the three gets a lone spike)
            step = sum(wk * np.hypot(*np.diff(np.array(a), axis=0).T) for wk, a in ((1, tips), (2, wrs), (2, els)))
            arc = np.r_[0, np.cumsum(step)]
            n = n1 - n0
            if prof == "cut":
                w = np.array(CUT_W, float)
            else:
                w = np.sin(np.pi * (np.arange(n) + 0.5) / n)
            w = w / w.sum()
            target = np.r_[0, np.cumsum(w)] * arc[-1]
            t_of = np.interp(target, arc, ts)
            new = [[n0 + i] + [float(c(t)) for c in pch] for i, t in enumerate(t_of)]
            keys = [k for k in keys if not (k0 <= k[0] <= k1)] + new
        keys.sort(key=lambda k: k[0])
    t = np.array([k[0] for k in keys], float)
    vals = np.array([k[1:] for k in keys], float)
    return [PchipInterpolator(t, vals[:, i]) for i in range(vals.shape[1])]


def timeline():
    """[(clip frame, swing frame or None)]."""
    out = []
    f = 0
    for seg in (IDLE_A, None, IDLE_MID, None, IDLE_B):
        if seg is None:
            for s in range(SWING_LEN + 1):
                out.append((f, s))
                f += 1
        else:
            for _ in range(seg):
                out.append((f, None))
                f += 1
    return out


def raw_pose(chs, s, clipf):
    """Channel values at swing time s (None: ready) plus the breath at clip time clipf (floats allowed)."""
    v = [float(c(0.0 if s is None else s)) for c in chs]
    d = dict(zip(CH, v))
    ph = 2 * math.pi * clipf / BREATH_T
    d["dy"] += 1.3 * math.sin(ph)
    d["a1"] += 0.75 * math.sin(ph + 0.6)
    d["b"] += 0.4 * math.sin(ph + 0.9)
    d["lean"] += 0.35 * math.sin(ph + 0.3)
    d["turn"] += 0.8 * math.sin(ph + 1.2)
    d["far"] += 0.8 * math.sin(ph + 0.4)
    d["sy"] += 0.6 * math.sin(ph + 0.2)
    return d


def stretch_at(s):
    return STRETCH.get(s, 1.0) if s is not None and float(s).is_integer() else 1.0


def pose_B(d, sign, stretch=1.0):
    import rig_r2 as rig
    target = rig.wrist_A(d["a1"], d["b"])
    if stretch != 1.0:
        target = target * rig.stretched_len(stretch) / rig.L
    a1, b = rig.ik(target, sign, stretch)
    return dict(a1=a1, b=b, stretch=stretch, turn=d["turn"], lean=d["lean"], head=-0.5 * d["lean"], dy=d["dy"],
                sq=d["sq"], shoulder=(d["sx"], d["sy"]), far=d["far"], fx=d["fx"], fy=d["fy"])


class Geo:
    """The rig's geometry without pictures (for the dry run and the smear)."""

    def __init__(self, fr):
        import rig_r2 as rig
        self.rig, self.fr = rig, fr

    def __call__(self, pose):
        rig, fr = self.rig, self.fr
        mats, S = fr.body_mats(pose)
        wpt, F = rig.wrist_of(pose["a1"], pose["b"], pose.get("stretch", 1.0))
        W = S + wpt
        el, _ = rig.centre(np.array([rig.UE]), pose["a1"], pose["b"], pose.get("stretch", 1.0))
        f = fr.p.fist
        blade = F - pose["turn"]
        Mf = rig.rot_about(f["wrist"], W, blade - f["blade"])
        grip = rig.apply(Mf, f["grip"])
        tip = grip + rig.vec(blade, fr.p.tip_len)
        knot = rig.apply(mats["head"], fr.p.geo["knot"])
        return {"S": S, "W": W, "E": S + el[0], "F": F, "blade": blade, "grip": grip, "tip": tip, "knot": knot,
                "head_world": pose["lean"] + pose["head"]}


def build(fr):
    """All clip frames: pose, geometry, tails angle, smear polygons."""
    geo = Geo(fr)
    chs = channels(geo)
    tl = timeline()
    frames = []
    sign = 1.0
    for clipf, s in tl:
        d = raw_pose(chs, s, clipf)
        sign = np.sign(d["b"]) if abs(d["b"]) > 1e-6 else sign
        p = pose_B(d, sign, stretch_at(s))
        frames.append({"f": clipf, "s": s, "pose": p, "g": geo(p), "d": d, "sign": sign})
    # tails: a damped spring on the head's world angle (per frame), the tails turn by the lag x gain
    w0, zeta, gain = 2 * math.pi / 14.0, 0.32, 1.7   # period ~14 frames, under-damped (overshoot)
    phi = frames[0]["g"]["head_world"]
    vel = 0.0
    for _ in range(2):  # run twice: the second pass starts from the end state (seamless loop)
        for fr_ in frames:
            th = fr_["g"]["head_world"]
            for _sub in range(4):
                acc = w0 * w0 * (th - phi) - 2 * zeta * w0 * vel
                vel += acc / 4
                phi += vel / 4
            fr_["pose"]["tail"] = float(np.clip(gain * (phi - th), -14, 14))
    # smear: the sweep of the blade over the last three frames, sampled between frames
    for i, fr_ in enumerate(frames):
        shapes = []
        for age in (2, 1, 0):
            j = i - age
            if j < 1 or frames[j]["s"] is None or frames[j - 1]["s"] is None:
                continue
            t0, t1 = frames[j - 1], frames[j]
            travel = float(np.hypot(*(t1["g"]["tip"] - t0["g"]["tip"])))
            if travel < SMEAR_MIN:
                continue
            samples = []
            n = 8
            for k in range(n + 1):
                ss = t0["s"] + (t1["s"] - t0["s"]) * k / n
                cf = t0["f"] + k / n
                d = raw_pose(chs, ss, cf)
                st = 1.0  # sub-frame samples: no stretch except on the keyed frame itself
                if k == n:
                    st = stretch_at(t1["s"])
                p = pose_B(d, t1["sign"], st)
                samples.append(geo(p))
            shapes.append((samples, age, i))
        fr_["smear_src"] = shapes
    return frames


def smear_polys(fr_, tip_len, ox, oy):
    """Flat bands, newest strongest; each band from the blade's inner radius (narrowing toward the tail) to the tip."""
    out = []
    for samples, age, _ in fr_["smear_src"]:
        n = len(samples) - 1
        outer, inner = [], []
        for k, g in enumerate(samples):
            # position along the whole three-frame trail: 0 = oldest end, 1 = the blade now
            pos = (2 - age + k / n) / 3.0
            r_in = tip_len * (1 - (1 - SMEAR_INNER) * pos)
            from meshlib import vec
            outer.append(g["grip"] + vec(g["blade"], tip_len))
            inner.append(g["grip"] + vec(g["blade"], r_in))
        pts = np.array(outer + inner[::-1]) - np.array([ox, oy])
        out.append((pts, SMEAR_ALPHA[age]))
    return out


def numbers(frames):
    T = np.array([f["g"]["tip"] for f in frames])
    W = np.array([f["g"]["W"] for f in frames])
    E = np.array([f["g"]["E"] for f in frames])
    dT = np.r_[0, np.hypot(*np.diff(T, axis=0).T)]
    dW = np.r_[0, np.hypot(*np.diff(W, axis=0).T)]
    dE = np.r_[0, np.hypot(*np.diff(E, axis=0).T)]
    in_cut = np.array([f["s"] is not None and CUT[0] <= f["s"] <= CUT[1] for f in frames])
    rows = []
    for i, f in enumerate(frames):
        p = f["pose"]
        rows.append({"f": f["f"], "s": f["s"], "a1": round(p["a1"], 1), "b": round(p["b"], 1),
                     "turn": round(p["turn"], 1), "lean": round(p["lean"], 2), "head": round(p["head"], 2),
                     "dy": round(p["dy"], 2), "tail": round(p["tail"], 1), "stretch": p["stretch"],
                     "S": np.round(f["g"]["S"], 1).tolist(), "W": np.round(f["g"]["W"], 1).tolist(),
                     "tip": np.round(f["g"]["tip"], 1).tolist(), "tip_step": round(float(dT[i]), 1),
                     "wrist_step": round(float(dW[i]), 1), "elbow_step": round(float(dE[i]), 1),
                     "smear_bands": len(f.get("smear_src", []))})
    S = np.array([f["g"]["S"] for f in frames])
    S0 = S[0]
    summary = {
        "frames": len(frames),
        "tip_step_max_in_cut": round(float(dT[in_cut].max()), 1),
        "tip_step_max_outside_cut": round(float(dT[~in_cut].max()), 1),
        "tip_step_outside_frame": int(frames[int(np.argmax(np.where(in_cut, 0, dT)))]["f"]),
        "wrist_step_max_outside_cut": round(float(dW[~in_cut].max()), 1),
        "elbow_step_max_outside_cut": round(float(dE[~in_cut].max()), 1),
        "tip_steps_cut": [round(float(v), 0) for v in dT[in_cut][:len(range(CUT[0], CUT[1] + 1))]],
        "turn_min": round(min(r["turn"] for r in rows), 1), "turn_max": round(max(r["turn"] for r in rows), 1),
        "turn_below_20": [(r["f"], r["s"], r["turn"]) for r in rows if r["turn"] < 20],
        "lean_range": [round(min(r["lean"] for r in rows), 1), round(max(r["lean"] for r in rows), 1)],
        "head_range": [round(min(r["head"] for r in rows), 1), round(max(r["head"] for r in rows), 1)],
        "shoulder_dx_range": [round(float((S[:, 0] - S0[0]).min()), 1), round(float((S[:, 0] - S0[0]).max()), 1)],
        "shoulder_dy_range": [round(float((S[:, 1] - S0[1]).min()), 1), round(float((S[:, 1] - S0[1]).max()), 1)],
        "tail_range": [min(r["tail"] for r in rows), max(r["tail"] for r in rows)],
        "smear_frames": [r["f"] for r in rows if r["smear_bands"]],
    }
    return rows, summary


def background(fr):
    """bg_fen_road, cut and scaled as the game places it behind the hero (design 1280x720, hero 240 design px tall
    = this drawing's 1028 px; feet at design y 500, x 330; the background lifted 90 design px) - as round 1."""
    bg = Image.open(RAW / "bg_fen_road.png").convert("RGB")
    k = 1028 / 240
    sx = bg.width / 1280

    def col(x):
        return (330 + (x - 760) / k) * sx

    def row(y):
        return (500 + (y - 1300) / k + 90) * sx
    box = (col(fr.ox), row(fr.oy), col(fr.ox + fr.w), row(fr.oy + fr.h))
    im = bg.resize((fr.w, fr.h), Image.BICUBIC, box=box)
    return np.asarray(im).astype(np.float32) / 255


def encode(frames_dir, out_mp4, out_webp):
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(frames_dir / "f%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", "-r", str(FPS), str(out_mp4)], check=True)
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(frames_dir / "f%04d.png"),
                    "-vf", "scale=iw/2:ih/2:flags=lanczos", "-c:v", "libwebp_anim", "-lossless", "0", "-q:v", "80",
                    "-loop", "0", str(out_webp)], check=True)


def main():
    import rig_r2 as rig
    mode = sys.argv[1] if len(sys.argv) > 1 else "dry"
    parts = rig.Parts()
    fr = rig.Frame(parts)
    frames = build(fr)
    rows, summary = numbers(frames)
    print(json.dumps(summary, indent=0))
    if mode == "dry":
        for r in rows:
            if r["s"] is not None and (r["s"] <= 34 or 42 <= r["s"] <= 62 or r["s"] >= 84) and r["f"] < 160:
                print(r["f"], r["s"], "turn", r["turn"], "lean", r["lean"], "tail", r["tail"], "tip", r["tip_step"],
                      "wrist", r["wrist_step"], "elbow", r["elbow_step"], "smear", r["smear_bands"])
        return
    out = pathlib.Path(os.environ.get("HJ_TMP", str(REDO / "_frames_r2")))
    out.mkdir(parents=True, exist_ok=True)
    bg = background(fr)
    flipped = 0
    resume = "--resume" in sys.argv   # keep frames already rendered, except those with a smear (scratch frames only)
    for f in frames:
        if resume and not f["smear_src"] and (out / f"f{f['f']:04d}.png").exists():
            continue
        smear = smear_polys(f, parts.tip_len, fr.ox, fr.oy) if f["smear_src"] else None
        px, info = fr.render(f["pose"], smear=smear)
        flipped += info["flipped"]
        img = px[..., :3] + bg * (1 - px[..., 3:4])
        Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(out / f"f{f['f']:04d}.png")
        if f["f"] % 20 == 0:
            print("frame", f["f"], flush=True)
    summary["flipped_triangles"] = flipped
    (TESTS / "r2" / "swing_r2.json").write_text(json.dumps({"summary": summary, "frames": rows}, indent=0))
    encode(out, TESTS / "r2" / "arm.mp4", TESTS / "r2" / "arm.webp")
    print("done", out)


if __name__ == "__main__":
    main()
