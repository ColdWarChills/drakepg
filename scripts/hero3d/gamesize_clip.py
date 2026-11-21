"""Way C cut out and put on the Fen Road backdrop at game size (hero 240 px tall in the 1280x720 design frame),
beside today's drawn blow from a real fight scaled to the same size.
python scripts/hero3d/gamesize_clip.py art/redo/animway src/assets/art/bg_fen_road@2x.webp <out.mp4|out.jpg> [--speed 0.5] [--still t1,t2,..] [--x2]"""
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

a = sys.argv[1:]
R, BG, OUT = Path(a[0]), a[1], Path(a[2])
SPEED = float(a[a.index("--speed") + 1]) if "--speed" in a else 1.0
STILLS = [float(x) for x in a[a.index("--still") + 1].split(",")] if "--still" in a else []
X2 = "--x2" in a                                   # 2x: as on a 1440p screen (RES 2)
Z = 2 if X2 else 1
HERO, PW, PH, FX, FY = 240 * Z, 425 * Z, 473 * Z, 171 * Z, 341 * Z
bg = Image.open(BG).convert("RGBA").resize((1280 * Z, 720 * Z), Image.LANCZOS).crop((60 * Z, 245 * Z, 60 * Z + PW, 245 * Z + PH))
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 18 * Z)
PANELS = [("Today: drawn poses (real fight)", R / "frames/old", 60, 406, (290, 578), False),
          ("C: 3D moves + drawn look", R / "hybrid/keyed_s1", 24, 377, (300, 427), True),
          ("C with a scale-mail picture", R / "hybrid/keyed_scale", 24, 377, (300, 427), True)]
srcs = [(lab, sorted(d.glob("*.png")), fps, h, f, keyed) for lab, d, fps, h, f, keyed in PANELS]
dur = max(len(fl) / fps for _, fl, fps, _, _, _ in srcs) + 0.5


def panel(i, t):
    lab, fl, fps, h, (fx, fy), keyed = srcs[i]
    im = Image.open(fl[min(int(t * fps), len(fl) - 1)]).convert("RGBA")
    s = HERO / h
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    p = bg.copy() if keyed else Image.new("RGBA", (PW, PH), (0, 0, 0, 255))
    p.alpha_composite(im, (round(FX - fx * s), round(FY - fy * s))) if keyed else p.paste(im, (round(FX - fx * s), round(FY - fy * s)))
    d = ImageDraw.Draw(p)
    d.rectangle((0, 0, PW, 28 * Z), fill=(24, 22, 20, 255))
    d.text((8 * Z, 3 * Z), lab, font=font, fill=(240, 232, 214))
    return p.convert("RGB")


def frame(t):
    W = Image.new("RGB", (3 * PW + 13, PH), (10, 10, 10))
    for i in range(len(srcs)):
        W.paste(panel(i, t), (i * (PW + 6), 0))
    return W


if STILLS:
    ims = [frame(t) for t in STILLS]
    S = Image.new("RGB", (ims[0].width, len(ims) * PH))
    for j, im in enumerate(ims):
        S.paste(im, (0, j * PH))
    S.save(OUT, quality=90)
    sys.exit()
tmp = Path(tempfile.mkdtemp())
n = int(dur / SPEED * 30)
for j in range(n):
    frame(j / 30 * SPEED).save(tmp / f"{j:04d}.png")
subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-framerate", "30", "-i", str(tmp / "%04d.png"), "-c:v", "libx264",
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", "yuv420p", "-crf", "18", str(OUT)], check=True)
print("wrote", OUT)
