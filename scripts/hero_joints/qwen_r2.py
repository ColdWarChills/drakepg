"""Round 2 Qwen edits of the picked drawing cutj3_s123 (same 1536 canvas, so the figure stays in place).
<venv python> -I -B qwen_r2.py <prompt name> <seeds a,b>
Writes art/redo/hero_joints/r2_<name>_s<seed>.png and logs each run in runs_r2.jsonl (round 1's runs.jsonl untouched)."""
import io
import json
import sys
import time
from pathlib import Path

from PIL import Image

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from qwen_batch import Comfy  # noqa: E402

OUT = ROOT / "art/redo/hero_joints"
SRC = OUT / "cutj3_s123.png"

KEEP = ("Keep everything else exactly the same: his pose, his size and his place in the picture, his face, his hair, "
        "the red headband with its two tails, both arms with their long plain cream sleeves down to the wrists, both "
        "fists, the wide brown belt with its square buckle, the plain dark teal trousers. ")
LOOK = ("Exactly the same clean cartoon art style: flat cel colours with hard-edged two-tone shadows, darker coloured "
        "inner lines only, no thick black ink outlines. Flat even light: no rim light, no pale glow or light line along "
        "the edges, no cast shadows. Plain flat purple background, nothing else in the picture, no text.")


def vest(cloth):
    return ("Edit <image1>. Change only his clothes on the chest: he now wears a plain sleeveless vest over his cream "
            "shirt. " + KEEP + "The vest: " + cloth + ", sleeveless, closed down the front with no buttons, no pockets, "
            "no patterns and no trim, covering his chest and belly from the shoulders down to the belt; the bottom of "
            "the vest goes under the belt. The cream shirt's high round collar shows above the vest, and the long "
            "plain cream shirt sleeves come out of the vest's armholes unchanged, the same width as before. " + LOOK)


P = {
    "vest_teal": vest("plain dark teal cloth, the same dark teal as his trousers, clearly darker than the cream sleeves"),
    "vest_brown": vest("plain warm mid-brown leather, a little lighter and warmer than the belt, clearly darker than "
                       "the cream sleeves"),
    "fist": ("Edit <image1>. Keep the hero exactly the same: his pose, his size and place in the picture, his face, "
             "hair, headband, clothes and his left arm stay exactly as they are. Change only his right arm, the arm on "
             "the left of the picture: he now holds a plain sword in his right hand at the ready, the elbow bent, the "
             "forearm level and pointing forward to the right in front of his belt, the big closed fist in front of "
             "his belly with the back of the hand and the four knuckles toward us and the thumb on top, wrapped "
             "tightly around the sword's brown leather-wrapped handle. The handle passes straight through the fist; "
             "the blade points up and forward to the right at forty-five degrees, a small steel crossguard just above "
             "the fist and a round pommel just below it. The long plain cream sleeve ends at the wrist. " + LOOK),
}

if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    buf = io.BytesIO()
    Image.open(SRC).convert("RGB").save(buf, "PNG")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    ref = c.upload("hero_joints_r2_src.png", buf.getvalue())
    for s in seeds:
        out = OUT / f"r2_{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], [ref], None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "runs_r2.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "src": SRC.name, "seconds": dt, "prompt": P[name]}) + "\n")
