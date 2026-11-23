"""Armor layers: an armor edit of a bare moment -> the pixels that changed, cut by code, plus the stacking of a dressed
hero. All rules are general (every later piece gets them for free); nothing is picked by hand per picture.

Per moment the bare drawing is split into plain sprites:
  body      the bare drawing with the rod taken out, minus the hair sprite
  hair      hair + headband + its tails (hidden when a helm is on: a helm layer carries every edit pixel there)
  fist      the sword fist (above the weapon)
  weapon    the weapon picture at the rod's spot and angle
Per piece and moment one layer (gloves: a far-hand and a near-hand layer). Fixed order, bottom to top:
  [weapon if 'back'] body, hair, legs, boots, chest, gloves_far, [weapon if 'front'], fist, gloves_near, helm
"""
import sys
from pathlib import Path

import numpy as np
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HERO_H  # noqa: E402
import cut  # noqa: E402
import place  # noqa: E402

ORDER = ["legs", "boots", "chest", "gloves_far", "WEAPON", "fist", "gloves_near", "helm"]
DIFF_T = 42          # a pixel changed if a channel moved more than this (after a 1.5 px blur of both)
DIFF_LOW = 16        # ... inside the slot's own bare region
MIN_BLOB = 0.00035   # changed blobs smaller than this share of the canvas are Qwen's redraw noise


def territories(bare: np.ndarray, rod: dict) -> dict:
    """Where each slot may draw on this moment, from the bare drawing's own colours."""
    c = place.classes(bare)
    head = place.head_box(c)
    H = HERO_H
    dil = lambda m, f: ndi.binary_dilation(m, iterations=max(1, int(f * H)))  # noqa: E731
    ys = np.nonzero(c["fig"])[0]
    top, bot = ys.min(), ys.max()
    low = np.zeros_like(head)
    low[int(bot - 0.33 * (bot - top)):] = True
    arms = c["skin"] & ~head
    belt = c["brown"] & ~low & ~head
    t = {
        "helm": dil(head, 0.06),
        # neck to belt, the shoulders and the upper arms (shoulder guards); never the face or the hair
        "chest": (dil(c["cream"] | belt, 0.035) | (arms & dil(c["cream"], 0.07))) & ~hair_mask(bare),
        "gloves": dil(arms, 0.03),
        "legs": dil(c["teal"] | belt, 0.025),
        "boots": dil(c["brown"] & low, 0.05),
    }
    # near arm = skin blobs on the rod fist; every other arm pixel belongs to the far arm (nearest blob wins)
    lab, n = ndi.label(arms)
    fist = cut.fist_mask(bare, rod)
    near_ids = set(np.unique(lab[fist & arms])) - {0}
    near = np.isin(lab, list(near_ids))
    far = arms & ~near
    _, (iy, ix) = ndi.distance_transform_edt(~(near | far), return_indices=True)
    t["near_side"] = near[iy, ix]
    return t


def face_hair(bare, c, head):
    face = ndi.binary_fill_holes(ndi.binary_closing(c["skin"] & head, iterations=3))
    return (face | hair_mask(bare)) & ~c["cream"]


def hair_mask(bare: np.ndarray) -> np.ndarray:
    """Hair, headband and its tails: the dark hair, red and brown pixels of the head zone (not the face skin, not the
    shirt collar), plus every red blob touching it."""
    c = place.classes(bare)
    head = place.head_box(c) & c["fig"]
    r, g, b = (bare[..., i].astype(int) for i in range(3))
    hairy = (c["dark"] | c["red"] | ((r < 120) & (g < 95) & (b < 80))) & ~c["skin"] & ~c["cream"]
    h = ndi.binary_opening(hairy & head, iterations=3)  # thin outlines (the collar, the V-neck) drop out
    h = ndi.binary_dilation(ndi.binary_closing(h, iterations=2), iterations=3) & head
    # the band's tails: every red blob near the head (a tail tip can fly loose), but not an open mouth (ringed by face)
    near_head = ndi.distance_transform_edt(~place.head_box(c)) < 0.2 * HERO_H
    tails = np.zeros_like(head)
    for t in place.red_blobs(c):
        if t["skin"] < 0.4 and (t["mask"] & near_head).any():
            tails |= t["mask"]
    lab2, _ = ndi.label(h | tails)
    top = np.nonzero(c["fig"].any(1))[0].min()
    keep = set(np.unique(lab2[top:top + 40][(h | tails)[top:top + 40]])) | set(np.unique(lab2[tails])) - {0}
    m = np.isin(lab2, list(keep - {0})) & c["fig"]
    # with the drawn rim round the hair and the tails (pale, neither skin nor shirt)
    lab3, n3 = ndi.label(c["cream"])
    shirt = lab3 == 1 + int(np.argmax(ndi.sum(c["cream"], lab3, range(1, n3 + 1)))) if n3 else c["cream"]
    return m | (ndi.binary_dilation(m, iterations=12) & c["fig"] & ~c["skin"] & ~ndi.binary_dilation(shirt, iterations=2))


