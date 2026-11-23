"""The battle view at 2560x1440: background, monster, shadows, and placing whole-figure sprites (move, lean about a
point on the ground, a few percent of squash/stretch, a flat tint). Units: design px (1280x720) unless named _px."""
import math

import numpy as np
from PIL import Image

from common import (ART_H, ART_MAX_W, BG_RAISE, BG_STRIP, DESIGN_H, DESIGN_W, ENEMY_X, GROUND_Y, MONSTER, PX, RAW,
                    SHADOW_ALPHA, SHADOW_H)

W_PX, H_PX = DESIGN_W * PX, DESIGN_H * PX
ALPHA_FLOOR = 10  # prepare_art.py: alpha below this in a transparent source is cleared
MARGIN = 48  # output px of world drawn past the screen edge, so a camera shake never shows a hole


def background(name="bg_fen_road") -> Image.Image:
    """Backdrop.Draw: cover the 1280x720 screen, lifted by BgRaise; the strip left at the bottom is filled dark. Drawn
    with MARGIN px of extra world on every side (edge pixels repeated)."""
    im = Image.open(RAW / f"{name}.png").convert("RGB")
    s = max(W_PX / im.width, H_PX / im.height)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    world = Image.new("RGB", (W_PX, H_PX), BG_STRIP)
    world.paste(im, ((W_PX - im.width) // 2, (H_PX - im.height) // 2 - BG_RAISE * PX))
    a = np.asarray(world)
    a = np.pad(a, ((MARGIN, MARGIN), (MARGIN, MARGIN), (0, 0)), mode="edge")
    return Image.fromarray(a).convert("RGBA")


def monster_sprite():
    """Figure.Build for a monster: art trimmed, scaled to 240 tall (at most 300 wide), times the look's scale; faces
    left (facing -1: the art faces right, mirrored). Returns (sprite, ox, oy) with (ox, oy) = feet centre in it."""
    im = Image.open(RAW / f"{MONSTER['art']}.png").convert("RGBA")
    # As prepare_art.py makes the game's sprite: alpha under 10 is noise (the raw canvas holds values 1-2 all over,
    # which made the draft trim the whole 1536 canvas: the toad came out at 64% size, floating 40 px above its
    # shadow), then trimmed with a 6 px pad. The game's sprite pivot is the bottom of that box.
    a = np.asarray(im)
    a = a.copy()
    a[..., 3] = np.where(a[..., 3] < ALPHA_FLOOR, 0, a[..., 3])
    im = Image.fromarray(a)
    ys, xs = np.nonzero(a[..., 3] > 13)
    box = (max(0, xs.min() - 6), max(0, ys.min() - 6), min(im.width, xs.max() + 7), min(im.height, ys.max() + 7))
    im = im.crop(box)
    s = min(ART_H / im.height, ART_MAX_W / im.width) * MONSTER["scale"] * PX
    sm = im.convert("RGBa").resize((round(im.width * s), round(im.height * s)), Image.LANCZOS).convert("RGBA")
    sm = sm.transpose(Image.FLIP_LEFT_RIGHT)
    return sm, sm.width / 2, sm.height, sm.width / PX  # drawn width in design px


def soft_shadow(width_design: float) -> Image.Image:
    """Art.Soft stretched to width x 26, black at 45% (Figure.AddShadow). Art.Soft's alpha is (1 - d)^2, d = distance
    from the middle over the radius (Art.cs), so only its middle reaches 45%."""
    w, h = max(2, round(width_design * PX)), round(SHADOW_H * PX)
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx + 0.5 - w / 2) / (w / 2)) ** 2 + ((yy + 0.5 - h / 2) / (h / 2)) ** 2)
    a = np.clip(1 - r, 0, 1) ** 2 * 255 * SHADOW_ALPHA
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 3] = a.astype(np.uint8)
    return Image.fromarray(out)


def to_px(x, y):
    """Design px -> px on the world canvas (screen px + MARGIN)."""
    return x * PX + MARGIN, y * PX + MARGIN


