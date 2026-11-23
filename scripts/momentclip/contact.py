"""Where the strike stands so the cut lands on the monster, and the cut's circle.

The cut is drawn on the circle the blade tip travels: its centre lies on the strike blade's line, `shoulder` blade
lengths behind the grip (it stands in for the sword arm's shoulder), so the radius, (1 + shoulder) blade lengths,
reaches the strike's blade tip exactly. The crescent covers the last `span` degrees before the strike angle and is
`width` blade lengths wide at the blade (its outer edge on the circle).

Where the hero stands (the strike's front foot) is the further back of two rules:
- depth: the monster's middle lies `depth` of the cut's width inside its outer edge, in front of the centre (a small
  monster: the thick end of the cut runs through its middle);
- clear: the sword hand's grip stops `clear` design px short of the monster's front edge (a wide monster: the fist
  and the cross-guard stay free of it, while the blade and the cut's thick end still reach into its front).
The hit (impact, sparks) shows where the middle of the cut's band crosses the monster's middle height. All of it is
data of the attack (timeline.CUT) read against the manifest and the monster's picture; nothing is typed in for one
drawing."""
import math

import numpy as np

from common import PX


def monster_box(mon_spr, mx, my):
    """(front edge x, middle x, middle y) of the monster's picture (design px): the left side of its alpha box (it
    faces left, toward the hero) and its centroid."""
    im, ox, oy = mon_spr[0], mon_spr[1], mon_spr[2]
    a = np.asarray(im)[..., 3] > 128
    ys, xs = np.nonzero(a)
    return mx + (xs.min() - ox) / PX, mx + (xs.mean() - ox) / PX, my + (ys.mean() - oy) / PX


def on_monster(mon_spr, mx, my, p):
    """True when the design point p lies on the monster's picture."""
    im, ox, oy = mon_spr[0], mon_spr[1], mon_spr[2]
    col, row = round(ox + (p[0] - mx) * PX), round(oy + (p[1] - my) * PX)
    return 0 <= col < im.width and 0 <= row < im.height and im.getpixel((col, row))[3] > 128


def cut(hero, tune):
    """The cut relative to the strike's feet: (centre, radius, start angle, end angle, blade length, full width).
    Angles in the blade convention (0 = up, clockwise)."""
    (gx, gy), (tx, ty) = hero.blade("strike")
    L = math.hypot(tx - gx, ty - gy)
    a1 = hero.moments["strike"]["weapon"]["angle"]
    ux, uy = math.sin(math.radians(a1)), -math.cos(math.radians(a1))
    c = (gx - tune["shoulder"] * ux * L, gy - tune["shoulder"] * uy * L)
    r = (1 + tune["shoulder"]) * L
    return c, r, a1 - tune["span"], a1, L, tune["width"] * L


def solve(hero, mon_spr, mx, my, Y, tune):
    """(the strike's figure x (front foot), the hit point, the rule that set the x ('depth' or 'clear'), how far the
    sword hand's grip then is from the monster's front edge). The hit lies on the middle of the cut's band, at the
    monster's middle height."""
    (cx, cy), r, a0, a1, L, W = cut(hero, tune)
    front, midx, midy = monster_box(mon_spr, mx, my)
    rr, rm = r - tune["depth"] * W, r - 0.5 * W
    dy = midy - (Y + cy)
    if abs(dy) > min(rr, rm):
        raise SystemExit("the cut's band cannot reach the monster's middle height")
    gx = hero.blade("strike")[0][0]
    x_depth = midx - math.sqrt(rr * rr - dy * dy) - cx  # monster in front (to the right) of the centre
    x_clear = front - tune["clear"] - gx
    X = min(x_depth, x_clear)
    hit = (X + cx + math.sqrt(rm * rm - dy * dy), midy)
    return X, hit, ("depth" if x_depth <= x_clear else "clear"), front - (X + gx)


def on_circle(p, centre, r):
    """(distance of p inside the circle (negative: outside), its angle on the circle in the blade convention)."""
    dx, dy = p[0] - centre[0], p[1] - centre[1]
    return r - math.hypot(dx, dy), math.degrees(math.atan2(dx, -dy))
