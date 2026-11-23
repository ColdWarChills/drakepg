"""May a step-A wind-up edit be kept? (step A4 rule) Step A's wind-up edits were made on the old wind-up body
(norm_plain_short_wind_s212); today's wind-up (norm_handmwind_s707, clean_wind_s901) is that body with only the fist
redrawn. Kept only if the clean wind-up equals the old body outside the fist region (same normalizing, compared
premultiplied) and the edit's layer stays clear of that region (grown by 3 px).

<venv python> -I -B windkeep.py     prints the region's size and, per step-A wind edit, its layer pixels inside it
"""
import json
import sys
from pathlib import Path

import numpy as np
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, load_rgba  # noqa: E402
import build  # noqa: E402
import layers as L  # noqa: E402
import place  # noqa: E402
from pieces import PIECES  # noqa: E402


def pm(x):
    return x[..., :3].astype(int) * x[..., 3:].astype(int) // 255


def main():
    old = load_rgba(REDO / "norm_plain_short_wind_s212.png")
    p = build.picks()
    c = load_rgba(REDO / f"{p['clean']['wind']}.png")
    d = (np.abs(pm(old) - pm(c)).max(-1) > 3) | (np.abs(old[..., 3].astype(int) - c[..., 3].astype(int)) > 3)
    lab, n = ndi.label(ndi.binary_dilation(d, iterations=2))
    sizes = sorted(ndi.sum(d, lab, range(1, n + 1)), reverse=True) if n else []
    ys, xs = np.nonzero(d)
    print("clean wind vs old wind body: differing px", int(d.sum()), "blobs", n, "largest", [int(s) for s in sizes[:5]],
          "box", (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if len(xs) else None)
    zone = ndi.binary_dilation(d, iterations=3)
    a, parts, hair, terr = build.moment_kit("wind", p)
    stepA = json.loads((REDO / "moments_stepA.json").read_text())["pieces"]
    out = {}
    for piece, by_m in stepA.items():
        tr = by_m.get("wind")
        if not tr:
            continue
        e, _ = place.align(load_rgba(REDO / f"{tr}.png"), old)
        lay = L.cut_piece(old, e, PIECES[piece][0], terr, hair)
        al = np.zeros(old.shape[:2], bool)
        for v in lay.values():
            al |= v[..., 3] > 0
        out[tr] = int((al & zone).sum())
        print(f"  {tr}: layer px {int(al.sum())}, inside the changed region {out[tr]}")
    return out


if __name__ == "__main__":
    main()
