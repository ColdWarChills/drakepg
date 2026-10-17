#!/usr/bin/env python3
"""Whole-screen style themes (docs/PLANS.md, Art): one battle-screen mock per theme with the UI, background, hero and a
monster together, so the look of everything is picked at once before separate buttons, icons and backgrounds are made.
The hero is the base hero (poster_color) passed as a reference, so only the theme around him changes.

  python scripts/theme_tests.py                      # every theme in THEMES, 1 seed
  python scripts/theme_tests.py tarot paper --seeds 2
  python scripts/theme_tests.py sheet tarot:1201 paper   # sheet from picked seeds (default: the first)
  python scripts/theme_tests.py paper --bare         # the same scene with no UI (the UI is drawn by hand)
Writes art/tests/themes/_raw/<theme>_s<seed>.png (+ .txt); the sheet goes to art/tests/themes.png and
art/tests/sheets/themes.webp.
"""
import sys
import zlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import TESTS, write_notes  # noqa: E402
from hero_edits import on_white  # noqa: E402
from qwen_batch import Comfy  # noqa: E402

DIR = TESTS / "themes"
HERO = TESTS / "poster_color" / "_raw" / "base_messy_s1831856038.png"
SIZE = (1664, 928)

# name: (label, UI look, world look)
THEMES = {
    "woodcut": ("Carved wood & parchment",
                "panels of dark carved wood with chiselled bevelled edges and small brass rivets, cream parchment insets "
                "behind the text, buttons shaped like wooden plaques and red wax seals, a chunky old-style serif font",
                "a flat woodblock-print landscape: layered hills and pine trees in a few flat inks, a fine carved "
                "wood-grain texture in the sky"),
    "paper": ("Layered paper",
              "panels built from stacked layers of coloured card paper with clean cut edges and soft drop shadows between "
              "the layers, rounded tab shapes, buttons like thick paper tokens, a friendly rounded bold font",
              "a paper-cut diorama forest: hills, trees and clouds cut from coloured paper in flat layers with soft "
              "shadows between them, gentle depth"),
    "tarot": ("Night & gold line",
              "deep indigo and near-black panels with thin gold linework, art-nouveau corner flourishes, skill buttons "
              "framed like small tarot cards, tiny star and moon ornaments, an elegant tall serif font",
              "a forest at dusk under a deep blue starry sky in flat jewel colours, a big pale moon, a few fireflies"),
    "sticker": ("Bold sticker",
                "chunky rounded panels with thick dark outlines and hard offset shadows, bright flat fills of teal, "
                "mustard and coral, pill-shaped buttons, round badge icons, a heavy rounded display font",
                "a sunny forest clearing made of simple bold shapes: big round trees, a clean sky, a few flat clouds"),
}


def prompt(ui: str, world: str) -> str:
    return (
        "A complete screenshot of a 2D turn-based fantasy RPG battle screen, 16:9. The characters are drawn as a flat "
        "graphic poster illustration in full colour: flat shapes with hard-edged shadows and no outlines, bold readable "
        "silhouettes, a warm golden rim light, minimal interior detail. On the left stands the hero from <image1>: "
        "exactly the same character, the same face, messy dark brown hair and red headband, cream shirt, brown belt, "
        "dark teal trousers and brown boots, now holding a short sword, facing right. On the right, facing left, a big "
        "angry wild boar with a mossy green back and curved tusks, in the same flat poster style. "
        f"Background: {world}. "
        f"The game's user interface is designed as {ui}. Top left: the hero's panel with a small round portrait, the "
        "name \"Wanderer  Lv 3\", a red HP bar \"48/60\" and a blue MP bar \"20/30\". Top right: the enemy plate "
        "\"Thornback Boar  Lv 3\" with a red HP bar, and under it the line \"Next: Gore\". Along the bottom: a bar with "
        "five square skill buttons with simple icons (a sword slash, a shield, a flame, a green leaf, a lightning bolt), "
        "each with a small number 1 to 5, then buttons \"Attack\", \"Items\" and \"Flee\". Crisp readable text, a clean "
        "balanced layout; the interface frames the scene and does not cover the characters."
    )


def bare_prompt(world: str) -> str:
    """The same scene with no interface: the UI is drawn by hand on top (round 12's AI UI read as AI)."""
    scene = prompt("", world).split(" The game's user interface")[0]
    return (scene.replace("A complete screenshot of a 2D turn-based fantasy RPG battle screen, 16:9.",
                          "A wide 16:9 battle scene for a 2D turn-based fantasy RPG.")
            + " The painted scene fills the whole picture edge to edge: open sky across the top, open ground across "
              "the bottom, the characters in the middle. No user interface, no panels, no buttons, no text, no white "
              "borders.")


def seed_for(theme: str, i: int) -> int:
    return (zlib.crc32(theme.encode()) & 0x7FFFFFFF) + i


def generate(themes: list[str], seeds: int, bare: bool = False) -> None:
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    hero = comfy.upload("drakepg_theme_hero.png", on_white(HERO))
    raw_dir = DIR / "_raw"
    raw_dir.mkdir(parents=True, exist_ok=True)
    for theme in themes:
        _, ui, world = THEMES[theme]
        for i in range(seeds):
            seed = seed_for(theme, i)
            out = raw_dir / f"{theme}{'_bare' if bare else ''}_s{seed}.png"
            if out.exists():
                continue
            print(f"{out.stem}", flush=True)
            text = bare_prompt(world) if bare else prompt(ui, world)
            out.write_bytes(comfy.generate(text, [hero], SIZE, seed, 30))
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


def sheet(picks: list[str]) -> None:
    chosen = []
    for pick in picks or list(THEMES):
        theme, _, seed = pick.partition(":")
        path = DIR / "_raw" / f"{theme}_s{seed or seed_for(theme, 0)}.png"
        chosen.append((THEMES[theme][0], Image.open(path).convert("RGB")))
    w, h = 1024, round(1024 * SIZE[1] / SIZE[0])
    cols = 2
    rows = (len(chosen) + cols - 1) // cols
    pad, label = 16, 44
    img = Image.new("RGB", (cols * (w + pad) + pad, rows * (h + label + pad) + pad), (24, 22, 28))
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arialbd.ttf", 28)
    except OSError:
        font = ImageFont.load_default()
    for n, (name, im) in enumerate(chosen):
        x = pad + (n % cols) * (w + pad)
        y = pad + (n // cols) * (h + label + pad)
        draw.text((x, y + 6), f"{n + 1}. {name}", fill=(240, 232, 214), font=font)
        img.paste(im.resize((w, h), Image.LANCZOS), (x, y + label))
    img.save(TESTS / "themes.png")
    (TESTS / "sheets").mkdir(exist_ok=True)
    img.save(TESTS / "sheets" / "themes.webp", quality=88)
    print(TESTS / "themes.png")


def main() -> None:
    args = sys.argv[1:]
    if args and args[0] == "sheet":
        sheet(args[1:])
        return
    seeds = 1
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    bare = "--bare" in args
    if bare:
        args.remove("--bare")
    generate(args or list(THEMES), seeds, bare)


if __name__ == "__main__":
    main()
