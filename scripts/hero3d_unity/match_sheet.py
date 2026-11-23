"""Unity's hero beside Blender's render, both at game size x3, feet on one line.
python -I scripts/hero3d_unity/match_sheet.py <real_fight.png> <hero box x0,y0,x1,y1 in the 1280x720 shot> <look_3x.png> <out.png>
The left panel of look_3x.png (Blender, round 1) is cut at its first dark separator column."""
import sys

import numpy as np
from PIL import Image, ImageDraw

shot, box, blender, out = sys.argv[1], [int(v) for v in sys.argv[2].split(",")], sys.argv[3], sys.argv[4]
b = Image.open(blender).convert("RGB")
a = np.asarray(b).astype(int)
# The first column below the caption strip that is dark top to bottom = the separator.
body = a[40:, :, :]
dark = (body.sum(axis=2) < 150).mean(axis=0) > 0.9
sep = next(x for x in range(50, a.shape[1]) if dark[x])
left = b.crop((0, 0, sep, b.height))
u = Image.open(shot).convert("RGB").crop(tuple(box))
u = u.resize((u.width * 3, u.height * 3), Image.NEAREST)
h = max(left.height, u.height)
sheet = Image.new("RGB", (u.width + 12 + left.width, h + 30), (24, 24, 24))
sheet.paste(u, (0, 30 + h - u.height))
sheet.paste(left, (u.width + 12, 30 + h - left.height))
d = ImageDraw.Draw(sheet)
d.text((8, 8), "Unity fight (Hero3DView), 1x game render shown x3 (hard pixels)", fill=(240, 240, 240))
d.text((u.width + 20, 8), "Blender round 1 render (look_3x.png left panel)", fill=(240, 240, 240))
sheet.save(out)
print("separator", sep, "unity crop", u.size, "blender panel", left.size, "->", out)
