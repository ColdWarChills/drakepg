"""Qwen redraw of a render of the 3D hero (a fill view) in the drawn hero's style, same outline and pose, so it registers.
<venv python> -I redraw.py <render png (RGBA)> <style ref png> <out prefix> <seeds a,b> [--outfit]
Writes <out prefix>_s<seed>.png (on white, the render's canvas size). The project's wording: edits keep the figure,
the drawn hero is <image2>, used only for the style; flat even cel shading, no rim light, no shadows."""
import io
import sys
import time
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from qwen_batch import Comfy  # noqa: E402

a = sys.argv[1:]
REN, REF, OUT, SEEDS = Path(a[0]), Path(a[1]), a[2], [int(s) for s in a[3].split(",")]
OUTFIT = "--outfit" in a
ANGLE = a[a.index("--angle") + 1] if "--angle" in a else "we see him from behind and from his side"
NOREF = "--noref" in a          # one reference only (the render): with the drawn hero as <image2> Qwen copied it whole


def on_white(p, pad=0):
    im = Image.open(p).convert("RGBA")
    bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
    bg.alpha_composite(im)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


DRESS = ("the steel scale-mail vest with round steel shoulder plates exactly as the hero in <image2> wears it, over the "
         "cream shirt with rolled sleeves, " if OUTFIT else "")
PROMPT = (
    "Edit <image1>. Redraw this exact figure as a finished hand-drawn game character in exactly the art style of the hero "
    f"in <image2>: the same young hero with messy dark brown hair and the red headband with two tails, {DRESS}"
    "cream shirt with rolled-up sleeves and soft cloth folds, brown belt with a square buckle, baggy dark teal trousers "
    "with folds, tall brown boots. Keep the figure of <image1> exactly: the same pose, the same viewing angle, the same "
    "outline, size and position on the canvas, every arm, leg, hand and the head exactly where they are. Only the "
    "surface changes: drawn hair strands, cloth folds and seams, boot folds, clean dark outlines and inner lines like "
    "<image2>. Flat, even cel shading with only soft two-tone shadows: no rim light, no glow along the edges, no cast "
    "shadows, no ground shadow, no highlights from a lamp. Use <image2> only for the art style and the clothes, not its "
    "pose. Plain pure white background, nothing else in the picture, no text.")
if NOREF:
    PROMPT = (
        "Edit <image1>. Redraw this exact figure as a finished hand-drawn 2D cartoon game character: a young hero with "
        "messy dark brown hair in drawn strands and a red cloth headband with two tails, "
        + ("a steel scale-mail vest of overlapping rounded steel scales with round polished steel shoulder plates, over "
           if OUTFIT else "") +
        "a loose cream linen shirt with rolled-up sleeves and soft cloth folds, a brown leather belt, baggy dark teal "
        "trousers with folds, tall brown leather boots with folds. Keep the figure of <image1> exactly: the same pose, "
        f"the same viewing angle ({ANGLE}), the same outline, size and position on "
        "the canvas, every arm, leg, hand and the head exactly where they are, the same colours. Only the surface "
        "changes: drawn hair strands, cloth folds and seams, boot folds, clean thin dark outlines and inner lines, "
        "in a clean flat cartoon style. Flat, even cel shading with soft two-tone shadows only: no rim light, no glow "
        "along the edges, no cast shadows, no ground shadow, no shiny highlights. Plain pure white background, "
        "nothing else in the picture, no text.")
c = Comfy("http://127.0.0.1:8188")
c.check()
r1 = c.upload(f"skin_{REN.parent.name}_{REN.stem}.png", on_white(REN))
refs = [r1] if NOREF else [r1, c.upload(f"skin_ref_{REF.stem}.png", on_white(REF))]
for s in SEEDS:
    out = Path(f"{OUT}_s{s}.png")
    if out.exists():
        continue
    t = time.time()
    out.write_bytes(c.generate(PROMPT, refs, None, s, 30))
    print("QWEN", out.name, round(time.time() - t), "s", flush=True)
