#!/usr/bin/env python3
"""Split a generated battle scene (theme_tests.py --bare) into parts the game can place on its own: the background
without characters (an edit of the scene) and the hero and monster as transparent sprites redrawn from it.

  python scripts/scene_parts.py                 # bg, hero and boar from SCENE, 2 seeds each
  python scripts/scene_parts.py hero --seeds 3
Writes art/tests/themes/parts/_raw/<part>_s<seed>.png (+ .txt).
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import TESTS, write_notes  # noqa: E402
from hero_edits import on_white  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

SCENE = TESTS / "themes" / "_raw" / "paper_bare_s1310351383.png"
DIR = TESTS / "themes" / "parts"
SPRITE = (f"{RGBA_OPEN} {{what}} Exactly the same character, colours and art style as in <image1>: flat shapes with "
          f"hard-edged shadows, a warm golden rim light. Full body, nothing else in the picture: no ground, no "
          f"shadow, no background. {RGBA_CLOSE}")
# name: (size or None for an edit at the scene's size, prompt)
PARTS = {
    "bg": (None, "Remove the young hero and the wild boar completely. Fill in the forest, bushes and ground behind "
                 "them so the clearing is empty. Keep everything else exactly the same: the same paper-cut layers, "
                 "colours, light and framing."),
    "hero": ((1024, 1536), SPRITE.format(what=(
        "The young hero from <image1> (messy dark brown hair, red headband, cream shirt, dark teal trousers, brown "
        "boots) alone, standing in a fighting stance facing right, holding a short sword low in his right hand."))),
    "boar": ((1536, 1024), SPRITE.format(what=(
        "The big angry wild boar from <image1> (mossy green back, curved tusks) alone, facing left, standing on all "
        "four legs."))),
}


def main() -> None:
    args = sys.argv[1:]
    seeds = 2
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    scene = comfy.upload("drakepg_scene.png", on_white(SCENE))
    raw = DIR / "_raw"
    raw.mkdir(parents=True, exist_ok=True)
    for part in args or list(PARTS):
        size, prompt = PARTS[part]
        for i in range(seeds):
            seed = 4242 + i
            out = raw / f"{part}_s{seed}.png"
            if out.exists():
                continue
            print(out.stem, flush=True)
            out.write_bytes(comfy.generate(prompt, [scene], size, seed, 30))
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


if __name__ == "__main__":
    main()
