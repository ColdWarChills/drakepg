"""Pictures to judge the clip without playing it: frame sheets, key instants, native-size crops (look/), the cut's
geometry (cut_debug.png), outfit stills, timing.md. Every sheet <= 1568 px on its long side and <= 1.1 megapixels;
the look/ crops are 1200x800 cut from the 2560x1440 frame, not scaled. Everything goes to common.OUT."""
import math
import textwrap

from PIL import Image, ImageDraw, ImageFont

import common
import timeline as T
from common import FONTS, FPS, GROUND_Y, PLAYER_X, PX, caption

CROP = (100, 120, 1160, 535)  # design px: the action, the wind-up's sword tip to the monster, the cut's top


def _font(n, bold=True):
    return ImageFont.truetype(str(FONTS / ("AlegreyaSans-Bold.ttf" if bold else "AlegreyaSans-Medium.ttf")), n)


def _fit(im):
    im.thumbnail((1568, 1568), Image.LANCZOS)
    while im.width * im.height > 1_100_000:
        im = im.resize((int(im.width * 0.97), int(im.height * 0.97)), Image.LANCZOS)
    return im


def _phase(b, f):
    name = "idle"
    for p in b.PHASES:
        if p[1] is not None and f >= p[1]:
            name = p[0]
    return name


def picks(b):
    out = []
    for f in range(b.LENGTH):
        if (b.HIT - 6 <= f <= b.HIT + 10 or b.SWAP_AT - 2 <= f <= b.SWAP_AT + 1 or f % 3 == 0
                or f == b.LENGTH - 1):
            out.append(f)
    return out


