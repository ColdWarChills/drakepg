"""The bare moments: a kept Qwen try -> body (rod out), weapon layer, sword-fist layer, in the moment's order.

<venv python> -I -B bare.py review <try> [<try> ...]   -> art/tests/moments/review_<n>.png (whole + 3x hand per try)
"""
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H, REDO, TESTS, load_rgba  # noqa: E402
import cut  # noqa: E402

# weapon: drawn in front of the body or behind, by moment (a drawing can override it in WEAPON_ORDER)
MOMENTS = {"ready": {"weapon": "front"}, "wind": {"weapon": "back"}, "strike": {"weapon": "front"},
           "follow": {"weapon": "front"}}
# The blade end is read from the picture: the lopsided green stick (blade = the long end, cut.read_long_end; grip3_
# tries) or the two-colour marker (cut.read_marker; grip_/grip2_ tries). Only drawings made before the marker have it
# here, as a screen vector (y down):
BLADE_BY_EYE = {
    # checked by eye on the 1536 px drawing (2026-11-22): the green leaves the fist at the thumb and forefinger end
    "plain_short_ready_s112": (1, -1),
    "plain_short_strike_s311": (1, 0.8),
    # step A's wind-up and follow-through: the end step A declared, never checked; both hold the sword the wrong way
    # round (grip.py fails them). Kept only to test the grip check and to rebuild step A's record.
    "plain_short_wind_s212": (-1, -1),
    "plain_short_follow_s411": (-0.7, 1),
}
# weapon in front of or behind the body, per drawing, from what the drawing shows (default: MOMENTS)
WEAPON_ORDER = {}
THREE_X = 720 / HERO_H  # hero 720 px tall = 3x game size


def moment_of(name: str) -> str:
    if "@" in name:
        return name.split("@")[1].split("_")[0]
    return re.search(r"(ready|wind|strike|follow)", name).group(1)


def drawing_key(name: str) -> str:
    """The Qwen try a picture comes from: 'norm_plain_short_ready_s112' -> 'plain_short_ready_s112'."""
    n = Path(str(name)).stem
    return n[5:] if n.startswith("norm_") else n


