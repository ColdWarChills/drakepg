#!/usr/bin/env python3
"""Epic stances for the hero's top skills (docs/PLANS.md, "fighters that animate"): full-body key poses of the base
hero as Qwen edits of his own art (local ComfyUI), so face, clothes and style stay his. Bending (unity World/Bend.cs)
can't move arms and legs into new poses; these are swapped in for a skill's charge-up, its leap and its landing.

The base (art/raw/char_hero_bare.png) is put on a wider square canvas at the same pixel size, so a wide stance or
raised arms fit without Qwen shrinking him; picks are cut out at that same scale with the feet on the base's baseline
and centered where the base is, so the game can swap pictures without the hero jumping or changing size.

  python scripts/hero_poses.py gen charge leap land --seeds 3    # tries -> art/redo/pose/<pose>_s<seed>.png
  python scripts/hero_poses.py gen sword_ dagger_ --seeds 2       # every pose whose name starts with these
  python scripts/hero_poses.py sheet                             # every try side by side -> art/redo/pose/_sheet.png
  python scripts/hero_poses.py pick charge:4243 leap:4242        # cut to the game canvas -> art/raw + prepare_art
"""
import io
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "art" / "redo" / "pose"
RAW = ROOT / "art" / "raw"
BASE = RAW / "char_hero_bare.png"
FIRST_SEED = 4242
# The edit canvas: the base hero (1024x1536) centered on a square this wide, same pixel size.
CANVAS = 1536
ALPHA_FLOOR = 10

KEEP = ("Keep everything else exactly the same: the same young hero with the same face, messy dark brown hair, red "
        "headband with two tails, cream shirt with rolled sleeves, brown belt with a square buckle, dark teal trousers "
        "and brown boots, the same size and body proportions, the same colours and the same flat cartoon art style with "
        "hard-edged cel shadows, thin dark outlines and a warm golden rim light along his edges.")
EMPTY = ("Both hands are closed into empty fists with nothing in them: no sword, no weapon, no staff anywhere. "
         "No ground, no shadow, no dust, no effects, no text.")
# Casting poses: the weapon hand is an empty fist, the other an open hand.
CASTING = ("The hand that would hold the staff is closed into an empty fist with nothing in it; the other hand is open with "
           "the fingers spread. No staff, no wand, no weapon anywhere. No ground, no shadow, no magic effects, no text.")
