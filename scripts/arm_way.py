#!/usr/bin/env python3
"""Hero animation rework (docs/PLANS.md, "One hero, a way that works for hands and elbows"): still tests of drawn sword
arms. A body pose rendered from the bones stage without its near arm (or the base drawing) is edited by Qwen (local
ComfyUI) so that the arm holding the sword is drawn, not bent from stiff pieces. Tries go to art/redo/armway/ (not in
git); nothing here touches the game, the bones kit or art/raw.

  python scripts/arm_way.py gen wind_high wind_shoulder --seeds 2   # -> art/redo/armway/<variant>_s<seed>.png
  python scripts/arm_way.py sheet                                    # every try side by side -> art/redo/armway/_sheet.png

Inputs: BODY_DIR/<moment>_noarm.png (1536 square, magenta key), rendered from the stage with BoneStage.Hold
(sword_cut_j5c) and the near arm and weapon hidden; the sword picture is unity/Bones/hero/iron_sword.png.
"""
import io
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "art" / "redo" / "armway"
SWORD = ROOT / "unity" / "Bones" / "hero" / "iron_sword.png"
BASE = ROOT / "art" / "raw" / "char_hero_bare.png"
BODY_DIR = Path(sys.argv[sys.argv.index("--bodies") + 1]) if "--bodies" in sys.argv else OUT / "bodies2"
FIRST_SEED = 7200
GUARD_AT = float(sys.argv[sys.argv.index("--guard") + 1]) if "--guard" in sys.argv else None
ALPHA_FLOOR = 10

STYLE = ("the same flat cartoon art style with hard-edged cel shadows, thin dark outlines and a warm golden rim light "
         "along his edges")
GRIP = ("His fist is closed firmly around the brown leather handle of the sword from <image2>: the four fingers wrap "
        "over the handle, the thumb closes over the fingers, the crossguard sits just above the fist and the round "
        "pommel just below it, and the blade, the handle and the pommel run on one straight line through the fist. "
        "The sword is exactly the sword in <image2>: the same plain iron blade, squared crossguard, leather handle and "
        "pommel, the same length relative to him as in <image2> compared with a man's arm (about as long as his arm).")
def keep_body(other_arm: bool) -> str:
    other = "his other arm and fist, " if other_arm else ""
    return ("Keep everything else exactly as it is in <image1>: his legs, hips, body, head, face, hair, headband, "
            + other + "the clothes, the same position, size and proportions, and " + STYLE + ". The sleeves are the "
            "same cream shirt sleeves, rolled up just above the elbow, and the forearms and fists are bare skin. Only "
            "one sword, only in his sword hand. No other weapon, no ground, no shadow, no motion lines, no effects, no "
            "text.")


NEAR = ("Draw only his missing sword arm: his right arm, the arm nearer to us, which starts at his shoulder on the left "
        "side of the picture (the back of his body, since he faces right).")

BOTH = ("Draw both of his arms; <image1> shows him without arms. His sword arm is his right arm, the arm nearer to "
        "us, which starts at his shoulder on the left side of the picture (the back of his body, since he faces right); "
        "his other arm starts at the far shoulder, partly behind his chest.")
FAR_BACK = ("His other arm is pulled back behind him for balance, bent at the elbow, its clenched fist near the back of "
            "his waist.")

