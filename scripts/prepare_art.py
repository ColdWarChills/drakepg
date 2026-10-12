#!/usr/bin/env python3
"""Turn raw generated images in art/raw/ into game-ready files in src/assets/art/.

File names decide what happens (see art/PROMPTS.md for the list):
  bg_*.png/jpg  -> background: center-cropped to 16:9, resized to 1280x720, saved as WebP.
  anything else -> sprite: flat background color removed, trimmed, resized to 480px tall, saved as WebP.

Usage:
  python3 scripts/prepare_art.py            # process everything in art/raw/
  python3 scripts/prepare_art.py mon_mossling bg_thornwood   # only these keys

Needs Pillow and numpy.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "art" / "raw"
OUT = ROOT / "src" / "assets" / "art"

BG_SIZE = (1280, 720)
SPRITE_HEIGHT = 480

# Sprites that came out facing left. Game art faces right; the engine mirrors enemies itself.
FLIP: set[str] = set()

# Background removal thresholds (RGB distance from the detected background color).
KEY_HARD = 70   # closer than this and connected to the border -> fully transparent
KEY_SOFT = 230  # edge pixels between HARD and SOFT get partial alpha
EXTS = {".png", ".jpg", ".jpeg", ".webp"}


def process_background(src: Path, key: str) -> Path:
    img = Image.open(src).convert("RGB")
    w, h = img.size
    target = BG_SIZE[0] / BG_SIZE[1]
    if w / h > target:
        new_w = round(h * target)
        img = img.crop(((w - new_w) // 2, 0, (w - new_w) // 2 + new_w, h))
    else:
        new_h = round(w / target)
        img = img.crop((0, (h - new_h) // 2, w, (h - new_h) // 2 + new_h))
    img = img.resize(BG_SIZE, Image.LANCZOS)
    out = OUT / f"{key}.webp"
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


def process_sprite(src: Path, key: str) -> Path:
    rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
    h, w, _ = rgb.shape

    # Background color = median of a thin border ring.
    ring = np.concatenate([rgb[:4].reshape(-1, 3), rgb[-4:].reshape(-1, 3), rgb[:, :4].reshape(-1, 3), rgb[:, -4:].reshape(-1, 3)])
    key_color = np.median(ring, axis=0)
    dist = np.sqrt(((rgb - key_color) ** 2).sum(axis=2))

    # Only remove background-colored pixels connected to the image border,
    # so matching colors inside the character (eyes, gems) survive.
    border = np.zeros((h, w), bool)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    bg = _grow(border, dist < KEY_HARD)

    # Soft alpha on a thin band around the removed area for anti-aliased edges.
    alpha = np.ones((h, w), np.float32)
    band = _dilate(bg, 3) & ~bg
    alpha[band] = np.clip((dist[band] - KEY_HARD) / (KEY_SOFT - KEY_HARD), 0, 1)
    alpha[bg] = 0

    # Remove background color bleeding into edge pixels.
    a = np.maximum(alpha, 1e-3)[..., None]
    clean = np.clip((rgb - (1 - a) * key_color) / a, 0, 255)
    clean = np.where(alpha[..., None] > 0, clean, 0)

    rgba = np.dstack([clean, alpha * 255]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA")

    # Trim to content.
    ys, xs = np.nonzero(alpha > 0.05)
    if len(xs) == 0:
        raise ValueError(f"{src.name}: everything was removed; is the background too close to the character's colors?")
    pad = 6
    img = img.crop((max(0, xs.min() - pad), max(0, ys.min() - pad), min(w, xs.max() + pad + 1), min(h, ys.max() + pad + 1)))

    if key in FLIP:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if img.height > SPRITE_HEIGHT:
        img = img.resize((round(img.width * SPRITE_HEIGHT / img.height), SPRITE_HEIGHT), Image.LANCZOS)

    out = OUT / f"{key}.webp"
    img.save(out, quality=90, method=6)
    print(f"  background color {key_color.round().astype(int).tolist()}, removed {bg.mean():.0%} of pixels")
    return out


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    wanted = set(sys.argv[1:])
    sources = sorted(p for p in RAW.iterdir() if p.suffix.lower() in EXTS)
    if wanted:
        sources = [p for p in sources if p.stem in wanted]
    if not sources:
        print(f"No images found in {RAW}")
        return
    for src in sources:
        key = src.stem
        print(f"{src.name}")
        for old in OUT.glob(f"{key}.*"):
            old.unlink()
        out = process_background(src, key) if key.startswith("bg_") else process_sprite(src, key)
        print(f"  -> {out.relative_to(ROOT)} ({out.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
