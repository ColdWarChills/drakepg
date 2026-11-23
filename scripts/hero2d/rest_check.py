"""Rebuild the cutting pose from the pieces (every joint at its rest angle) beside the drawing it was cut from."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, Kit, draw_hero, rest_pose, to_image  # noqa: E402

kit = Kit()
pose = rest_pose(kit)
pose["sword"] = False
src = Image.open(KIT / kit.meta["src"]).convert("RGB")
px, _ = draw_hero(kit, pose, src.size, rim_on=False)
im = to_image(px, None, kit.height)
bg = Image.new("RGBA", src.size, (163, 55, 211, 255))
bg.alpha_composite(im)
both = Image.new("RGB", (src.width * 2, src.height))
both.paste(src, (0, 0))
both.paste(bg.convert("RGB"), (src.width, 0))
both.crop((400, 180, 2 * src.width - 400, 1400)).save(sys.argv[1])
