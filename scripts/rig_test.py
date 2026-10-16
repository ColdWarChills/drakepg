#!/usr/bin/env python3
"""Cut-out rig test for the art directions (docs/PLANS.md Art): one hero split into parts and animated (idle + attack).

  python scripts/rig_test.py flash base     # generate the Warrior in a rig-friendly pose, then the same image with no arms
  python scripts/rig_test.py flash build    # cut the parts, animate: rig.gif, rig_parts.png, rig_pose.png
Options: --seed N (base generation), --force (regenerate the base images)

Parts come from two generations: the hero with arms hanging a little away from the body, and a Qwen edit of it with
both arms removed (the body layer, so nothing is missing behind a raised arm). The arms are what differs between the
two; the body layer is split into legs, torso and head at the lines in art/tests/<direction>/rig/params.json (fractions
of the figure's height, written with guesses on the first build: look at rig_parts.png and adjust). The sword is the
direction's own icon_sword. Pixel art is posed at its native size and rotated with nearest neighbour, as an engine
would show it.
"""
import io
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import DIRECTIONS, NEGATIVE, TESTS, cut_out, pixel_snap, write_notes  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

HEIGHT = 960  # final figure height (2x game size)
GIF_HEIGHT = 480

BASE_WHAT = ("a young warrior hero, a lean young man with short messy brown hair and a determined look, wearing a plain "
             "steel breastplate over a blue knee-length tunic, a brown leather belt, brown leather gloves, dark grey "
             "trousers and tall brown leather boots. Full body, three-quarter view facing right, standing straight with "
             "feet a little apart. Both arms hang straight down, held a little away from the body so there is a clear gap "
             "between each arm and the torso, hands are empty fists. No weapon, no cape, no shoulder guards. The whole "
             "figure visible from head to boots with empty margin around it. No shadow on the ground.")
NOARMS = ("Edit <image1>: remove both of the hero's arms completely, from the shoulders down, including the sleeves, "
          "gloves and fists, as if he were a puppet with the arms taken off. Keep everything else exactly the same: the "
          "head, face, hair, breastplate, tunic, belt, legs, boots, pose, size, position and art style, on the same "
          "background.")

DEFAULT_PARAMS = {
    "neck": 0.16,       # head/torso split, fraction of figure height from the top
    "hip": 0.50,        # torso/legs split
    "overlap": 0.025,   # how far each upper layer reaches under the next
    "sword_grip": 0.82,  # grip point on the sword art, fraction from the top
    "sword_size": 0.62,  # sword length / figure height
    "sword_angle": 50,   # degrees the blade leans forward from straight up while the arm hangs
    "fist": 0.90,       # fist centre along the arm, fraction from the shoulder
    "swap_arms": False,  # the near (sword) arm is the one on the left of the image: facing right in three-quarter
                         # view the far arm tucks behind the chest. Set this if a generation has it the other way.
}


def white(img: Image.Image) -> bytes:
    bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    bg.alpha_composite(img.convert("RGBA"))
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "PNG")
    return buf.getvalue()


def base(dkey: str, seed: int, force: bool) -> None:
    d = DIRECTIONS[dkey]
    rig = TESTS / dkey / "rig"
    rig.mkdir(parents=True, exist_ok=True)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    neg = NEGATIVE if d["cfg"] > 1 else ""
    base_path = rig / "base_raw.png"
    if force or not base_path.exists():
        prompt = f"{RGBA_OPEN} {d['prefix']}{BASE_WHAT} {d['style']} {RGBA_CLOSE}"
        base_path.write_bytes(comfy.generate(prompt, [], (1024, 1536), seed, 30, loras=d["loras"], cfg=d["cfg"], negative=neg))
        write_notes(base_path.with_suffix(".txt"), comfy.last_graph)
        print(f"{dkey}: {base_path.name} (seed {seed})")
    noarms_path = rig / "noarms_raw.png"
    if force or not noarms_path.exists():
        ref = comfy.upload(f"drakepg_rig_{dkey}.png", white(Image.open(base_path)))
        prompt = f"{RGBA_OPEN} {NOARMS} {d['style']} {RGBA_CLOSE}"
        noarms_path.write_bytes(comfy.generate(prompt, [ref], None, seed, 30, loras=d["loras"], cfg=d["cfg"], negative=neg))
        write_notes(noarms_path.with_suffix(".txt"), comfy.last_graph)
        print(f"{dkey}: {noarms_path.name}")


