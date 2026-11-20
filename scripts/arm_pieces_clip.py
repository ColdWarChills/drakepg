#!/usr/bin/env python3
"""The arm-pieces clip (the designer, 2026-11-20): the near arm bending and the sword turning in the fist, today's pieces
beside the new ones (scripts/arm_pieces.py -> unity/Bones/hero/bare_v3), drawn by the Python bones math (bones_parts.Puppet,
the same math as the bones code; not Unity). The sword turn follows the game's rule (BoneHero.GripHand): the fist drawn
for this blade-to-forearm angle (swaps.json zones) is pinned to the sword by its grip and the arm reaches its wrist.

  python scripts/arm_pieces_clip.py            -> art/tests/sheets/armway/arm_bend_clip.mp4 + arm_bend_stills.webp
  python scripts/arm_pieces_clip.py --blind    also an unlabelled, shuffled pairs sheet (+ key) in the temp folder
  (--force to replace the clip and stills)
"""
import os, sys, math, json, tempfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bones_parts as bp
import bones_sheets as bs

SW = json.loads((bp.BONES / "swaps.json").read_text())["swaps"]
HANDS = ("n_hand_grip2", "n_hand_hammer")
REST = math.degrees(math.atan2(733.4 - 600, 456.4 - 573.4))   # elbow -> wrist in the rig canvas, screen degrees
_cache = {}


def puppet(kind, hand):
    k = (kind, hand)
    if k not in _cache:
        parts = {}
        if kind == "new":
            parts = {a: tuple(b) for a, b in json.loads((bp.BONES / "bare_v3" / "parts.json").read_text()).items()}
        _cache[k] = bp.Puppet(sets=("bare_v2",), swaps={"n_hand": hand}, parts=parts)
    return _cache[k]

def norm(a):
    return (a + 180) % 360 - 180

def zone_pick(rel, keep):
    for h in HANDS:
        lo, hi = SW[h]["zone"]
        if lo <= rel <= hi or lo <= rel + 360 <= hi or lo <= rel - 360 <= hi:
            return h
    return keep or HANDS[0]

def frame(kind, sh, el, rel, keep=None, sign=1):
    fore = REST + sh + el                     # the forearm's screen direction
    blade = fore + sign * rel                 # the blade's screen direction
    hand = zone_pick(norm(rel), keep)
    p = puppet(kind, hand)
    g = SW[hand]["grip"]["angle"]
    local = {"n_upper": sh, "n_fore": el, "n_hand": blade - g - (sh + el)}
    im, turn, at = p.draw(local, weapons={"n": "iron_sword"}, ground=bp.GROUND)
    return im, hand, at

# --- the game's rule for the sword turning in the hand (BoneHero.GripHand): the sword stays where it is, the hand
# drawn for this blade-to-forearm angle is pinned to it by its grip, and the arm reaches its wrist (two-bone IK).
SH, EL, WR = (688, 495), (573.4, 600), (456.4, 733.4)
L1, L2 = math.dist(SH, EL), math.dist(EL, WR)
REST_UP = math.degrees(math.atan2(EL[1] - SH[1], EL[0] - SH[0]))

def wrist_of(hand, G, blade):
    sw = SW[hand]
    th = blade - sw["grip"]["angle"]                 # the hand's world turn for this blade direction
    off = bp.rot(bp.sub(tuple(sw["grip"]["at"]), tuple(sw["joints"]["n_wrist"])), th)
    return bp.sub(G, off), th

def ik(shoulder, W):
    dx, dy = W[0] - shoulder[0], W[1] - shoulder[1]
    d = min(max(math.hypot(dx, dy), abs(L1 - L2) + 1), L1 + L2 - 1)
    base = math.degrees(math.atan2(dy, dx))
    a = math.degrees(math.acos((L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)))
    u = base + a                                    # the elbow behind (the same side as the bend in the moves)
    E = (shoulder[0] + L1 * math.cos(math.radians(u)), shoulder[1] + L1 * math.sin(math.radians(u)))
    f = math.degrees(math.atan2(W[1] - E[1], W[0] - E[0]))
    return u, f

