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
]
ICONS = ["icon_sword", "icon_potion", "icon_ring", "icon_cap"]


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
        tds = "".join(f'<td>{cells.get((k, row)) or "<span class=missing>not yet</span>"}</td>' for k in COLUMNS)
        body.append(f'<tr><th class="row">{html.escape(label)}</th>{tds}</tr>')
    (COMPARE / "index.html").write_text(PAGE.format(head=head, body="\n".join(body)), encoding="utf-8")
    print(f"page: {(COMPARE / 'index.html').relative_to(TESTS.parent.parent)}")


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Art Direction Tests</title>
<style>
:root {{ --bg:#f4f1ea; --panel:#fffdf8; --ink:#2a2622; --muted:#7a7268; --line:#e2dccf; --check:#e9e4d8; }}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{ --bg:#16181c; --panel:#1f2227; --ink:#ece8e1; --muted:#9a948a; --line:#30343b; --check:#2a2e35; }} }}
:root[data-theme="dark"] {{ --bg:#16181c; --panel:#1f2227; --ink:#ece8e1; --muted:#9a948a; --line:#30343b; --check:#2a2e35; }}
* {{ box-sizing:border-box; }}
body {{ margin:0; background:var(--bg); color:var(--ink); font:15px/1.45 system-ui, sans-serif; }}
header {{ padding:20px 16px 8px; max-width:1800px; margin:auto; }}
h1 {{ font-size:22px; margin:0 0 4px; }}
header p {{ margin:0; color:var(--muted); }}
.wrap {{ overflow-x:auto; padding:8px 16px 32px; }}
table {{ border-collapse:separate; border-spacing:8px; margin:auto; min-width:1100px; }}
thead th {{ position:sticky; top:0; background:var(--bg); font-size:17px; padding:8px; z-index:1; }}
th.row {{ text-align:right; color:var(--muted); font-weight:600; width:120px; vertical-align:middle; }}
td {{ background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:8px; text-align:center; vertical-align:middle; width:25%; }}
td img.art {{ max-width:100%; max-height:360px; image-rendering:auto; }}
td img.wide {{ max-height:none; width:100%; border-radius:6px; }}
.icons {{ display:grid; grid-template-columns:repeat(2, 1fr); gap:6px; }}
.icons img {{ width:100%; aspect-ratio:1; object-fit:contain; }}
td:has(img:not(.wide)) {{ background-image:conic-gradient(var(--check) 25%, transparent 0 50%, var(--check) 0 75%, transparent 0); background-size:24px 24px; }}
.missing {{ color:var(--muted); font-style:italic; }}
</style></head><body>
<header><h1>Art direction tests</h1>
<p>Same subjects, four styles, generated on the local Qwen-Image 2.1 with a style LoRA each (2x size). Click an image for the larger copy.</p></header>
<div class="wrap"><table><thead><tr><th></th>{head}</tr></thead><tbody>
{body}
</tbody></table></div></body></html>
"""

if __name__ == "__main__":
    what = sys.argv[1:] or ["mock", "page"]
    if "mock" in what:
        for k in COLUMNS:
            mock(k)
    if "page" in what:
        page()
