"""Where the strike stands so the cut lands on the monster, and where the hit shows.

The strike drawing shows the blade already through and low (its tip near the ground), so the cut is the slash arc:
the blade's path from the wind-up angle over the top down to the strike angle, around the strike's grip, radius = the
blade. The hero stands so that this arc's band runs through the middle of the monster's body; the hit shows where it
does. The blade drawn in the strike then ends low, past the cut."""
import math

import numpy as np

from common import PX


def monster_middle(mon_spr, mx, my):
    """Centroid of the monster's picture (design px)."""
    im, ox, oy = mon_spr[0], mon_spr[1], mon_spr[2]
    a = np.asarray(im)[..., 3] > 128
    ys, xs = np.nonzero(a)
    return mx + (xs.mean() - ox) / PX, my + (ys.mean() - oy) / PX


def arc(hero):
    """(grip relative to the strike's feet, blade radius, wind-up angle, strike angle) in design px / degrees."""
    (gx, gy), (tx, ty) = hero.blade("strike")
    r = math.hypot(tx - gx, ty - gy)
    a0 = hero.moments["wind"]["weapon"]["angle"]
    a1 = hero.moments["strike"]["weapon"]["angle"]
    while a1 < a0:  # clockwise from the wind-up, over the top
        a1 += 360
    return (gx, gy), r, a0, a1


def solve(hero, mon_spr, mx, my, Y, band=0.9):
    """The strike's figure x (front foot), the hit point, and the arc angle at the hit."""
    (gx, gy), r, a0, a1 = arc(hero)
    cx, cy = monster_middle(mon_spr, mx, my)
    dy = cy - (Y + gy)
    rr = r * band
    if abs(dy) > rr:
        raise SystemExit("the slash arc cannot reach the monster's middle")
    dx = math.sqrt(rr * rr - dy * dy)
    X = cx - dx - gx  # monster in front (to the right) of the grip
    ang = math.degrees(math.atan2(dx, -dy))  # blade-angle convention
    if not (a0 <= ang <= a1 or a0 <= ang + 360 <= a1):
        raise SystemExit(f"the arc crosses the monster at {ang:.0f} deg, outside the swing {a0:.0f}..{a1:.0f}")
    return X, (cx, cy), ang
