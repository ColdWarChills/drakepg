"""Step A4 look files (art/tests/moments/), everything stacked from the built layers folder (layers/manifest.json) in
the clip renderer's order (scripts/momentclip/layers.py): weapon (back), body, shirt (unless a chest piece), hair
(unless a helm), legs, boots, chest, gloves_far, weapon (front), fist (unless a near glove hides it), gloves_near,
helm. Each picture at most 1568 px on its long side and 1.1 megapixels.

<venv python> -I -B look.py [strips|game|pieces|heads|gloves|seams|all]
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, RAW, REDO, TESTS, load_rgba  # noqa: E402
import cut  # noqa: E402
from sheets import PAPER, font  # noqa: E402

LAY = REDO / "layers"
MOMENTS = ["ready", "wind", "strike"]
LABEL = {"ready": "ready", "wind": "wind-up", "strike": "strike"}
MIX1 = ["scale_mail", "leather_cap", "scale_gauntlets", "leather_leggings", "scale_boots"]
MIX2 = ["leather_armor", "scale_coif", "leather_gloves", "scale_leggings", "leather_boots"]
MIXES = {"bare": [], "mix1": MIX1, "mix2": MIX2}
GAME = 480 / HERO_H      # drawing px -> designer's size (him 480 px tall on 2560x1440)
PAD = 520                # the sword reaches past the 1536 canvas
INK = (40, 30, 20)


def man():
    return json.loads((LAY / "manifest.json").read_text())


def limit(im: Image.Image) -> Image.Image:
    k = min(1.0, 1568 / max(im.size), (1.1e6 / (im.width * im.height)) ** 0.5)
    if k < 1:
        im = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)
    return im


def sprites(m: str, worn: list, pad: int = 0) -> list:
    """[(slot, RGBA)] bottom to top, as the clip renderer stacks them; the weapon redrawn on the padded canvas."""
    import bare
    mm = man()["moments"][m]
    p = json.loads((REDO / "moments.json").read_text())
    d = LAY / m
    padit = (lambda x: np.pad(x, ((pad, pad), (pad, pad), (0, 0)))) if pad else (lambda x: x)
    worn_l = {}
    hides = False
    for piece in worn:
        for slot, info in mm["pieces"][piece].items():
            if info["box"] is None:
                continue
            worn_l[slot] = padit(load_rgba(d / info["file"]))
            hides |= slot == "gloves_near" and bool(info.get("hides_fist"))
    if pad:
        a = load_rgba(REDO / f"{p[m]}.png")
        rod = dict(bare.read_blade(a, p[m]))
        rod["center"] = (rod["center"][0] + pad, rod["center"][1] + pad)
        wpn = cut.weapon_layer((a.shape[0] + 2 * pad, a.shape[1] + 2 * pad, 4), rod)
    else:
        wpn = load_rgba(d / "weapon.png")
    back = mm["weapon_order"] == "back"
    out = []
    for slot in ["weapon_back", "body", "shirt", "hair", "legs", "boots", "chest", "gloves_far", "weapon_front", "fist",
                 "gloves_near", "helm"]:
        if slot == "weapon_back":
            if back:
                out.append((slot, wpn))
        elif slot == "weapon_front":
            if not back:
                out.append((slot, wpn))
        elif slot in ("body", "shirt", "hair", "fist"):
            if (slot == "shirt" and "chest" in worn_l) or (slot == "hair" and "helm" in worn_l) or \
                    (slot == "fist" and hides):
                continue
            out.append((slot, padit(load_rgba(d / f"{slot}.png"))))
        elif slot in worn_l:
            out.append((slot, worn_l[slot]))
    return out


def stacked(m: str, worn: list, pad: int = 0) -> np.ndarray:
    out = None
    for _, a in sprites(m, worn, pad):
        out = a.copy() if out is None else cut.over(out, a)
    return out


def road(w: int, h: int, x0: int = 0) -> Image.Image:
    bg = Image.open(RAW / "bg_fen_road.png").convert("RGB").resize((2560, 1440), Image.LANCZOS)
    return bg.crop((x0, 1020 - 560, x0 + w, 1020 - 560 + h))


def game_fig(m: str, worn: list):
    """(image at the designer's size, soles y in it)."""
    a = stacked(m, worn, PAD)
    soles = man()["moments"][m]["soles_y"] + PAD
    ys, xs = np.nonzero(a[..., 3] > 20)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    im = Image.fromarray(a).crop((x0, y0, x1, y1))
    im = im.convert("RGBa").resize((max(1, round(im.width * GAME)), max(1, round(im.height * GAME))),
                                   Image.LANCZOS).convert("RGBA")
    return im, round((soles + 1 - y0) * GAME)


def row_on_road(figs: list, title: str, gap: int = 30, x0: int = 0) -> Image.Image:
    """figs: [(label, image, soles)], one ground line at y 560 of a 640 px road."""
    ground, H = 560, 640
    W = sum(f[1].width for f in figs) + gap * (len(figs) + 1)
    sheet = Image.new("RGB", (W, 40 + H), PAPER)
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), title, fill=INK, font=font(22))
    sheet.paste(road(W, H, x0), (0, 40))
    x = gap
    for lab, im, so in figs:
        sheet.paste(im, (x, 40 + ground - so), im)
        d.text((x, 46), lab, fill=(255, 250, 240), font=font(22))
        x += im.width + gap
    return sheet


