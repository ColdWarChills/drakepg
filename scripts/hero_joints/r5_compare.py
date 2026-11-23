"""Attempt 2 (round 5): today's hero and hero 4 (Unity 2D Animation, strike drawings) doing the sword attack in battle.
python -I -B r5_compare.py <old take dir> <new take dir>
Both take dirs hold the lossless game frames p004..p024.png (2560x1440) and track.json from BonesCapture (press on take
frame 6 in both; hero 4's clip frame = take frame - 5).

-> art/tests/hero_joints/r5/attack_old_new.mp4  old | new at ONE fixed framing and scale, game speed looped twice, then
                                                once at quarter speed
   frames_old.png / frames_new.png             every frame from the first lift (take 6) to the end of the recover
                                                (take 19), game size, a window that follows the feet, in rows
   swaps.png                                   hero 4's three swaps (into the wind-up, into the strike, out of the
                                                strike): the frame before, the swap and the frame after at 2x, in a
                                                window fixed to the feet point, with the ground line, the feet point and
                                                a guide at the standing back heel
   joints_3x.png                               shoulder, elbow and fist at 3x on the ready, the wind-up hold and the
                                                strike hold; belt/waist and neck at 3x on the strike hold"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

from PIL import Image, ImageDraw, ImageFont

import r4_compare as c4
from jcommon import TESTS

OUT = TESTS / "r5"
OUT.mkdir(parents=True, exist_ok=True)
OLD, NEW = c4.OLD, c4.NEW
FRAMES = c4.FRAMES
FRAMING = (250, 150, 2150, 1250)
STRIP = range(6, 20)
CLIP = {i: i - 5 for i in range(5, 25)}
# wind-up fix round: the lift is dropped, the coiled wind-up is clip frames 1-4 (takes 6-9, as today's wind pose)
NAMES = {6: "wind-up swap", 7: "wind-up hold", 8: "wind-up hold", 9: "wind-up hold", 10: "cut swap", 11: "strike (contact)",
         12: "held", 13: "held", 14: "held", 15: "held", 16: "recover swap", 17: "recover", 18: "recover", 19: "ready"}
# screen px per drawing px, measured on the take (waist 262.2 and neck 376.2 screen px above the feet point for 561 and
# 805 canvas px); the standing back boot's rear edge is at canvas x 484 (studio, every frame), the feet point at 772
PX_PER_CANVAS = 0.4674
BACK_HEEL = (484 - 772) * PX_PER_CANVAS
GROUND_Y = 1000.0
FONT = ImageFont.truetype("arial.ttf", 30)
FONT_VIDEO = ImageFont.truetype("arial.ttf", 40)


def label(im, text, font=FONT):
    d = ImageDraw.Draw(im)
    w = d.textlength(text, font=font)
    d.rectangle([0, 0, w + 16, font.size + 14], fill=(255, 255, 255))
    d.text((8, 6), text, font=font, fill=(0, 0, 0))
    return im


def video():
    c4.OUT, c4.FRAMING = OUT, FRAMING
    c4.label = lambda im, text, size=1: label(im, text, FONT_VIDEO)
    return c4.video()


WIDE = (0, 150, 2150, 1250)           # hero 4 stands 267 drawing px further back: his wind-up blade reaches screen x ~40


def video_wide():
    """As video(), both heroes at one wider framing that keeps the wind-up blades: attack_old_new_wide.mp4."""
    c4.OUT, c4.FRAMING = OUT, WIDE
    c4.label = lambda im, text, size=1: label(im, text, FONT_VIDEO)
    out, n = c4.video()
    wide = OUT / "attack_old_new_wide.mp4"
    out.replace(wide)
    return wide, n


OLD_NAMES = {i: "wind pose" for i in range(6, 10)}
OLD_NAMES.update({10: "step", 11: "strike (contact)", **{i: "strike pose" for i in range(12, 16)}, 16: "rest",
                  17: "back home", 18: "back home", 19: "back home"})


def strip(d, name, who):
    tr = c4.track(d)
    tiles = []
    for i in STRIP:
        fx = tr[i]["points"]["feet"][0]
        x0 = int(round(fx - 470))
        t = c4.frame(d, i).crop((x0, 150, x0 + 1000, 1150))
        tiles.append(label(t, f"take f{i}" + (f" (clip f{CLIP[i]}) {NAMES[i]}" if who == "new" else f" {OLD_NAMES[i]}")))
    out = OUT / f"frames_{name}.png"
    c4.grid(tiles, 7).save(out)
    return out


def swaps():
    tr = c4.track(NEW)
    groups = [("into the wind-up", 6), ("into the strike (cut)", 10), ("out of the strike", 16)]
    rows = []
    for title, s in groups:
        tiles = []
        for i in (s - 1, s, s + 1):
            fx, fy = tr[i]["points"]["feet"]
            x0, y0 = int(round(fx - 420)), 330
            t = c4.frame(NEW, i).crop((x0, y0, x0 + 820, y0 + 760))
            t = t.resize((t.width * 2, t.height * 2), Image.LANCZOS)
            d = ImageDraw.Draw(t)
            gy = (GROUND_Y - y0) * 2
            d.line([(0, gy), (t.width, gy)], fill=(255, 230, 0), width=2)
            fxl = (fx - x0) * 2
            d.line([(fxl, gy - 40), (fxl, gy + 40)], fill=(255, 0, 255), width=3)
            hx = (fx + BACK_HEEL - x0) * 2
            for yy in range(int(gy - 160), int(gy + 20), 16):
                d.line([(hx, yy), (hx, yy + 8)], fill=(0, 200, 255), width=2)
            tag = "before" if i < s else "SWAP" if i == s else "after"
            label(t, f"{title}: take f{i} (clip f{CLIP[i]}) {tag}")
            dd = ImageDraw.Draw(t)
            dd.rectangle([0, 50, t.width, 96], fill=(255, 255, 255))
            dd.text((8, 56), f"feet point {fx:.0f},{fy:.0f}  yellow ground 1000, magenta feet point, cyan standing back heel",
                    font=ImageFont.truetype("arial.ttf", 26), fill=(0, 0, 0))
            tiles.append(t)
        rows.append(tiles)
    out = OUT / "swaps.png"
    c4.grid([t for r in rows for t in r], 3).save(out)
    return out


def joints():
    tr = c4.track(NEW)
    tiles = []
    for i, name in [(5, "ready"), (6, "wind-up swap"), (8, "wind-up hold"), (9, "wind-up hold"), (13, "strike hold")]:
        p = tr[i]["points"]
        xs = [p[k][0] for k in ("S", "elbow", "wrist", "grip")]
        ys = [p[k][1] for k in ("S", "elbow", "wrist", "grip")]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        half = max(max(xs) - min(xs), max(ys) - min(ys)) / 2 + 55
        t = c4.frame(NEW, i).crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))
        tiles.append(label(t.resize((t.width * 3, t.height * 3), Image.NEAREST), f"f{i} {name}: arm 3x"))
    p = tr[13]["points"]
    for key, name, half in (("waist", "belt", 70), ("neck", "neck", 60), ("knot", "tails/far arm", 70)):
        cx, cy = p[key]
        if key == "knot":     # the knot and the far shoulder together
            cx, cy = (p["knot"][0] + p["far"][0]) / 2, (p["knot"][1] + p["far"][1]) / 2
        t = c4.frame(NEW, 13).crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))
        tiles.append(label(t.resize((t.width * 3, t.height * 3), Image.NEAREST), f"f13 {name} 3x"))
    out = OUT / "joints_3x.png"
    c4.grid(tiles, 4).save(out)
    return out


WIND_HOLD = 8                         # a take frame inside both wind-up holds (today's wind pose 6-9, hero 4's 6-9)
WIND_WINDOW = (40, 230, 1090, 1060)   # one fixed screen window for both heroes' wind-up holds (blade tips included)


def wind():
    """The wind-up hold at 3x (sword hand + pommel, elbow, shoulder/armhole, belt, both boots) and both heroes' wind-up
    holds at game size in one fixed screen window."""
    tr = c4.track(NEW)
    p = tr[WIND_HOLD]["points"]
    fx, fy = p["feet"]
    k = PX_PER_CANVAS
    crops = [
        ("sword hand + pommel", p["grip"], 100),
        ("elbow", p["elbow"], 95),
        ("shoulder + armhole", p["S"], 95),
        ("belt", (p["waist"][0] - 5, p["waist"][1] - 25), 95),
        ("back boot", (fx + (520 - 772) * k, GROUND_Y - 40), 90),
        ("front boot", (fx + (1060 - 772) * k, GROUND_Y - 75), 95),
    ]
    im = c4.frame(NEW, WIND_HOLD)
    tiles = []
    for name, (cx, cy), half in crops:
        t = im.crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half)))
        tiles.append(label(t.resize((t.width * 3, t.height * 3), Image.NEAREST), f"take f{WIND_HOLD} wind-up hold: {name} 3x"))
    row = c4.grid(tiles, 3)
    pair = [label(c4.frame(d, WIND_HOLD).crop(WIND_WINDOW), f"{who} wind-up hold, take f{WIND_HOLD}, game size")
            for d, who in ((OLD, "today's hero"), (NEW, "hero 4"))]
    out = OUT / "wind_3x.png"
    s = c4.grid([c4.grid(pair, 2), row], 1)
    s.save(out)
    return out


if __name__ == "__main__":
    print("saved", *video_wide(), "frames (wide)")
    v, n = video()
    print("saved", v, n, "frames")
    print("saved", strip(OLD, "old", "old"), strip(NEW, "new", "new"), swaps(), joints(), wind())
