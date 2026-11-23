"""Frozen design references for step A4: each piece's design is its ready layer, cut onto a plain (transparent) ground,
saved once into art/redo/moments/refs/ and never rebuilt by build.py (which empties the layers folders).

<venv python> -I -B refs.py freeze <piece> <ready edit try> [<base: clean|marker>]
    -> refs/<piece>_ready.png (the piece alone, enlarged so its long side is about 900 px)
       gloves: refs/<piece>_near.png = the sword-hand glove alone (split from the other hand by the sword arm's own
       skin, cut.own_arm), plus one copy per moment turned on its own picture so its forearm points the way the
       moment's sword forearm does (grip.ARMS): refs/<piece>_near_<moment>.png. Only this reference picture is turned,
       never a sprite.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, load_rgba  # noqa: E402

REFS = REDO / "refs"
LONG = 900
MOMENTS3 = ["ready", "wind", "strike"]


def picks():
    return json.loads((REDO / "moments.json").read_text())


def forearm(m: str) -> float:
    import bare
    import grip
    arm = grip.ARMS[bare.drawing_key(picks()[m])]
    return grip.ang((arm["wrist"][0] - arm["elbow"][0], arm["wrist"][1] - arm["elbow"][1]))


def sword_arm(m: str, img: np.ndarray | None = None):
    """(sword-arm skin, other skin outside the head) of moment m, from the clean drawing (or img)."""
    import bare
    import cut
    import place
    p = picks()
    a = load_rgba(REDO / f"{p[m]}.png")
    c = img if img is not None else load_rgba(REDO / f"{p['clean'][m]}.png")
    rod = bare.read_blade(a, p[m])
    skin = cut.skin_mask(c)
    head = place.head_box(place.classes(c))
    own = cut.own_arm(skin & ~head, rod, forearm(m))
    return own, skin & ~head & ~own


def nearest_side(own: np.ndarray, other: np.ndarray) -> np.ndarray:
    """True where the nearest bare skin pixel is the sword arm's."""
    d1 = ndi.distance_transform_edt(~own)
    d2 = ndi.distance_transform_edt(~other) if other.any() else np.full(own.shape, 1e9)
    return d1 <= d2


def alone(a: np.ndarray, m: np.ndarray) -> Image.Image:
    out = a.copy()
    out[..., 3] = np.where(m, out[..., 3], 0)
    ys, xs = np.nonzero(out[..., 3] > 40)
    out = out[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    im = Image.fromarray(out)
    k = LONG / max(im.size)
    im = im.convert("RGBa").resize((round(im.width * k), round(im.height * k)), Image.LANCZOS).convert("RGBA")
    pad = Image.new("RGBA", (im.width + 80, im.height + 80), (0, 0, 0, 0))
    pad.alpha_composite(im, (40, 40))
    return pad


def freeze(piece: str, try_name: str, base: str = "clean"):
    import layers as L
    import place
    from pieces import PIECES
    REFS.mkdir(exist_ok=True)
    p = picks()
    b = load_rgba(REDO / f"{p['clean']['ready'] if base == 'clean' else p['ready']}.png")
    e, _ = place.align(load_rgba(REDO / f"{try_name}.png"), b)
    if base != "clean":  # an edit of the marker drawing: the stick out of both, so the reference shows no stick
        import cut
        b, e = (cut.remove_rod(x, cut.marker_mask(x)) if cut.marker_mask(x).sum() > 50 else x for x in (b, e))
    slot = PIECES[piece][0]
    ch = L.changed(b, e, L.slot_region(b, slot))
    if slot != "gloves":
        lay = load_rgba(REDO / "layers" / "ready" / f"{piece}__{slot}.png") if base == "layer" else None
        im = alone(e, ch & (e[..., 3] > 0))
        im.save(REFS / f"{piece}_ready.png")
        print(REFS / f"{piece}_ready.png", im.size)
        return
    own, other = sword_arm("ready")
    near = ch & nearest_side(own, other) & (e[..., 3] > 0)
    lab, n = ndi.label(near)
    if n > 1:  # the biggest blob: the sword-hand glove (specks of the other hand's cuff drop out)
        sizes = ndi.sum(near, lab, range(1, n + 1))
        near = lab == 1 + int(np.argmax(sizes))
    near = ndi.binary_fill_holes(near)
    near = ndi.binary_opening(near, iterations=3)  # the comb left where the other fist was split off
    im = alone(e, near)
    im.save(REFS / f"{piece}_near.png")
    print(REFS / f"{piece}_near.png", im.size)
    f0 = forearm("ready")
    for m in MOMENTS3:
        t = im.rotate(forearm(m) - f0, resample=Image.BICUBIC, expand=True)  # counter-clockwise, screen degrees
        t = t.crop(t.getbbox())
        pad = Image.new("RGBA", (t.width + 80, t.height + 80), (0, 0, 0, 0))
        pad.alpha_composite(t, (40, 40))
        pad.save(REFS / f"{piece}_near_{m}.png")
        print(REFS / f"{piece}_near_{m}.png", pad.size, "turned", round(forearm(m) - f0, 1))


def from_layer(piece: str, layer_file: str):
    """The reference from a layer file that exists today (copied before build.py empties the folder)."""
    REFS.mkdir(exist_ok=True)
    a = load_rgba(Path(layer_file))
    m = a[..., 3] > 0
    if "__helm" in Path(layer_file).name:  # never a face in a helm's reference (Qwen would copy its expression)
        import cut
        m &= ~ndi.binary_dilation(cut.skin_mask(a), iterations=2)
        lab, n = ndi.label(m)
        if n > 1:  # eyes, brows and mouth left as islands inside the opening: dropped
            sizes = ndi.sum(m, lab, range(1, n + 1))
            m = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 0.05 * sizes.max()])
    im = alone(a, m)
    im.save(REFS / f"{piece}_ready.png")
    print(REFS / f"{piece}_ready.png", im.size)


if __name__ == "__main__":
    if sys.argv[1] == "freeze":
        freeze(sys.argv[2], sys.argv[3], *(sys.argv[4:5]))
    elif sys.argv[1] == "layer":
        from_layer(sys.argv[2], sys.argv[3])