def sheet_strips():
    for mix in ("mix1", "mix2"):
        worn = MIXES[mix]
        name = "Mix 1" if mix == "mix1" else "Mix 2"
        figs = {m: game_fig(m, worn) for m in MOMENTS}
        s1 = row_on_road([(LABEL[m], *figs[m]) for m in ("ready", "wind")],
                         f"{name}: ready and wind-up (him 480 px tall, real training sword)")
        s2 = row_on_road([(LABEL["strike"], *figs["strike"])], f"{name}: strike (him 480 px tall)", x0=700)
        for k, s in ((1, s1), (2, s2)):
            s = limit(s)
            s.save(TESTS / f"{mix}_strip_{k}.png")
            print(f"{mix}_strip_{k}.png", s.size)


def sheet_game():
    """The three moments side by side at exactly the designer's size (nothing enlarged). Too wide for 1568 px with the
    swords' reach, so neighbours overlap only where nothing is drawn (the figures' boxes may touch, not cross)."""
    for mix, worn in MIXES.items():
        figs = [(LABEL[m], *game_fig(m, worn)) for m in MOMENTS]
        name = "Bare" if mix == "bare" else "Mix 1" if mix == "mix1" else "Mix 2"
        # placed as close as their pixels allow, never scaled; three are wider than 1568 px with the swords' reach,
        # so the sheet is split in two files that share the wind-up (ready + wind-up, wind-up + strike)
        s = pack(figs, f"{name}: ready, wind-up, strike at the designer's size (him 480 px tall), nothing enlarged")
        if max(s.size) <= 1568 and s.width * s.height <= 1.1e6:
            s.save(TESTS / f"mix_game_{mix}.png")
            print(f"mix_game_{mix}.png", s.size)
            continue
        for k, pair in ((1, figs[:2]), (2, figs[1:])):
            s = pack(pair, f"{name}: {pair[0][0]} and {pair[1][0]} at the designer's size (him 480 px tall), "
                           f"nothing enlarged")
            assert max(s.size) <= 1568 and s.width * s.height <= 1.1e6, s.size
            s.save(TESTS / f"mix_game_{mix}_{k}.png")
            print(f"mix_game_{mix}_{k}.png", s.size)


def pack(figs, title):
    ground, H = 560, 640
    xs = [0]
    for (_, a, sa), (_, b, sb) in zip(figs, figs[1:]):
        # the smallest shift so that no row has both figures' opaque pixels closer than 12 px
        A = np.asarray(a)[..., 3] > 20
        B = np.asarray(b)[..., 3] > 20
        best = a.width + 10
        for shift in range(a.width + 10, 0, -4):
            ok = True
            for y in range(0, H, 2):
                ya, yb = y - (ground - sa), y - (ground - sb)
                if 0 <= ya < A.shape[0] and 0 <= yb < B.shape[0]:
                    ra, rb = np.nonzero(A[ya])[0], np.nonzero(B[yb])[0]
                    if len(ra) and len(rb) and ra.max() + 12 > rb.min() + shift:
                        ok = False
                        break
            if not ok:
                break
            best = shift
        xs.append(xs[-1] + best)
    W = xs[-1] + figs[-1][1].width + 20
    sheet = Image.new("RGB", (W, 40 + H), PAPER)
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), title, fill=INK, font=font(20))
    sheet.paste(road(W, H), (0, 40))
    for x, (lab, im, so) in zip(xs, figs):
        sheet.paste(im, (x + 10, 40 + ground - so), im)
        d.text((x + 10, 46), lab, fill=(255, 250, 240), font=font(20))
    return sheet


# ---- close-ups ----------------------------------------------------------------------------------------------------
def piece_box(m: str, piece: str, grow: int = 30):
    mm = man()["moments"][m]["pieces"][piece]
    bs = [v["box"] for v in mm.values() if v["box"]]
    x0, y0 = min(b[0] for b in bs) - grow, min(b[1] for b in bs) - grow
    x1, y1 = max(b[2] for b in bs) + grow, max(b[3] for b in bs) + grow
    return x0, y0, x1, y1


