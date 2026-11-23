"""Round 4 check sheet: the turned wind-up torso (parts_r4) against round 2's wind-up and today's hero.
<venv python> -I -B r4_turned.py [a1 b turn]
-> art/tests/hero_joints/r4/turned.png  (game size: (a) round 2 wind-up | (b) turned torso wind-up | (c) in the game now;
                                         under it the near shoulder of (a) and (b) at 3x)
   art/tests/hero_joints/r4/flip.png + flip.gif  (ready -> wind-up at the same place, game size and 2x on the waist/neck)
and prints the flip numbers (collar, vest sides above the belt, head) between the ready and the wind-up frame."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import numpy as np
from PIL import Image, ImageDraw

import rig_r2 as rig
import r2_stills
from jcommon import RAW, REDO, TESTS, fit

OUT = TESTS / "r4"
OUT.mkdir(parents=True, exist_ok=True)
G4 = json.loads((REDO / "parts_r4.json").read_text())
KK = 480 / 1028                      # game size on a 1440p screen (as r2_stills)
ARGS = [float(a) for a in sys.argv[1:4]] if len(sys.argv) >= 4 else None
SHADE = float(sys.argv[4]) if len(sys.argv) >= 5 else -1.0
TAG = sys.argv[5] if len(sys.argv) >= 6 else ""          # output name suffix (angle tests: turned_<tag>.png)
BODY = ("lean", "head", "dy", "sq")    # round 2's wind-up body moves, given to (b) too so (a) and (b) compare alike


_RIM = rig.rim_ring
_ARM_A = [None]


def rim_with_armhole(alpha):
    """rig_r2 leaves the armhole layer out of the rim's outline (it was off in round 2); here the drawn shoulder is
    part of the outline, so the rim runs on along it."""
    if _ARM_A[0] is not None:
        alpha = _ARM_A[0] + alpha * (1 - _ARM_A[0])
    return _RIM(alpha)


def render4(fr, p, pose):
    mats, _ = fr.body_mats(pose)
    _ARM_A[0] = rig.warp(*p.layers["armhole"], mats["armhole"], (fr.w, fr.h))[..., 3]
    rig.rim_ring = rim_with_armhole
    try:
        return fr.render(pose)
    finally:
        rig.rim_ring, _ARM_A[0] = _RIM, None


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 10 + 7 * len(text), 20], fill=(255, 255, 255))
    d.text((5, 4), text, fill=(0, 0, 0))
    return im


def on_bg(px, bg):
    out = px[..., :3] + bg * (1 - px[..., 3:4])
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))


def game(im, fr, cx=760):
    im = im.resize((round(im.width * KK), round(im.height * KK)), Image.LANCZOS)
    c = (cx - fr.ox) * KK
    return im.crop((int(c - 260), 0, int(c + 200), im.height))


def zoom(im, fr, centre, half=95, k=3, canvas=True):
    """centre in drawing-canvas px (canvas=True) or frame px."""
    x, y = (centre[0] - fr.ox, centre[1] - fr.oy) if canvas else centre
    return im.crop((int(x - half), int(y - half), int(x + half), int(y + half))).resize((2 * half * k, 2 * half * k),
                                                                                     Image.NEAREST)


def row(tiles, gap=6):
    W = sum(t.width for t in tiles) + gap * (len(tiles) - 1)
    s = Image.new("RGB", (W, max(t.height for t in tiles)), (255, 255, 255))
    x = 0
    for t in tiles:
        s.paste(t, (x, 0))
        x += t.width + gap
    return s


def col(tiles, gap=6):
    H = sum(t.height for t in tiles) + gap * (len(tiles) - 1)
    s = Image.new("RGB", (max(t.width for t in tiles), H), (255, 255, 255))
    y = 0
    for t in tiles:
        s.paste(t, (0, y))
        y += t.height + gap
    return s


def edges_at(alpha, y):
    xs = np.nonzero(alpha[int(y)] > 0.5)[0]
    return (int(xs.min()), int(xs.max())) if len(xs) else None


def main():
    import swing_r2 as sw
    # ---- round 2 (old torso): ready and wind-up, exactly as r2_stills
    p2 = rig.Parts()
    fr2 = rig.Frame(p2)
    bg = sw.background(fr2)
    ready = r2_stills.pose_B(r2_stills.STILLS["ready"])
    wind2 = r2_stills.pose_B(r2_stills.STILLS["wind-up"])
    px_ready, inf_ready = fr2.render(ready)
    px_w2, inf_w2 = fr2.render(wind2)
    # ---- round 4: the turned torso; the body transforms zeroed (the drawing carries the lean); arm along the drawn root
    S_old = rig.S0.copy()
    rig.S0 = np.array(G4["S"], float)
    rig.ARMHOLE = True
    # the sleeve's hard shadow band on its underside (light from his upper back): with the arm raised over his head
    # the sleeve is turned about half a turn from how it hangs, so the band changes side (the drawn root has it below)
    rig.SHADE_SIDE = SHADE
    p4 = rig.Parts(REDO / "parts_r4")
    fr4 = rig.Frame(p4)
    a1, b, turn = ARGS if ARGS else (G4["a1_drawn"], -62.0, 62.0)
    wind4 = dict(a1=a1, b=b, turn=turn)                       # the flip frame: no body moves, only the swap
    wind4b = dict(wind4, **{k: wind2[k] for k in BODY if k in wind2})
    # the drawn root turns with the torso: the painted upper arm keeps the same angle to it (a1 is a screen angle)
    wind4b["a1"] = a1 + wind4b.get("lean", 0.0)
    px_w4, inf_w4 = render4(fr4, p4, wind4)
    px_w4b, inf_w4b = render4(fr4, p4, wind4b)
    rig.S0, rig.ARMHOLE, rig.SHADE_SIDE = S_old, False, 1.0
    print("wind-up r2: a1 %.1f b %.1f | r4: a1 %.1f b %.1f turn %.1f flipped %d tip %s" % (
        wind2["a1"], wind2["b"], a1, b, turn, inf_w4["flipped"], np.round(inf_w4["tip"])))

    im_ready, im_w2, im_w4, im_w4b = on_bg(px_ready, bg), on_bg(px_w2, bg), on_bg(px_w4, bg), on_bg(px_w4b, bg)
    print("(b) body moves from round 2's wind-up:", {k: wind4b.get(k) for k in BODY})
    # ---- today's hero at the same height
    old = Image.open(RAW / "char_hero_bare.png").convert("RGBA")
    ob = old.getchannel("A").point(lambda v: 255 if v >= 10 else 0).getbbox()
    old = old.crop(ob)
    ko = 480 / old.height
    old = old.resize((round(old.width * ko), round(old.height * ko)), Image.LANCZOS)
    g_bg = Image.fromarray((bg * 255).astype(np.uint8)).resize((round(fr2.w * KK), round(fr2.h * KK)), Image.LANCZOS)
    oc = g_bg.crop((0, 0, 330, g_bg.height)).convert("RGBA")
    oc.alpha_composite(old, (int(165 - old.width / 2), int((290 - fr2.oy) * KK)))
    top = row([label(game(im_w2, fr2), "(a) round 2 wind-up"), label(game(im_w4b, fr4), "(b) turned torso wind-up"),
               label(oc.convert("RGB"), "(c) in the game now")])
    zc2 = inf_w2["S"] + np.array([-20.0, -15.0])
    zc4 = inf_w4b["S"] + np.array([-20.0, -15.0])
    zs = row([label(zoom(im_w2, fr2, zc2, canvas=False), "(a) shoulder 3x"),
              label(zoom(im_w4b, fr4, zc4, canvas=False), "(b) shoulder 3x")])
    zs = zs.resize((top.width, round(zs.height * top.width / zs.width)), Image.LANCZOS) if zs.width > top.width else zs
    fit(col([top, zs]), 1600, 2.0).save(OUT / f"turned{TAG}.png")
    if TAG:
        print("saved", OUT / f"turned{TAG}.png")
        return

    # ---- flip: ready (round 2 parts) -> wind-up (round 4 parts), same place
    g_ready, g_w4 = game(im_ready, fr2), game(im_w4, fr4)
    waist = np.array(G4["waist"], float)
    neck = np.array(G4["neck"], float)
    z_r = [zoom(im_ready, fr2, waist + [0, -40], 110, 2), zoom(im_ready, fr2, neck + [0, -40], 110, 2)]
    z_w = [zoom(im_w4, fr4, waist + [0, -40], 110, 2), zoom(im_w4, fr4, neck + [0, -40], 110, 2)]
    # guide lines at the same canvas rows/columns on both frames: belt top, neck, head top
    sheet = col([row([label(g_ready, "ready (front torso)"), label(g_w4, "wind-up (turned torso)")]),
                 row([label(z_r[0], "ready waist 2x"), label(z_w[0], "wind-up waist 2x")]),
                 row([label(z_r[1], "ready neck 2x"), label(z_w[1], "wind-up neck 2x")])])
    fit(sheet, 1600, 2.2).save(OUT / "flip.png")
    frames = [g_ready] * 1 + [g_w4] * 1
    frames[0].save(OUT / "flip.gif", save_all=True, append_images=frames[1:], duration=[700, 700], loop=0)

    # ---- flip numbers on the pieces as placed (canvas px): vest sides above the belt, collar box, head
    yb = G4["belt_top_mid"]
    def lay(fr, p, pose, k):
        mats, _ = fr.body_mats(pose)
        return rig.warp(*p.layers[k], mats[k], (fr.w, fr.h))
    t_r = lay(fr2, p2, ready, "torso")
    t_w = lay(fr4, p4, wind4, "torso")
    import cut_r4
    def vest_sides(t, y):
        a = cut_r4.vest_alpha(np.asarray(rig.to_image(t)).astype(np.float32))
        e = edges_at(a / 255, y - fr2.oy)
        return (e[0] + fr2.ox, e[1] + fr2.ox) if e else None
    for y in (yb - 25, yb - 10, yb - 4):
        print("vest sides at canvas y %d: ready %s  wind-up %s" % (y, vest_sides(t_r, y), vest_sides(t_w, y)))
    def collar(t):
        a = np.asarray(rig.to_image(t)).astype(np.float32)
        R, Gc, B, A = [a[..., i] for i in range(4)]
        m = (A > 128) & (R > 200) & (Gc > 180) & (B > 120)
        m[int(520 - fr2.oy):] = False        # the collar only: rows above 520, columns 690..830 (no sleeve cream)
        m[:, :690 - fr2.ox] = False
        m[:, 830 - fr2.ox:] = False
        ys, xs = np.nonzero(m)
        return [int(xs.min() + fr2.ox), int(ys.min() + fr2.oy), int(xs.max() + fr2.ox), int(ys.max() + fr2.oy)]
    print("collar box (canvas): ready", collar(t_r), "wind-up", collar(t_w))
    h_r = lay(fr2, p2, ready, "head")
    h_w = lay(fr4, p4, wind4, "head")
    print("head piece identical in both frames:", bool(np.abs(h_r - h_w).max() < 1e-3))
    print("saved", OUT / "turned.png", OUT / "flip.png", OUT / "flip.gif")


if __name__ == "__main__":
    main()
