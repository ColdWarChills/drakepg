"""Close-up of the sword hand in every frame: one row per take, the crop centred on the grip that Blender tracked.
python hand_strip.py <track.json> <out.jpg> <label>=<frames dir>[:offset] ...  (offset: the picture used for frame f is the (f+offset)th file by name; a Wan take runs one frame behind its driver, so its f001 already matches driver frame 0: use 0)"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

trk = json.load(open(sys.argv[1]))
out = sys.argv[2]
rows = []
for spec in sys.argv[3:]:
    lab, rest = spec.split("=", 1)
    d, off = (rest.rsplit(":", 1) + ["0"])[:2] if ":" in rest[2:] else (rest, "0")
    rows.append((lab, sorted(p for p in Path(d).glob("*.png")), int(off)))
frames = sorted(int(k) for k in trk)
B, S = 70, 150                                   # half box in source px, tile size
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 14)
W = Image.new("RGB", (len(frames) * S, len(rows) * (S + 18)), (30, 28, 26))
d = ImageDraw.Draw(W)
for r, (lab, fl, off) in enumerate(rows):
    for c, f in enumerate(frames):
        gx, gy = trk[str(f)]["grip"]
        im = Image.open(fl[min(f + off, len(fl) - 1) if off >= 0 else f]).convert("RGB")
        W.paste(im.crop((int(gx - B), int(gy - B), int(gx + B), int(gy + B))).resize((S, S), Image.LANCZOS), (c * S, r * (S + 18) + 18))
        d.text((c * S + 4, r * (S + 18) + 1), f"{lab} {f}", font=font, fill=(235, 225, 205))
W.save(out, quality=88)
print(W.size)
