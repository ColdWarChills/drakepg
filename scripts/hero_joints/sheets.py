"""Check sheets from the rendered frames and the rig.
<venv python> -I -B sheets.py strips [kinds]   -> motion_strip_<K>.png (+ motion_strips.png = the three stacked, small)
<venv python> -I -B sheets.py look              -> look.png (ready pose beside char_hero_bare, same height; game size)
Frames are read from HJ_TMP/<kind>/f####.png (swing.py)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import os

import numpy as np
from PIL import Image, ImageDraw

from jcommon import RAW, REDO, TESTS, fit

FR = pathlib.Path(os.environ.get("HJ_TMP", str(REDO / "_frames")))


def strips(kinds):
    import swing
    first = swing.IDLE_A
    picks = [s for s in range(0, 45, 3)] + list(range(45, 62)) + [s for s in range(62, 101, 3)]
    ox, oy = -150, 190
    box = (380 - ox, 190 - oy, 1150 - ox, 890 - oy)  # the arm's room (canvas x 380..1150, y 190..890)
    k = 0.3
    out = []
    for kind in kinds:
        tiles = []
        for s in picks:
            im = Image.open(FR / kind / f"f{first + s:04d}.png").crop(box)
            im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
            d = ImageDraw.Draw(im)
            fast = 45 <= s <= 61
            d.rectangle([0, 0, 40, 13], fill=(255, 255, 255) if not fast else (255, 220, 120))
            d.text((2, 1), f"{s}", fill=(0, 0, 0))
            tiles.append(im)
        cols = 9
        tw, th = tiles[0].size
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * (tw + 3), rows * (th + 3) + 20), (255, 255, 255))
        ImageDraw.Draw(sheet).text((4, 4), f"kind {kind}: every 3rd frame; every frame 45-61 (orange) from the last "
                                            f"wind-up frame to the settled strike", fill=(0, 0, 0))
        for i, t in enumerate(tiles):
            sheet.paste(t, ((i % cols) * (tw + 3), 20 + (i // cols) * (th + 3)))
        sheet.save(TESTS / f"motion_strip_{kind}.png")
        out.append(sheet)
        print(TESTS / f"motion_strip_{kind}.png", sheet.size)
    if len(out) == 3:
        W = max(s.width for s in out)
        big = Image.new("RGB", (W, sum(s.height for s in out) + 20), (255, 255, 255))
        y = 0
        for s in out:
            big.paste(s, (0, y))
            y += s.height + 10
        big.save(TESTS / "motion_strips.png")


def look():
    import rig
    parts = rig.Parts()
    fr = rig.Frame(parts, window=(380, 220, 700, 1120))
    pose = dict(a1=70.0, b=70.0, turn=44.5, lean=0.0, head=0.0, far=0.0, shoulder=(0, 0), fist="ready", stretch=1.0)
    BG = np.array([0.85, 0.83, 0.80], np.float32)
    tiles = []
    for kind in ("A", "B", "C"):
        px, _ = fr.render(kind, pose)
        tiles.append((f"new, {rig.KINDS[kind]} (ready)", rig.to_image(px, BG)))
    old = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
    old = old.crop(Image.fromarray(((np.asarray(old)[..., 3] > 40) * 255).astype(np.uint8)).getbbox())
    bg = Image.new("RGBA", old.size, tuple(int(v * 255) for v in BG) + (255,))
    bg.alpha_composite(old)
    old = bg.convert("RGB")
    # same height: the new hero's figure (hair to soles) is 1028 px, the old 1033
    new_h = 1028
    H = 1000
    row = []
    for name, im in [("char_hero_bare (in the game now)", old)] + tiles[1:2]:
        a = np.asarray(im)
        k = H / (new_h if "new" in name else old.height)
        row.append((name, im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)))
    # game size (480 px tall at 1440p) beside
    small = [(n + " - game size", im.resize((round(im.width * 480 / H), round(im.height * 480 / H)), Image.LANCZOS))
             for n, im in row]
    W = sum(r.width for _, r in row) + sum(s.width for _, s in small) + 60
    sheet = Image.new("RGB", (W, H + 40), (255, 255, 255))
    d = ImageDraw.Draw(sheet)
    x = 0
    for n, im in row + small:
        sheet.paste(im, (x, 30))
        d.text((x + 4, 8), n, fill=(0, 0, 0))
        x += im.width + 15
    fit(sheet).save(TESTS / "look.png")
    print(TESTS / "look.png")


if __name__ == "__main__":
    if sys.argv[1] == "strips":
        strips(sys.argv[2] if len(sys.argv) > 2 else "ABC")
    else:
        look()
