"""Part 3: the sword cut, rendered per kind at 60 fps, with per-frame numbers.
<venv python> -I -B swing.py [kinds e.g. ABC] [--frames-only]
-> art/tests/hero_joints/arm_<K>.mp4 / .webp (half size), arm_kinds.mp4 / .webp, swing_numbers.json, and the frames in
   the scratch folder given by HJ_TMP (default art/redo/hero_joints/_frames, deleted by nobody).

Timeline (frames at 60 fps): ready -> raise (eased, small overshoot) -> wind-up held, drifting -> a small cock back ->
the cut in 5 frames (the blade lags, then whips) -> overshoot, settle -> strike held, drifting -> back to ready, eased.
Played twice with an idle between. Channels are interpolated with a monotone cubic (no overshoot between keys: every
overshoot is a key). The wrist follows kind A's arm (the arc); kinds B and C solve their own elbow for that wrist."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import os
import shutil
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.interpolate import PchipInterpolator

import keys
import rig
from jcommon import FFMPEG, RAW, REDO, TESTS
from meshlib import vec

FPS = 60
CH = ["a1", "b", "turn", "lean", "head", "far", "sx", "sy"]
# frame: a1, b, turn, lean, head, far, shoulder dx, dy
K = [
    (0, 70.0, 70.0, 44.5, 0.0, 0.0, 0.0, 0.0, 0.0),
    (6, 69.0, 71.0, 45.0, 0.3, -0.2, 0.5, 0.0, -1.0),
    (16, -40.0, 4.0, 72.0, -1.5, 0.8, 1.5, -1.0, -5.0),     # mid raise: arm up in front, straight (the bend changes side)
    (24, -152.0, -66.0, 100.0, -3.4, 1.8, 3.2, -5.0, -9.0),  # wind-up overshoot
    (28, -145.0, -60.0, 96.8, -3.0, 1.5, 3.0, -4.0, -8.0),   # WIND (A3)
    (36, -143.0, -58.0, 98.0, -2.7, 1.3, 2.8, -4.0, -7.0),
    (43, -150.0, -63.0, 106.0, -3.4, 1.7, 3.3, -5.0, -9.0),  # cock back
    (45, -152.0, -64.0, 108.0, -3.5, 1.8, 3.4, -5.0, -9.0),  # the cut starts
    (46, -114.0, -36.0, 111.0, -1.6, 1.0, 1.6, -3.0, -8.0),  # the blade lags (turn opens)
    (47, -74.0, -3.0, 104.0, 0.4, 0.0, -0.2, 0.0, -6.0),     # over the top, arm straight
    (48, -32.0, 7.0, 76.0, 2.6, -1.3, -2.0, 5.0, -2.0),
    (49, 12.0, 9.0, 42.0, 4.8, -2.4, -3.8, 10.0, 3.0),
    (50, 64.0, 9.0, 20.5, 6.0, -3.0, -4.5, 12.0, 5.0),       # strike overshoot (the blade whips past)
    (53, 60.0, 8.0, 21.0, 5.8, -2.9, -4.4, 11.5, 4.8),
    (57, 49.0, 6.0, 23.0, 4.6, -2.3, -3.8, 9.5, 3.6),        # settle back under
    (61, 52.0, 7.0, 21.5, 5.0, -2.5, -4.0, 10.0, 4.0),       # STRIKE (A3)
    (68, 53.5, 7.5, 22.5, 5.3, -2.6, -4.2, 10.5, 4.3),
    (74, 51.0, 6.5, 21.8, 4.8, -2.4, -3.9, 9.8, 3.8),
    (78, 51.5, 7.0, 22.0, 4.9, -2.5, -4.0, 10.0, 4.0),
    (86, 63.0, 38.0, 33.0, 2.5, -1.2, -2.0, 5.0, 2.0),
    (94, 71.5, 72.0, 45.5, -0.3, 0.2, 0.3, 0.0, 0.0),        # ready, a touch past
    (100, 70.0, 70.0, 44.5, 0.0, 0.0, 0.0, 0.0, 0.0),
]
SWING_LEN = 100
# stretch on B and C in the fastest frames (mid sleeve only; the cap and the cuff keep their size)
STRETCH = {47: 1.15, 48: 1.22, 49: 1.10}
IDLE_A, IDLE_MID, IDLE_B = 12, 30, 18   # frames of idle before, between and after the two swings


def channels():
    t = np.array([k[0] for k in K], float)
    vals = np.array([k[1:] for k in K], float)
    return [PchipInterpolator(t, vals[:, i]) for i in range(vals.shape[1])]


def timeline():
    """[(clip frame, swing frame or None)]: idle frames hold ready with a slow breath."""
    out = []
    f = 0
    for _ in range(IDLE_A):
        out.append((f, None)); f += 1
    for s in range(SWING_LEN + 1):
        out.append((f, s)); f += 1
    for _ in range(IDLE_MID):
        out.append((f, None)); f += 1
    for s in range(SWING_LEN + 1):
        out.append((f, s)); f += 1
    for _ in range(IDLE_B):
        out.append((f, None)); f += 1
    return out


def pose_at(chs, s, clipf):
    if s is None:
        br = np.sin(clipf / FPS * 2 * np.pi * 0.6)  # idle breath
        v = [70.0 + 0.6 * br, 70.0 - 0.5 * br, 44.5, 0.25 * br, -0.15 * br, 0.5 * br, 0.0, -1.0 * br]
    else:
        v = [float(c(s)) for c in chs]
    d = dict(zip(CH, v))
    pose = dict(a1=d["a1"], b=d["b"], turn=d["turn"], lean=d["lean"], head=d["head"], far=d["far"],
                shoulder=(d["sx"], d["sy"]), fist=keys.fist_for(d["turn"]))
    st = STRETCH.get(s, 1.0) if s is not None else 1.0
    return pose, st


def background(fr):
    """bg_fen_road, cut and scaled as the game places it behind the hero (design 1280x720, hero 240 design px tall
    = this drawing's 1028 px; feet at design y 500, x 330; the background lifted 90 design px)."""
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


def gate(info):
    """The grip rule per frame: turn inside 20..120 (back of the hand: +25..+120; the strike's drawn 21.5 accepted),
    and within 28 of the fist picture's drawn turn."""
    t = info["turn"]
    ok = 20.0 <= t <= 120.0 and abs(info["delta"]) <= 28.0
    return ok


def render_all(kinds, outdir):
    parts = rig.Parts()
    fr = rig.Frame(parts)
    bg = background(fr)
    chs = channels()
    tl = timeline()
    nums = {}
    for kind in kinds:
        d = outdir / kind
        d.mkdir(parents=True, exist_ok=True)
        rows = []
        prev_sign = 1.0
        for clipf, s in tl:
            pose, st = pose_at(chs, s, clipf)
            if kind != "A":
                pose["stretch"] = st
            sign = np.sign(pose["b"]) if abs(pose["b"]) > 1e-6 else prev_sign
            prev_sign = sign
            p = keys.pose_for(kind, pose, sign)
            px, info = fr.render(kind, p)
            out = px[..., :3] + bg * (1 - px[..., 3:4])
            Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(d / f"f{clipf:04d}.png")
            el = info["S"] + rig.centre(kind, np.array([rig.UE]), p["a1"], p["b"], p.get("stretch", 1.0))[0][0]
            rows.append({"f": clipf, "s": s, "a1": round(p["a1"], 2), "b": round(p["b"], 2), "stretch": st if kind != "A" else 1.0,
                         "W": np.round(info["W"], 1).tolist(), "elbow": np.round(el, 1).tolist(),
                         "tip": np.round(info["tip"], 1).tolist(), "turn": round(info["turn"], 1),
                         "delta": round(info["delta"], 1), "fist": info["fist"], "flipped": info["flipped"],
                         "gate": gate(info)})
        nums[kind] = rows
        print(kind, "frames", len(rows), "gate fails", sum(not r["gate"] for r in rows),
              "flipped", sum(r["flipped"] for r in rows), flush=True)
    return nums


def motion_numbers(rows):
    """Per-frame displacement of wrist, elbow and blade tip; a pop = a frame whose elbow step is much bigger than
    both neighbours' (second difference)."""
    W = np.array([r["W"] for r in rows])
    E = np.array([r["elbow"] for r in rows])
    T = np.array([r["tip"] for r in rows])
    dW = np.r_[0, np.hypot(*np.diff(W, axis=0).T)]
    dE = np.r_[0, np.hypot(*np.diff(E, axis=0).T)]
    dT = np.r_[0, np.hypot(*np.diff(T, axis=0).T)]
    acc = np.r_[0, np.hypot(*np.diff(E, 2, axis=0).T), 0]
    return {"max_wrist_step": float(dW.max()), "max_elbow_step": float(dE.max()), "max_tip_step": float(dT.max()),
            "max_elbow_accel": float(acc.max()), "elbow_accel_frame": int(rows[int(acc.argmax())]["f"]),
            "elbow_steps_cut": [round(float(v), 1) for v in dE[[i for i, r in enumerate(rows) if r["s"] is not None and 43 <= r["s"] <= 62]][:20]],
            "wrist_steps_cut": [round(float(v), 1) for v in dW[[i for i, r in enumerate(rows) if r["s"] is not None and 43 <= r["s"] <= 62]][:20]]}


def encode(frames_dir, out_mp4, out_webp=None, scale=1.0):
    vf = [] if scale == 1.0 else ["-vf", f"scale=iw*{scale}:ih*{scale}:flags=lanczos"]
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(frames_dir / "f%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", "-r", str(FPS), str(out_mp4)], check=True)
    if out_webp:
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(frames_dir / "f%04d.png"),
                        "-vf", "scale=iw/2:ih/2:flags=lanczos", "-c:v", "libwebp_anim", "-lossless", "0", "-q:v", "80",
                        "-loop", "0", str(out_webp)], check=True)


