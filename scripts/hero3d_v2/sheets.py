"""Compose the judging pictures from render.py's frames (PIL, outside Blender).
<venv python> -I scripts/hero3d_v2/sheets.py <mode> <frames dir> <out png> [repo root]
modes: overlay (model over the drawn front/side views), turnaround, bends, look (writes look_gamesize.png and
look_3x.png next to <out png>; needs the repo root for the backdrop and the drawn hero)."""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

MODE, D, OUT = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
ROOT = Path(sys.argv[4]) if len(sys.argv) > 4 else Path(__file__).resolve().parents[2]
META = json.loads((D / "meta.json").read_text()) if (D / "meta.json").exists() else {}
FONT = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 22)
SMALL = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
HERO_SPOT = (231, 586)                   # the game's hero spot (feet) in the 1280x720 design frame
BG = (232, 228, 220)


def label(im, text, font=FONT, fill=(30, 26, 22)):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, im.width, 34), fill=(250, 248, 244))
    d.text((10, 4), text, font=font, fill=fill)
    return im


def panel(name, scale=0.5):
    im = Image.open(D / f"{name}.png").convert("RGBA")
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    p = Image.new("RGBA", im.size, BG + (255,))
    p.alpha_composite(im)
    return label(p.convert("RGB"), META.get(name, {}).get("label", name))


def row(ims, gap=8):
    W = sum(i.width for i in ims) + gap * (len(ims) - 1)
    H = max(i.height for i in ims)
    out = Image.new("RGB", (W, H), (40, 36, 32))
    x = 0
    for i in ims:
        out.paste(i, (x, 0))
        x += i.width + gap
    return out


def backdrop():
    bg = Image.open(ROOT / "art/raw/bg_fen_road.png").convert("RGBA")
    return bg.resize((1280, 720), Image.LANCZOS)


RIM = {"colour": (210, 176, 106), "width_px_720p": 0.8}  # the drawn hero's pale warm edge (measured, see HOWTO)


def rim(im, k):
    """The pale edge: dilate the alpha by RIM width (game pixels; k render px per game px), fill the new ring with the
    rim colour, under the picture. Outer silhouette only."""
    from scipy import ndimage as ndi
    a = np.asarray(im)[..., 3].astype(float) / 255
    r = RIM["width_px_720p"] * k
    d = ndi.distance_transform_edt(a < 0.5)
    ring = np.clip(r + 0.5 - d, 0, 1)                   # anti-aliased band of width r outside the alpha edge
    base = np.zeros(a.shape + (4,), np.uint8)
    base[..., :3] = RIM["colour"]
    base[..., 3] = (ring * 255).astype(np.uint8)
    out = Image.fromarray(base)
    out.alpha_composite(im)
    return out


def place(bg, hero, ground, k):
    """Paste a render (RGBA) scaled by k so its ground pixel lands on the hero spot."""
    h = hero.resize((round(hero.width * k), round(hero.height * k)), Image.LANCZOS)
    x = round(HERO_SPOT[0] - ground[0] * k)
    y = round(HERO_SPOT[1] - ground[1] * k)
    bg.alpha_composite(h, (x, y)) if x >= 0 and y >= 0 else bg.paste(h, (x, y), h)
    return bg


if MODE == "overlay":
    outs = []
    for view, ren in (("front", "ov_front"), ("right", "ov_right")):
        dr = Image.open(ROOT / f"art/redo/animway/cutB/{view}.png").convert("RGB")
        r = Image.open(D / f"{ren}.png").convert("RGBA")
        a = np.asarray(r)[..., 3:] / 255.0
        blend = np.asarray(dr) * (1 - 0.55 * a) + np.asarray(r)[..., :3] * 0.55 * a
        outs.append(label(Image.fromarray(blend.astype(np.uint8)), f"{view}: model at 55% over the drawing"))
        outs.append(label(r.convert("RGB") if False else Image.alpha_composite(Image.new("RGBA", r.size, BG + (255,)), r).convert("RGB"), f"{view}: model"))
        outs.append(label(dr.copy(), f"{view}: drawing"))
    row(outs[:3]).save(OUT.with_name(OUT.stem + "_front.png"))
    row(outs[3:]).save(OUT.with_name(OUT.stem + "_right.png"))
