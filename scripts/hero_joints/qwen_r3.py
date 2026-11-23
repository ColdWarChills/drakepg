"""Round 3 Qwen edits of the picked vest drawing r2_vest_teal_s202 (same 1536 canvas): short cap shoulders on the vest.
<venv python> -I -B qwen_r3.py <prompt name> <seeds a,b>
Writes art/redo/hero_joints/r3_<name>_s<seed>.png and logs each run in runs_r3.jsonl (earlier logs untouched)."""
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
SRC = OUT / "r2_vest_teal_s202.png"

KEEP = ("Keep everything else exactly the same: his pose, his size and his place in the picture, his face, his hair, "
        "the red headband with its two tails, both arms with their long plain cream sleeves down to the wrists, both "
        "fists, the wide brown belt with its square buckle, the plain dark teal trousers, the vest's colour and the "
        "high round cream collar. ")
LOOK = ("Exactly the same clean cartoon art style: flat cel colours with hard-edged two-tone shadows, darker coloured "
        "inner lines only, no thick black ink outlines. Flat even light: no rim light, no pale glow or light line along "
        "the edges, no cast shadows. Plain flat purple background, nothing else in the picture, no text.")

P = {
    "cap": ("Edit <image1>. Change only the vest's shoulders: the dark teal vest now has short cap shoulders that cover "
            "the tops of both arms. Each cap is the same plain dark teal cloth as the vest, a smooth rounded cap over "
            "the top of the shoulder that comes down over the top of the upper arm and ends about a hand's width below "
            "the shoulder in a clean, slightly curved hemmed edge with a darker teal line; below the cap the cream "
            "shirt sleeve comes out from under it, unchanged, with a small hard shadow under the cap's edge. " + KEEP +
            LOOK),
}

if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    buf = io.BytesIO()
    Image.open(SRC).convert("RGB").save(buf, "PNG")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    ref = c.upload("hero_joints_r3_src.png", buf.getvalue())
    for s in seeds:
        out = OUT / f"r3_{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], [ref], None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "runs_r3.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "src": SRC.name, "seconds": dt, "prompt": P[name]}) + "\n")
