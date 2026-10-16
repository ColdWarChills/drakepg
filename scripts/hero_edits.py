#!/usr/bin/env python3
"""Base-hero variants as Qwen edits of one anchor image (docs/PLANS.md, Art): only the asked part changes, so the
rest (body, clothes, pose, light, style) stays identical across variants. Used for the head variants (round 10) and the
character-creator options (round 11: body types, hair, faces).

  python scripts/hero_edits.py                     # every edit in EDITS
  python scripts/hero_edits.py hair_long body_curvy --seeds 2 --first 2
Options: --seeds N (default 1), --first N (start N seeds after the anchor's seed)
Writes art/tests/poster_color/_raw/edit_<name>_s<seed>.png (+ .txt), and art/tests/poster_color/edit_<name>.png from
the first seed.
"""
import io
import sys
import time
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import SUBJECTS, TESTS, finish, write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

DIR = TESTS / "poster_color"
# Anchors: raw generations (file stem in _raw/, seed used for the edits).
ANCHORS = {
    "messy": ("base_messy_s1831856038", 1831856038),  # young man, messy hair, red headband
    "braid": ("edit_braid_s1831856039", 1831856039),  # young woman, copper braid (an edit of messy)
    "shaved": ("edit_hair_shaved_s1831856038", 1831856038),  # the base body for hair/headband layers (an edit of messy)
}
KEEP = ("Keep everything else exactly the same: the same pose, the same cream shirt, brown belt, dark teal trousers and "
        "brown boots, the same colors, the same warm rim light and the same flat graphic poster art style, the same size "
        "and position.")


def head(change: str) -> str:
    return f"change only the hero's head and face to {change}. Remove the red headband. Keep the same body. {KEEP}"


def hair(change: str) -> str:
    return f"change only the hair to {change}. Remove the red headband. Keep the same face and body. {KEEP}"


def face(change: str) -> str:
    return f"change only the face to {change}. Keep the same hair, the red headband and the same body. {KEEP}"


def body(change: str) -> str:
    return (f"change only the body shape to {change}, with the same clothes fitted to the new body. Keep the same head, "
            f"face and hair. {KEEP}")


# name: (label, anchor, instruction)
EDITS = {
    # round 10: head variants
    "braid": ("Short braid", "messy", head("a young woman with copper-red hair in a short thick braid over one shoulder, "
                                           "freckles and a determined look")),
    "crop": ("Cropped hair", "messy", head("short cropped black hair, warm brown skin on the face, neck, arms and hands, "
                                           "bright eyes and an easygoing grin")),
    "wild": ("Wild white hair", "messy", head("wild spiky white hair, a small bandage across the bridge of the nose and "
                                              "sharp amber eyes")),
    # round 11: character-creator options
    "body_slim": ("Slim", "messy", body("a very thin, lanky build: clearly narrower shoulders, a thin waist and noticeably thinner arms and legs")),
    "body_broad": ("Broad", "messy", body("a much bigger, broad and bulky build: very wide shoulders, a "
                                          "thick barrel chest, a wider waist and heavy, thick arms and legs")),
    "body_fslim": ("Slim, small chest", "braid", body("a slim young woman's build with a small chest and narrow hips")),
    "body_curvy": ("Curvy", "braid", body("a curvy young woman's build with a fuller chest and wider hips")),
    "hair_long": ("Long, tied back", "messy", hair("long dark brown hair tied back in a low ponytail")),
    "hair_undercut": ("Undercut", "messy", hair("a dark brown undercut: shaved sides and a longer swept-back top")),
    "hair_curly": ("Curly", "messy", hair("a big mass of thick dark brown curly hair, medium length, with no headband")),
    "hair_bun": ("Top knot", "messy", hair("dark brown hair pulled up into a top knot bun")),
    "hair_shaved": ("Shaved", "messy", hair("a cleanly shaved head with short stubble")),
    "hair_bob": ("Straight bob", "messy", hair("smooth, straight chin-length dark brown hair cut in a neat bob with a straight-cut fringe across the forehead")),
    "noband": ("Messy, no band", "messy", "remove only the red headband and draw the same messy dark brown hair where it "
               f"was. Keep the same face, hair style and body. {KEEP}"),
    "band_on_shaved": ("Headband on the base", "shaved", "add a red cloth headband around the forehead, tied at the "
                       "back of the head with two short loose tails. Keep the same shaved head, face and body. " + KEEP),
    "face_round": ("Round, friendly", "messy", face("a round, friendly face with soft features and a warm smile")),
    "face_sharp": ("Sharp, serious", "messy", face("a lean, sharp face with high cheekbones, narrow eyes and a serious "
                                                   "look")),
    "face_beard": ("Short beard", "messy", face("a slightly older face with a short dark beard and a calm look")),
}


def prompt(instruction: str) -> str:
    return f"{RGBA_OPEN} Edit <image1>: {instruction} {RGBA_CLOSE}"


def on_white(path: Path) -> bytes:
    img = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def main() -> None:
    args = sys.argv[1:]

    def opt(name: str, default: int) -> int:
        if name in args:
            i = args.index(name)
            value = int(args[i + 1])
            del args[i:i + 2]
            return value
        return default

    seeds, first = opt("--seeds", 1), opt("--first", 0)
    names = args or list(EDITS)
    unknown = [n for n in names if n not in EDITS]
    if unknown:
        sys.exit(f"Unknown edits: {', '.join(unknown)} (have {', '.join(EDITS)})")
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    uploaded: dict[str, str] = {}
    for name in names:
        _, anchor, instruction = EDITS[name]
        stem, seed = ANCHORS[anchor]
        if anchor not in uploaded:
            uploaded[anchor] = comfy.upload(f"drakepg_anchor_{anchor}.png", on_white(DIR / "_raw" / f"{stem}.png"))
        for s in range(seed + first, seed + first + seeds):
            t0 = time.time()
            data = comfy.generate(prompt(instruction), [uploaded[anchor]], None, s, 30)
            raw = DIR / "_raw" / f"edit_{name}_s{s}.png"
            raw.write_bytes(data)
            write_notes(raw.with_suffix(".txt"), comfy.last_graph)
            if s == seed + first:
                finish(Image.open(raw), SUBJECTS["base_messy"], None).save(DIR / f"edit_{name}.png")
            print(f"edit_{name}: {raw.relative_to(TESTS.parent.parent)} ({time.time() - t0:.0f}s)", flush=True)


if __name__ == "__main__":
    main()
