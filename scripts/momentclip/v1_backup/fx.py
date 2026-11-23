"""Effects drawn by code in the flat cut-paper look: hard-edged flat shapes (antialiased edges only), no blur, no glow.
Each is a separate sprite in Unity (a mesh or a sprite moved by an Anim tween). Positions in design px."""
import math
import random

from PIL import Image, ImageDraw, ImageFont

from common import FONTS, PX
from scene import to_px

SS = 3  # supersampling for clean edges

GOLD = (255, 231, 160)
WHITE = (255, 255, 255)
CREAM = (255, 244, 214)
DUST = (184, 164, 132)
BURST = (255, 176, 58)
INK = (20, 14, 10)


def polys(canvas: Image.Image, shapes):
    """shapes: [(points_design, (r, g, b), alpha 0-1)] drawn in order, flat, onto the canvas."""
    pts_all = [to_px(x, y) for s in shapes for (x, y) in s[0]]
    if not pts_all:
        return
    x0 = max(0, int(min(p[0] for p in pts_all)) - 2)
    y0 = max(0, int(min(p[1] for p in pts_all)) - 2)
    x1 = min(canvas.width, int(max(p[0] for p in pts_all)) + 3)
    y1 = min(canvas.height, int(max(p[1] for p in pts_all)) + 3)
    if x1 <= x0 or y1 <= y0:
        return
    layer = Image.new("RGBA", ((x1 - x0) * SS, (y1 - y0) * SS), (0, 0, 0, 0))
    for pts, col, a in shapes:
        if len(pts) < 3 or a <= 0:
            continue
        one = Image.new("RGBA", layer.size, (0, 0, 0, 0))
        ImageDraw.Draw(one).polygon([((to_px(x, y)[0] - x0) * SS, (to_px(x, y)[1] - y0) * SS) for x, y in pts],
                                    fill=col + (round(255 * a),))
        layer.alpha_composite(one)
    layer = layer.convert("RGBa").resize((x1 - x0, y1 - y0), Image.BOX).convert("RGBA")
    canvas.alpha_composite(layer, (x0, y0))


def _pt(c, r, a):
    """A point at blade angle a (degrees, 0 = up, clockwise) and radius r from c."""
    t = math.radians(a)
    return c[0] + math.sin(t) * r, c[1] - math.cos(t) * r


def arc_shapes(center, radius, a0, a1, tail=0.0, width=1.0, alpha=1.0):
    """The slash: a crescent around `center` from angle a0 (wind-up) clockwise to a1 (strike), thin at the start and
    thick at the blade. `tail` 0-1 cuts the start away toward the blade (the cut wiping out); `width` scales it."""
    n = 64
    ro = radius + 6
    outer, inner, core_o, core_i = [], [], [], []
    for i in range(n + 1):
        t = tail + (1 - tail) * i / n
        a = a0 + (a1 - a0) * t
        th = (3 + 30 * t ** 1.6) * width
        outer.append(_pt(center, ro, a))
        inner.append(_pt(center, ro - th, a))
        core_o.append(_pt(center, ro - 1.5, a))
        core_i.append(_pt(center, ro - 1.5 - th * 0.42, a))
    # in 8 pieces whose opacity ramps up toward the blade (a mesh with vertex alpha in Unity)
    out = []
    seg = n // 8
    for s in range(8):
        i0, i1 = s * seg, min(n, (s + 1) * seg + 1)
        k = 0.3 + 0.7 * ((s + 1) / 8) ** 1.2
        band = outer[i0:i1] + inner[i0:i1][::-1]
        core = core_o[i0:i1] + core_i[i0:i1][::-1]
        out += [([(x + 3, y + 4) for x, y in band], INK, 0.16 * alpha * k), (band, GOLD, 0.95 * alpha * k),
                (core, WHITE, alpha * k)]
    return out


def star(c, r, spikes, rot, inner=0.48):
    pts = []
    for i in range(spikes * 2):
        rr = r if i % 2 == 0 else r * inner
        a = rot + i * 180 / spikes
        pts.append(_pt(c, rr, a))
    return pts


def impact_shapes(c, size, alpha=1.0, rot=12.0):
    """Fx.Impact: a jagged burst in gold with a white one inside (no glow, no rings)."""
    return [(star(c, 50 * size, 9, rot), INK, 0.85 * alpha), (star(c, 46 * size, 9, rot), BURST, alpha),
            (star(c, 30 * size, 9, rot + 20, 0.5), GOLD, alpha), (star(c, 17 * size, 9, rot + 8, 0.5), WHITE, alpha)]