def alpha(img: Image.Image) -> np.ndarray:
    a = np.asarray(img.convert("RGBA"))[..., 3].astype(np.float32) / 255
    a[a < 10 / 255] = 0
    return a


def components(mask: np.ndarray) -> list[np.ndarray]:
    """Connected regions of a boolean mask, largest first (4-connectivity, flood fill)."""
    h, w = mask.shape
    labels = np.zeros((h, w), np.int32)
    regions = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if labels[y0, x0]:
            continue
        n = len(regions) + 1
        stack = [(y0, x0)]
        labels[y0, x0] = n
        pts = []
        while stack:
            y, x = stack.pop()
            pts.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not labels[yy, xx]:
                    labels[yy, xx] = n
                    stack.append((yy, xx))
        regions.append(labels == n)
    return sorted(regions, key=lambda r: -r.sum())


def layer(src: np.ndarray, mask: np.ndarray) -> Image.Image:
    out = src.copy()
    out[..., 3] = (out[..., 3] * mask).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def cut_parts(dkey: str, params: dict) -> dict:
    """Parts at the final scale, each with its pivot in figure coordinates (feet centre = origin is not used; plain
    image coordinates of the assembled figure)."""
    rig = TESTS / dkey / "rig"
    b = Image.open(rig / "base_raw.png").convert("RGBA")
    n = Image.open(rig / "noarms_raw.png").convert("RGBA").resize(b.size)
    ab, an = alpha(b), alpha(n)
    rgb_b = np.asarray(b)[..., :3].astype(np.int16)
    rgb_n = np.asarray(n)[..., :3].astype(np.int16)
    differs = (np.abs(rgb_b - rgb_n).sum(axis=2) > 90) & (ab > 0.5)
    arms = (ab > 0.5) & ((an < 0.5) | differs)
    # Small specks from edit noise go; the two big regions are the arms.
    arms_img = Image.fromarray((arms * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(7))
    regions = components(np.asarray(arms_img) > 127)[:2]
    regions.sort(key=lambda r: np.nonzero(r)[1].mean())  # left to right
    front_m, back_m = (regions[1], regions[0]) if params["swap_arms"] else (regions[0], regions[1])

    src_b = np.asarray(b).copy()
    src_b[..., 3] = (ab * 255).astype(np.uint8)
    src_n = np.asarray(n).copy()
    src_n[..., 3] = (an * 255).astype(np.uint8)
    body_rows = np.nonzero(an.max(axis=1) > 0)[0]
    top, bottom = body_rows[0], body_rows[-1]
    fig_h = bottom - top
    y_neck = top + params["neck"] * fig_h
    y_hip = top + params["hip"] * fig_h
    ov = params["overlap"] * fig_h
    rows = np.arange(an.shape[0])[:, None]

    def soft(m: np.ndarray) -> np.ndarray:  # feather a cut a few pixels so seams don't show
        return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255

    parts = {
        "arm_back": layer(src_b, soft(back_m)),
        "legs": layer(src_n, soft(np.broadcast_to(rows >= y_hip - ov, an.shape).astype(np.float32))),
        "torso": layer(src_n, soft(np.broadcast_to((rows >= y_neck - ov) & (rows < y_hip + ov), an.shape).astype(np.float32))),
        "head": layer(src_n, soft(np.broadcast_to(rows < y_neck + ov, an.shape).astype(np.float32))),
        "arm_front": layer(src_b, soft(front_m)),
    }

    def shoulder(m: np.ndarray) -> tuple[float, float, float]:
        ys, xs = np.nonzero(m)
        y0, y1 = ys.min(), ys.max()
        top_band = xs[ys < y0 + (y1 - y0) * 0.12]
        return float(top_band.mean()), float(y0 + (y1 - y0) * 0.06), float(y1 - y0)

    sb, sf = shoulder(back_m), shoulder(front_m)
    xs = np.nonzero(an[int(y_hip)] > 0.5)[0]
    hip = (float(xs.mean()) if len(xs) else b.width / 2, float(y_hip))
    xs = np.nonzero(an[int(y_neck)] > 0.5)[0]
    neck = (float(xs.mean()) if len(xs) else b.width / 2, float(y_neck))
    pivots = {"arm_back": sb[:2], "arm_front": sf[:2], "hip": hip, "neck": neck}
    fist = (sf[0], sf[1] + params["fist"] * sf[2])

    # Everything to the final scale, cropped to the figure with room for the swing.
    s = HEIGHT / fig_h
    sword = cut_out(Image.open(TESTS / dkey / "icon_sword.png"))
    sw_h = params["sword_size"] * HEIGHT
    sword = sword.resize((max(1, round(sword.width * sw_h / sword.height)), round(sw_h)), Image.LANCZOS)
    scaled = {k: v.resize((round(b.width * s), round(b.height * s)), Image.LANCZOS) for k, v in parts.items()}
    pv = {k: (x * s, y * s) for k, (x, y) in pivots.items()}
    return {"parts": scaled, "pivots": pv, "fist": (fist[0] * s, fist[1] * s), "sword": sword,
            "top": top * s, "bottom": bottom * s, "size": scaled["torso"].size}


def rotate_about(img: Image.Image, angle: float, pivot: tuple[float, float], resample) -> Image.Image:
    """PIL angle (counter-clockwise on screen) around a pivot, same canvas."""
    return img.rotate(angle, resample=resample, center=pivot)


def rot(p: tuple[float, float], c: tuple[float, float], angle: float) -> tuple[float, float]:
    """Point p turned like Image.rotate(angle, center=c) turns pixels (counter-clockwise on screen)."""
    a = math.radians(angle)
    dx, dy = p[0] - c[0], p[1] - c[1]
    return c[0] + dx * math.cos(a) + dy * math.sin(a), c[1] - dx * math.sin(a) + dy * math.cos(a)


def pose(rig: dict, params: dict, t: dict, pixel: bool) -> Image.Image:
    """One frame. t: lean (torso around hip), head, front, back (arm angles), wrist (sword against the front arm),
    dy (body bob), dx (lunge). Angles in degrees, counter-clockwise on screen (a hanging arm swings forward)."""
    parts, pv = rig["parts"], rig["pivots"]
    resample = Image.NEAREST if pixel else Image.BICUBIC
    W, H = rig["size"]
    pad = round(H * 0.45)
    canvas = Image.new("RGBA", (W + 2 * pad, H + pad), (0, 0, 0, 0))
    ox, oy = pad + t.get("dx", 0), pad

    def put(img: Image.Image, dx: float = 0, dy: float = 0) -> None:
        canvas.alpha_composite(img, (round(ox + dx), round(oy + dy)))

    hip = pv["hip"]
    lean = t.get("lean", 0)
    dy = t.get("dy", 0)
    # Upper body points follow the lean around the hip, then the bob.
    neck = rot(pv["neck"], hip, lean)
    sh_b = rot(pv["arm_back"], hip, lean)
    sh_f = rot(pv["arm_front"], hip, lean)

    def arm(name: str, shoulder_now: tuple[float, float], angle: float) -> Image.Image:
        img = rotate_about(parts[name], angle + lean, pv[name], resample)
        return img, shoulder_now[0] - pv[name][0], shoulder_now[1] - pv[name][1] + dy

    img, ax, ay = arm("arm_back", sh_b, t.get("back", 0))
    put(img, ax, ay)
    put(parts["legs"])
    put(rotate_about(parts["torso"], lean, hip, resample), 0, dy)
    head = rotate_about(parts["head"], lean + t.get("head", 0), pv["neck"], resample)
    put(head, neck[0] - pv["neck"][0], neck[1] - pv["neck"][1] + dy)

    # Sword in the front fist: turns with the arm, blade leaning forward by sword_angle while the arm hangs.
    front = t.get("front", 0)
    total = front + lean
    fist = rot(rig["fist"], pv["arm_front"], total)
    fist = (fist[0] + sh_f[0] - pv["arm_front"][0], fist[1] + sh_f[1] - pv["arm_front"][1] + dy)
    sword = rig["sword"]
    grip = (sword.width / 2, params["sword_grip"] * sword.height)
    big = Image.new("RGBA", (sword.height * 2, sword.height * 2), (0, 0, 0, 0))
    big.alpha_composite(sword, (round(sword.height - grip[0]), round(sword.height - grip[1])))
    big = big.rotate(-params["sword_angle"] + total + t.get("wrist", 0), resample=resample, center=(sword.height, sword.height))
    put(big, fist[0] - sword.height, fist[1] - sword.height)

    img, ax, ay = arm("arm_front", sh_f, front)
    put(img, ax, ay)
    return canvas


def ease(a: float, b: float, x: float) -> float:
    x = max(0.0, min(1.0, x))
    return a + (b - a) * (0.5 - 0.5 * math.cos(math.pi * x))


def frames(rig: dict, params: dict, pixel: bool) -> list[tuple[Image.Image, int]]:
    H = HEIGHT
    out = []
    for i in range(16):  # idle: breathe, arms sway
        p = 2 * math.pi * i / 16
        out.append((pose(rig, params, {"dy": -H * 0.008 * (1 - math.cos(p)), "front": 4 * math.sin(p),
                                       "back": -4 * math.sin(p), "head": 1.5 * math.sin(p + 0.6)}, pixel), 70))
    out = out * 2
    # attack: a forward chop. Wind up with the fist raised in front and the blade over the shoulder, swing down and
    # forward with a lunge, hold, recover.
    up = {"front": 140, "wrist": -75, "back": 20, "lean": 5, "dx": -H * 0.02}
    up2 = {**up, "front": 145, "wrist": -80, "lean": 6, "dx": -H * 0.025}
    hit = {"front": 70, "wrist": -122, "back": -20, "lean": -9, "dx": H * 0.07}
    hit2 = {**hit, "front": 66, "wrist": -120, "lean": -8}
    keys = [(5, 50, {}, up), (2, 90, up, up2), (3, 30, up2, hit), (3, 90, hit, hit2), (6, 60, hit2, {})]
    for n, ms, a, b in keys:
        for i in range(1, n + 1):
            x = i / n
            t = {k: ease(a.get(k, 0), b.get(k, 0), x) for k in set(a) | set(b)}
            out.append((pose(rig, params, t, pixel), ms))
    return out


def build(dkey: str) -> None:
    d = TESTS / dkey
    rig_dir = d / "rig"
    params_path = rig_dir / "params.json"
    params = dict(DEFAULT_PARAMS)
    if params_path.exists():
        params.update(json.loads(params_path.read_text()))
    else:
        params_path.write_text(json.dumps(params, indent=2))
    pixel = DIRECTIONS[dkey].get("post") == "pixel"
    rig = cut_parts(dkey, params)
    if pixel:
        # Pose at the native pixel size (a quarter), then blow up 4x: rotations stay on the grid like in an engine.
        rig = shrink(rig, 4)
    seq = frames(rig, params, pixel)
    bbox = None
    for img, _ in seq:
        bb = img.getbbox()
        bbox = bb if bbox is None else (min(bbox[0], bb[0]), min(bbox[1], bb[1]), max(bbox[2], bb[2]), max(bbox[3], bb[3]))
    m = 8
    bbox = (max(0, bbox[0] - m), max(0, bbox[1] - m), bbox[2] + m, bbox[3] + m)
    crops = [(img.crop(bbox), ms) for img, ms in seq]
    if pixel:
        crops = [(c.resize((c.width * 4, c.height * 4), Image.NEAREST), ms) for c, ms in crops]
    # Full-size idle frame for the battle mock.
    first = crops[0][0]
    first.crop(first.getbbox()).save(d / "rig_pose.png")
    # GIF on a light backdrop (GIF alpha is 1-bit; a flat colour keeps the edges clean).
    s = GIF_HEIGHT / crops[0][0].height * 1.15
    gif = []
    for c, ms in crops:
        bg = Image.new("RGBA", c.size, (236, 232, 222, 255))
        bg.alpha_composite(c)
        gif.append(bg.convert("RGB").resize((round(c.width * s), round(c.height * s)), Image.NEAREST if pixel else Image.LANCZOS))
    gif[0].save(d / "rig.gif", save_all=True, append_images=gif[1:], duration=[ms for _, ms in crops], loop=0,
                optimize=False, disposal=2)
    parts_sheet(rig, d / "rig_parts.png", pixel)
    print(f"{dkey}: rig.gif ({len(gif)} frames), rig_parts.png, rig_pose.png; params {params_path.relative_to(TESTS.parent.parent)}")


def shrink(rig: dict, f: int) -> dict:
    parts = {k: pixel_snap(v, (round(v.width / f), round(v.height / f)), 1, 64) for k, v in rig["parts"].items()}
    sw = rig["sword"]
    return {**rig, "parts": parts, "pivots": {k: (x / f, y / f) for k, (x, y) in rig["pivots"].items()},
            "fist": (rig["fist"][0] / f, rig["fist"][1] / f),
            "sword": pixel_snap(sw, (max(1, round(sw.width / f)), round(sw.height / f)), 1, 32),
            "size": parts["torso"].size}


def parts_sheet(rig: dict, path: Path, pixel: bool) -> None:
    """The parts laid out side by side, pivots marked."""
    items = [(k, rig["parts"][k]) for k in ("head", "torso", "arm_back", "arm_front", "legs")] + [("sword", rig["sword"])]
    crops = []
    for k, img in items:
        bb = img.getbbox()
        piv = {"arm_back": rig["pivots"]["arm_back"], "arm_front": rig["pivots"]["arm_front"], "head": rig["pivots"]["neck"],
               "torso": rig["pivots"]["hip"]}.get(k)
        c = img.crop(bb)
        if pixel:
            c = c.resize((c.width * 4, c.height * 4), Image.NEAREST)
            piv = piv and ((piv[0] - bb[0]) * 4, (piv[1] - bb[1]) * 4)
        else:
            piv = piv and (piv[0] - bb[0], piv[1] - bb[1])
        crops.append((c, piv))
    h = max(c.height for c, _ in crops)
    sheet = Image.new("RGBA", (sum(c.width for c, _ in crops) + 40 * (len(crops) + 1), h + 80), (236, 232, 222, 255))
    draw = ImageDraw.Draw(sheet)
    x = 40
    for c, piv in crops:
        y = 40 + (h - c.height) // 2
        sheet.alpha_composite(c, (x, y))
        if piv:
            px, py = x + piv[0], y + piv[1]
            draw.ellipse((px - 9, py - 9, px + 9, py + 9), outline=(220, 40, 40, 255), width=4)
        x += c.width + 40
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    args = sys.argv[1:]
    seed = 4242
    if "--seed" in args:
        i = args.index("--seed")
        seed = int(args[i + 1])
        del args[i:i + 2]
    if len(args) < 2 or args[0] not in DIRECTIONS or args[1] not in ("base", "build"):
        sys.exit(__doc__)
    if args[1] == "base":
        base(args[0], seed, "--force" in args)
    else:
        build(args[0])
