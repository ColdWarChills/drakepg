"""The one texture of the 3D hero: his drawn face (from the front view), cleaned to flat colours on the exact flat skin.
Features (brows, eyes, eye whites, nose and mouth lines) are kept inside a box; everything else is the skin colour,
so the face has no seam against the neck and ears (they use the same flat skin and the same shade step).
<venv python> -I scripts/hero3d_v2/make_face.py <front.png> <out face.png>
The crop box (front.png pixels) must match FACE_BOX in build_hero.py."""
import sys

import numpy as np
from PIL import Image, ImageFilter

FACE_BOX = (462, 128, 562, 228)          # x0, y0, x1, y1 in front.png (1024 px = 1.9628 m)
FEAT_BOX = (473, 133, 552, 199)          # where features live (brows .. mouth); the hair locks outside are dropped
SKIN = (0xdf, 0x9d, 0x4f)
DARK = (0x2b, 0x1a, 0x10)
WHITE = (0xf4, 0xea, 0xd8)
LINE = (0xb0, 0x6c, 0x2e)                # nose / mouth line: the skin shade
UP = 8                                   # classify at 8x so edges stay smooth, then shrink to 512

src = Image.open(sys.argv[1]).convert("RGB")
x0, y0, x1, y1 = FACE_BOX
crop = src.crop(FACE_BOX).resize(((x1 - x0) * UP, (y1 - y0) * UP), Image.BICUBIC)
a = np.asarray(crop).astype(float) / 255
lum = a @ np.array([0.299, 0.587, 0.114])
sat = a.max(2) - a.min(2)
H, W = lum.shape
yy, xx = np.mgrid[0:H, 0:W]
fx0, fy0, fx1, fy1 = [(v - o) * UP for v, o in zip(FEAT_BOX, (x0, y0, x0, y0))]
inside = (xx >= fx0) & (xx < fx1) & (yy >= fy0) & (yy < fy1)


def box(bx0, by0, bx1, by1):
    return (xx >= (bx0 - x0) * UP) & (xx < (bx1 - x0) * UP) & (yy >= (by0 - y0) * UP) & (yy < (by1 - y0) * UP)


eyes = inside & box(473, 133, 552, 166)  # brows and eyes: dark + whites
lower = box(495, 166, 532, 182) | box(488, 184, 540, 197)  # nose, mouth: thin skin-shade lines only
skin_l = np.dot(np.array(SKIN) / 255, [0.299, 0.587, 0.114])
out = np.zeros((H, W, 3)) + np.array(SKIN) / 255
dark = eyes & (lum < 0.30)
white = eyes & (lum > 0.72) & (sat < 0.28)
line = lower & (lum < skin_l * 0.86) & (lum > 0.08)
out[line] = np.array(LINE) / 255
out[dark] = np.array(DARK) / 255
out[white] = np.array(WHITE) / 255
im = Image.fromarray((out * 255).astype(np.uint8)).filter(ImageFilter.ModeFilter(5))
im = im.resize((512, 512), Image.LANCZOS)
im.save(sys.argv[2])
print("wrote", sys.argv[2], "dark", dark.mean().round(3), "white", white.mean().round(3), "line", line.mean().round(3))