def font(sz):
    from jcommon import ROOT
    for f in (ROOT / "unity/Assets/Resources/Fonts").glob("*.ttf"):
        return ImageFont.truetype(str(f), sz)
    return ImageFont.load_default()


def side_by_side(outdir, kinds, n):
    """The three kinds stacked (half size each), labelled in plain words."""
    d = outdir / "kinds"
    d.mkdir(exist_ok=True)
    f = font(22)
    for i in range(n):
        tiles = [Image.open(outdir / k / f"f{i:04d}.png") for k in kinds]
        w, h = tiles[0].width // 2, tiles[0].height // 2
        sheet = Image.new("RGB", (w, (h + 34) * len(tiles)), (20, 16, 28))
        dr = ImageDraw.Draw(sheet)
        for j, (k, t) in enumerate(zip(kinds, tiles)):
            sheet.paste(t.resize((w, h), Image.LANCZOS), (0, j * (h + 34) + 34))
            dr.text((10, j * (h + 34) + 5), rig.KINDS[k], fill=(240, 230, 200), font=f)
        sheet.save(d / f"f{i:04d}.png")
    return d


if __name__ == "__main__":
    kinds = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "ABC"
    outdir = pathlib.Path(os.environ.get("HJ_TMP", str(REDO / "_frames")))
    nums = render_all(kinds, outdir)
    summary = {k: motion_numbers(v) for k, v in nums.items()}
    for k, v in summary.items():
        print(k, json.dumps(v))
    prev = json.loads((TESTS / "swing_numbers.json").read_text()) if (TESTS / "swing_numbers.json").exists() else {}
    prev.update({k: {"summary": summary[k], "frames": nums[k]} for k in nums})
    (TESTS / "swing_numbers.json").write_text(json.dumps(prev, indent=0))
    if "--frames-only" in sys.argv:
        sys.exit(0)
    n = len(timeline())
    for k in kinds:
        encode(outdir / k, TESTS / f"arm_{k}.mp4", TESTS / f"arm_{k}.webp")
    if len(kinds) == 3:
        d = side_by_side(outdir, kinds, n)
        encode(d, TESTS / "arm_kinds.mp4", None)
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(d / "f%04d.png"),
                        "-c:v", "libwebp_anim", "-lossless", "0", "-q:v", "80", "-loop", "0",
                        str(TESTS / "arm_kinds.webp")], check=True)
    print("done")
