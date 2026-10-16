#!/usr/bin/env python3
"""Battle screen mocks and the comparison page for the art direction tests (scripts/art_tests.py, docs/PLANS.md Art).

  python scripts/art_compare.py mock        # art/tests/<direction>/battle_mock.png for every direction with the parts
  python scripts/art_compare.py page        # art/tests/compare/index.html + small webp copies of every image
  python scripts/art_compare.py             # both

The mock uses the game's battle layout at 2x: hero at x 660, enemy at x 1900, feet on y 1000, sizes by the game's rule
(480px tall box, 600px max width, times the monster's look scale), and the real battle HUD captured from the game
(art/tests/hud_2x.png). The hero is the rig test's assembled pose (with sword) when there is one, else the bare Warrior.
"""
import html
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import DIRECTIONS, TESTS  # noqa: E402

COMPARE = TESTS / "compare"
# The page's columns: one direction each (swap in "painterly_alt" / "ink_alt" if the other LoRA wins).
COLUMNS = ["flash", "painterly", "pixel", "ink"]
HERO_X, ENEMY_X, FEET_Y = 660, 1900, 1000
ENEMY = ("mon_boar", 1.1)  # the Thornback Boar's look scale

ROWS = [
    ("battle_mock", "Battle screen"),
    ("hero_warrior", "Warrior"),
    ("hero_mage", "Mage"),
    ("hero_rogue", "Rogue"),
    ("mon_mossling", "Cute trash: Mossling"),
    ("mon_boar", "Tough: Thornback Boar"),
    ("mon_thornheart", "Boss: Thornheart"),
    ("bg_room", "Exploration room"),
    ("icons", "Item icons"),
    ("rig", "Cut-out rig (idle + attack)"),
    ("notes", "Notes"),
]
ICONS = ["icon_sword", "icon_potion", "icon_ring", "icon_cap"]
# Under each column: where the style comes from, and a one-line opinion (the designer picks).
NOTES = {
    "flash": ("LoRA: Soyuzmultfilm (Civitai, selling images OK)",
              "Looks the most hand-made and the least AI; flat colours and clean outlines are the easiest to cut into "
              "rig parts and keep matching. Risk: simple, a bit retro. Rig fit: best."),
    "painterly": ("LoRA: Disco Elysium (Civitai, selling images OK)",
                  "Richest mood, but this LoRA paints ground shadows and broke the boss's anatomy most often (needed "
                  "the most re-rolls); swords come point-down (flipped here). Soft edges hide rig seams; its no-arms "
                  "edit kept the sleeves, so only the forearms swing. Rig fit: good."),
    "pixel": ("No LoRA: plain Qwen + a snap to a real pixel grid (4x, 64 colours)",
              "Crisp and consistent with no LoRA at all; but rotating parts breaks the pixel grid (jaggy arm in the "
              "rig), so it suits frame-by-frame animation more than cut-out. Rig fit: weakest."),
    "ink": ("LoRA: Sidney Sime (Civitai, selling images OK)",
            "Most storybook charm, steady linework across the set; the room is busy and it draws swords point-down "
            "(flipped here). Ink lines hide rig seams. Rig fit: good."),
}


def place(canvas: Image.Image, sprite: Image.Image, x: int, scale: float, flip: bool, pixel: bool) -> None:
    s = min(480 / sprite.height, 600 / sprite.width) * scale
    if pixel:  # whole screen pixels per art pixel (art pixels are 4px in the 2x files)
        s = max(1, round(4 * s)) / 4
    w, h = round(sprite.width * s), round(sprite.height * s)
    img = sprite.resize((w, h), Image.NEAREST if pixel else Image.LANCZOS)
    if flip:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((x - w * 0.35, FEET_Y - 22, x + w * 0.35, FEET_Y + 22), fill=(0, 0, 0, 90))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(img, (round(x - w / 2), FEET_Y - h))


def is_pixel(dkey: str) -> bool:
    return DIRECTIONS[dkey].get("post") == "pixel"


