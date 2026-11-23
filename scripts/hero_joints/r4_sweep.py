"""Round 4, step 2, test 1: hero 4's joint sweep (Test2DRun.Sweep: shoulder -180..180, elbow 0..140, wrist -90..90,
driven directly with the solver off) as one sheet.
<venv python> -I -B r4_sweep.py <sweep dir>  ->  art/tests/hero_joints/r4/sweep.png
Rows: shoulder (every 60 deg), elbow (every 20), wrist (every ~22); each tile is the whole studio frame at 1/4."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

from PIL import Image, ImageDraw

from jcommon import TESTS

D = pathlib.Path(sys.argv[1])
ROWS = [[0, 10, 20, 30, 40, 50, 60], [61, 66, 71, 76, 81, 86, 91, 96], [97, 102, 108, 113, 119, 124, 130, 136, 142]]


def main():
    js = json.loads((D / "sweep.json").read_text())
    W, H, gap = 450, 250, 6
    cols = max(len(r) for r in ROWS)
    s = Image.new("RGB", (cols * (W + gap), len(ROWS) * (H + gap)), (255, 255, 255))
    for r, row in enumerate(ROWS):
        for c, i in enumerate(row):
            t = Image.open(D / f"w{i:03d}.png").convert("RGB").resize((W, H), Image.LANCZOS)
            d = ImageDraw.Draw(t)
            txt = f"{js[i]['joint']} {js[i]['deg']:+.0f}"
            d.rectangle([0, 0, 10 + 7 * len(txt), 18], fill=(255, 255, 255))
            d.text((5, 3), txt, fill=(0, 0, 0))
            s.paste(t, (c * (W + gap), r * (H + gap)))
    out = TESTS / "r4" / "sweep.png"
    s.save(out)
    print("saved", out)


if __name__ == "__main__":
    main()
