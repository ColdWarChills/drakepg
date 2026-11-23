"""Contact sheet of edit tries (cropped to the figure) for a quick look: view_edits.py <out png> <try> [<try>...]"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, load_rgba  # noqa: E402
import sheets  # noqa: E402

out, names = sys.argv[1], sys.argv[2:]
tiles = []
for n in names:
    a = load_rgba(REDO / f"{n}.png")
    x0, y0, x1, y1 = sheets.bbox(a, 10)
    im = Image.new("RGBA", (x1 - x0, y1 - y0), sheets.PAPER + (255,))
    im.alpha_composite(Image.fromarray(a).crop((x0, y0, x1, y1)))
    im.thumbnail((520, 520))
    t = Image.new("RGB", (520, 545), sheets.PAPER)
    t.paste(im.convert("RGB"), ((520 - im.width) // 2, 25))
    ImageDraw.Draw(t).text((4, 2), n, fill=(30, 20, 10), font=sheets.font(16))
    tiles.append(t)
cols = min(3, len(tiles))
s = Image.new("RGB", (cols * 522, ((len(tiles) + cols - 1) // cols) * 547), (255, 255, 255))
for i, t in enumerate(tiles):
    s.paste(t, ((i % cols) * 522, (i // cols) * 547))
s.thumbnail((1568, 1568))
s.save(out)
print(out, s.size)