def make(stage, outfit):
    b = stage.b
    v = b.moments
    out = common.OUT
    frames = {}
    for f in range(b.LENGTH):
        frames[f] = stage.frame(outfit, f, 0, f)
    # frame sheets: 3 x 3 cells
    cw = 500
    x0, y0, x1, y1 = (q * PX for q in CROP)
    ch = round(cw * (y1 - y0) / (x1 - x0))
    lab = 52
    cols, rows = 3, 3
    sel = picks(b)
    f1, f2 = _font(15), _font(12, False)
    pages = [sel[i:i + cols * rows] for i in range(0, len(sel), cols * rows)]
    for old in out.glob(f"frames{v}_*.png"):
        old.unlink()
    for pi, page in enumerate(pages, 1):
        sheet = Image.new("RGB", (cw * cols, (ch + lab) * rows), (24, 20, 30))
        d = ImageDraw.Draw(sheet)
        for i, f in enumerate(page):
            img, info = frames[f]
            cell = img.crop((x0, y0, x1, y1)).resize((cw, ch), Image.LANCZOS)
            cx, cy = (i % cols) * cw, (i // cols) * (ch + lab)
            sheet.paste(cell, (cx, cy + lab))
            mark = "  << HIT" if f == b.HIT else ("  << SWAP" if f == b.SWAP_AT else "")
            d.text((cx + 4, cy + 1), f"f{f}  {round(f * 1000 / FPS)} ms  {info['moment']}  ({_phase(b, f)}){mark}",
                   fill=(255, 220, 120) if b.HIT <= f < b.AFTER else (240, 236, 226), font=f1)
            eff = ", ".join(info["effects"]) or "-"
            for li, line in enumerate(textwrap.wrap(eff, 88)[:2]):
                d.text((cx + 4, cy + 19 + li * 15), line, fill=(190, 200, 210), font=f2)
            d.line([(cx + cw - 1, cy), (cx + cw - 1, cy + ch + lab)], fill=(60, 55, 70))
        p = out / f"frames{v}_{pi}.png"
        _fit(sheet).save(p)
        print(p)
    swaps(stage, outfit, frames)
    # key instants, whole frames
    held = (b.BACKOFF_AT - 1, "strike, end of its hold") if v == 3 else (b.FOLLOW_AT + 7, "follow-through held")
    keys = [(b.DASH_AT - 1, "wind-up, end of the crouch"), (b.HIT, "the hit frame"), held,
            (b.LENGTH - 1, "back at ready")]
    kw, kh = 696, 392
    sheet = Image.new("RGB", (kw * 2, kh * 2), (0, 0, 0))
    d = ImageDraw.Draw(sheet)
    for i, (f, name) in enumerate(keys):
        cell = frames[f][0].resize((kw, kh), Image.LANCZOS)
        sheet.paste(cell, ((i % 2) * kw, (i // 2) * kh))
        d.text(((i % 2) * kw + 8, (i // 2) * kh + 6), f"{name}: f{f}, {round(f * 1000 / FPS)} ms",
               fill=(255, 255, 255), font=_font(20), stroke_width=2, stroke_fill=(0, 0, 0))
    p = out / f"key{v}_full.png"
    _fit(sheet).save(p)
    print(p, sheet.size)
    cut_debug(stage, frames[b.HIT][0])
    timing_md(stage)


def swaps(stage, outfit, frames):
    """Each swap between two drawings, the frame before beside the frame of the swap, at a larger size: the ground
    line (red) and, where a foot stays planted, a blue mark on that foot's x."""
    att, b = stage.att, stage.b
    pairs = [
        (-1, 0, "ready -> wind-up: back foot held", PLAYER_X + att.foot("ready", "back"), PLAYER_X),
        (b.DASH_AT - 1, b.DASH_AT, "crouch -> dash: back foot held, then off", PLAYER_X + att.foot("ready", "back"),
         PLAYER_X + 40),
        (b.HIT - 1, b.HIT, "wind-up (in the air) -> strike: lands on the hit", stage.x_strike, stage.x_strike - 60),
    ]
    if b.moments == 4:
        pairs.append((b.FOLLOW_AT - 1, b.FOLLOW_AT, "strike -> follow-through: front foot held", stage.x_strike,
                      stage.x_strike))
    pairs.append((b.SWAP_AT - 1, b.SWAP_AT, f"{b.last} (backing off) -> ready: swap in the air", None,
                  stage.x_strike - 140))
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
            gy = (GROUND_Y - 220) * chh / 300
            cd.line([(0, gy), (cw, gy)], fill=(230, 40, 40), width=1)
            if mark is not None:
                mx = (mark - (cx - 190)) * cw / 380
                cd.line([(mx, gy - 40), (mx, gy + 12)], fill=(40, 90, 255), width=2)
            cd.text((4, chh - 18), f"f{f}", fill=(255, 255, 0), font=_font(14))
            sheet.paste(cell, (c * cw, r * (chh + lab) + lab))
    p = common.OUT / f"swaps{b.moments}.png"
    _fit(sheet).save(p)
    print(p, sheet.size)


def cut_debug(stage, hit_img):
    """The hit frame with the cut's circle, its centre, the wind-up's blade tip on the last dash frame, the top of the
    arc and the strike's blade tip marked."""
    (cx, cy), r, a0, a1, W = stage.cut
    tip = stage.wind_tip()
    (_, _), (sx, sy) = stage.hero.blade("strike")
    st = (stage.x_strike + sx, GROUND_Y + sy)
    tail = (cx + r * math.sin(math.radians(a0)), cy - r * math.cos(math.radians(a0)))
    img = hit_img.copy()
    d = ImageDraw.Draw(img)
    kx, ky = stage.att.camera(stage.b.HIT)  # the hit frame is drawn with the camera kick: mark in screen px
    P = lambda x, y: ((x - kx) * PX, (y - ky) * PX)  # noqa: E731
    c = P(cx, cy)
    d.ellipse((c[0] - r * PX, c[1] - r * PX, c[0] + r * PX, c[1] + r * PX), outline=(255, 40, 200), width=3)
    d.ellipse((c[0] - (r - W) * PX, c[1] - (r - W) * PX, c[0] + (r - W) * PX, c[1] + (r - W) * PX),
              outline=(255, 40, 200), width=1)
    font = _font(26)
    for (x, y), col, name in (((cx, cy), (255, 40, 200), f"centre ({cx:.0f}, {cy:.0f})"),
                              ((cx, cy - r), (40, 220, 255), f"top of the arc y {cy - r:.0f}"),
                              (tip, (255, 80, 40), f"wind-up tip, last dash frame ({tip[0]:.0f}, {tip[1]:.0f})"),
                              (st, (60, 255, 90), f"strike blade tip ({st[0]:.0f}, {st[1]:.0f})"),
                              (tail, (255, 230, 40), f"cut's tail ({tail[0]:.0f}, {tail[1]:.0f})"),
                              (stage.hit, (255, 255, 255), f"the hit ({stage.hit[0]:.0f}, {stage.hit[1]:.0f})")):
        p = P(x, y)
        d.ellipse((p[0] - 9, p[1] - 9, p[0] + 9, p[1] + 9), outline=col, width=4)
        d.line([(p[0] - 16, p[1]), (p[0] + 16, p[1])], fill=col, width=2)
        d.line([(p[0], p[1] - 16), (p[0], p[1] + 16)], fill=col, width=2)
        d.text((p[0] + 14, p[1] + 8), name, fill=col, font=font, stroke_width=3, stroke_fill=(0, 0, 0))
    d.text((20, 20), stage.checks[3], fill=(255, 255, 255), font=_font(28), stroke_width=3, stroke_fill=(0, 0, 0))
    box = tuple(int(q) for q in (P(min(tip[0], cx - r) - 40, cy - r - 40) + P(cx + r + 160, GROUND_Y + 40)))
    box = (max(0, box[0]), max(0, box[1]), min(img.width, box[2]), min(img.height, box[3]))
    p = common.OUT / "cut_debug.png"
    _fit(img.crop(box)).save(p)
    print(p)


def look(stage, outfit):
    """Native-size crops (1200x800 px of the 2560x1440 frame) of the frames to judge, into OUT/look."""
    b = stage.b
    v = b.moments
    d = common.OUT / "look"
    d.mkdir(parents=True, exist_ok=True)
    for old in d.glob(f"a{v}_*.png"):
        old.unlink()
    cut_x = stage.cut[0][0] + 20  # the whole cut
    near = stage.x_strike - 50    # the hero at the monster, the monster's front half in view
    if v == 3:
        shots = [(b.CROUCH_AT - 1, "windup_end_of_hold", 330), (b.DASH_AT - 1, "crouch", 330),
                 (b.HIT - 1, "last_dash", 640), (b.HIT, "hit", cut_x), (b.HIT + 3, "freeze_middle", cut_x),
                 (b.AFTER + 1, "cut_wiping_1", cut_x), (b.AFTER + 3, "cut_wiping_2", cut_x),
                 (b.BACKOFF_AT - 1, "strike_end_of_hold", near), (b.BACKOFF_AT + 4, "backoff_middle", near),
                 (b.SWAP_AT - 1, "before_swap", near), (b.SWAP_AT, "swap", near), (b.SWAP_AT + 1, "after_swap", near),
                 (b.SWAP_AT + 7, "midhop", 520), (b.LAND_AT, "landing", 330)]
    else:
        shots = [(b.FOLLOW_AT + 7, "follow_held", near), (b.BACKOFF_AT + 4, "backoff_middle", near),
                 (b.SWAP_AT - 1, "before_swap", near), (b.SWAP_AT, "swap", near), (b.SWAP_AT + 7, "midhop", 520)]
    for f, name, x in shots:  # one window per stretch, so neighbouring crops compare without the frame moving
        img, info = stage.frame(outfit, f, 0, f)
        _crop(img, x, 330).save(d / f"a{v}_f{f:03d}_{name}.png")
    if v == 3:  # the feet on their shadows: at home (idle) and at the toad (the strike held), with and without
        for f, name, x, y in ((None, "home", PLAYER_X + 60, 420), (b.BACKOFF_AT - 1, "at_toad", 860, 420)):
            for sh in (True, False):
                img, _ = stage.frame(outfit, f, 0, f or 0, shadows=sh)
                tag = "f000_idle" if f is None else f"f{f:03d}"
                _crop(img, x, y).save(d / f"a3_{tag}_feet_{name}_{'shadow' if sh else 'no_shadow'}.png")
    print(d, len(list(d.glob(f"a{v}_*.png"))), "crops")


def _crop(img, x, y):
    """A 1200x800 px window centred on design (x, y), kept inside the frame."""
    cx = min(max(round(x * PX) - 600, 0), img.width - 1200)
    cy = min(max(round(y * PX) - 400, 0), img.height - 800)
    return img.crop((cx, cy, cx + 1200, cy + 800))


def stills(stage, outfits):
    """Ready and strike per outfit from the renderer, cropped around the hero."""
    cw, chh = 560, 420
    lab = 30
    sheet = Image.new("RGB", (cw * 2, (chh + lab) * len(outfits)), (24, 20, 30))
    d = ImageDraw.Draw(sheet)
    for r, (name, pieces) in enumerate(outfits):
        for c, (f, x) in enumerate(((None, 330), (stage.b.AFTER - 1, stage.x_strike + 20))):
            img, _ = stage.frame(pieces, f, 0, 0, effects=False)
            w, h = 400 * PX, 300 * PX
            cx = int(x * PX - w / 2)
            box = (cx, (520 - 300) * PX, cx + w, 520 * PX)
            sheet.paste(img.crop(box).resize((cw, chh), Image.LANCZOS), (c * cw, r * (chh + lab) + lab))
        d.text((6, r * (chh + lab) + 4), f"{name}: {caption(pieces)}  (ready | strike)", fill=(240, 236, 226),
               font=_font(18))
    p = common.OUT / "outfit_stills.png"
    _fit(sheet).save(p)
    print(p, sheet.size)


def _variant_md(stage, b):
    att = T.Attack(stage.hero, stage.x_strike, b)
    held_s = (b.BACKOFF_AT - b.HIT) / FPS
    lines = [f"## {b.moments} moments: {b.LENGTH} frames = {b.LENGTH / FPS * 1000:.0f} ms", "",
             f"Wind-up {b.DASH_AT} frames ({b.DASH_AT / FPS * 1000:.0f} ms, the crouch at its end) + dash {T.DASH}; "
             f"the hit on frame {b.HIT}; the {'strike' if b.moments == 3 else 'strike and follow-through'} on screen "
             f"{b.SWAP_AT - b.HIT} frames ({(b.SWAP_AT - b.HIT) / FPS * 1000:.0f} ms, of which the back-off "
             f"{T.BACKOFF} = {T.BACKOFF / FPS * 1000:.0f} ms; held in place {held_s * 1000:.0f} ms incl. the freeze); "
             f"ready in the air to home {T.HOME} frames ({T.HOME / FPS * 1000:.0f} ms); landing {T.LAND}.", "",
             "| phase | frames | ms | moment shown | where: figure x (its anchor foot/feet), feet y at the top of a hop | what moves | Unity |",
             "|---|---|---|---|---|---|---|"]
    for name, start, n, m, what, unity in b.PHASES:
        if start is None:
            lines.append(f"| {name} | - | - | {m} | x {att.home}, y {T.GROUND_Y} | {what} | {unity} |")
            continue
        xs = [att.hero(f)[1] for f in range(start, start + n)]
        ys = [att.hero(f)[2] for f in range(start, start + n)]
        lines.append(f"| {name} | {start}-{start + n - 1} ({n}) | {round(start * 1000 / FPS)}-"
                     f"{round((start + n) * 1000 / FPS)} | {m} | x {xs[0]:.0f} -> {xs[-1]:.0f}, y min "
                     f"{min(ys):.0f} | {what} | {unity} |")
    lines += ["", "| effect | starts (frame) | lasts (frames) | what it does | Unity |", "|---|---|---|---|---|"]
    for name, start, n, what, unity in b.EFFECTS:
        lines.append(f"| {name} | {start} ({round(start * 1000 / FPS)} ms) | {n} | {what} | {unity} |")
    lines += ["", f"During the freeze (frames {b.HIT}-{b.AFTER - 1}) every tween holds, effects included (the dash "
              f"streaks, ghosts and dust too), as Fx.HitStop does, except the monster white flash, which runs on "
              f"real time; they resume on frame {b.AFTER}.", ""]
    return lines


def timing_md(stage):
    lines = ["# The sword attack: timing (60 fps), both variants", "",
             f"Generated by `scripts/momentclip/clip.py --sheets` from the tables in `timeline.py`. Design px "
             f"(1280x720; x right, y down; ground y {T.GROUND_Y}). The hero's home x {stage.att.home}; the strike "
             f"stands at x {stage.x_strike:.0f} (its front foot): the further back of the cut's thick end running "
             f"through the monster's middle and the sword hand's grip stopping {T.CUT['clear']} px short of the "
             f"monster's front edge (here the '{stage.stand_rule}' rule). The hit shows at ({stage.hit[0]:.0f}, "
             f"{stage.hit[1]:.0f}), on the cut's band. Both variants come from one timeline "
             f"(`clip.py --moments 3|4`); they are the same up to the end of the freeze.", ""]
    lines += [f"- {c}" for c in stage.checks] + [""]
    for m in (3, 4):
        if m == 4 and "follow" not in stage.hero.moments:
            continue
        lines += _variant_md(stage, T.Beats(m))
    p = common.OUT / "timing.md"
    p.write_text("\n".join(lines), encoding="utf-8")
    print(p)