def place(canvas: Image.Image, spr, x: float, y: float, sx=1.0, sy=1.0, angle=0.0, pivot=None, tint=None,
          alpha=1.0):
    """Draw a sprite whose anchor (ox, oy) stands on design (x, y): scaled sx, sy about the anchor, then turned `angle`
    degrees clockwise about `pivot` (design px, default the anchor). tint: flat RGB over the whole sprite (a white
    flash, a ghost)."""
    im, ox, oy = spr[0], spr[1], spr[2]
    ax, ay = to_px(x, y)
    px_, py_ = to_px(*pivot) if pivot is not None else (ax, ay)
    th = math.radians(angle)
    cs, sn = math.cos(th), math.sin(th)

    def fwd(u, v):  # sprite px -> canvas px
        X = ax + (u - ox) * sx
        Y = ay + (v - oy) * sy
        dx, dy = X - px_, Y - py_
        return px_ + cs * dx - sn * dy, py_ + sn * dx + cs * dy

    corners = [fwd(u, v) for u, v in ((0, 0), (im.width, 0), (0, im.height), (im.width, im.height))]
    x0 = math.floor(min(c[0] for c in corners)) - 1
    y0 = math.floor(min(c[1] for c in corners)) - 1
    x1 = math.ceil(max(c[0] for c in corners)) + 1
    y1 = math.ceil(max(c[1] for c in corners)) + 1
    # inverse map: canvas (x0 + i, y0 + j) -> sprite (u, v)
    # canvas = P + R * S * (s - o) + R*(A - P)  =>  s = o + S^-1 R^T (c - P) - S^-1 (A - P)
    a11, a12, a21, a22 = cs / sx, sn / sx, -sn / sy, cs / sy
    cx0, cy0 = x0 - px_, y0 - py_
    c = ox + a11 * cx0 + a12 * cy0 - (ax - px_) / sx
    f = oy + a21 * cx0 + a22 * cy0 - (ay - py_) / sy
    if abs(angle) < 1e-3 and abs(sx - 1) < 1e-4 and abs(sy - 1) < 1e-4:
        out = im
        x0, y0 = round(ax - ox), round(ay - oy)
    else:
        out = im.convert("RGBa").transform((x1 - x0, y1 - y0), Image.AFFINE, (a11, a12, c, a21, a22, f),
                                           Image.BICUBIC).convert("RGBA")
    if tint is not None or alpha < 1:
        arr = np.asarray(out).copy()
        if tint is not None:
            arr[..., :3] = tint
        if alpha < 1:
            arr[..., 3] = (arr[..., 3] * alpha).astype(np.uint8)
        out = Image.fromarray(arr)
    canvas.alpha_composite(out, (int(x0), int(y0))) if 0 <= x0 and 0 <= y0 else _paste_clipped(canvas, out, x0, y0)


def point(dx, dy, x, y, sx=1.0, sy=1.0, angle=0.0, pivot=None):
    """Where a point of a figure (dx, dy design px from its anchor) lands when the figure is placed as place() does."""
    px, py = pivot if pivot is not None else (x, y)
    X, Y = x + dx * sx - px, y + dy * sy - py
    th = math.radians(angle)
    return px + math.cos(th) * X - math.sin(th) * Y, py + math.sin(th) * X + math.cos(th) * Y


def _paste_clipped(canvas, im, x0, y0):
    l, t = max(0, -x0), max(0, -y0)
    if l >= im.width or t >= im.height:
        return
    canvas.alpha_composite(im.crop((l, t, im.width, im.height)), (x0 + l, y0 + t))


def mask_of(spr, x, y):
    """The sprite's alpha as a boolean design-px grid lookup: returns f(xd, yd) -> inside? (no transforms)."""
    im, ox, oy = spr[0], spr[1], spr[2]
    a = np.asarray(im)[..., 3] > 128

    def inside(xd, yd):
        u = int(round((xd - x) * PX + ox))
        v = int(round((yd - y) * PX + oy))
        return 0 <= u < a.shape[1] and 0 <= v < a.shape[0] and bool(a[v, u])
    return inside


GROUND = GROUND_Y
ENEMY = ENEMY_X
