#!/usr/bin/env python3
"""Hair and headband as separate sprites (docs/PLANS.md, Art: character creator).

Every hairstyle is a Qwen edit of the same anchor (scripts/hero_edits.py), so it lines up pixel for pixel with the
shaved-head edit, which is the base body. A hair layer is what differs from that base in the head area; the headband
is what differs between the base and an edit of it wearing the headband, kept where it is red. Writes the layers to
art/tests/layers/ and two sheets: layers.png (the base and each layer alone) and layers_combos.png (every hairstyle
with and without the headband, composed from the layers).

  python scripts/hair_layers.py
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import TESTS  # noqa: E402

RAW = TESTS / "poster_color" / "_raw"
OUT = TESTS / "layers"
BASE = "edit_hair_shaved_s1831856038"
HAIRS = {  # label: raw stem (the picked seed of each hair edit)
    "Messy": "edit_noband_s1831856038",
    "Long, tied": "edit_hair_long_s1831856038",
    "Undercut": "edit_hair_undercut_s1831856038",
    # Worked back from the combo (its headband removed), so plain and squeezed share one curl style (replaced the first
    # curly edit, edit_hair_curly_s1831856040).
    "Curly": "edit_puffy_s1831856049",
    "Top knot": "edit_hair_bun_s1831856038",
    "Bob": "edit_hair_bob_s1831856039",
}
# Combo sprites: a hairstyle redrawn with one accessory (the easter egg: curly hair squeezed under the headband). When
# both are worn the game draws the combo sprite instead of the hair and the accessory sprites (the accessory is drawn
# in it), so the edit may move the band a little and let curls overlap it.
COMBOS = {("Curly", "headband"): "edit_curly_banded_s1831856048"}
# The headband drawn onto the base itself (no hair over it), so it comes out whole.
BAND_WITH, BAND_WITHOUT = "edit_band_on_shaved_s1831856038", BASE


def load(stem: str) -> np.ndarray:
    a = np.asarray(Image.open(RAW / f"{stem}.png").convert("RGBA")).astype(np.int16)
    a[a[..., 3] < 10] = 0
    return a


def head_rows(base: np.ndarray) -> int:
    """Bottom of the head area: a bit below the shoulders' top (long hair and ponytails reach the shoulders)."""
    rows = np.nonzero(base[..., 3].max(axis=1) > 0)[0]
    return int(rows[0] + 0.36 * (rows[-1] - rows[0]))


