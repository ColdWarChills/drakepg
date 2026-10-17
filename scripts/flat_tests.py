#!/usr/bin/env python3
"""Round 13 (docs/PLANS.md, Art): a strict flat-vector look asked for at generation time (a post-filter on painted
images turned them into blobs). No references (they pull in the soft anime face).

  python scripts/flat_tests.py                 # every subject, 3 seeds
  python scripts/flat_tests.py hero --seeds 2
Writes art/tests/flat/_raw/<subject>_s<seed>.png (+ .txt).
"""
import sys
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import TESTS, write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

DIR = TESTS / "flat"
STYLE = ("Flat vector illustration built from simple geometric shapes: circles, rounded rectangles and triangles. "
         "Solid flat colours only, no gradients, no soft shading, no texture, no fur or hair strands, no tiny details. "
         "At most one hard-edged flat shadow shape per part. A uniform thick dark outline of the same width everywhere. "
         "A small limited palette.")
SPRITE = f"{RGBA_OPEN} {{what}} {STYLE} Full body, side view, centered, nothing else in the picture. {RGBA_CLOSE}"
SUBJECTS = {
    "hero": ((1024, 1536), SPRITE.format(what=(
        "A young wandering hero for a 2D fantasy game, standing in a relaxed fighting stance facing right, about four "
        "heads tall. Simple face: two small dot eyes, short eyebrows and a small line mouth. Messy dark brown hair as "
        "one solid spiky shape, a red headband with two short tails, a cream shirt with rolled sleeves, a brown belt, "
        "dark teal trousers and brown boots. Both hands are empty closed fists."))),
    "boar": ((1536, 1024), SPRITE.format(what=(
        "A stocky angry wild boar for a 2D fantasy game, facing left. Its brown body is one big rounded shape, a row of "
        "pale wooden thorn spikes along its back drawn as simple triangles, two curved cream tusks, a pink snout, a "
        "small dot eye under a heavy angry brow, short sturdy legs."))),
    "boar2": ((1536, 1024), SPRITE.format(what=(
        "A tough angry wild boar for a 2D fantasy game, facing left, with a strong readable silhouette: big hunched "
        "shoulders much higher than its hips, a heavy head held low, thick curved cream tusks, a pink snout, a small "
        "glaring eye under a heavy brow, short thick legs with dark hooves. A bristly mane along its back drawn as a "
        "few big jagged shapes, with pale wooden thorn spikes sticking out of it. Dark brown body with a lighter "
        "belly shape and one flat darker shadow shape on the far side."))),
    "forest": ((1664, 928), (
        "A wide 16:9 background for a 2D fantasy game: a forest path in the late afternoon. " + STYLE.replace(
            "A uniform thick dark outline of the same width everywhere. ", "") +
        " Three depth layers, each one flat colour with a lighter top edge: far hills, a row of trees drawn as simple "
        "triangles and rounded blobs, and near bushes at the sides. A clean flat sky with two flat clouds. The lower "
        "third is open flat ground with a dirt path. No characters, no text.")),
}


def seed_for(subject: str, i: int) -> int:
    return (zlib.crc32(("flat_" + subject).encode()) & 0x7FFFFFFF) + i


def main() -> None:
    args = sys.argv[1:]
    seeds = 3
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    raw = DIR / "_raw"
    raw.mkdir(parents=True, exist_ok=True)
    for subject in args or list(SUBJECTS):
        size, prompt = SUBJECTS[subject]
        for i in range(seeds):
            seed = seed_for(subject, i)
            out = raw / f"{subject}_s{seed}.png"
            if out.exists():
                continue
            print(f"{subject} seed {seed}", flush=True)
            out.write_bytes(comfy.generate(prompt, [], size, seed, 30))
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


if __name__ == "__main__":
    main()
