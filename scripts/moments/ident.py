"""The bare hero stacked from layers/ is pixel-identical, moment by moment, to the one stacked from the frozen copy
layers_A3/ (step A4 promise: the body, fist and weapon are not touched). Also compares each moment's weapon spot,
angle, scale, order and soles.

<venv python> -I -B ident.py      prints one line per moment and exits 1 on any difference
"""
import json
import sys
from pathlib import Path

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, load_rgba  # noqa: E402
import cut  # noqa: E402


def stack(folder: Path, m: str, man: dict) -> np.ndarray:
    d = man["moments"][m]
    out = None
    names = (["weapon"] if d["weapon_order"] == "back" else []) + ["body", "shirt", "hair"] + \
            (["weapon"] if d["weapon_order"] != "back" else []) + ["fist"]
    for n in names:
        a = load_rgba(folder / m / f"{n}.png")
        out = a if out is None else cut.over(out, a)
    return out


def main():
    new, old = REDO / "layers", REDO / "layers_A3"
    mn = json.loads((new / "manifest.json").read_text())
    mo = json.loads((old / "manifest.json").read_text())
    bad = 0
    for m in ("ready", "wind", "strike"):
        a, b = stack(new, m, mn), stack(old, m, mo)
        diff = int((a != b).any(-1).sum())
        keys = ("weapon", "weapon_order", "soles_y", "offset_y", "clean", "from")
        same_keys = all(mn["moments"][m][k] == mo["moments"][m][k] for k in keys)
        print(f"{m}: pixels differing {diff}, weapon/order/soles/clean equal {same_keys}")
        bad += diff > 0 or not same_keys
    print("moments in the manifest:", list(mn["moments"]))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
