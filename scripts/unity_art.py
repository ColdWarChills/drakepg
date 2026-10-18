#!/usr/bin/env python3
"""Copy the game's art into the Unity project (docs/UNITY_PORT.md, step 4). Unity can't import WebP, so every file in
src/assets/art/ becomes a PNG (backgrounds: JPG) in unity/Assets/Resources/Art/, keyed like the web version
(`mon_mossling@2x.webp` -> `mon_mossling@2x.png`). The copies are generated, not committed (git-ignored): run this after
cloning and after `prepare_art.py`. Unity's import rules (pixels per unit from the @2x suffix, pivots) live in
unity/Assets/Editor/ArtImport.cs; unity/Assets/Resources/Art/index.json lists what exists for the game.

Sprites (everything but backgrounds) sit in the top-left corner of a power-of-two canvas: the web build only compresses
mipmapped textures with power-of-two sides (otherwise they stay raw RGBA, 4-16x bigger). index.json keeps each picture's
own size (w, h), and ArtImport cuts the sprite back out of the canvas at that size.

  python scripts/unity_art.py            # everything that changed
  python scripts/unity_art.py --force    # everything
"""
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src" / "assets" / "art"
OUT = ROOT / "unity" / "Assets" / "Resources" / "Art"


def pow2(n: int) -> int:
    """The smallest power of two >= n."""
    p = 1
    while p < n:
        p *= 2
    return p


def main() -> None:
    force = "--force" in sys.argv
    OUT.mkdir(parents=True, exist_ok=True)
    best: dict[str, Path] = {}
    for f in sorted(SRC.glob("*.webp")):
        name = f.stem
        key, _, res = name.partition("@")
        # Both sizes of one key: the sharper file wins (like src/ui/art.ts).
        if key not in best or "@" in name:
            best[key] = f
    # The index goes first: Unity's importer (ArtImport.cs) reads each key's resolution from it.
    index = {}
    for key, f in sorted(best.items()):
        w, h = Image.open(f).size
        index[key] = {"res": 2 if "@2x" in f.stem else 1, "w": w, "h": h}
    (OUT / "index.json").write_text(json.dumps(index, indent=1), encoding="utf-8")
    written = 0
    for key, f in sorted(best.items()):
        ext = ".jpg" if key.startswith("bg_") else ".png"
        out = OUT / f"{key}{ext}"
        if force or not out.exists() or out.stat().st_mtime < f.stat().st_mtime:
            im = Image.open(f)
            if ext == ".jpg":
                im.convert("RGB").save(out, "JPEG", quality=88)
            else:
                im = im.convert("RGBA")
                canvas = Image.new("RGBA", (pow2(im.width), pow2(im.height)), (0, 0, 0, 0))
                canvas.paste(im, (0, 0))
                canvas.save(out, "PNG", optimize=True)
            written += 1
    # Stale copies of art that was removed.
    for old in OUT.glob("*.*"):
        if old.suffix in (".png", ".jpg") and old.stem not in best:
            old.unlink()
            meta = old.with_suffix(old.suffix + ".meta")
            if meta.exists():
                meta.unlink()
    print(f"{written} written, {len(index)} keys in {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
