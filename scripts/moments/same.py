"""Is a piece the same piece in every moment? Numbers of one layer (all of a piece's layers at a moment merged) against
its ready layer. Written for step A4; calibrated on the known failures (scale_gauntlets_a3 ready s932 vs wind s936:
steel gauntlet vs leather glove; step A's Scale Mail with and without its shoulder plate).

metrics(layer)  -> steel share, steel L (median, quartiles), other colours' median Lab, plate area
compare(m, ready) -> per number the difference and a verdict
<venv python> -I -B same.py calib     prints the numbers of the calibration pairs
"""
import sys
from pathlib import Path

import cv2
import numpy as np
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, load_rgba  # noqa: E402

PLATE_MIN = 2500     # a smooth steel region at least this big (drawing px, hero 1000 px tall) is a plate, not a scale
LIMITS = {"steel_share": 0.15, "steel_L": 8.0, "other_dE": 14.0}


def lab(a):
    return cv2.cvtColor(np.ascontiguousarray(a[..., :3]), cv2.COLOR_RGB2LAB).astype(np.float32) * [100 / 255, 1, 1] - [0, 128, 128]


def steel_mask(a):
    rgb = a[..., :3].astype(int)
    return (a[..., 3] > 200) & (rgb.max(-1) - rgb.min(-1) < 28) & (rgb.max(-1) > 45)


def skin_like(a):
    import cut
    return cut.skin_mask(a)


def metrics(layer: np.ndarray, plate: bool = False) -> dict:
    L = lab(layer)
    # the piece's own pixels: not skin, not the cream shirt sleeves a chest layer carries where it hides the shirt
    cream = (L[..., 0] > 58) & (L[..., 2] > 8) & (np.abs(L[..., 1]) < 20)
    solid = (layer[..., 3] > 200) & ~skin_like(layer) & ~cream
    st = steel_mask(layer) & solid
    other = solid & ~st & (L[..., 0] > 12)  # leather, cloth: not steel, not the outline
    out = {"px": int(solid.sum()), "steel_share": round(float(st.sum() / max(1, solid.sum())), 3),
           "steel_L": [round(float(x), 1) for x in np.percentile(L[..., 0][st], [25, 50, 75])] if st.sum() > 50 else None,
           "other_Lab": [round(float(np.median(L[..., i][other])), 1) for i in range(3)] if other.sum() > 50 else None}
    if plate:
        out["plate_px"] = plate_area(layer)
    return out


def plate_area(layer: np.ndarray) -> int:
    """The biggest smooth steel region: steel pixels whose 7x7 neighbourhood is all steel without a dark line (a scale
    is a small region ringed by its outline; a shoulder plate is one big one)."""
    st = steel_mask(layer)
    L = lab(layer)[..., 0]
    dark = (L < 22) & (layer[..., 3] > 100)
    smooth = ndi.binary_erosion(st & ~dark, iterations=3)
    lab_, n = ndi.label(smooth)
    if not n:
        return 0
    return int(ndi.sum(smooth, lab_, range(1, n + 1)).max())


def compare(cur: dict, ref: dict) -> dict:
    d = {}
    bad = []
    d["steel_share"] = round(cur["steel_share"] - ref["steel_share"], 3)
    if abs(d["steel_share"]) > LIMITS["steel_share"]:
        bad.append("steel share")
    if cur["steel_share"] < 0.05 and ref["steel_share"] < 0.05:
        pass  # a leather piece: a buckle or two is not steel to compare
    elif cur["steel_L"] and ref["steel_L"]:
        d["steel_L"] = round(cur["steel_L"][1] - ref["steel_L"][1], 1)
        if abs(d["steel_L"]) > LIMITS["steel_L"]:
            bad.append("steel tone")
    elif bool(cur["steel_L"]) != bool(ref["steel_L"]):
        bad.append("steel missing")
    if cur["other_Lab"] and ref["other_Lab"]:
        d["other_dE"] = round(float(np.linalg.norm(np.array(cur["other_Lab"]) - ref["other_Lab"])), 1)
        if d["other_dE"] > LIMITS["other_dE"]:
            bad.append("leather/cloth colour")
    if "plate_px" in ref:
        has_r, has_c = ref["plate_px"] >= PLATE_MIN, cur.get("plate_px", 0) >= PLATE_MIN
        d["plate"] = f"{cur.get('plate_px', 0)} vs {ref['plate_px']}"
        if has_r != has_c:
            bad.append("shoulder plate " + ("missing" if has_r else "added"))
    d["verdict"] = "same" if not bad else "DIFFERS: " + ", ".join(bad)
    return d


def merged(folder: Path, piece: str) -> np.ndarray:
    acc = None
    for f in sorted(folder.glob(f"{piece}__*.png")):
        a = load_rgba(f)
        acc = a if acc is None else np.where(a[..., 3:] > acc[..., 3:], a, acc)
    return acc


def calib():
    A3 = REDO / "layers_A3"
    r = metrics(merged(A3 / "ready", "scale_gauntlets_a3"))
    w = metrics(merged(A3 / "wind", "scale_gauntlets_a3"))
    print("gauntlets ready s932", r)
    print("gauntlets wind  s936", w, compare(w, r))
    SA = REDO / "layers_stepA"
    ref = metrics(merged(SA / "ready", "scale_mail"), plate=True)
    print("mail stepA ready", ref)
    for m in ("wind", "strike", "follow"):
        c = metrics(merged(SA / m, "scale_mail"), plate=True)
        print("mail stepA", m, c, compare(c, ref))


if __name__ == "__main__":
    if sys.argv[1] == "calib":
        calib()
