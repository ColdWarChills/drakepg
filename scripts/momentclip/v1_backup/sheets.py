"""Pictures to judge the clip without playing it: frame sheets, the four key instants, outfit stills, timing.md.
Every picture <= 1568 px on its long side and <= 1.1 megapixels."""
from PIL import Image, ImageDraw, ImageFont

import timeline as T
from common import FONTS, FPS, OUT, PLAYER_X, PX, caption
from scene import W_PX, H_PX

CROP = (100, 150, 1160, 535)  # design px: the action, the wind-up's sword tip to the monster


def _font(n, bold=True):
    return ImageFont.truetype(str(FONTS / ("AlegreyaSans-Bold.ttf" if bold else "AlegreyaSans-Medium.ttf")), n)


def _fit(im):
    im.thumbnail((1568, 1568), Image.LANCZOS)
    while im.width * im.height > 1_100_000:
        im = im.resize((int(im.width * 0.97), int(im.height * 0.97)), Image.LANCZOS)
    return im


def _phase(f):
    name = "idle"
    for p in T.PHASES:
        if p[1] is not None and f >= p[1]:
            name = p[0]
    return name


def picks():
    out = []
    for f in range(T.LENGTH):
        if T.HIT - 6 <= f <= T.HIT + 10 or f % 3 == 0 or f == T.LENGTH - 1:
            out.append(f)
    return out