class Sparks:
    """Fx.HitSparks: flat shards flying out of the hit, falling, shrinking (420 ms)."""

    def __init__(self, c, n=14, seed=3, forward=1):
        rnd = random.Random(seed)
        self.c = c
        self.p = []
        for _ in range(n):
            ang = rnd.uniform(-150, 150) if forward > 0 else rnd.uniform(0, 360)
            ang = math.radians(ang * 0.8 - 30 * forward)  # mostly forward and up, the way the blow travels
            sp = rnd.uniform(220, 520)
            col = rnd.choice([WHITE, (255, 241, 168), (255, 179, 71)])
            self.p.append((math.cos(ang) * sp * forward, math.sin(ang) * sp, rnd.uniform(9, 17), col))

    def shapes(self, t):
        """t: seconds since emission."""
        life = 0.42
        if t >= life:
            return []
        k = 1 - t / life
        out = []
        for vx, vy, ln, col in self.p:
            x = self.c[0] + vx * t
            y = self.c[1] + vy * t + 0.5 * 900 * t * t
            vyt = vy + 900 * t
            m = math.hypot(vx, vyt) or 1
            ux, uy = vx / m, vyt / m
            L, w = ln * (0.4 + 0.6 * k), 2.6 * k + 0.6
            out.append(([(x + ux * L, y + uy * L), (x - uy * w, y + ux * w), (x - ux * L * 0.6, y - uy * L * 0.6),
                         (x + uy * w, y - ux * w)], col, 1.0))
        return out


class Dust:
    """Fx.Dust as flat paper puffs: a few cream discs that rise a little, grow, then shrink away (450 ms)."""

    def __init__(self, x, y, n=3, seed=1, spread=20, push=0):
        rnd = random.Random(seed)
        self.p = [(x + rnd.uniform(-spread, spread), y - rnd.uniform(0, 4), rnd.uniform(-50, 50) + push,
                   rnd.uniform(-40, -10), rnd.uniform(7, 12)) for _ in range(n)]

    def shapes(self, t):
        life = 0.45
        if t >= life or t < 0:
            return []
        k = t / life
        out = []
        for i, (x, y, vx, vy, r) in enumerate(self.p):
            rr = r * (0.7 + 0.9 * k) * (1 - k ** 3)
            cx, cy = x + vx * t, y + vy * t - rr * 0.35
            # a flat torn-paper puff: a wide lumpy oval, wider than tall
            pts = []
            for j, a in enumerate(range(0, 360, 24)):
                bump = 1 + 0.16 * ((j + i) % 3 - 1)
                t_ = math.radians(a)
                pts.append((cx + math.sin(t_) * rr * 1.5 * bump, cy - math.cos(t_) * rr * 0.75 * bump))
            out.append((pts, DUST, 0.55 * (1 - k) ** 0.7))
        return out


def streak_shapes(x, y, dir_, t, seed=5):
    """Fx.SpeedLines: flat white streaks shooting back from a dashing figure (200 ms)."""
    rnd = random.Random(seed)
    life = 0.2
    if t >= life or t < 0:
        return []
    k = t / life
    out = []
    for _ in range(7):
        sx = x + rnd.uniform(-30, 30)
        sy = y - rnd.uniform(10, 150)
        v = rnd.uniform(500, 900)
        L = rnd.uniform(40, 80) * (1.1 - 0.8 * k)
        cx = sx - v * t * dir_
        h = 2.2
        out.append(([(cx, sy - h), (cx - L * dir_, sy - h * 0.3), (cx - L * dir_, sy + h * 0.3), (cx, sy + h)],
                    WHITE, 0.55 * (1 - k)))
    return out


_font_cache = {}


def number(canvas, x, y, text, size_design, scale, alpha, color=(255, 255, 255)):
    """Ui.FloatText: bold text with a black outline, popping in by scale, rising and fading."""
    px = max(4, round(size_design * scale * PX))
    key = px
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(str(FONTS / "AlegreyaSans-ExtraBold.ttf"), px)
    f = _font_cache[key]
    stroke = max(2, round(px * 0.075))
    d0 = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
    l, t, r, b = d0.textbbox((0, 0), text, font=f, stroke_width=stroke)
    im = Image.new("RGBA", (r - l + 4, b - t + 4), (0, 0, 0, 0))
    ImageDraw.Draw(im).text((2 - l, 2 - t), text, font=f, fill=color + (255,), stroke_width=stroke,
                            stroke_fill=(0, 0, 0, 255))
    if alpha < 1:
        a = im.getchannel("A").point(lambda v: int(v * alpha))
        im.putalpha(a)
    cx, cy = to_px(x, y)
    canvas.alpha_composite(im, (int(cx - im.width / 2), int(cy - im.height / 2)))


def caption(canvas, text):
    """A small plain caption of what he wears, bottom left over the dark strip."""
    f = _font_cache.get("cap") or ImageFont.truetype(str(FONTS / "AlegreyaSans-Bold.ttf"), 30 * PX // 2)
    _font_cache["cap"] = f
    x, y = to_px(24, 720 - 52)
    ImageDraw.Draw(canvas).text((x, y), text, font=f, fill=(236, 228, 210, 255))
