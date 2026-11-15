#!/usr/bin/env python3
"""Layered hero TEST (docs/PLANS.md, "How the hero is put together"): the hero as parts on joints, gear per part.

Not part of the game: nothing here feeds prepare_art.py or the Unity build. Pictures are Qwen edits on one canvas
(art/redo/layers/), the parts are cut from them with fixed masks (hero_layers_rig.py holds the joints and masks) and
the Unity test (unity/Assets/Scripts/Game/Test/LayeredHero.cs) loads them from art/redo/layers/parts/ in the editor.

Usage:
  python scripts/hero_layers.py gen rig --seeds 3          # tries of an edit: art/redo/layers/<edit>_s<seed>.png
  python scripts/hero_layers.py gen chest_leather helm_cap # edits of the picked rig pose
  python scripts/hero_layers.py sheet rig                  # art/redo/layers/_sheet_<edit>.png
Needs ComfyUI with Qwen-Image 2.1 running (see scripts/qwen_batch.py).
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import hero_poses as hp  # noqa: E402
from art_tests import write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = hp.ROOT
OUT = ROOT / "art" / "redo" / "layers"
RAW = hp.RAW
SEED = 6100

STYLE = ("the same flat cartoon art style with hard-edged cel shadows, thin dark outlines and a warm golden rim light "
         "along his edges")
HERO = ("the same young hero with the same face, messy dark brown hair, red headband with two tails, cream shirt with "
        "sleeves rolled up to the elbows, brown belt with a square buckle, dark teal trousers and brown boots, the same "
        "size and body proportions, the same colours")
NOTHING = "No weapon anywhere. No ground, no shadow, no text."
SAME = (f"Keep everything else of <image1> exactly the same: the same young hero with messy dark brown hair and the red "
        f"headband, the same face, the same body and exactly the same pose with every limb in exactly the same place, "
        f"both fists in exactly the same place, {STYLE}.")

# The rig pose every part is cut from: limbs straight and clear of each other, so a fixed mask can cut each part.
RIG_POSE = ("a neutral paper-doll rig pose, standing upright and relaxed in the same three-quarter view facing right: "
            "both arms are perfectly straight and held out away from his body, pointing down and outward at 45 degrees "
            "like the letter A, with a wide clear gap between each arm and his torso, elbows not bent, both hands closed "
            "into fists; both legs are perfectly straight with locked knees, feet apart a little wider than his "
            "shoulders, with a clear gap between the legs from the crotch down; his head is upright; a calm face")

# name: (source, prompt, second reference in art/raw or None). Source "base" is the game's bare hero; any other name
# is the picked try of that edit (PICKS).
EDITS = {
    "rig": ("base", f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {RIG_POSE}. Keep everything "
                    f"else exactly the same: {HERO}, {STYLE}. Both hands are closed into empty fists. {NOTHING} "
                    f"{RGBA_CLOSE}", None),
    # Both fists around a plain green rod: the hand layers (fingers in front of a handle, palm behind) are cut from it.
    "grip": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only his two hands: each closed fist now firmly grips a short "
                    f"straight rod, like the handle of a tool. Each rod is a plain smooth cylinder of pure bright green, "
                    f"about as thick as his thumb and about three times as long as his fist is wide, sticking out the "
                    f"same distance on both sides of the fist, held at a right angle to that forearm. His fingers wrap "
                    f"around the rod and his thumb closes over them, so the rod passes through the closed fist. "
                    f"{SAME} The same clothes. No other object, no ground, no shadow, no text. {RGBA_CLOSE}", None),
    "chest_leather": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his upper body and arms: dress him "
                             f"in exactly the leather armor of <image2>: the brown leather chest piece with its straps "
                             f"and buckles worn over the cream shirt, the leather shoulder pads, and a leather bracer on "
                             f"each forearm. Take only these clothes from <image2>, not its pose. His belt, trousers, "
                             f"boots, head, hair and headband are unchanged. {SAME} {NOTHING} {RGBA_CLOSE}",
                      "char_hero_leather_armor"),
    "chest_scale": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his upper body and arms: dress him "
                           f"in exactly the scale mail of <image2>: the sleeveless shirt of small overlapping steel "
                           f"scales worn over the cream shirt, and the steel plate shoulder guards. Take only these "
                           f"clothes from <image2>, not its pose. His belt, trousers, boots, head, hair and headband "
                           f"are unchanged. {SAME} {NOTHING} {RGBA_CLOSE}", "char_hero_scale_mail"),
    "helm_cap": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his head: he now wears exactly the "
                        f"brown leather cap of <image2>, sitting firmly on his head over his hair, with its short brim "
                        f"above his eyes and some messy dark hair still showing under it at the sides and the back. The "
                        f"red headband is gone. His face is unchanged. His shirt, belt, trousers and boots are unchanged. "
                        f"{SAME} {NOTHING} {RGBA_CLOSE}", "icon_leather_cap"),
    "helm_coif": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his head: he now wears exactly the "
                         f"scale coif of <image2>: a hood of small overlapping steel scales with a brown leather rim, "
                         f"covering his hair, ears and neck and resting on the top of his shoulders, his face showing "
                         f"through the opening. The red headband is gone. His face is unchanged. His shirt, belt, "
                         f"trousers and boots are unchanged. {SAME} {NOTHING} {RGBA_CLOSE}", "icon_scale_coif"),
    # No legs slot in the game yet: two invented looks for the test.
    "legs_plain": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only his trousers: he now wears plain loose trousers of "
                          f"undyed ochre linen with a square sewn patch on one knee, tucked into the same tall brown "
                          f"boots. His shirt, belt, head, hair and headband are unchanged. {SAME} {NOTHING} "
                          f"{RGBA_CLOSE}", None),
    "legs_plate": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his legs: he now wears steel plate "
                          f"leg armor over his dark teal trousers: a steel plate on the front of each thigh, a round "
                          f"steel knee guard on each knee and steel greaves covering each shin down to the ankle, with "
                          f"brown leather straps; brown boots under the greaves. His shirt, belt, head, hair and "
                          f"headband are unchanged. {SAME} {NOTHING} {RGBA_CLOSE}", None),
}

# --- Step 1 of the layered hero rebuild: the fight. Turning the A-pose arms gave stiff blows (no twist, no arm coming at
# the viewer), so each key moment gets a drawn upper body ("up_<moment>": whole-figure pose edits of the rig pose, the
# sword fist around a green rod; hero_layers_rig.py cuts the upper body, the hands and the grip out of them). The legs
# stay rig parts on IK, the head stays the rig head with a swapped face. ---
ROD = ("His right fist firmly grips a short straight rod, like the handle of a tool: a plain smooth cylinder of pure "
       "bright green, about as thick as his thumb and about three times as long as his fist is wide, sticking out the "
       "same distance on both sides of the fist. His fingers wrap around the rod and his thumb closes over them, so the "
       "rod passes through the closed fist. His other hand is a closed empty fist. No sword, no blade, no other object.")
NEAR = "his right arm, the arm nearer to the viewer,"
UPPER = {
    "ready": (f"a ready sword-fighting stance, braced and heavy: three-quarter view facing right, feet planted wide "
              f"apart with the front foot forward, both knees bent, his weight sunk low, his chest turned a little "
              f"toward the viewer and his shoulders broad and squared; {NEAR} is bent, with its fist held in front of "
              f"his belt a little away from his body, gripping the rod so that the rod points up and forward to the "
              f"upper right, like a sword held in a low guard; his other arm is bent with a clenched fist held low "
              f"beside his far hip; a calm, alert face"),
    "wind": (f"coiled to swing a sword: three-quarter view facing right, feet apart, knees bent, his weight back on "
             f"the rear foot, his chest twisted away from the viewer and his shoulders turned away to the left; {NEAR} "
             f"is pulled back and up so its fist is high behind his head, above and behind his rear shoulder, gripping "
             f"the rod so that the rod points up and back, like a sword cocked back over his shoulder ready to swing; "
             f"his other arm reaches forward and low with a clenched fist for balance; a focused, determined face with "
             f"gritted teeth"),
    "strike": (f"the end of a big downward diagonal sword slash: three-quarter view facing right, a deep forward lunge "
               f"with the front knee bent and the back leg stretched far out behind, his upper body bent far forward "
               f"and twisted so his chest faces the viewer; {NEAR} is swung down and forward, stretched out in front "
               f"of his front knee and coming a little toward the viewer, the fist low, gripping the rod so that the "
               f"rod points forward and down at the ground, like a sword that has just cut down through an enemy; his "
               f"other arm is flung up and back behind him with a clenched fist; a fierce shout, mouth wide open"),
    "hurt": (f"recoiling from a heavy blow that has just hit him in the chest: three-quarter view facing right, knocked "
             f"backward to the left, his upper body bent back and twisted away, his shoulders hunched and his chest "
             f"caved in, knees buckling with his weight on the rear foot; {NEAR} is thrown out low to the side, still "
             f"gripping the rod, which points down and forward; his other arm is raised in front of his chest with a "
             f"clenched fist, shielding himself; his head is snapped back, eyes squeezed shut and teeth clenched in "
             f"pain"),
}
for _name, _pose in UPPER.items():
    EDITS[f"up_{_name}"] = ("rig", f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {_pose}. Keep "
                                   f"everything else exactly the same: {HERO}, {STYLE}. {ROD} No ground, no shadow, no "
                                   f"dust, no effects, no text. {RGBA_CLOSE}", None)
    # The full steel set on that moment: the Scale Mail of the picked rig edit, and steel gauntlets.
    EDITS[f"set_{_name}"] = (f"up_{_name}",
                             f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his upper body, arms and hands: "
                             f"dress him in exactly the scale mail of <image2>: the sleeveless shirt of small overlapping "
                             f"steel scales worn over the cream shirt, and the steel plate shoulder guards; and on each "
                             f"hand a steel plate gauntlet covering the fist and the wrist with a short flared cuff. "
                             f"Take only these clothes from <image2>, not its pose. The green rod in his fist is "
                             f"unchanged, held by the gauntlet exactly as before. Keep everything else of <image1> "
                             f"exactly the same: the same hero, the same face, hair and headband, the same belt, "
                             f"trousers and boots, exactly the same pose with every limb and both fists in exactly the "
                             f"same place, {STYLE}. No sword, no blade, no ground, no shadow, no text. {RGBA_CLOSE}",
                             "pick:chest_scale")
# The lunge is deeper than turned rig legs can go (the drawn legs are far longer), so the strike brings its own
# drawn legs; this is their armor: the Plate Legs of the picked rig edit and steel boots.
EDITS["low_strike"] = ("up_strike",
                       f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his legs and feet: dress his legs in "
                       f"exactly the steel plate leg armor of <image2>, worn over his dark teal trousers: a steel plate "
                       f"on the front of each thigh, a round steel knee guard on each knee and a steel greave covering "
                       f"each shin down to the ankle, with brown leather straps; and instead of the brown boots a "
                       f"jointed steel plate shoe covering each foot. Take only these clothes from <image2>, not its "
                       f"pose. Keep everything else of <image1> exactly the same: the same hero, the same face, hair "
                       f"and headband, the same shirt, belt, arms and fists, the green rod, exactly the same pose with "
                       f"both legs and both feet in exactly the same place, {STYLE}. No sword, no blade, no ground, no "
                       f"shadow, no text. {RGBA_CLOSE}", "pick:legs_plate")
FACE = ("His head stays in exactly the same place at the same size and angle; his hair, headband and neck are "
        "unchanged.")
EDITS.update({
    "face_shout": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only the expression on his face: a fierce battle shout, "
                          f"his mouth wide open showing his teeth, his eyebrows drawn down hard in anger, his eyes "
                          f"narrowed and fixed straight ahead. {FACE} {SAME} {NOTHING} {RGBA_CLOSE}", None),
    "face_hurt": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only the expression on his face: wincing in sharp pain, "
                         f"both eyes squeezed tightly shut, his teeth clenched and bared, his eyebrows pinched together "
                         f"and raised in the middle. {FACE} {SAME} {NOTHING} {RGBA_CLOSE}", None),
    "boots_steel": ("rig", f"{RGBA_OPEN} Edit <image1>. Change only what he wears on his lower legs and feet: instead "
                           f"of the brown boots he now wears armored steel boots: a steel plate greave covering each "
                           f"shin from just below the knee to the ankle, and a jointed steel plate shoe covering each "
                           f"foot, with brown leather straps. His trousers above the knee, his belt, shirt, head, hair "
                           f"and headband are unchanged. {SAME} {NOTHING} {RGBA_CLOSE}", None),
})
PICKS: dict[str, int] = {"rig": 6100, "grip": 6100, "chest_leather": 6100, "chest_scale": 6100, "helm_cap": 6100,
                         "helm_coif": 6101, "legs_plain": 6100, "legs_plate": 6100,
                         "up_ready": 6100, "up_wind": 6100, "up_strike": 6100, "up_hurt": 6101,
                         "face_shout": 6100, "face_hurt": 6100, "boots_steel": 6100,
                         "set_ready": 6102, "set_wind": 6100, "set_strike": 6102, "set_hurt": 6100, "low_strike": 6101}


def source(name: str) -> Image.Image:
    if name == "base":
        return hp.base_canvas()
    return hp.cleaned(OUT / f"{name}_s{PICKS[name]}.png")


def gen(names: list[str], seeds: int, first: int) -> None:
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    OUT.mkdir(parents=True, exist_ok=True)
    for name in names:
        src, prompt, second = EDITS[name]
        refs = [comfy.upload(f"drakepg_layers_{src}.png", hp.on_white(source(src)))]
        if second and second.startswith("pick:"):  # the picked try of another edit
            refs.append(comfy.upload(f"drakepg_layers_ref_{second[5:]}.png", hp.on_white(source(second[5:]))))
        elif second:
            refs.append(comfy.upload(f"drakepg_layers_ref_{second}.png",
                                     hp.on_white(Image.open(RAW / f"{second}.png").convert("RGBA"))))
        for i in range(seeds):
            out = OUT / f"{name}_s{first + i}.png"
            if out.exists():
                continue
            print(out.relative_to(ROOT), flush=True)
            try:
                out.write_bytes(comfy.generate(prompt, refs, None, first + i, 30))
            except RuntimeError as e:
                print(f"  ComfyUI error: {e}", flush=True)
                continue
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


def sheet(prefixes: list[str], cell: int = 560) -> None:
    """Tries side by side on a light and a dark half, the base (or the rig pick) first, a line at the base's feet."""
    tries = [p for pre in prefixes for p in sorted(OUT.glob(f"{pre}_s*.png"))]
    first = hp.base_canvas() if "rig" not in PICKS else source("rig")
    cells = [("base" if "rig" not in PICKS else "rig", first)] + [(p.stem, hp.cleaned(p)) for p in tries]
    feet = hp.figure_box(hp.base_canvas())[3]
    font = ImageFont.truetype("arialbd.ttf", 18)
    cols = min(4, len(cells))
    rows = (len(cells) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cell + 8) + 8, rows * (cell + 30) + 8), (30, 28, 34))
    d = ImageDraw.Draw(img)
    for n, (label, fig) in enumerate(cells):
        x, y = 8 + (n % cols) * (cell + 8), 8 + (n // cols) * (cell + 30)
        d.text((x, y), label, fill=(235, 228, 210), font=font)
        tile = Image.new("RGBA", fig.size, (214, 205, 182, 255))
        ImageDraw.Draw(tile).rectangle((fig.width // 2, 0, fig.width, fig.height), fill=(58, 70, 52, 255))
        ImageDraw.Draw(tile).line((0, feet, fig.width, feet), fill=(200, 40, 40, 255), width=3)
        tile.alpha_composite(fig)
        box = (150, 250, 1386, 1486) if fig.size == (hp.CANVAS, hp.CANVAS) else (0, 0, *fig.size)
        img.paste(tile.convert("RGB").crop(box).resize((cell, cell), Image.LANCZOS), (x, y + 24))
    out = OUT / f"_sheet_{'_'.join(prefixes)}.png"
    img.save(out)
    print(out)


def main() -> None:
    args = sys.argv[1:]
    cmd, rest = args[0], args[1:]
    seeds, first = 1, SEED
    if "--seeds" in rest:
        i = rest.index("--seeds")
        seeds = int(rest[i + 1])
        del rest[i:i + 2]
    if "--seed" in rest:
        i = rest.index("--seed")
        first = int(rest[i + 1])
        del rest[i:i + 2]
    if cmd == "gen":
        gen(rest, seeds, first)
    elif cmd == "sheet":
        sheet(rest)


if __name__ == "__main__":
    main()
