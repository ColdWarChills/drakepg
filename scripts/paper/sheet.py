"""Paper damage proof (2026-11-25): a contact sheet of recorded battle frames, for stepping through a fold.

<venv python> -I -B sheet.py <frames dir> <first> <last> [step] [box x0,y0,x1,y1] [zoom] [out.png]
Frames are Drive.Record's f000.jpg...; box is in the frames' own px (default: the monster's side of a 2560 frame).
Each tile is labelled with its frame number. Writes <frames dir>/sheet_<first>_<last>.png unless out is given.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw


def main():
    d = Path(sys.argv[1])
    first, last = int(sys.argv[2]), int(sys.argv[3])
    step = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    box = tuple(int(v) for v in sys.argv[5].split(",")) if len(sys.argv) > 5 else (1480, 420, 2320, 1100)
    zoom = float(sys.argv[6]) if len(sys.argv) > 6 else 0.5
    out = Path(sys.argv[7]) if len(sys.argv) > 7 else d / f"sheet_{first}_{last}.png"
    tiles = []
    for i in range(first, last + 1, step):
        f = d / f"f{i:03d}.jpg"
        if not f.exists():
            continue
        t = Image.open(f).convert("RGB").crop(box)
        t = t.resize((round(t.width * zoom), round(t.height * zoom)), Image.LANCZOS)
        ImageDraw.Draw(t).text((6, 4), str(i), fill=(255, 255, 255))
        tiles.append(t)
    if not tiles:
        sys.exit("no frames")
    cols = min(6, len(tiles))
    rows = (len(tiles) + cols - 1) // cols
    w, h = tiles[0].size
    sheet = Image.new("RGB", (cols * (w + 4), rows * (h + 4)), (0, 0, 0))
    for n, t in enumerate(tiles):
        sheet.paste(t, ((n % cols) * (w + 4), (n // cols) * (h + 4)))
    sheet.save(out)
    print(out, sheet.size)


if __name__ == "__main__":
    main()
