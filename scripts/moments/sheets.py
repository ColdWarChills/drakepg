"""Sheets for the designer (art/tests/moments/, each at most 1568 px on its long side).

<venv python> -I -B sheets.py [bare|mixes|hands|heads|elbows|beside|sleeves|seams|all]
Reads the kept moments (moments.json) and the layers in art/redo/moments/layers/ (build.py makes them).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import FEET_Y, HERO_H, RAW, REDO, TESTS, load_rgba  # noqa: E402

MOMENTS = ["ready", "wind", "strike", "follow"]
LABEL = {"ready": "ready", "wind": "wind-up", "strike": "strike", "follow": "follow-through"}
MIX1 = ["scale_mail", "leather_cap", "scale_gauntlets", "leather_leggings", "scale_boots"]
MIX2 = ["leather_armor", "scale_coif", "leather_gloves", "scale_leggings", "leather_boots"]
PAPER = (240, 234, 222)
MAX = 1568


def font(n):
    try:
        return ImageFont.truetype("arial.ttf", n)
    except OSError:
        return ImageFont.load_default()


def fit(im: Image.Image) -> Image.Image:
    if max(im.size) > MAX:
        im = im.copy()
        im.thumbnail((MAX, MAX), Image.LANCZOS)
    return im


def bbox(a: np.ndarray, pad: int = 0):
    ys, xs = np.nonzero(a[..., 3] > 20)
    return xs.min() - pad, ys.min() - pad, xs.max() + 1 + pad, ys.max() + 1 + pad


def soles() -> dict:
    """moment -> his soles' y on the canvas, from the body layer (no weapon): layers/manifest.json (build.py)."""
    man = json.loads((REDO / "layers" / "manifest.json").read_text())["moments"]
    return {m: man[m]["soles_y"] for m in MOMENTS}


def strip(figs: dict, title: str, sole_y: dict | None = None) -> Image.Image:
    """figs: moment -> RGBA canvas. Him 480 px tall standing (a 2560x1440 screen), soles on one ground line on Fen
    Road: the soles are found on the body layer (sole_y, default the manifest's), never from the lowest pixel, which
    can be the sword tip. Four moments at 480 px are wider than 1568 px, so the strip is two rows of two (same ground
    line height)."""
    s = 480 / HERO_H
    sole_y = sole_y or soles()
    cut = {}
    for m in MOMENTS:
        a = figs[m]
        x0, y0, x1, y1 = bbox(a)
        im = Image.fromarray(a).crop((x0, y0, x1, y1))
        im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
        cut[m] = (im, round((sole_y[m] + 1 - y0) * s))  # soles, in the scaled crop
    bg = Image.open(RAW / "bg_fen_road.png").convert("RGB").resize((2560, 1440), Image.LANCZOS)
    ground, H, gap = 600, 660, 40  # in-game feet at y 510 of 720 -> 1020 on 2560x1440
    rows = [MOMENTS[:2], MOMENTS[2:]]
    W = min(MAX, max(sum(cut[m][0].width for m in r) + gap * 3 for r in rows))
    sheet = Image.new("RGB", (W, 40 + 2 * H), PAPER)
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), title, fill=(40, 30, 20), font=font(22))
    for i, r in enumerate(rows):
        y = 40 + i * H
        sheet.paste(bg.crop((i * 700, 1020 - ground, i * 700 + W, 1020 - ground + H)), (0, y))
        x = gap
        for m in r:
            im, soles = cut[m]
            sheet.paste(im, (x, y + ground - soles), im)
            d.text((x, y + 6), LABEL[m], fill=(255, 250, 240), font=font(22))
            x += im.width + gap
    return fit(sheet)


