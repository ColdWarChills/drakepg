"""Round 4, step 2: the comparison of today's hero and hero 4 (Unity 2D Animation) doing the sword attack in battle.
python -I -B r4_compare.py <old take dir> <new take dir>
Both take dirs hold the lossless game frames p004..p024.png (2560x1440, the 1440p game size) and track.json from
Test2DRun/BonesCapture (the press is on take frame 6 in both).

-> art/tests/hero_joints/r4/attack_old_new.mp4  old | new at ONE fixed framing, game speed (30 fps) looped twice,
                                                then once at quarter speed
   art/tests/hero_joints/r4/frames_old.png / frames_new.png  every frame from the wind-up start (f6) to the end of the
                                                strike (f15), game size, a window that follows the feet, in rows
   art/tests/hero_joints/r4/joints_3x.png       hero 4's shoulder, elbow and fist at 3x on the ready, the swap frame
                                                and its neighbours, the wind-up hold, the swap back and the strike"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw

from jcommon import FFMPEG, TESTS

OUT = TESTS / "r4"
OUT.mkdir(parents=True, exist_ok=True)
OLD, NEW = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
FRAMES = range(4, 25)                 # the lossless frames of both takes
FRAMING = (250, 150, 2150, 1250)      # ONE framing for the clip: home spot to past the rat, head room for the sword
STRIP = range(6, 16)                  # wind-up start .. end of the strike
NAMES = {6: "lift", 7: "up", 8: "swap", 9: "hold", 10: "cut", 11: "strike", 12: "", 13: "", 14: "", 15: "end"}


def frame(d, i):
    return Image.open(d / f"p{i:03d}.png").convert("RGB")


def track(d):
    return {f["i"]: f for f in json.loads((d / "track.json").read_text())["frames"]}


def label(im, text, size=1):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 12 + 7 * len(text), 22], fill=(255, 255, 255))
    d.text((6, 5), text, fill=(0, 0, 0))
    return im


def grid(tiles, per_row, gap=6):
    """Tiles may differ in size: each row is as tall as its tallest tile."""
    rows = [tiles[k:k + per_row] for k in range(0, len(tiles), per_row)]
    W = max(sum(t.width for t in r) + gap * (len(r) - 1) for r in rows)
    H = sum(max(t.height for t in r) for r in rows) + gap * (len(rows) - 1)
    s = Image.new("RGB", (W, H), (255, 255, 255))
    y = 0
    for r in rows:
        x = 0
        for t in r:
            s.paste(t, (x, y))
            x += t.width + gap
        y += max(t.height for t in r) + gap
    return s


def video():
    tmp = pathlib.Path(tempfile.mkdtemp())
    seq = list(FRAMES) * 2 + [i for i in FRAMES for _ in range(4)]
    for n, i in enumerate(seq):
        o = label(frame(OLD, i).crop(FRAMING), f"today f{i}")
        w = label(frame(NEW, i).crop(FRAMING), f"hero 4 (Unity 2D) f{i}" + ("  1/4 speed" if n >= 2 * len(FRAMES) else ""))
        s = Image.new("RGB", (o.width * 2 + 8, o.height), (255, 255, 255))
        s.paste(o, (0, 0))
        s.paste(w, (o.width + 8, 0))
        s = s.resize((s.width // 2, s.height // 2), Image.LANCZOS)   # 1904x550: half game size, even sides
        s.save(tmp / f"v{n:04d}.png")
    out = OUT / "attack_old_new.mp4"
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "30", "-i", str(tmp / "v%04d.png"),
                    "-c:v", "libx264", "-crf", "16", "-pix_fmt", "yuv420p", str(out)], check=True)
    shutil.rmtree(tmp)
    return out, len(seq)


def strip(d, name):
    tr = track(d)
    tiles = []
    for i in STRIP:
        fx = tr[i]["points"]["feet"][0]
        x0 = int(round(fx - 470))
        t = frame(d, i).crop((x0, 150, x0 + 1000, 1150))
        tiles.append(label(t, f"f{i} {NAMES[i]}"))
    out = OUT / f"frames_{name}.png"
    grid(tiles, 5).save(out)
    return out


def joints():
    tr = track(NEW)
    pick = [(5, "ready"), (7, "before swap"), (8, "swap"), (9, "wind-up hold"), (10, "swap back (cut)"),
            (11, "strike"), (12, "after strike"), (15, "strike held")]
    tiles = []
    for i, name in pick:
        p = tr[i]["points"]
        xs = [p[k][0] for k in ("S", "elbow", "wrist", "grip")]
        ys = [p[k][1] for k in ("S", "elbow", "wrist", "grip")]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        half = max(max(xs) - min(xs), max(ys) - min(ys)) / 2 + 55    # shoulder cap, armhole and whole fist inside
        t = frame(NEW, i).crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))
        tiles.append(label(t.resize((t.width * 3, t.height * 3), Image.NEAREST), f"f{i} {name} 3x"))
    out = OUT / "joints_3x.png"
    grid(tiles, 4).save(out)
    return out


if __name__ == "__main__":
    v, n = video()
    print("saved", v, n, "frames")
    print("saved", strip(OLD, "old"), strip(NEW, "new"), joints())
