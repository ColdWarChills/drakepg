"""The clean drawing of a moment: the same drawing with the marker stick gone and the sword hand a whole closed fist.

Everything the player sees comes from the clean drawing (the body sprite, the fist copy over the weapon, every armor
edit made from step A3 on). The marker drawing is used only to read the fist's spot and the blade's angle
(bare.read_blade) and for the grip check.

How: one Qwen edit of a crop round the fist and the whole marker ("remove the stick, the hand stays exactly as it is"),
enlarged so the crop is about 1300 px. The edit is shrunk back, shifted onto the drawing (best integer shift at the
enlarged scale, judged on the unchanged ring round the stick and the fist), colour-matched on that ring (gain and offset
per channel) and taken only inside the paste region: the old and new sword fist and the marker's footprint with its
coloured fringe, grown a little, with a soft edge. Outside it the clean drawing is the marker drawing pixel for pixel.
The crop box comes from the marker's extent and the fist (no hand-placed numbers).

<venv python> -I -B clean.py run <moment>:<seed>[,<seed>] ...   Qwen -> art/redo/moments/clean_<moment>_s<seed>.png
                                                                   (+ _edit.png, the enlarged edit); runs.jsonl
<venv python> -I -B clean.py check <moment> <clean try> ...       the code checks (also written by `run`)
Checks per try (clean_checks.json): skin overlap of the sword fist (IoU, marker drawing vs clean) >= 0.85, its centre
moved <= 3 px, no marker colour or fringe left, the ring where the paste fades out equal to the drawing (mean step).
"""
import json
import math
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CANVAS, REDO, RGBA_CLOSE, RGBA_OPEN, STYLE, Comfy, load_rgba, png_bytes  # noqa: E402
import bare  # noqa: E402
import cut  # noqa: E402

TARGET = 1300      # the enlarged crop's side (px)
MARGIN = 48        # round the marker and the fist (drawing px)
FIST_R = 1.15      # the sword fist's disc: FIST_R x max(gap, 3 x stick width)
CHECKS = REDO / "clean_checks.json"


def picks() -> dict:
    return json.loads((REDO / "moments.json").read_text())