def normalize(a: np.ndarray, target: float) -> tuple:
    """Scale the whole drawing about the middle of his soles so his head (hair top to chin) is `target` px: Qwen draws
    the lunges a little bigger. One uniform scale for the whole picture, done once before any armor edit."""
    import cv2
    import gate1
    h = gate1.head_height(a)
    s = target / h
    ys, xs = np.nonzero((a[..., 3] > 40) & ~ndi.binary_dilation(cut.marker_mask(a), iterations=4))  # soles, not the rod
    fx, fy = float(np.median(xs[ys > ys.max() - 30])), float(ys.max())
    M = np.array([[s, 0, fx - s * fx], [0, s, fy - s * fy]], np.float32)
    pre = a.astype(np.float32)
    pre[..., :3] *= pre[..., 3:] / 255
    out = cv2.warpAffine(pre, M, (a.shape[1], a.shape[0]), flags=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
    out = np.clip(out, 0, 255)
    al = out[..., 3:]
    out[..., :3] = np.where(al > 0, out[..., :3] * 255 / np.maximum(al, 1e-3), 0)
    out = np.clip(out + 0.5, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(out[..., 3] < 10, 0, out[..., 3])
    return out, h, s


def read_blade(a: np.ndarray, name: str) -> dict:
    """The fist centre and blade direction: from the two-colour marker when the drawing has one, else from
    BLADE_BY_EYE (drawings made before the marker). Never from the moment's name."""
    key = drawing_key(name)
    if key.startswith(("grip_", "grip2_", "grip4_", "handm")):  # two-colour marker
        return cut.read_marker(a)
    if key.startswith("grip3_") or key.startswith("hand"):  # lopsided one-colour stick: blade = the long end
        return cut.read_long_end(a)
    if key not in BLADE_BY_EYE:
        raise ValueError(f"{key}: no marker and no blade end checked by eye")
    rod = cut.read_rod(a, BLADE_BY_EYE[key])
    rod["source"] = "eye"
    return rod


def build(a: np.ndarray, moment: str, name: str, weapon_order: str | None = None) -> dict:
    rod = read_blade(a, name)
    body = cut.remove_rod(a, rod["mask"])
    wpn = cut.weapon_layer(a.shape, rod)
    fist = cut.fist_mask(a, rod)
    order = weapon_order or WEAPON_ORDER.get(drawing_key(name), MOMENTS[moment]["weapon"])
    return {"rod": rod, "body": body, "weapon": wpn, "fist": fist, "fist_layer": cut.masked(body, fist),
            "order": order}


def compose(parts: dict) -> np.ndarray:
    if parts["order"] == "back":
        out = cut.over(parts["weapon"], parts["body"])
    else:
        out = cut.over(parts["body"], parts["weapon"])
    return cut.over(out, parts["fist_layer"])


def font(n):
    try:
        return ImageFont.truetype("arial.ttf", n)
    except OSError:
        return ImageFont.load_default()


def review(names):
    tiles = []
    for n in names:
        a = load_rgba(REDO / f"{n}.png")
        m = moment_of(n)
        try:
            p = build(a, m, n)
            comp = compose(p)
            info = f"{n}: angle {p['rod']['angle']:.0f}, gap {p['rod']['gap']:.0f}, rod w {p['rod']['width']:.0f}, segs {p['rod']['segments']}"
            cx, cy = p["rod"]["center"]
        except ValueError as e:
            comp, info, (cx, cy) = a, f"{n}: {e}", (768, 768)
        bg = Image.new("RGBA", a.shape[1::-1], (226, 220, 205, 255))
        whole = Image.alpha_composite(bg, Image.fromarray(comp))
        whole_s = whole.resize((560, 560), Image.LANCZOS)
        raw_s = Image.alpha_composite(bg, Image.fromarray(a)).resize((560, 560), Image.LANCZOS)
        # 3x crop: the near arm, from the fist to the shoulder (the head zone's centre stands in for the shoulder)
        import place
        hb = place.head_box(place.classes(a))
        hy, hx = (np.nonzero(hb)[0].mean(), np.nonzero(hb)[1].mean()) if hb.any() else (cy, cx)
        x0, x1 = sorted((cx, hx))
        y0, y1 = sorted((cy, hy + 120))
        side = max(x1 - x0, y1 - y0) + 220
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        r = min(side / 2, 280)
        box = (int(mx - r), int(my - r), int(mx + r), int(my + r))
        hand = whole.crop(box).resize((round(2 * r * THREE_X),) * 2, Image.LANCZOS)
        hand_raw = Image.alpha_composite(bg, Image.fromarray(a)).crop(box).resize(hand.size, Image.LANCZOS)
        t = Image.new("RGB", (560 * 2 + hand.width * 2 + 30, 600), (245, 240, 230))
        t.paste(raw_s.convert("RGB"), (0, 30))
        t.paste(whole_s.convert("RGB"), (565, 30))
        t.paste(hand_raw.convert("RGB"), (1130, 30))
        t.paste(hand.convert("RGB"), (1135 + hand.width, 30))
        ImageDraw.Draw(t).text((6, 4), info, fill=(30, 20, 10), font=font(20))
        tiles.append(t)
    sheet = Image.new("RGB", (tiles[0].width, 600 * len(tiles)), (245, 240, 230))
    for i, t in enumerate(tiles):
        sheet.paste(t, (0, 600 * i))
    return sheet


def norm(names):
    """norm_<try>.png: the try scaled about his soles so his head matches moments.json head_ref (once, before edits)."""
    import json
    target = json.loads((REDO / "moments.json").read_text())["head_ref"]
    for n in names:
        out = REDO / f"norm_{n}.png"
        if out.exists():
            print("exists", out.name)
            continue
        a, h, s = normalize(load_rgba(REDO / f"{n}.png"), target)
        Image.fromarray(a).save(out)
        print(out.name, "head", h, "scale", round(s, 4))


if __name__ == "__main__":
    if sys.argv[1] == "norm":
        norm(sys.argv[2:])
    elif sys.argv[1] == "review":
        names = sys.argv[3:]
        s = review(names)
        s.thumbnail((1568, 1568))
        s.save(TESTS / f"review_{sys.argv[2]}.png")
        print(TESTS / f"review_{sys.argv[2]}.png", s.size)
