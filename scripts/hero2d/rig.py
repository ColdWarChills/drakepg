"""The cutout hero (hero2d): put the drawn pieces together from joint angles or hand/foot targets.

Coordinates: y down, angles in degrees, 0 = screen right, 90 = straight down (so a positive turn is clockwise).
Every piece is a drawn PNG cut from one Qwen drawing (art/redo/hero2d/pieces/), with a pivot (the joint at its parent
end) and a tip (the joint at its child end); pieces.json holds them, the caps and the layer order.

Fixed rules:
- the elbow folds the forearm toward his front: forearm = upper arm - flex (flex 0..150)
- the knee folds the shin toward his back: shin = thigh + flex
- the blade leaves the fist on the thumb side, on the side the elbow folds toward: blade = forearm - wrist, wrist
  (the angle between forearm and blade) kept in WRIST_RANGE; the fist turns with the sword about the wrist
- the sword is drawn under the near fist (the back of the near hand faces us)
- the pale rim is one pass over the finished figure (outer silhouette only)
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
KIT = ROOT / "art/redo/hero2d/r2"      # step 1b (step 1 kit: art/redo/hero2d)
TESTS = ROOT / "art/tests/hero2d/r2"
LINE = np.array([0x2a, 0x1a, 0x12], np.float32) / 255
RIM = np.array([0xf9, 0xde, 0x7a], np.float32) / 255   # measured on char_hero_bare_sword_ready (median rim colour)
RIM_720 = 1.4            # rim width in 1280x720 pixels on top and back (drawn: p90 depth 6 px at 1034 px tall)
SHOULDER_LIFT = 16.0     # kit px the near shoulder rises with the arm raised above the horizontal
PAD_FOLLOW = 0.35        # armor shoulder plates turn this part of the upper arm's turn (they sit on the shoulder)
FAR_PAD_FOLLOW = 0.0     # the far plate is seen edge-on: turned, it reads as a fin, so it stays on the shoulder
WRIST_RANGE = (30, 115)  # blade vs forearm: 30 = tilted 60 toward the forearm line, 115 = 25 back toward the elbow


def d2r(a):
    return a * math.pi / 180


def vec(a, l=1.0):
    return np.array([math.cos(d2r(a)) * l, math.sin(d2r(a)) * l])


def ang(v):
    return math.degrees(math.atan2(v[1], v[0]))


def load_rgba(p: Path) -> np.ndarray:
    """Premultiplied float RGBA."""
    a = np.asarray(Image.open(p).convert("RGBA"), np.float32) / 255
    a[..., :3] *= a[..., 3:4]
    return a


class Kit:
    def __init__(self, path: Path = KIT / "pieces.json"):
        self.meta = json.loads(path.read_text())
        self.dir = path.parent
        self.img = {k: load_rgba(self.dir / v["file"]) for k, v in self.meta["pieces"].items()}
        self.p = self.meta["pieces"]
        self.height = self.meta["hero_height_px"]

    def length(self, k):
        v = self.p[k]
        return float(np.hypot(v["tip"][0] - v["pivot"][0], v["tip"][1] - v["pivot"][1]))

    def rest(self, k):
        v = self.p[k]
        return ang(np.subtract(v["tip"], v["pivot"]))

    def point(self, k, name, at, a):
        """World position of a named point of piece k placed with its pivot at `at` and bone angle a."""
        v = self.p[k]
        return xform(v["pivot"], at, a - self.rest(k))(np.array(v["points"][name], float))


def xform(pivot, at, rot):
    c, s = math.cos(d2r(rot)), math.sin(d2r(rot))
    R = np.array([[c, -s], [s, c]])
    return lambda p: R @ (np.asarray(p, float) - pivot) + at


def two_bone(root, target, l1, l2, bend: int):
    """Angles (a1, a2) of a two-bone chain from root reaching toward target; bend=+1 puts the joint on the clockwise
    side of the root->target line (screen, y down). Out of reach: straight toward the target."""
    d = np.subtract(target, root)
    dist = float(np.hypot(*d))
    dist = min(max(dist, abs(l1 - l2) + 1e-3), l1 + l2 - 1e-3)
    base = ang(d)
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    a = math.degrees(math.acos(max(-1, min(1, cos_a))))
    a1 = base - bend * a
    joint = np.asarray(root, float) + vec(a1, l1)
    a2 = ang(np.asarray(root, float) + vec(base, dist) - joint)
    return a1, a2


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = np.zeros((h, w, 4), np.float32)

    def over(self, layer):
        a = layer[..., 3:4]
        self.px = layer + self.px * (1 - a)

    def put(self, img, pivot, at, rot, under=False):
        """Rotate premultiplied img by rot degrees (clockwise on screen) about pivot and move pivot to `at`."""
        c, s = math.cos(d2r(rot)), math.sin(d2r(rot))
        M = np.array([[c, -s, 0], [s, c, 0]], np.float32)
        M[:, 2] = np.asarray(at, np.float32) - M[:, :2] @ np.asarray(pivot, np.float32)
        layer = cv2.warpAffine(img, M, (self.w, self.h), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT)
        layer = np.clip(layer, 0, 1)
        layer[..., :3] = np.minimum(layer[..., :3], layer[..., 3:4])
        if under:
            a = self.px[..., 3:4]
            self.px = self.px + layer * (1 - a)
        else:
            self.over(layer)
        return layer

    def disc(self, centre, r, rgb):
        yy, xx = np.mgrid[0:self.h, 0:self.w]
        d = np.hypot(xx - centre[0], yy - centre[1])
        a = np.clip(r - d + 0.5, 0, 1)[..., None].astype(np.float32)
        layer = np.concatenate([np.broadcast_to(rgb, a.shape[:2] + (3,)) * a, a], -1)
        self.over(layer)


def rim(px: np.ndarray, width: float) -> np.ndarray:
    """The pale warm rim: grow the figure's alpha by `width` px (sub-pixel ok) and put the colour under it. Like the
    drawn hero's, it is full on top and on the sides and fades out on edges that face down (measured by edge
    direction on char_hero_bare_sword_ready)."""
    a = px[..., 3]
    inside = (a > 0.5).astype(np.uint8)
    dist = cv2.distanceTransform(1 - inside, cv2.DIST_L2, 5)
    ab = cv2.GaussianBlur(a, (0, 0), max(2.0, width))
    gx = cv2.Sobel(ab, cv2.CV_32F, 1, 0)
    gy = cv2.Sobel(ab, cv2.CV_32F, 0, 1)
    n = np.hypot(gx, gy) + 1e-6
    down = -gy / n                       # 1 where the edge faces straight down (y down), -1 straight up
    wmap = width * np.clip(0.8 - 0.7 * down, 0.15, 1.0)
    ring = np.clip(wmap + 0.5 - dist, 0, 1) * (1 - a)
    out = px.copy()
    out[..., :3] += RIM * ring[..., None]
    out[..., 3] += ring
    return out


def to_image(px: np.ndarray, height_px: float | None, kit_height: float) -> Image.Image:
    a = px[..., 3:4]
    rgb = np.where(a > 1e-4, px[..., :3] / np.maximum(a, 1e-4), 0)
    im = Image.fromarray((np.concatenate([np.clip(rgb, 0, 1), a], -1) * 255 + 0.5).astype(np.uint8), "RGBA")
    if height_px:
        f = height_px / kit_height
        im = im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)
    return im


def draw_near_arm(cv: Canvas, kit: Kit, shoulder, a_upper, flex, wrist=95.0, sword=True, parts=None):
    """The sword arm from the shoulder: upper arm angle, elbow fold (0..150, toward his front), wrist = angle between
    forearm and blade (kept in WRIST_RANGE). Layers: sleeve, forearm, cuff band, sword, fist."""
    assert -5 <= flex <= 150, flex   # the cutting pose itself is straight within a few degrees
    wrist = min(max(wrist, WRIST_RANGE[0]), WRIST_RANGE[1])
    a_fore = a_upper - flex          # the forearm folds toward his front (counter-clockwise on screen)
    a_blade = a_fore - wrist         # the blade on the same side as the fold: the thumb side of the fist
    # rule check: the blade is on the side the elbow folds toward (same rotational sign as flex)
    assert ((a_fore - a_blade) % 360) <= 180, (a_fore, a_blade)
    shoulder = np.asarray(shoulder, float)
    # the shoulder rises with the arm above the horizontal (a raised arm is not a stiff tube against the head)
    shoulder = shoulder + np.array([0.0, -SHOULDER_LIFT * float(np.clip((-15 - a_upper) / 60, 0, 1))])
    elbow = shoulder + vec(a_upper, kit.length("n_upper"))
    wristp = elbow + vec(a_fore, kit.length("n_fore"))
    P = kit.p
    show = (lambda k: parts is None or k in parts)
    if show("n_upper"):
        cv.put(kit.img["n_upper"], P["n_upper"]["pivot"], shoulder, a_upper - kit.rest("n_upper"))
    if "n_pad" in P and show("n_pad"):
        cv.put(kit.img["n_pad"], P["n_pad"]["pivot"], shoulder, PAD_FOLLOW * (a_upper - kit.rest("n_upper")))
    if show("n_fore"):
        cv.put(kit.img["n_fore"], P["n_fore"]["pivot"], elbow, a_fore - kit.rest("n_fore"))
    if show("n_cuff"):
        cv.put(kit.img["n_cuff"], P["n_cuff"]["pivot"], shoulder, a_upper - kit.rest("n_cuff"))
    # the fist turns with the sword about the wrist: its thumb axis (grip -> thumb) along the blade
    f = P["n_fist"]
    thumb_rest = ang(np.subtract(f["points"]["thumb"], f["points"]["grip"]))
    rot = a_blade - thumb_rest
    grip_w = xform(f["pivot"], wristp, rot)(f["points"]["grip"])
    if sword and show("sword"):
        cv.put(kit.img["sword"], P["sword"]["pivot"], grip_w, a_blade - kit.rest("sword"))
    if show("n_fist"):
        cv.put(kit.img["n_fist"], f["pivot"], wristp, rot)
    return {"elbow": elbow, "wrist": wristp, "grip": grip_w, "a_fore": a_fore, "a_blade": a_blade, "wrist_deg": wrist}


def rest_pose(kit: Kit) -> dict:
    """The cutting pose as joint angles (absolute, degrees) and the waist position."""
    J = kit.meta["joints"]
    A = lambda a, b: ang(np.subtract(J[b], J[a]))  # noqa: E731
    return {
        "waist": tuple(J["waist"]), "torso": A("waist", "neck"), "hips": kit.rest("hips"), "head": kit.rest("head"),
        "n_upper": A("n_shoulder", "n_elbow"), "n_flex": A("n_shoulder", "n_elbow") - A("n_elbow", "n_wrist"),
        "n_wrist": 95.0,
        "f_upper": A("f_shoulder", "f_elbow"), "f_flex": A("f_shoulder", "f_elbow") - A("f_elbow", "f_wrist"),
        "f_wrist": 0.0,
        "n_thigh": A("n_hip", "n_knee"), "n_knee": A("n_knee", "n_hem") - A("n_hip", "n_knee"), "n_foot": 0.0,
        "f_thigh": A("f_hip", "f_knee"), "f_knee": A("f_knee", "f_hem") - A("f_hip", "f_knee"), "f_foot": 0.0,
        "sword": True,
    }


def leg_ik(kit: Kit, pose: dict, side: str, hip, hem_target):
    """Thigh and knee from a target for the trouser hem; the knee bends forward (shin folds back)."""
    a1, a2 = two_bone(hip, hem_target, kit.length(f"{side}_thigh"), kit.length(f"{side}_shin"), bend=+1)
    pose[f"{side}_thigh"], pose[f"{side}_knee"] = a1, a2 - a1
    return pose


def arm_ik(kit: Kit, pose: dict, side: str, shoulder, wrist_target):
    """Upper arm and elbow from a wrist target; the elbow bends toward his back (the forearm folds forward)."""
    a1, a2 = two_bone(shoulder, wrist_target, kit.length(f"{side}_upper"), kit.length(f"{side}_fore"), bend=-1)
    pose[f"{side}_upper"], pose[f"{side}_flex"] = a1, a1 - a2
    return pose


def joints(kit: Kit, pose: dict) -> dict:
    """World positions of the body joints for a pose."""
    w = np.asarray(pose["waist"], float)
    out = {"waist": w}
    for k in ("neck", "n_shoulder", "f_shoulder"):
        out[k] = kit.point("torso", k, w, pose["torso"])
    for k in ("n_hip", "f_hip"):
        out[k] = kit.point("hips", k, w, pose["hips"])
    return out


def draw_hero(kit: Kit, pose: dict, size, rim_on=True, only=None):
    """The whole hero. Layers back to front: far leg (boot, shin, thigh), near leg, hips, far arm, torso, head, near
    arm (sleeve, forearm, cuff, sword, fist). Returns (premultiplied canvas, info)."""
    cv = Canvas(*size)
    P = kit.p
    j = joints(kit, pose)
    show = (lambda k: only is None or k in only)

    def put(k, at, a):
        if show(k):
            cv.put(kit.img[k], P[k]["pivot"], at, a - kit.rest(k))

    for s in ("f", "n"):
        hip = j[f"{s}_hip"]
        a_th = pose[f"{s}_thigh"]
        a_sh = a_th + pose[f"{s}_knee"]
        knee = hip + vec(a_th, kit.length(f"{s}_thigh"))
        hem = knee + vec(a_sh, kit.length(f"{s}_shin"))
        J = kit.meta["joints"]
        boot_rel = ang(np.subtract(J[f"{s}_ankle"], J[f"{s}_hem"])) - ang(np.subtract(J[f"{s}_hem"], J[f"{s}_knee"]))
        put(f"{s}_boot", hem, a_sh + boot_rel + pose[f"{s}_foot"])
        put(f"{s}_shin", knee, a_sh)
        put(f"{s}_thigh", hip, a_th)
        j[f"{s}_knee"], j[f"{s}_hem"] = knee, hem
    put("hips", j["waist"], pose["hips"])
    # far arm
    sh = j["f_shoulder"]
    a_u = pose["f_upper"]
    a_f = a_u - pose["f_flex"]
    el = sh + vec(a_u, kit.length("f_upper"))
    wr = el + vec(a_f, kit.length("f_fore"))
    put("f_upper", sh, a_u)
    put("f_fore", el, a_f)
    put("f_cuff", sh, a_u)
    J = kit.meta["joints"]
    fist_rel = kit.rest("f_fist") - ang(np.subtract(J["f_wrist"], J["f_elbow"]))
    put("f_fist", wr, a_f + fist_rel + pose["f_wrist"])
    j.update({"f_elbow": el, "f_wrist": wr})
    put("torso", j["waist"], pose["torso"])
    if "f_pad" in P:                   # armor: the far shoulder plate turns with the far upper arm, over the torso
        put("f_pad", sh, kit.rest("f_pad") + FAR_PAD_FOLLOW * (a_u - kit.rest("f_upper")))
    put("head", j["neck"], pose["head"])
    info = draw_near_arm(cv, kit, j["n_shoulder"], pose["n_upper"], pose["n_flex"], pose["n_wrist"],
                         sword=pose.get("sword", True), parts=only)
    j.update({"n_elbow": info["elbow"], "n_wrist": info["wrist"], "n_grip": info["grip"]})
    px = cv.px
    if rim_on:
        px = rim(px, RIM_720 * kit.height / 240)
    return px, {"joints": j, "arm": info}
