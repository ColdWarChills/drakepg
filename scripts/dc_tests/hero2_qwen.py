"""Our hero on the knight's skeleton, try 2 of the look (2026-11-25): each knight part repainted by Qwen in today's hero's
drawn style (ink lines, cel shading, folds), inside the knight's own outline, the test-2 way.

<venv python> -I -B hero2_qwen.py gen <Part> <seeds a,b,c> [version]   Qwen runs -> art/tests/dc/hero2/qwen/<Part>_v<n>_s<seed>.png
<venv python> -I -B hero2_qwen.py lock <Part> <candidate>               -> art/tests/dc/hero2/paint/<Part>.png (2x the knight's size)
<venv python> -I -B hero2_qwen.py seed                                    copies try 1's paint to hero2/paint (the parts not repainted yet)

Input: try 1's flat repaint of the part (art/tests/dc/hero/paint, today's hero's colours on the knight's outline), 4x,
on flat green. Reference: the matching crop of char_hero_bare (never the whole figure: Qwen copies a whole character).
Lock: alpha = the knight's own (on a carved part, the free end may only lose pixels where Qwen painted none, with holes
filled); every green pixel takes the nearest paint; colours pulled back per material to try 1's palette (so seams
between parts painted in different runs match); downscaled to 2x (DcHero.MakePart takes a 2x PNG at double ppu).
Everything written under art/tests/dc stays out of git (the knight's outlines).
"""
import io
import json
import shutil
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
sys.path.insert(0, str(HERE))
from qwen_batch import Comfy  # noqa: E402
from hero_paint import HERO  # noqa: E402

TRY1 = ROOT / "art/tests/dc/hero/paint"
H2 = ROOT / "art/tests/dc/hero2"
QW = H2 / "qwen"
PAINT = H2 / "paint"
RAW = Path(r"C:/Users/Epic/Desktop/dragonfab/art/raw/char_hero_bare.png")
SCALE = 4
OUT_SCALE = 2
BG = (0, 168, 72)
REFS = {"head": (310, 270, 560, 500), "arm": (200, 470, 360, 910), "fist": (590, 500, 700, 640),
        "legs": (230, 640, 560, 1300), "body": (250, 470, 560, 760)}

PARTS = {
    "Head__Head": dict(ref="head", carve=0, desc=(
        "the head of a cartoon young man seen from the side, facing right: his tanned face (cheek, nose, chin), his ear, "
        "dark brown hair at the back of his head and a red cloth headband across his forehead. His eyes, eyebrows and "
        "mouth are separate pieces put on top later, so this face has no eyes, no eyebrows and no mouth: plain skin "
        "where they go."), colours="tanned skin, dark brown hair, a red headband"),
    "Hair_Front__Hair_Front": dict(ref="head", carve=0, desc=(
        "the front of a young man's spiky dark brown hair, a messy sweep of hair seen from the side, the tips pointing "
        "back and up."), colours="dark brown hair with lighter brown strands"),
    "Arm_R__Arm_R": dict(ref="arm", carve=0.36, desc=(
        "a young man's arm hanging straight down, seen from the side: at the top the upper arm in a loose cream linen "
        "shirt sleeve with soft folds, then the sleeve rolled up into a thick cuff, then his bare tanned wrist, and at "
        "the bottom his clenched fist: rounded knuckles, the fingers curled in, the thumb over them."),
        colours="cream sleeve, tanned skin"),
    "Hand_R_Bottom__Hand_R_Bottom": dict(ref="fist", carve=1.0, desc=(
        "the curled fingers of a young man's clenched fist seen from the side, wrapped round a sword grip: four "
        "rounded fingers in a row, tanned skin."), colours="tanned skin"),
}

LOOK = ("Repaint this piece in exactly the art style of <image2>: the same hand-painted cartoon look, thin dark brown "
        "ink lines inside the shape on folds, creases, knuckles and the edges of the clothing, cel shading with clean "
        "two-tone shadows (light from the right), soft cloth folds, the same warm colours.")
SHAPE = ("Keep the exact same outline and silhouette as <image1>: the same shape, size and position, every edge in the "
         "same place. Fill the whole shape right to its edges: nothing added outside it, no green left inside it. No "
         "dark outline along the outer edge of the piece, no rim light, no pale or golden edge glow. Use <image2> only "
         "as an example of the style, not its shape, pose or framing: copy nothing else from it.")
BACK = "Plain flat green background, the same green as in <image1>, nothing else in the picture, no text."
P = {1: lambda d: (f"Edit <image1>. <image1> is one cut-out piece of a cartoon character on a flat green background: "
                   f"{d['desc']} {LOOK} Keep the colours of <image1>: {d['colours']}. {SHAPE} {BACK}")}


def canvas_for(w, h):
    cw, ch = w * SCALE + 96, h * SCALE + 96
    return (cw + 31) // 32 * 32, (ch + 31) // 32 * 32


