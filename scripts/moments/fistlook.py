"""Look files for the clean sword fist (step A3), in art/tests/moments/. Each at most 1568 px and 1.1 megapixels.

<venv python> -I -B fistlook.py try <moment> <clean try> [<clean try> ...]   quick: fist4x_<moment>_try.png (one row
                                                                              per try: marker | clean | fist alone | sandwich)
<venv python> -I -B fistlook.py all     fist4x_<m>_a/_b, fists_game, glove4x_ready/_wind, bare_strip_1/_2
Everything is built in memory from moments.json (marker drawing + clean drawing), as build.py does.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, RAW, REDO, TESTS, load_rgba  # noqa: E402
import bare  # noqa: E402
import cut  # noqa: E402
from sheets import LABEL, PAPER, font  # noqa: E402

B = 175        # box round the fist centre, drawing px
GREY = (128, 128, 128, 255)
LIGHT = (232, 228, 218, 255)
MOMENTS = ["ready", "wind", "strike", "follow"]


def picks():
    return json.loads((REDO / "moments.json").read_text())


def kit(m: str, clean_name: str | None = None, pad: int = 0):
    p = picks()
    a = load_rgba(REDO / f"{p[m]}.png")
    cn = clean_name or p.get("clean", {}).get(m)
    c = load_rgba(REDO / f"{cn}.png") if cn else None
    if pad:
        a = np.pad(a, ((pad, pad), (pad, pad), (0, 0)))
        c = np.pad(c, ((pad, pad), (pad, pad), (0, 0))) if c is not None else None
    parts = bare.build(a, m, p[m], clean=c)
    return a, c, parts


def stack(parts: dict, glove: np.ndarray | None = None) -> np.ndarray:
    out = np.zeros_like(parts["body"])
    if parts["order"] == "back":
        out = cut.over(out, parts["weapon"])
    out = cut.over(out, parts["body"])
    if parts["order"] == "front":
        out = cut.over(out, parts["weapon"])
    if not (glove is not None and parts.get("glove_hides_fist")):
        out = cut.over(out, parts["fist_layer"])
    if glove is not None:
        out = cut.over(out, glove)
    return out


def box4x(src: np.ndarray, centre, ground=LIGHT) -> Image.Image:
    cx, cy = centre
    b = (int(round(cx - B / 2)), int(round(cy - B / 2)))
    b = b + (b[0] + B, b[1] + B)
    im = Image.new("RGBA", (B, B), ground)
    im.alpha_composite(Image.fromarray(src).crop(b))
    return im.resize((B * 4, B * 4), Image.NEAREST)


def pair(left: Image.Image, right: Image.Image, title: str, ll: str, rl: str) -> Image.Image:
    top = 36
    s = Image.new("RGB", (B * 8 + 10, B * 4 + top), PAPER)
    s.paste(left.convert("RGB"), (0, top))
    s.paste(right.convert("RGB"), (B * 4 + 10, top))
    d = ImageDraw.Draw(s)
    d.text((8, 6), f"{title} - left: {ll}; right: {rl} (175 px box at 4x, nearest)", fill=(40, 30, 20), font=font(20))
    return s


def look_try(m: str, names: list):
    rows = []
    for n in names:
        a, c, parts = kit(m, n)
        cen = parts["rod"]["center"]
        tiles = [box4x(a, cen), box4x(c, cen), box4x(parts["fist_layer"], cen, GREY), box4x(stack(parts), cen)]
        row = Image.new("RGB", (4 * 350 + 30, 380), PAPER)
        for i, t in enumerate(tiles):
            row.paste(t.resize((350, 350), Image.NEAREST).convert("RGB"), (i * 360, 30))
        ImageDraw.Draw(row).text((6, 4), f"{n}: marker | clean | fist copy on grey | body+sword+fist",
                                 fill=(40, 30, 20), font=font(18))
        rows.append(row)
    s = Image.new("RGB", (rows[0].width, 380 * len(rows)), PAPER)
    for i, r in enumerate(rows):
        s.paste(r, (0, 380 * i))
    s.save(TESTS / f"fist4x_{m}_try.png")
    print(TESTS / f"fist4x_{m}_try.png", s.size)


def game_box(comp: np.ndarray, centre) -> tuple:
    """The composed hero at game size (480 px tall standing), a 110 px box round the fist -> (1x, 3x nearest)."""
    s = 480 / HERO_H
    im = Image.fromarray(comp)
    small = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    cx, cy = centre[0] * s, centre[1] * s
    b = (int(round(cx - 55)), int(round(cy - 55)), int(round(cx + 55)), int(round(cy + 55)))
    t = Image.new("RGBA", (110, 110), LIGHT)
    t.alpha_composite(small.crop(b))
    return t, t.resize((330, 330), Image.NEAREST)


def gloves_for(m: str, parts: dict):
    """The near-hand glove layer of the step-A3 glove (built by build.py into layers/<m>/), or None."""
    man = json.loads((REDO / "layers" / "manifest.json").read_text())["moments"].get(m, {})
    g = man.get("pieces", {}).get(GLOVE, {}).get("gloves_near")
    if not g:
        return None
    parts["glove_hides_fist"] = g.get("hides_fist", False)
    return load_rgba(REDO / "layers" / m / g["file"])


GLOVE = "scale_gauntlets_a3"


def look_all():
    games_bare, games_glove = [], []
    for m in MOMENTS:
        a, c, parts = kit(m)
        cen = parts["rod"]["center"]
        lab = LABEL[m]
        pair(box4x(a, cen), box4x(c, cen), f"{lab}", "marker drawing", "clean drawing").save(
            TESTS / f"fist4x_{m}_a.png")
        pair(box4x(parts["fist_layer"], cen, GREY), box4x(stack(parts), cen), f"{lab}", "fist layer alone on grey",
             "body + training sword + fist").save(TESTS / f"fist4x_{m}_b.png")
        games_bare.append((lab, game_box(stack(parts), cen)))
        g = gloves_for(m, parts)
        if g is not None:
            pair(box4x(g, cen, GREY), box4x(stack(parts, g), cen), f"{lab}, Scale Gauntlets", "near glove layer alone",
                 "body + sword + fist + glove").save(TESTS / f"glove4x_{m}.png")
            games_glove.append((lab + ", gloved", game_box(stack(parts, g), cen)))
    # fists_game: three columns of 3x boxes (bare moments, then the gloved ones), then the same boxes at 1x
    tiles = games_bare + games_glove
    rows = (len(tiles) + 2) // 3
    s = Image.new("RGB", (3 * 340 + 20, 40 + rows * 360 + 140), PAPER)  # at most 1.1 megapixels
    d = ImageDraw.Draw(s)
    d.text((8, 8), "At game size (him 480 px tall): 110 px round the sword fist, 3x nearest; bottom: the same at 1x",
           fill=(40, 30, 20), font=font(17))
    for i, (lab, (one, three)) in enumerate(tiles):
        x, y = 10 + (i % 3) * 340, 40 + (i // 3) * 360
        d.text((x, y), lab, fill=(40, 30, 20), font=font(18))
        s.paste(three.convert("RGB"), (x, y + 24))
    for i, (lab, (one, three)) in enumerate(tiles):
        s.paste(one.convert("RGB"), (10 + i * 120, 40 + rows * 360 + 15))
    s.save(TESTS / "fists_game.png")
    # the strips on Fen Road, through sheets.strip (soles on one line, him 480 px tall)
    import grip
    import sheets
    figs, sy = {}, {}
    for m in MOMENTS:
        a, c, parts = kit(m, pad=500)
        figs[m], sy[m] = stack(parts), grip.soles_y(parts["body"])
    sheets.MOMENTS = MOMENTS
    for k, two in ((1, MOMENTS[:2]), (2, MOMENTS[2:])):
        sub = {m: figs[m] for m in two}
        old = sheets.MOMENTS
        sheets.MOMENTS = two + two  # strip() lays out rows of two; draw only the first row
        st = sheets.strip({**sub}, f"Bare: {LABEL[two[0]]} and {LABEL[two[1]]} (him 480 px tall, real training "
                                   f"sword, clean drawing + fist copy)", sy)
        sheets.MOMENTS = old
        st = st.crop((0, 0, st.width, 40 + 660))
        st.save(TESTS / f"bare_strip_{k}.png")
    print("look files written")


def follow_tries(entries: list):
    """follow_tries.png: every follow-through try of step A3 (and the kept one), small, with the real sword placed,
    F (hand-read when grip.ARMS has it, else auto), T, turn, tip room and why it was dropped or kept.
    entries: [(try name, verdict text)]"""
    import grip
    cols = 4 if len(entries) <= 8 else 6  # at most 1.1 megapixels
    cell, txt_h, wrap = (280, 140, 40) if cols == 4 else (180, 175, 25)
    rows = (len(entries) + cols - 1) // cols
    s = Image.new("RGB", (cols * cell + (cols + 1) * 8, 40 + rows * (cell + txt_h)), PAPER)
    d = ImageDraw.Draw(s)
    d.text((8, 8), "Follow-through tries (step A3), the real training sword placed by the marker; F forearm, T blade, "
                   "turn T-F", fill=(40, 30, 20), font=font(18))
    for i, (n, why) in enumerate(entries):
        x, y = 8 + (i % cols) * (cell + 8), 40 + (i // cols) * (cell + txt_h)
        a = load_rgba(REDO / f"{n}.png")
        if not n.startswith("norm_"):
            a = bare.normalize(a, picks()["head_ref"])[0]
        a = np.pad(a, ((0, 300), (150, 150), (0, 0)))
        try:
            parts = bare.build(a, "follow", n)
            comp = cut.over(parts["body"], parts["weapon"])
            comp = cut.over(comp, parts["fist_layer"])
            rod = parts["rod"]
            T = grip.ang(rod["d"])
            key = bare.drawing_key(n)
            if key in grip.ARMS:
                e, w = grip.ARMS[key]["elbow"], grip.ARMS[key]["wrist"]
                F, fsrc = grip.ang((w[0] - e[0], w[1] - e[1])), ""
            else:
                F, fsrc = grip.auto_F(a, rod), " auto"
            r = grip.reach(n)
            info = (f"F {F:.0f}{fsrc}  T {T:.0f}  turn {grip.wrap(T - F):+.0f}\nfist {r['fist_above_soles']} px above "
                    f"the soles; tip {abs(r['tip_below_soles'])} px "
                    f"{'below' if r['tip_below_soles'] > 0 else 'above'} the soles")
        except ValueError as e:
            comp, info = a, f"marker unreadable: {str(e)[:60]}"
        im = Image.new("RGBA", (a.shape[1], a.shape[0]), LIGHT)
        im.alpha_composite(Image.fromarray(comp))
        ys, xs = np.nonzero(comp[..., 3] > 30)
        im = im.crop((xs.min() - 10, ys.min() - 10, xs.max() + 10, ys.max() + 10))
        im.thumbnail((cell, cell), Image.LANCZOS)
        s.paste(im.convert("RGB"), (x + (cell - im.width) // 2, y))
        import textwrap
        lines = [n] + [w for part in (info + "\n" + why).split("\n") for w in textwrap.wrap(part, wrap)]
        d.text((x, y + cell + 2), "\n".join(lines[:10]), fill=(40, 30, 20), font=font(13 if cols == 6 else 14))
    s.save(TESTS / "follow_tries.png")
    print(TESTS / "follow_tries.png", s.size)


if __name__ == "__main__":
    if sys.argv[1] == "try":
        look_try(sys.argv[2], sys.argv[3:])
    elif sys.argv[1] == "follow":
        follow_tries(json.loads(Path(sys.argv[2]).read_text()))
    elif sys.argv[1] == "all":
        look_all()