def hold_frame(kind, shoulder, G, blade, keep):
    hand = keep
    for _ in range(2):
        W, th = wrist_of(hand, G, blade)
        u, f = ik(shoulder, W)
        rel = norm(blade - f)
        pick = zone_pick(rel, hand)
        if pick == hand:
            break
        hand = pick
    up = u - REST_UP
    fo = f - REST - up
    local = {"n_upper": up, "n_fore": fo, "n_hand": th - up - fo}
    im, turn, at = puppet(kind, hand).draw(local, weapons={"n": "iron_sword"}, ground=bp.GROUND)
    return im, hand, at, rel, fo


def main():
    OUTDIR = str(Path(__file__).resolve().parent.parent / "art" / "tests" / "sheets" / "armway")
    FPS = 24
    def ease(t): return 0.5 - 0.5 * math.cos(math.pi * t)
    def seg(n, a, b): return [a + (b - a) * ease(i / (n - 1)) for i in range(n)]

    # (shoulder, elbow, relation) per frame
    plan = [(0, 0, 121.7)] * 8
    plan += [(0, e, 121.7) for e in seg(32, 0, -120)] + [(0, -120, 121.7)] * 8
    plan += [(0, e, 121.7) for e in seg(32, -120, 0)] + [(0, 0, 121.7)] * 6
    plan += [(0, e, 121.7) for e in seg(12, 0, -45)]
    # then the sword turns in the hand at elbow 45 the game's way: the sword stays, the arm follows (arm_clip.hold_frame)
    turn = seg(24, 0, 100) + [100] * 4 + seg(40, 100, -80) + [-80] * 4 + seg(20, -80, 0) + [0] * 8
    plan += [("hold", 0, -45, d) for d in turn]

    def render(kind):
        out, keep, G, track = [], None, None, []
        for step in plan:
            if step[0] == "hold":
                if G is None:
                    im0, keep, at0 = frame(kind, 0, -45, 121.7)
                    sw = SW[keep]; blade0 = REST - 45 + 121.7
                    G = bp.add(at0["n_hand"], bp.rot(bp.sub(tuple(sw["grip"]["at"]), tuple(sw["joints"]["n_wrist"])), blade0 - sw["grip"]["angle"]))
                    shoulder = at0["n_upper"]
                im, keep, at, rel, fo = hold_frame(kind, shoulder, G, blade0 + step[3], keep)
            else:
                sh, el, rel = step
                r = (rel + 180) % 360 - 180
                im, keep, at = frame(kind, sh, el, r, keep)
            out.append(im.convert("RGB"))
            track.append((at["n_fore"], at["n_hand"], keep))
        steps = [max(math.dist(a[0], b[0]), math.dist(a[1], b[1])) for a, b in zip(track, track[1:])]
        k = int(np.argmax(steps))
        print(kind, "largest move of elbow or wrist between frames:", round(steps[k], 1), "px at frame", k + 1,
              "(median", round(float(np.median(steps)), 1), "px); fist pictures used:", sorted({t[2] for t in track}))
        return out

    old, new = render("old"), render("new")
    # crop: where anything moves, plus a margin
    ref = np.asarray(new[0]).astype(int)
    moving = np.zeros(ref.shape[:2], bool)
    for im in new[::3] + old[::3]:
        moving |= np.abs(np.asarray(im).astype(int) - ref).sum(2) > 30
    ys, xs = np.nonzero(moving)
    box = [270, 320, 890, 940]   # the arm, close: the sword may leave the frame
    w, h = box[2] - box[0], box[3] - box[1]
    print("box", box)
    S = 460 / max(w, h)
    cw, ch = int(w * S) // 2 * 2, int(h * S) // 2 * 2
    try:
        F = ImageFont.truetype("arial.ttf", 18); f2 = ImageFont.truetype("arial.ttf", 14)
    except OSError:
        F = f2 = ImageFont.load_default()

    def pair(a, b, cap=True):
        g = Image.new("RGB", (cw * 2 + 8, ch + (24 if cap else 0)), (24, 24, 28))
        for i, (im, name) in enumerate(((a, "today"), (b, "new"))):
            c = im.crop(box).resize((cw, ch), Image.LANCZOS)
            g.paste(c, (i * (cw + 8), 0))
            ImageDraw.Draw(g).text((i * (cw + 8) + 10, 8), name, fill=(255, 255, 255), font=F, stroke_width=2, stroke_fill=(0, 0, 0))
        if cap:
            ImageDraw.Draw(g).text((10, ch + 4), "same arm, same moves; drawn by the bones math outside the game", fill=(200, 196, 186), font=f2)
        return g

    tmp = tempfile.mkdtemp(prefix="armclip_")
    for i, (a, b) in enumerate(zip(old, new)):
        pair(a, b).save(os.path.join(tmp, f"c{i:04d}.png"))
    os.makedirs(OUTDIR, exist_ok=True)
    clip = os.path.join(OUTDIR, "arm_bend_clip.mp4")
    if os.path.exists(clip) and "--force" not in sys.argv: raise SystemExit("exists: " + clip)
    bs.run_ffmpeg(["-framerate", str(FPS), "-i", os.path.join(tmp, "c%04d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", clip])
    print(clip, len(plan), "frames", round(len(plan) / FPS, 1), "s")

    # stills: elbow 0 / 45 / 90 / 135 / 150, then the sword at six angles
    picks = [0, 8 + 8, 8 + 15, 8 + 22, 8 + 33]
    idx = [i for i, p in enumerate(plan)]
    def nearest(pred):
        return min(range(len(plan)), key=pred)
    cols = [nearest(lambda i, e=e: (abs(plan[i][1] - e) if i < 50 else 9999)) for e in (0, -30, -60, -90, -120)]
    first = next(i for i, p in enumerate(plan) if p[0] == "hold")
    cols += [nearest(lambda i, d=d: (abs(plan[i][3] - d) if first <= i < first + 70 else 9999)) for d in (100, 60, 20, -40, -80)]
    tiles = [pair(old[i], new[i], cap=False) for i in cols]
    tw, th = tiles[0].size
    sheet = Image.new("RGB", (tw * 2 + 10, (th + 6) * 5 + 30), (24, 24, 28))
    for k, t in enumerate(tiles):
        sheet.paste(t, ((k // 5) * (tw + 10), (k % 5) * (th + 6)))
    ImageDraw.Draw(sheet).text((10, sheet.height - 24), "left pair of columns: the elbow bending (straight to 120 degrees); right: the sword turning in the fist (one fist picture)", fill=(200, 196, 186), font=f2)
    sheet = sheet.resize((sheet.width // 2 * 2 * 3 // 4, sheet.height * 3 // 4), Image.LANCZOS)
    sheet.save(os.path.join(OUTDIR, "arm_bend_stills.webp")); print("stills", sheet.size)

    if "--blind" in sys.argv:
        import random, json as _j
        rnd = random.Random(7)
        key, rows = {}, []
        for r, i in enumerate(cols):
            a, b = old[i].crop(box).resize((300, 300), Image.LANCZOS), new[i].crop(box).resize((300, 300), Image.LANCZOS)
            flip = rnd.random() < 0.5
            left, right = (b, a) if flip else (a, b)
            key[r + 1] = {"A": "new" if flip else "today", "B": "today" if flip else "new"}
            g = Image.new("RGB", (612, 300), (24, 24, 28)); g.paste(left, (0, 0)); g.paste(right, (312, 0))
            d = ImageDraw.Draw(g)
            for x, n in ((6, f"{r+1}A"), (318, f"{r+1}B")):
                d.text((x, 6), n, fill=(255, 255, 255), font=F, stroke_width=2, stroke_fill=(0, 0, 0))
            rows.append(g)
        sheet = Image.new("RGB", (612 * 2 + 12, 306 * 5), (24, 24, 28))
        for k, g in enumerate(rows):
            sheet.paste(g, ((k // 5) * 624, (k % 5) * 306))
        tmpd = tempfile.gettempdir(); sheet.save(os.path.join(tmpd, "blind_arm.png")); _j.dump(key, open(os.path.join(tmpd, "blind_key.json"), "w")); print("blind", sheet.size)


if __name__ == "__main__":
    main()
