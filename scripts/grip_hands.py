#!/usr/bin/env python3
"""Gripping hands for the base hero (docs/IMPROVEMENTS_2026-10.md, "Weapons held in the fist"). The hero's fists are
closed, with no room for a handle, so no crop of them can hold a weapon. This redraws the raised fist as a hand that
grips a handle, with the local Qwen (ComfyUI), as edits of the hero's own art so the style matches:

  rod    the fist gripping a flat magenta rod straight through it (the rod marks the grip line and what the fingers cover)
  open   the picked rod try with the rod removed (to check: it gives back the closed fist, so the art is the palm)
  layers the fingers sprite from the picked rod try (src/assets/art/part_hero_fingers@2x.webp) and the hand data

  python scripts/grip_hands.py rod --seeds 3
  python scripts/grip_hands.py open <rod seed> --seeds 2
  python scripts/grip_hands.py layers <rod seed>

Tries go to art/redo/grip/ (git-ignored) with a sheet per step.
"""
import io
import random
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "art" / "redo" / "grip"
BODY = ROOT / "src" / "assets" / "art" / "char_hero_bare@2x.webp"
# The raised fist's box in the @2x art (the fist is about x 428-503, y 227-298) with some forearm, and the upscale
# the edit works at.
BOX = (380, 190, 540, 350)
UP = 6
STEPS = 20

ROD = (
    "Edit <image1>. It shows a cartoon hero's raised right fist and forearm on a transparent background. Change only the "
    "hand: make the fist tightly grip a straight, round rod of flat, solid bright magenta color (pure #FF00FF, no shading). "
    "The rod is held upright and runs straight through the fist, square to the curled fingers: it comes out above the fist "
    "between the thumb and the index finger and below the fist right under the little finger, and it reaches past the top "
    "and the bottom of the picture. The four fingers curl around the rod and cover it; the thumb wraps around over the "
    "index finger. The rod is very thick, half as wide as the whole fist, like a thick wooden staff. Keep the hand the same size and at the same place, with the "
    "same skin color, the same dark outlines and the same flat cartoon painting style. Keep the forearm exactly the same."
)
OPEN = (
    "Edit <image1>. Remove the magenta rod completely. Keep the hand exactly as it is, still curled around the now empty "
    "space where the rod was: where the rod covered the palm, show the palm and the inside of the fingers. Keep everything "
    "else exactly the same: the same size, place, skin color, outlines and flat cartoon painting style."
)


def crop() -> Image.Image:
    body = Image.open(BODY).convert("RGBA")
    c = body.crop(BOX)
    return c.resize((c.width * UP, c.height * UP), Image.LANCZOS)


def sheet(paths: list[Path], out: Path) -> None:
    ims = [Image.open(p).convert("RGBA") for p in paths]
    w, h = ims[0].size
    s = Image.new("RGB", (w * len(ims), h), (70, 120, 90))
    for i, im in enumerate(ims):
        s.paste(im, (i * w, 0), im)
    s.thumbnail((2400, 2400))
    s.save(out)
    print(out)


def run(step: str, source: Image.Image, prompt: str, seeds: list[int]) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    buf = io.BytesIO()
    source.save(buf, "PNG")
    ref = comfy.upload(f"grip_{step}_src.png", buf.getvalue())
    done = []
    for seed in seeds:
        data = comfy.generate(f"{RGBA_OPEN} {prompt} {RGBA_CLOSE}", [ref], None, seed, STEPS)
        im = Image.open(io.BytesIO(data)).convert("RGBA")
        # Qwen leaves faint alpha (1-9) on much of the background.
        a = im.getchannel("A").point(lambda v: 0 if v < 10 else v)
        im.putalpha(a)
        path = OUT / f"{step}_s{seed}.png"
        im.save(path)
        (OUT / f"{step}_s{seed}.txt").write_text(f"{prompt}\nseed {seed}, steps {STEPS}\n", encoding="utf-8")
        print(path, im.size)
        done.append(path)
    sheet(done, OUT / f"_{step}.png")


FINGERS = ROOT / "src" / "assets" / "art" / "part_hero_fingers@2x.webp"
# Half the widest common handle (staffs, about 19 px) plus room, in 1x px from the grip line.
BAND = 12


