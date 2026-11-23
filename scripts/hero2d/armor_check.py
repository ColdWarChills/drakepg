"""Does an armor edit keep the cutting pose? Zone by zone against cut2_s21: the parts the armor must not move
(head, forearms, fists, trousers, boots) are compared by mask overlap (IoU) and centre shift in px.
<venv python> -I -B armor_check.py mail_s31 mail_s32 -> art/tests/hero2d/r2/armor_overlay.png + numbers printed."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from cutlib import key_purple, poly_mask, unmix_purple  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
KIT = ROOT / "art/redo/hero2d/r2"
TESTS = ROOT / "art/tests/hero2d/r2"
BASE = "cut2_s21"
HEAD = [(540, 170), (1010, 170), (1010, 438), (830, 438), (812, 452), (760, 452), (712, 434), (540, 434)]


def load(name):
    raw = np.asarray(Image.open(KIT / f"{name}.png").convert("RGB"))
    a = key_purple(raw)
    rgb = unmix_purple(raw, a)
    fa = a > 0.5
    r, g, b = rgb[..., 0] + 1, rgb[..., 1], rgb[..., 2]
    skin = (g / r < 0.76) & (b / r < 0.45) & (r > 120) & fa
    teal = (g > r + 3) & (b > r - 1) & fa
    brown = (r >= g) & (g >= b - 6) & (r < 150) & ~skin & fa
    red = (g / r < 0.42) & (r > 90) & fa
    H, W = fa.shape
    YY, XX = np.mgrid[0:H, 0:W]
    head = fa & poly_mask(fa.shape, HEAD)
    zones = {
        "head (hair, face, band)": head,
        "headband": red & poly_mask(fa.shape, HEAD),
        "near forearm + fist": skin & (XX < 640) & (YY > 560) & (YY < 900),
        "far forearm + fist": skin & (XX > 900) & (YY > 560) & (YY < 900),
        "trousers": teal & (YY > 700) & (YY < 1100),
        "boots": brown & (YY > 1010),
    }
    return raw, fa, zones


def iou(a, b):
    return float((a & b).sum() / max(1, (a | b).sum()))


def cen(m):
    ys, xs = np.nonzero(m)
    return np.array([xs.mean(), ys.mean()]) if len(xs) else np.array([np.nan, np.nan])


if __name__ == "__main__":
    names = sys.argv[1:] or ["mail_s31", "mail_s32"]
    base_raw, base_fa, base_z = load(BASE)
    rows = []
    for n in names:
        raw, fa, z = load(n)
        ys, xs = np.nonzero(fa)
        bys, bxs = np.nonzero(base_fa)
        scale = (ys.max() - ys.min()) / (bys.max() - bys.min())
        res = {"height ratio": round(float(scale), 3), "top shift": int(ys.min() - bys.min()),
               "bottom shift": int(ys.max() - bys.max())}
        for k in base_z:
            d = cen(z[k]) - cen(base_z[k])
            res[k] = (round(iou(z[k], base_z[k]), 2), round(float(np.hypot(*d)), 1))
        rows.append((n, raw, fa, res))
        print(n, res)

    # sheet: base | each try | overlay (base red outline-ish, try green) with the numbers under each
    try:
        font = ImageFont.truetype("arial.ttf", 20)
    except OSError:
        font = ImageFont.load_default()
    crop = (420, 120, 1160, 1300)
    cw, ch = (crop[2] - crop[0]) // 2, (crop[3] - crop[1]) // 2
    sheet = Image.new("RGB", (cw * (1 + 2 * len(rows)), ch + 260), (236, 230, 216))
    d = ImageDraw.Draw(sheet)
    sheet.paste(Image.fromarray(base_raw).crop(crop).resize((cw, ch)), (0, 40))
    d.text((6, 8), f"{BASE} (bare)", fill=(40, 30, 20), font=font)
    for i, (n, raw, fa, res) in enumerate(rows):
        x = cw * (1 + 2 * i)
        sheet.paste(Image.fromarray(raw).crop(crop).resize((cw, ch)), (x, 40))
        ov = np.full(base_fa.shape + (3,), 255, np.uint8)
        ov[base_fa & ~fa] = (220, 40, 40)
        ov[fa & ~base_fa] = (30, 150, 60)
        ov[fa & base_fa] = (200, 200, 200)
        sheet.paste(Image.fromarray(ov).crop(crop).resize((cw, ch)), (x + cw, 40))
        d.text((x + 6, 8), n, fill=(40, 30, 20), font=font)
        d.text((x + cw + 6, 8), "overlay", fill=(40, 30, 20), font=font)
        lines = [f"height x{res['height ratio']}, top {res['top shift']:+d} px, soles {res['bottom shift']:+d} px"]
        lines += [f"{k}: overlap {v[0]:.2f}, moved {v[1]} px" for k, v in res.items() if isinstance(v, tuple)]
        for j, t in enumerate(lines):
            d.text((x + 6, ch + 52 + j * 28), t, fill=(40, 30, 20), font=font)
    d.text((6, ch + 52), "overlay: grey = both,", fill=(40, 30, 20), font=font)
    d.text((6, ch + 80), "red = bare only, green = armor only", fill=(40, 30, 20), font=font)
    out = TESTS / "armor_overlay.png"
    sheet.save(out)
    print(out, sheet.size)