# Poses that use CASTING instead of EMPTY.
OPEN_HAND = set()
POSES = {
    # SkillCharge: gathering power before the blow, the weapon cocked back over the shoulder.
    "charge": ("a low, wide power stance, gathering all his strength for a mighty blow: three-quarter view facing "
               "right, feet planted far apart, knees deeply bent, his weight sunk low and a little back; the arm on the "
               "right side of the picture is pulled back with its fist raised beside his head, at the height of his "
               "ear, as if holding a big sword cocked back over his shoulder; the other arm is bent in front of his "
               "chest with a clenched fist; a fierce, focused face with gritted teeth"),
    # The heavy strike's leap: airborne, both fists raised together high overhead for the slam.
    "leap": ("leaping high into the air for an overhead slam: three-quarter view facing right, both knees pulled up "
             "under him, back arched, both arms raised high above his head with both fists together, one fist "
             "right on top of the other, as if gripping one big sword with both hands high overhead; a fierce shout"),
    # The heavy strike's landing: the blow has come down, a deep lunge, both fists low in front.
    "land": ("landing a huge downward blow: three-quarter view facing right, a deep forward lunge, the front knee "
             "bent, the back leg stretched far behind him, his body leaning forward and low, both arms swung down "
             "in front of him with both fists together, one fist right next to the other, low in front of his front "
             "knee, as if a big sword in both hands has just struck the ground; a fierce shout"),
    # --- A blow per weapon kind (BattleScreen.WeaponBlow): <kind>_wind (anticipation, held), <kind>_step (the lunge
    # in), <kind>_strike (contact and follow-through). ---
    "sword_wind": ("coiled to swing a sword: three-quarter view facing right, feet apart, knees bent, his weight back on "
                   "the rear foot and his shoulders turned away to the left; his sword arm is pulled back and up so its "
                   "fist is high behind his head, above and behind his rear shoulder, as if a sword is cocked back over "
                   "his shoulder ready to swing; his other arm reaches forward and low with a clenched fist for balance; "
                   "a focused, determined face"),
    "sword_step": ("striding in to swing a sword: three-quarter view facing right, a long step forward, the front leg "
                   "reaching ahead with the knee bent and the back foot pushing off on its toes, his body leaning "
                   "forward; his sword fist is still raised high beside and behind his head, as if a sword is held back "
                   "over his shoulder; his other arm swings back behind him; a fierce face"),
    "sword_strike": ("the end of a big downward diagonal sword slash: three-quarter view facing right, a deep forward "
                     "lunge with the front knee bent and the back leg stretched far out behind, his upper body bent far "
                     "forward; his sword arm hangs straight down in front of his front knee, the fist low near the "
                     "ground, as if the sword has just cut down through the enemy and its tip points at the ground; his "
                     "other arm is flung up and back behind him; a fierce shout"),
    "axe_wind": ("raising a heavy axe for an overhead chop: three-quarter view facing right, feet planted wide apart, "
                 "leaning back, both arms raised straight up high above and slightly behind his head with both fists "
                 "together, one fist right on top of the other, as if gripping one long axe handle with both hands, "
                 "ready to chop straight down; gritted teeth"),
    "axe_step": ("stepping in with an axe raised overhead: three-quarter view facing right, a big stride forward, the "
                 "front knee bent and the back foot pushing off, his body leaning forward; both arms still raised high "
                 "above his head with both fists together, one fist on top of the other, as if holding an axe up in "
                 "both hands; a fierce face"),
    "axe_strike": ("the end of a heavy overhead chop: three-quarter view facing right, a deep forward lunge, his body "
                   "bent forward over his front knee, both arms brought straight down in front of him with both fists "
                   "together, one fist right on top of the other, just above the ground in front of his front foot, as "
                   "if an axe in both hands has just chopped down; a fierce shout"),
    "hammer_wind": ("winding up a huge war hammer: three-quarter view facing right, a low wide crouch, his body twisted "
                    "far away to the left, both fists together, one right on top of the other, low behind his back hip "
                    "on the left side of the picture, as if a heavy hammer in both hands is drawn far back behind him; "
                    "straining, gritted teeth"),
    "hammer_strike": ("slamming a huge hammer into the ground: three-quarter view facing right, crouched very low with "
                      "both knees deeply bent and his feet wide apart, his body bent far forward, both arms straight "
                      "down in front of him with both fists together, one right on top of the other, touching the "
                      "ground in front of his front foot, as if a hammer in both hands has just smashed into the "
                      "ground; a fierce shout"),
    "dagger_wind": ("crouched low, ready to pounce with two daggers: three-quarter view facing right, knees deeply "
                    "bent, his body low and leaning forward, one fist pulled back beside his hip ready to stab "
                    "forward, the other fist held low in front of him; a sly, focused face"),
    "dagger_step": ("dashing forward low and fast: three-quarter view facing right, a long low running stride, his "
                    "body leaning far forward, the front foot reaching ahead and the back foot kicking up behind him, "
                    "both fists low near his hips, as if holding two daggers ready; a focused face"),
    "dagger_strike": ("a straight thrusting stab: three-quarter view facing right, a deep forward lunge with the front "
                      "knee bent and the back leg stretched behind, one arm stretched straight out forward to the right "
                      "at chest height with the fist leading, as if stabbing with a dagger; the other fist pulled back "
                      "beside his hip; a fierce face"),
    "staff_wind": ("gathering magic before a spell: three-quarter view facing right, feet apart, standing tall, his "
                   "staff arm raised high with its fist above his head, as if holding a tall staff up toward the sky; "
                   "his other hand is open with the fingers spread, held out in front of his chest; an intense, "
                   "focused face"),
    "staff_step": ("stepping forward into a spell: three-quarter view facing right, a stride forward with the front "
                   "knee bent, his body leaning in, his staff fist swinging down and forward from above his head at "
                   "the height of his forehead; his other hand open, reaching forward; an intense face"),
    "staff_strike": ("casting a spell: three-quarter view facing right, a forward lunge, his staff arm thrust straight "
                     "out forward at shoulder height with the fist leading, as if pointing a staff at an enemy; his "
                     "other hand open with the fingers spread, pushed forward beside it; a shout"),
    "wand_wind": ("about to flick a wand: three-quarter view facing right, his weight on the rear foot, his wand arm "
                  "bent with its fist raised beside his ear, cocked back; his other hand open, held out in front of "
                  "him; a sly, focused face"),
    "wand_step": ("a quick step into a spell: three-quarter view facing right, a short step forward, leaning in, his "
                  "wand fist coming forward at head height; his other hand open, pulled back beside his chest; an "
                  "intense face"),
    "wand_strike": ("flicking a wand at an enemy: three-quarter view facing right, leaning forward on the front foot, "
                    "his wand arm stretched out forward and a little up with the fist leading, as if snapping a wand "
                    "toward an enemy; his other hand open, held back beside his hip; a confident face"),
}
OPEN_HAND.update(k for k in POSES if k.startswith(("staff_", "wand_")))


