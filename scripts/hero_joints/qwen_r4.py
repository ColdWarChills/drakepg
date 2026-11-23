"""Round 4 Qwen edits of the picked vest drawing r2_vest_teal_s202 (same 1536 canvas): the body for the sword wind-up.
<venv python> -I -B qwen_r4.py <prompt name> <seeds a,b>
Writes art/redo/hero_joints/r4_<name>_s<seed>.png and logs each run in runs_r4.jsonl (earlier logs untouched).
Prompt names: windup (approach A: the whole upper body in one edit). Approach B (if A fails): two narrow edits in turn."""
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

KEEP = ("Keep these exactly the same and exactly where they are: his head, face, messy dark brown hair and the red "
        "headband with its two tails, at the same size; the plain dark teal vest and the cream shirt with its high round "
        "cream collar and long plain cream sleeves down to the wrists; the wide brown belt with its square buckle at the "
        "same height; the plain dark teal trousers, both legs, the brown boots and his feet exactly where they are. "
        "Nothing in his hands: no sword, no weapon. ")
LOOK = ("Exactly the same clean cartoon art style: flat cel colours with hard-edged two-tone shadows, darker coloured "
        "inner lines only, no thick black ink outlines. Flat even light: no rim light, no pale glow or light line along "
        "the edges, no cast shadows. Plain flat purple background, nothing else in the picture, no text.")

P = {
    "windup": ("Edit <image1>. Change only his arms and the pose of his chest and shoulders: he is winding up to swing "
               "a sword down at an enemy on the right. The arm on the left side of the picture, the one nearest to the "
               "viewer, is raised up and back: its shoulder lifted high toward his ear and pulled back to the left, the "
               "upper arm pointing up and back to the upper left, the elbow bent and held up at the height of his head "
               "on the left side of the picture, its closed empty fist held high above and a little behind his head, "
               "as if a sword were cocked back over his shoulder. This raised arm stays on the left side of the picture "
               "and never crosses his face. His chest turns a little more toward us and his upper body leans back a "
               "little to the left, the shoulders tilted with the raised shoulder higher; we still see the front of his "
               "vest and his collar, not his back. The arm on the right side of the picture reaches forward to the "
               "right at chest height with a clenched fist, for balance. His head still faces right, looking at the "
               "enemy. " + KEEP + LOOK),
}

if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    src = Path(sys.argv[3]) if len(sys.argv) > 3 else SRC
    buf = io.BytesIO()
    Image.open(src).convert("RGB").save(buf, "PNG")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    ref = c.upload(f"hero_joints_r4_{src.stem}.png", buf.getvalue())
    for s in seeds:
        out = OUT / f"r4_{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], [ref], None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "runs_r4.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "src": src.name, "seconds": dt, "prompt": P[name]}) + "\n")