elif MODE == "turnaround":
    names = sorted(k for k in META if k.startswith("t"))
    row([panel(n, 0.5) for n in names]).save(OUT)
elif MODE == "face_turn":
    names = sorted(k for k in META if k.startswith("f") and k[1].isdigit())
    row([panel(n, 1.0) for n in names]).save(OUT)
elif MODE == "bends":
    names = [k for k in META if k.startswith("b_")] + [k for k in META if k.startswith("s_")]
    ims = [panel(n, 0.5) for n in names]
    rows = [row(ims[i:i + 4]) for i in range(0, len(ims), 4)]
    W = max(r.width for r in rows)
    out = Image.new("RGB", (W, sum(r.height + 8 for r in rows)), (40, 36, 32))
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + 8
    out.save(OUT)
    zoom = []                                      # the joints at full render size (6x game size)
    for n, box in (("b_elbow140", (150, 250, 850, 1000)), ("b_knee90", (150, 900, 850, 1650)),
                   ("b_armfwd", (150, 250, 850, 1000)), ("b_overhead", (150, 0, 850, 750)),
                   ("s_overhead", (150, 0, 850, 750)), ("s_knee90", (150, 900, 850, 1650))):
        if (D / f"{n}.png").exists():
            im = Image.open(D / f"{n}.png").convert("RGBA")
            p = Image.new("RGBA", im.size, BG + (255,))
            p.alpha_composite(im)
            zoom.append(label(p.crop(box).convert("RGB"), META.get(n, {}).get("label", n), SMALL))
    row(zoom).save(OUT.with_name(OUT.stem + "_zoom.png"))
