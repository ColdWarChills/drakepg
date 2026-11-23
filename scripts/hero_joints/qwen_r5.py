"""Round 5 (attempt 2: the strike drawings) Qwen edits for hero 4, same 1536 canvas as r2_vest_teal_s202.
<venv python> -I -B qwen_r5.py <prompt name> <seeds a,b> [image1 key] [image2 key]
Keys are art/redo/hero_joints/<key>.png. Writes art/redo/hero_joints/r5_<name>_s<seed>.png and logs each run in
r5_runs.jsonl (earlier logs untouched). Stop rule: at most 8 runs in all.
Prompt names:
  strike  (approach A) image1 = r5_input (today's strike drawing on hero 4's canvas, r5_prep.py), image2 = s202:
          only the clothes change, to hero 4's, and the golden rim goes.
  noarm   image1 = the picked strike: the arm nearest the viewer removed (the body behind it shown), for the pixels
          under the painted arm.
  shout   image1 = s202: only the face: mouth open in a shout, brows down (the head piece if the strike's head drifts).
  lunge   (approach B) image1 = s202, image2 = r5_input as the pose to copy."""
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
LOOK = ("Exactly the same clean cartoon art style as <image2>: flat cel colours with hard-edged two-tone shadows, "
        "darker coloured inner lines only, no thick black ink outlines. Flat even light: no rim light, no golden or "
        "yellow light line along his edges, no pale glow, no cast shadows. Plain flat purple background, nothing else in "
        "the picture, no text.")
LOOK1 = LOOK.replace("as <image2>", "")
OUTFIT = ("a plain dark teal sleeveless vest, closed, over a cream shirt with a high round cream collar and long plain "
          "slim cream sleeves down to the wrists, each ending in a narrow cream cuff, the sleeves exactly as slim as in "
          "<image2>, not puffy, not rolled up; a wide brown belt with a square buckle; plain dark teal trousers; brown "
          "boots")

P = {
    "strike": ("Edit <image1>. Keep his pose exactly as it is: the deep forward lunge facing right, the front knee bent "
               "and the back leg stretched far out behind, his upper body bent far forward over the front leg, the arm "
               "nearest the viewer hanging down in front of his front knee with its empty fist low, the other arm flung "
               "up and back behind him, his head thrust forward with the open-mouthed shout and lowered brows. Keep his "
               "face, his messy dark brown hair and the red headband with its two tails exactly as they are. Change only "
               "his clothes, so he is dressed exactly like the young man in <image2>: " + OUTFIT + ". The same colours "
               "as <image2>. Both hands are empty fists: no sword, no weapon. " + LOOK),
    "noarm": ("Edit <image1>. Remove the arm on the near side completely, the one nearest the viewer that hangs down in "
              "front of his body and his front knee, with its cream sleeve and its fist: show instead the teal vest, "
              "the belt and the dark teal trousers and boot that are behind it, as if that arm were not there. Keep "
              "everything else exactly the same and exactly where it is: his head, face, hair, red headband and tails, "
              "the other arm raised behind him, the vest, belt, legs and boots, the same size, the same pose. " + LOOK1),
    "shout": ("Edit <image1>. Change only his face: his mouth is wide open in a fierce battle shout, showing his upper "
              "teeth and tongue, his brows pulled down hard into an angry frown, his eyes narrowed and fierce, looking "
              "right. Keep everything else exactly the same and exactly where it is: the same head size, the same head "
              "position and tilt, the messy dark brown hair, the red headband and its two tails, the neck, the collar, "
              "the clothes, both arms, the legs and the pose. " + LOOK1),
    "lunge": ("Edit <image1>. Change only his pose, to the pose of the hero in <image2>: a deep forward lunge facing "
              "right, the front knee bent and the back leg stretched far out behind, his upper body bent far forward "
              "over the front leg, the arm nearest the viewer hanging down in front of his front knee with its empty "
              "fist low, the other arm flung up and back behind him, his head thrust forward, mouth wide open in a "
              "fierce shout, brows lowered. Keep his face, hair, red headband with two tails and all his clothes exactly "
              "as in <image1>: " + OUTFIT.replace("<image2>", "<image1>") + ". Keep his size and body proportions as in "
              "<image1>, his feet on the same ground line. Both hands are empty fists: no sword, no weapon. Exactly the "
              "same clean cartoon art style as <image1>, not the style of <image2>: flat cel colours with hard-edged "
              "two-tone shadows, darker coloured inner lines only, no rim light, no golden line along his edges. Plain "
              "flat purple background, nothing else in the picture, no text."),
}
DEFAULT_REFS = {"strike": ["r5_input", "r2_vest_teal_s202"], "shout": ["r2_vest_teal_s202"],
                "lunge": ["r2_vest_teal_s202", "r5_input"]}


def png(key):
    buf = io.BytesIO()
    Image.open(OUT / f"{key}.png").convert("RGB").save(buf, "PNG")
    return buf.getvalue()


if __name__ == "__main__":
    name, seeds = sys.argv[1], [int(s) for s in sys.argv[2].split(",")]
    keys = sys.argv[3:] or DEFAULT_REFS[name]
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    refs = [c.upload(f"hero_joints_r5_{k}.png", png(k)) for k in keys]
    for s in seeds:
        out = OUT / f"r5_{name}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        out.write_bytes(c.generate(P[name], refs, None, s, 30))
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", flush=True)
        with open(OUT / "r5_runs.jsonl", "a") as f:
            f.write(json.dumps({"name": name, "seed": s, "refs": keys, "seconds": dt, "prompt": P[name]}) + "\n")
