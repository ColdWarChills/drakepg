#!/usr/bin/env python3
"""Turn raw generated images in art/raw/ into game-ready files in src/assets/art/.

File names decide what happens (see art/PROMPTS.md for the list):
  bg_*.png/jpg  -> background: center-cropped to 16:9, resized to 2560x1440, saved as WebP.
  wpn_*         -> weapon: like a sprite, but 640px tall. Draw weapons upright, handle at the bottom.
  icon_*        -> inventory icon: like a sprite, but fit inside 512x512.
  anything else -> sprite: flat background color removed, trimmed, resized to 960px tall, saved as WebP.

Art is saved at 2x (twice the 1x size the game lays out in: 1280x720 backgrounds, 480px sprites, 320px weapons,
256px icons) as `<key>@2x.webp`, so it stays sharp on a 1440p screen; the game draws it at the 1x size, so old 1x
files and new 2x files mix freely. A source too small for 2x (less than 3/4 of it) is saved at 1x as `<key>.webp`,
and so is everything with `--1x`. Hand anchors (`look.bare.hands`) are always in 1x pixels: halve what you measure on a
@2x file.

Sprites that already have a transparent background (PNG with alpha, e.g. from Qwen-Image 2.1 via
scripts/qwen_batch.py) skip the background removal: faint alpha noise is cleared, then they are trimmed the same way.

Usage:
  python3 scripts/prepare_art.py            # process everything in art/raw/
  python3 scripts/prepare_art.py mon_mossling bg_thornwood   # only these keys
  (keys in HAND_EDITED are skipped unless --force is given)
  python3 scripts/prepare_art.py --1x mon_mossling          # the old 1x sizes

Needs Pillow and numpy.
"""
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "art" / "raw"
OUT = ROOT / "src" / "assets" / "art"

# 1x sizes; files are written at RES times these (see the docstring).
BG_SIZE = (1280, 720)
SPRITE_HEIGHT = 480
WEAPON_HEIGHT = 320
ICON_SIZE = 256
RES = 2
# A source smaller than this share of the 2x size is saved at 1x instead (a small upscale is fine, a big one isn't).
MIN_2X_SHARE = 0.75

# Where to crop backgrounds that aren't 16:9: 0 = keep the top/left, 1 = keep the bottom/right.
BG_FOCUS: dict[str, float] = {"bg_brindlemoor": 0.75, "bg_windscour_ridge": 0.75}
# Backgrounds kept at their own aspect (scaled to 720 tall). The scene lays them out itself.
BG_KEEP_ASPECT = {"bg_title"}

# Sprites that came out facing left. Game art faces right; the engine mirrors enemies itself.
FLIP: set[str] = set()

# Background removal thresholds (RGB distance from the detected background color).
KEY_HARD = 70   # closer than this and connected to the border -> fully transparent
KEY_HOLE = 40   # this close anywhere (e.g. a gap between wing and body) -> transparent too
KEY_SOFT = 230  # edge pixels between HARD and SOFT get partial alpha
SPILL_RIM = 28  # green-keyed art: px from the edge where green spill is removed...
SPILL_FADE = 2.5  # ...and how fast green-dominated pixels there fade out
# Magenta-keyed art that needs the same spill pass (glows over magenta leave pink specks). Opt-in, so the
# subject's own pinks and purples near the edge stay untouched everywhere else.
MAGENTA_SPILL = {"icon_stormwatch_amulet", "mon_stormcoil", "mon_rime_acolyte", "mon_frostbinder"}
# Processed files fixed by hand afterwards (key-color spill in steam, glows and glass). A full run skips them, so the
# fix isn't lost; `--force` reprocesses them anyway (a new raw image for one of these: remove it from this list).
HAND_EDITED: set[str] = set()
# Transparent sources: alpha below this (0-255) is cleared. Qwen-Image 2.1 leaves values of 1-9 over about a
# third of the "empty" canvas, which would otherwise stretch the trim box to the whole image.
ALPHA_FLOOR = 10
EXTS = {".png", ".jpg", ".jpeg", ".webp"}


def art_file(key: str, res: int) -> Path:
    """Where a processed key goes: `<key>.webp` at 1x, `<key>@2x.webp` at 2x (src/ui/art.ts reads the suffix)."""
    return OUT / (f"{key}.webp" if res == 1 else f"{key}@{res}x.webp")


def pick_res(size: int, size_1x: int, max_res: int) -> int:
    """2x when the source has (nearly) enough pixels for it, else 1x."""
    return RES if max_res >= RES and size >= size_1x * RES * MIN_2X_SHARE else 1


