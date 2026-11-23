"""The 10-second check before a build: the four kept bare moments with the head box (red outline), the hair sprite
(blue), the shirt sprite (pink) and the bits moved to the hair sprite (magenta) -> art/tests/moments/masks_check.png.
Run it after picking new moments; hair area should stay near ready's in every moment, and the box sit on the head.

<venv python> -I -B masks.py
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, TESTS, load_rgba  # noqa: E402
import bare  # noqa: E402
import layers  # noqa: E402
import place  # noqa: E402


def main():
    p = json.loads((REDO / "moments.json").read_text())
    tiles = []
    for m in ["ready", "wind", "strike", "follow"]:
        a = load_rgba(REDO / f"{p[m]}.png")
        parts = bare.build(a, m, p[m])
        hb = place.head_box(place.classes(a))
        hair = layers.hair_mask(a)
        split = layers.hair_split(parts["body"], hair)
        shirt = layers.shirt_mask(a, parts["body"], hair)
        ys, xs = np.nonzero(hb)
        print(m, "head box centre", int(xs.mean()), int(ys.mean()), "hair px", int(hair.sum()), "shirt px",
              int(shirt.sum()))
        rgb = a[..., :3].astype(float) * (a[..., 3:] / 255) + 255 * (1 - a[..., 3:] / 255)
        rgb[hair] = rgb[hair] * 0.4 + np.array([0, 90, 255]) * 0.6
        rgb[shirt] = rgb[shirt] * 0.4 + np.array([255, 0, 255]) * 0.4
        rgb[split & ~hair] = [255, 0, 255]
        rgb[hb ^ ndi.binary_erosion(hb, iterations=3)] = [255, 0, 0]
        fy, fx = np.nonzero(a[..., 3] > 128)
        im = Image.fromarray(rgb[fy.min():fy.max(), fx.min():fx.max()].astype(np.uint8))
        im.thumbnail((380, 700))
        tiles.append(im)
    s = Image.new("RGB", (4 * 385, max(t.height for t in tiles)), (255, 255, 255))
    for i, t in enumerate(tiles):
        s.paste(t, (i * 385, 0))
    s.save(TESTS / "masks_check.png")
    print(TESTS / "masks_check.png", s.size)


if __name__ == "__main__":
    main()