def part_input(part):
    t = Image.open(TRY1 / f"{part}.png").convert("RGBA")
    big = t.resize((t.width * SCALE, t.height * SCALE), Image.BICUBIC)
    cw, ch = canvas_for(t.width, t.height)
    off = ((cw - big.width) // 2, (ch - big.height) // 2)
    canvas = Image.new("RGBA", (cw, ch), BG + (255,))
    canvas.alpha_composite(big, off)
    return canvas.convert("RGB"), off


def ref_crop(key):
    c = Image.open(RAW).convert("RGBA").crop(REFS[key])
    bg = Image.new("RGBA", c.size, (255, 255, 255, 255))
    bg.alpha_composite(c)
    img = bg.convert("RGB")
    k = max(1, 768 // max(img.size))
    return img.resize((img.width * k, img.height * k), Image.LANCZOS)


def png(img):
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


def gen(part, seeds, version):
    QW.mkdir(parents=True, exist_ok=True)
    d = PARTS[part]
    inp, _ = part_input(part)
    ref = ref_crop(d["ref"])
    inp.save(QW / f"input_{part}.png")
    c = Comfy("http://127.0.0.1:8188")
    c.check()
    names = [c.upload(f"dc_h2_{part}.png", png(inp)), c.upload(f"dc_h2_ref_{d['ref']}.png", png(ref))]
    prompt = P[version](d)
    for s in seeds:
        out = QW / f"{part}_v{version}_s{s}.png"
        if out.exists():
            continue
        t = time.time()
        img = Image.open(io.BytesIO(c.generate(prompt, names, None, s, 30))).convert("RGB")
        note = ""
        if img.size != inp.size:
            note = f"resized from {img.size}"
            img = img.resize(inp.size, Image.LANCZOS)
        img.save(out)
        dt = round(time.time() - t)
        print("QWEN", out.name, dt, "s", note, flush=True)
        with open(QW / "runs.jsonl", "a") as f:
            f.write(json.dumps({"part": part, "version": version, "seed": s, "seconds": dt, "note": note, "prompt": prompt}) + "\n")


def is_green(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (g - np.maximum(r, b) > 12) & (g > 1.25 * r) & (g > 1.12 * b)


def materials(rgb, alpha):
    """Try 1's flat paint -> a material per pixel (nearest of the HERO tones), -1 outside."""
    names, tones = [], []
    for m, ts in HERO.items():
        for t in ts:
            names.append(m.replace("_hi", ""))
            tones.append(t)
    tones = np.array(tones, float)
    lab = ((rgb[..., None, :].astype(float) - tones) ** 2).sum(-1).argmin(-1)
    mats = np.array(names)[lab]
    mats[alpha < 128] = ""
    return mats


def lock(part, cand):
    d = PARTS[part]
    orig = np.asarray(Image.open(TRY1 / f"{part}.png").convert("RGBA"))
    h, w = orig.shape[:2]
    q = np.asarray(Image.open(QW / f"{cand}.png").convert("RGB")).astype(np.int32)
    _, (x0, y0) = part_input(part)
    region = q[y0:y0 + h * SCALE, x0:x0 + w * SCALE]
    a4 = np.asarray(Image.fromarray(orig[..., 3]).resize((w * SCALE, h * SCALE), Image.BICUBIC)).astype(np.int32)
    paint = ~is_green(region)
    inside = a4 > 127
    m = {"iou": round(float((paint & inside).sum() / (paint | inside).sum()), 4),
         "leak_inside": round(float((~paint & inside).sum() / inside.sum()), 4)}
    alpha = a4.copy()
    if d["carve"] > 0:
        # the free end (the bottom `carve` share) keeps only Qwen's own shape, holes filled, edge softened a little
        own = ndimage.binary_fill_holes(ndimage.binary_closing(paint, iterations=3))
        own = ndimage.binary_opening(own, iterations=2)
        zone = np.zeros_like(own)
        zone[int(h * SCALE * (1 - d["carve"])):] = True
        soft = (ndimage.gaussian_filter(own.astype(float), 1.5) * 255).astype(np.int32)
        alpha = np.where(zone, np.minimum(a4, soft), a4)
        m["carved"] = round(float(((a4 > 127) & (alpha <= 127)).sum() / inside.sum()), 4)
    _, (iy, ix) = ndimage.distance_transform_edt(~paint, return_indices=True)
    filled = region[iy, ix].astype(float)
    # colour lock: per material of try 1, move Qwen's median to try 1's median (shading kept, seams match)
    big1 = np.asarray(Image.fromarray(orig[..., :3]).resize((w * SCALE, h * SCALE), Image.NEAREST))
    mats = materials(big1, a4)
    for mat in set(mats[mats != ""].tolist()):
        sel = (mats == mat) & inside
        if sel.sum() < 200:
            continue
        shift = np.median(big1[sel].astype(float), 0) - np.median(filled[sel], 0)
        mask = ndimage.gaussian_filter(sel.astype(float), 3)[..., None]
        filled = filled + mask * shift * 0.85
        m[f"shift_{mat}"] = [int(v) for v in shift]
    filled = np.clip(filled, 0, 255).astype(np.uint8)
    ow, oh = w * OUT_SCALE, h * OUT_SCALE
    rgb = np.asarray(Image.fromarray(filled).resize((ow, oh), Image.BOX))
    al = np.asarray(Image.fromarray(np.clip(alpha, 0, 255).astype(np.uint8)).resize((ow, oh), Image.BOX))
    PAINT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.dstack([rgb, al]), "RGBA").save(PAINT / f"{part}.png")
    log = PAINT / "locks.json"
    allm = json.loads(log.read_text()) if log.exists() else {}
    allm[part] = dict(candidate=cand, **m)
    log.write_text(json.dumps(allm, indent=1))
    print(part, cand, m)


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "seed":
        PAINT.mkdir(parents=True, exist_ok=True)
        for f in TRY1.glob("*.png"):
            shutil.copy(f, PAINT / f.name)
        print("seeded", len(list(PAINT.glob("*.png"))))
    elif cmd == "gen":
        gen(sys.argv[2], [int(s) for s in sys.argv[3].split(",")], int(sys.argv[4]) if len(sys.argv) > 4 else 1)
    elif cmd == "lock":
        lock(sys.argv[2], sys.argv[3])