# name: (moment, body picture suffix, text, his other arm stays as rendered)
VARIANTS = {
    # The big overhead wind-up: the fist above and a little behind the head, the blade hanging down his back.
    "wind_high": ("wind", "noarm", NEAR + " He is winding up for a big overhead sword cut: that arm is raised high, "
                  "the upper arm pointing up beside his head, the elbow bent and lifted to about the height of the top "
                  "of his head, the fist above and slightly behind his head. The wrist is cocked back so the blade hangs "
                  "behind his head and shoulders, pointing down and back on a slant, its tip behind his back.", True),
    # The sword cocked on the shoulder (as in old fencing books): fist by the chin, elbow down, blade back over it.
    "wind_shoulder": ("wind", "noarm", NEAR + " He is winding up for a strong sword cut with the sword cocked on his "
                      "shoulder: the fist is up beside his near shoulder at the height of his chin, the elbow bent tight "
                      "and pointing down in front of his chest, and the blade rests back over his shoulder, pointing back "
                      "and a little up behind his head.", True),
    # Raised high, the blade pointing up and back (what the first wind_high tries drew, asked for on purpose).
    "wind_up": ("wind", "noarm", NEAR + " He is winding up for a big overhead sword cut: that arm is raised high, the "
                "upper arm pointing up beside his head, the elbow bent, the fist just above and a little behind his "
                "head, and the sword held high above him, the blade pointing up and back behind him on a slant.", True),
    "stand_low": ("stand", "noarms", BOTH + " His sword arm hangs low and back beside his rear hip, the elbow a little "
                  "bent, and the sword in that fist trails down and back behind him, its tip low behind his rear leg, a "
                  "little above the ground. His other arm is bent in front of his chest, the elbow down close to his "
                  "side, the clenched fist raised in front of his chest as a guard.", False),
    # the accepted stance (stand_sword_j6) with only the sword arm drawn: low and back, fist clear of the body
    "stand_near": ("stand", "noarm", NEAR + " His sword arm hangs down and a little back, the elbow slightly bent, "
                   "the fist beside his rear hip and clear of his body, and the sword in that fist trails down and "
                   "back behind him on a slant, its tip low behind his rear foot, a little above the ground.", True),
    "hit": ("hit", "noarms", BOTH + " He has just struck: his sword arm swings forward and down in front of his body "
            "toward the right, nearly straight with the elbow only slightly bent; the fist is out in front of him at "
            "about the height of his belt, and the blade continues the line of his arm forward and down on a slant "
            "toward the lower right, the whole blade clearly visible. " + FAR_BACK, False),
    "after": ("after", "noarms", BOTH + " The cut has carried through: his sword arm is stretched down in front of "
              "him, the fist low in front of his front knee, and the blade points down and forward at the ground in "
              "front of his front foot, its tip just above the ground. " + FAR_BACK, False),
}


def keyed_white(path: Path) -> Image.Image:
    """The stage render with its magenta key turned white (Qwen sees a figure on white, as in hero_poses)."""
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    key = (a[..., 0] > 200) & (a[..., 1] < 70) & (a[..., 2] > 200)
    out = a.copy()
    out[key] = 255
    return Image.fromarray(out.astype(np.uint8))


