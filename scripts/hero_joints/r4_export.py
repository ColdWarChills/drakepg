"""Round 4, step 2: hero 4's parts for Unity's 2D Animation package (unity/Assets/Test2D/Art/).
<venv python> -I -B r4_export.py

Writes one PNG per sprite (cropped from the 1536 drawing canvas, or painted by rig_r2 for the near arm) and its rim twin
(<name>_rim.png: the rim ring outside that part alone, same size and pivot; all rims sit under all parts, so only the
outer silhouette's rim shows), plus rig.json: per sprite its pivot (px in the PNG, y down) and the joint points on the
drawing canvas (y down) that the Unity build script turns into bones.

Swap drawings (Sprite Library categories): torso front|windup, head front|windup, far_arm front|windup,
armhole front (blank)|windup, sleeve lit|under (the hard shadow band's side), shoulder lit|under."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image

import fist_r2
import rig_r2 as rig
from jcommon import RAW, REDO, ROOT

OUT = ROOT / "unity" / "Assets" / "Test2D" / "Art"
OUT.mkdir(parents=True, exist_ok=True)
G2 = json.loads((REDO / "parts_r2.json").read_text())
G4 = json.loads((REDO / "parts_r4.json").read_text())
M = 10                       # crop margin (rim width 4.5 + room)
ARM_RIM_G = 0.75             # the arm pieces turn, so their rim is even all round (0.75 x 4.5 px)


def premul_to_rgba(px):
    a = px[..., 3:4]
    rgb = np.where(a > 1e-4, px[..., :3] / np.maximum(a, 1e-4), 0)
    return Image.fromarray((np.concatenate([np.clip(rgb, 0, 1), a], -1) * 255 + 0.5).astype(np.uint8), "RGBA")


def rim_of(alpha, even=None):
    """rig_r2's rim ring on one part's alpha; even=g gives the same width all round."""
    if even is None:
        return rig.rim_ring(alpha)
    import cv2
    inside = (alpha > 0.5).astype(np.uint8)
    dist = cv2.distanceTransform(1 - inside, cv2.DIST_L2, 5)
    return np.clip(rig.RIM_W * even + 0.5 - dist, 0, 1) * (1 - alpha)


def save(name, px, pivot, even=None, rim=True):
    """px premultiplied float (H, W, 4); pivot px (x, y down) in px. Saves name.png (+ name_rim.png)."""
    premul_to_rgba(px).save(OUT / f"{name}.png")
    entry = {"file": f"{name}.png", "size": [px.shape[1], px.shape[0]], "pivot": [float(pivot[0]), float(pivot[1])]}
    if rim:
        ring = rim_of(px[..., 3], even)
        r = np.zeros_like(px)
        r[..., :3] = rig.RIM_RGB * ring[..., None]
        r[..., 3] = ring
        premul_to_rgba(r).save(OUT / f"{name}_rim.png")
        entry["rim"] = f"{name}_rim.png"
    return entry


def drop_crumbs(px, share=0.01):
    """Pieces under 1% of the part's biggest piece are left-overs of the cut (round 2's head carried a knuckle of the
    far fist that shows once the far arm moves away): cleared."""
    import cv2
    a = (px[..., 3] > 0.03).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    if n <= 2:
        return px, 0
    big = st[1:, cv2.CC_STAT_AREA].max()
    drop = [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] < share * big]
    keep = (lab > 0) & ~np.isin(lab, drop)
    near = cv2.dilate(keep.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0   # keep the soft edge of kept pieces
    return px * near[..., None], len(drop)


def canvas_part(d, key, pivot_canvas):
    px = rig.to_premul(Image.open(REDO / d / f"{key}.png"))
    px, n = drop_crumbs(px)
    if n:
        print(f"  {d}/{key}: {n} crumbs cleared")
    ys, xs = np.nonzero(px[..., 3] > 0.002)
    x0, y0, x1, y1 = xs.min() - M, ys.min() - M, xs.max() + M + 1, ys.max() + M + 1
    crop = px[y0:y1, x0:x1].copy()
    return crop, (pivot_canvas[0] - x0, pivot_canvas[1] - y0), (int(x0), int(y0))


def pad(px, m):
    return np.pad(px, ((m, m), (m, m), (0, 0)))


def main():
    sprites = {}
    waist, neck, knot = G2["waist"], G2["neck"], G2["knot"]
    for name, d, key, piv in [
        ("hips", "parts_r2", "hips", waist),
        ("torso_front", "parts_r2", "torso", waist),
        ("torso_windup", "parts_r4", "torso", waist),
        ("head_front", "parts_r2", "head", neck),
        ("head_windup", "parts_r4", "head", neck),
        ("tails", "parts_r2", "tails", knot),
        ("far_arm_front", "parts_r2", "far_arm", G2["far_shoulder"]),
        ("far_arm_windup", "parts_r4", "far_arm", G4["far_shoulder"]),
        ("armhole_windup", "parts_r4", "armhole", waist),
    ]:
        crop, pv, org = canvas_part(d, key, piv)
        sprites[name] = save(name, crop, pv)
        sprites[name]["origin"] = org
    # the armhole drawn over the sleeve only in the wind-up: front = a blank of the same size
    blank = np.zeros((8, 8, 4), np.float32)
    sprites["armhole_front"] = save("armhole_front", blank, (4, 4), rim=False)
    # near arm, painted (rig_r2): straight sleeve, axis straight down from the shoulder pivot SRC_S
    for side, tag in ((1.0, "lit"), (-1.0, "under")):
        rig.SHADE_SIDE = side
        sl = pad(rig.paint_sleeve(), M)
        sprites[f"sleeve_{tag}"] = save(f"sleeve_{tag}", sl, (rig.SRC_S[0] + M, rig.SRC_S[1] + M), even=ARM_RIM_G)
        sh, c = rig.paint_shoulder()
        sh = pad(sh, M)
        sprites[f"shoulder_{tag}"] = save(f"shoulder_{tag}", sh, (c[0] + M, c[1] + M), even=ARM_RIM_G)
    rig.SHADE_SIDE = 1.0
    f = fist_r2.load_fist()
    fx = pad(f["img"], M)
    o = (f["origin"][0] - M, f["origin"][1] - M)
    sprites["fist"] = save("fist", fx, (f["wrist"][0] - o[0], f["wrist"][1] - o[1]), even=ARM_RIM_G)
    sw = Image.open(RAW / "wpn_training_sword.png").convert("RGBA")
    k = rig.SWORD_SCALE
    sw = sw.convert("RGBa").resize((round(sw.width * k), round(sw.height * k)), Image.LANCZOS).convert("RGBA")
    sw.save(OUT / "sword.png")
    sprites["sword"] = {"file": "sword.png", "size": [sw.width, sw.height], "pivot": [sw.width / 2, 0.79 * sw.height]}
    geo = {
        "note": "drawing canvas px, y down, angles clockwise (rig_r2 convention); hero 1028 px tall, soles at y 1283",
        "waist": waist, "neck": neck, "knot": knot,
        "S_front": G2["S"], "S_windup": G4["S"], "a1_drawn_windup": G4["a1_drawn"],
        "far_shoulder_front": G2["far_shoulder"], "far_shoulder_windup": G4["far_shoulder"],
        "L": rig.L, "UE": rig.UE, "HW0": rig.HW0, "HALF": rig.HALF, "R_B": rig.R_B, "UC1": rig.UC1,
        "fist_wrist": list(f["wrist"]), "fist_F": f["F"], "fist_blade": f["blade"], "fist_grip": list(f["grip"]),
        "fist_turn_ccw": f["turn_ccw"], "sword_tip_len": float(0.79 * sw.height - 50 * k),
        "soles_y": 1283, "head_top_y": 256,
    }
    (OUT / "rig.json").write_text(json.dumps({"sprites": sprites, "geo": geo}, indent=1))
    print("wrote", len(sprites), "sprites to", OUT)
    for n, s in sprites.items():
        print(" ", n, s["size"], [round(v, 1) for v in s["pivot"]])


if __name__ == "__main__":
    main()