def process_background(src: Path, key: str, max_res: int) -> Path:
    img = Image.open(src).convert("RGB")
    w, h = img.size
    if key in BG_KEEP_ASPECT:
        res = pick_res(h, BG_SIZE[1], max_res)
        img = img.resize((round(w * BG_SIZE[1] * res / h), BG_SIZE[1] * res), Image.LANCZOS)
    else:
        focus = BG_FOCUS.get(key, 0.5)
        target = BG_SIZE[0] / BG_SIZE[1]
        if w / h > target:
            new_w = round(h * target)
            x0 = round((w - new_w) * focus)
            img = img.crop((x0, 0, x0 + new_w, h))
        else:
            new_h = round(w / target)
            y0 = round((h - new_h) * focus)
            img = img.crop((0, y0, w, y0 + new_h))
        res = pick_res(img.width, BG_SIZE[0], max_res)
        img = img.resize((BG_SIZE[0] * res, BG_SIZE[1] * res), Image.LANCZOS)
    out = art_file(key, res)
    img.save(out, quality=85, method=6)
    return out


def _grow(seed: np.ndarray, allowed: np.ndarray) -> np.ndarray:
    """Flood-fill `seed` through `allowed` pixels (8-connected), using array shifts."""
    region = seed & allowed
    while True:
        grown = region.copy()
        grown[1:, :] |= region[:-1, :]
        grown[:-1, :] |= region[1:, :]
        grown[:, 1:] |= region[:, :-1]
        grown[:, :-1] |= region[:, 1:]
        grown[1:, 1:] |= region[:-1, :-1]
        grown[:-1, :-1] |= region[1:, 1:]
        grown[1:, :-1] |= region[:-1, 1:]
        grown[:-1, 1:] |= region[1:, :-1]
        grown &= allowed
        if (grown == region).all():
            return region
        region = grown


def _dilate(mask: np.ndarray, steps: int) -> np.ndarray:
    out = mask.copy()
    for _ in range(steps):
        m = out.copy()
        m[1:, :] |= out[:-1, :]
        m[:-1, :] |= out[1:, :]
        m[:, 1:] |= out[:, :-1]
        m[:, :-1] |= out[:, 1:]
        out = m
    return out


