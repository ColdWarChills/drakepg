"""Round 3 gate before any clip: the shoulder at 3x on the clip's own poses (swing_r3.build, smear as in the clip).
<venv python> -I -B r3_shoulder.py [tag]         -> art/tests/hero_joints/r3/shoulder_3x<tag>.png
<venv python> -I -B r3_shoulder.py pre r1,r2,..  -> r3/_shoulder_pre.png: 2x, one row per cap rest turn (one knob), and
                                                    the coverage count over every clip frame per setting
Panels: ready, mid raise, wind-up, the most raised frame, mid cut, strike (+ the worst coverage frame if not 0)."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702

import numpy as np
from PIL import Image, ImageDraw

import rig_r3 as rig
import swing_r3 as sw
from jcommon import TESTS, fit

OUT = TESTS / "r3"
C = 69          # half crop (drawing px)


def coverage(fr, frames):
    worst = (0, None)
    for f in frames:
        n = fr.cap_uncovered(f["pose"], f["g"]["S"])
        if n > worst[0]:
            worst = (n, f)
    return worst


def tile(fr, parts, bg, name, f, K):
    smear = sw.smear_polys(f, parts.tip_len, fr.ox, fr.oy) if f["smear_src"] else None
    px, info = fr.render(f["pose"], smear=smear)
    img = px[..., :3] + bg * (1 - px[..., 3:4])
    im = Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8))
    c = info["S"] + rig.vec(f["pose"]["a1"], 22)
    box = (int(c[0] - C), int(c[1] - C), int(c[0] + C), int(c[1] + C))
    t = im.crop(box).resize((2 * C * K, 2 * C * K), Image.LANCZOS)
    d = ImageDraw.Draw(t)
    txt = f"{name} f{f['f']} a1 {f['pose']['a1']:.0f} ua {f['pose']['ua']:.2f} open {info['cap_uncovered']}"
    d.rectangle([0, 0, 6 * len(txt) + 6, 13], fill=(255, 255, 255))
    d.text((3, 1), txt, fill=(0, 0, 0))
    print(txt, "seam px %.0f" % info.get("seam_line_px", 0))
    return t


def main():
    parts = rig.Parts()
    fr = rig.Frame(parts)
    bg = sw.background(fr)
    if len(sys.argv) > 1 and sys.argv[1] == "pre":
        rows = []
        for r in [float(v) for v in sys.argv[2].split(",")]:
            rig.CAP_REST = r
            frames = sw.build(fr)
            n, wf = coverage(fr, frames)
            print("cap rest", r, "worst uncovered", n, "at", wf["f"] if wf else None)
            first = [f for f in frames if f["s"] is not None][:112]
            by_s = {f["s"]: f for f in first}
            lifted = max(first, key=lambda f: 1 - f["pose"]["ua"])
            pick = [(f"rest {r:+.0f} ready", frames[0]), ("wind-up", by_s[34]), (f"most raised s{lifted['s']}", lifted),
                    ("mid cut", by_s[49])]
            rows.append([tile(fr, parts, bg, nm, f, 2) for nm, f in pick])
        t = rows[0][0].width
        sheet = Image.new("RGB", (4 * (t + 4), len(rows) * (t + 4)), (255, 255, 255))
        for j, row in enumerate(rows):
            for i, tl in enumerate(row):
                sheet.paste(tl, (i * (t + 4), j * (t + 4)))
        sheet = fit(sheet)
        sheet.save(OUT / "_shoulder_pre.png")
        print("saved", sheet.size)
        return
    tag = sys.argv[1] if len(sys.argv) > 1 else ""
    frames = sw.build(fr)
    n, wf = coverage(fr, frames)
    print("worst uncovered", n, "at", wf["f"] if wf else None)
    first = [f for f in frames if f["s"] is not None][:112]
    by_s = {f["s"]: f for f in first}
    lifted = max(first, key=lambda f: 1 - f["pose"]["ua"])
    pick = [("ready", frames[0]), ("mid raise", by_s[15]), ("wind-up", by_s[34]),
            (f"most raised s{lifted['s']}", lifted), ("mid cut", by_s[49]), ("strike", by_s[65])]
    if n:
        pick[3] = (f"worst cover f{wf['f']}", wf)
    tiles = [tile(fr, parts, bg, nm, f, 3) for nm, f in pick]
    t = tiles[0].width
    sheet = Image.new("RGB", (3 * (t + 4), 2 * (t + 4)), (255, 255, 255))
    for i, tl in enumerate(tiles):
        sheet.paste(tl, ((i % 3) * (t + 4), (i // 3) * (t + 4)))
    sheet = fit(sheet)
    sheet.save(OUT / f"shoulder_3x{tag}.png")
    print("saved", sheet.size)


if __name__ == "__main__":
    main()
