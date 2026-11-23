"""Round 2 stills (gate before any motion): the hand, the cuff and the look at the three holds and the two wrist extremes.
<venv python> -I -B r2_stills.py [tag]  -> art/tests/hero_joints/r2/fist_4x<tag>.png, cuff_4x<tag>.png, look<tag>.png"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

import numpy as np
from PIL import Image, ImageDraw

import rig_r2 as rig
from jcommon import RAW, ROOT, TESTS, fit

OUT = TESTS / "r2"
OUT.mkdir(exist_ok=True)
TAG = sys.argv[1] if len(sys.argv) > 1 else ""
BG = np.array([0.82, 0.80, 0.76], np.float32)

# key poses: arm given as round 1's two straight parts (a1, b); kind B solves its own elbow for the same wrist
STILLS = {
    "ready": dict(a1=70.0, b=70.0, turn=44.5),
    "wind-up": dict(a1=-145.0, b=-60.0, turn=62.0, lean=-7.0, head=3.5, shoulder=(0.0, -10.0), dy=4.0, sq=0.98,
                    far=-8.0, fx=8.0, fy=-4.0),
    "strike": dict(a1=52.0, b=7.0, turn=21.5, lean=10.0, head=-5.0, shoulder=(6.0, 3.0), dy=-1.0, far=8.0, fx=-18.0,
                   fy=10.0),
    "cut, blade lags (turn 77)": dict(a1=-114.0, b=-36.0, turn=77.0, lean=-4.0, head=2.0, shoulder=(2.0, -6.0)),
    "strike overshoot (turn 10)": dict(a1=64.0, b=9.0, turn=10.0, lean=12.0, head=-6.0, shoulder=(8.0, 4.0), far=10.0,
                                       fx=-22.0, fy=12.0),
}


def pose_B(k, sign=None, stretch=1.0):
    p = dict(k)
    target = rig.wrist_A(k["a1"], k["b"])
    if stretch != 1.0:
        target = target * rig.stretched_len(stretch) / rig.L
    sg = (np.sign(k["b"]) or 1.0) if sign is None else sign
    p["a1"], p["b"] = rig.ik(target, sg, stretch)
    p["stretch"] = stretch
    return p


def label(im, text, size=18):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 8 + 8 * len(text), 22], fill=(255, 255, 255))
    d.text((5, 4), text, fill=(0, 0, 0))
    return im


def main():
    parts = rig.Parts()
    fr = rig.Frame(parts)
    renders = {}
    for name, k in STILLS.items():
        p = pose_B(k)
        px, info = fr.render(p)
        renders[name] = (px, info)
        print(name, "a1 %.1f b %.1f turn %.1f wrist bend %+.1f flipped %d tip %s" % (
            p["a1"], p["b"], info["turn"], info["wrist_bend"], info["flipped"], np.round(info["tip"], 0)))
    # ---- hand at 4x of the approved sheet's zoom (its hero ~319 px tall; ours 1028)
    k = 319 / 1028
    tiles = []
    for name, (px, info) in renders.items():
        im = rig.to_image(px, BG)
        im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        c = 62
        g = (info["grip"] * 0.6 + info["W"] * 0.4) * k
        box = (int(g[0] - c), int(g[1] - c), int(g[0] + c), int(g[1] + c))
        tiles.append(label(im.crop(box).resize((8 * c // 2 * 2, 8 * c // 2 * 2), Image.NEAREST), name))
    ref = Image.open(ROOT / "art/tests/sheets/layered_hero_hands.webp").convert("RGB")
    t = tiles[0].width
    r1 = label(ref.crop((303, 718, 303 + t // 4, 718 + t // 4)).resize((t, t), Image.NEAREST), "approved: ready")
    r2 = label(ref.crop((403, 1166, 403 + t // 4, 1166 + t // 4)).resize((t, t), Image.NEAREST), "approved: strike")
    allt = tiles + [r1, r2]
    cols = 4
    rows = (len(allt) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (t + 6), rows * (t + 6)), (255, 255, 255))
    for i, tl in enumerate(allt):
        sheet.paste(tl, ((i % cols) * (t + 6), (i // cols) * (t + 6)))
    fit(sheet).save(OUT / f"fist_4x{TAG}.png")
    # ---- cuff and elbow at full drawing size x2 (sleeve from the elbow to the fist)
    tiles = []
    for name, (px, info) in renders.items():
        im = rig.to_image(px, BG)
        c = 95
        g = info["W"] * 0.7 + info["elbow"] * 0.3
        box = (int(g[0] - c), int(g[1] - c), int(g[0] + c), int(g[1] + c))
        tiles.append(label(im.crop(box).resize((4 * c, 4 * c), Image.NEAREST), name))
    t = tiles[0].width
    sheet = Image.new("RGB", (3 * (t + 6), 2 * (t + 6)), (255, 255, 255))
    for i, tl in enumerate(tiles):
        sheet.paste(tl, ((i % 3) * (t + 6), (i // 3) * (t + 6)))
    fit(sheet).save(OUT / f"cuff_4x{TAG}.png")
    # ---- look: game size (hero 480 px tall as on a 1440p screen) beside char_hero_bare at the same height
    import swing_r2 as sw
    bg = sw.background(fr)
    kk = 480 / 1028
    tiles = []
    old = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
    ob = old.getchannel("A").point(lambda v: 255 if v >= 10 else 0).getbbox()
    old = old.crop(ob)
    ko = 480 / old.height
    old = old.resize((round(old.width * ko), round(old.height * ko)), Image.LANCZOS)
    # the same strip of the figure as the frames show (top of the hair to just below the belt)
    o_bg = Image.fromarray((bg * 255).astype(np.uint8)).resize((round(fr.w * kk), round(fr.h * kk)), Image.LANCZOS)
    for name in ("ready", "wind-up", "strike"):
        px, info = renders[name]
        out = px[..., :3] + bg * (1 - px[..., 3:4])
        im = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))
        im = im.resize((round(im.width * kk), round(im.height * kk)), Image.LANCZOS)
        cx = (760 - fr.ox) * kk
        tiles.append(label(im.crop((int(cx - 260), 0, int(cx + 200), im.height)), name))
    oc = o_bg.crop((0, 0, 330, o_bg.height)).convert("RGBA")
    top = (290 - fr.oy) * kk  # the hair top of the new drawing (canvas y ~290) in the tile
    oc.alpha_composite(old, (int(165 - old.width / 2), int(top)))
    tiles.insert(0, label(oc.convert("RGB"), "in the game now"))
    W = sum(tl.width for tl in tiles) + 6 * len(tiles)
    sheet = Image.new("RGB", (W, tiles[0].height), (255, 255, 255))
    x = 0
    for tl in tiles:
        sheet.paste(tl, (x, 0))
        x += tl.width + 6
    fit(sheet).save(OUT / f"look{TAG}.png")
    print("saved", OUT)


if __name__ == "__main__":
    main()
