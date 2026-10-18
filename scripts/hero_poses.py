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

Every pose in every body-armor outfit (`char_hero_<armor>_<pose>`, the look Player.look picks with that armor): edits
of the picked bare try with the outfit's own prompt (redo_art.OUTFITS), cut with the bare pose's exact box, so the
pose's hand anchors fit every outfit.
  python scripts/hero_poses.py outfits gen [--armors a,b] [--poses p,q] [--seed 5100] [--no-ref]  # -> art/redo/pose/outfit/
  python scripts/hero_poses.py outfits sheet <armor>                                   # an outfit's poses, bare beside
  python scripts/hero_poses.py outfits cut [armor_pose:seed ...]                        # -> art/raw + prepare_art
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


# The try picked for each pose (`pick`): the outfit versions are edits of these.
PICKS = {
    "charge": 4244, "leap": 4242, "land": 4242,
    "sword_wind": 4243, "sword_step": 4242, "sword_strike": 4250, "axe_wind": 4242, "axe_step": 4242, "axe_strike": 4242,
    "hammer_wind": 4242, "hammer_strike": 4242, "dagger_wind": 4243, "dagger_step": 4243, "dagger_strike": 4242,
    "staff_wind": 4242, "staff_step": 4243, "staff_strike": 4242, "wand_wind": 4243, "wand_step": 4243, "wand_strike": 4243,
}
OUTFIT_DIR = OUT / "outfit"
# Extra canvas on both sides of every pose (bare and outfits), in edit px (2.154 per 1x game px: 1034 / 480). The pose
# hands in src/data/classes.ts are measured on canvases that include it.
MARGIN_X = 64
OUTFIT_SEED = 5100


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


def cut_of(img: Image.Image) -> tuple[tuple[int, int, int, int], int]:
    """Where a try is cut to the game canvas: the box (its feet on the base's baseline, centered where the base figure's
    center is, as wide as the pose needs on both sides so the bottom-center pivot stays put) and the vertical shift."""
    base = base_canvas()
    bx0, by0, bx1, by1 = figure_box(base)
    pad = 6
    cx = (bx0 + bx1) / 2
    x0, y0, x1, y1 = figure_box(img)
    # Feet on the base's baseline: shift the picture down/up so its lowest pixel meets the base's.
    dy = by1 - y1
    # MARGIN_X more on both sides: outfit robes flare past the bare pose (up to 58 px) and are cut with its box.
    half = max(cx - x0, x1 - cx) + pad + MARGIN_X
    top = min(by0, y0 + dy) - pad
    return (round(cx - half), round(top), round(cx + half), by1 + pad), dy


def cut(img: Image.Image, box: tuple[int, int, int, int], dy: int) -> Image.Image:
    shifted = Image.new("RGBA", img.size, (0, 0, 0, 0))
    shifted.alpha_composite(img, (0, dy)) if dy >= 0 else shifted.alpha_composite(img.crop((0, -dy, img.width, img.height)))
    canvas = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0))
    canvas.alpha_composite(shifted, (-box[0], -box[1]))
    return canvas


def pick(picks: list[str]) -> None:
    """Cut each pick to the game canvas (cut_of): the base's scale and height, its feet on the base's baseline."""
    keys = []
    for p in picks:
        pose, _, seed = p.partition(":")
        img = cleaned(OUT / f"{pose}_s{seed or FIRST_SEED}.png")
        box, dy = cut_of(img)
        canvas = cut(img, box, dy)
        key = f"char_hero_bare_{pose}"
        canvas.save(RAW / f"{key}.png")
        keys.append(key)
        print(f"{key}: {canvas.size}, feet moved {dy:+d} px")
    subprocess.run([sys.executable, str(ROOT / "scripts" / "prepare_art.py"), "--force", *keys], check=False)


# --- The poses in every outfit ---

# Outfits that drifted in motion, described for it: stormweave turned into white silk trousers in strides and lunges.
OUTFIT_IN_MOTION = {
    "stormweave_robe": "a long pale silver-blue silk robe with small yellow lightning zigzags stitched along the seams, "
                       "wrapped closed across his chest and belted at the waist exactly as in <image2>, and OPEN only "
                       "below the belt like a long coat: its two lower front panels hang loose at his sides and swing "
                       "with the motion, and his dark teal trousers show clearly on both legs between them",
}


def outfit_prompt(armor: str, pose: str, ref: bool = True) -> str:
    """The edit of a bare pose into an outfit. ref: <image2> is the standing hero in that outfit (its exact design:
    length, pattern, the trousers under a robe); without it robes drifted into pants and lost their stitching."""
    from redo_art import OUTFITS
    OUTFITS = {**OUTFITS, **OUTFIT_IN_MOTION}
    hands = ("his fist and his open hand exactly the same and in exactly the same place" if pose in OPEN_HAND
             else "both empty fists exactly the same and in exactly the same place")
    if ref:
        dress = (f"Dress the hero of <image1> in exactly the outfit the hero wears in <image2>: {OUTFITS[armor]}, with the "
                 "same cut, length, colours and pattern as in <image2>, and the same dark teal trousers and brown boots "
                 "under it as in <image2>. Take only the clothes from <image2>, not its pose. ")
    else:
        dress = f"Change only the clothing: the hero now wears {OUTFITS[armor]}. "
    return (f"{RGBA_OPEN} Edit <image1>. {dress}Keep everything else of <image1> exactly the same: the same young hero "
            "with messy dark brown hair and the red headband, the same face and expression, the same body, pose and "
            f"proportions, {hands}, the same flat cartoon art style with hard-edged cel shadows, thin dark outlines and a "
            f"warm golden rim light. No weapon anywhere. No ground, no shadow, no text. {RGBA_CLOSE}")


