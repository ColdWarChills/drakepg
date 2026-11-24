"""Dragon Crashers test 2 (clothes only), step 1: Qwen repaints the knight's `torso` drawing as our armor.
<venv python> -I -B t2_qwen.py <armor scale|leather> <seeds a,b,c> [prompt version, default 1]
Reads art/tests/dc/t2/torso_orig.png (the knight's torso pixels, exported by DcClothes.Export in Unity) and our hero's
chest from art/raw/char_hero_<armor>.png (main checkout). Writes art/tests/dc/t2/qwen/<armor>_v<n>_s<seed>.png at the
input canvas size, plus the inputs and a runs.jsonl log. Budget (stop rule): 3 seeds per armor, one prompt revision.
Nothing here is committed from art/tests/dc/ (Dragon Crashers pixels)."""
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

T2 = ROOT / "art/tests/dc/t2"
OUT = T2 / "qwen"
RAW = Path(r"C:/Users/Epic/Desktop/dragonfab/art/raw")
SCALE = 4                      # torso 153x298 -> 612x1192
CANVAS = (768, 1344)           # multiple of 32, margin around the torso
OFFSET = ((CANVAS[0] - 153 * SCALE) // 2, (CANVAS[1] - 298 * SCALE) // 2)
BG = (0, 168, 72)              # flat green, in neither armor
CHEST = (362, 500, 512, 765)   # our hero's chest and belt in the 1024x1536 raw art

LOOK = ("Flat cel colours with hard-edged two-tone shadows, darker coloured inner lines, no thick black outlines, no "
        "rim light, no pale or golden edge glow, no soft airbrush. Plain flat green background, the same green as in "
        "<image1>, nothing else in the picture, no text.")
SHAPE = ("Keep the exact same outline and silhouette as <image1>: the same shape, size and position, every edge in the "
         "same place. Fill the whole shape right to its edges: nothing added outside it, no green left inside it. Use "
         "<image2> only as an example of the style and armor design, not its shape or framing: draw no arms, no hands, "
         "no head, no shoulder plates, no skin.")
PIECE = ("<image1> is one cut-out clothing piece of a cartoon knight seen from the side: the garment of his chest and "
         "waist, with a hood collar at the top, the chest, a belt with a round buckle, and a cloth strip hanging below "
         "the belt.")
P = {
    ("scale", 1): ("Edit <image1>. " + PIECE + " Repaint this piece as scale mail armor like the one in <image2>: rows "
                   "of overlapping steel scales over the whole chest and belly, a dark brown leather collar band along "
                   "the top where the hood is, a brown leather belt with a square metal buckle where the belt is, and "
                   "the strip hanging below the belt made of the same overlapping steel scales. " + SHAPE + " " + LOOK),
    ("leather", 1): ("Edit <image1>. " + PIECE + " Repaint this piece as a brown leather cuirass like the one in "
                     "<image2>: shaped brown leather chest plates with stitched seams over the whole chest and belly, "
                     "a darker brown leather collar band along the top where the hood is, a dark brown leather belt "
                     "with a square metal buckle where the belt is, and the strip hanging below the belt a brown "
                     "leather flap with stitched edges. " + SHAPE + " " + LOOK),
}
ARMOR_FILE = {"scale": "char_hero_scale_mail.png", "leather": "char_hero_leather_armor.png"}


def png(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


def torso_input() -> Image.Image:
    t = Image.open(T2 / "torso_orig.png").convert("RGBA")
    big = t.resize((t.width * SCALE, t.height * SCALE), Image.BICUBIC)
    canvas = Image.new("RGBA", CANVAS, BG + (255,))
    canvas.alpha_composite(big, OFFSET)
    return canvas.convert("RGB")


def chest_ref(armor: str) -> Image.Image:
    c = np.asarray(Image.open(RAW / ARMOR_FILE[armor]).convert("RGB").crop(CHEST)).astype(int)
    purple = (c[..., 0] > 120) & (c[..., 2] > 150) & (c[..., 1] < 110)
    c[purple] = 255
    img = Image.fromarray(c.astype(np.uint8))
    return img.resize((img.width * 3, img.height * 3), Image.LANCZOS)


if __name__ == "__main__":
    armor, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    version = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    OUT.mkdir(parents=True, exist_ok=True)
    inp, ref = torso_input(), chest_ref(armor)
    inp.save(OUT / "input_torso.png")
    ref.save(OUT / f"input_ref_{armor}.png")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    names = [c.upload("dc_t2_torso.png", png(inp)), c.upload(f"dc_t2_ref_{armor}.png", png(ref))]
    for s in seeds:
        out = OUT / f"{armor}_v{version}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        data = c.generate(P[(armor, version)], names, None, s, 30)
        img = Image.open(io.BytesIO(data)).convert("RGB")
        note = ""
        if img.size != CANVAS:
            note = f"resized from {img.size}"
            img = img.resize(CANVAS, Image.LANCZOS)
        img.save(out)
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", note, flush=True)
        with open(OUT / "runs.jsonl", "a") as f:
            f.write(json.dumps({"armor": armor, "version": version, "seed": s, "seconds": dt, "note": note,
                                "prompt": P[(armor, version)]}) + "\n")
