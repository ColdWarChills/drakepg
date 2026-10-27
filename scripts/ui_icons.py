"""Hand-drawn ink icons for the UI (never AI-generated: docs/PLANS.md, Art). Each icon is a small SVG in the
round-15 mock's style (art/ui/battle_craft.html: 32x32 viewBox, ink #2a1d16 outlines 2.4 wide, flat fills from the
paper palette, a slight wobble filter). Rendered with headless Edge to transparent PNGs for Unity:
unity/Assets/Resources/UiIcons/<name>.png (128 px). `--light`: the same drawings in cream ink with navy fills for
dark felt (the felt mock's icons), to UiIconsLight/.

    python scripts/ui_icons.py            # render all
    python scripts/ui_icons.py sword book # render some
    python scripts/ui_icons.py --light    # the set for dark felt
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
# The felt mock's icon colors on navy felt: cream outlines, navy where the ink set has cream.
LIGHT_INK = "#f1e6cc"
LIGHT_FILL = "#3a4a66"
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

# --- Skills and statuses (`skill_<id>`, `status_<id>`): one drawing each, filled in its element's or status's color ---
FIRE, FLAME = "#e0703a", "#f2b33d"
WATER, ICE, WIND, EARTH = "#4f8fcf", "#a9dcef", "#9fcf8f", "#a97c4f"
ENERGY, LIGHT, DARK = "#f2cf3d", "#f6e27a", "#6b5a92"
STEEL, BLOOD, VENOM, HEAL = "#b9c2cc", "#c8352f", "#7cc45a", "#6aa84f"
STONE, SMOKE = "#9aa6b8", "#c9c4bc"


def _star(cx: float, cy: float, r: float, inner: float, points: int = 4, turn: float = 0) -> str:
    """A pointed star path (a crit flash, a burst)."""
    import math
    pts = []
    for i in range(points * 2):
        a = math.pi * i / points + turn - math.pi / 2
        rr = r if i % 2 == 0 else inner
        pts.append(f"{cx + rr * math.cos(a):.1f} {cy + rr * math.sin(a):.1f}")
    return "M" + " L".join(pts) + " Z"


def _fan() -> str:
    """Fan of Knives: five knives spread from one point."""
    import math
    out = []
    for deg in (-56, -28, 0, 28, 56):
        a = math.radians(deg - 90)
        ux, uy = math.cos(a), math.sin(a)
        px, py = -uy, ux
        ox, oy = 16 + ux * 7, 30 + uy * 7
        tip = (16 + ux * 27, 30 + uy * 27)
        base = (ox, oy)
        w = 2.2
        out.append(f'<path d="M{base[0] + px * w:.1f} {base[1] + py * w:.1f} L{tip[0]:.1f} {tip[1]:.1f} '
                   f'L{base[0] - px * w:.1f} {base[1] - py * w:.1f} Z" fill="{STEEL}"/>')
    return f'<g {G}>{"".join(out)}<circle cx="16" cy="29" r="3" fill="{INK}"/></g>'


def _cuts() -> str:
    """Thousand Cuts: slashes crossing every way around a flash."""
    import math
    lines = []
    for i, deg in enumerate((10, 40, 75, 110, 140, 170)):
        a = math.radians(deg)
        r = 13 if i % 2 == 0 else 10
        dx, dy = math.cos(a) * r, math.sin(a) * r
        lines.append(f"M{16 - dx:.1f} {16 - dy:.1f} L{16 + dx:.1f} {16 + dy:.1f}")
    return (f'<path d="{_star(16, 16, 7, 2.6, 4, 0.4)}" fill="{LIGHT}" {G}/>'
            f'<path d="{" ".join(lines)}" fill="none" {G} stroke-width="2"/>')


DROP = "M16 3 C21 11 25 16 25 21 C25 26 21 29 16 29 C11 29 7 26 7 21 C7 16 11 11 16 3 Z"
SHIELD = "M16 3 L27 7 V15 C27 22 22 27 16 29 C10 27 5 22 5 15 V7 Z"
HEART = "M16 28 C6 21 3 15 3 10 C3 6 6 4 9.5 4 C12.5 4 14.5 6 16 9 C17.5 6 19.5 4 22.5 4 C26 4 29 6 29 10 C29 15 26 21 16 28 Z"
PLUS = "M{x} {y} h4 v4 h4 v4 h-4 v4 h-4 v-4 h-4 v-4 h4 Z"
FLAME_PATH = ("M16 3 C21 10 26 13 26 20 C26 26 21 29 16 29 C11 29 6 26 6 20 C6 16 9 13 11 10 C12 14 14 15 16 15 "
              "C15 11 15 7 16 3 Z")
CRACKED = ('<path d="M5 7 L14 3.5 L12 10 L15 15 L12 22 L14 28.5 C8.5 26.5 5 21 5 15 Z" fill="{c}"/>'
           '<path d="M17.5 3.5 L27 7 V15 C27 21 23.5 26.5 18 28.5 L16 22 L19 15 L16 10 Z" fill="{c}"/>')
HORN = (f'<path d="M3 26 C11 26 17 21 20 11 L28.5 14.5 C25 25 16 30.5 4.5 30 Z" fill="{CREAM}"/>'
        f'<path d="M9.5 26.5 L10.5 29.6 M15 24.5 L17 28" fill="none"/>'
        f'<path d="M20 6 C22 4 25 4 27 5 M24 1.5 C27 0.5 30 1.5 31.5 3.5 M27.5 9 C29 8.5 30.5 9 31.5 10" fill="none" stroke-width="2"/>')
PLATE = (f'<path d="M8 5 L13 7 C14 9 18 9 19 7 L24 5 L28 9 L25 13 V24 C22 27 18 28 16 28 C14 28 10 27 7 24 V13 L4 9 Z" '
         f'fill="{STEEL}"/><path d="M16 11 V26" fill="none"/>'
         f'<circle cx="10.5" cy="15" r="1" fill="{INK}" stroke="none"/><circle cx="21.5" cy="15" r="1" fill="{INK}" stroke="none"/>')
STONES = (f'<circle cx="16" cy="16" r="12.5" fill="{EARTH}"/>'
          f'<path d="M9 8 L15 12 L22 7 M15 12 L16 20 M5 18 L16 20 L27 17 M16 20 L13 28 M16 20 L21 27" fill="none" stroke-width="1.8"/>')
RETICLE = (f'<circle cx="16" cy="16" r="10" fill="none"/><path d="M16 2 V9 M16 23 V30 M2 16 H9 M23 16 H30" fill="none"/>'
           f'<circle cx="16" cy="16" r="3.4" fill="{{c}}"/>')
EYE = (f'<path d="M3 16 C8 8 24 8 29 16 C24 24 8 24 3 16 Z" fill="{CREAM}"/>'
       f'<circle cx="16" cy="16" r="4.5" fill="{{c}}"/><circle cx="16" cy="16" r="1.6" fill="{INK}" stroke="none"/>')
SNOWFLAKE = ('<path d="M16 3 V29 M4.7 9.5 L27.3 22.5 M4.7 22.5 L27.3 9.5 M12.5 4.5 L16 8 L19.5 4.5 M12.5 27.5 L16 24 '
             'L19.5 27.5 M4 13.5 L8.5 12 L7.5 7.5 M28 18.5 L23.5 20 L24.5 24.5 M4 18.5 L8.5 20 L7.5 24.5 M28 13.5 '
             'L23.5 12 L24.5 7.5" fill="none"/>')
DAGGER_A = (f'<path d="M9 21 L23 5 L25.5 6.5 L11 23 Z" fill="{STEEL}"/><path d="M6.5 19.5 L13 25.5" fill="none" stroke-width="2.8"/>'
            f'<path d="M8.5 24 L4 28.5" fill="none" stroke-width="3.4"/>')
DAGGER_B = (f'<path d="M23 21 L9 5 L6.5 6.5 L21 23 Z" fill="{STEEL}"/><path d="M25.5 19.5 L19 25.5" fill="none" stroke-width="2.8"/>'
            f'<path d="M23.5 24 L28 28.5" fill="none" stroke-width="3.4"/>')

SKILLS = {
    # Warrior
    "heavy_strike": f'<g {G}><path d="M5 28.5 L17 16.5" fill="none" stroke-width="3.4"/>'
                    f'<path d="M16.8 3.8 L28.2 15.2 L23.2 20.2 L11.8 8.8 Z" fill="{STEEL}"/>'
                    f'<path d="M26 23 L29.5 26.5 M22 26.5 L23.5 30.5 M29 19.5 L31.5 19.5" fill="none" stroke-width="2"/></g>',
    "second_wind": f'<g {G}><path d="M3 13 H13 C17 13 18 8 15 7 C12.5 6 11 8.5 12.5 10" fill="none"/>'
                   f'<path d="M3 19 H20 C25 19 26 25 22.5 26.5 C20 27.5 18 25 19.5 23" fill="none"/>'
                   f'<path d="{PLUS.format(x=21, y=2)}" fill="{HEAL}"/></g>',
    "war_cry": f'<g {G}>{HORN}</g>',
    "skull_splitter": f'<g {G}><path d="M6 30 L21 6" fill="none" stroke-width="3.2"/>'
                      f'<path d="M15 9.5 C15 4 19 1.5 23 2 C25 6 28 8.5 31 9 C29 13 25 15.5 20.5 14.5 Z" fill="{STEEL}"/>'
                      f'<path d="M23.5 4.5 C25 7.5 27 9.5 29.5 10.5" fill="none" stroke="{CREAM}" stroke-width="1.6"/>'
                      f'<path d="M3 5 L6 10 L4 13.5 L7 18" fill="none" stroke-width="2"/></g>',
    "shield_bash": f'<g {G}><path d="M3 7 L12 4 L21 7 V14 C21 20 17 24 12 26.5 C7 24 3 20 3 14 Z" fill="#2f7f78"/>'
                   f'<path d="M24 9 L30 5 M25 15 H31 M24 21 L30 25" fill="none"/></g>',
    "sunder": f'<g {G}>{CRACKED.format(c=STEEL)}</g>',
    "iron_skin": f'<g {G}>{PLATE}</g>',
    "cleave": f'<g {G}><path d="M3 24 C6 11 18 3 30 6 C19 8 11 14 8 27 Z" fill="{CREAM}"/>'
              f'<path d="M12 29 C14 21 21 15 29 14 C22 18 18 23 16 30 Z" fill="{CREAM}"/></g>',
    "bloodlust": f'<g {G}><path d="M5 23 L18 5 L22.5 3.5 L21.5 8 L8.5 26 Z" fill="{STEEL}"/>'
                 f'<path d="M3.5 20.5 L11 28" fill="none" stroke-width="2.8"/><path d="M6.5 26.5 L3 30" fill="none" stroke-width="3.2"/>'
                 f'<path d="M24 14 C27 18.5 28.5 21 28.5 23.5 C28.5 26.5 26.5 28.5 24 28.5 C21.5 28.5 19.5 26.5 19.5 23.5 '
                 f'C19.5 21 21 18.5 24 14 Z" fill="{BLOOD}"/></g>',
    "earthshaker": f'<g {G}><path d="M2 26 H11 L13.5 22.5 L16 28 L18.5 23.5 L20.5 26 H30" fill="none"/>'
                   f'<path d="M3.5 14 L9 10 L14 13.5 L12 19.5 L5.5 19.5 Z" fill="{EARTH}"/>'
                   f'<path d="M17 6.5 L24 3.5 L29.5 8.5 L26.5 15 L19 14.5 Z" fill="{EARTH}"/>'
                   f'<path d="M9 21.5 V23.5 M23 17 V20" fill="none" stroke-width="2"/></g>',
    "rallying_roar": f'<g {G}><path d="M8 3 V30" fill="none" stroke-width="3"/>'
                     f'<path d="M8 4.5 H27 L22.5 11 L27 17.5 H8 Z" fill="#c8452f"/>'
                     f'<path d="M14 7.5 h3 v3 h3 v3 h-3 v3 h-3 v-3 h-3 v-3 h3 Z" fill="{CREAM}" stroke-width="1.6"/></g>',
    "titans_wrath": f'<g {G}><path d="M16 1.5 V8.5" fill="none" stroke-width="3.4"/><path d="M8.5 9 H23.5" fill="none" stroke-width="3"/>'
                    f'<path d="M13 9.5 H19 V21 L16 26 L13 21 Z" fill="{STEEL}"/><path d="M2 28 H30" fill="none"/>'
                    f'<path d="M7 24.5 L3.5 20.5 M25 24.5 L28.5 20.5 M10 27 L5 25.5 M22 27 L27 25.5" fill="none" stroke-width="2"/></g>',
    # Mage
    "light_bolt": f'<g {G}><path d="{_star(21, 12, 10, 3.2)}" fill="{LIGHT}"/>'
                  f'<path d="M2 22 L11 17 M4 28 L13 21 M9 31 L15 24.5" fill="none" stroke-width="2.2"/></g>',
    "ember_bolt": f'<g {G}><circle cx="21" cy="11" r="7.5" fill="#7fa8d8"/><path d="M17.5 8.5 C18.5 6.5 20.5 5.8 22 6" '
                  f'fill="none" stroke="{CREAM}" stroke-width="1.8"/>'
                  f'<path d="M2 25 L14 17 M6 30 L16 21 M2 19 L11.5 14" fill="none" stroke-width="2.2"/></g>',
    "mend": f'<g {G}><path d="{HEART}" fill="#de6f6a"/><path d="M14 10 h4 v3.5 h3.5 v4 H18 V21 h-4 v-3.5 h-3.5 v-4 H14 Z" '
            f'fill="{CREAM}" stroke-width="1.8"/></g>',
    "frost_lance": f'<g {G}><path d="M3 24 L8 29 L30 2 Z" fill="{ICE}"/><path d="M8 26 L23 10" fill="none" stroke-width="1.6"/>'
                   f'<path d="M4 9 L7 12 M10 4 L11 8 M23 25 L26 27" fill="none" stroke-width="2"/></g>',
    "meteor": f'<g {G}><path d="M2 2 C8 6 10 8 14 10 C11 5 9 3 8 2 C13 4 17 7 21 11 C20 7 19 5 18 3 C23 7 26 11 26 16 L17 22 '
              f'C11 18 6 11 2 2 Z" fill="{FIRE}"/><path d="M11 9 C15 11 18 13 21 16 L19 19 C15 16 13 13 11 9 Z" fill="{FLAME}" '
              f'stroke="none"/><circle cx="21" cy="21.5" r="8" fill="#7a5a44"/>'
              f'<path d="M17 19 L20 21 M23.5 24 L25.5 26 M22 16.5 L23.5 17.5" fill="none" stroke-width="1.8"/></g>',
    "spark_chain": f'<g {G}><path d="M6 8 L9.5 16 L12.5 14 L16 24 M16 24 L20 15 L23 17 L27 9" fill="none" stroke-width="2.2"/>'
                   f'<circle cx="6" cy="8" r="3.4" fill="{ENERGY}"/><circle cx="16" cy="24" r="3.4" fill="{ENERGY}"/>'
                   f'<circle cx="27" cy="9" r="3.4" fill="{ENERGY}"/></g>',
    "stone_skin": f'<g {G}>{STONES}</g>',
    "gale": f'<g {G}><path d="M3 10 H18 C22 10 23 5 20 4 C17.5 3 16 5.5 17.5 7.5" fill="none"/>'
            f'<path d="M3 17 H24 C29 17 30 23 26.5 24 C24 24.8 22 22.5 23.5 20.5" fill="none"/>'
            f'<path d="M6 24 H15 C19 24 19.5 29 16.5 29.5" fill="none"/>'
            f'<path d="M23 9 C26 5.5 30 5 31 5.5 C30.5 9 27.5 11.5 23.5 11 Z" fill="{WIND}" stroke-width="1.8"/></g>',
    "tidal_wave": f'<g {G}><path d="M2 27 C4 15 12 6 23 6 C28.5 6 31 10 29 13.5 C27.5 16 24 15.5 23.5 13 C23 11 25 10 26 11 '
                  f'C21 10 15 14 15 21 C15 24.5 17.5 26.5 21 27 Z" fill="{WATER}"/><path d="M2 30 H30" fill="none"/></g>',
    "sunburst": f'<g {G}><path d="M16 2 V7 M16 25 V30 M2 16 H7 M25 16 H30 M6 6 L9.5 9.5 M22.5 22.5 L26 26 M26 6 L22.5 9.5 '
                f'M9.5 22.5 L6 26" fill="none" stroke-width="2.6"/><circle cx="16" cy="16" r="6.5" fill="{LIGHT}"/></g>',
    "shadow_hex": f'<g {G}><circle cx="16" cy="16" r="13" fill="{DARK}"/>{EYE.format(c=DARK)}</g>',
    "thunderstorm": f'<g {G}><path d="M7 15 C2.5 15 2.5 9 7 9 C7 4 14 3 16 7 C18 3 26 4 26 9 C30.5 9 30.5 15 26 15 Z" fill="{STONE}"/>'
                    f'<path d="M10 17 L7.5 23 H11 L9 30 L15 21 H11.5 L13.5 17 Z" fill="{ENERGY}"/>'
                    f'<path d="M21 17 L18.5 23 H22 L20 30 L26 21 H22.5 L24.5 17 Z" fill="{ENERGY}"/></g>',
    "cataclysm": f'<g {G}><path d="{_star(16, 16, 15, 7, 8)}" fill="#d9603a"/>'
                 f'<path d="{_star(16, 16, 8, 3.5, 8, 0.2)}" fill="{FLAME}" stroke-width="1.6"/></g>',
    # Rogue
    "gas_shot": f'<g {G}><path d="M8 13 V8 H13 V13 C17 15 18.5 20.5 15.5 24.5 C13 28 7.5 28 5.5 24.5 C3 20.5 4 15 8 13 Z" fill="{VENOM}"/>'
                f'<path d="M7 7.5 H14" fill="none" stroke-width="3"/>'
                f'<circle cx="21" cy="11" r="4" fill="#bfe3a4"/><circle cx="26.5" cy="16" r="3.4" fill="#bfe3a4"/>'
                f'<circle cx="26" cy="7" r="2.8" fill="#bfe3a4"/></g>',
    "twin_cut": f'<g {G}>{DAGGER_A}{DAGGER_B}</g>',
    "bandage": f'<g {G}><path d="M13 17 L28 25 L25.5 30 L10.5 22 Z" fill="{CREAM}"/><circle cx="11" cy="13" r="8" fill="{CREAM}"/>'
               f'<circle cx="11" cy="13" r="3" fill="#d9c59c"/><path d="M18 20.5 L23 23.3" fill="none" stroke="{BLOOD}" stroke-width="2"/>'
               f'<path d="{PLUS.format(x=23, y=1)}" fill="{HEAL}" stroke-width="1.8"/></g>',
    "flurry": f'<g {G} fill="none" stroke-width="2.6"><path d="M4 21 C9 13 14 8 21 4"/><path d="M8 26 C13 18 18 13 25 9"/>'
              f'<path d="M12 31 C17 23 22 18 29 14"/></g>',
    "envenom": f'<g {G}>{DAGGER_A}<path d="M25 13 C27 16 28 17.5 28 19 C28 20.7 26.7 22 25 22 C23.3 22 22 20.7 22 19 '
               f'C22 17.5 23 16 25 13 Z" fill="{VENOM}"/><path d="M19 22 C20.5 24.5 21 25.5 21 26.5 C21 27.8 20 28.8 18.8 28.8 '
               f'C17.6 28.8 16.6 27.8 16.6 26.5 C16.6 25.5 17.3 24.5 19 22 Z" fill="{VENOM}"/></g>',
    "assassinate": f'<g {G}><path d="M16 1.5 V8" fill="none" stroke-width="3.4"/><path d="M10.5 9 H21.5" fill="none" stroke-width="2.8"/>'
                   f'<path d="M13 10 H19 L16 24 Z" fill="{STEEL}"/><path d="{_star(16, 26.5, 5.5, 1.8)}" fill="#e8c34a" stroke-width="1.6"/></g>',
    "smoke_bomb": f'<g {G}><circle cx="13" cy="20" r="9" fill="#4a4a58"/><path d="M9 16 C10 14.5 11.5 13.5 13 13.5" fill="none" '
                  f'stroke="{CREAM}" stroke-width="1.8"/><path d="M19 13 C20.5 10 22.5 9 25 9.5" fill="none"/>'
                  f'<circle cx="26" cy="7" r="3.4" fill="{SMOKE}"/><circle cx="29.5" cy="3.5" r="2.2" fill="{SMOKE}"/></g>',
    "expose_weakness": f'<g {G}>{EYE.format(c="#ff9a4d")}<path d="M16 1.5 V6 M16 26 V30.5 M1.5 16 H3 M29 16 H30.5" fill="none"/></g>',
    "shadowstep": f'<g {G}><path d="M8 15 C5 15 3.5 18.5 4.5 22 C5 24.5 10 24.5 10.5 22 C11 18.5 10.5 15 8 15 Z" fill="{DARK}"/>'
                  f'<ellipse cx="8" cy="27.5" rx="2.6" ry="2.2" fill="{DARK}"/>'
                  f'<path d="M21 3 C18 3 16.5 6.5 17.5 10 C18 12.5 23 12.5 23.5 10 C24 6.5 23.5 3 21 3 Z" fill="{DARK}"/>'
                  f'<ellipse cx="21" cy="15.5" rx="2.6" ry="2.2" fill="{DARK}"/>'
                  f'<path d="M14 26 C19 25 23 23 26 19 M26 27 C28 26 30 24 31 21" fill="none" stroke-width="2"/></g>',
    "fan_of_knives": _fan(),
    "hemorrhage": f'<g {G}><path d="{DROP}" fill="{BLOOD}"/><path d="M5 27 L27 6" fill="none" stroke-width="4.5"/>'
                  f'<path d="M5 27 L27 6" fill="none" stroke="{CREAM}" stroke-width="1.8"/></g>',
    "death_mark": f'<g {G}>{RETICLE.format(c="#ff4d4d")}</g>',
    "thousand_cuts": _cuts(),
}

STATUSES = {
    "burn": f'<g {G}><path d="{FLAME_PATH}" fill="{FIRE}"/><path d="M16 17 C19 20 20 22 20 24 C20 26.5 18 28 16 28 '
            f'C14 28 12 26.5 12 24 C12 22 13 20 16 17 Z" fill="{FLAME}" stroke-width="1.6"/></g>',
    "poison": f'<g {G}><path d="{DROP}" fill="{VENOM}"/><circle cx="13" cy="21" r="2.4" fill="#bfe3a4" stroke-width="1.4"/>'
              f'<circle cx="19" cy="24" r="1.6" fill="#bfe3a4" stroke-width="1.2"/></g>',
    "bleed": f'<g {G}><path d="{DROP}" fill="{BLOOD}"/><path d="M11.5 20 C11.5 17.5 12.5 15.5 14 13.5" fill="none" '
             f'stroke="{CREAM}" stroke-width="1.8"/></g>',
    "spores": f'<g {G}><path d="M4 17 C4 8 28 8 28 17 Z" fill="#b07a4a"/><path d="M12 17 H20 V26 C20 28.5 12 28.5 12 26 Z" fill="{CREAM}"/>'
              f'<circle cx="11" cy="13" r="1.4" fill="{CREAM}" stroke="none"/><circle cx="18" cy="11.5" r="1.4" fill="{CREAM}" stroke="none"/>'
              f'<circle cx="7" cy="5" r="1.6" fill="#c9b26a"/><circle cx="16" cy="3" r="1.6" fill="#c9b26a"/>'
              f'<circle cx="25" cy="5.5" r="1.6" fill="#c9b26a"/></g>',
    "windchill": f'<g {G}><g stroke="{ICE}" transform="translate(9 1) scale(0.66)">{SNOWFLAKE}</g>'
                 f'<g transform="translate(9 1) scale(0.66)">{SNOWFLAKE}</g>'
                 f'<path d="M2 22 H17 C21 22 22 27 19 28 M2 28 H11" fill="none"/></g>',
    "shadow_rot": f'<g {G}><path d="M5 12 C5 4.5 27 4.5 27 12 V18 C27 21 24 21 24 18 C24 24 19 25 19 20 C19 27 14 28 14 21 '
                  f'C14 24 10 24 10 19 C10 22 5 22 5 18 Z" fill="{DARK}"/><circle cx="12" cy="12" r="2" fill="{CREAM}"/>'
                  f'<circle cx="20" cy="12" r="2" fill="{CREAM}"/></g>',
    "stun": f'<g {G}><ellipse cx="16" cy="17" rx="13" ry="6" fill="none" stroke-width="2"/>'
            f'<path d="{_star(6, 15, 5, 2, 5)}" fill="#ffe066" stroke-width="1.6"/><path d="{_star(19, 23, 5, 2, 5)}" '
            f'fill="#ffe066" stroke-width="1.6"/><path d="{_star(25, 10, 4.5, 1.8, 5)}" fill="#ffe066" stroke-width="1.6"/></g>',
    "chill": f'<g {G} stroke-width="2.6"><g stroke="{ICE}" stroke-width="5">{SNOWFLAKE}</g>{SNOWFLAKE}</g>',
    "blind": f'<g {G}>{EYE.format(c="#8a7aa8")}<path d="M5 28 L27 4" fill="none" stroke-width="3"/></g>',
    "shock": f'<path d="M19 2 L6 18 H14 L11 30 L26 12 H18 L22 2 Z" fill="#ffe14d" {G}/>',
    "weaken": f'<path d="M5 12 L16 23 L27 12 L22 7 L16 13 L10 7 Z" fill="#b08cff" {G}/>',
    "expose": f'<g {G}>{CRACKED.format(c="#ff9a4d")}</g>',
    "marked": f'<g {G}>{RETICLE.format(c="#ff4d4d")}</g>',
    "war_cry": SKILLS["war_cry"],
    "iron_skin": SKILLS["iron_skin"],
    "stone_skin": SKILLS["stone_skin"],
    "regen": f'<g {G}><path d="M5 27 C5 16 12 8 25 6 C25 18 18 26 5 27 Z" fill="{HEAL}"/><path d="M5 27 C10 21 15 17 20 12" '
             f'fill="none" stroke-width="1.8"/><path d="{PLUS.format(x=19, y=16)}" fill="#7dff9a" stroke-width="1.6"/></g>',
    "focus": f'<g {G}><path d="{_star(16, 16, 14, 4.5)}" fill="#ffe08a"/><circle cx="16" cy="16" r="2.6" fill="{INK}" stroke="none"/></g>',
    "evasive": f'<g {G}><path d="M7 28 C7 16 13 6 27 3 C27 14 20 23 7 28 Z" fill="#b8f5c8"/>'
               f'<path d="M4 31 L22 10 M14 19 L19 22 M18 14 L23 16" fill="none" stroke-width="1.8"/></g>',
    "bark_hide": f'<g {G}><path d="{SHIELD}" fill="#a0784a"/><path d="M11 9 C10 14 12 18 11 24 M16 7 V27 M21 9 C22 14 20 18 21 24" '
                 f'fill="none" stroke-width="1.6"/></g>',
}
ICONS.update({f"skill_{k}": v for k, v in SKILLS.items()})
ICONS.update({f"status_{k}": v for k, v in STATUSES.items()})

# --- Elements and weak spots (`el_<element>`, `spot_blunt`, `spot_poison`): the monster panel's weak and resist row ---
ICONS.update({
    "el_fire": f'<g {G}><path d="{FLAME_PATH}" fill="{FIRE}"/><path d="M16 15 C18.5 18.5 20.5 20 20.5 23 C20.5 26 18.5 27.5 16 27.5 '
               f'C13.5 27.5 11.5 26 11.5 23 C11.5 20.5 14 18 16 15 Z" fill="{FLAME}" stroke-width="1.6"/></g>',
    "el_water": f'<g {G}><path d="{DROP}" fill="{WATER}"/><path d="M11.5 20 C11.5 23 13 25 15.5 25.5" fill="none" stroke="{CREAM}" '
                f'stroke-width="2"/></g>',
    "el_ice": f'<g {G}><path d="M16 3 L27 9.5 V22.5 L16 29 L5 22.5 V9.5 Z" fill="{ICE}"/>'
              f'<path d="M16 3 V29 M5 9.5 L27 22.5 M27 9.5 L5 22.5" fill="none" stroke-width="1.6"/></g>',
    "el_wind": f'<path d="M4 11 H19 C23.5 11 24 5.5 20.5 5.5 M4 17 H24.5 C29 17 29.5 23.5 25.5 23.5 M4 23 H14" fill="none" '
               f'stroke="{INK}" stroke-width="5" stroke-linecap="round"/>'
               f'<path d="M4 11 H19 C23.5 11 24 5.5 20.5 5.5 M4 17 H24.5 C29 17 29.5 23.5 25.5 23.5 M4 23 H14" fill="none" '
               f'stroke="{WIND}" stroke-width="2.4" stroke-linecap="round"/>',
    "el_earth": f'<g {G}><path d="M3 27 L11 12 L15.5 18 L21 7 L29 27 Z" fill="{EARTH}"/>'
                f'<path d="M18.5 12 L21 7 L23.5 12 L21 13.5 Z" fill="{CREAM}" stroke-width="1.6"/></g>',
    "el_energy": f'<path d="M18.5 3 L7 18 H15 L13 29 L25 13 H17 Z" fill="{ENERGY}" {G}/>',
    "el_light": f'<g {G}><path d="M16 2.5 V7 M16 25 V29.5 M2.5 16 H7 M25 16 H29.5 M6.5 6.5 L9.5 9.5 M22.5 22.5 L25.5 25.5 '
                f'M25.5 6.5 L22.5 9.5 M9.5 22.5 L6.5 25.5" fill="none"/><circle cx="16" cy="16" r="7" fill="{LIGHT}"/></g>',
    "el_dark": f'<path d="M20 3.5 C12 4.5 6.5 10.5 6.5 17 C6.5 24 12.5 29.5 20 29 C14 26.5 11 21.5 11 16.5 C11 11 14 6 20 3.5 Z" '
               f'fill="{DARK}" {G}/>',
    "spot_blunt": f'<g {G}><path d="M15 13 L21 28" fill="none" stroke-width="3.4"/><path d="M5 9 L19 4 L22 12 L8 17 Z" fill="{STEEL}"/></g>',
    "spot_poison": f'<g {G}><path d="M16 9 C20 15 23 18.5 23 22.5 C23 26.5 20 29 16 29 C12 29 9 26.5 9 22.5 C9 18.5 12 15 16 9 Z" '
                   f'fill="{VENOM}"/><circle cx="21.5" cy="5.5" r="2.2" fill="{VENOM}" stroke-width="1.6"/>'
                   f'<circle cx="26" cy="10" r="1.6" fill="{VENOM}" stroke-width="1.4"/></g>',
})

# --- The Spire (docs/SPIRE_ASSETS.md): its currency, the tower, Sigil caches, one icon per kind of twist ---
GOLD = "#d9a93a"
ICONS.update({
    # Marks: a gold token stamped with a star.
    "mark": f'<g {G}><circle cx="16" cy="16" r="12.5" fill="{GOLD}"/><path d="{_star(16, 16, 7.5, 3.2, 5)}" fill="{CREAM}"/></g>',
    # The Spire: a crenellated tower with a door and a window.
    "spire": f'<g {G}><path d="M10 9 V4 H13 V6.5 H15 V4 H17 V6.5 H19 V4 H22 V9 Z" fill="{STONE}"/>'
             f'<path d="M11 29 L12 9 H20 L21 29 Z" fill="{STONE}"/><path d="M14 29 V25 C14 22.5 18 22.5 18 25 V29" fill="#7a5232"/>'
             f'<path d="M15 13 H17 V17 H15 Z" fill="{INK}" stroke-width="1"/></g>',
    # A Sigil cache: a chest with a star clasp.
    "cache": f'<g {G}><path d="M5 15 C5 8.5 27 8.5 27 15 Z" fill="#c09060"/><path d="M5 15 H27 V27 H5 Z" fill="#a8764a"/>'
             f'<path d="{_star(16, 17, 4.6, 2, 5)}" fill="{GOLD}" stroke-width="1.6"/></g>',
    # Unshakable: an anchor.
    "twist_unshakable": f'<g {G} fill="none"><circle cx="16" cy="6.5" r="3" fill="{STEEL}"/><path d="M16 9.5 V27.5" stroke-width="3"/>'
                        f'<path d="M10 14 H22"/><path d="M5.5 19.5 C6.5 25.5 11.5 28 16 27.5 C20.5 28 25.5 25.5 26.5 19.5"/>'
                        f'<path d="M3.5 21.5 L5.5 18.5 L8.5 20.5 M28.5 21.5 L26.5 18.5 L23.5 20.5"/></g>',
    # Short Fuse: a bomb with a short burning fuse.
    "twist_short_fuse": f'<g {G}><circle cx="14" cy="19" r="9" fill="#4a4a5a"/><path d="M20 12 C21 9 22.5 8 24.5 7" fill="none"/>'
                        f'<path d="{_star(25.5, 5.5, 4, 1.4, 4, 0.4)}" fill="{FLAME}" stroke-width="1.6"/>'
                        f'<path d="M10 15 C11 13.5 12.5 13 14 13" fill="none" stroke="{CREAM}" stroke-width="1.8"/></g>',
    # No Potions: a potion bottle struck through.
    "twist_no_potions": f'<g {G}><path d="M13 4 H19 V10 C25 12 27 17 26 22 C25 27 21 29 16 29 C11 29 7 27 6 22 C5 17 7 12 13 10 Z" '
                        f'fill="#c8452f"/><path d="M12 4 H20" fill="none" stroke-width="3.2"/></g>'
                        f'<path d="M5 27 L27 5" stroke="{INK}" stroke-width="6" stroke-linecap="round"/>'
                        f'<path d="M5 27 L27 5" stroke="{CREAM}" stroke-width="2.8" stroke-linecap="round"/>',
    # Element Veil: a ring closed all around but one gap, where the one weak element shines through.
    "twist_veil": f'<path d="M24.5 7.5 A11.5 11.5 0 1 0 28 17" fill="none" stroke="{INK}" stroke-width="6.4" stroke-linecap="round"/>'
                  f'<path d="M24.5 7.5 A11.5 11.5 0 1 0 28 17" fill="none" stroke="#9fb8e6" stroke-width="3.6" stroke-linecap="round"/>'
                  f'<path d="{_star(27, 11.5, 7, 2.8, 4, 0.4)}" fill="{FLAME}" {G} stroke-width="1.8"/>',
    # Element Shell: a shield holding three charges (the hits that break it).
    "twist_shell": f'<g {G}><path d="{SHIELD}" fill="#8fa3b8"/><circle cx="10.5" cy="15" r="2.4" fill="{CREAM}"/>'
                   f'<circle cx="16" cy="15" r="2.4" fill="{CREAM}"/><circle cx="21.5" cy="15" r="2.4" fill="{CREAM}"/></g>',
    # Ambush: eyes glowing in a bush.
    "twist_ambush": f'<g {G}><path d="M3.5 27 C2.5 19 7.5 14 11.5 16 C12.5 9.5 20 8.5 22 13.5 C27 12 30 18.5 28.5 27 Z" fill="#4f7a4a"/>'
                    f'<path d="M10 20.5 C11.5 18.5 14 18.5 15 20.5 C13.5 22 11.5 22 10 20.5 Z" fill="{FLAME}" stroke-width="1.6"/>'
                    f'<path d="M17 20.5 C18 18.5 20.5 18.5 22 20.5 C20.5 22 18.5 22 17 20.5 Z" fill="{FLAME}" stroke-width="1.6"/></g>',
    # The Elite affixes as twists. Hulking: a heart with a plus (more HP).
    "twist_hulking": f'<g {G}><path d="{HEART}" fill="{BLOOD}"/><path d="{PLUS.format(x=14, y=9)}" fill="{CREAM}" stroke-width="1.6"/></g>',
    # Frenzied: three claw slashes.
    "twist_frenzied": f'<path d="M7 6 L13 26 M14 4.5 L19 25 M21 6 L25 22" fill="none" stroke="{INK}" stroke-width="5" stroke-linecap="round"/>'
                      f'<path d="M7 6 L13 26 M14 4.5 L19 25 M21 6 L25 22" fill="none" stroke="{BLOOD}" stroke-width="2.4" stroke-linecap="round"/>',
    # Warded: a shield with a rune star (resists everything a little).
    "twist_warded": f'<g {G}><path d="{SHIELD}" fill="#7a6bb0"/><path d="{_star(16, 15, 6, 2.2, 4)}" fill="{CREAM}" stroke-width="1.6"/></g>',
    # Venomous: two fangs and a drop of venom.
    "twist_venomous": f'<g {G}><path d="M4 5 H28" fill="none" stroke-width="2.8"/>'
                      f'<path d="M6 5 H13 C12.5 11 11 15 9.5 18.5 C8 15 7 10.5 6 5 Z" fill="{CREAM}"/>'
                      f'<path d="M19 5 H26 C25 10.5 24 15 22.5 18.5 C21 15 19.5 11 19 5 Z" fill="{CREAM}"/>'
                      f'<path d="M16 17.5 C18.5 21 19.5 23 19.5 24.5 C19.5 26.7 18 28 16 28 C14 28 12.5 26.7 12.5 24.5 C12.5 23 13.5 21 16 17.5 Z" '
                      f'fill="{VENOM}"/></g>',
    # Ironhide: a riveted iron plate.
    "twist_ironhide": f'<g {G}><rect x="5.5" y="6.5" width="21" height="19" rx="4" fill="{STEEL}"/>'
                      f'<path d="M10 12 L15 9.5" fill="none" stroke="{CREAM}" stroke-width="2"/></g>'
                      f'<g fill="{INK}"><circle cx="9.5" cy="21.5" r="1.5"/><circle cx="22.5" cy="21.5" r="1.5"/>'
                      f'<circle cx="22.5" cy="10.5" r="1.5"/></g>',
})

SHEET = """<!doctype html><html><head><meta charset="utf-8"><style>
body {{ margin: 0; background: #1f2738; font: 11px sans-serif; color: #fff4dc; }}
.row {{ display: flex; flex-wrap: wrap; width: {w}px; }}
.cell {{ width: 180px; height: 96px; display: flex; flex-direction: column; align-items: center; justify-content: center; }}
.pair {{ display: flex; gap: 6px; }}
.sand {{ background: #d8c59c; border-radius: 8px; padding: 4px; }}
.navy {{ background: #2b3650; border-radius: 8px; padding: 4px; }}
</style></head><body>
<svg width="0" height="0" style="position:absolute"><defs>
<filter id="wobble"><feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="5"/>
<feDisplacementMap in="SourceGraphic" scale="2.4"/></filter></defs></svg>
<div class="row">{cells}</div></body></html>"""


def sheet(names: list[str], out: Path) -> None:
    """A review sheet: every icon at button size on sand felt (ink set) and dark felt (light set), named."""
    cells = []
    for n in names:
        dark = ICONS[n].replace(INK, LIGHT_INK).replace(CREAM, LIGHT_FILL)
        svg = '<svg width="{s}" height="{s}" viewBox="-2 -2 36 36"><g filter="url(#wobble)">{b}</g></svg>'
        cells.append(f'<div class="cell"><div class="pair"><div class="sand">{svg.format(s=56, b=ICONS[n])}</div>'
                     f'<div class="navy">{svg.format(s=56, b=dark)}</div><div class="sand">{svg.format(s=24, b=ICONS[n])}</div>'
                     f'</div><div>{n.replace("skill_", "").replace("status_", "")}</div></div>')
    cols = 8
    w = cols * 180
    h = ((len(names) + cols - 1) // cols) * 96
    with tempfile.TemporaryDirectory() as t:
        html = Path(t) / "sheet.html"
        html.write_text(SHEET.format(w=w, cells="".join(cells)), encoding="utf-8")
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--window-size={w},{h}",
                        f"--screenshot={out}", html.as_uri()], check=True, capture_output=True, timeout=60)
    print(out)


PAGE = """<!doctype html><html><head><meta charset="utf-8"><style>
html, body {{ margin: 0; padding: 0; background: transparent; }}
svg {{ display: block; }}
</style></head><body>
<svg width="0" height="0" style="position:absolute"><defs>
<filter id="wobble"><feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="5"/>
<feDisplacementMap in="SourceGraphic" scale="2.4"/></filter></defs></svg>
<svg width="{size}" height="{size}" viewBox="-2 -2 36 36"><g filter="url(#wobble)">{body}</g></svg>
</body></html>"""


def render(name: str, body: str, tmp: Path, out: Path = OUT) -> None:
    html = tmp / f"{name}.html"
    html.write_text(PAGE.format(size=SIZE, body=body), encoding="utf-8")
    shot = tmp / f"{name}.png"
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                    f"--window-size={SIZE},{SIZE}", f"--screenshot={shot}", html.as_uri()],
                   check=True, capture_output=True, timeout=60)
    img = Image.open(shot).convert("RGBA").crop((0, 0, SIZE, SIZE))
    out.mkdir(parents=True, exist_ok=True)
    img.save(out / f"{name}.png")
    print(f"  {name}.png")


def main() -> None:
    if len(sys.argv) > 2 and sys.argv[1] == "sheet":
        # python scripts/ui_icons.py sheet <out.png> [prefix]: a review sheet of every icon (or those starting with prefix)
        prefix = sys.argv[3] if len(sys.argv) > 3 else ""
        sheet([n for n in ICONS if n.startswith(prefix)], Path(sys.argv[2]))
        return
    light = "--light" in sys.argv
    names = [a for a in sys.argv[1:] if not a.startswith("--")] or list(ICONS)
    out = OUT.parent / "UiIconsLight" if light else OUT
    with tempfile.TemporaryDirectory() as t:
        for n in names:
            body = ICONS[n]
            if light:
                body = body.replace(INK, LIGHT_INK).replace(CREAM, LIGHT_FILL)
            render(n, body, Path(t), out)


if __name__ == "__main__":
    main()
