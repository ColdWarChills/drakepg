"""Round 2: the vest tries at game size (hero 480 px tall, as on a 1440p screen) beside char_hero_bare and the
picked drawing without a vest, on the Fen Road colour; prints how far each edit moved the figure (phase correlation).
-> art/tests/hero_joints/r2/outfit_pick.png"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import cv2
import numpy as np
from PIL import Image, ImageDraw
from jcommon import RAW, REDO, TESTS, PURPLE, fit

names = ["cutj3_s123", "r2_vest_teal_s201", "r2_vest_teal_s202", "r2_vest_brown_s211", "r2_vest_brown_s212"]
base = np.asarray(Image.open(REDO / "cutj3_s123.png").convert("RGB"))
BG = (196, 190, 168)


def cut(name):
    rgb = np.asarray(Image.open(REDO / f"{name}.png").convert("RGB")).astype(np.float32)
    dpu = np.abs(rgb - np.array(PURPLE)).max(-1)
    a = np.clip((dpu - 40) / 50, 0, 1)
    im = np.concatenate([rgb, a[..., None] * 255], -1).astype(np.uint8)
    return Image.fromarray(im, "RGBA")


def shift(a, b, box):
    x0, y0, x1, y1 = box
    ga = cv2.cvtColor(a[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY).astype(np.float32)
    gb = cv2.cvtColor(b[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY).astype(np.float32)
    (dx, dy), r = cv2.phaseCorrelate(ga, gb)
    return round(dx, 1), round(dy, 1)


tiles = []
old = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
old = old.crop(old.getchannel("A").point(lambda v: 255 if v >= 10 else 0).getbbox())
k = 480 / old.height
old = old.resize((round(old.width * k), 480), Image.LANCZOS)
t = Image.new("RGBA", (300, 520), BG + (255,))
t.alpha_composite(old, ((300 - old.width) // 2, 30))
ImageDraw.Draw(t).text((5, 5), "in the game now", fill=(0, 0, 0))
tiles.append(t)
for n in names:
    im = cut(n)
    other = np.asarray(Image.open(REDO / f"{n}.png").convert("RGB"))
    print(n, "head", shift(base, other, (650, 280, 900, 470)), "belt", shift(base, other, (660, 690, 880, 760)),
          "near sleeve", shift(base, other, (540, 560, 700, 800)))
    bb = (380, 270, 1180, 1340)   # the same box for all (the hero ~1028 tall in it)
    c = im.crop(bb)
    k = 480 / 1028
    c = c.resize((round(c.width * k), round(c.height * k)), Image.LANCZOS)
    t = Image.new("RGBA", (300, 520), BG + (255,))
    t.alpha_composite(c.crop((40, 0, 340, c.height)), (0, 30))
    ImageDraw.Draw(t).text((5, 5), n.replace("r2_", ""), fill=(0, 0, 0))
    tiles.append(t)
sheet = Image.new("RGB", (len(tiles) * 304, 520), (255, 255, 255))
for i, t in enumerate(tiles):
    sheet.paste(t.convert("RGB"), (i * 304, 0))
fit(sheet).save(TESTS / "r2" / "outfit_pick.png")
print(sheet.size)
