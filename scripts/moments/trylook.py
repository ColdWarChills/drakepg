"""Quick eye check of raw edit tries (before build): crops of each try round a spot, at 3x game size, beside the ready
pick, plus the whole figure at game size (480 px tall). Output: art/tests/moments/try_<name>.png (<= 1568 px).

<venv python> -I -B trylook.py <out name> <spot: fist|chest|head|legs|boots> <ready try> <try> [<try> ...]
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, REDO, TESTS, load_rgba  # noqa: E402
from sheets import PAPER, font  # noqa: E402


def picks():
    return json.loads((REDO / "moments.json").read_text())


def spot(m: str, what: str):
    """(centre, half size) in drawing px for a moment."""
    import bare
    p = picks()
    a = load_rgba(REDO / f"{p[m]}.png")
    if what == "fist":
        rod = bare.read_blade(a, p[m])
        return rod["center"], 110
    ys, xs = np.nonzero(a[..., 3] > 100)
    top, bot = ys.min(), ys.max()
    h = bot - top
    fy = {"head": 0.13, "chest": 0.33, "legs": 0.62, "boots": 0.85}[what]
    row = int(top + fy * h)
    xs_r = np.nonzero(a[row, :, 3] > 100)[0]
    return (float(np.median(xs_r)), float(row)), {"head": 140, "chest": 230, "legs": 260, "boots": 230}[what]


def main(out, what, tries):
    tiles = []
    for t in tries:
        m = t.split("@")[1].split("_")[0]
        a = load_rgba(REDO / f"{t}.png")
        (cx, cy), half = spot(m, what)
        box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
        im = Image.new("RGBA", (2 * half, 2 * half), PAPER + (255,))
        im.alpha_composite(Image.fromarray(a).crop(box))
        k = 3 * 480 / HERO_H
        big = im.resize((round(2 * half * k),) * 2, Image.LANCZOS)
        g = Image.new("RGBA", (a.shape[1], a.shape[0]), PAPER + (255,))
        g.alpha_composite(Image.fromarray(a))
        ys, xs = np.nonzero(a[..., 3] > 30)
        g = g.crop((xs.min(), ys.min(), xs.max(), ys.max()))
        s = 480 / HERO_H
        small = g.resize((round(g.width * s), round(g.height * s)), Image.LANCZOS)
        tiles.append((t, big, small))
    cw = max(max(b.width, s.width) for _, b, s in tiles)
    ch = max(b.height for _, b, _ in tiles) + max(s.height for _, _, s in tiles) + 40
    sheet = Image.new("RGB", (len(tiles) * (cw + 10), ch), PAPER)
    d = ImageDraw.Draw(sheet)
    for i, (t, big, small) in enumerate(tiles):
        x = i * (cw + 10)
        d.text((x, 4), t, fill=(40, 30, 20), font=font(16))
        sheet.paste(big.convert("RGB"), (x, 26))
        sheet.paste(small.convert("RGB"), (x, 30 + big.height))
    # at most 1568 px long side and 1.1 megapixels
    k = min(1, 1568 / max(sheet.size), (1.1e6 / (sheet.width * sheet.height)) ** 0.5)
    if k < 1:
        sheet = sheet.resize((int(sheet.width * k), int(sheet.height * k)), Image.LANCZOS)
    sheet.save(TESTS / f"try_{out}.png")
    print(TESTS / f"try_{out}.png", sheet.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
