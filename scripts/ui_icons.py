"""Hand-drawn ink icons for the UI (never AI-generated: docs/PLANS.md, Art). Each icon is a small SVG in the
round-15 mock's style (art/ui/battle_craft.html: 32x32 viewBox, ink #2a1d16 outlines 2.4 wide, flat fills from the
paper palette, a slight wobble filter). Rendered with headless Edge to transparent PNGs for Unity:
unity/Assets/Resources/UiIcons/<name>.png (128 px).

    python scripts/ui_icons.py            # render all
    python scripts/ui_icons.py sword book # render some
"""
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "unity" / "Assets" / "Resources" / "UiIcons"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SIZE = 128

INK = "#2a1d16"
CREAM = "#efe2c2"
G = f'stroke="{INK}" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"'

# name -> SVG body (inside a 32x32 viewBox). The first seven are the mock's own.
ICONS = {
    # Attack: a sword (mock).
    "sword": f'<g {G}><path d="M24 3 L29 3 L29 8 L13 24 L8 19 Z" fill="{CREAM}"/><path d="M6 17 L15 26" fill="none"/>'
             f'<path d="M9 23 L4 28" fill="none" stroke-width="3.2"/></g>',
    # Skills: an open book (mock).
    "book": f'<g {G}><path d="M16 8 C12 5 7 5 3 6 L3 26 C7 25 12 25 16 28 Z" fill="#d9c59c"/>'
            f'<path d="M16 8 C20 5 25 5 29 6 L29 26 C25 25 20 25 16 28 Z" fill="{CREAM}"/></g>',
    # Items: a pouch (mock).
    "bag": f'<g {G}><path d="M8 12 C6 18 6 26 10 28 L22 28 C26 26 26 18 24 12 Z" fill="#c09060"/>'
           f'<path d="M8 12 L24 12 L21 7 L11 7 Z" fill="#a8764a"/><path d="M12 12 L16 16 L20 12" fill="none"/></g>',
    # Defend: a shield (mock).
    "shield": f'<path d="M16 3 L27 7 V15 C27 22 22 27 16 29 C10 27 5 22 5 15 V7 Z" fill="#2f7f78" {G}/>',
    # Flee: a boot with speed lines (mock).
    "boot": f'<g {G}><path d="M10 4 L18 4 L18 18 L27 22 L27 28 L8 28 Z" fill="#8a6440"/>'
            f'<path d="M3 12 L8 12 M2 17 L7 17 M4 22 L8 22" fill="none"/></g>',
    # "Next:" the monster's move: a claw (mock).
    "claw": f'<path d="M5 26 C6 15 13 7 26 5 C21 12 20 19 22 26 Z" fill="{CREAM}" {G}/>',
    # "Weak:" a flame (mock).
    "flame": f'<path d="M16 3 C21 10 26 13 26 20 C26 26 21 29 16 29 C11 29 6 26 6 20 C6 16 9 13 11 10 C12 14 14 15 16 15 '
             f'C15 11 15 7 16 3 Z" fill="#e0a93a" {G}/>',
    # Pierce attacks: an arrow.
    "arrow": f'<g {G}><path d="M7 25 L24 8" fill="none" stroke-width="2.8"/><path d="M19 5 L28 4 L27 13 Z" fill="{CREAM}"/>'
             f'<path d="M7 25 L4 20 M7 25 L12 28 M10 22 L7 17 M10 22 L15 25" fill="none"/></g>',
    # Magic attacks: a four-point spark.
    "spark": f'<path d="M16 3 L19 13 L29 16 L19 19 L16 29 L13 19 L3 16 L13 13 Z" fill="#7fa8d8" {G}/>',
    # Heals: a cross.
    "cross": f'<path d="M12 4 H20 V12 H28 V20 H20 V28 H12 V20 H4 V12 H12 Z" fill="#6aa84f" {G}/>',
    # Buffs: a chevron going up.
    "rise": f'<path d="M5 19 L16 8 L27 19 L22 24 L16 18 L10 24 Z" fill="#e0a93a" {G}/>',
    # Swap weapons: two arrows.
    "swap": f'<g {G} fill="none"><path d="M5 11 H23 M19 6 L24 11 L19 16"/><path d="M27 21 H9 M13 16 L8 21 L13 26"/></g>',
    # Back: an arrow pointing left.
    "back": f'<g {G} fill="none" stroke-width="2.8"><path d="M26 16 H8 M14 9 L7 16 L14 23"/></g>',
    # More (next page): three dots.
    "more": f'<g fill="{INK}"><circle cx="7" cy="16" r="3"/><circle cx="16" cy="16" r="3"/><circle cx="25" cy="16" r="3"/></g>',
    # Items in battle: a potion bottle.
    "potion": f'<g {G}><path d="M13 4 H19 V10 C25 12 27 17 26 22 C25 27 21 29 16 29 C11 29 7 27 6 22 C5 17 7 12 13 10 Z" '
              f'fill="#c8452f"/><path d="M12 4 H20" fill="none" stroke-width="3.2"/><path d="M10 19 C13 21 19 21 22 19" '
              f'fill="none" stroke="{CREAM}" stroke-width="2"/></g>',
    # A monster's trait (signature move): a little star.
    "star": f'<path d="M16 3 L20 12 L29 13 L22 19 L24 28 L16 23 L8 28 L10 19 L3 13 L12 12 Z" fill="{CREAM}" {G}/>',
}

PAGE = """<!doctype html><html><head><meta charset="utf-8"><style>
html, body {{ margin: 0; padding: 0; background: transparent; }}
svg {{ display: block; }}
</style></head><body>
<svg width="0" height="0" style="position:absolute"><defs>
<filter id="wobble"><feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="5"/>
<feDisplacementMap in="SourceGraphic" scale="2.4"/></filter></defs></svg>
<svg width="{size}" height="{size}" viewBox="-2 -2 36 36"><g filter="url(#wobble)">{body}</g></svg>
</body></html>"""


def render(name: str, body: str, tmp: Path) -> None:
    html = tmp / f"{name}.html"
    html.write_text(PAGE.format(size=SIZE, body=body), encoding="utf-8")
    shot = tmp / f"{name}.png"
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                    f"--window-size={SIZE},{SIZE}", f"--screenshot={shot}", html.as_uri()],
                   check=True, capture_output=True, timeout=60)
    img = Image.open(shot).convert("RGBA").crop((0, 0, SIZE, SIZE))
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / f"{name}.png")
    print(f"  {name}.png")


def main() -> None:
    names = sys.argv[1:] or list(ICONS)
    with tempfile.TemporaryDirectory() as t:
        for n in names:
            render(n, ICONS[n], Path(t))


if __name__ == "__main__":
    main()
