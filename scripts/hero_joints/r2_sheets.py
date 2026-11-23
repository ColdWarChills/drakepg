"""Round 2 motion strips from the rendered frames (first swing), cropped to the hero, labelled with clip and swing frame.
<venv python> -I -B r2_sheets.py <frames dir>
-> art/tests/hero_joints/r2/motion_strip.png (every 3rd frame of the whole swing), motion_strip_raise.png (every frame
   of the raise into the held wind-up), motion_strip_cut.png (every frame of the cut through the settle)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

from PIL import Image, ImageDraw

from jcommon import TESTS, fit

FR = pathlib.Path(sys.argv[1])
rows = json.loads((TESTS / "r2" / "swing_r2.json").read_text())["frames"]
first = [r for r in rows if r["s"] is not None][: 112]
CROP = (300, 40, 1450, 960)   # window px: the hero and his sword's reach


def sheet(sel, cols, tile_w, out, title):
    tiles = []
    for r in sel:
        im = Image.open(FR / f"f{r['f']:04d}.png").convert("RGB").crop(CROP)
        k = tile_w / im.width
        im = im.resize((tile_w, round(im.height * k)), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, 92, 13], fill=(255, 255, 255))
        d.text((2, 1), f"f{r['f']} s{r['s']} t{r['tip_step']:.0f}", fill=(0, 0, 0))
        tiles.append(im)
    th = tiles[0].height
    n = len(tiles)
    rows_n = (n + cols - 1) // cols
    sh = Image.new("RGB", (cols * (tile_w + 3), rows_n * (th + 3) + 16), (255, 255, 255))
    ImageDraw.Draw(sh).text((4, 2), title, fill=(0, 0, 0))
    for i, t in enumerate(tiles):
        sh.paste(t, ((i % cols) * (tile_w + 3), 16 + (i // cols) * (th + 3)))
    sh = fit(sh, 1500, 1.05)
    sh.save(TESTS / "r2" / out)
    print(out, sh.size, n, "tiles")


sheet([r for r in first if 3 <= r["s"] <= 33], 7, 210, "motion_strip_raise.png",
      "the raise into the held wind-up: every frame (f clip frame, s swing frame, t blade tip step px)")
sheet([r for r in first if 44 <= r["s"] <= 64], 7, 210, "motion_strip_cut.png",
      "the cut through the settle: every frame (f clip frame, s swing frame, t blade tip step px)")
sheet([r for r in first if r["s"] % 3 == 0], 8, 180, "motion_strip.png",
      "the whole swing: every 3rd frame (f clip frame, s swing frame, t blade tip step px)")
