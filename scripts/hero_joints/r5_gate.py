"""Round 5 gate on each strike try before cutting: is it still hero 4 (head size, belt, boots, no rim) and is it the
pose of today's strike drawing (silhouette overlap with the input)?
<venv python> -I -B r5_gate.py <key> [<key> ...]   (keys like r5_strike_s501)
Prints per try: head scale / turn / score (round 2's head, s202's HEAD_BOX, matched over scale and rotation), the belt
(long side and thickness of its min-area box), boot areas (sqrt ratio = size ratio), golden rim pixels on the outline,
silhouette IoU with r5_input. Writes art/tests/hero_joints/r5/seeds.png (s202 | input | tries, rated) and gate.json."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

from jcommon import REDO, TESTS, fit

OUT = TESTS / "r5"
OUT.mkdir(parents=True, exist_ok=True)
REF = "r2_vest_teal_s202"
INP = "r5_input"
HEAD_BOX = (650, 250, 900, 480)
RATINGS = {
    REF: "hero 4 standing (the source of the outfit)",
    INP: "input: today's strike drawing on hero 4's canvas",
}
RATE_FILE = OUT / "ratings.json"     # ratings written by eye after the numbers (key -> text)


def load(key):
    return np.asarray(Image.open(REDO / f"{key}.png").convert("RGB"))


def fig_mask(rgb):
    s = rgb.astype(np.float32)
    bg = np.median(np.concatenate([s[:40, :40].reshape(-1, 3), s[-40:, -40:].reshape(-1, 3)]), 0)
    m = (np.abs(s - bg).max(-1) > 40).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    return lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))


def comp(mask, k_open=5):
    m = cv2.morphologyEx(mask.astype(np.uint8), cv2.MORPH_OPEN, np.ones((k_open, k_open), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    return [(lab == i, int(st[i, cv2.CC_STAT_AREA])) for i in range(1, n)]


def browns(rgb, fig):
    R, G, B = [rgb[..., i].astype(np.int32) for i in range(3)]
    return fig & (R < 120) & (G < 85) & (B < 60) & (R - B > 15) & (R > G)


def belt(rgb, fig):
    """The belt = the brown component with the most elongated min-area box in the figure's middle third."""
    H = fig.shape[0]
    ys = np.nonzero(fig.any(1))[0]
    y0, y1 = ys.min(), ys.max()
    m = browns(rgb, fig)
    m[: int(y0 + 0.3 * (y1 - y0))] = False
    m[int(y0 + 0.65 * (y1 - y0)):] = False
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0  # across the buckle
    best = None
    for c, a in comp(m):
        if a < 800:
            continue
        pts = np.column_stack(np.nonzero(c)[::-1]).astype(np.float32)
        (cx, cy), (w, h), ang = cv2.minAreaRect(pts)
        L, T = max(w, h), min(w, h)
        if best is None or L / T > best["L"] / best["T"]:
            best = dict(cx=round(cx, 1), cy=round(cy, 1), L=round(L, 1), T=round(T, 1), angle=round(ang, 1), area=a)
    return best


def boots(rgb, fig):
    """The two biggest brown components in the lowest 35% of the figure (boots): areas."""
    ys = np.nonzero(fig.any(1))[0]
    y0, y1 = ys.min(), ys.max()
    m = browns(rgb, fig)
    m[: int(y0 + 0.6 * (y1 - y0))] = False
    cs = sorted(comp(m), key=lambda t: -t[1])[:2]
    out = []
    for c, a in cs:
        yy, xx = np.nonzero(c)
        out.append(dict(area=a, x=[int(xx.min()), int(xx.max())], y=[int(yy.min()), int(yy.max())]))
    return sorted(out, key=lambda d: d["x"][0])