def crop_at(a: np.ndarray, box, k: float, ground=(150, 150, 150)) -> Image.Image:
    im = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), ground + (255,))
    im.alpha_composite(Image.fromarray(a).crop(box))
    return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)


def numbers(piece: str, m: str) -> str:
    ch = json.loads((REDO / "checks.json").read_text()).get(f"{piece}@{m}", {})
    mt = ch.get("metrics", {})
    s = f"{ch.get('edit', '?')}\nsteel {mt.get('steel_share')}  steel L {mt.get('steel_L', [None] * 3)[1] if mt.get('steel_L') else None}"
    if mt.get("other_Lab"):
        s += f"\nleather/cloth Lab {mt['other_Lab']}"
    if "plate_px" in mt:
        s += f"\nshoulder plate px {mt['plate_px']}"
    vr = ch.get("vs_ready")
    if vr:
        s += f"\nvs ready: {vr['verdict']}"
    extra = [f"{k} {ch[k]}" for k in ("keep", "face_changed", "far_box", "new_holes_px", "specks") if k in ch]
    if extra:
        s += "\n" + "  ".join(extra)
    return s


def sheet_pieces(only=None):
    k = 3 * GAME
    for piece in MIX1 + MIX2:
        if only and piece not in only:
            continue
        if not all(piece in man()["moments"][m]["pieces"] for m in MOMENTS):
            print("skip", piece, "(not on every moment)")
            continue
        tiles = []
        for m in MOMENTS:
            a = stacked(m, [piece])
            tiles.append((m, crop_at(a, piece_box(m, piece), k), numbers(piece, m)))
        th = 150
        Wd = sum(t[1].width for t in tiles) + 20 * 4
        Hd = max(t[1].height for t in tiles) + th + 40
        s = Image.new("RGB", (Wd, Hd), PAPER)
        d = ImageDraw.Draw(s)
        d.text((10, 8), f"{piece}: only this piece on, the bare hero under it, 3x the designer's size",
               fill=INK, font=font(20))
        x = 20
        for m, im, txt in tiles:
            s.paste(im.convert("RGB"), (x, 40))
            d.text((x, 40 + im.height + 6), LABEL[m] + "  " + txt, fill=INK, font=font(14))
            x += im.width + 20
        s = limit(s)
        s.save(TESTS / f"pieces_{piece}.png")
        print(f"pieces_{piece}.png", s.size)


def head_box(m: str, grow: int = 50):
    import place
    a = load_rgba(LAY / m / "hair.png")
    ys, xs = np.nonzero(a[..., 3] > 40)
    return xs.min() - grow, ys.min() - grow, xs.max() + grow, ys.max() + grow


def sheet_heads():
    k = 3 * GAME
    rows = []
    for m in MOMENTS:  # one row per moment: bare head | Leather Cap | Scale Coif, the same box round the face
        bs = [piece_box(m, h, 20) for h in ("leather_cap", "scale_coif")]
        box = (min(b[0] for b in bs), min(b[1] for b in bs), max(b[2] for b in bs), max(b[3] for b in bs))
        rows.append((LABEL[m], [crop_at(stacked(m, w), box, k, PAPER) for w in ([], ["leather_cap"], ["scale_coif"])]))
    cw = max(t.width for _, ts in rows for t in ts)
    chh = max(t.height for _, ts in rows for t in ts)
    s = Image.new("RGB", (3 * (cw + 10), 40 + 3 * (chh + 26)), PAPER)
    d = ImageDraw.Draw(s)
    d.text((10, 8), "Helms at 3x the designer's size: bare head | Leather Cap | Scale Coif", fill=INK, font=font(20))
    for r, (lab, ts) in enumerate(rows):
        y = 40 + r * (chh + 26)
        for c, (t, name) in enumerate(zip(ts, ("bare", "Leather Cap", "Scale Coif"))):
            d.text((c * (cw + 10) + 4, y), f"{lab}: {name}", fill=INK, font=font(16))
            s.paste(t.convert("RGB"), (c * (cw + 10), y + 22))
    s = limit(s)
    s.save(TESTS / "heads_3x.png")
    print("heads_3x.png", s.size)