def slot_region(bare: np.ndarray, slot: str) -> np.ndarray:
    """The bare drawing's own pixels of what the slot covers (the shirt for a chest piece, the trousers for legs...):
    anything that differs there at all belongs to the piece (pale steel highlights are close to cream)."""
    c = place.classes(bare)
    head = place.head_box(c)
    ys = np.nonzero(c["fig"])[0]
    low = np.zeros_like(head)
    low[int(ys.max() - 0.33 * (ys.max() - ys.min())):] = True
    reg = {"chest": c["cream"], "legs": c["teal"], "boots": c["brown"] & low, "gloves": c["skin"] & ~head,
           "helm": hair_mask(bare)}[slot]
    return ndi.binary_dilation(ndi.binary_closing(reg, iterations=3), iterations=3)


def changed(bare: np.ndarray, edit: np.ndarray, region: np.ndarray | None = None) -> np.ndarray:
    import cv2
    def pm(x):
        f = x.astype(np.float32)
        f[..., :3] *= f[..., 3:] / 255
        return cv2.GaussianBlur(f, (0, 0), 1.5)
    d = np.abs(pm(bare) - pm(edit)).max(-1)
    m = d > DIFF_T
    if region is not None:
        m |= region & (d > DIFF_LOW)
    m = ndi.binary_opening(m, iterations=2)
    lab, n = ndi.label(m)
    if n:
        sizes = ndi.sum(m, lab, range(1, n + 1))
        m = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > MIN_BLOB * m.size])
    m = ndi.binary_closing(m, iterations=4)
    m = ndi.binary_fill_holes(m)
    return ndi.binary_dilation(m, iterations=2)


def hair_split(body: np.ndarray, hair: np.ndarray) -> np.ndarray:
    """The hair sprite's mask. The hair mask's soft outer edge (thin lines next to it) belongs to the hair too, else it
    stays on the body as a ghost line under a helm: next to the hair the body keeps only what is thick (survives an
    opening), so the face and the shirt stay with the body. Body + hair still give back the bare drawing."""
    ring = ndi.binary_dilation(hair, iterations=10) & ~hair
    solid = (body[..., 3] > 0) & ~hair
    thick = ndi.binary_dilation(ndi.binary_opening(solid, iterations=4), iterations=4) & solid
    out = hair | (ring & ~thick)
    # bits of the body left detached from it (specks above the head, a loose tail tip) go with the hair too
    rest = (body[..., 3] > 40) & ~out
    lab, n = ndi.label(rest)
    if n > 1:
        sizes = ndi.sum(rest, lab, range(1, n + 1))
        main = lab == 1 + int(np.argmax(sizes))
        small = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s < 3000])
        loose = small & ~main & (ndi.distance_transform_edt(~hair) < 0.25 * HERO_H)
        out |= ndi.binary_dilation(loose, iterations=2) & (body[..., 3] > 0) & ~main
    return out


def shirt_mask(bare: np.ndarray, body: np.ndarray, hair: np.ndarray) -> np.ndarray:
    """The shirt sprite's mask: the cream shirt with its sleeves and its drawn outline and rim (not the skin, the
    trousers, the belt or the hair). Hidden when a chest piece is worn: the chest layer then carries the edit's own
    pixels over all of it, so where the armor is drawn narrower than the shirt, nothing of the shirt shows."""
    c = place.classes(bare)
    # the shirt in light and shade: what is none of the other colours (skin, trousers, belt and boots, hair, band)
    other = c["skin"] | c["teal"] | c["brown"] | c["dark"] | c["red"] | cut.green_mask(bare)
    cream = ndi.binary_closing(c["fig"] & ~other, iterations=2)
    cream = ndi.binary_opening(cream, iterations=5)  # the figure's thin pale rim drops out
    lab, n = ndi.label(cream)
    if n:
        sizes = ndi.sum(cream, lab, range(1, n + 1))
        cream = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 2000])  # shirt and sleeves
    shirt = ndi.binary_fill_holes(ndi.binary_closing(cream, iterations=5))  # its tan shading reads as skin
    m = ndi.binary_dilation(shirt, iterations=8) & (body[..., 3] > 0)
    return m & ~(c["skin"] & ~shirt) & ~c["teal"] & ~c["brown"] & ~hair_split(body, hair)


def body_sprite(body: np.ndarray, hair: np.ndarray, shirt: np.ndarray | None = None) -> np.ndarray:
    keep = ~hair_split(body, hair)
    if shirt is not None:
        keep &= ~shirt
    return cut.masked(body, keep)