def make(stage, outfit):
    frames = {}
    for f in range(T.LENGTH):
        img, info = stage.frame(outfit, f, 0, f)
        frames[f] = (img, info)
    # frame sheets: 3 x 3 cells, every frame from 6 before the hit to 10 after it, every 3rd elsewhere
    import textwrap
    cw = 500
    x0, y0, x1, y1 = (v * PX for v in CROP)
    ch = round(cw * (y1 - y0) / (x1 - x0))
    lab = 52
    cols, rows = 3, 3
    sel = picks()
    f1, f2 = _font(15), _font(12, False)
    pages = [sel[i:i + cols * rows] for i in range(0, len(sel), cols * rows)]
    for old in OUT.glob("frames_*.png"):
        old.unlink()
    for pi, page in enumerate(pages, 1):
        sheet = Image.new("RGB", (cw * cols, (ch + lab) * rows), (24, 20, 30))
        d = ImageDraw.Draw(sheet)
        for i, f in enumerate(page):
            img, info = frames[f]
            cell = img.crop((x0, y0, x1, y1)).resize((cw, ch), Image.LANCZOS)
            cx, cy = (i % cols) * cw, (i // cols) * (ch + lab)
            sheet.paste(cell, (cx, cy + lab))
            hit = "  << HIT" if f == T.HIT else ""
            d.text((cx + 4, cy + 1), f"f{f}  {round(f * 1000 / FPS)} ms  {info['moment']}  ({_phase(f)}){hit}",
                   fill=(255, 220, 120) if T.HIT <= f < T.AFTER else (240, 236, 226), font=f1)
            eff = ", ".join(info["effects"]) or "-"
            for li, line in enumerate(textwrap.wrap(eff, 88)[:2]):
                d.text((cx + 4, cy + 19 + li * 15), line, fill=(190, 200, 210), font=f2)
            d.line([(cx + cw - 1, cy), (cx + cw - 1, cy + ch + lab)], fill=(60, 55, 70))
        p = OUT / f"frames_{pi}.png"
        _fit(sheet).save(p)
        print(p)
    swaps(stage, outfit, frames)
    # key instants, whole frames
    keys = [(T.HIT - T.DASH - 1, "wind-up held"), (T.HIT, "the hit frame"), (T.FOLLOW_AT + 7, "follow-through"),
            (T.LENGTH - 1, "back at ready")]
    kw, kh = 696, 392
    sheet = Image.new("RGB", (kw * 2, kh * 2), (0, 0, 0))
    d = ImageDraw.Draw(sheet)
    for i, (f, name) in enumerate(keys):
        cell = frames[f][0].resize((kw, kh), Image.LANCZOS)
        sheet.paste(cell, ((i % 2) * kw, (i // 2) * kh))
        d.text(((i % 2) * kw + 8, (i // 2) * kh + 6), f"{name}: f{f}, {round(f * 1000 / FPS)} ms",
               fill=(255, 255, 255), font=_font(20), stroke_width=2, stroke_fill=(0, 0, 0))
    p = OUT / "key_full.png"
    _fit(sheet).save(p)
    print(p, sheet.size)
    timing_md(stage)


def swaps(stage, outfit, frames):
    """Each swap between two drawings, the frame before beside the frame of the swap, at a larger size: the ground
    line (red) and, where a foot stays planted, a blue mark on that foot's x."""
    att = stage.att
    pairs = [
        (-1, 0, "ready -> wind-up: back foot held", PLAYER_X + att.foot("ready", "back"), PLAYER_X),
        (T.HIT - 1, T.HIT, "wind-up (in the air) -> strike: lands on the hit", stage.x_strike, stage.x_strike - 60),
        (T.FOLLOW_AT - 1, T.FOLLOW_AT, "strike -> follow-through: front foot held", stage.x_strike, stage.x_strike),
        (T.RECOVER_AT - 1, T.RECOVER_AT, "follow-through -> ready: swap in the air", None, stage.x_strike - 40),
    ]
    cw, chh, lab = 380, 300, 26
    sheet = Image.new("RGB", (cw * 2, (chh + lab) * len(pairs)), (24, 20, 30))
    d = ImageDraw.Draw(sheet)
    for r, (fa, fb, name, mark, cx) in enumerate(pairs):
        d.text((6, r * (chh + lab) + 4), name, fill=(240, 236, 226), font=_font(17))
        for c, f in enumerate((fa, fb)):
            img = frames[f][0] if f >= 0 else stage.frame(outfit, None, 0, 0)[0]
            bx0, by0 = (cx - 190) * PX, (520 - 300) * PX
            cell = img.crop((bx0, by0, bx0 + 380 * PX, by0 + 300 * PX)).resize((cw, chh), Image.LANCZOS)
            cd = ImageDraw.Draw(cell)
            gy = (T.GROUND_Y - 220) * chh / 300
            cd.line([(0, gy), (cw, gy)], fill=(230, 40, 40), width=1)
            if mark is not None:
                mx = (mark - (cx - 190)) * cw / 380
                cd.line([(mx, gy - 40), (mx, gy + 12)], fill=(40, 90, 255), width=2)
            cd.text((4, chh - 18), f"f{f}", fill=(255, 255, 0), font=_font(14))
            sheet.paste(cell, (c * cw, r * (chh + lab) + lab))
    p = OUT / "swaps.png"
    _fit(sheet).save(p)
    print(p, sheet.size)


def stills(stage, outfits):
    """Ready and strike per outfit from the renderer, cropped around the hero."""
    cw, chh = 560, 420
    lab = 30
    sheet = Image.new("RGB", (cw * 2, (chh + lab) * len(outfits)), (24, 20, 30))
    d = ImageDraw.Draw(sheet)
    for r, (name, pieces) in enumerate(outfits):
        for c, (f, x) in enumerate(((None, 330), (T.AFTER - 1, stage.x_strike + 20))):
            img, _ = stage.frame(pieces, f, 0, 0, effects=False)
            w, h = 400 * PX, 300 * PX
            cx = int(x * PX - w / 2)
            box = (cx, (520 - 300) * PX, cx + w, 520 * PX)
            sheet.paste(img.crop(box).resize((cw, chh), Image.LANCZOS), (c * cw, r * (chh + lab) + lab))
        d.text((6, r * (chh + lab) + 4), f"{name}: {caption(pieces)}  (ready | strike)", fill=(240, 236, 226),
               font=_font(18))
    p = OUT / "outfit_stills.png"
    _fit(sheet).save(p)
    print(p, sheet.size)


def timing_md(stage):
    att = stage.att
    lines = ["# The sword attack: timing (60 fps)", "",
             f"Generated by `scripts/momentclip/clip.py --sheets` from the tables in `timeline.py`. Design px "
             f"(1280x720; x right, y down; ground y {T.GROUND_Y}). The hero's home x {att.home}; the strike stands "
             f"at x {stage.x_strike:.0f} (its front foot), solved so the slash arc's band runs through the "
             f"monster's middle; the hit shows at ({stage.hit[0]:.0f}, {stage.hit[1]:.0f}). Whole attack: "
             f"{T.LENGTH} frames = {T.LENGTH / FPS * 1000:.0f} ms.", "",
             "## Phases (the hero)", "",
             "| phase | frames | ms | moment shown | where: figure x (its anchor foot/feet), feet y at the top of a hop | what moves | Unity |",
             "|---|---|---|---|---|---|---|"]
    for name, start, n, m, what, unity in T.PHASES:
        if start is None:
            lines.append(f"| {name} | - | - | {m} | x {att.home}, y {T.GROUND_Y} | {what} | {unity} |")
            continue
        xs = [att.hero(f)[1] for f in range(start, start + n)]
        ys = [att.hero(f)[2] for f in range(start, start + n)]
        lines.append(f"| {name} | {start}-{start + n - 1} ({n}) | {round(start * 1000 / FPS)}-"
                     f"{round((start + n) * 1000 / FPS)} | {m} | x {xs[0]:.0f} -> {xs[-1]:.0f}, y min "
                     f"{min(ys):.0f} | {what} | {unity} |")
    lines += ["", "## Effects", "", "| effect | starts (frame) | lasts (frames) | what it does | Unity |",
              "|---|---|---|---|---|"]
    for name, start, n, what, unity in T.EFFECTS:
        lines.append(f"| {name} | {start} ({round(start * 1000 / FPS)} ms) | {n} | {what} | {unity} |")
    lines += ["", "During the freeze (frames %d-%d) every tween holds, effects included (the dash streaks, ghosts and dust too), as Fx.HitStop does, except the monster white flash, which runs on real time; they "
              "resume on frame %d." % (T.HIT, T.AFTER - 1, T.AFTER), ""]
    p = OUT / "timing.md"
    p.write_text("\n".join(lines), encoding="utf-8")
    print(p)
