#!/usr/bin/env python3
"""Base-hero variants as edits of one anchor image (docs/PLANS.md, Art): only the head changes, so body, pose,
clothes, lighting and style stay identical across the variants.

  python scripts/hero_edits.py                    # every variant in HEADS from the anchor
  python scripts/hero_edits.py braid --seeds 2    # one variant, two seeds
Options: --first N (start N seeds after the anchor's), --anchor DIR/KEY (default poster_color/base_messy: art/tests/<DIR>/_raw/<KEY>_s<seed>.png, the picked seed)
Writes art/tests/<DIR>/_raw/edit_<variant>_s<seed>.png (+ .txt), and art/tests/<DIR>/edit_<variant>.png from the first.
"""
import io
import sys
import time
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import SUBJECTS, TESTS, finish, write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ANCHOR = ("poster_color", "base_messy", 1831856038)
HEADS = {
    "braid": ("Short braid", "a young woman with copper-red hair in a short thick braid over one shoulder, freckles and "
              "a determined look"),
    "crop": ("Cropped hair", "short cropped black hair, warm brown skin on the face, neck, arms and hands, bright eyes and "
             "an easygoing grin"),
    "wild": ("Wild white hair", "wild spiky white hair, a small bandage across the bridge of the nose and sharp amber "
             "eyes"),
}


def prompt(head: str) -> str:
    return (f"{RGBA_OPEN} Edit <image1>: change only the hero's head and face to {head}. Remove the red headband. "
            "Keep everything else exactly "
            "the same: the same body, pose, proportions, hands, shirt, belt, trousers and boots, the same colors, the same "
            "warm rim light and the same flat graphic poster art style, the same size and position. "
            f"{RGBA_CLOSE}")


def on_white(path: Path) -> bytes:
    img = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def main() -> None:
    args = sys.argv[1:]
    seeds = 1
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    first = 0
    if "--first" in args:  # seed offset, to try more seeds after the first ones
        i = args.index("--first")
        first = int(args[i + 1])
        del args[i:i + 2]
    names = args or list(HEADS)
    d, key, seed = ANCHOR
    anchor = TESTS / d / "_raw" / f"{key}_s{seed}.png"
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    ref = comfy.upload(f"drakepg_anchor_{key}.png", on_white(anchor))
    for name in names:
        _, head = HEADS[name]
        for s in range(seed + first, seed + first + seeds):
            t0 = time.time()
            data = comfy.generate(prompt(head), [ref], None, s, 30)
            raw = TESTS / d / "_raw" / f"edit_{name}_s{s}.png"
            raw.write_bytes(data)
            write_notes(raw.with_suffix(".txt"), comfy.last_graph)
            if s == seed + first:
                finish(Image.open(raw), SUBJECTS[key], None).save(TESTS / d / f"edit_{name}.png")
            print(f"edit_{name}: {raw.relative_to(TESTS.parent.parent)} ({time.time() - t0:.0f}s)", flush=True)


if __name__ == "__main__":
    main()
