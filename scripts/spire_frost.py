#!/usr/bin/env python3
"""The Spire's ice hall in the cutout style (docs/SPIRE_ASSETS.md). Text alone gave a rendered stone hall, so two routes:

  edit: a Qwen edit of the picked Thornwood hall (same layout and paper look, frozen over)
  refs: a new drawing with two finished cutout pictures as style references (that hall and a winter room)

Tries go to art/redo/bg_spire_frostfang/<route>_s<seed>.png; pick one with
  python scripts/redo_art.py pick bg_spire_frostfang:<file stem>  (copy the try over <key>_s<seed>.png first)

  python scripts/spire_frost.py edit refs --seeds 2
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import write_notes  # noqa: E402
from hero_edits import on_white  # noqa: E402
from qwen_batch import Comfy  # noqa: E402
from redo_art import RAW, REDO, SPIRE_HALL  # noqa: E402

FROZEN = ("the tree roots, moss, plants and green lanterns become ice, frost and snow; long icicles hang from the ceiling "
          "edge and the window arches; the stones are frosted pale blue-grey; snow lies drifted along the walls and the "
          "floor edges; the windows show white snowy peaks and a pale winter sky; two iron braziers with small orange "
          "flames stand by the side walls")
PAPER = ("layered paper-cut diorama: every stone, icicle, snowdrift and window shape is cut from matte coloured card paper "
         "and stacked in flat layers with soft shadows between the layers, flat colours with a fine paper grain, clean cut "
         "edges with thin white paper rims, no outlines, big simple shapes with few small details, a soft muted palette")

PROMPTS = {
    "edit": (f"Edit <image1>. Turn this hall into the frozen top hall of the same tower, high in snowy mountains: {FROZEN}. "
             f"Keep everything else exactly the same: the same {PAPER}; the same composition, walls, stairs, windows and "
             "light; the same flat, open flagstone floor across the bottom 30%, now frosted. No characters, no text.",
             ["bg_spire_thornwood"], None),
    "refs": ("A wide 16:9 background for a 2D side-view fantasy RPG. Inside the top hall of an ancient round stone tower "
             "high in snowy mountains: frost and long icicles on the grey stone walls, drifts of snow along the floor "
             "edges under tall arched windows that show white peaks and a pale winter sky, two iron braziers with small "
             "orange flames by the side walls, worn stone steps rising along the back wall. Only snow, ice, frosted stone "
             f"and iron inside the hall: no trees, no plants. Made as a {PAPER}, exactly the paper-cut look of <image1> and "
             "<image2>. Use <image1> only as an example of how an indoor stone hall is cut from paper, and <image2> only "
             "for the winter palette and the paper snow: none of their roots, trees, mountains or layout appear in this "
             f"scene. {SPIRE_HALL}",
             ["bg_spire_thornwood", "bg_frozen_roost"], (2048, 1152)),
}


def main() -> None:
    args = sys.argv[1:]
    seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 2
    routes = [a for a in args if a in PROMPTS] or list(PROMPTS)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    out_dir = REDO / "bg_spire_frostfang"
    out_dir.mkdir(parents=True, exist_ok=True)
    for route in routes:
        text, refs, size = PROMPTS[route]
        uploaded = [comfy.upload(f"drakepg_frost_{r}.png", on_white(RAW / f"{r}.png")) for r in refs]
        for i in range(seeds):
            seed = 4242 + i
            out = out_dir / f"{route}_s{seed}.png"
            if out.exists():
                continue
            print(out.relative_to(REDO.parent.parent), flush=True)
            out.write_bytes(comfy.generate(text, uploaded, size, seed, 30))
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


if __name__ == "__main__":
    main()
