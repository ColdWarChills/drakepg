"""Round 5 fix (the wind-up) gate on each try before cutting: r5_gate's measures against r5w_input instead of
r5_input. Does NOT call r5_gate.main() (that rewrites seeds.png / gate.json of the strike).
<venv python> -I -B r5w_gate.py <key> [<key> ...]   (keys like r5w_wind_s521)
Band (the strike's pass, r5_strike_s501 vs s202): head scale 0.97-1.05 with score >= 0.6, belt L ratio >= 0.92,
golden rim px < 50, IoU with the input >= 0.88.
-> art/tests/hero_joints/r5/seeds_wind.png (s202 | input | tries, rated from ratings_wind.json) and gate_wind.json."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image, ImageDraw

import r5_gate as g
from jcommon import fit

OUT = g.OUT
INP = "r5w_input"
RATE_FILE = OUT / "ratings_wind.json"
BAND = dict(head=(0.97, 1.05), head_score=0.6, belt_L=0.92, rim=50, iou=0.88)


def verdict(r):
    fails = []
    h = r["head"]
    if not (BAND["head"][0] <= h["scale"] <= BAND["head"][1]) or h["score"] < BAND["head_score"]:
        fails.append("head %.2f/%.2f" % (h["scale"], h["score"]))
    if r.get("belt_L_ratio", 0) < BAND["belt_L"]:
        fails.append("belt %.2f" % r.get("belt_L_ratio", 0))
    if r["rim"] >= BAND["rim"]:
        fails.append("rim %d" % r["rim"])
    if r["iou_input"] < BAND["iou"]:
        fails.append("iou %.2f" % r["iou_input"])
    return "PASS" if not fails else "FAIL " + ", ".join(fails)


def main(keys):
    ref, inp = g.load(g.REF), g.load(INP)
    rfig, ifig = g.fig_mask(ref), g.fig_mask(inp)
    base = dict(belt=g.belt(ref, rfig), boots=g.boots(ref, rfig), rim=g.rim_px(ref, rfig))
    res = {g.REF: base}
    ratings = {g.REF: "hero 4 standing (the source of the outfit)", INP: "input: today's wind drawing on hero 4's canvas"}
    if RATE_FILE.exists():
        ratings.update(json.loads(RATE_FILE.read_text()))
    tiles = []
    for key in [g.REF, INP] + keys:
        im = g.load(key)
        fig = g.fig_mask(im)
        if key not in res:
            r = dict(belt=g.belt(im, fig), boots=g.boots(im, fig), rim=g.rim_px(im, fig), head=g.head_match(ref, im))
            ys, xs = np.nonzero(fig)
            r["bbox"] = [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]
            r["iou_input"] = round(float((fig & ifig).sum() / max((fig | ifig).sum(), 1)), 3)
            if r["belt"] and base["belt"]:
                r["belt_L_ratio"] = round(r["belt"]["L"] / base["belt"]["L"], 3)
                r["belt_T_ratio"] = round(r["belt"]["T"] / base["belt"]["T"], 3)
            if len(r["boots"]) == 2:
                r["boot_size_ratio"] = [round(float(np.sqrt(b["area"] / o["area"])), 3)
                                        for b, o in zip(r["boots"], base["boots"])]
            if key != INP:
                r["verdict"] = verdict(r)
            res[key] = r
            print(key, json.dumps(r))
        t = Image.fromarray(im).convert("RGB")
        d = ImageDraw.Draw(t)
        d.rectangle([250, 140, 1290, 196], fill=(255, 255, 255))
        d.text((258, 146), key + "   " + res[key].get("verdict", ""), fill=(0, 0, 0))
        d.text((258, 170), ratings.get(key, "")[:150], fill=(0, 0, 0))
        d.line([(0, 1283), (1536, 1283)], fill=(255, 255, 0), width=2)
        tiles.append(t.crop((250, 140, 1290, 1340)))
    cols = 3
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tiles[0].width + 8), rows * (tiles[0].height + 8)), (255, 255, 255))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * (t.width + 8), (i // cols) * (t.height + 8)))
    fit(sheet, 2400, 3.0).save(OUT / "seeds_wind.png")
    gf = OUT / "gate_wind.json"
    old = json.loads(gf.read_text()) if gf.exists() else {}
    old.update(res)
    gf.write_text(json.dumps(old, indent=1))


if __name__ == "__main__":
    main(sys.argv[1:])