def base_canvas() -> Image.Image:
    """The base hero on the square edit canvas: same pixel size, centered sideways, same height from the bottom."""
    base = Image.open(BASE).convert("RGBA")
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    canvas.alpha_composite(base, ((CANVAS - base.width) // 2 + offset_x(), CANVAS - base.height))
    return canvas


def figure_box(img: Image.Image) -> tuple[int, int, int, int]:
    a = np.asarray(img.convert("RGBA"))[..., 3] >= ALPHA_FLOOR
    ys, xs = np.nonzero(a)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def offset_x() -> int:
    """Shift that centers the base figure (not its canvas) on the edit canvas."""
    base = Image.open(BASE).convert("RGBA")
    x0, _, x1, _ = figure_box(base)
    return base.width // 2 - (x0 + x1) // 2


def on_white(img: Image.Image) -> bytes:
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def prompt(pose: str) -> str:
    hands = CASTING if pose in OPEN_HAND else EMPTY
    return (f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {POSES[pose]}. {KEEP} {hands} "
            f"{RGBA_CLOSE}")


def gen(poses: list[str], seeds: int, first: int) -> None:
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    OUT.mkdir(parents=True, exist_ok=True)
    canvas = base_canvas()
    canvas.save(OUT / "_base.png")
    ref = comfy.upload("drakepg_pose_base.png", on_white(canvas))
    for pose in poses:
        for i in range(seeds):
            seed = first + i
            out = OUT / f"{pose}_s{seed}.png"
            if out.exists():
                continue
            print(out.relative_to(ROOT), flush=True)
            try:
                out.write_bytes(comfy.generate(prompt(pose), [ref], None, seed, 30))
            except RuntimeError as e:
                print(f"  ComfyUI error: {e}", flush=True)
                continue
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


def cleaned(path: Path) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    a = img.getchannel("A").point(lambda v: 0 if v < ALPHA_FLOOR else v)
    img.putalpha(a)
    return img


def sheet(out: Path) -> None:
    """Every try on a light and a dark tile at the same scale, with the base for comparison; a line at the base's feet."""
    tries = [OUT / "_base.png"] + sorted(OUT.glob("*_s*.png"))
    cell = 420
    font = ImageFont.load_default()
    try:
        font = ImageFont.truetype("arialbd.ttf", 16)
    except OSError:
        pass
    cols = 4
    rows = (len(tries) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cell + 10) + 10, rows * (cell + 34) + 10), (30, 28, 34))
    draw = ImageDraw.Draw(img)
    feet = figure_box(Image.open(OUT / "_base.png"))[3]
    for n, path in enumerate(tries):
        x, y = 10 + (n % cols) * (cell + 10), 10 + (n // cols) * (cell + 34)
        draw.text((x, y), path.stem, fill=(235, 228, 210), font=font)
        tile = Image.new("RGBA", (CANVAS, CANVAS), (214, 205, 182, 255))
        ImageDraw.Draw(tile).rectangle((CANVAS // 2, 0, CANVAS, CANVAS), fill=(58, 70, 52, 255))
        ImageDraw.Draw(tile).line((0, feet, CANVAS, feet), fill=(200, 40, 40, 255), width=3)
        fig = cleaned(path)
        if fig.size != (CANVAS, CANVAS):
            fig = fig.resize((CANVAS, CANVAS), Image.LANCZOS)
        tile.alpha_composite(fig)
        img.paste(tile.convert("RGB").resize((cell, cell), Image.LANCZOS), (x, y + 22))
    img.save(out)
    print(out)


def pick(picks: list[str]) -> None:
    """Cut each pick to the game canvas: the base's scale and height, its feet on the base's baseline, centered where
    the base figure's center is; as wide as the pose needs (symmetric, so the bottom-center pivot stays put)."""
    base = base_canvas()
    bx0, by0, bx1, by1 = figure_box(base)
    pad = 6
    cx = (bx0 + bx1) / 2
    keys = []
    for p in picks:
        pose, _, seed = p.partition(":")
        img = cleaned(OUT / f"{pose}_s{seed or FIRST_SEED}.png")
        x0, y0, x1, y1 = figure_box(img)
        # Feet on the base's baseline: shift the picture down/up so its lowest pixel meets the base's.
        dy = by1 - y1
        half = max(cx - x0, x1 - cx) + pad
        top = min(by0, y0 + dy) - pad
        box = (round(cx - half), round(top), round(cx + half), by1 + pad)
        shifted = Image.new("RGBA", img.size, (0, 0, 0, 0))
        shifted.alpha_composite(img, (0, dy)) if dy >= 0 else shifted.alpha_composite(img.crop((0, -dy, img.width, img.height)))
        canvas = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0))
        canvas.alpha_composite(shifted, (-box[0], -box[1]))
        key = f"char_hero_bare_{pose}"
        canvas.save(RAW / f"{key}.png")
        keys.append(key)
        print(f"{key}: {canvas.size} (base figure {bx1 - bx0 + 2 * pad}x{by1 - by0 + 2 * pad}), feet moved {dy:+d} px")
    subprocess.run([sys.executable, str(ROOT / "scripts" / "prepare_art.py"), "--force", *keys], check=False)


def main() -> None:
    args = sys.argv[1:]
    if not args:
        sys.exit(__doc__)
    if args[0] == "sheet":
        sheet(OUT / "_sheet.png")
        return
    if args[0] == "pick":
        pick(args[1:])
        return
    if args[0] == "gen":
        seeds, first = 2, FIRST_SEED
        if "--seeds" in args:
            seeds = int(args[args.index("--seeds") + 1])
        if "--first-seed" in args:
            first = int(args[args.index("--first-seed") + 1])
        wanted = [a for a in args[1:] if not a.startswith("--") and not a.isdigit()]
        poses = [p for p in POSES if any(p == w or (w.endswith("_") and p.startswith(w)) for w in wanted)]
        gen(poses or list(POSES), seeds, first)
        return
    sys.exit(__doc__)


if __name__ == "__main__":
    main()