elif MODE == "look":
    from scipy import ndimage as ndi
    m = META["look"]
    k = 240 / 1.75 / m["ppm"]                       # render pixels -> game pixels
    hero = rim(Image.open(D / "look.png").convert("RGBA"), 1 / k)
    bare = rim(Image.open(D / "look_bare.png").convert("RGBA"), 1 / k)
    game = place(backdrop(), hero, m["ground"], k)
    game.convert("RGB").save(OUT.with_name("look_gamesize.png"))
    # the drawn hero at 240 px tall, feet centre on the same spot
    dr = Image.open(ROOT / "art/raw/char_hero_bare.png").convert("RGBA")
    al = np.asarray(dr)[..., 3] > 40
    ys, xs = np.nonzero(al)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    dr = dr.crop(box)
    kk = 240 / dr.height
    a2 = np.asarray(dr)[..., 3] > 40
    feet = np.nonzero(a2[-int(dr.height * 0.04):].any(0))[0]
    ground = ((feet.min() + feet.max()) / 2, dr.height)
    drawn = place(backdrop(), dr, ground, kk)
    crop = (HERO_SPOT[0] - 125, HERO_SPOT[1] - 270, HERO_SPOT[0] + 125, HERO_SPOT[1] + 22)

    def x3(im):
        return im.crop(crop).resize(((crop[2] - crop[0]) * 3, (crop[3] - crop[1]) * 3), Image.NEAREST).convert("RGB")

    # the sword as the game draws it today (a fight screen with the drawn hero and the drawn training sword), x3
    fight = ROOT / "art/tests/hero3d_v2/unity/fight_without.png"
    if fight.exists():
        fx, fy = 330, 495                              # the drawn hero's feet in that screenshot
        fc = Image.open(fight).convert("RGB").crop((fx - 125, fy - 270, fx + 125, fy + 22))
        fc = fc.resize((fc.width * 3, fc.height * 3), Image.NEAREST)
        row([label(x3(game), "new 3D sword (game size x3)"), label(fc, "drawn sword in the game today (x3)")]).save(
            OUT.with_name("sword_compare.png"))
    gbare = place(backdrop(), bare, m["ground"], k)
    row([label(x3(gbare), "new 3D, bare hands (game size x3)"), label(x3(game), "new 3D, with sword"),
         label(x3(drawn), "drawn hero (char_hero_bare)")]).save(OUT.with_name("look_3x.png"))

    # silhouettes: both normalised to the same height, feet centre on the same point
    def norm_mask(alpha, gx=None):
        mk = alpha > 128
        ys_, xs_ = np.nonzero(mk)
        y0, y1 = ys_.min(), ys_.max()
        Hh = y1 - y0 + 1
        if gx is None:
            ft = np.nonzero(mk[y1 - int(Hh * 0.04):y1 + 1].any(0))[0]
            gx = (ft.min() + ft.max()) / 2
        im = Image.fromarray((mk * 255).astype(np.uint8)).crop((int(gx - Hh * 0.45), y0, int(gx + Hh * 0.45), y1 + 1))
        return np.asarray(im.resize((360, 400), Image.BILINEAR)) > 127, y0, y1, gx, Hh

    sd, *_ = norm_mask(np.asarray(dr)[..., 3])
    sb, by0, by1, bgx, bH = norm_mask(np.asarray(bare)[..., 3])
    ss, *_ = norm_mask(np.asarray(hero)[..., 3])
    iou = (sb & sd).sum() / (sb | sd).sum()
    print(f"SILHOUETTE IoU new bare vs drawn: {iou:.3f}")

    def sil(mk, title):
        im = Image.new("RGB", mk.shape[::-1], (245, 243, 238))
        im.paste((0, 0, 0), mask=Image.fromarray((mk * 255).astype(np.uint8)))
        return label(im.resize((mk.shape[1] * 2, mk.shape[0] * 2), Image.NEAREST), title, SMALL)

    ov = np.zeros(sd.shape + (3,), np.uint8) + 245
    ov[sd & ~sb] = (220, 60, 50)
    ov[sb & ~sd] = (50, 90, 220)
    ov[sb & sd] = (30, 30, 30)
    ovi = label(Image.fromarray(ov).resize((720, 800), Image.NEAREST), f"overlap (red drawn only, blue 3D only) IoU {iou:.2f}", SMALL)
    row([sil(sb, "new 3D stance"), sil(ss, "new 3D, with sword"), sil(sd, "drawn hero"), ovi]).save(
        OUT.with_name("silhouette.png"))
    # landmarks as fractions of height from the feet centre (x forward = screen right, y up), bare render
    print("LANDMARKS (x, y) / height from the feet centre; drawn: head(0.02,0.85) fist_L(0.21,0.75) "
          "fist_R(-0.175,0.49) feet -0.21/+0.17")
    for nm, (px, py, pz) in m["landmarks"].items():
        print(f"  {nm:7s} ({(px - bgx) / bH:+.3f}, {(by1 - py) / bH:.3f})  depth {pz:.3f}")
    # sword crossing the head, hair or the shirt above the belt (render masks)
    msk = {n: np.asarray(Image.open(D / f"mask_{n}.png"))[..., 3] > 128 for n in ("Sword", "Head", "Hair", "Shirt",
                                                                                  "Belt")}
    belt_top = np.nonzero(msk["Belt"].any(1))[0].min()
    shirt_hi = msk["Shirt"].copy()
    shirt_hi[belt_top:] = False
    for n, mk in (("Head", msk["Head"]), ("Hair", msk["Hair"]), ("Shirt above belt", shirt_hi)):
        print(f"SWORD over {n}: {(msk['Sword'] & mk).sum()} px (render 4x)")
print("wrote", OUT)
