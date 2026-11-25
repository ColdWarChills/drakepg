"""Zoomed stills of the knight's box from a take: python -I -B hero_zoom.py <take dir> <out png> <frame,frame,...> [scale]
Each frame: the take's lossless pNNN.png cropped around the 'knight' box from track.json (+margin), scaled so the
figure is `scale` times game size (the 2560 take is 2x game px), with the frame number and phase."""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

take, out = Path(sys.argv[1]), Path(sys.argv[2])
frames = [int(v) for v in sys.argv[3].split(",")]
scale = float(sys.argv[4]) if len(sys.argv) > 4 else 3
t = json.loads((take / "track.json").read_text())["frames"]
font = ImageFont.truetype("arial.ttf", 22)
phase = ""
phases = {}
for f in t:
    for e in f.get("events", []):
        if e.startswith("phase:"):
            phase = e[6:]
    phases[f["i"]] = phase
tiles = []
for i in frames:
    b = t[i].get("boxes", {}).get("knight")
    x0, y0, x1, y1 = b if b else (0, 0, 2560, 1440)
    cx = (x0 + x1) / 2
    w = max(x1 - x0, 520) + 80
    h = 640
    box = (int(cx - w / 2), int(y1 - h + 30), int(cx + w / 2), int(y1 + 30))
    im = Image.open(take / f"p{i:03d}.png").convert("RGB").crop(box)
    im = im.resize((int(im.width * scale / 2), int(im.height * scale / 2)), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 260, 30], fill=(0, 0, 0))
    d.text((6, 4), f"frame {i} {phases.get(i, '')}", font=font, fill=(255, 255, 0))
    tiles.append(im)
W = sum(t.width for t in tiles) + 6 * (len(tiles) - 1)
H = max(t.height for t in tiles)
sheet = Image.new("RGB", (W, H), (30, 30, 30))
x = 0
for im in tiles:
    sheet.paste(im, (x, 0))
    x += im.width + 6
sheet.save(out)
print(out, sheet.size)