def no_crumbs(layer: np.ndarray, min_px: int = 600, always: np.ndarray | None = None) -> np.ndarray:
    """Drop detached bits of a layer (ghost lines Qwen leaves where it took something away). `always`: the footprint
    of the body sprite the piece hides (hair under a helm, shirt under a chest piece); bits there are never dropped,
    or the hidden sprite would leave holes (the eyes under a helm, flecks at the belt)."""
    m = layer[..., 3] > 40
    lab, n = ndi.label(m)
    if not n:
        return layer
    sizes = ndi.sum(m, lab, range(1, n + 1))
    ids = {i + 1 for i, s in enumerate(sizes) if s >= min_px}
    if always is not None:
        ids |= set(np.unique(lab[always & m])) - {0}  # whole bits that touch it
    keep = ndi.binary_dilation(np.isin(lab, list(ids)), iterations=2)
    if always is not None:
        keep |= always
    return cut.masked(layer, keep)


def cut_piece(bare: np.ndarray, edit_aligned: np.ndarray, slot: str, terr: dict, hair: np.ndarray) -> dict:
    """-> {layer name: RGBA} for one piece on one moment. Both pictures have their rod taken out first (the edit's
    from its own green pixels), so the piece is whole where the rod crossed it."""
    def derod(a):
        gm = cut.green_mask(a)
        return cut.remove_rod(a, gm) if gm.sum() > 50 else a
    b0, e0 = derod(bare), derod(edit_aligned)
    ch = changed(b0, e0, slot_region(bare, slot))
    if slot == "helm":  # where the helm edit left the ground showing (hair it took away), keep it clean: no ghosts
        e1 = e0.copy()
        solid = ndi.binary_dilation(e1[..., 3] >= 200, iterations=2)
        hidden = hair_split(b0, hair)  # what the hair sprite hides; the helm layer carries every edit pixel there
        e1[..., 3] = np.where(hidden & ~solid, 0, e1[..., 3])  # faint ghosts of the old headband tails
        # detached bits are ghosts, unless they touch the face (eyes and brows sit in the hair footprint)
        face_bits = hidden & ndi.binary_dilation(cut.skin_mask(e1) & (e1[..., 3] > 128), iterations=6)
        # the face always comes from the bare drawing: changed() fills holes, so a hood ringing the face would take the
        # whole face; inside the face only the really changed pixels that connect to the helm stay
        inner = ndi.binary_erosion(face_mask(b0), iterations=4)
        raw = raw_changed(b0, e0) & terr["helm"]
        chh = (ch & terr["helm"] & ~inner) | (raw & inner & ~face_islands(b0, raw))
        return {"helm": despeck(no_crumbs(cut.masked(e1, chh | hidden), 3000, face_bits))}
    if slot == "gloves":
        m = ch & terr["gloves"]
        far = no_crumbs(cut.masked(e0, m & ~terr["near_side"]))
        if "fist_copy" in terr:  # step A3 (edit of the clean drawing): the near glove hides the fist copy and carries
            # every edit pixel in its footprint, so a glove drawn slimmer than the hand leaves no skin and no hole;
            # its edge inside the figure gets the fist copy's 1 px feather
            fc = terr["fist_copy"]
            # step A4: which hand a pixel belongs to = the nearest bare skin of the sword arm (cut.own_arm on the clean
            # drawing) or of the other arm; the near glove never grows across into the other hand (ready: the two
            # fists are close together)
            side = terr.get("sword_side", terr["near_side"])
            near = (m & side) | (ndi.binary_dilation(fc, iterations=1) & (e0[..., 3] > 0))
            near = ndi.binary_fill_holes(ndi.binary_closing(near, iterations=2)) & (side | fc)
            # the glove is bigger than the bare hand, so the near/far split (nearest bare skin) cuts into its edge
            # (a notch between the finger plates, a ragged cuff): changed pixels within 14 px of it are its own
            near |= m & ndi.binary_dilation(near, iterations=14) & side
            near = ndi.binary_fill_holes(ndi.binary_closing(near, iterations=2)) & (side | fc)
            # pixels the edit left as they were (the shirt by the cuff, the ground by a plate) are the body's: out of
            # the layer, so its own edge is the glove's (the body sprite under it shows the very same pixels)
            same = (np.abs(e0[..., :3].astype(int) - b0[..., :3].astype(int)).max(-1) < 24) & \
                   (np.abs(e0[..., 3].astype(int) - b0[..., 3].astype(int)) < 24)
            # step A4: only solid unchanged areas count (pixel by pixel, a brown cuff next to dark skin made a comb)
            same = ndi.binary_opening(same, iterations=2)
            near &= ~(same & ~fc)
            near = ndi.binary_opening(near, iterations=1) | (near & fc)  # 1 px spurs (never off the fist footprint)
            farm = m & ~side & ~near
            if "other_hand" in terr:  # the other hand's glove stays inside that hand's own region
                farm &= terr["other_hand"]
            far = no_crumbs(cut.masked(e0, farm))
            return {"gloves_near": despeck(soft_masked(no_crumbs(cut.masked(e0, near), always=fc), near)),
                    "gloves_far": despeck(far)}
        return {"gloves_near": no_crumbs(cut.masked(e0, m & terr["near_side"])), "gloves_far": far}
    m = ch & terr[slot]
    always = None
    if slot == "chest" and "shirt" in terr:
        m |= terr["shirt"]
        always = terr["shirt"]
    return {slot: despeck(no_crumbs(cut.masked(e0, m), always=always))}


