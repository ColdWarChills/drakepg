"""Side-by-side clip of the same sword blow made four ways, every hero scaled to the same height, feet on one line.
python scripts/hero3d/compare.py <out.mp4> [--speed 0.5] [--still t1,t2,...] [--root art/redo/animway] [--fists]
(frame folders are listed in PANELS below, under the root)
Each panel plays its take once from the start and then holds its last frame."""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

a = sys.argv[1:]
SP = Path(a[a.index("--root") + 1]) if "--root" in a else Path(__file__).resolve().parents[2] / "art/redo/animway"
PW, PH, HERO, FEET = 440, 400, 262, 368           # panel size, hero height, feet row
FPS = 30
OUT = Path(a[0])
SPEED = float(a[a.index("--speed") + 1]) if "--speed" in a else 1.0
STILLS = [float(x) for x in a[a.index("--still") + 1].split(",")] if "--still" in a else []
PANELS = [  # label, cost line, frames dir, fps, source hero height px, source feet (x, y), backdrop
    ("Today: drawn poses (game)", "pictures per armor and per move", SP / "frames/old", 60, 406, (290, 578), None),
    # the bones take runs to the monster: its panel follows his waist (track.json from BonesCapture)
    ("2D bones (Nov 18 cut, camera follows)", "gear pieces cut per armor; moves reuse them", SP / "frames/bones", 60, 225,
     (330, 498), 0.40),
    ("A: AI video (LTX)", "a video per gear mix and move", SP / "ltx/n3", 24, 616, (470, 670), None),
    ("B: 3D hero in Blender", "gear made once; moves reuse it", SP / "frames/clip3_bg", 24, 377, (300, 427), None),
    ("C: 3D moves + drawn look", "a video per gear mix and move", SP / "hybrid/frames_s1", 24, 377, (300, 427), None),
]
if "--fists" in a:   # the sword-grip fix (2026-11-21): today's blow, the bones cut, way C before and after
    PANELS = [("Today in the game (wrong grip)",) + PANELS[0][1:], ("2D bones, Nov 18 (wrong grip)",) + PANELS[1][1:],
              ("C, first try (wrong grip)", "3D keys put the blade in line with the arm", SP / "hybrid/frames_s1", 24, 377,
               (300, 427), None),
              ("C, grip fixed", "blade leaves the fist by the thumb wherever it shows", SP / "hybrid/frames_s4", 24, 377,
               (300, 427), None)]
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 19)
small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)


def frames(d):
    return sorted(p for p in Path(d).glob("*") if p.suffix in (".png", ".jpg"))


def follow(d):
    """Per-frame waist x from a BonesCapture track.json next to the frames (smoothed), or None."""
    t = Path(d) / "track.json"
    if not t.exists():
        return None
    xs = [f["points"]["j_waist"][0] for f in json.load(open(t))["frames"]]
    return [sum(xs[max(0, i - 4):i + 5]) / len(xs[max(0, i - 4):i + 5]) for i in range(len(xs))]


srcs = [(lab, cost, frames(d), fps, h, feet, ax or 0.30, follow(d)) for lab, cost, d, fps, h, feet, ax in PANELS if frames(d)]
dur = max(len(f) / fps for _, _, f, fps, _, _, _, _ in srcs) + 0.6
cache = {}


def panel(i, t):
    lab, cost, fl, fps, h, (fx, fy), ax, fol = srcs[i]
    k = min(int(t * fps), len(fl) - 1)
    if fol:
        fx = fol[min(k, len(fol) - 1)]
    key = (i, k)
    if key not in cache:
        im = Image.open(fl[k]).convert("RGB")
        s = HERO / h
        im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
        p = Image.new("RGB", (PW, PH + 58), (36, 33, 31))
        p.paste(im, (round(PW * ax - fx * s), round(FEET - fy * s)))
        d = ImageDraw.Draw(p)
        d.rectangle((0, 0, PW, 30), fill=(24, 22, 20))
        d.text((10, 4), lab, font=font, fill=(240, 232, 214))
        d.rectangle((0, PH, PW, PH + 58), fill=(24, 22, 20))
        d.text((10, PH + 8), cost, font=small, fill=(205, 196, 180))
        cache[key] = p
    return cache[key]


def frame(t_out):
    t = t_out * SPEED
    W = Image.new("RGB", (PW * len(srcs) + 6 * (len(srcs) - 1), PH + 58), (10, 10, 10))
    for i in range(len(srcs)):
        W.paste(panel(i, t), (i * (PW + 6), 0))
    d = ImageDraw.Draw(W)
    d.text((W.width - 150, PH + 34), f"{'half speed' if SPEED < 1 else 'normal speed'}  {t:4.2f}s", font=small,
           fill=(150, 145, 135))
    return W


if STILLS:
    ims = [frame(t / SPEED) for t in STILLS]
    S = Image.new("RGB", (ims[0].width, sum(i.height for i in ims)))
    for j, im in enumerate(ims):
        S.paste(im, (0, j * ims[0].height))
    S.save(OUT, quality=90)
    sys.exit()
tmp = Path(tempfile.mkdtemp())
n = int(dur / SPEED * FPS)
for j in range(n):
    frame(j / FPS).save(tmp / f"{j:04d}.png")
subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-framerate", str(FPS), "-i", str(tmp / "%04d.png"),
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", str(OUT)], check=True)
print("wrote", OUT, n, "frames", round(n / FPS, 2), "s")
