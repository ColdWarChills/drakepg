#!/usr/bin/env python3
"""Art direction tests: the same subjects drawn in each candidate style on the local Qwen-Image 2.1 (docs/PLANS.md, Art).

Each direction is a style LoRA from Civitai (files in ComfyUI/models/loras) plus a style line added to every prompt.
Results land in art/tests/<direction>/<subject>.png at 2x game size (heroes and monsters 960px tall, icons in a
512px box, the room 2560x1440); the untouched generations stay in art/tests/<direction>/_raw/.

Usage:
  python scripts/art_tests.py flash                      # every subject in one direction
  python scripts/art_tests.py all hero_warrior bg_room   # these subjects in every direction
  python scripts/art_tests.py ink mon_boar --seed 7 --variants 3   # 3 seeds to pick from (the first becomes the result)
Options:
  --seed N     seed (default: fixed per subject, the same in every direction, so the rows compare)
  --variants N extra seeds into _raw/ (the result is the first)    --force   regenerate an existing result
  --pick N     rebuild the result from _raw/<subject>_sN.png (a variant you liked), no generating; --flip turns it
               upside down (a sword drawn point-down)
  --nolora     the style line only, no LoRA (results go to art/tests/_nolora/<direction>/)
  --steps N    sampler steps (default 30)   --dry   print the prompts   --host URL   ComfyUI address
Needs Pillow and numpy, plus ComfyUI running with the Qwen-Image 2.1 files (scripts/qwen_batch.py).
"""
import sys
import time
import zlib
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
TESTS = ROOT / "art" / "tests"

LORAS = "art_tests\\"  # subfolder of ComfyUI/models/loras with the Civitai downloads (licenses: docs/PLANS.md, Art)
NEGATIVE = ("photo, photorealistic, 3d render, blurry, noisy, jpeg artifacts, extra fingers, malformed hands, extra limbs, "
            "melted shapes, cast shadow on the ground, floor, text, letters, watermark, signature, frame, border")

# prefix: how the LoRA wants the prompt to start (trigger word or medium). style: added after the subject.
# post: 'pixel' snaps the result to a real pixel grid (native size x4) with a small palette.
DIRECTIONS = {
    "flash": {
        "name": "Flash cartoon",
        "loras": [(LORAS + "Qwen2.1_Soyuzmultfilmstyle_v1.2.1.safetensors", 0.9)],
        "cfg": 3.0,
        "prefix": "soyuzmultfilmstyle. ",
        "style": ("2D cartoon game art in the style of an early-2000s Flash cartoon: clean bold dark outlines of even "
                  "weight, flat colors with one hard-edged shadow tone, simple rounded shapes, very little detail, no "
                  "texture."),
    },
    "painterly": {
        "name": "Painterly",
        "loras": [(LORAS + "Rostov_qwen21.safetensors", 1.0)],
        "cfg": 3.0,
        "prefix": "Digital painting of ",
        "style": ("Painted 2D RPG game art: visible loose brush strokes, soft painted edges, simple readable shapes, "
                  "limited detail, rich warm colors, no outlines."),
    },
    "pixel": {
        "name": "HD pixel art",
        "loras": [],
        "cfg": 1.0,
        "prefix": "Pixel art of ",
        "style": ("Hi-bit 16-bit era pixel art game sprite: crisp square pixels, limited color palette, clean dark "
                  "1-pixel outline, flat shading with three tones per color, no anti-aliasing, no blur, no gradients."),
        "post": "pixel",
    },
    # Second candidates for two directions, compared before picking the column's LoRA.
    "painterly_alt": {
        "name": "Painterly (Cinematic Painterly)",
        "loras": [(LORAS + "cine1p_c1-st4000.safetensors", 1.0)],
        "cfg": 3.0,
        "prefix": "cine1p. Painting of ",
        "style": ("Painted 2D RPG game art: visible brush strokes, soft painted edges, simple readable shapes, limited "
                  "detail, rich warm colors, no outlines."),
    },
    "ink_alt": {
        "name": "Storybook ink (Dream Loom)",
        "loras": [(LORAS + "DreamLoom_qwen21.safetensors", 0.8)],
        "cfg": 3.0,
        "prefix": "Illustration of ",
        "style": ("Old storybook illustration: confident pen-and-ink linework with light hatching, soft transparent "
                  "watercolor washes, muted earthy colors, simple readable shapes, limited detail."),
    },
    "ink": {
        "name": "Storybook ink",
        "loras": [(LORAS + "Sime_qwen21.safetensors", 0.9)],
        "cfg": 3.0,
        "prefix": "Illustration of ",
        "style": ("Old storybook illustration: confident pen-and-ink linework with light hatching, soft transparent "
                  "watercolor washes, muted earthy colors, simple readable shapes, limited detail."),
    },
}