def differs(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    alpha = np.abs(a[..., 3] - b[..., 3]) > 60
    color = (np.abs(a[..., :3] - b[..., :3]).sum(axis=2) > 75) & (a[..., 3] > 128)
    return alpha | color


def clean(mask: np.ndarray, min_area: int) -> np.ndarray:
    """Close small gaps, drop specks (face drift between edits), feather the edge."""
    img = Image.fromarray((mask * 255).astype(np.uint8))
    img = img.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    m = np.asarray(img) > 127
    # Drop small connected specks with a cheap labelling pass on a downscaled copy.
    small = Image.fromarray((m * 255).astype(np.uint8)).resize((m.shape[1] // 4, m.shape[0] // 4), Image.BOX)
    s = np.asarray(small) > 64
    keep = np.zeros_like(s)
    seen = np.zeros_like(s)
    h, w = s.shape
    for y0, x0 in zip(*np.nonzero(s)):
        if seen[y0, x0]:
            continue
        stack, pts = [(y0, x0)], []
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            pts.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and s[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    stack.append((yy, xx))
        if len(pts) * 16 >= min_area:
            for y, x in pts:
                keep[y, x] = True
    keep_big = np.asarray(Image.fromarray((keep * 255).astype(np.uint8)).resize((m.shape[1], m.shape[0]), Image.NEAREST)
                          .filter(ImageFilter.MaxFilter(9))) > 127
    m = m & keep_big
    soft = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    return np.asarray(soft).astype(np.float32) / 255


def layer(src: np.ndarray, alpha: np.ndarray) -> Image.Image:
    out = src.copy()
    out[..., 3] = (src[..., 3] * alpha).astype(np.int16)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


def hair_layer(stem: str, base: np.ndarray, bottom: int) -> Image.Image:
    src = load(stem)
    m = differs(src, base)
    m[bottom:] = False
    return layer(src, clean(m, 2500))


def strict_red(a: np.ndarray) -> np.ndarray:
    """The headband's red only (skin passes a looser red test)."""
    return (a[..., 0] > 120) & (a[..., 1] < 90) & (a[..., 2] < 90) & (a[..., 0] - a[..., 1] > 70) & (a[..., 3] > 128)


def combo_layer(stem: str, bottom: int) -> Image.Image:
    """The whole head of an edit that already wears the accessory, fading out across the shoulders. Cutting only the
    hair left notches and outline stubs where hair met the band; the edit's own head is clean, and as an edit of the
    same base its face and neck match the base. It replaces both the hair and the accessory sprite."""
    src = load(stem)
    fade = 48
    rows = np.clip((bottom - np.arange(src.shape[0])) / fade, 0, 1)[:, None]
    return layer(src, np.broadcast_to(rows, src.shape[:2]).astype(np.float32))


def band_layer(bottom: int) -> Image.Image:
    a, b = load(BAND_WITH), load(BAND_WITHOUT)
    m = differs(a, b)
    m[bottom:] = False
    red = (a[..., 0] - np.maximum(a[..., 1], a[..., 2]) > 35) & (a[..., 3] > 128)
    near_red = np.asarray(Image.fromarray((red * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 127
    return layer(a, clean(m & near_red, 1500))


def trim_box(images: list[Image.Image]) -> tuple[int, int, int, int]:
    boxes = [im.getbbox() for im in images if im.getbbox()]
    return min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    base_arr = load(BASE)
    bottom = head_rows(base_arr)
    base = Image.fromarray(base_arr.astype(np.uint8), "RGBA")
    base.save(OUT / "base_body.png")
    hairs = {label: hair_layer(stem, base_arr, bottom) for label, stem in HAIRS.items()}
    for label, im in hairs.items():
        im.save(OUT / f"hair_{label.split(',')[0].lower().replace(' ', '_')}.png")
    band = band_layer(bottom)
    band.save(OUT / "headband.png")
    combos = {key: combo_layer(stem, bottom) for key, stem in COMBOS.items()}
    for (hair_label, acc), im in combos.items():
        im.save(OUT / f"hair_{hair_label.lower()}__{acc}.png")

    font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 24)
    head_box = trim_box([base.crop((0, 0, base.width, bottom))] + [h.crop((0, 0, base.width, bottom)) for h in hairs.values()]
                        + [band.crop((0, 0, base.width, bottom))])
    pad = 20
    head_box = (head_box[0] - pad, max(0, head_box[1] - pad), head_box[2] + pad, bottom)

    def tile(im: Image.Image, label: str, size: int = 300, checker: bool = False) -> Image.Image:
        crop = im.crop(head_box)
        s = size / max(crop.size)
        crop = crop.resize((round(crop.width * s), round(crop.height * s)), Image.LANCZOS)
        t = Image.new("RGBA", (size, size + 40), (232, 234, 237, 255))
        if checker:
            d = ImageDraw.Draw(t)
            for y in range(40, size + 40, 20):
                for x in range(0, size, 20):
                    if (x // 20 + y // 20) % 2:
                        d.rectangle((x, y, x + 19, y + 19), fill=(214, 218, 224, 255))
        t.alpha_composite(crop, ((size - crop.width) // 2, 40 + (size - crop.height) // 2))
        ImageDraw.Draw(t).text((10, 8), label, font=font, fill=(30, 32, 39))
        return t.convert("RGB")

    def sheet(rows: list[list[Image.Image]], titles: list[str], path: Path) -> None:
        head = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 32)
        w = max(sum(t.width + 10 for t in r) + 10 for r in rows)
        h = sum(r[0].height + 60 for r in rows) + 10
        out = Image.new("RGB", (w, h), (210, 214, 221))
        y = 0
        for r, title in zip(rows, titles):
            ImageDraw.Draw(out).text((14, y + 12), title, font=head, fill=(30, 32, 39))
            x = 10
            for t in r:
                out.paste(t, (x, y + 55))
                x += t.width + 10
            y += r[0].height + 60
        out.save(path)

    # The layers alone.
    sheet([[tile(base, "Base body (shaved)", checker=True)] + [tile(h, label, checker=True) for label, h in hairs.items()]
           + [tile(band, "Headband", checker=True)]],
          ["Layers: one base body, separate hair sprites, an optional headband"], TESTS / "layers.png")

    # Composed: base + hair (+ headband).
    def compose(h: Image.Image | None, with_band: bool) -> Image.Image:
        c = base.copy()
        if h is not None:
            c.alpha_composite(h)
        if with_band:
            c.alpha_composite(band)
        return c

    no_band = [tile(compose(None, False), "Shaved")] + [tile(compose(h, False), label) for label, h in hairs.items()]
    def with_headband(label: str, h: Image.Image) -> Image.Image:
        """As the game would draw it: a combo sprite replaces the hair and the headband when there is one."""
        if (label, "headband") not in COMBOS:
            return compose(h, True)
        c = base.copy()
        c.alpha_composite(combo_layer(COMBOS[(label, "headband")], bottom))
        return c

    with_band = [tile(compose(None, True), "Shaved")] + [
        tile(with_headband(label, h), label + (" (combo)" if (label, "headband") in COMBOS else ""))
        for label, h in hairs.items()]
    sheet([no_band, with_band], ["Composed from layers: no headband", "Same hair + the headband layer"],
          TESTS / "layers_combos.png")

    # The combo swap: the plain hair, the plain layers with the headband, and the combo sprite (which replaces both).
    rows, titles = [], []
    for (hair_label, acc), stem in COMBOS.items():
        swapped = base.copy()
        swapped.alpha_composite(combo_layer(stem, bottom))
        rows.append([tile(compose(hairs[hair_label], False), hair_label), tile(compose(hairs[hair_label], True),
                     "Plain layers"), tile(swapped, "Combo sprite"), tile(hairs[hair_label], f"{hair_label} layer alone",
                     checker=True)])
        titles.append(f"Easter egg: {hair_label} + {acc} swaps in the squeezed-hair sprite")
    sheet(rows, titles, TESTS / "layers_easter_egg.png")
    print("art/tests/layers/, art/tests/layers.png, art/tests/layers_combos.png, art/tests/layers_easter_egg.png")


if __name__ == "__main__":
    main()