def layers(seed: str) -> None:
    """The fingers sprite from a rod try: the hand in front of the rod, near the grip line, without the rod. The hero's
    own art is the palm behind the handle; Unity draws body, handle, then this. Prints the hand data for classes.ts."""
    import numpy as np

    im = np.asarray(Image.open(OUT / f"rod_s{seed}.png").convert("RGBA")).astype(float)
    r, g, b, a = im[..., 0], im[..., 1], im[..., 2], im[..., 3]
    rod = (r > 180) & (g < 90) & (b > 180) & (a > 128)
    # The grip line: the rod's long axis.
    ys, xs = np.nonzero(rod)
    pts = np.stack([xs, ys], 1).astype(float)
    c = pts.mean(0)
    ev, evec = np.linalg.eigh(np.cov((pts - c).T))
    d = evec[:, np.argmax(ev)]
    if d[1] < 0:
        d = -d
    n = np.array([-d[1], d[0]])
    t, w = (pts - c) @ d, (pts - c) @ n
    half = (np.percentile(w, 99) - np.percentile(w, 1)) / 2
    # Where the rod goes under the fingers and comes back out: the biggest gap along its middle.
    tt = np.sort(t[np.abs(w) < half * 0.5])
    gap = int(np.argmax(np.diff(tt)))
    t_in, t_out = tt[gap], tt[gap + 1]
    # Where the rod comes back out at the edges of its width (the fingers' lower edge there).
    def out_at(wb):
        sel = (np.abs(w - wb) < 3) & (t > (t_in + t_out) / 2)
        return t[sel].min() if sel.any() else t_out
    edge_l, edge_r = out_at(-half * 0.85), out_at(half * 0.85)

    H, W = a.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    pt = (xx - c[0]) * d[0] + (yy - c[1]) * d[1]
    pw = (xx - c[0]) * n[0] + (yy - c[1]) * n[1]
    px = UP * 2  # 6x px per 1x px
    keep = np.clip((BAND * px - np.abs(pw)) / (2 * px), 0, 1)  # the band around the grip line, soft sides
    # Across the rod the edge is where the rod shows (the drawn fingertip curve); beside it, the edge carries on flat
    # from the rod's sides, softly (it lies over the same skin in the hero's art).
    beside = np.abs(pw) > half
    flat = np.where(pw < 0, edge_l, edge_r)
    keep *= np.where(beside, np.clip((flat - pt) / (2 * px), 0, 1), 1)
    keep *= np.clip((t_out + 8 * px - pt) / px, 0, 1)  # nothing below the rod's way out
    # No rod, and none of its fringe: magenta-ish pixels go, and the rest lose the magenta spill (skin and outlines
    # have less blue than green).
    pink = np.clip(((r - g) + (b - g) - 120) / 120, 0, 1)
    keep *= 1 - np.maximum(rod, pink)
    out = im.copy()
    # The hand's edge against the rod is a mix of outline and magenta: repaint a thin border (about 1 px at 2x)
    # beside the rod in the hand's dark brown outline color, so the fingers end in a clean drawn line.
    near = np.zeros_like(rod)
    reach = 5
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            if dx * dx + dy * dy <= reach * reach:
                near |= np.roll(np.roll(rod, dy, 0), dx, 1)
    border = near & ~rod
    out[border, 0], out[border, 1], out[border, 2] = 92, 56, 34
    out[..., 2] = np.minimum(out[..., 2], out[..., 1])
    out[..., 3] = a * keep
    # Premultiply for the downscale (no dark halo), then back.
    pre = out.copy()
    pre[..., :3] *= pre[..., 3:4] / 255
    small = Image.fromarray(pre.astype(np.uint8), "RGBA").resize((W // UP, H // UP), Image.LANCZOS)
    s = np.asarray(small).astype(float)
    s[..., :3] = np.where(s[..., 3:4] > 0, s[..., :3] * 255 / np.maximum(s[..., 3:4], 1), 0)
    small = Image.fromarray(np.clip(s, 0, 255).astype(np.uint8), "RGBA")
    small.save(FINGERS, "WEBP", lossless=True)
    print(FINGERS, small.size)

    def to1x(p):
        return (BOX[0] + p[0] / UP) / 2, (BOX[1] + p[1] / UP) / 2

    angle = -np.degrees(np.arctan2(d[0], d[1]))
    enter = np.array(to1x(c + d * t_in))
    hand = enter + d * 20  # the guard rests on the fist: 20 px up the line from the hand point
    print(f"hand: x {hand[0]:.1f}, y {hand[1]:.1f}, angle {angle:.1f}; fingers sprite top-left {BOX[0] / 2:.0f}, {BOX[1] / 2:.0f} (1x px)")


def main() -> None:
    args = sys.argv[1:]
    n = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 2
    seeds = [random.randrange(1, 2**31) for _ in range(n)]
    if args and args[0] == "rod":
        run("rod", crop(), ROD, seeds)
    elif args and args[0] == "open":
        src = Image.open(OUT / f"rod_s{args[1]}.png").convert("RGBA")
        run("open", src, OPEN, seeds)
    elif args and args[0] == "layers":
        layers(args[1])
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