# Round 2 (2026-10-16): the designer turned down all four characters (PLANS.md, Art). Six low-detail looks from plain
# Qwen, no LoRA and no reference, tried on the Warrior alone before anything else.
_LOOK = {"loras": [], "cfg": 1.0, "prefix": "Game character art of "}
DIRECTIONS.update({
    "cartoon": {**_LOOK, "name": "Modern cartoon", "style": (
        "Modern 2D TV-cartoon style: thick even black outlines, flat bright colors with at most one soft shadow, simple "
        "geometric shapes, slightly big head and hands, very few details.")},
    "cel": {**_LOOK, "name": "Clean cel shading", "style": (
        "Clean cel-shaded JRPG character art: crisp thin lineart, flat colors with one hard shadow tone and a small "
        "highlight, slightly stylized proportions, few details.")},
    "gouache": {**_LOOK, "name": "Flat gouache", "style": (
        "Flat gouache illustration: matte opaque paint shapes with a visible dry-brush texture, a limited palette of six "
        "colors, no outlines, no glossy highlights, no fine detail.")},
    "chunky": {**_LOOK, "name": "Chunky indie", "style": (
        "Chunky stylized indie-game character: bold angular shapes, chunky exaggerated proportions with big hands and "
        "feet, thick dark outlines, flat colors with hard-edged dark shadows, a strong silhouette, minimal detail.")},
    "storylight": {**_LOOK, "name": "Light storybook", "style": (
        "Light storybook illustration: a thin loose ink outline with very few lines, flat pale watercolor fills, no "
        "hatching, simple shapes, lots of empty space.")},
    "papercut": {**_LOOK, "name": "Paper cut-out", "style": (
        "Paper cut-out style: flat colored paper shapes layered on each other, no outlines, a subtle paper texture, each "
        "body part one simple separate shape, minimal detail.")},
})
LOOKS = ["cartoon", "cel", "gouache", "chunky", "storylight", "papercut"]

# Round 3 (2026-10-16): Chunky read as too edgy. Rounder and friendlier: two takes on the chunky mobile-strategy look
# (traits only, no studio or game named) and four new mixes.
DIRECTIONS.update({
    "mobile3d": {**_LOOK, "name": "Mobile cartoon 3D", "style": (
        "Mobile strategy game cartoon character: chunky rounded proportions, only about three and a half heads tall, "
        "with a big head, huge hands and short stubby legs, "
        "a soft 3D-rendered look with smooth shading and glossy highlights, bright saturated colors, a friendly "
        "expression, simple shapes, very little detail.")},
    "mobile2d": {**_LOOK, "name": "Mobile cartoon 2D", "style": (
        "Flat 2D mobile game cartoon character: chunky rounded proportions, only about three and a half heads tall, "
        "with a big head, huge hands and short stubby legs, a "
        "thick dark-brown outline, flat bright colors with one soft shadow and a small glossy highlight, a friendly "
        "expression, very little detail.")},
    "clay": {**_LOOK, "name": "Clay figure", "style": (
        "Handmade modelling-clay figure: soft rounded sculpted shapes, a matte clay surface with faint tool marks, soft "
        "studio light, bright cheerful colors, a big head and stubby limbs, simple shapes, no fine detail.")},
    "toy": {**_LOOK, "name": "Vinyl toy", "style": (
        "Collectible vinyl toy figure: smooth rounded shapes, satin plastic with soft highlights, a big head and small "
        "body, simple molded details, bright colors, soft studio light.")},
    "plush": {**_LOOK, "name": "Felt plush", "style": (
        "Handmade felt plush toy: soft stuffed rounded shapes, fuzzy felt fabric texture, visible stitched seams, "
        "button-like details, a big head and stubby limbs, warm cozy colors.")},
    "softstory": {**_LOOK, "name": "Soft storybook", "style": (
        "Soft storybook cartoon: chunky rounded proportions with a big head and hands, a thin warm-brown ink outline, "
        "soft flat watercolor fills with gentle shading, warm friendly colors, very few details.")},
})
LOOKS3 = ["mobile3d", "mobile2d", "clay", "toy", "plush", "softstory"]
# The designer's mix of the two 2D looks left after round 3: Mobile cartoon 2D shapes, Soft storybook colour and line.
DIRECTIONS["mix"] = {**_LOOK, "name": "Mobile shapes + storybook", "style": (
    "Soft storybook cartoon with chunky mobile-game proportions: only about three and a half heads tall, a big head, "
    "huge hands and short stubby legs, a thin warm-brown ink outline, soft flat watercolor fills with gentle shading, "
    "warm friendly colors, very few details.")}