def outfit_gen(armors: list[str], poses: list[str], seed: int, with_ref: bool = True) -> None:
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    OUTFIT_DIR.mkdir(parents=True, exist_ok=True)
    looks = {}
    if with_ref:
        for armor in armors:
            standing = Image.open(ROOT / "src" / "assets" / "art" / f"char_hero_{armor}@2x.webp").convert("RGBA")
            looks[armor] = comfy.upload(f"drakepg_outfit_{armor}.png", on_white(standing))
    for pose in poses:
        src = cleaned(OUT / f"{pose}_s{PICKS[pose]}.png")
        ref = comfy.upload(f"drakepg_pose_{pose}.png", on_white(src))
        for armor in armors:
            out = OUTFIT_DIR / f"{armor}_{pose}_s{seed}.png"
            if out.exists():
                continue
            print(out.relative_to(ROOT), flush=True)
            refs = [ref, looks[armor]] if with_ref else [ref]
            try:
                out.write_bytes(comfy.generate(outfit_prompt(armor, pose, with_ref), refs, None, seed, 30))
            except RuntimeError as e:
                print(f"  ComfyUI error: {e}", flush=True)
                continue
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


def outfit_sheet(armor: str, out: Path) -> None:
    """An outfit's poses next to the bare ones, cut like the game will see them, on a light tile."""
    font = ImageFont.load_default()
    try:
        font = ImageFont.truetype("arialbd.ttf", 15)
    except OSError:
        pass
    cells = []
    for pose in PICKS:
        tries = sorted(OUTFIT_DIR.glob(f"{armor}_{pose}_s*.png"))
        if not tries:
            continue
        bare = cleaned(OUT / f"{pose}_s{PICKS[pose]}.png")
        box, dy = cut_of(bare)
        row = [(f"bare {pose}", cut(bare, box, dy))] + [(t.stem, cut(fit(cleaned(t)), box, dy)) for t in tries]
        cells.append(row)
    h = 300
    width = max(sum(round(img.width * h / img.height) + 8 for _, img in row) for row in cells) + 8
    sheet_img = Image.new("RGB", (width, len(cells) * (h + 22) + 8), (30, 28, 34))
    d = ImageDraw.Draw(sheet_img)
    y = 6
    for row in cells:
        x = 6
        for label, img in row:
            w = round(img.width * h / img.height)
            tile = Image.new("RGBA", img.size, (226, 220, 204, 255))
            tile.alpha_composite(img)
            sheet_img.paste(tile.convert("RGB").resize((w, h), Image.LANCZOS), (x, y + 18))
            d.text((x, y), label, fill=(235, 228, 210), font=font)
            x += w + 8
        y += h + 22
    sheet_img.save(out)
    print(out)


def fit(img: Image.Image) -> Image.Image:
    """An edit back on the try's square canvas (the edit may come back a few pixels off size)."""
    return img if img.size == (CANVAS, CANVAS) else img.resize((CANVAS, CANVAS), Image.LANCZOS)


def outfit_cut(picks: list[str]) -> None:
    """Cut outfit poses with their bare pose's box. picks: `armor_pose:seed`, or nothing for every edit there is (the
    newest seed of each). Warns when the outfit spills past the box (it would be clipped)."""
    from redo_art import OUTFITS
    chosen: dict[tuple[str, str], int] = {}
    if picks:
        for p in picks:
            name, _, seed = p.partition(":")
            armor = next(a for a in OUTFITS if name.startswith(a + "_"))
            chosen[(armor, name[len(armor) + 1:])] = int(seed or OUTFIT_SEED)
    else:
        for path in sorted(OUTFIT_DIR.glob("*_s*.png")):
            name, _, seed = path.stem.rpartition("_s")
            armor = next((a for a in OUTFITS if name.startswith(a + "_")), None)
            if armor is None:
                continue
            key = (armor, name[len(armor) + 1:])
            chosen[key] = max(chosen.get(key, 0), int(seed))
    keys = []
    for (armor, pose), seed in sorted(chosen.items()):
        bare = cleaned(OUT / f"{pose}_s{PICKS[pose]}.png")
        box, dy = cut_of(bare)
        img = fit(cleaned(OUTFIT_DIR / f"{armor}_{pose}_s{seed}.png"))
        canvas = cut(img, box, dy)
        a = np.asarray(canvas)[..., 3] >= 128
        spill = int(a[0].sum() + a[:, 0].sum() + a[:, -1].sum())
        key = f"char_hero_{armor}_{pose}"
        canvas.save(RAW / f"{key}.png")
        keys.append(key)
        if spill > 20:
            print(f"  {key}: {spill} px touch the edge (clipped)")
    print(f"cut {len(keys)} outfit poses")
    for i in range(0, len(keys), 40):
        subprocess.run([sys.executable, str(ROOT / "scripts" / "prepare_art.py"), "--force", *keys[i:i + 40]], check=False)


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
    if args[0] == "outfits":
        from redo_art import OUTFITS
        sub = args[1] if len(args) > 1 else ""
        opt = lambda name: args[args.index(name) + 1] if name in args else None  # noqa: E731
        if sub == "gen":
            armors = opt("--armors").split(",") if opt("--armors") else list(OUTFITS)
            poses = opt("--poses").split(",") if opt("--poses") else list(PICKS)
            outfit_gen(armors, poses, int(opt("--seed") or OUTFIT_SEED), "--no-ref" not in args)
            return
        if sub == "sheet":
            outfit_sheet(args[2], OUTFIT_DIR / f"_sheet_{args[2]}.png")
            return
        if sub == "cut":
            outfit_cut(args[2:])
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