def png(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


def sword_on_white() -> Image.Image:
    s = Image.open(SWORD).convert("RGBA")
    bg = Image.new("RGBA", (s.width + 80, s.height + 80), (255, 255, 255, 255))
    bg.alpha_composite(s, (40, 40))
    return bg.convert("RGB")


def prompt(text: str, other_arm: bool) -> str:
    return f"{RGBA_OPEN} Edit <image1>. {text} {GRIP} {keep_body(other_arm)} {RGBA_CLOSE}"


def gen(names: list[str], seeds: int, first: int) -> None:
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    OUT.mkdir(parents=True, exist_ok=True)
    sword = comfy.upload("armway_sword.png", png(sword_on_white()))
    for name in names:
        moment, suffix, text, other_arm = VARIANTS[name]
        body = comfy.upload(f"armway2_{moment}_{suffix}.png", png(keyed_white(BODY_DIR / f"{moment}_{suffix}.png")))
        for i in range(seeds):
            seed = first + i
            out = OUT / f"{name}_s{seed}.png"
            if out.exists():
                continue
            print(out.relative_to(ROOT), flush=True)
            try:
                out.write_bytes(comfy.generate(prompt(text, other_arm), [body, sword], None, seed, 30))
            except RuntimeError as e:
                print(f"  ComfyUI error: {e}", flush=True)
                continue
            (out.with_suffix(".txt")).write_text(prompt(text, other_arm), encoding="utf-8")


def sheet() -> None:
    tries = sorted(OUT.glob("*_s*.png"))
    if not tries:
        return
    th = 600
    row = []
    for p in tries:
        im = Image.open(p).convert("RGBA")
        bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
        bg.alpha_composite(im)
        row.append(bg.convert("RGB").resize((int(im.width * th / im.height), th)))
    out = Image.new("RGB", (sum(r.width for r in row), th), "white")
    x = 0
    for r in row:
        out.paste(r, (x, 0))
        x += r.width
    out.save(OUT / "_sheet.png")
    print(OUT / "_sheet.png", out.size)


# --- Composing a try with the game's own sword ---------------------------------------------------------------------
# The drawn sword is never kept: Qwen draws the arm and the fist around a handle, the game's sword picture is put through
# the fist along the drawn handle's line (blade on the side given), the fist drawn on top. Two results per try:
#   whole: Qwen's whole drawing (its body too);  pinned: only the drawn arm, on the bones body it was drawn onto.
SPRITE_GRIP = (72.0, 500.0)      # rig.json weapons.iron_sword gx/gy (blade up, pommel down)
SWORD_LEN_RENDER = 712.0 * 1.45 / 1.7  # tip to pommel: 712 px measured at camera size 1.45; bodies2 use 1.7


def key_mask(rgb: np.ndarray) -> np.ndarray:
    return (rgb[..., 0] > 200) & (rgb[..., 1] < 70) & (rgb[..., 2] > 200)


def box_mean(m: np.ndarray, r: int) -> np.ndarray:
    """Share of set pixels in the (2r+1) box around each pixel (PIL box blur: fast)."""
    from PIL import ImageFilter
    img = Image.fromarray(m.astype(np.float32) if False else (m * 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.BoxBlur(r))).astype(np.float32) / 255.0


def grow(m: np.ndarray, r: int) -> np.ndarray:
    return box_mean(m, r) > 0.002


def shrink(m: np.ndarray, r: int) -> np.ndarray:
    return box_mean(m, r) > 0.998


def big_parts(m: np.ndarray, min_px: int, n: int = 1) -> np.ndarray:
    """The n biggest parts of a mask (each at least min_px), holes filled (PIL flood fills; no scipy here)."""
    from PIL import ImageDraw
    img = Image.fromarray(np.where(m, 255, 0).astype(np.uint8)).copy()  # fromarray shares a read-only buffer
    parts = []
    label = 1
    while label <= 250:
        a = np.asarray(img)
        ys, xs = np.nonzero(a == 255)
        if len(xs) == 0:
            break
        ImageDraw.floodfill(img, (int(xs[0]), int(ys[0])), label)
        size = int((np.asarray(img) == label).sum())
        if size >= min_px:
            parts.append((size, label))
        label += 1
    a = np.asarray(img)
    keep = np.isin(a, [lab for _, lab in sorted(parts, reverse=True)[:n]])
    pad = Image.fromarray(np.pad(np.where(keep, 255, 0).astype(np.uint8), 1)).copy()
    ImageDraw.floodfill(pad, (0, 0), 128)
    return ~(np.asarray(pad)[1:-1, 1:-1] == 128)


def tidy(rgba: np.ndarray, parts: int = 3) -> np.ndarray:
    """Drop the key's magenta fringe and every loose fleck or faint ghost line away from his body."""
    r, g, b, a = (rgba[..., i].astype(np.int16) for i in range(4))
    out = rgba.copy()
    out[..., 3][(r - g > 60) & (b - g > 60)] = 0
    solid = out[..., 3] > 128
    if solid.any():
        out[..., 3][~grow(big_parts(solid, 1500, parts), 3)] = 0
    return out


def compose(try_path: Path, moment: str, blade_hint: float, sword_front: bool, out_dir: Path, arms: int = 1) -> dict:
    """blade_hint: rough blade direction in degrees (image coords: 0 = right, 90 = down, -90 = up)."""
    t = np.asarray(Image.open(try_path).convert("RGBA")).astype(np.int16)
    t[..., 3][t[..., 3] < ALPHA_FLOOR] = 0
    border = np.concatenate([t[:8].reshape(-1, 4), t[-8:].reshape(-1, 4), t[:, :8].reshape(-1, 4), t[:, -8:].reshape(-1, 4)])
    if np.median(border[:, 3]) > 128:   # some tries come back on an opaque background (the base drawing's purple)
        bg = np.median(border[:, :3], 0)
        t[..., 3][np.abs(t[..., :3] - bg).sum(-1) < 90] = 0
    body = np.asarray(Image.open(BODY_DIR / f"{moment}_{'noarms' if arms == 2 else 'noarm'}.png").convert("RGB")).astype(np.int16)
    bkey = key_mask(body)
    drawn = t[..., 3] >= 128
    diff = np.abs(t[..., :3] - body).sum(-1)
    arm = big_parts(shrink(grow(drawn & (bkey | (diff > 90)), 6), 6) & drawn, 3000, arms)
    # the shoulder end: what Qwen redrew around the arm's root (the bones torso's sleeve cap would show under it)
    arm = arm | (grow(arm, 30) & drawn & ~bkey & (diff > 30))
    rgb = t[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    # In the arm, keep warm skin, cream cloth and rim light, and dark outline only where it hugs them; everything else
    # there is the drawn sword (its metal, its outline, its pommel and guard) and goes.
    warm = ((rgb[..., 0] - rgb[..., 2]) >= 35) & (rgb[..., 0] >= 150)   # skin and sleeve; the drawn brown handle
    # (red under 150) is not his, or the fist would grow along it and the game's sword would sit too far out
    dark = mx < 80
    other = arm & drawn & ~warm & ~dark          # the drawn sword's metal (and anything else not skin or cloth)
    hug = box_mean(arm & warm, 4) > box_mean(other, 4) + 0.05
    arm_keep = arm & drawn & (warm | (dark & hug))
    skin_or_cloth = warm
    sword_px = arm & drawn & ~arm_keep
    ys, xs = np.nonzero(sword_px)
    pts = np.stack([xs, ys], 1).astype(float)
    c = pts.mean(0)
    ax = np.linalg.svd(pts - c, full_matrices=False)[2][0]
    if np.dot(ax, [np.cos(np.radians(blade_hint)), np.sin(np.radians(blade_hint))]) < 0:
        ax = -ax
    hint_ax = ax.copy()
    # the fist: skin pixels of the arm within 70 px of the handle line; the grip is its centre projected on the line
    fy, fx = np.nonzero(arm & skin_or_cloth)
    fp = np.stack([fx, fy], 1).astype(float)
    rel = fp - c
    along = rel @ ax
    across = np.abs(rel @ np.array([-ax[1], ax[0]]))
    fist = fp[(across < 45) & (along > (pts - c).dot(ax).min() - 20) & (along < (pts - c).dot(ax).max() + 20)]
    grip = c + ax * float(((fist - c) @ ax).mean()) if len(fist) else c
    # the blade is the long side: more drawn sword beyond the grip on that side than on the pommel side
    along_s = (pts - grip) @ ax
    if (along_s > 0).sum() < 0.6 * (along_s < 0).sum():
        ax = -ax
    elif (along_s < 0).sum() < 0.6 * (along_s > 0).sum():
        pass
    else:
        ax = hint_ax
    angle = float(np.degrees(np.arctan2(ax[1], ax[0])))
    # Everything along the drawn sword's line that is not skin or cloth goes: its outline, guard and pommel too (a band
    # along the line, wider at the fist's two ends where the guard and the pommel are). Under it the bones body shows.
    yy, xx = np.mgrid[0:t.shape[0], 0:t.shape[1]]
    dx, dy = xx - grip[0], yy - grip[1]
    al = dx * ax[0] + dy * ax[1]
    ac = np.abs(-dx * ax[1] + dy * ax[0])
    al_pts = (pts - grip) @ ax
    fist_half = float(np.abs((fist - grip) @ ax).max()) if len(fist) else 40.0
    # the whole drawn sword: out to its full length on the blade side (its tip can lie apart from the arm), the
    # pommel side to just past the fist
    band = drawn & (al > min(al_pts.min(), -fist_half) - 40) & (al < max(al_pts.max(), SWORD_LEN_RENDER) + 40) & (
        (ac < 42) | ((ac < 80) & (al > -fist_half - 45) & (al < fist_half + 55)))
    warm_thick = grow(shrink(warm & band, 3), 3)   # the fist's skin; the drawn blade's thin golden rim light is not
    drop = band & (~warm_thick | (al > 0.6 * SWORD_LEN_RENDER))   # out along the blade nothing of his can be thick
    arm_keep = arm_keep & ~drop
    sword_px = sword_px | drop
    # the fist's edge on the blade side: walking the sword's line from the pommel side, the last place where his skin
    # still covers the line. The game's guard goes right against it, so the handle sits in the fist.
    skin = arm_keep & warm
    near = (ac < 14) & (al > -200) & (al < 0.4 * SWORD_LEN_RENDER)
    a_s, cov = al[near], skin[near]
    covered = {}
    for a0 in range(-200, int(0.4 * SWORD_LEN_RENDER), 3):
        sel = (a_s >= a0) & (a_s < a0 + 3)
        covered[a0] = bool(sel.any() and cov[sel].mean() > 0.3)
    edge = None
    hits = [a0 for a0, c in covered.items() if c]
    if hits:
        a0 = min(hits, key=abs)          # the fist, nearest the grip; walk out along the blade until skin stops
        misses = 0
        while a0 + 3 in covered and misses < 2:
            a0 += 3
            misses = 0 if covered[a0] else misses + 1
            if covered[a0]:
                edge = a0 + 3.0
        edge = edge if edge is not None else min(hits, key=abs) + 3.0
    # where Qwen drew the guard (the widest cross-section of its sword just past the grip): when his forearm lies along
    # the sword's line (wound up, blade down his back) the skin runs on past the fist, and the drawn guard is nearer
    guard_at, widest = None, 25.0
    acr = np.abs((pts - grip) @ np.array([-ax[1], ax[0]]))
    alp = (pts - grip) @ ax
    for a0 in range(0, 160, 6):
        sel = (alp >= a0) & (alp < a0 + 6)
        if sel.sum() > 20 and float(np.percentile(acr[sel], 95)) > widest:
            widest, guard_at = float(np.percentile(acr[sel], 95)), a0 + 3.0
    # the game's sword through the fist
    spr = Image.open(SWORD).convert("RGBA")
    a = np.asarray(spr)[..., 3] > 40
    sy = np.nonzero(a.any(1))[0]
    scale = SWORD_LEN_RENDER / float(sy.max() - sy.min())
    spr = spr.resize((round(spr.width * scale), round(spr.height * scale)), Image.LANCZOS)
    # where the fist sits on the handle: its blade-side edge against the guard, as Qwen draws it (the rig's grip
    # point is a little lower, for the stiff fist pictures)
    widths = (np.asarray(spr)[..., 3] > 40).sum(1)
    guard_rows = np.nonzero(widths > 0.8 * widths.max())[0]
    guard_bottom = float(guard_rows.max())
    gx = SPRITE_GRIP[0] * scale
    # the guard against the fist, the handle in it: not a long bare handle (a blind check read Qwen's own
    # long-handle grips as a spear shaft)
    gy = guard_bottom + (edge if edge is not None else fist_half) + 2
    if GUARD_AT is not None:                  # set by hand for a pose where the rule misses (--guard <px>)
        gy = guard_bottom + GUARD_AT
    print(f"grip {grip.round().tolist()} skin edge {edge} drawn guard {guard_at} used {round(gy - guard_bottom)}")
    side = 2 * spr.height + 8                     # square, so the turned blade is never clipped
    big = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    big.alpha_composite(spr, (round(side / 2 - gx), round(side / 2 - gy)))
    rot = big.rotate(-(angle + 90), resample=Image.BICUBIC, center=(side / 2, side / 2))
    sword = Image.new("RGBA", (t.shape[1], t.shape[0]), (0, 0, 0, 0))
    ox, oy = round(grip[0] - side / 2), round(grip[1] - side / 2)
    sword.paste(rot, (ox, oy), rot)
    # layers
    arm_rgba = np.zeros_like(t)
    keep_arm = arm_keep
    arm_rgba[keep_arm] = t[keep_arm]
    arm_rgba[..., 3][keep_arm] = 255
    arm_rgba = tidy(arm_rgba.astype(np.uint8), arms)
    arm_img = Image.fromarray(arm_rgba)
    keep_arm = arm_rgba[..., 3] > 0          # flecks the tidy took off the arm stay in the drawn body below
    whole_body = t.copy()
    whole_body[..., 3][sword_px | drop] = 0
    whole_body[..., 3][keep_arm] = 0
    bones = np.zeros(body.shape[:2] + (4,), np.uint8)
    bones[..., :3] = body
    bones[..., 3] = np.where(bkey, 0, 255)
    res = {}
    bones_img = Image.fromarray(tidy(bones, 1))
    whole_img = Image.new("RGBA", bones_img.size, (0, 0, 0, 0))
    fill = np.asarray(bones_img).copy()      # where the drawn sword lay over his body, the same pose's bones body
    fill[..., 3][~grow(sword_px | drop, 2)] = 0
    whole_img.alpha_composite(Image.fromarray(fill))
    whole_img.alpha_composite(Image.fromarray(whole_body.astype(np.uint8)))
    whole_img = Image.fromarray(tidy(np.asarray(whole_img), 3))
    for name, under in (("whole", whole_img), ("pinned", bones_img)):
        canvas = Image.new("RGBA", under.size, (0, 0, 0, 0))
        layers = [under, sword, arm_img] if sword_front else [sword, under, arm_img]
        for layer in layers:
            canvas.alpha_composite(layer)
        out = out_dir / f"{try_path.stem}_{name}.png"
        canvas.save(out)
        res[name] = out
    res["angle"], res["grip"] = angle, grip.tolist()
    return res


# --- Old beside new at the designer's size -------------------------------------------------------------------------
GAME = 1536 / 3.4 / 100      # render px per 1280-frame px is 4.518 (camera size 1.7 vs the game's 100 px per unit)
SIZES = {"064": 0.64 / GAME, "3x": 3.0 / GAME}
BG = (146, 160, 140)         # a muted swamp green, about the fen backdrop behind the hero


def keyed_rgba(path: Path) -> Image.Image:
    a = np.asarray(Image.open(path).convert("RGBA")).copy()
    a[..., 3][key_mask(a[..., :3].astype(np.int16))] = 0
    return Image.fromarray(a)


def figure_box(img: Image.Image) -> tuple[int, int, int, int]:
    a = np.asarray(img)[..., 3] >= 128
    ys, xs = np.nonzero(a)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def label(img: Image.Image, text: str) -> Image.Image:
    from PIL import ImageDraw, ImageFont
    try:
        font = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    out = Image.new("RGB", (img.width, img.height + 26), (30, 30, 30))
    out.paste(img, (0, 26))
    ImageDraw.Draw(out).text((6, 3), text, fill=(235, 235, 235), font=font)
    return out


def show(pairs: list[str], size: str, name: str, upper: bool = False) -> Path:
    """pairs: moment:file[:file...] (files in composed/, or 'old' for the bones take at that moment). One row per
    moment, same framing for every picture of a row; upper=True crops head to hips (the arms) instead of the figure."""
    k = SIZES[size]
    rows = []
    for spec in pairs:
        moment, *files = spec.split(":")
        imgs, names = [], []
        for f in files:
            if f == "old":
                imgs.append(keyed_rgba(BODY_DIR / f"{moment}_full.png"))
                names.append(f"{moment}: old (sword_cut_j5c)")
            else:
                imgs.append(Image.open(OUT / "composed" / f"{f}.png").convert("RGBA"))
                names.append(f"{moment}: new ({f.split('_s')[0]}, {f.rsplit('_', 1)[1]})")
        boxes = [figure_box(i) for i in imgs]
        x0, y0 = min(b[0] for b in boxes) - 30, min(b[1] for b in boxes) - 30
        x1, y1 = max(b[2] for b in boxes) + 30, max(b[3] for b in boxes) + 30
        if upper:
            y1 = y0 + int((y1 - y0) * 0.75)
        cells = []
        for img, nm in zip(imgs, names):
            crop = img.crop((x0, y0, x1, y1))
            crop = crop.resize((max(1, round(crop.width * k)), max(1, round(crop.height * k))), Image.LANCZOS)
            bg = Image.new("RGBA", crop.size, BG + (255,))
            bg.alpha_composite(crop)
            cells.append(label(bg.convert("RGB"), nm if size != "064" else nm.split(":")[1].strip()))
        row = Image.new("RGB", (sum(c.width for c in cells) + 8 * len(cells), max(c.height for c in cells)), (30, 30, 30))
        x = 0
        for c in cells:
            row.paste(c, (x, 0))
            x += c.width + 8
        rows.append(row)
    out = Image.new("RGB", (max(r.width for r in rows), sum(r.height for r in rows) + 8 * len(rows)), (30, 30, 30))
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + 8
    path = OUT / f"{name}_{size}.png"
    out.save(path)
    print(path, out.size)
    return path


# --- The proof sheets: old beside new, at the designer's size and as close-ups of the arms ---------------------------
PROOF = ROOT / "art" / "tests" / "sheets" / "armway"
PICKS = {"wind": "wind_high_s7200_whole", "hit": "hit_s7201_whole", "after": "after_s7201_whole"}
MOMENTS = {"stand": "standing", "wind": "wound up", "hit": "the hit", "after": "after the hit"}
BASE_H = 958       # the base drawing at the bones hero's size (head to belt matched: 472 px there, 438 here)


def base_drawing() -> Image.Image:
    a = np.asarray(Image.open(ROOT / "art" / "raw" / "char_hero_bare.png").convert("RGBA")).copy()
    a[..., 3][np.abs(a[..., :3].astype(np.int16) - [167, 55, 209]).sum(-1) < 60] = 0
    im = Image.fromarray(tidy(a, 1))
    im = im.crop(figure_box(im))
    return im.resize((round(im.width * BASE_H / im.height), BASE_H), Image.LANCZOS)


def on_bg(img: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", img.size, BG + (255,))
    bg.alpha_composite(img)
    return bg.convert("RGB")


def grid(rows: list[list[Image.Image]], gap: int = 8) -> Image.Image:
    rws = []
    for cells in rows:
        r = Image.new("RGB", (sum(c.width for c in cells) + gap * (len(cells) - 1), max(c.height for c in cells)), (30, 30, 30))
        x = 0
        for c in cells:
            r.paste(c, (x, 0))
            x += c.width + gap
        rws.append(r)
    out = Image.new("RGB", (max(r.width for r in rws), sum(r.height for r in rws) + gap * (len(rws) - 1)), (30, 30, 30))
    y = 0
    for r in rws:
        out.paste(r, (0, y))
        y += r.height + gap
    return out


def fit(img: Image.Image, width: int = 1480) -> Image.Image:
    return img if img.width <= width else img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)


def proof() -> list[Path]:
    PROOF.mkdir(parents=True, exist_ok=True)
    old = {m: Image.fromarray(tidy(np.asarray(keyed_rgba(BODY_DIR / f"{m}_full.png")), 1)) for m in MOMENTS}
    new = {m: Image.open(OUT / "composed" / f"{f}.png").convert("RGBA") for m, f in PICKS.items()}
    new["stand"] = old["stand"]
    base = base_drawing()
    # 1. the designer's size: one column per moment, old on top, new below, same framing in a column
    k = SIZES["064"]
    top, bottom = [], []
    for m, nm in MOMENTS.items():
        b = [figure_box(old[m]), figure_box(new[m])]
        box = (min(x[0] for x in b) - 20, min(x[1] for x in b) - 20, max(x[2] for x in b) + 20, max(x[3] for x in b) + 20)
        cell = lambda im: on_bg(im.crop(box)).resize((round((box[2] - box[0]) * k), round((box[3] - box[1]) * k)), Image.LANCZOS)
        top.append(label(cell(old[m]), f"{nm}: old"))
        bottom.append(label(cell(new[m]), f"{nm}: " + ("same" if m == "stand" else "new")))
    small_base = label(on_bg(base).resize((round(base.width * k), round(base.height * k)), Image.LANCZOS), "drawing")
    sheet1 = grid([[small_base] + top, [Image.new("RGB", small_base.size, (30, 30, 30))] + bottom])
    # 2. close-ups of hands and elbows at 3x: head to belt, old | new, same framing in a row
    k = SIZES["3x"]
    rows = []
    sb = base.crop((0, 0, base.width, int(base.height * 0.62)))
    rows.append([label(on_bg(sb).resize((round(sb.width * k), round(sb.height * k)), Image.LANCZOS), "base drawing"),
                 None])
    for m in ("stand", "wind", "hit", "after"):
        b = [figure_box(old[m]), figure_box(new[m])]
        y0, y1 = min(x[1] for x in b) - 20, max(x[3] for x in b)
        y1 = y0 + int((y1 - y0) * 0.66)
        cols = np.nonzero(np.asarray(old[m])[y0:y1, :, 3].max(0) + np.asarray(new[m])[y0:y1, :, 3].max(0) > 128)[0]
        box = (int(cols.min()) - 20, y0, int(cols.max()) + 20, y1)
        cell = lambda im: on_bg(im.crop(box)).resize((round((box[2] - box[0]) * k), round((box[3] - box[1]) * k)), Image.LANCZOS)
        if m == "stand":
            rows[0][1] = label(cell(old[m]), "standing: the same in both (no new try beat it)")
        else:
            rows.append([label(cell(old[m]), f"{MOMENTS[m]}: old"), label(cell(new[m]), f"{MOMENTS[m]}: new")])
    sheet2 = grid(rows)
    paths = [PROOF / "cut_moments_064.png", PROOF / "cut_arms_3x.png"]
    fit(sheet1).save(paths[0])
    fit(sheet2).save(paths[1])
    for p_ in paths:
        print(p_, Image.open(p_).size)
    return paths


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    seeds = int(sys.argv[sys.argv.index("--seeds") + 1]) if "--seeds" in sys.argv else 2
    if "--seeds" in sys.argv:
        args.remove(str(seeds))
    if "--bodies" in sys.argv:
        args.remove(str(BODY_DIR))
    if "--guard" in sys.argv:
        args.remove(sys.argv[sys.argv.index("--guard") + 1])
    if args and args[0] == "gen":
        gen(args[1:], seeds, FIRST_SEED)
    elif args and args[0] == "compose":
        # compose <try.png> <blade_hint_deg> front|back   (moment and arms from the try's variant name)
        try_path = Path(args[1])
        moment, suffix, _, _ = VARIANTS[try_path.stem.rsplit("_s", 1)[0]]
        r = compose(try_path, moment, float(args[2]), args[3] == "front", OUT / "composed", 2 if suffix == "noarms" else 1)
        print({k: (str(v) if isinstance(v, Path) else v) for k, v in r.items()})
    elif args and args[0] == "show":
        # show <name> <size 064|3x> [upper] moment:old:<composed stem>...
        upper = "upper" in args
        show([a for a in args[3:] if a != "upper"], args[2], args[1], upper)
    elif args and args[0] == "proof":
        proof()
    elif args and args[0] == "sheet":
        sheet()
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