FINALISTS = ["mobile2d", "softstory", "mix"]

# Round 4 (2026-10-16): the designer asked about looks inspired by a classic dramatic action manga and a modern dark
# action webtoon. Traits only: no series, studio or artist is named.
DIRECTIONS.update({
    "manga": {**_LOOK, "name": "Dramatic manga", "style": (
        "Bold 1990s Japanese action manga style: confident thick black ink lines, heavy black shadows with hatching only "
        "inside the shadows, angular sculpted forms and sharp facial features, a confident dramatic stance with a twist "
        "of the torso, bold unusual flat colors like teal, magenta and mustard, high contrast, no small details.")},
    "webtoon": {**_LOOK, "name": "Dark webtoon", "style": (
        "Modern Korean action webtoon style: sharp clean digital lineart, cool cinematic cel shading with deep "
        "blue-purple shadows, a faint glowing blue rim light, sleek tall proportions, a calm intense expression, "
        "minimal detail.")},
})

HERO_FRAME = ("Full body, standing idle pose, three-quarter view facing right, the whole figure visible from head to "
              "boots with empty margin around it. No shadow on the ground.")
MONSTER_FRAME = ("Full body, standing idle pose, side view facing right, the whole figure visible with empty margin around it. "
                 "No shadow on the ground.")
ICON_FRAME = ("Shown alone as a game inventory icon, centered, the whole item visible and filling most of the image with "
              "a small empty margin. No hands, no character, no stand, no text.")

# kind: sprite (transparent, `height` px tall), icon (transparent, fits a `box` square), bg (opaque 2560x1440).
# gen: generation canvas (multiples of 32).
SUBJECTS = {
    "hero_warrior": {
        "kind": "sprite", "height": 960, "gen": (1024, 1536),
        "what": ("a young warrior hero, a lean young man with short messy brown hair and a determined look, wearing a "
                 "plain steel breastplate with round steel shoulder guards over a blue knee-length tunic, a brown leather "
                 "belt with a buckle, brown leather gloves, dark grey trousers, steel knee guards and tall brown leather "
                 "boots. Both hands are empty clenched fists at his sides. No weapon. " + HERO_FRAME),
    },
    "hero_mage": {
        "kind": "sprite", "height": 960, "gen": (1024, 1536),
        "what": ("a young mage hero, a slim young man with shoulder-length wavy dark brown hair and a friendly face, "
                 "wearing a long purple wizard robe with gold trim, wide sleeves and a gold swirl pattern at the hem, a "
                 "hood resting on his shoulders, and a brown leather satchel with rolled scrolls on his back. One hand is "
                 "raised in a loose fist, the other hangs at his side. No staff, no weapon. " + HERO_FRAME),
    },
    "hero_rogue": {
        "kind": "sprite", "height": 960, "gen": (1024, 1536),
        "what": ("a young rogue hero, a wiry young man with messy auburn-red hair and a cocky grin, wearing a dark green "
                 "hooded cloak over a brown leather jerkin with buttons, a belt with small pouches, fingerless brown "
                 "leather gloves, olive green trousers and brown leather boots. Both hands are empty fists. No weapon. "
                 + HERO_FRAME),
    },
    "mon_mossling": {
        "kind": "sprite", "height": 960, "gen": (1280, 1280),
        "what": ("a small, cute but mischievous moss creature monster: a soft round fuzzy blob of green moss with two big "
                 "glowing yellow eyes on its front, a few tiny red-capped mushrooms and two leaves growing on top, stubby "
                 "root-like feet. Full body, standing idle pose, three-quarter view facing right with its face turned "
                 "toward the viewer so both eyes are clearly visible, the whole figure visible with empty margin around "
                 "it. No shadow on the ground."),
    },
    "mon_boar": {
        "kind": "sprite", "height": 960, "gen": (1536, 1152),
        "what": ("a large, aggressive wild boar monster, dark brown bristly fur, long curved ivory tusks, glowing red "
                 "eyes, a ridge of sharp wooden thorns growing along its spine, snorting. " + MONSTER_FRAME),
    },
    "mon_thornheart": {
        "kind": "sprite", "height": 960, "gen": (1536, 1280),
        "what": ("a huge ancient forest guardian boss monster gone wild: a hulking four-legged boar-like beast made of "
                 "twisted dark wood and bark, a glowing sickly green heart visible through a crack in its chest, thick "
                 "thorny vines wrapped around its body, moss and small mushrooms on its back, antler-like branches on its "
                 "head, glowing green eyes. Menacing, powerful stance. " + MONSTER_FRAME),
    },
    "bg_room": {
        "kind": "bg", "gen": (2560, 1440),
        "what": ("a dark enchanted forest clearing as a 2D side-scrolling game background: tall twisted trees with thorny "
                 "brambles, hanging vines, mossy rocks, soft light beams through the canopy, green and teal palette. The "
                 "bottom 30% of the image is flat, open, mossy dirt ground running across the full width. Flat side-on "
                 "view, no perspective tilt. No characters, no people, no animals, no text."),
    },
    "icon_sword": {
        "kind": "icon", "box": 512, "gen": (1024, 1536),
        "what": ("a single plain iron longsword with a simple straight cross guard, a brown leather-wrapped grip and a "
                 "round pommel, standing upright with the point at the top. " + ICON_FRAME),
    },
    "icon_potion": {
        "kind": "icon", "box": 512, "gen": (1280, 1280),
        "what": ("a single small round glass potion bottle with a cork stopper, filled with bright red liquid, with a "
                 "little heart-shaped paper label. " + ICON_FRAME),
    },
    "icon_ring": {
        "kind": "icon", "box": 512, "gen": (1280, 1280),
        "what": "a single silver ring with a tiny curled-up orange fox sleeping on top of the band. " + ICON_FRAME,
    },
    "icon_cap": {
        "kind": "icon", "box": 512, "gen": (1280, 1280),
        "what": ("a single empty simple brown boiled-leather cap with stitched seams and a short neck guard, no person "
                 "inside. " + ICON_FRAME),
    },
}