def mock(dkey: str) -> bool:
    d = TESTS / dkey
    hero = d / "rig_pose.png" if (d / "rig_pose.png").exists() else d / "hero_warrior.png"
    parts = [d / "bg_room.png", hero, d / f"{ENEMY[0]}.png"]
    if not all(p.exists() for p in parts):
        print(f"{dkey}: mock skipped, missing {', '.join(p.name for p in parts if not p.exists())}")
        return False
    canvas = Image.open(parts[0]).convert("RGBA").resize((2560, 1440))
    pixel = is_pixel(dkey)
    place(canvas, Image.open(hero).convert("RGBA"), HERO_X, 1.0, False, pixel)
    place(canvas, Image.open(parts[2]).convert("RGBA"), ENEMY_X, ENEMY[1], True, pixel)
    canvas.alpha_composite(Image.open(TESTS / "hud_2x.png").convert("RGBA"))
    canvas.convert("RGB").save(d / "battle_mock.png")
    print(f"{dkey}: {(d / 'battle_mock.png').relative_to(TESTS.parent.parent)}")
    return True


def small(src: Path, dst: Path, long_side: int, pixel: bool) -> None:
    img = Image.open(src)
    if getattr(img, "is_animated", False):
        dst.with_suffix(".gif").write_bytes(src.read_bytes())
        return
    img = img.convert("RGBA")
    s = min(1.0, long_side / max(img.size))
    if s < 1:
        # Pixel art shrinks by a whole factor with nearest neighbour so the grid stays even.
        if pixel:
            f = max(1, round(1 / s))
            img = img.resize((img.width // f, img.height // f), Image.NEAREST)
        else:
            img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    img.save(dst, "WEBP", quality=88, lossless=pixel)


def page() -> None:
    COMPARE.mkdir(parents=True, exist_ok=True)
    cells: dict[tuple[str, str], str] = {}
    for dkey in COLUMNS:
        d = TESTS / dkey
        for row, _ in ROWS:
            if row == "notes":
                src_line, opinion = NOTES.get(dkey, ("", ""))
                cells[(dkey, row)] = f'<p class="note"><b>{html.escape(src_line)}</b><br>{html.escape(opinion)}</p>'
                continue
            if row == "icons":
                found = [i for i in ICONS if (d / f"{i}.png").exists()]
                tags = []
                for i in found:
                    small(d / f"{i}.png", COMPARE / f"{dkey}_{i}.webp", 256, is_pixel(dkey))
                    tags.append(f'<img class="icon" src="{dkey}_{i}.webp" alt="{i}">')
                cells[(dkey, row)] = f'<div class="icons">{"".join(tags)}</div>' if tags else ""
                continue
            src = d / ("rig.gif" if row == "rig" else f"{row}.png")
            if not src.exists():
                cells[(dkey, row)] = ""
                continue
            ext = ".gif" if row == "rig" else ".webp"
            name = f"{dkey}_{row}{ext}"
            small(src, COMPARE / name, 1280 if row in ("battle_mock", "bg_room") else 640, is_pixel(dkey))
            wide = " wide" if row in ("battle_mock", "bg_room") else ""
            cells[(dkey, row)] = f'<a href="{name}" target="_blank"><img class="art{wide}" src="{name}" alt="{row}"></a>'

    head = "".join(f"<th>{html.escape(DIRECTIONS[k]['name'])}</th>" for k in COLUMNS)
    body = []
    for row, label in ROWS:
        cls = ' class="sprite"' if row.startswith(("hero_", "mon_")) or row == "icons" else ""
        tds = "".join(f'<td{cls}>{cells.get((k, row)) or "<span class=missing>not yet</span>"}</td>' for k in COLUMNS)
        body.append(f'<tr><th class="row">{html.escape(label)}</th>{tds}</tr>')
    (COMPARE / "index.html").write_text(PAGE.format(head=head, body="\n".join(body)), encoding="utf-8")
    print(f"page: {(COMPARE / 'index.html').relative_to(TESTS.parent.parent)}")


PAGE = """<title>Art Direction Tests</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya:wght@500;700&family=Alegreya+Sans:wght@400;500&display=swap">
<style>
/* Layout: a gallery table, one column per art direction, the same subject across each row; art sits on a neutral
   ground (a cool grey, so the styles' own colours read true), with the game's HUD gold for the column names. */
:root {{
  --bg:#e8eaed; --panel:#f7f8fa; --ink:#1e2027; --muted:#5d6370; --line:#d2d6dd; --gold:#856526; --check:#dfe2e7;
  --display:"Alegreya", Georgia, "Times New Roman", serif; --body:"Alegreya Sans", system-ui, sans-serif;
}}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{
  --bg:#14161b; --panel:#1d2027; --ink:#e8e6e1; --muted:#9aa0ab; --line:#2e333c; --gold:#d4af64; --check:#262a32; color-scheme:dark; }} }}
:root[data-theme="dark"] {{ --bg:#14161b; --panel:#1d2027; --ink:#e8e6e1; --muted:#9aa0ab; --line:#2e333c; --gold:#d4af64; --check:#262a32; color-scheme:dark; }}
* {{ box-sizing:border-box; }}
body {{ background:var(--bg); color:var(--ink); font:16px/1.5 var(--body);
  padding-left:max(16px, env(safe-area-inset-left, 0px)); padding-right:max(16px, env(safe-area-inset-right, 0px)); padding-block:20px 40px; }}
header {{ max-width:1800px; margin:0 auto 12px; display:grid; gap:6px; }}
h1 {{ font:700 30px/1.15 var(--display); margin:0; text-wrap:balance; }}
header p {{ margin:0; color:var(--muted); max-width:75ch; }}
.spec {{ font-size:13px; letter-spacing:.04em; text-transform:uppercase; color:var(--gold); }}
.wrap {{ overflow-x:auto; max-width:1800px; margin:0 auto; }}
table {{ border-collapse:separate; border-spacing:8px; min-width:1100px; width:100%; }}
thead th {{ font:700 19px var(--display); color:var(--gold); padding:4px 8px; text-align:center; }}
th.row {{ text-align:right; color:var(--muted); font:500 15px var(--body); width:130px; vertical-align:middle; }}
td {{ background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:8px; text-align:center; vertical-align:middle; width:25%; }}
td a:focus-visible {{ outline:2px solid var(--gold); outline-offset:2px; }}
td img.art {{ max-width:100%; max-height:360px; }}
td img.wide {{ max-height:none; width:100%; border-radius:4px; }}
td.sprite {{ background-image:conic-gradient(var(--check) 25%, transparent 0 50%, var(--check) 0 75%, transparent 0); background-size:24px 24px; }}
.icons {{ display:grid; grid-template-columns:repeat(2, 1fr); gap:6px; }}
.icons img {{ width:100%; aspect-ratio:1; object-fit:contain; max-width:100%; }}
.missing {{ color:var(--muted); font-style:italic; }}
.note {{ text-align:left; margin:0; font-size:15px; }}
.note b {{ font-weight:500; color:var(--gold); display:block; margin-bottom:4px; }}
</style>
<header>
<span class="spec">2x masters · rooms 2560×1440 · sprites 960 px tall · Qwen-Image 2.1 int8 · 30 steps</span>
<h1>Art direction tests</h1>
<p>The same subjects in four styles, each generated on the local Qwen with its own style LoRA. Click an image for a larger copy.
The rig GIFs are a quick cut-out test: the hero plus a no-arms edit, with parts rotated in code. The gap at the shoulder is what a real part sheet would cover with a drawn shoulder cap.</p>
</header>
<div class="wrap"><table><thead><tr><th></th>{head}</tr></thead><tbody>
{body}
</tbody></table></div>
"""

if __name__ == "__main__":
    what = sys.argv[1:] or ["mock", "page"]
    if "mock" in what:
        for k in COLUMNS:
            mock(k)
    if "page" in what:
        page()