def _drop_specks(alpha: np.ndarray, min_share: float = 0.002) -> None:
    """Clear small detached blobs (stray marks in the background) so they don't stretch the trim box.
    Works on a 4x downsampled mask so a plain Python flood fill stays fast."""
    f = 4
    h, w = alpha.shape
    small = (alpha[: h - h % f, : w - w % f] > 0.3).reshape(h // f, f, w // f, f).any(axis=(1, 3))
    sh, sw = small.shape
    seen = np.zeros_like(small)
    min_cells = max(4, int(small.size * min_share))
    for y0 in range(sh):
        for x0 in range(sw):
            if not small[y0, x0] or seen[y0, x0]:
                continue
            stack, cells = [(y0, x0)], []
            seen[y0, x0] = True
            while stack:
                y, x = stack.pop()
                cells.append((y, x))
                for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                    if 0 <= ny < sh and 0 <= nx < sw and small[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(cells) < min_cells:
                for y, x in cells:
                    alpha[y * f : (y + 1) * f, x * f : (x + 1) * f] = 0


def _trim_box(alpha: np.ndarray, pad: int = 6) -> tuple[int, int, int, int]:
    h, w = alpha.shape
    ys, xs = np.nonzero(alpha > 0.05)
    if len(xs) == 0:
        raise ValueError("everything was removed; is the background too close to the character's colors?")
    return max(0, xs.min() - pad), max(0, ys.min() - pad), min(w, xs.max() + pad + 1), min(h, ys.max() + pad + 1)


# Key pose names (scripts/hero_poses.py): the epic ones and <weapon kind>_<beat>.
POSE_NAME = re.compile(r"_(charge|leap|land|(?:sword|axe|hammer|dagger|staff|wand)_(?:wind|step|strike))$")


def _outfit_base(key: str) -> Path | None:
    """Armor outfits (`char_<class>_<armor>`) are edits of the class's bare art in the same framing. Cropping them
    with the bare art's box keeps the fists at the same pixels, so the class's hand anchors stay valid."""
    parts = key.split("_", 2)
    if len(parts) < 3 or parts[0] != "char" or parts[2].startswith("bare") or POSE_NAME.search(key):
        return None
    return next((p for p in RAW.glob(f"char_{parts[1]}_bare.*") if p.suffix.lower() in EXTS), None)


def _pose_base(key: str) -> Path | None:
    """Key poses (`char_<class>_bare_<pose>`, and `char_<class>_<armor>_<pose>` in an outfit; scripts/hero_poses.py)
    come already cut to their canvas (feet on the bare art's baseline, centered on it) in the bare art's pixels: they
    are scaled like the bare art, not trimmed."""
    parts = key.split("_", 2)
    if len(parts) < 3 or parts[0] != "char" or not POSE_NAME.search(key):
        return None
    return next((p for p in RAW.glob(f"char_{parts[1]}_bare.*") if p.suffix.lower() in EXTS), None)


def process_sprite(src: Path, key: str, max_res: int) -> Path:
    source = Image.open(src)
    rgb = np.asarray(source.convert("RGB")).astype(np.float32)
    given_alpha = _real_alpha(source)

    # Outfits (see _outfit_base) are cut out and cropped like their bare art, with the same trim box.
    base_alpha = base_bg = None
    base = _outfit_base(key)
    if base is not None:
        base_src = Image.open(base)
        base_rgb = np.asarray(base_src.convert("RGB")).astype(np.float32)
        base_given = _real_alpha(base_src)
        if base_rgb.shape == rgb.shape and base_given is not None:
            base_alpha = base_given  # a transparent base (Qwen): its own alpha gives the trim box
        elif base_rgb.shape == rgb.shape:
            _, base_alpha, _, base_bg = _cutout(base_rgb, base.stem)
        else:
            print(f"  WARNING: {base.stem} is a different size, trimmed on its own (re-measure the hand anchors)")
    if given_alpha is not None:
        alpha = given_alpha
        clean = np.where(alpha[..., None] > 0, rgb, 0)
        key_color, bg = None, alpha == 0
    else:
        clean, alpha, key_color, bg = _cutout(rgb, key, base_bg)
    rgba = np.dstack([clean, alpha * 255]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA")

    box = _trim_box(alpha)
    pose_base = _pose_base(key)
    pose_height = None
    if pose_base is not None:
        # The bare art's own trim box sets the scale (its height becomes the standard height).
        bare_alpha = _real_alpha(Image.open(pose_base))
        if bare_alpha is None:
            sys.exit(f"{key}: the bare art {pose_base.name} needs a transparent background")
        bx0, by0, bx1, by1 = _trim_box(bare_alpha)
        pose_height = by1 - by0
        box = (0, 0, img.width, img.height)
        print(f"  a pose: kept its canvas, scaled like {pose_base.stem}")
    elif base_alpha is not None:
        box = _trim_box(base_alpha)
        x0, y0, x1, y1 = box
        outside = alpha.copy()
        outside[y0:y1, x0:x1] = 0
        clipped = int((outside > 0.5).sum())
        print(f"  cropped like {base.stem}" + (f"; {clipped} px fell outside and were cut" if clipped else ""))
    img = img.crop(box)

    if key in FLIP:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if key.startswith("icon_"):
        # Icons are fit into their box wherever they're drawn, so smaller ones stay small.
        res = pick_res(max(img.width, img.height), ICON_SIZE, max_res)
        scale = min(1.0, ICON_SIZE * res / max(img.width, img.height))
        if scale < 1:
            img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    elif pose_height is not None:
        res = RES if max_res >= RES else 1
        scale = SPRITE_HEIGHT * res / pose_height
        img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    else:
        # Exactly the standard height (hand anchors are pixels on it), scaled up a little if need be.
        max_h = WEAPON_HEIGHT if key.startswith("wpn_") else SPRITE_HEIGHT
        res = pick_res(img.height, max_h, max_res)
        if img.height != max_h * res:
            img = img.resize((round(img.width * max_h * res / img.height), max_h * res), Image.LANCZOS)

    out = art_file(key, res)
    img.save(out, quality=90, method=6)
    if key_color is None:
        print(f"  transparent source, alpha below {ALPHA_FLOOR} cleared ({bg.mean():.0%} of pixels empty)")
    else:
        print(f"  background color {key_color.round().astype(int).tolist()}, removed {bg.mean():.0%} of pixels")
    return out


def _real_alpha(img: Image.Image) -> np.ndarray | None:
    """The image's own alpha (0-1, faint noise and stray specks cleared) when it has a transparent background;
    None for opaque images, which go through the flat-color background removal instead."""
    if img.mode not in ("RGBA", "LA", "PA") and "transparency" not in img.info:
        return None
    a = np.asarray(img.convert("RGBA"))[..., 3]
    if (a < ALPHA_FLOOR).mean() < 0.01:
        return None
    alpha = np.where(a < ALPHA_FLOOR, 0, a).astype(np.float32) / 255
    _drop_specks(alpha)
    return alpha


def _cutout(rgb: np.ndarray, key: str, base_bg: np.ndarray | None = None) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """Remove the flat background: returns (color, alpha 0-1, background color, removed-background mask).
    `base_bg` is the removed background of the art this one was edited from (outfits): inside that art's body only
    near-exact key colors go, and pockets only where it had pockets. Muted keys sit close to skin and parchment
    shading, so one soft outline would otherwise let the background flood into a hand."""
    h, w, _ = rgb.shape

    # Background color = median of a thin border ring.
    ring = np.concatenate([rgb[:4].reshape(-1, 3), rgb[-4:].reshape(-1, 3), rgb[:, :4].reshape(-1, 3), rgb[:, -4:].reshape(-1, 3)])
    key_color = np.median(ring, axis=0)
    dist = np.sqrt(((rgb - key_color) ** 2).sum(axis=2))

    # Remove background-colored areas connected to the border, plus enclosed pockets that are
    # very close to the key color. Merely similar colors inside the character (eyes, gems) survive.
    border = np.zeros((h, w), bool)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    pockets = dist < KEY_HOLE
    allowed = dist < KEY_HARD
    if base_bg is not None:
        pockets &= _dilate(base_bg, 2)
        allowed &= ~(~_dilate(base_bg, 4) & (dist >= KEY_HOLE))
    bg = _grow(border | pockets, allowed)

    # Soft alpha on a thin band around the removed area for anti-aliased edges.
    alpha = np.ones((h, w), np.float32)
    band = _dilate(bg, 3) & ~bg
    alpha[band] = np.clip((dist[band] - KEY_HARD) / (KEY_SOFT - KEY_HARD), 0, 1)
    alpha[bg] = 0

    _drop_specks(alpha)

    # Remove background color bleeding into edge pixels.
    a = np.maximum(alpha, 1e-3)[..., None]
    clean = np.clip((rgb - (1 - a) * key_color) / a, 0, 255)
    clean = np.where(alpha[..., None] > 0, clean, 0)

    # Green-screen spill: glows and anti-aliasing over the green leave a lime rim along the silhouette.
    # Near the edge only (so the subject's own greens survive): the key's channels can't exceed the brightest
    # other channel, and the more key-colored a pixel, the more transparent it gets (a glow keeps the rest).
    green = key_color[1] > key_color[0] + 60 and key_color[1] > key_color[2] + 60
    magenta = key in MAGENTA_SPILL and min(key_color[0], key_color[2]) > key_color[1] + 60
    if green or magenta:
        keys, others = ([1], [0, 2]) if green else ([0, 2], [1])
        rim = _dilate(bg, SPILL_RIM) & ~bg
        other = clean[..., others].max(axis=2)
        spill = np.clip(clean[..., keys].min(axis=2) - other, 0, 255)
        alpha = np.where(rim, alpha * np.clip(1 - spill / 255 * SPILL_FADE, 0, 1), alpha)
        weakest = keys[0] if green else np.where(clean[..., 0] < clean[..., 2], 0, 2)
        for c in keys:
            fix = rim & (weakest == c)
            clean[..., c] = np.where(fix, np.minimum(clean[..., c], other), clean[..., c])

    return clean, alpha, key_color, bg


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    args = sys.argv[1:]
    force = "--force" in args
    max_res = 1 if "--1x" in args else RES
    wanted = {a for a in args if a not in ("--force", "--1x")}
    sources = sorted(p for p in RAW.iterdir() if p.suffix.lower() in EXTS)
    if wanted:
        sources = [p for p in sources if p.stem in wanted]
    if not sources:
        print(f"No images found in {RAW}")
        return
    for src in sources:
        key = src.stem
        if key in HAND_EDITED and not force:
            print(f"{src.name}: skipped, its processed file was touched up by hand (--force redoes it)")
            continue
        print(f"{src.name}")
        for old in [*OUT.glob(f"{key}.*"), *OUT.glob(f"{key}@*")]:
            old.unlink()
        out = process_background(src, key, max_res) if key.startswith("bg_") else process_sprite(src, key, max_res)
        with Image.open(out) as done:
            print(f"  -> {out.relative_to(ROOT)} ({done.width}x{done.height}, {out.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