def prompt_for(direction: dict, subject: dict) -> str:
    style = direction["style"]
    if "monster" in subject["what"]:  # body words meant for people put boots and hands on the boar
        style = style.replace("huge hands", "big chunky feet").replace("big hands", "big feet")
    text = f"{direction['prefix']}{subject['what']} {style}"
    if subject["kind"] == "bg":
        return text
    return f"{RGBA_OPEN} {text} {RGBA_CLOSE}"


def default_seed(subject_key: str) -> int:
    return zlib.crc32(subject_key.encode()) & 0x7FFFFFFF


def write_notes(path: Path, graph: dict) -> None:
    """A readable .txt next to an image: what was sent to ComfyUI (the PNG also carries the graph: drag it into the
    ComfyUI window to see the nodes)."""
    lines = []
    for nid, node in graph.items():
        values = {k: v for k, v in node["inputs"].items() if not (isinstance(v, list) and len(v) == 2)}
        lines.append(f"[{nid}] {node['_meta']['title']}  ({node['class_type']})")
        lines += [f"    {k}: {v}" for k, v in values.items()]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def cut_out(img: Image.Image) -> Image.Image:
    """Clear Qwen's near-zero alpha noise and trim to the figure."""
    a = np.asarray(img.convert("RGBA")).copy()
    a[a[..., 3] < 10] = 0
    out = Image.fromarray(a, "RGBA")
    box = out.getbbox()
    return out.crop(box) if box else out


def pixel_snap(img: Image.Image, size: tuple[int, int], scale: int, colors: int) -> Image.Image:
    """A real pixel grid: sample the generation down to `size` (nearest keeps outlines crisp; box blur smears them),
    hard alpha, a small palette with no dithering, then blow up `scale` times with nearest neighbour."""
    small = img.convert("RGBA").resize(size, Image.NEAREST)
    arr = np.asarray(small).copy()
    opaque = arr[..., 3] >= 128
    rgb = Image.fromarray(arr[..., :3], "RGB").quantize(colors, method=Image.Quantize.MEDIANCUT, kmeans=4,
                                                         dither=Image.Dither.NONE)
    arr[..., :3] = np.asarray(rgb.convert("RGB"))
    arr[..., 3] = np.where(opaque, 255, 0)
    return Image.fromarray(arr, "RGBA").resize((size[0] * scale, size[1] * scale), Image.NEAREST)


