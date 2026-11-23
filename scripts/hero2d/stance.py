"""The ready stance, fitted to the drawn one (art/raw/char_hero_bare_sword_ready.png) by joint spots read off that
drawing (look.py grid, 702x1046 picture, hero 1034 px tall), scaled to the kit, with the training sword in his right
(near) fist at the game's sword_ready angle (classes.ts: angle 45 = halfway between up and forward).
Sheets (art/tests/hero2d/r2): stance_2x.png (Fen Road 2560x1440, hero 480 px), game_1280.png, judge_3x.png (beside
the drawn stance at the same height + crops), stance_overlay.png (the fit check). <venv python> -I -B stance.py"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, ROOT, TESTS, Kit, ang, arm_ik, draw_hero, joints, rest_pose, to_image, two_bone, vec, xform  # noqa: E402

# joint spots on the drawn ready stance (702x1046 px), read by eye
DRAWN = {
    "waist": (361, 478), "neck": (370, 228),
    "n_shoulder": (257, 286), "n_elbow": (232, 430), "n_wrist": (310, 491), "n_fist": (349, 530),
    "f_shoulder": (413, 308), "f_elbow": (449, 430), "f_wrist": (515, 536), "f_fist": (538, 563),
    "n_hip": (317, 543), "n_knee": (231, 714), "n_hem": (203, 807), "n_ankle": (131, 964),
    "f_hip": (396, 543), "f_knee": (489, 714), "f_hem": (489, 793), "f_ankle": (496, 950),
}
DRAWN_H = 1034
BLADE = -45.0          # sword_ready: angle 45 from straight up toward forward


def stance_pose(kit: Kit, waist=(900.0, 800.0)):
    K = kit.height / DRAWN_H
    D = {k: np.asarray(v, float) for k, v in DRAWN.items()}
    at = lambda k: np.asarray(waist) + K * (D[k] - D["waist"])  # noqa: E731
    J = kit.meta["joints"]
    p = rest_pose(kit)
    p["waist"] = tuple(waist)
    p["torso"] = ang(D["neck"] - D["waist"])
    j = joints(kit, p)
    # the sword fist: its grip goes on the drawn fist's middle (with the blade at BLADE the grip sits at a fixed
    # offset from the wrist, since the fist turns with the sword)
    f = kit.p["n_fist"]
    thumb_rest = ang(np.subtract(f["points"]["thumb"], f["points"]["grip"]))
    grip_off = xform(f["pivot"], (0.0, 0.0), BLADE - thumb_rest)(f["points"]["grip"])
    arm_ik(kit, p, "n", j["n_shoulder"], at("n_fist") - grip_off)
    arm_ik(kit, p, "f", j["f_shoulder"], at("f_wrist"))
    a_fore = p["n_upper"] - p["n_flex"]
    p["n_wrist"] = a_fore - BLADE
    for s in ("n", "f"):
        hip = j[f"{s}_hip"]
        lt, ls = kit.length(f"{s}_thigh"), kit.length(f"{s}_shin")
        lb = float(np.hypot(*np.subtract(J[f"{s}_ankle"], J[f"{s}_hem"])))
        # reach the drawn ankle with thigh + (shin and boot as one); then lay the boot along the drawn hem->ankle
        a1, a2 = two_bone(hip, at(f"{s}_ankle"), lt, ls + lb, bend=+1)
        p[f"{s}_thigh"], p[f"{s}_knee"] = a1, a2 - a1
        boot_rest = ang(np.subtract(J[f"{s}_ankle"], J[f"{s}_hem"])) - ang(np.subtract(J[f"{s}_hem"], J[f"{s}_knee"]))
        p[f"{s}_foot"] = ang(D[f"{s}_ankle"] - D[f"{s}_hem"]) - (a2 + boot_rest)
    return p


def figure(kit, pose, height, size=(1900, 1800)):
    px, info = draw_hero(kit, pose, size)
    body, _ = draw_hero(kit, {**pose, "sword": False}, size, rim_on=False)
    a = body[..., 3] > 0.5
    ys, xs = np.nonzero(a)
    bb = (xs.min(), ys.min(), xs.max(), ys.max())
    f = height / (bb[3] - bb[1])
    im = to_image(px, None, kit.height)
    im = im.resize((round(im.width * f), round(im.height * f)), Image.LANCZOS)
    return im, f, bb, info


def font(n):
    try:
        return ImageFont.truetype("arial.ttf", n)
    except OSError:
        return ImageFont.load_default()


def on_road(figs, W=2560, Hh=1440, hero_h=480, feet_y=1020):
    """figs: list of (pose, kit, centre x). One frame of Fen Road with each hero standing on the same ground line."""
    frame = Image.open(ROOT / "art/raw/bg_fen_road.png").convert("RGBA").resize((W, Hh), Image.LANCZOS)
    for pose, kit, cx in figs:
        im, f, bb, _ = figure(kit, pose, hero_h)
        frame.alpha_composite(im, (round(cx - (bb[0] + bb[2]) / 2 * f), round(feet_y - bb[3] * f)))
    return frame


def judge(kit, pose, out, ref_png="char_hero_bare_sword_ready.png"):
    ref = Image.open(ROOT / "art/raw" / ref_png).convert("RGBA")
    ref = ref.crop(ref.getbbox())
    ref = ref.resize((round(ref.width * 720 / ref.height), 720), Image.LANCZOS)
    im, f, bb, info = figure(kit, pose, 720)
    body = im.crop((round(bb[0] * f) - 40, round(bb[1] * f) - 270, round(bb[2] * f) + 270, round(bb[3] * f) + 10))
    TOPH = max(ref.height, body.height) + 50
    sheet = Image.new("RGB", (max(ref.width + body.width + 120, 1300), TOPH + 3 * 340 + 20), (236, 230, 216))
    d = ImageDraw.Draw(sheet)
    sheet.paste(ref, (20, TOPH - ref.height - 10), ref)
    sheet.paste(body, (ref.width + 80, TOPH - body.height - 10), body)
    d.text((20, 10), "drawn (game now), 720 px", fill=(40, 30, 20), font=font(20))
    d.text((ref.width + 80, 10), "cutout hero (new), 720 px", fill=(40, 30, 20), font=font(20))
    J = info["joints"]
    ups = pose["f_upper"] - pose["f_flex"]
    crops = [("face and hair", J["neck"] + np.array([20, -120])), ("sword hand", J["n_grip"]),
             ("far fist", J["f_wrist"] + vec(ups, 30)), ("near elbow and cuff", J["n_elbow"]),
             ("near shoulder", J["n_shoulder"] + np.array([0, 20])), ("belt and crotch", J["waist"] + np.array([0, 70])),
             ("near knee", J["n_knee"]), ("far knee", J["f_knee"]),
             ("near boot", J["n_hem"] + vec(pose["n_thigh"] + pose["n_knee"], 100)),
             ("far boot", J["f_hem"] + vec(pose["f_thigh"] + pose["f_knee"], 100) + np.array([30, 0]))]
    for i, (name, c) in enumerate(crops):
        cx, cy = c[0] * f, c[1] * f
        cr = im.crop((round(cx - 110), round(cy - 110), round(cx + 110), round(cy + 110))).resize((300, 300), Image.LANCZOS)
        tile = Image.new("RGBA", cr.size, (236, 230, 216, 255))
        tile.alpha_composite(cr)
        x, y = 20 + (i % 4) * 320, TOPH + (i // 4) * 340
        sheet.paste(tile.convert("RGB"), (x, y + 24))
        d.text((x, y), name + " (x1.36)", fill=(40, 30, 20), font=font(20))
    sheet.save(out)


if __name__ == "__main__":
    kit = Kit()
    pose = stance_pose(kit)
    print({k: (round(v, 1) if isinstance(v, float) else v) for k, v in pose.items()})
    (TESTS / "stance_legs.json").write_text(json.dumps({s: [pose[f"{s}_thigh"], pose[f"{s}_knee"]] for s in "nf"}))
    im, f, bb, info = figure(kit, pose, 960)
    im.save(KIT / "stance_master.png")
    print("wrist deg", info["arm"]["wrist_deg"], "blade", info["arm"]["a_blade"])
    on_road([(pose, kit, 674)]).convert("RGB").save(TESTS / "stance_2x.png")
    on_road([(pose, kit, 337)], 1280, 720, 240, 510).convert("RGB").save(TESTS / "game_1280.png")

    # fit check: ours (half see-through) over the drawn one at the same scale, aligned at the waist spot
    ref = Image.open(ROOT / "art/raw/char_hero_bare_sword_ready.png").convert("RGBA")
    rb = ref.getbbox()
    s_ref = 720 / (rb[3] - rb[1])
    ref = ref.crop(rb).resize((round((rb[2] - rb[0]) * s_ref), 720), Image.LANCZOS)
    px, info = draw_hero(kit, {**pose, "sword": False}, (1900, 1800), rim_on=False)
    s_kit = s_ref * DRAWN_H / kit.height
    ours = to_image(px, None, kit.height)
    ours = ours.resize((round(ours.width * s_kit), round(ours.height * s_kit)), Image.LANCZOS)
    kw = np.asarray(pose["waist"]) * s_kit
    dw = (np.asarray(DRAWN["waist"]) - (rb[0], rb[1])) * s_ref
    ov = Image.new("RGBA", (1000, 840), (255, 255, 255, 255))
    ox, oy = 250, 60
    ov.alpha_composite(ref, (ox, oy))
    a = np.asarray(ours).copy()
    a[..., 3] = (a[..., 3] * 0.55).astype(np.uint8)
    ov.alpha_composite(Image.fromarray(a), (round(ox + dw[0] - kw[0]), round(oy + dw[1] - kw[1])))
    d = ImageDraw.Draw(ov)
    for k, v in DRAWN.items():
        x, y = ox + (v[0] - rb[0]) * s_ref, oy + (v[1] - rb[1]) * s_ref
        d.ellipse([x - 3, y - 3, x + 3, y + 3], outline=(220, 0, 0))
    ov.convert("RGB").save(TESTS / "stance_overlay.png")
    judge(kit, pose, TESTS / "judge_3x.png")
    print("ok")
