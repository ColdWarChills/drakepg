"""Sword-hand close-ups at four moments of the blow (wind-up, hit, after the hit, ready), one row per version: today's
drawn blow, the Nov 18 bones cut, way C's first try and way C with the grip fixed. Every hand is cropped at the same
size relative to its hero. python scripts/hero3d/fist_sheet.py <out.png> [--root art/redo/animway]
Root layout (local data, not in git): frames/old, hybrid/frames_s1, hybrid/frames_s4, tracks/track_slash2.json,
tracks/track_clip4.json (grip pixels per frame from rig_hero.py --track); the bones cut is read from
art/redo/bones_frames/s1b/pick_x1/main (frames + its own track.json)."""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

a = sys.argv[1:]
REPO = Path(__file__).resolve().parents[2]
ROOT = Path(a[a.index("--root") + 1]) if "--root" in a else REPO / "art/redo/animway"
BONES = REPO / "art/redo/bones_frames/s1b/pick_x1/main"
bt = json.load(open(BONES / "track.json"))["frames"]
bfl = sorted(BONES.glob("f*.jpg"))
t_new = json.load(open(ROOT / "tracks/track_clip4.json"))
t_old = json.load(open(ROOT / "tracks/track_slash2.json"))


def game(f, xy):                 # today's blow from a real fight, 60 fps; the fist found by eye
    return ROOT / f"frames/old/{f:03d}.png", xy, 406


def bones(f):
    return bfl[f], bt[f]["points"]["grip"], 250


def way_c(folder, trk, n):       # Wan file n+1 shows 3D frame n
    return ROOT / f"hybrid/{folder}/f{n + 1:03d}.png", trk[str(n)]["grip"], 377


ROWS = [
    ("In the game today", [game(15, (300, 254)), game(29, (420, 527)), game(37, (410, 523)), game(1, (370, 393))]),
    ("Bones cut (Nov 18)", [bones(10), bones(22), bones(28), bones(0)]),
    ("New way, first try", [way_c("frames_s1", t_old, n) for n in (8, 15, 20, 35)]),
    ("New way, fixed", [way_c("frames_s4", t_new, n) for n in (8, 15, 20, 35)]),
]
COLS = ["wind-up", "hit", "after the hit", "ready"]
S, LW, TH = 300, 230, 34
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 22)
W = Image.new("RGB", (LW + len(COLS) * (S + 6), TH + len(ROWS) * (S + 6)), (24, 22, 20))
d = ImageDraw.Draw(W)
for c, lab in enumerate(COLS):
    d.text((LW + c * (S + 6) + 10, 4), lab, font=font, fill=(240, 232, 214))
for r, (lab, cells) in enumerate(ROWS):
    y = TH + r * (S + 6)
    d.text((10, y + S // 2 - 14), lab, font=font, fill=(240, 232, 214) if r < len(ROWS) - 1 else (150, 230, 140))
    for c, (p, (gx, gy), h) in enumerate(cells):
        B = 0.20 * h
        im = Image.open(p).convert("RGB")
        W.paste(im.crop((int(gx - B), int(gy - B), int(gx + B), int(gy + B))).resize((S, S), Image.LANCZOS),
                (LW + c * (S + 6), y))
W.save(a[0])
print(W.size)
