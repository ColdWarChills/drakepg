#!/usr/bin/env python3
"""Two recorded takes of the same fight (Drive.Record frames f000.jpg ...), old beside new, cropped around the hero:
a clip and a sheet of picked frames. python scripts/grip_fight_clip.py <old dir> <new dir> <out stem>
[--box x0,y0,x1,y1] [--frames 0,12,20] [--labels "old,new"] [--speed 0.5] [--scale 0.6]"""
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

a = sys.argv[1:]
old, new, stem = Path(a[0]), Path(a[1]), Path(a[2])
opt = lambda k, d: a[a.index(k) + 1] if k in a else d
box = tuple(int(v) for v in opt("--box", "0,0,0,0").split(","))
picks = [int(v) for v in opt("--frames", "").split(",") if v]
labels = opt("--labels", "now,new").split(",")
speed = float(opt("--speed", "1"))
scale = float(opt("--scale", "1"))
fps = int(opt("--fps", "60"))
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 26)
of, nf = sorted(old.glob("f*.jpg")), sorted(new.glob("f*.jpg"))
n = min(len(of), len(nf))


def pair(i):
    ims = []
    for lab, p in zip(labels, (of[i], nf[i])):
        im = Image.open(p).convert("RGB")
        if box[2] > box[0]:
            im = im.crop(box)
        if scale != 1:
            im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        d.rectangle((0, 0, 14 + 15 * len(lab) + 120, 38), fill=(20, 20, 20))
        d.text((8, 4), f"{lab}  {i * 1000 // fps} ms", font=font, fill=(255, 255, 255))
        ims.append(im)
    w, h = ims[0].size
    out = Image.new("RGB", (w * 2 + 8, h), (20, 20, 20))
    out.paste(ims[0], (0, 0)); out.paste(ims[1], (w + 8, 0))
    return out


with tempfile.TemporaryDirectory() as t:
    for i in range(n):
        im = pair(i)
        if im.width % 2 or im.height % 2:
            im = im.crop((0, 0, im.width // 2 * 2, im.height // 2 * 2))
        im.save(f"{t}/p{i:03d}.png")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(fps * speed), "-i", f"{t}/p%03d.png",
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", "-r", "30", str(stem) + ".mp4"], check=True)
if picks:
    rows = [pair(i) for i in picks]
    S = Image.new("RGB", (rows[0].width, sum(r.height + 8 for r in rows)), (20, 20, 20))
    y = 0
    for r in rows:
        S.paste(r, (0, y)); y += r.height + 8
    S.save(str(stem) + ".webp", quality=90)
print(n, "frames", stem)