def rim_px(rgb, fig):
    """Golden rim: warm light-yellow pixels within 6 px of the outline."""
    R, G, B = [rgb[..., i].astype(np.int32) for i in range(3)]
    gold = (R > 215) & (G > 185) & (B < 150) & (R - B > 90)
    d = cv2.distanceTransform(fig.astype(np.uint8), cv2.DIST_L2, 5)
    return int((gold & fig & (d < 6)).sum())


def head_match(ref, im, angles=range(-24, 25, 4)):
    x0, y0, x1, y1 = HEAD_BOX
    tpl0 = cv2.cvtColor(ref[y0:y1, x0:x1], cv2.COLOR_RGB2GRAY)
    g = cv2.cvtColor(im, cv2.COLOR_RGB2GRAY)
    best = (-1, 1.0, 0, (0, 0))
    for a in angles:
        M = cv2.getRotationMatrix2D(((x1 - x0) / 2, (y1 - y0) / 2), a, 1.0)
        tr = cv2.warpAffine(tpl0, M, tpl0.shape[::-1], borderMode=cv2.BORDER_REPLICATE)
        for k in np.arange(0.85, 1.151, 0.01):
            t = cv2.resize(tr, None, fx=k, fy=k, interpolation=cv2.INTER_AREA)
            r = cv2.matchTemplate(g, t, cv2.TM_CCOEFF_NORMED)
            _, mx, _, loc = cv2.minMaxLoc(r)
            if mx > best[0]:
                best = (float(mx), float(k), a, loc)
    score, k, a, (lx, ly) = best
    return dict(scale=round(k, 3), turn=a, score=round(score, 3), x=int(lx), y=int(ly))


def main(keys):
    ref, inp = load(REF), load(INP)
    rfig, ifig = fig_mask(ref), fig_mask(inp)
    res = {}
    base = dict(belt=belt(ref, rfig), boots=boots(ref, rfig), rim=rim_px(ref, rfig))
    res[REF] = base
    print(REF, json.dumps(base))
    ratings = dict(RATINGS)
    if RATE_FILE.exists():
        ratings.update(json.loads(RATE_FILE.read_text()))
    tiles = []
    for key in [REF, INP] + keys:
        im = load(key)
        fig = fig_mask(im)
        if key not in res:
            r = dict(belt=belt(im, fig), boots=boots(im, fig), rim=rim_px(im, fig), head=head_match(ref, im))
            ys, xs = np.nonzero(fig)
            r["bbox"] = [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]
            r["iou_input"] = round(float((fig & ifig).sum() / max((fig | ifig).sum(), 1)), 3)
            if r["belt"] and base["belt"]:
                r["belt_L_ratio"] = round(r["belt"]["L"] / base["belt"]["L"], 3)
                r["belt_T_ratio"] = round(r["belt"]["T"] / base["belt"]["T"], 3)
            if len(r["boots"]) == 2:
                r["boot_size_ratio"] = [round(float(np.sqrt(b["area"] / o["area"])), 3)
                                        for b, o in zip(r["boots"], base["boots"])]
            res[key] = r
            print(key, json.dumps(r))
        t = Image.fromarray(im).convert("RGB")
        d = ImageDraw.Draw(t)
        d.rectangle([250, 140, 1290, 196], fill=(255, 255, 255))
        d.text((258, 146), key, fill=(0, 0, 0))
        d.text((258, 170), ratings.get(key, "")[:150], fill=(0, 0, 0))
        d.line([(0, 1283), (1536, 1283)], fill=(255, 255, 0), width=2)
        tiles.append(t.crop((250, 140, 1290, 1340)))
    cols = 3
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tiles[0].width + 8), rows * (tiles[0].height + 8)), (255, 255, 255))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * (t.width + 8), (i // cols) * (t.height + 8)))
    fit(sheet, 2400, 3.0).save(OUT / "seeds.png")
    old = json.loads((OUT / "gate.json").read_text()) if (OUT / "gate.json").exists() else {}
    old.update(res)
    (OUT / "gate.json").write_text(json.dumps(old, indent=1))


if __name__ == "__main__":
    main(sys.argv[1:])