def fringe(a: np.ndarray, rod_mask: np.ndarray) -> np.ndarray:
    """The marker with its coloured fringe: green- or pink-tinted pixels near it (remove_rod's loose tests)."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    tint = (a[..., 3] > 0) & (g > r + 12) & (g > b + 12)
    pink = (a[..., 3] > 0) & (b > g + 12) & (r > g + 12)
    near = ndi.binary_dilation(rod_mask, iterations=24)
    return rod_mask | cut.marker_mask(a) & near | (tint | pink) & near


def leftover(a: np.ndarray, rod_mask: np.ndarray, magenta: np.ndarray | None = None) -> int:
    """Marker colour or fringe left near the old marker (the loose tests: they catch the pink and green fringe)."""
    r, g, b = (a[..., i].astype(int) for i in range(3))
    tint = (a[..., 3] > 30) & (g > r + 12) & (g > b + 12)
    pink = (a[..., 3] > 30) & (b > g + 12) & (r > g + 12)
    red = (a[..., 3] > 30) & (r > 170) & (r > g + 50) & (b > g - 15)  # magenta blended into a warm edge: red, b close to g
    near = ndi.binary_dilation(rod_mask, iterations=24)
    out = int(((tint | pink | red | red_speck(a)) & near).sum() + cut.marker_mask(a).sum())
    if magenta is not None:
        out += int((warm_red(a) & ndi.binary_dilation(magenta, iterations=6)).sum())
    return out


def warm_red(c: np.ndarray) -> np.ndarray:
    """Red edge pixels (g low, well under the skin's) that Qwen draws where the magenta stub's fringe was."""
    r, g, b = (c[..., i].astype(int) for i in range(3))
    return (c[..., 3] > 0) & (r > 110) & (g < 0.47 * r)


def red_speck(c: np.ndarray) -> np.ndarray:
    """Red or pink specks: g under 0.40 r, or b at least g with r well above both (never skin, its lines or the shirt)."""
    r, g, b = (c[..., i].astype(int) for i in range(3))
    return (c[..., 3] > 0) & (r > 110) & ((g < 0.40 * r) | ((b >= g - 4) & (r > g + 40)))


def scrub(c: np.ndarray, rod_mask: np.ndarray, reg: np.ndarray, magenta: np.ndarray | None = None) -> np.ndarray:
    """Marker-tinted pixels the edit left inside the paste region take the colour of the nearest untinted solid pixel
    with their alpha kept (faint ones away from the figure's edge are dropped); then `edge_tint`."""
    r, g, b = (c[..., i].astype(int) for i in range(3))
    bad = ((c[..., 3] > 0) & (((g > r + 12) & (g > b + 12)) | ((b > g + 12) & (r > g + 12)) | ((r > 170) & (r > g + 50) & (b > g - 15)))
           | cut.marker_mask(c))
    bad &= ndi.binary_dilation(reg, iterations=2)
    out = c.copy()
    wr = np.zeros_like(bad)
    if magenta is not None:  # warm red edge by the old magenta stub (Qwen's redraw of its fringe)
        wr = warm_red(c) & ndi.binary_dilation(magenta, iterations=6) & ndi.binary_dilation(reg, iterations=2)
    # anywhere in the region: plain red or pinkish specks (skin and its lines keep g >= 0.5 r and b well under g)
    wr |= (red_speck(c) & ndi.binary_dilation(reg, iterations=2))
    if bad.any():  # take the nearest untinted solid pixel's colour, keep the alpha (the edge stays anti-aliased)
        solid = (c[..., 3] > 200) & ~bad & ~wr
        _, (iy, ix) = ndi.distance_transform_edt(~solid, return_indices=True)
        out[bad, :3] = c[iy[bad], ix[bad], :3]
        # faint ones away from the figure's edge are leftover fringe over the ground: dropped
        out[bad & (c[..., 3] < 128) & ~ndi.binary_dilation(solid, iterations=2), 3] = 0
    if wr.any():  # -> the hand's own warm brown line colour
        rr = out[..., 0].astype(np.float32)
        out[wr, 1] = np.clip(rr[wr] * 0.58, 0, 255).astype(np.uint8)
        out[wr, 2] = np.clip(rr[wr] * 0.33, 0, 255).astype(np.uint8)
    return edge_tint(out, reg)


def edge_tint(c: np.ndarray, reg: np.ndarray) -> np.ndarray:
    """Semi-transparent edge pixels in the paste region that are redder or pinker than the solid pixel next to them
    (Qwen's salmon anti-aliasing where the magenta stub was) take that pixel's colour; their alpha is kept. The
    drawing's pale yellow rim is redder in nothing and keeps its colour."""
    out = c.copy()
    a = c[..., 3]
    edge = ndi.binary_dilation(reg, iterations=2) & (a > 0) & (a < 250)
    solid = a >= 250
    if not edge.any() or not solid.any():
        return out
    _, (iy, ix) = ndi.distance_transform_edt(~solid, return_indices=True)
    f = c[..., :3].astype(np.float32)
    n = f[iy, ix]
    gr, ngr = f[..., 1] / np.maximum(f[..., 0], 1), n[..., 1] / np.maximum(n[..., 0], 1)
    pink = (gr < ngr - 0.05) | ((f[..., 2] - n[..., 2] > 30) & (gr <= ngr + 0.04))
    fix = edge & pink
    out[fix, :3] = c[iy[fix], ix[fix], :3]
    return out


def fist_disc(rod: dict, shape, scale: float = FIST_R) -> np.ndarray:
    H, W = shape[:2]
    cx, cy = rod["center"]
    rad = max(rod["gap"], rod["width"] * 3) * scale
    YY, XX = np.mgrid[0:H, 0:W]
    return (XX - cx) ** 2 + (YY - cy) ** 2 < rad ** 2


def sword_skin(a: np.ndarray, rod: dict) -> np.ndarray:
    """Skin of the sword hand: the skin blobs inside the fist disc that touch the stick's middle (the gap) or lie
    within a stick width of the fist centre."""
    sk = cut.skin_mask(a) & fist_disc(rod, a.shape, 0.85)
    lab, _ = ndi.label(sk)
    H, W = sk.shape
    cx, cy = rod["center"]
    YY, XX = np.mgrid[0:H, 0:W]
    core = (XX - cx) ** 2 + (YY - cy) ** 2 < (rod["width"] * 1.5) ** 2
    ids = set(np.unique(lab[core])) - {0}
    return np.isin(lab, list(ids))


def crop_box(a: np.ndarray, rod: dict) -> tuple:
    """Square box round the marker (with its fringe) and the fist disc, plus MARGIN; inside the canvas."""
    m = fringe(a, rod["mask"]) | fist_disc(rod, a.shape)
    ys, xs = np.nonzero(m)
    x0, y0, x1, y1 = xs.min() - MARGIN, ys.min() - MARGIN, xs.max() + MARGIN, ys.max() + MARGIN
    side = int(max(x1 - x0, y1 - y0))
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    x0 = int(round(min(max(cx - side / 2, 0), CANVAS - side)))
    y0 = int(round(min(max(cy - side / 2, 0), CANVAS - side)))
    return x0, y0, x0 + side, y0 + side


def prompt(colours: str) -> str:
    return (f"{RGBA_OPEN} Edit <image1>, a close-up of a cartoon hero's arm and right fist. Remove the stick "
            f"completely: the thin straight {colours} stick that passes through his fist is gone, nothing is left of it, "
            f"no {colours} anywhere. His hand stays exactly as it is: the same closed fist, empty, the fingers curled "
            f"tight into the palm, the same side of the hand toward the viewer, the thumb exactly where it is, the "
            f"same size, place and shape; where the stick came out of the fist, the fist is closed with its own "
            f"outline and rim. Where the stick lay in front of his body or clothes, draw his body and clothes as they "
            f"are around it; where the stick lay over the empty background, there is only transparent background. "
            f"Keep everything else of <image1> exactly the same: the forearm, the wrist, the clothes, every outline and "
            f"the rim light, {STYLE}. No sword, no stick, no other object, no text. {RGBA_CLOSE}")


def colours_of(a: np.ndarray) -> str:
    return "green and magenta" if cut.magenta_mask(a).sum() > 60 else "bright green"


def _premul(x):
    f = x.astype(np.float32)
    f[..., :3] *= f[..., 3:] / 255
    return f


def paste(a: np.ndarray, rod: dict, b: tuple, edit: np.ndarray) -> tuple:
    """-> (clean drawing, info). The edit (enlarged crop) shifted, colour-matched and pasted inside the paste region."""
    side = b[2] - b[0]
    z = edit.shape[0] / side
    old = a[b[1]:b[3], b[0]:b[2]]
    foot = fringe(a, rod["mask"])[b[1]:b[3], b[0]:b[2]]
    disc = fist_disc(rod, a.shape)[b[1]:b[3], b[0]:b[2]]
    # the unchanged ring: inside the crop, away from the stick and the fist, where either picture is drawn
    hold = ~ndi.binary_dilation(foot | disc, iterations=10)
    hold[:6], hold[-6:], hold[:, :6], hold[:, -6:] = False, False, False, False
    big_old = cv2.resize(_premul(old), (edit.shape[1], edit.shape[0]), interpolation=cv2.INTER_CUBIC)
    big_hold = cv2.resize(hold.astype(np.uint8), (edit.shape[1], edit.shape[0]), interpolation=cv2.INTER_NEAREST) > 0
    pe = _premul(edit)
    best = None
    R = int(round(3 * z))
    for dy in range(-R, R + 1):
        for dx in range(-R, R + 1):
            sh = np.roll(pe, (dy, dx), (0, 1))
            d = np.abs(sh - big_old)[big_hold].mean()
            if best is None or d < best[0]:
                best = (d, dx, dy)
    _, dx, dy = best
    ed = np.roll(edit, (dy, dx), (0, 1))
    small = cv2.resize(_premul(ed), (side, side), interpolation=cv2.INTER_AREA)
    al = small[..., 3:]
    rgb = np.where(al > 0, small[..., :3] * 255 / np.maximum(al, 1e-3), 0)
    new = np.concatenate([rgb, al], -1)
    new[..., 3] = np.where(new[..., 3] < 10, 0, new[..., 3])
    # colour match on the ring where both are solid
    both = hold & (old[..., 3] > 230) & (new[..., 3] > 230)
    gains = []
    for c in range(3):
        x, y = new[..., c][both], old[..., c].astype(np.float32)[both]
        k, o = np.polyfit(x, y, 1) if len(x) > 500 else (1.0, 0.0)
        new[..., c] = np.clip(new[..., c] * k + o, 0, 255)
        gains.append((round(float(k), 3), round(float(o), 1)))
    # the paste region: old and new sword fist, the marker and its fringe, grown; soft edge
    newfull = np.zeros_like(a)
    newfull[b[1]:b[3], b[0]:b[2]] = np.clip(new + 0.5, 0, 255).astype(np.uint8)
    fist_old = ndi.binary_fill_holes(ndi.binary_closing(sword_skin(a, rod), iterations=int(rod["width"])))
    fist_new = sword_skin(newfull, rod)
    reg = np.zeros(a.shape[:2], bool)
    reg[b[1]:b[3], b[0]:b[2]] = foot
    reg = ndi.binary_dilation(reg, iterations=6) | ndi.binary_dilation(fist_old | fist_new, iterations=8)
    inbox = np.zeros_like(reg)
    inbox[b[1] + 3:b[3] - 3, b[0] + 3:b[2] - 3] = True
    reg &= inbox
    soft = ndi.gaussian_filter(reg.astype(np.float32), 1.5)[..., None]
    soft[~ndi.binary_dilation(reg, iterations=4)] = 0
    out = _premul(a) * (1 - soft) + _premul(newfull) * soft
    al = out[..., 3:]
    rgb = np.where(al > 0, out[..., :3] * 255 / np.maximum(al, 1e-3), 0)
    clean = np.clip(np.concatenate([rgb, al], -1) + 0.5, 0, 255).astype(np.uint8)
    clean[..., 3] = np.where(clean[..., 3] < 10, 0, clean[..., 3])
    clean = scrub(clean, rod["mask"], reg, rod["mask"] & cut.magenta_mask(a))
    clean[clean[..., 3] == 0, :3] = 0
    info = {"box": list(map(int, b)), "zoom": round(z, 3), "shift_px": [round(dx / z, 2), round(dy / z, 2)],
            "ring_diff": round(float(best[0]), 2), "colour_fit": gains, "region_px": int(reg.sum())}
    return clean, reg, info


def check(a: np.ndarray, clean: np.ndarray, rod: dict, reg: np.ndarray | None = None) -> dict:
    old = ndi.binary_fill_holes(ndi.binary_closing(sword_skin(a, rod), iterations=int(rod["width"])))
    new = ndi.binary_fill_holes(ndi.binary_closing(sword_skin(clean, rod), iterations=3))
    iou = float((old & new).sum() / max(1, (old | new).sum()))
    co = np.array(ndi.center_of_mass(old))[::-1]
    cn = np.array(ndi.center_of_mass(new))[::-1]
    diff = np.abs(_premul(a) - _premul(clean)).max(-1)
    diff[(a[..., 3] == 0) & (clean[..., 3] == 0)] = 0
    out = {"fist_iou": round(iou, 3), "fist_centre_moved_px": round(float(np.hypot(*(co - cn))), 2),
           "fist_area_old_new": [int(old.sum()), int(new.sum())], "marker_left_px": leftover(clean, rod["mask"], rod["mask"] & cut.magenta_mask(a))}
    if reg is not None:
        out["changed_outside_region_px"] = int(((diff > 0.5) & ~ndi.binary_dilation(reg, iterations=4)).sum())
        # the paste's fade-out band inside the figure: how far the clean drawing steps from the marker drawing there
        band = ndi.binary_dilation(reg, iterations=4) & ~ndi.binary_erosion(reg, iterations=3) & (a[..., 3] > 230)
        band &= ~ndi.binary_dilation(fringe(a, rod["mask"]), iterations=8)
        out["fade_band_mean_step"] = round(float(diff[band].mean()), 2) if band.any() else 0.0
        out["fade_band_share_over_40"] = round(float((diff[band] > 40).mean()), 4) if band.any() else 0.0
    out["pass"] = bool(out["fist_iou"] >= 0.85 and out["fist_centre_moved_px"] <= 3 and out["marker_left_px"] == 0)
    return out


def save_check(name: str, row: dict):
    d = json.loads(CHECKS.read_text()) if CHECKS.exists() else {}
    d[name] = row
    CHECKS.write_text(json.dumps(d, indent=1))


def run(args):
    c = Comfy()
    c.check()
    p = picks()
    for arg in args:
        m, seeds = arg.split(":")
        src = p[m]
        a = load_rgba(REDO / f"{src}.png")
        rod = bare.read_blade(a, src)
        b = crop_box(a, rod)
        side = b[2] - b[0]
        z = max(1.0, TARGET / side)
        im = Image.fromarray(a).crop(b)
        big = np.array(im.resize((round(side * z), round(side * z)), Image.LANCZOS))
        big[big[..., 3] == 0, :3] = 0
        Image.fromarray(big).save(REDO / f"clean_{m}_in.png")
        up = [c.upload(f"moments_clean_{m}_in.png", png_bytes(big))]
        pr = prompt(colours_of(a))
        for s in [int(x) for x in seeds.split(",")]:
            name = f"clean_{m}_s{s}"
            out = REDO / f"{name}.png"
            if out.exists():
                continue
            t = time.time()
            data = c.generate(pr, up, s)
            sec = round(time.time() - t)
            (REDO / f"{name}_edit.png").write_bytes(data)
            edit = load_rgba(REDO / f"{name}_edit.png")
            clean, reg, info = paste(a, rod, b, edit)
            Image.fromarray(clean).save(out)
            np.save(REDO / f"{name}_region.npy", np.packbits(reg))
            row = {"from": src, **info, **check(a, clean, rod, reg), "sec": sec}
            save_check(name, row)
            with open(REDO / "runs.jsonl", "a") as f:
                f.write(json.dumps({"file": out.name, "sec": sec, "utc": datetime.now(timezone.utc).isoformat(
                    timespec="seconds"), "prompt": pr, "body": src, "box": list(map(int, b))}) + "\n")
            print("QWEN", out.name, sec, "s", json.dumps(row), flush=True)


def region(name: str, shape) -> np.ndarray:
    return np.unpackbits(np.load(REDO / f"{name}_region.npy"))[:shape[0] * shape[1]].reshape(shape[:2]).astype(bool)


def repaste(names):
    """Paste a kept edit (<name>_edit.png) again with the present paste code: no Qwen run."""
    p = picks()
    for name in names:
        m = name.split("_")[1]
        src = p[m]
        a = load_rgba(REDO / f"{src}.png")
        rod = bare.read_blade(a, src)
        b = crop_box(a, rod)
        clean, reg, info = paste(a, rod, b, load_rgba(REDO / f"{name}_edit.png"))
        Image.fromarray(clean).save(REDO / f"{name}.png")
        np.save(REDO / f"{name}_region.npy", np.packbits(reg))
        old = json.loads(CHECKS.read_text()).get(name, {}) if CHECKS.exists() else {}
        row = {"from": src, **info, **check(a, clean, rod, reg), "sec": old.get("sec")}
        save_check(name, row)
        print(name, json.dumps(row), flush=True)


if __name__ == "__main__":
    if sys.argv[1] == "run":
        run(sys.argv[2:])
    elif sys.argv[1] == "repaste":
        repaste(sys.argv[2:])
    elif sys.argv[1] == "check":
        m = sys.argv[2]
        src = picks()[m]
        a = load_rgba(REDO / f"{src}.png")
        rod = bare.read_blade(a, src)
        for n in sys.argv[3:]:
            print(n, check(a, load_rgba(REDO / f"{n}.png"), rod, region(n, a.shape)))
