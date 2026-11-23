"""Two last checks.
swaps_4x.png: the fist swaps that happen in slow frames (raise s15->16, recovery s85->86), kind B, at 4x, frame before
              and after side by side, window centred on the wrist (the raise swap is above the clip's top edge).
look_kinds.png: A, B, C at ready and at strike beside char_hero_bare, at game size (480 px tall) and at 1000 px."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

import numpy as np
from PIL import Image, ImageDraw

import keys
import rig
import swing
from jcommon import RAW, TESTS, fit

BG = np.array([0.85, 0.83, 0.80], np.float32)


def pose_for_s(kind, chs, s):
    pose, st = swing.pose_at(chs, s, 0)
    if kind != "A":
        pose["stretch"] = st
    sign = np.sign(pose["b"]) if abs(pose["b"]) > 1e-6 else 1.0
    return keys.pose_for(kind, pose, sign)


def swaps():
    parts = rig.Parts()
    chs = swing.channels()
    big = rig.Frame(parts, window=(300, -200, 1400, 1300))
    rows = []
    for a, b in ((15, 16), (85, 86)):
        pa = pose_for_s("B", chs, a)
        _, ia = big.render("B", pa)
        cx, cy = ia["W"][0] + big.ox, ia["W"][1] + big.oy   # W is window-local
        win = (int(cx - 90), int(cy - 90), 180, 180)
        fr = rig.Frame(parts, window=win)
        tiles = []
        for s in (a, b):
            p = pose_for_s("B", chs, s)
            px, info = fr.render("B", p)
            im = rig.to_image(px, BG).resize((720, 720), Image.NEAREST)
            ImageDraw.Draw(im).text((6, 6), f"swing frame {s}: fist '{info['fist']}', turn {info['turn']:.1f}",
                                    fill=(0, 0, 0))
            tiles.append(im)
        rows.append(tiles)
    sheet = Image.new("RGB", (2 * 730, 2 * 730), (255, 255, 255))
    for r, tiles in enumerate(rows):
        for c, t in enumerate(tiles):
            sheet.paste(t, (c * 730, r * 730))
    fit(sheet).save(TESTS / "swaps_4x.png")
    print(TESTS / "swaps_4x.png")


def look_kinds():
    parts = rig.Parts()
    chs = swing.channels()
    fr = rig.Frame(parts, window=(300, 220, 800, 900))
    H = 1000
    k = H / 1028
    old = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
    old = old.crop(Image.fromarray(((np.asarray(old)[..., 3] > 40) * 255).astype(np.uint8)).getbbox())
    ob = Image.new("RGBA", old.size, tuple(int(v * 255) for v in BG) + (255,))
    ob.alpha_composite(old)
    old = ob.convert("RGB")
    old = old.resize((round(old.width * H / 1033), H), Image.LANCZOS)
    tiles = [("in the game now", old)]
    for s, name in ((0, "ready"), (61, "strike")):
        for kind in "ABC":
            px, _ = fr.render(kind, pose_for_s(kind, chs, s))
            im = rig.to_image(px, BG)
            tiles.append((f"{rig.KINDS[kind]}, {name}", im.resize((round(im.width * k), round(im.height * k)),
                                                                   Image.LANCZOS)))
    g = 480 / H
    small = [(n, im.resize((round(im.width * g), round(im.height * g)), Image.LANCZOS)) for n, im in tiles]
    W = sum(t.width for _, t in small) + 10 * len(small)
    sheet = Image.new("RGB", (W, 520), (255, 255, 255))
    d = ImageDraw.Draw(sheet)
    x = 0
    for n, im in small:
        sheet.paste(im, (x, 30))
        d.text((x + 3, 8), n, fill=(0, 0, 0))
        x += im.width + 10
    sheet.save(TESTS / "look_kinds.png")
    print(TESTS / "look_kinds.png", sheet.size)


if __name__ == "__main__":
    swaps()
    look_kinds()
