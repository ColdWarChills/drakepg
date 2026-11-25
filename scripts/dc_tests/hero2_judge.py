"""Try 2 of the hero's look: today's hero beside the dressed skeleton, judged fairly (2026-11-25).

<venv python> -I -B hero2_judge.py <stills dir> <name> [out.png]
Both figures are brought to the same pixel height at game size (GAME_H, today's hero in a 1280-wide battle), then
enlarged 3x the same way (LANCZOS both times), so neither gets a sharper camera. The skeleton's height is read from its
standing still (frame 0, the sword held low inside the body's height) and the same factor is used for the strike (33).
Prints both figures' heights in heads (crown of the hair to chin) to check "stocky". Writes into art/tests (not in git).
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

RAW = Path(r"C:/Users/Epic/Desktop/dragonfab/art/raw/char_hero_bare.png")
GAME_H = 236
ZOOM = 3
BG = (102, 112, 120)


def figure_box(img):
    a = np.asarray(img.convert("RGB")).astype(int)
    bg = a[3, 3]
    m = np.abs(a - bg).sum(-1) > 30
    ys, xs = np.where(m)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def at_game(img, factor):
    small = img.resize((max(1, round(img.width * factor)), max(1, round(img.height * factor))), Image.LANCZOS)
    return small.resize((small.width * ZOOM, small.height * ZOOM), Image.LANCZOS)


def main():
    d, name = Path(sys.argv[1]), sys.argv[2]
    out = Path(sys.argv[3]) if len(sys.argv) > 3 else d / f"judge_{name}.png"
    hero = Image.open(RAW).convert("RGBA")
    hb = hero.split()[3].point(lambda v: 255 if v > 10 else 0).getbbox()
    hero = hero.crop(hb)
    bgimg = Image.new("RGBA", hero.size, BG + (255,))
    bgimg.alpha_composite(hero)
    hero_rgb = bgimg.convert("RGB")
    f_hero = GAME_H / hero.height
    stand = Image.open(d / f"{name}_f000.png").convert("RGB")
    sb = figure_box(stand)
    f_sk = GAME_H / (sb[3] - sb[1])
    tiles = [at_game(hero_rgb, f_hero), at_game(stand.crop(sb), f_sk)]
    strike = d / f"{name}_f033.png"
    if strike.exists():
        s = Image.open(strike).convert("RGB")
        tiles.append(at_game(s.crop(figure_box(s)), f_sk))
    H = max(t.height for t in tiles) + 40
    W = sum(t.width for t in tiles) + 40 * (len(tiles) + 1)
    sheet = Image.new("RGB", (W, H), BG)
    x = 40
    for t in tiles:
        sheet.paste(t, (x, H - 20 - t.height))
        x += t.width + 40
    sheet.save(out)
    print("hero", hero.size, "skeleton stand box", sb, "->", out)


if __name__ == "__main__":
    main()
