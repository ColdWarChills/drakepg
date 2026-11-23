"""Grid crops for reading points by eye. <venv python> -I -B gridlook.py <out png> <src>:<x0,y0,x1,y1>[:scale] ...
src = an A3 moment name (ready/wind/strike, stacked) or a png path relative to art/redo/hero_joints (no .png).
Grid every 20 px (labelled every 100), coordinates are the source canvas's."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

from PIL import Image, ImageDraw

from jcommon import REDO, TESTS, a3_moment, fit, on_bg

tiles = []
for spec in sys.argv[2:]:
    parts = spec.split(":")
    src, box = parts[0], [int(v) for v in parts[1].split(",")]
    k = float(parts[2]) if len(parts) > 2 else 1.5
    im = a3_moment(src) if src in ("ready", "wind", "strike") else Image.open(REDO / f"{src}.png").convert("RGBA")
    c = on_bg(im.crop(box), (200, 200, 200)) if im.mode == "RGBA" else im.crop(box)
    c = c.resize((round(c.width * k), round(c.height * k)), Image.LANCZOS)
    d = ImageDraw.Draw(c)
    for x in range((box[0] // 20 + 1) * 20, box[2], 20):
        X = (x - box[0]) * k
        d.line([(X, 0), (X, c.height)], fill=(0, 0, 255) if x % 100 == 0 else (90, 90, 160), width=1)
        if x % 100 == 0:
            d.text((X + 2, 2), str(x), fill=(0, 0, 255))
    for y in range((box[1] // 20 + 1) * 20, box[3], 20):
        Y = (y - box[1]) * k
        d.line([(0, Y), (c.width, Y)], fill=(0, 0, 255) if y % 100 == 0 else (90, 90, 160), width=1)
        if y % 100 == 0:
            d.text((2, Y + 2), str(y), fill=(0, 0, 255))
    d.text((4, c.height - 14), src, fill=(255, 0, 0))
    tiles.append(c)
W = sum(t.width for t in tiles) + 10 * len(tiles)
sh = Image.new("RGB", (W, max(t.height for t in tiles)), (255, 255, 255))
x = 0
for t in tiles:
    sh.paste(t, (x, 0))
    x += t.width + 10
fit(sh).save(TESTS / sys.argv[1])
print(sh.size)