def raw_changed(bare: np.ndarray, edit: np.ndarray) -> np.ndarray:
    """Changed pixels without closing or hole filling (changed() fills holes: a ring of change takes its inside)."""
    import cv2
    def pm(x):
        f = x.astype(np.float32)
        f[..., :3] *= f[..., 3:] / 255
        return cv2.GaussianBlur(f, (0, 0), 1.5)
    return ndi.binary_opening(np.abs(pm(bare) - pm(edit)).max(-1) > DIFF_T, iterations=1)


def face_mask(bare: np.ndarray) -> np.ndarray:
    """The bare face: the skin of the head zone, closed and filled (eyes, brows and mouth inside it)."""
    c = place.classes(bare)
    head = place.head_box(c)
    return ndi.binary_fill_holes(ndi.binary_closing(c["skin"] & head, iterations=4))


def face_islands(bare: np.ndarray, ch: np.ndarray) -> np.ndarray:
    """Changed pixels inside the face (4 px in from its edge) that do not connect to the changed pixels outside it:
    a redrawn mouth, eye or cheek, never the helm's brim (which comes in from the face's edge). Step A4 rule."""
    inner = ndi.binary_erosion(face_mask(bare), iterations=4)
    lab, n = ndi.label(ch & inner)
    if not n:
        return np.zeros_like(ch)
    outside = ndi.binary_dilation(ch & ~inner, iterations=2)
    touch = set(np.unique(lab[outside & (lab > 0)])) - {0}
    return (lab > 0) & ~np.isin(lab, list(touch))


def despeck(layer: np.ndarray, always: np.ndarray | None = None, min_px: int = 24) -> np.ndarray:
    """Step A4: detached bits of a few pixels (8-connected, so a diagonal pixel is its own bit) are dropped, unless
    they lie in the footprint of the body sprite the piece hides (a hole there would show the ground)."""
    m = layer[..., 3] > 0
    lab, n = ndi.label(m, structure=np.ones((3, 3), bool))
    if n < 2:
        return layer
    sizes = ndi.sum(m, lab, range(1, n + 1))
    small = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s < min_px])
    if always is not None:
        small &= ~always
    return cut.masked(layer, ~small)


def soft_masked(layer: np.ndarray, m: np.ndarray) -> np.ndarray:
    """The layer's last pixel row inside the figure at half alpha (no staircase where it meets the body); the outer
    contour keeps the drawing's own alpha."""
    out = layer.copy()
    keep = out[..., 3] > 0
    rim = keep & ~ndi.binary_erosion(keep, iterations=1) & (out[..., 3] >= 250)
    out[rim, 3] = 128
    return out


def dress(parts: dict, hair: np.ndarray, layers: dict, shirt: np.ndarray | None = None,
          hide_fist: bool = False) -> np.ndarray:
    """parts: bare.build() of the moment; layers: {layer name: RGBA} of the worn pieces. The hair sprite is hidden under
    a helm, the shirt sprite under a chest piece."""
    body = parts["body"]
    if "helm" in layers:
        body = cut.masked(body, ~hair_split(parts["body"], hair))
    if "chest" in layers and shirt is not None:
        body = cut.masked(body, ~shirt)
    out = np.zeros_like(body)
    if parts["order"] == "back":
        out = cut.over(out, parts["weapon"])
    out = cut.over(out, body)
    for name in ORDER:
        if name == "WEAPON":
            if parts["order"] == "front":
                out = cut.over(out, parts["weapon"])
        elif name == "fist":
            if not hide_fist:
                out = cut.over(out, parts["fist_layer"])
        elif name in layers:
            out = cut.over(out, layers[name])
    return out


def leak(layer: np.ndarray, terr: np.ndarray) -> float:
    m = layer[..., 3] > 30
    return float((m & ~terr).sum() / max(1, m.sum()))
