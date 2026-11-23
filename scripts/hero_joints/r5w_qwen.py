"""Round 5 fix (the wind-up) Qwen edits for hero 4, same 1536 canvas as r2_vest_teal_s202. As qwen_r5.py.
<venv python> -I -B r5w_qwen.py <prompt name> <seeds a,b> [image1 key] [image2 key]
Keys are art/redo/hero_joints/<key>.png. Writes art/redo/hero_joints/r5w_<name>_s<seed>.png and logs each run in
r5w_runs.jsonl. Budget: two generation rounds.
Prompt names:
  wind   image1 = r5w_input (today's sword_wind drawing on hero 4's canvas, r5w_prep.py), image2 = s202: only the
         clothes change, to hero 4's, and the golden rim goes. Only the lower body (belt down) is used."""
import io
import json
import sys
import time
from pathlib import Path

from PIL import Image

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import qwen_r5  # noqa: E402  (its LOOK and OUTFIT text; nothing runs on import)
from qwen_r5 import LOOK, OUTFIT, OUT, png  # noqa: E402,F401
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from qwen_batch import Comfy  # noqa: E402

P = {
    "wind": ("Edit <image1>. Keep his pose exactly as it is: coiled to swing, facing right, feet wide apart, both knees "
             "bent, his weight back on the rear leg, the rear leg bent and the front leg reaching forward with its boot "
             "flat on the ground, his shoulders turned away, the arm nearest the viewer pulled back and up with its empty "
             "fist high behind his head, the other arm reaching forward with a clenched fist. Keep his face, his messy "
             "dark brown hair and the red headband with its two tails exactly as they are. Change only his clothes, so he "
             "is dressed exactly like the young man in <image2>: " + OUTFIT + ". The same colours as <image2>. Both hands "
             "are empty fists: no sword, no weapon. " + LOOK),
}
DEFAULT_REFS = {"wind": ["r5w_input", "r2_vest_teal_s202"]}


if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    keys = sys.argv[3:] or DEFAULT_REFS[name]
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    refs = [c.upload(f"hero_joints_r5w_{k}.png", png(k)) for k in keys]
    for s in seeds:
        out = OUT / f"r5w_{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], refs, None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "r5w_runs.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "refs": keys, "seconds": dt, "prompt": P[name]}) + "\n")
