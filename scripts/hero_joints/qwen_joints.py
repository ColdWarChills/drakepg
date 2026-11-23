"""Qwen edits of the drawn hero (art/raw/char_hero_bare.png) into a hero made for joints.
<venv python> -I -B qwen_joints.py <prompt name> <seeds a,b,c> [src png]
Writes art/redo/hero_joints/<name>_s<seed>.png and logs each run in runs.jsonl. One reference only (the hero on flat
purple, 1536 canvas, as scripts/hero2d/qwen_pose.py)."""
import io
import json
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from qwen_batch import Comfy  # noqa: E402

PURPLE = (163, 55, 211)
OUT = ROOT / "art/redo/hero_joints"
OUT.mkdir(parents=True, exist_ok=True)

LOOK = ("Exactly the same clean cartoon art style and colours as <image1>: flat cel colours with hard-edged two-tone "
        "shadows, darker coloured inner lines only, no thick black ink outlines. Full body from the top of his hair "
        "to the soles of his boots, the same size as in <image1>. Flat even light: no rim light, no pale glow or light "
        "line along the edges, no cast shadows, no ground shadow. Plain flat purple background, nothing else in the "
        "picture, no text.")

P = {
    # try 1: the cutting pose with clothes that hide the joints
    "cutj": ("Edit <image1>. Redraw the same young hero in a neutral pose for a cutout puppet, in new simple clothes. "
             "Keep exactly his face, his spiky messy dark brown hair, the red headband with its two loose tails, his "
             "skin colour, his sturdy cartoon proportions and his big fists. The same three-quarter view: he faces "
             "right with his chest turned toward us. He stands upright. Both arms are long and strong, held straight "
             "and away from his body, about 45 degrees out from his sides, with a wide gap between each arm and his "
             "body; the elbows are straight. Both hands are closed in big fists with the backs of the hands toward us. "
             "He wears a long-sleeved plain cream tunic: each sleeve is one long smooth plain tube from the shoulder all "
             "the way down to the wrist, a little loose, as thick as a strong forearm, with no folds, no wrinkles, no "
             "seams, no bands, no patches and no shading lines at the elbow or the shoulder; each sleeve ends at the "
             "wrist in a plain narrow cuff and the fist comes out of the cuff. A high plain round collar around his "
             "neck. A wide brown leather belt with a square buckle around his waist. Plain dark teal trousers, smooth "
             "and simple with no folds or patches at the knees. His legs are straight and spread wide apart like an "
             "upside-down letter V, with a clear gap between the legs; plain tall brown boots without folded tops, "
             "both pointing to the right. Nothing overlaps: arms, legs and body are all clear of each other. " + LOOK),
    # try 2 (after s101-s104: sleeves fine, but he turned to face us and grew taller and slimmer)
    "cutj2": ("Edit <image1>. Redraw the same young hero in a neutral pose for a cutout puppet, in new simple clothes. "
              "Keep exactly his face, his spiky messy dark brown hair, the red headband with its two loose tails, his "
              "skin colour, his big head, his short sturdy stocky cartoon proportions (about five heads tall) and his "
              "big fists, and keep him exactly as tall as in <image1>. Keep the same three-quarter view as in "
              "<image1>: his whole body is turned toward the right, his chest seen from the front-right, his right "
              "shoulder nearer to us on the left of the picture and his left shoulder further away, partly behind his "
              "chest. He stands upright. Both arms are long and thick, held straight and away from his body, about 45 "
              "degrees out from his sides, with a wide gap between each arm and his body; the elbows are straight. "
              "Both hands are closed in big fists with the backs of the hands toward us. He wears a long-sleeved plain "
              "cream tunic: each sleeve is one long smooth plain tube from the shoulder all the way down to the wrist, "
              "a little loose, as thick as a strong forearm, with no folds, no wrinkles, no seams, no bands, no "
              "patches and no shading lines at the elbow or the shoulder; each sleeve ends at the wrist in a plain "
              "narrow cuff and the fist comes out of the cuff. A high plain round collar around his neck. A wide brown "
              "leather belt with a square buckle around his waist. Plain dark teal trousers, smooth and simple with no "
              "folds or patches at the knees. His legs are straight and spread apart like an upside-down letter V, "
              "with a clear gap between the legs; plain brown boots without folded tops, both pointing to the right. "
              "Nothing overlaps: arms, legs and body are all clear of each other. " + LOOK),
    # try 3 (after cutj2: an A pose keeps coming back facing us, tall and slim): keep the reference's own pose and
    # proportions, change only the clothes. Its near arm already hangs straight and clear; its far arm is already a
    # guard; the legs are below the test's frame.
    "cutj3": ("Edit <image1>. Change only his clothes. Keep the hero's pose, size, proportions and place in the picture "
              "exactly the same: his head, face, hair, the red headband with its tails, both arms, both fists and both "
              "legs stay exactly where they are. His right arm, on the left of the picture, hangs straight down beside "
              "his body with a small gap between the arm and his body. New clothes: a long-sleeved plain cream tunic; "
              "each sleeve is one long smooth plain tube from the shoulder all the way down to the wrist, a little "
              "loose, with no folds, no wrinkles, no seams, no bands, no patches and no shading lines at the elbow or "
              "the shoulder, ending at the wrist in a plain narrow cuff; the fist comes out of the cuff. A high plain "
              "round collar around his neck, no opening at the chest. A wide brown leather belt with a square buckle. "
              "Plain dark teal trousers, smooth and simple, no folds at the knees. Plain brown boots without folded "
              "tops. Exactly the same clean cartoon art style and colours: flat cel colours with hard-edged two-tone "
              "shadows, darker coloured inner lines only, no thick black ink outlines. Flat even light: no rim light, "
              "no pale glow or light line along the edges, no cast shadows, no ground shadow. Plain flat purple "
              "background, nothing else in the picture, no text."),
}

if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    src = Path(sys.argv[3]) if len(sys.argv) > 3 else ROOT / "art/raw/char_hero_bare.png"
    im = np.array(Image.open(src).convert("RGBA"))
    im[..., 3] = np.where(im[..., 3] < 10, 0, im[..., 3])
    fig = Image.fromarray(im)
    fig = fig.crop(fig.getbbox())
    W, H = 1536, 1536
    canvas = Image.new("RGBA", (W, H), PURPLE + (255,))
    canvas.alpha_composite(fig, ((W - fig.width) // 2, (H - fig.height) // 2))
    buf = io.BytesIO()
    canvas.convert("RGB").save(buf, "PNG")
    canvas.convert("RGB").save(OUT / f"{name}_input.png")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    ref = c.upload(f"hero_joints_{name}_input.png", buf.getvalue())
    for s in seeds:
        out = OUT / f"{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], [ref], None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "runs.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "src": src.name, "seconds": dt, "prompt": P[name]}) + "\n")