def fit(img: Image.Image, subject: dict) -> tuple[int, int]:
    """Final size: sprites `height` tall, icons inside a `box` square."""
    s = subject["height"] / img.height if subject["kind"] == "sprite" else subject["box"] / max(img.size)
    return round(img.width * s), round(img.height * s)


def finish(raw: Image.Image, subject: dict, post: str | None) -> Image.Image:
    if subject["kind"] == "bg":
        if post == "pixel":
            return pixel_snap(raw, (640, 360), 4, 64).convert("RGB")
        return raw.convert("RGB").resize((2560, 1440), Image.LANCZOS)
    img = cut_out(raw)
    w, h = fit(img, subject)
    if post == "pixel":
        scale = 4 if subject["kind"] == "sprite" else 8
        return pixel_snap(img, (round(w / scale), round(h / scale)), scale, 64 if subject["kind"] == "sprite" else 32)
    return img.resize((w, h), Image.LANCZOS)


def main() -> None:
    args = sys.argv[1:]

    def opt(name: str, default: str | None = None) -> str | None:
        if name in args:
            i = args.index(name)
            value = args[i + 1]
            del args[i: i + 2]
            return value
        return default

    host = opt("--host", "http://127.0.0.1:8188")
    seed_opt = opt("--seed")
    steps = int(opt("--steps", "30"))
    variants = int(opt("--variants", "1"))
    pick = opt("--pick")
    flags = {a for a in args if a.startswith("--")}
    names = [a for a in args if not a.startswith("--")]
    if not names:
        sys.exit(__doc__)
    dirs = list(DIRECTIONS) if names[0] == "all" else [names[0]]
    if any(d not in DIRECTIONS for d in dirs):
        sys.exit(f"Directions: {', '.join(DIRECTIONS)} or all")
    subjects = names[1:] or list(SUBJECTS)
    unknown = [s for s in subjects if s not in SUBJECTS]
    if unknown:
        sys.exit(f"Unknown subjects: {', '.join(unknown)} (have {', '.join(SUBJECTS)})")
    nolora = "--nolora" in flags

    comfy = Comfy(host)
    if "--dry" not in flags:
        comfy.check()
    for dkey in dirs:
        d = DIRECTIONS[dkey]
        out_dir = TESTS / "_nolora" / dkey if nolora else TESTS / dkey
        loras = [] if nolora else d["loras"]
        for skey in subjects:
            s = SUBJECTS[skey]
            prompt = prompt_for(d, s)
            seed = int(seed_opt) if seed_opt else default_seed(skey)
            if "--dry" in flags:
                print(f"\n{dkey}/{skey}  seed {seed}  {s['gen']}  loras {loras}  cfg {d['cfg']}\n{prompt}")
                continue
            result = out_dir / f"{skey}.png"
            if pick:
                raw_path = out_dir / "_raw" / f"{skey}_s{pick}.png"
                img = finish(Image.open(raw_path), s, d.get("post"))
                (img.transpose(Image.FLIP_TOP_BOTTOM) if "--flip" in flags else img).save(result)
                print(f"{dkey}/{skey}: {result.relative_to(ROOT)} from seed {pick}")
                continue
            if result.exists() and "--force" not in flags:
                print(f"{dkey}/{skey}: exists (--force regenerates)")
                continue
            for i in range(variants):
                t0 = time.time()
                try:
                    data = comfy.generate(prompt, [], s["gen"], seed + i, steps, loras=loras, cfg=d["cfg"],
                                          negative=NEGATIVE if d["cfg"] > 1 else "")
                except RuntimeError as e:
                    print(f"{dkey}/{skey}: ComfyUI error: {e}")
                    break
                raw_path = out_dir / "_raw" / f"{skey}_s{seed + i}.png"
                raw_path.parent.mkdir(parents=True, exist_ok=True)
                raw_path.write_bytes(data)
                write_notes(raw_path.with_suffix(".txt"), comfy.last_graph)
                if i == 0:
                    finish(Image.open(raw_path), s, d.get("post")).save(result)
                print(f"{dkey}/{skey}: {raw_path.relative_to(ROOT)} ({time.time() - t0:.0f}s)", flush=True)


if __name__ == "__main__":
    main()
