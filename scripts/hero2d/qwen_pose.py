"""Qwen edits of the drawn hero (art/raw/char_hero_bare.png) for the cutout hero (hero2d).
<venv python> -I -B qwen_pose.py <name> <seeds a,b,c> [src png]
Writes art/redo/hero2d/<name>_s<seed>.png. One reference only (the hero composited on flat purple, wide canvas)."""
import io
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from qwen_batch import Comfy  # noqa: E402

PURPLE = (163, 55, 211)
OUT = ROOT / "art/redo/hero2d/r2"   # step 1b; step 1's files in art/redo/hero2d stay untouched
OUT.mkdir(parents=True, exist_ok=True)

KEEP = ("Keep exactly the same young hero: his face, spiky messy dark brown hair, the red headband with its two loose "
        "tails, the cream shirt with rolled-up sleeves, the brown belt with the square buckle, the baggy dark teal "
        "trousers and the tall brown boots with folded tops, in exactly the same clean cel-shaded cartoon art style, "
        "outlines and colours. Full body from the top of his hair to the soles of his boots, the same size. Flat even "
        "light with two-tone cel shading only: no rim light, no pale glow or light line along the edges, no cast "
        "shadows, no ground shadow. Plain flat purple background, nothing else in the picture, no text.")
P = {
    "cut": ("Edit <image1>. Redraw the same young hero in a neutral pose for a cutout puppet. The same three-quarter "
            "view: he faces right with his chest turned toward us. He stands upright and relaxed. Both arms hang "
            "straight and are held out away from his body, about 40 degrees out from his sides like a letter A, so "
            "neither arm touches or overlaps his body; the elbows are straight. Both hands are closed in loose fists "
            "with the backs of the hands toward us. His legs are straight and spread apart, the feet well apart, so the "
            "legs do not touch or cross each other; both boots point to the right. " + KEEP),
    # step 1b: forms a puppet can use (a fresh pose from char_hero_bare)
    "cut2": ("Edit <image1>. Redraw the same young hero in a neutral pose for a cutout puppet. The same three-quarter "
             "view: he faces right with his chest turned toward us. He stands upright and relaxed. Both arms are held "
             "straight, away from his body, about 45 degrees out from his sides, so there is a wide gap between each "
             "arm and his body; the elbows are straight. Each shirt sleeve is rolled up to just below the elbow and ends "
             "in one neat smooth rolled band with a clean round edge, no frayed or ragged cloth. Both hands are closed in "
             "loose fists with the backs of the hands toward us. His legs are straight and spread wide apart like an "
             "upside-down letter V, with a clear gap between the legs all the way up to the crotch; the trouser legs have "
             "smooth simple outlines, their folds drawn only as lines inside. Both boots point to the right, seen from "
             "the side. The red headband's two long loose tails hang out behind his head. " + KEEP),
    # step 1b: the same details fixed on step 1's drawing (src art/redo/hero2d/cut_s11.png), pose kept
    "fix": ("Edit <image1>. Keep the hero's pose, size and place in the picture exactly the same: his head, both arms, "
            "both fists and both legs stay exactly where they are. Change only these details: each rolled shirt sleeve "
            "ends in one neat smooth rolled band with a clean round edge instead of ragged cloth; the trouser legs get "
            "smooth simple outlines, their folds drawn only as lines inside; the boot on the left turns to point to the "
            "right, seen from the side, like the other boot; the red headband's two loose tails are longer. " + KEEP),
    # step 1b, the first armor: Scale Mail as body armor (torso, shoulders, upper arms), edit of the cutting pose
    "mail": ("Edit <image1>. Change only the clothing on his upper body. Keep the hero's pose, size and place in the "
             "picture exactly the same: his head, both arms, both fists, both legs and both boots stay exactly where "
             "they are. Over his cream shirt he now wears a steel scale mail vest: rows of overlapping rounded "
             "silver-grey steel scales, each scale with a dark outline and a bright highlight, covering his chest and "
             "stomach from the neck down to the belt, with a dark leather collar band around his neck. On each "
             "shoulder sits a rounded polished steel shoulder plate with small rivets, over a short dark leather "
             "sleeve; below it the cream shirt sleeve with its neat rolled band shows as before, and his forearms and "
             "fists stay bare. His face, hair, the red headband with its tails, the brown belt with the square buckle, "
             "the baggy dark teal trousers and the brown boots stay exactly the same. Exactly the same clean "
             "cel-shaded cartoon art style, outlines and colours. Flat even light with two-tone cel shading only: no "
             "rim light, no pale glow or light line along the edges, no cast shadows, no ground shadow. Plain flat "
             "purple background, nothing else in the picture, no text."),
}

name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
src = Path(sys.argv[3]) if len(sys.argv) > 3 else ROOT / "art/raw/char_hero_bare.png"
im = np.array(Image.open(src).convert("RGBA"))
im[..., 3] = np.where(im[..., 3] < 10, 0, im[..., 3])
fig = Image.fromarray(im)
box = fig.getbbox()
fig = fig.crop(box)
W, H = 1536, 1536
canvas = Image.new("RGBA", (W, H), PURPLE + (255,))
canvas.alpha_composite(fig, ((W - fig.width) // 2, (H - fig.height) // 2))
buf = io.BytesIO()
canvas.convert("RGB").save(buf, "PNG")
canvas.convert("RGB").save(OUT / f"{name}_input.png")

c = Comfy("http://127.0.0.1:8188")
c.check()
ref = c.upload(f"hero2d_{name}_input.png", buf.getvalue())
for s in seeds:
    out = OUT / f"{name}_s{s}.png"
    if out.exists():
        continue
    t = time.time()
    out.write_bytes(c.generate(P[name], [ref], None, s, 30))
    print("QWEN", out.name, round(time.time() - t), "s", flush=True)
