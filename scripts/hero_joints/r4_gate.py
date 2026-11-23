"""Round 4 gate on each Qwen try, before any cutting: did the edit keep the hero (head size, belt) and the hips/legs?
<venv python> -I -B r4_gate.py <key> [<key> ...]   (keys like r4_windup_s401)
Prints per try: head scale and shift against r2_vest_teal_s202 (template match over scales), the belt's x range,
top and width, the legs' shift once the belts are aligned and how much of the legs' outline differs (IoU).
Writes art/tests/hero_joints/r4/gate.json and seeds.png (each try beside s202, belt lines and the head box marked)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

from jcommon import REDO, TESTS, fit

OUT = TESTS / "r4"
OUT.mkdir(parents=True, exist_ok=True)
REF = "r2_vest_teal_s202"
HEAD_BOX = (650, 250, 900, 480)   # s202's head (hair top to chin, headband tails left out mostly)
PURPLE_LO = 40
# the ratings written on seeds.png (by eye at game size and from the numbers this script prints)
RATINGS = {
    "r2_vest_teal_s202": "the source (ready drawing)",
    "r4_windup_s401": "2/5: arm up, legs kept, but the chest does not move (no turn, no lean)",
    "r4_windup_s402": "4/5 PICK: shoulder raised ~29 px, chest opened (+16% wide), far arm forward; legs redrawn (torso only is used)",
    "r4_windup_s403": "3/5: shoulder raised ~16 px, chest +9%, little lean; legs moved 8 px",
}


def load(key):
    return np.asarray(Image.open(REDO / f"{key}.png").convert("RGB"))


def fig_mask(rgb):
    s = rgb.astype(np.float32)
    bg = np.median(np.concatenate([s[:40, :40].reshape(-1, 3), s[-40:, -40:].reshape(-1, 3)]), 0)
    m = (np.abs(s - bg).max(-1) > PURPLE_LO).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    return lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))


def belt(rgb, fig):
    R, G, B = [rgb[..., i].astype(np.int32) for i in range(3)]
    H, W = fig.shape
    yy, xx = np.mgrid[0:H, 0:W]
    m = fig & (R < 115) & (G < 80) & (B < 60) & (R - B > 15) & (yy > 660) & (yy < 790) & (xx > 600) & (xx < 900)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((3, 61), np.uint8)) > 0   # across the buckle
    n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    if n < 2:
        return None
    b = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    ys, xs = np.nonzero(b)
    tops = [ys[xs == x].min() for x in range(xs.min(), xs.max() + 1) if (xs == x).any()]
    return dict(x0=int(xs.min()), x1=int(xs.max()), w=int(xs.max() - xs.min()), top=float(np.median(tops)),
                bottom=float(np.median([ys[xs == x].max() for x in range(xs.min(), xs.max() + 1) if (xs == x).any()])))


def head_match(ref, im):
    x0, y0, x1, y1 = HEAD_BOX
    tpl = cv2.cvtColor(ref[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY)
    g = cv2.cvtColor(im, cv2.COLOR_RGB2GRAY)
    best = (-1, 1.0, (0, 0))
    for k in np.arange(0.90, 1.101, 0.005):
        t = cv2.resize(tpl, None, fx=k, fy=k, interpolation=cv2.INTER_AREA)
        r = cv2.matchTemplate(g, t, cv2.TM_CCOEFF_NORMED)
        _, mx, _, loc = cv2.minMaxLoc(r)
        if mx > best[0]:
            best = (float(mx), float(k), loc)
    score, k, (lx, ly) = best
    return dict(scale=round(k, 3), score=round(score, 3), dx=int(lx - x0), dy=int(ly - y0))


def main(keys):
    ref = load(REF)
    rfig = fig_mask(ref)
    rb = belt(ref, rfig)
    res = {REF: dict(belt=rb)}
    tiles = []
    for key in [REF] + keys:
        im = load(key)
        fig = fig_mask(im)
        b = belt(im, fig)
        r = dict(belt=b)
        if key != REF:
            r["head"] = head_match(ref, im)
            dx = round((b["x0"] + b["x1"]) / 2 - (rb["x0"] + rb["x1"]) / 2)
            dy = round(b["top"] - rb["top"])
            r["belt_shift"] = (dx, dy)
            r["belt_w_ratio"] = round(b["w"] / rb["w"], 3)
            # legs: below the belt, after moving the try so its belt sits on s202's
            M = np.float32([[1, 0, -dx], [0, 1, -dy]])
            f2 = cv2.warpAffine(fig.astype(np.uint8), M, fig.shape[::-1]) > 0
            y_l = int(rb["bottom"]) + 4
            a, c = rfig[y_l:], f2[y_l:]
            r["legs_iou"] = round(float((a & c).sum() / max((a | c).sum(), 1)), 3)
            g1 = a.astype(np.float32)
            g2 = c.astype(np.float32)
            (sx, sy), _ = cv2.phaseCorrelate(g1, g2)
            r["legs_shift_after_belt"] = (round(sx, 1), round(sy, 1))
            r["legs_raw_shift"] = (round(sx + dx, 1), round(sy + dy, 1))
        res[key] = r
        print(key, json.dumps(r))
        t = Image.fromarray(im).convert("RGB")
        d = ImageDraw.Draw(t)
        d.line([(0, rb["top"]), (1536, rb["top"])], fill=(255, 255, 0), width=2)
        d.line([(rb["x0"], 600), (rb["x0"], 900)], fill=(255, 255, 0), width=2)
        d.line([(rb["x1"], 600), (rb["x1"], 900)], fill=(255, 255, 0), width=2)
        d.rectangle(HEAD_BOX, outline=(255, 255, 255), width=2)
        d.rectangle([380, 180, 1180, 232], fill=(255, 255, 255))
        d.text((388, 186), key, fill=(0, 0, 0))
        d.text((388, 206), RATINGS.get(key, "")[:110], fill=(0, 0, 0))
        tiles.append(t.crop((380, 180, 1180, 1340)))
    sheet = Image.new("RGB", (sum(t.width + 8 for t in tiles), tiles[0].height), (255, 255, 255))
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0))
        x += t.width + 8
    fit(sheet, 2400, 2.2).save(OUT / "seeds.png")
    old = json.loads((OUT / "gate.json").read_text()) if (OUT / "gate.json").exists() else {}
    old.update(res)
    (OUT / "gate.json").write_text(json.dumps(old, indent=1))


if __name__ == "__main__":
    main(sys.argv[1:])
