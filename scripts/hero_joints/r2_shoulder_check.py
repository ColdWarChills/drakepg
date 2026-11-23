"""Round 2: the near shoulder at chosen clip frames (rendered fresh, full drawing size), crops round the pivot.
<venv python> -I -B r2_shoulder_check.py <out name> f1,f2,...  -> art/tests/hero_joints/r2/<out name>.png"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import numpy as np
from PIL import Image, ImageDraw

import rig_r2 as rig
import swing_r2 as sw
from jcommon import TESTS, fit

name, fl = sys.argv[1], [int(v) for v in sys.argv[2].split(",")]
parts = rig.Parts()
fr = rig.Frame(parts)
frames = sw.build(fr)
bg = sw.background(fr)
tiles = []
for f in fl:
    F = frames[f]
    smear = sw.smear_polys(F, parts.tip_len, fr.ox, fr.oy) if F["smear_src"] else None
    px, info = fr.render(F["pose"], smear=smear)
    im = Image.fromarray((np.clip(px[..., :3] + bg * (1 - px[..., 3:4]), 0, 1) * 255 + 0.5).astype(np.uint8))
    S = info["S"]
    c = 150
    t = im.crop((int(S[0] - c), int(S[1] - c), int(S[0] + c), int(S[1] + c)))
    ImageDraw.Draw(t).text((4, 4), f"f{f} s{F['s']}", fill=(255, 255, 255))
    tiles.append(t)
cols = 4
sh = Image.new("RGB", (cols * 304, ((len(tiles) + cols - 1) // cols) * 304), (255, 255, 255))
for i, t in enumerate(tiles):
    sh.paste(t, ((i % cols) * 304, (i // cols) * 304))
fit(sh).save(TESTS / "r2" / f"{name}.png")
print(sh.size)