def sheet_gloves():
    k = 4 * GAME
    files = []
    for glove in ("scale_gauntlets", "leather_gloves"):
        if not all(glove in man()["moments"][m]["pieces"] for m in MOMENTS):
            print("skip", glove)
            continue
        tiles = []
        for m in MOMENTS:
            mm = man()["moments"][m]["pieces"].get(glove, {})
            a = stacked(m, [glove], PAD)
            for side in ("gloves_near", "gloves_far"):
                info = mm.get(side)
                if not info or not info["box"]:
                    tiles.append((f"{LABEL[m]} {'sword hand' if side == 'gloves_near' else 'other hand'}: NO LAYER", None))
                    continue
                b = info["box"]
                cx, cy = (b[0] + b[2]) / 2 + PAD, (b[1] + b[3]) / 2 + PAD
                if side == "gloves_near":  # the sword fist itself: round the weapon's grip point
                    gx, gy = man()["moments"][m]["weapon"]["grip_xy"]
                    cx, cy = gx + PAD, gy + PAD
                half = 100
                box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
                tiles.append((f"{LABEL[m]} {'sword hand (his right)' if side == 'gloves_near' else 'other hand (his left)'}",
                              crop_at(a, box, k, PAPER)))
        cell = round(200 * k)
        s = Image.new("RGB", (3 * (cell + 10), 40 + 2 * (cell + 26)), PAPER)
        d = ImageDraw.Draw(s)
        d.text((10, 8), f"{glove} at 4x the designer's size, body and sword in", fill=INK, font=font(20))
        for i, (lab, im) in enumerate(tiles):
            mi, side = divmod(i, 2)
            x, y = mi * (cell + 10), 40 + side * (cell + 26)
            d.text((x + 2, y), lab, fill=INK, font=font(15))
            if im is not None:
                s.paste(im.convert("RGB"), (x, y + 22))
        s = limit(s)
        files.append(s)
    for i, s in enumerate(files, 1):
        s.save(TESTS / f"gloves_4x_{i}.png")
        print(f"gloves_4x_{i}.png", s.size)


def sheet_seams():
    """Where layers meet, both mixes, 3x: neck (chest/helm), belt (chest/legs), wrist (glove/chest or arm), boot top."""
    k = 3 * GAME
    tiles = []
    for mix in ("mix1", "mix2"):
        worn = MIXES[mix]
        for m in MOMENTS:
            a = stacked(m, worn)
            mm = man()["moments"][m]["pieces"]
            chest = worn[0]
            cb = piece_box(m, chest, 0)
            hb = head_box(m, 0)
            lb = piece_box(m, worn[3], 0)
            bb = piece_box(m, worn[4], 0)
            spots = {"neck": ((cb[0] + cb[2]) / 2, cb[1] + 20),
                     "belt": ((lb[0] + lb[2]) / 2, lb[1] + 10),
                     "boot top": ((bb[0] + bb[2]) / 2, bb[1] + 20)}
            gfar = mm[worn[2]].get("gloves_far", {}).get("box")
            if gfar:
                spots["wrist (other hand)"] = ((gfar[0] + gfar[2]) / 2, (gfar[1] + gfar[3]) / 2)
            for name, (cx, cy) in spots.items():
                box = (int(cx - 60), int(cy - 60), int(cx + 60), int(cy + 60))
                tiles.append((f"{mix} {LABEL[m]} {name}", crop_at(a, box, k, PAPER)))
    cell = round(120 * k)
    cols = 8
    rows = (len(tiles) + cols - 1) // cols
    s = Image.new("RGB", (cols * (cell + 6), 40 + rows * (cell + 22)), PAPER)
    d = ImageDraw.Draw(s)
    d.text((10, 8), "Seams at 3x: neck, belt, boot top, other-hand wrist; Mix 1 then Mix 2", fill=INK, font=font(20))
    for i, (lab, im) in enumerate(tiles):
        r, c = divmod(i, cols)
        x, y = c * (cell + 6), 40 + r * (cell + 22)
        d.text((x + 2, y), lab, fill=INK, font=font(11))
        s.paste(im.convert("RGB"), (x, y + 18))
    s = limit(s)
    s.save(TESTS / "seams_3x.png")
    print("seams_3x.png", s.size)


def run_stats():
    """Step A4's Qwen runs (files <piece>_a4@...) from runs.jsonl: count, seconds, per piece and moment."""
    rows = [json.loads(x) for x in (REDO / "runs.jsonl").read_text().splitlines() if x.strip()]
    a4 = [r for r in rows if "_a4@" in r["file"] or "_a4fix@" in r["file"]]
    per = {}
    for r in a4:
        k = r["file"].rsplit("_s", 1)[0]
        per.setdefault(k, []).append(r["file"])
    sec = sum(r["sec"] for r in a4)
    print(f"A4 runs {len(a4)}, {sec} s = {sec / 60:.1f} min, mean {sec / max(1, len(a4)):.0f} s")
    for k, v in sorted(per.items()):
        print(" ", k, len(v), [f.rsplit('_s', 1)[1][:-4] for f in v])
    return a4, per


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what == "stats":
        run_stats()
    if what in ("strips", "all"):
        sheet_strips()
    if what in ("game", "all"):
        sheet_game()
    if what in ("pieces", "all"):
        sheet_pieces(sys.argv[2:] or None)
    if what in ("heads", "all"):
        sheet_heads()
    if what in ("gloves", "all"):
        sheet_gloves()
    if what in ("seams", "all"):
        sheet_seams()