def grid(rows: list, cols: list, cells: dict, title: str, cell: int) -> Image.Image:
    """cells[(row, col)] = PIL image (cell x cell)."""
    lw, th = 150, 70
    sheet = Image.new("RGB", (lw + cell * len(cols), th + cell * len(rows)), PAPER)
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), title, fill=(40, 30, 20), font=font(22))
    for j, c in enumerate(cols):
        d.text((lw + j * cell + 8, 40), c, fill=(60, 45, 30), font=font(18))
    for i, r in enumerate(rows):
        d.text((8, th + i * cell + cell // 2 - 10), r, fill=(60, 45, 30), font=font(18))
        for j, c in enumerate(cols):
            im = cells.get((r, c))
            if im is not None:
                sheet.paste(im.convert("RGB"), (lw + j * cell, th + i * cell))
    return fit(sheet)


def crop3x(a: np.ndarray, centre, half: int, cell: int) -> Image.Image:
    """A square around centre (canvas px), scaled to 3x game size (him 720 px tall), on paper, resized to cell."""
    cx, cy = centre
    s = 720 / HERO_H
    box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
    im = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), PAPER + (255,))
    im.alpha_composite(Image.fromarray(a).crop(box))
    im = im.resize((round(2 * half * s),) * 2, Image.LANCZOS)
    if im.width != cell:  # pad/crop to the cell, keeping 3x scale
        c = Image.new("RGBA", (cell, cell), PAPER + (255,))
        c.paste(im, ((cell - im.width) // 2, (cell - im.height) // 2))
        im = c
    return im


def dressed(m: str, worn: list) -> np.ndarray:
    """The hero of moment m wearing the listed pieces, stacked from the built sprites (build.py) in the fixed order."""
    import layers as L
    d = REDO / "layers" / m
    man = json.loads((REDO / "layers" / "manifest.json").read_text())["moments"][m]
    sp = {k: load_rgba(d / f"{k}.png") for k in ("body", "shirt", "hair", "fist", "weapon")}
    lay = {}
    for piece in worn:
        for k, v in man["pieces"].get(piece, {}).items():
            lay[k] = load_rgba(d / v["file"])
    out = np.zeros_like(sp["body"])
    if man["weapon_order"] == "back":
        out = L.cut.over(out, sp["weapon"])
    out = L.cut.over(out, sp["body"])
    if "chest" not in lay:
        out = L.cut.over(out, sp["shirt"])
    if "helm" not in lay:
        out = L.cut.over(out, sp["hair"])
    for name in L.ORDER:
        if name == "WEAPON":
            if man["weapon_order"] == "front":
                out = L.cut.over(out, sp["weapon"])
        elif name == "fist":
            out = L.cut.over(out, sp["fist"])
        elif name in lay:
            out = L.cut.over(out, lay[name])
    return out


# ---- the sheets -------------------------------------------------------------------------------------------------
def picks():
    return json.loads((REDO / "moments.json").read_text())


def composed(name: str, moment: str, normalize: bool = False, pad: int = 0):
    """pad: transparent margin round the canvas, so a sword reaching past the 1536 canvas is drawn whole on a sheet."""
    import bare
    a = load_rgba(REDO / f"{name}.png")
    if normalize:
        a = bare.normalize(a, picks()["head_ref"])[0]
    if pad:
        a = np.pad(a, ((pad, pad), (pad, pad), (0, 0)))
    p = bare.build(a, moment, name)
    return a, p, bare.compose(p)


def near_arm_crop(a, p, comp, cell=None):
    """The near arm from the shoulder to the fist at 3x."""
    import gate1
    near, _ = gate1.arm_boxes(a, p)
    from scipy import ndimage as ndi
    ys, xs = np.nonzero(ndi.binary_dilation(near, iterations=50))
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    half = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2
    im = crop3x(comp, (cx, cy), half, round(2 * half * 720 / HERO_H))
    return im


def sheet_sleeves():
    """Retired in step A4: the sleeve was chosen in step A (plain short sleeve) and its tries were built the step-A2 way
    (rod inpainted, no clean drawing), which no longer matches what the game shows. sleeves_3x.png stays as the record
    of that choice; the clean moments are seen in look.py's sheets."""
    print("sheets.py sleeves is retired (step A4): see the docstring; sleeves_3x.png is kept as the record")
    return


def _sheet_sleeves_step_a():
    rows =[("ready", "bare_ready_s102", picks()["raw"]["ready"]), ("strike", "bare_strike_s301", picks()["raw"]["strike"])]
    tiles = []
    for m, rolled, plain in rows:
        for n in (rolled, plain):
            a, p, comp = composed(n, m, normalize=True)
            tiles.append(near_arm_crop(a, p, comp))
    cw = max(t.width for t in tiles)
    ch = max(t.height for t in tiles)
    s = Image.new("RGB", (120 + 2 * cw + 10, 70 + 2 * ch + 10), PAPER)
    d = ImageDraw.Draw(s)
    d.text((10, 6), "Near arm at 3x", fill=(40, 30, 20), font=font(22))
    d.text((120, 40), "rolled sleeve (game today)", fill=(40, 30, 20), font=font(18))
    d.text((120 + cw + 10, 40), "plain short sleeve (picked)", fill=(40, 30, 20), font=font(18))
    for i, t in enumerate(tiles):
        r, c = divmod(i, 2)
        s.paste(t.convert("RGB"), (120 + c * (cw + 10), 70 + r * (ch + 10)))
        if c == 0:
            d.text((8, 70 + r * (ch + 10) + ch // 2), rows[r][0], fill=(60, 45, 30), font=font(20))
    fit(s).save(TESTS / "sleeves_3x.png")
    print("sleeves_3x", fit(s).size)


def sheet_beside():
    """Bare ready and strike beside the game's drawn ones at the same height (hero 480 px standing)."""
    figs = []
    for key in ("char_hero_bare_sword_ready", "char_hero_bare_sword_strike"):
        im = Image.open(RAW / f"{key}.png").convert("RGBA")
        figs.append((key, im))
    out = []
    # ours
    for m in ("ready", "strike"):
        a, p, comp = composed(picks()[m], m)
        im = Image.fromarray(comp)
        im = im.crop(im.getbbox())
        sc = 480 / HERO_H
        out.append((f"new {m}", im.resize((round(im.width * sc), round(im.height * sc)), Image.LANCZOS)))
    # the game's: same head height as ours (measured), so the same size on screen
    import gate1
    ref = picks()["head_ref"] * 480 / HERO_H
    game = []
    for key, im in figs:
        a = np.array(im)
        a[..., 3] = np.where(a[..., 3] < 10, 0, a[..., 3])
        hh = gate1.head_height(a)
        sc = ref / hh
        im2 = Image.fromarray(a)
        im2 = im2.crop(im2.getbbox())
        game.append((f"game {key.split('_')[-1]} (head-matched)", im2.resize((round(im2.width * sc), round(im2.height * sc)), Image.LANCZOS)))
    items = [game[0], out[0], game[1], out[1]]
    W = sum(i[1].width for i in items) + 50 * 5
    H = max(i[1].height for i in items) + 100
    s = Image.new("RGB", (W, H), PAPER)
    d = ImageDraw.Draw(s)
    x = 50
    for lab, im in items:
        s.paste(im, (x, H - 30 - im.height), im)
        d.text((x, 10), lab, fill=(40, 30, 20), font=font(20))
        x += im.width + 50
    fit(s).save(TESTS / "beside_game.png")
    print("beside_game", fit(s).size)


def sheet_bare():
    import grip
    figs, sy = {}, {}
    for m in MOMENTS:
        a, p, comp = composed(picks()[m], m, pad=500)
        figs[m], sy[m] = comp, grip.soles_y(p["body"])
    strip(figs, "Bare, four moments of one sword attack (him 480 px tall, as on a 2560x1440 screen; soles on one "
          "line)", sy).save(TESTS / "bare_strip.png")


def sheet_hands_full():
    """hands_full_1 (ready, wind-up) and hands_full_2 (strike, follow-through): per moment a 260 px box round the sword
    fist of the 1536 px drawing, enlarged to 700 px: left the raw drawing with the marker, right body + sword + fist."""
    B, Z, top, lab = 260, 700, 50, 34
    for k, pair in ((1, ("ready", "wind")), (2, ("strike", "follow"))):
        sheet = Image.new("RGB", (2 * Z + 10, top + 2 * (Z + lab)), PAPER)
        d = ImageDraw.Draw(sheet)
        d.text((10, 10), "Sword hand (his right, the near arm), 260 px of the 1536 px drawing shown at 700 px",
               fill=(40, 30, 20), font=font(22))
        for i, m in enumerate(pair):
            a, p, comp = composed(picks()[m], m)
            cx, cy = p["rod"]["center"]
            box = (round(cx - B / 2), round(cy - B / 2), round(cx + B / 2), round(cy + B / 2))
            y = top + i * (Z + lab)
            for j, (src, what) in enumerate(((a, "raw drawing with the marker"), (comp, "body + sword + fist"))):
                im = Image.new("RGBA", (B, B), PAPER + (255,))
                im.alpha_composite(Image.fromarray(src).crop(box))
                sheet.paste(im.resize((Z, Z), Image.LANCZOS).convert("RGB"), (j * (Z + 10), y + lab))
                d.text((j * (Z + 10) + 6, y + 6), f"{LABEL[m]}: {what}", fill=(40, 30, 20), font=font(20))
        fit(sheet).save(TESTS / f"hands_full_{k}.png")
        print(f"hands_full_{k}", fit(sheet).size)


def _bare_kit(m):
    import bare
    a = load_rgba(REDO / f"{picks()[m]}.png")
    return a, bare.build(a, m, picks()[m])


def head_centre(a):
    import place
    hb = place.head_box(place.classes(a))
    ys, xs = np.nonzero(hb)
    return xs.mean(), ys.mean()


def sheet_hands():
    """Both hands at 3x: the sword hand (his right, the near arm) and the free hand (his left, the far arm)."""
    import gate1
    kinds = ["bare", "Leather Gloves", "Scale Gauntlets"]
    cols = [f"sword hand: {k}" for k in kinds] + [f"free hand: {k}" for k in kinds]
    cells, rows = {}, []
    C = 235
    for m in MOMENTS:
        a, p = _bare_kit(m)
        r = LABEL[m]
        rows.append(r)
        import place
        _, far = gate1.arm_boxes(a, p)
        hy, hx = np.nonzero(place.head_box(place.classes(a)))
        free = gate1.tip(far, (hx.mean(), hy.mean())) if far.any() else p["rod"]["center"]  # fist = arm end off the head
        free = (free[0] + (8 if free[0] > hx.mean() else -8), free[1])
        for k, worn in zip(kinds, ([], ["leather_gloves"], ["scale_gauntlets"])):
            d = dressed(m, worn)
            cells[(r, f"sword hand: {k}")] = crop3x(d, p["rod"]["center"], C / 2 / 0.72, C)
            if m == "ready":  # his free fist sits right under the sword fist: it is in the sword-hand cells
                note = Image.new("RGB", (C, C), PAPER)
                ImageDraw.Draw(note).text((10, C // 2 - 20), "free fist: lower right\nof the sword-hand cell",
                                          fill=(60, 45, 30), font=font(16))
                cells[(r, f"free hand: {k}")] = note
            else:
                cells[(r, f"free hand: {k}")] = crop3x(d, free, C / 2 / 0.72, C)
    fit(grid(rows, cols, cells, "Both hands at 3x (him 720 px tall): sword hand = his right (near arm), free hand = "
             "his left (far arm)", C)).save(TESTS / "hands_3x.png")


def sheet_heads():
    cols = ["bare", "Leather Cap", "Scale Coif"]
    cells, rows = {}, []
    for m in MOMENTS:
        a, p = _bare_kit(m)
        r = LABEL[m]
        rows.append(r)
        for c, worn in zip(cols, ([], ["leather_cap"], ["scale_coif"])):
            cells[(r, c)] = crop3x(dressed(m, worn), head_centre(a), 190 / 0.72, 380)
    grid(rows, cols, cells, "Head at 3x (him 720 px tall)", 380).save(TESTS / "heads_3x.png")


def sheet_elbows():
    cols = ["bare", "Scale Mail", "Leather Armor"]
    cells, rows = {}, []
    import gate1
    for m in MOMENTS:
        a, p = _bare_kit(m)
        r = LABEL[m]
        rows.append(r)
        near, _ = gate1.arm_boxes(a, p)
        fist_c = p["rod"]["center"]
        sh = gate1.tip(near, fist_c)
        cen = ((fist_c[0] + sh[0]) / 2, (fist_c[1] + sh[1]) / 2)
        half = max(abs(fist_c[0] - sh[0]), abs(fist_c[1] - sh[1])) / 2 + 70
        for c, worn in zip(cols, ([], ["scale_mail"], ["leather_armor"])):
            im = crop3x(dressed(m, worn), cen, half, round(2 * half * 0.72))
            im.thumbnail((380, 380))
            cc = Image.new("RGBA", (380, 380), PAPER + (255,))
            cc.paste(im, ((380 - im.width) // 2, (380 - im.height) // 2))
            cells[(r, c)] = cc
    grid(rows, cols, cells, "Near arm, shoulder to wrist, at 3x (shrunk to fit where the arm is longer)", 380).save(
        TESTS / "elbows_3x.png")


def sheet_mixes():
    strip({m: dressed(m, MIX1) for m in MOMENTS},
          "Mix 1: Scale Mail, Leather Cap, Scale Gauntlets, Leather Leggings, Scale Boots").save(TESTS / "mix1_strip.png")
    strip({m: dressed(m, MIX2) for m in MOMENTS},
          "Mix 2: Leather Armor, Scale Coif, Leather Gloves, Scale Leggings, Leather Boots").save(TESTS / "mix2_strip.png")


def sheet_seams():
    """The worst spots, listed in art/redo/moments/seams.json: [[mix, moment, x, y, note], ...] (canvas px)."""
    spots = json.loads((REDO / "seams.json").read_text())
    tiles = []
    for mix, m, x, y, note in spots:
        im = crop3x(dressed(m, MIX1 if mix == 1 else MIX2), (x, y), 150 / 0.72, 300).convert("RGB")
        ImageDraw.Draw(im).text((6, 270), f"mix {mix}, {LABEL[m]}: {note}", fill=(20, 10, 0), font=font(15))
        tiles.append(im)
    cols = 5
    rows = (len(tiles) + cols - 1) // cols
    s = Image.new("RGB", (cols * 305, 50 + rows * 305), PAPER)
    ImageDraw.Draw(s).text((10, 10), "Worst spots found, at 3x", fill=(40, 30, 20), font=font(22))
    for i, t in enumerate(tiles):
        s.paste(t, ((i % cols) * 305, 50 + (i // cols) * 305))
    fit(s).save(TESTS / "seams_3x.png")


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    for k, f in {"sleeves": sheet_sleeves, "beside": sheet_beside, "bare": sheet_bare, "hands": sheet_hands,
                 "heads": sheet_heads, "hands_full": sheet_hands_full, "elbows": sheet_elbows, "mixes": sheet_mixes, "seams": sheet_seams}.items():
        if what in (k, "all"):
            f()
