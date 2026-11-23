"""A layers folder -> the hero's four drawn moments as game-size sprites, per outfit.

Everything is read from the folder's manifest.json: stacking order, weapon spot/angle/scale, soles offset, pieces.
The only thing measured from the pictures is where each foot stands (the body layer's bottom edge), to keep a
planted foot in place across a swap.
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

from common import PX, RAW, WEAPON_GRIP

PAD = 800            # the sword reaches past the 1536 canvas at the wind-up and the strike
SOLES = 1390         # manifest: shift a moment down by offset_y and its soles stand on y 1390
HERO_DESIGN_H = 240  # the hero is drawn 240 design px tall in the battle (Figure.ArtHeight)

# Stacking, bottom to top (manifest "order"). None = the weapon.
SLOTS_ORDER = ["weapon_back", "body", "shirt", "hair", "legs", "boots", "chest", "gloves_far", "weapon_front",
               "fist", "gloves_near", "helm"]


def _load(p: Path) -> Image.Image:
    return Image.open(p).convert("RGBA")


def _premul_resize(im: Image.Image, size) -> Image.Image:
    return im.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


class Weapon:
    """The weapon art with its grip point (centre, `grip` of the height) and the blade tip (top of its alpha box)."""

    def __init__(self, art: str):
        self.art = art
        self.im = _load(RAW / f"{art}.png")
        a = np.asarray(self.im)[..., 3]
        ys = np.nonzero(a.max(1) > 40)[0]
        self.top = int(ys.min())
        self.grip = (self.im.width / 2, WEAPON_GRIP.get(art, 0.79) * self.im.height)

    def tip_len(self, scale: float) -> float:
        return (self.grip[1] - self.top) * scale

    def placed(self, grip_xy, angle: float, scale: float, canvas: int, pad: int) -> Image.Image:
        """The weapon on a padded full canvas: grip point on grip_xy, turned `angle` (0 = blade up, clockwise)."""
        w, h = self.im.size
        small = _premul_resize(self.im, (round(w * scale), round(h * scale)))
        gx, gy = small.width / 2, self.grip[1] / h * small.height
        th = math.radians(angle)
        cs, sn = math.cos(th), math.sin(th)
        cx, cy = grip_xy[0] + pad, grip_xy[1] + pad
        # forward: p' = R p + t, with R = [[cs, -sn], [sn, cs]] (clockwise on screen, y down), grip -> (cx, cy)
        tx, ty = cx - (cs * gx - sn * gy), cy - (sn * gx + cs * gy)
        # PIL wants the inverse map: p = R^T (p' - t)
        inv = (cs, sn, -(cs * tx + sn * ty), -sn, cs, -(-sn * tx + cs * ty))
        size = canvas + 2 * pad
        return small.convert("RGBa").transform((size, size), Image.AFFINE, inv, Image.BICUBIC).convert("RGBA")

    def tip_xy(self, grip_xy, angle, scale):
        th = math.radians(angle)
        L = self.tip_len(scale)
        return grip_xy[0] + math.sin(th) * L, grip_xy[1] - math.cos(th) * L


def feet(body: Image.Image, soles_y: int, box) -> dict:
    """Where each foot stands: columns of the body layer whose lowest pixel is near the soles, grouped; a group left of
    the body box's middle is the back foot (he faces right), right of it the front foot. x = middle of the group's
    lowest 15 px; y = its lowest pixel."""
    a = np.asarray(body)[..., 3] > 100
    h = a.shape[0]
    has = a.any(0)
    bottom = np.where(has, h - 1 - np.argmax(a[::-1], 0), -1)
    cand = np.nonzero(bottom >= soles_y - 110)[0]
    groups, cur = [], [cand[0]] if len(cand) else []
    for c in cand[1:]:
        if c - cur[-1] > 12:
            groups.append(cur)
            cur = [c]
        else:
            cur.append(c)
    if cur:
        groups.append(cur)
    mid = (box[0] + box[2]) / 2
    out = {}
    for g in groups:
        if len(g) < 15:
            continue
        g = np.array(g)
        lo = bottom[g].max()
        xs = g[bottom[g] >= lo - 15]
        side = "back" if g.mean() < mid else "front"
        if side not in out or lo > out[side][1]:
            out[side] = (float(xs.mean()), int(lo), int(g.min()), int(g.max()))
    return out


class Hero:
    """The four moments from one layers folder. `sprite(moment, outfit)` -> game-size RGBA sprite and its anchor."""

    def __init__(self, folder, anchor_override=None):
        self.folder = Path(folder)
        self.man = json.loads((self.folder / "manifest.json").read_text())
        self.canvas = int(self.man["canvas"])
        self.k = HERO_DESIGN_H / float(self.man["hero_h"]) * PX  # output px per drawing px
        self.design_k = HERO_DESIGN_H / float(self.man["hero_h"])
        self.moments = self.man["moments"]
        self.weapons = {}
        self._cache = {}
        self.feet = {m: feet(_load(self.folder / m / "body.png"), d["soles_y"], d["sprites"]["body"])
                     for m, d in self.moments.items()}
        self.anchor = self._anchors(anchor_override or {})

    def weapon(self, m):
        art = self.moments[m]["weapon"]["art"]
        if art not in self.weapons:
            self.weapons[art] = Weapon(art)
        return self.weapons[art]

    def _need(self, m, side):
        f = self.feet.get(m, {}).get(side)
        if f is None:
            raise SystemExit(f"{m}: no {side} foot found near the soles (feet found: {self.feet.get(m)}). "
                             f"Pass --anchor {m}=<x> to set it by hand.")
        return f[0]

    def _anchors(self, override):
        """Drawing x of each moment that sits on the figure's x. ready: between its feet (the figure's middle). wind:
        ready's back foot stays planted. strike: its front foot. follow: strike's front foot stays planted."""
        a = {}
        rb, rf = self._need("ready", "back"), self._need("ready", "front")
        a["ready"] = (rb + rf) / 2
        a["wind"] = self._need("wind", "back") + (a["ready"] - rb)
        a["strike"] = self._need("strike", "front")
        a["follow"] = self._need("follow", "front")
        for m, x in override.items():
            a[m] = float(x)
        return a

    def pieces_of(self, m, outfit):
        """(slot, file) for each worn piece at moment m, only as the manifest lists them."""
        listed = self.moments[m].get("pieces", {})
        out = {}
        for p in outfit:
            if p not in listed:
                raise SystemExit(f"{p}: the manifest has no layer for it at '{m}' (has: {sorted(listed)})")
            for slot, info in listed[p].items():
                if info.get("box") is None:  # an empty cut: nothing to draw
                    continue
                out[slot] = self.folder / m / info["file"]
        return out

    def full(self, m, outfit=()):
        """The moment stacked on the padded canvas (drawing px), soles shifted onto SOLES."""
        d = self.moments[m]
        size = self.canvas + 2 * PAD
        img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        worn = self.pieces_of(m, outfit)
        w = d["weapon"]
        wpn = self.weapon(m).placed(w["grip_xy"], w["angle"], w["scale_px_per_art_px"], self.canvas, PAD)
        order = d.get("weapon_order", "front")
        for slot in SLOTS_ORDER:
            if slot == "weapon_back":
                layer = wpn if order == "back" else None
            elif slot == "weapon_front":
                layer = wpn if order != "back" else None
            elif slot in ("body", "fist"):
                layer = _load(self.folder / m / f"{slot}.png")
            elif slot == "shirt":
                layer = None if "chest" in worn else _load(self.folder / m / "shirt.png")
            elif slot == "hair":
                layer = None if "helm" in worn else _load(self.folder / m / "hair.png")
            else:
                layer = _load(worn[slot]) if slot in worn else None
            if layer is None:
                continue
            if layer.size[0] == size:
                img.alpha_composite(layer)
            else:
                img.alpha_composite(layer, (PAD, PAD))
        off = int(d["offset_y"])
        if off:
            shifted = Image.new("RGBA", img.size, (0, 0, 0, 0))
            shifted.alpha_composite(img, (0, off))
            img = shifted
        return img

    def sprite(self, m, outfit=()):
        """(sprite, ox, oy): the moment at game size (output px), trimmed; (ox, oy) is where the anchor x on the
        ground lies inside it."""
        key = (m, tuple(outfit))
        if key not in self._cache:
            full = self.full(m, outfit)
            x0, y0, x1, y1 = full.getbbox()
            crop = full.crop((x0, y0, x1, y1))
            k = self.k
            sm = _premul_resize(crop, (max(1, round(crop.width * k)), max(1, round(crop.height * k))))
            ox = (self.anchor[m] + PAD - x0) * k
            oy = (SOLES + PAD - y0) * k
            self._cache[key] = (sm, ox, oy)
        return self._cache[key]

    def to_figure(self, m, x, y):
        """A drawing point of moment m (unshifted canvas px) -> design px relative to the figure's feet (y up < 0)."""
        d = self.moments[m]
        return (x - self.anchor[m]) * self.design_k, (y + d["offset_y"] - SOLES) * self.design_k

    def blade(self, m):
        """Grip and tip of the blade at moment m, in design px relative to the figure's feet."""
        w = self.moments[m]["weapon"]
        tip = self.weapon(m).tip_xy(w["grip_xy"], w["angle"], w["scale_px_per_art_px"])
        return self.to_figure(m, *w["grip_xy"]), self.to_figure(m, *tip)
