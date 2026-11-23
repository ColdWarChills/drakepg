"""The ten armor pieces and their edit prompts. Each piece is an "Edit <image1>. Change only ..." edit of a kept bare
moment (art/redo/moments/moments.json picks), or of a 2x2 sheet of all four (job <piece>@sheet). The game's icon of the
piece, alone on a plain ground, is <image2> (no second hero: a body armor's reference is the torso of its outfit
picture, cut from the belt up to the shoulders).

Slot rules (said in every prompt, so pieces never uncover each other): boots never smaller than his own boots; leg
pieces keep his trousers' outline, between belt and boot tops; body armor covers neck to belt, may hang over the hips,
leaves the forearms and hands bare; gloves may run up the forearm; helms leave the face open.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CANVAS, RAW, REDO, RGBA_CLOSE, RGBA_OPEN, STYLE, load_rgba  # noqa: E402

MOMENT_NAMES = ["ready", "wind", "strike", "follow"]

SLOT_RULE = {
    "chest": ("Change only what he wears on his upper body: over his cream shirt he now wears {what}, covering his "
              "chest and stomach from the neck down to the belt (it may hang a little over his hips). His short "
              "shirt sleeves, both bare arms and both fists stay unchanged, and the belt, trousers and boots "
              "are unchanged."),
    "helm": ("Change only what he wears on his head: he now wears {what}, sitting firmly on his head over his hair and "
             "leaving his whole face visible. The red headband and its tails are gone. His face is unchanged."),
    "gloves": ("Change only his hands: he now wears {what} on both hands, covering each fist and wrist; the cuffs may "
               "run a little up the forearm, never past the elbow. Both fists stay clenched exactly as before, "
               "the same size and in the same place; the right fist still grips the green rod exactly as before, "
               "fingers wrapped around it and the thumb closed over them."),
    "legs": ("Change only his trousers: instead of the dark teal trousers he now wears {what}, keeping exactly the "
             "shape and outline of his trousers, from the belt down to the tops of his boots. The belt, shirt and "
             "boots are unchanged."),
    "boots": ("Change only his boots: instead of his brown boots he now wears {what}. The new boots are at least as "
              "tall and as wide as his own boots, reach up to just below the knee and cover all of his own boots; "
              "the feet point the same way and stand exactly where they stood."),
}
REF = " (look at <image2> only for what the piece looks like, not for its shape on the page)"
PIECES = {  # id: (slot, words, reference image in art/raw or None)
    "scale_mail": ("chest", "exactly the scale mail of <image2>: a sleeveless shirt of small overlapping steel scales "
                            "and steel plate shoulder guards" + REF, "char_hero_scale_mail"),
    "leather_armor": ("chest", "exactly the leather armor of <image2>: a brown leather chest piece with its straps and "
                               "buckles and leather shoulder pads, without any bracers" + REF, "char_hero_leather_armor"),
    "leather_cap": ("helm", "exactly the brown leather cap of <image2>, with its short brim above his eyes and some "
                            "messy dark hair still showing under it at the sides and the back" + REF, "icon_leather_cap"),
    "scale_coif": ("helm", "exactly the scale coif of <image2>: a hood of small overlapping steel scales with a brown "
                           "leather rim, covering his hair, ears and neck and resting on the top of his shoulders, "
                           "his face showing through the opening" + REF, "icon_scale_coif"),
    "leather_gloves": ("gloves", "exactly the plain brown leather gloves of <image2>" + REF, "icon_leather_gloves"),
    "scale_gauntlets": ("gloves", "exactly the steel gauntlets of <image2>, with jointed steel plates over the fingers "
                                  "and the back of the hand and brown leather cuffs" + REF, "icon_scale_gauntlets"),
    "leather_leggings": ("legs", "leggings of warm brown leather with stitched seams down the side and a padded darker "
                                 "leather patch on each knee", None),
    "scale_leggings": ("legs", "leggings covered in small overlapping steel scales sewn over padded dark cloth, a brown "
                               "leather strap below each knee", None),
    "leather_boots": ("boots", "tall brown leather boots in exactly the tan-brown leather of <image2>, with its "
                               "criss-cross laces up the front and thick soles" + REF, "icon_leather_boots"),
    "scale_boots": ("boots", "exactly the scale boots of <image2>: tall boots covered in small overlapping steel scales, "
                             "with brown leather soles and a brown leather rim at the top" + REF, "icon_scale_boots"),
}
# Step A3: pieces made on the clean drawing (the stick gone, the sword hand an empty closed fist; clean.py). They have
# their own ids, so their layers never mix with the old rod-shaped edits (ready_piece reads layers by id), and build.py
# cuts them against the clean drawing.
ON_CLEAN = {"scale_gauntlets_a3": "scale_gauntlets"}
PIECES.update({k: PIECES[v] for k, v in ON_CLEAN.items()})
CLEAN_HAND = ("the right fist stays an empty closed fist exactly as before, the same side of the hand toward the viewer "
              "and the thumb exactly where it was")
KEEP = ("Keep everything else of <image1> exactly the same: the same hero, the same face, hair and headband (unless "
        "said otherwise above), the same clothes, exactly the same pose with every limb, both fists, both legs and both "
        "feet in exactly the same place and at the same size, and the green rod in his right fist unchanged, held "
        "exactly as before, " + STYLE + ". No sword, no blade, no ground, no shadow, no text.")
SHEET = ("<image1> shows the same hero four times, in four poses, one in each quarter of the picture. Dress all four "
         "the same way, so the piece looks exactly the same on all four, and keep each of the four exactly in its "
         "place and pose. ")


# Step A4: every piece on all three moments, made on the clean drawing, each matched to its ready design. Ids
# <piece>_a4 (the layers and the manifest use the plain name). The second picture is the frozen design reference
# (refs.py: the piece alone, cut from its ready layer; a glove: the sword-hand glove alone, turned to the moment's
# forearm), and the words say what that ready piece actually shows (written after looking at it).
A4 = ["scale_mail", "leather_armor", "leather_cap", "scale_coif", "leather_gloves", "scale_gauntlets",
      "leather_leggings", "scale_leggings", "leather_boots", "scale_boots"]
ON_CLEAN.update({f"{p}_a4": p for p in A4})
PIECES.update({f"{p}_a4": PIECES[p] for p in A4})
DESIGN = {
    "scale_gauntlets": (
        "steel gauntlets on both hands: each gauntlet is mostly plain mid-grey steel with thin dark outlines; a "
        "steel bracer of three overlapping curved bands covers the forearm from the wrist about halfway to the elbow "
        "and ends in a flared brown leather cuff; the back of the hand is one big rounded steel plate with a single "
        "small round rivet; each finger is covered by its own small rounded steel plates and the thumb by a curved "
        "steel plate; brown leather shows in strips between the plates, on the palm side and at the cuff. The steel "
        "is the same mid grey as in <image2>, with soft lighter highlights: not bright silver, not dark, never brown "
        "leather over the back of the hand; the same gauntlet on both hands"),
    "scale_mail": (
        "scale mail: a sleeveless shirt of dark gunmetal steel scales, each scale rounded at the bottom with a bright "
        "silver highlight, overlapping in rows from the neck down to the belt; a dark leather collar round the neck; "
        "over his right shoulder, the shoulder nearer to us, a big curved dark steel shoulder plate of two "
        "overlapping lames with one small rivet; over his other shoulder a smaller dark steel plate; his short cream "
        "shirt sleeves show under the shoulder plates. Dark steel with silver highlights, the same steel and the same "
        "scales as <image2>"),
    "leather_armor": (
        "leather armor: a dark chocolate-brown leather chest piece from the neck down to the belt, with leather straps "
        "and small brass buckles across the chest, a rounded dark brown leather shoulder pad with a brass buckle over "
        "his right shoulder, the shoulder nearer to us, and a smaller pad on his other shoulder; his short cream shirt "
        "sleeves show under the pads; no bracers. The same dark brown leather and brass as <image2>"),
    "leather_gloves": (
        "plain dark brown leather gloves on both hands: smooth leather without plates or studs, a few darker seam "
        "lines on the back of the hand, the cuff reaching a little up the forearm. The same dark brown as <image2>"),
    "leather_leggings": (
        "leggings of warm orange-brown leather with a stitched seam down the outer side of each leg and a darker "
        "brown padded leather patch on each knee, the same colours as <image2>"),
    "scale_leggings": (
        "leggings covered in small overlapping dark steel scales with silver highlights over dark cloth, a brown "
        "leather strap just below each knee, the same scales and colours as <image2>"),
    "scale_coif": (
        "a scale coif: a hood of small overlapping bright silver steel scales covering his hair, ears and neck and "
        "resting on the top of his shoulders, with a brown leather rim round the face opening; his whole face shows "
        "through the opening exactly as it is; the same silver scales and brown rim as <image2>"),
}
SIDE_PLAIN = {  # the same, for a glove without the steel plate (leather gloves)
    "wind": ("; on the raised right fist we see the back of the hand, the knuckles along the top of the fist, the "
             "fingertips hidden, and the thumb curls over at the LEFT end of the fist, toward his back"),
    "strike": ("; on the right fist low in front of him we see the back of the hand and the knuckles, the thumb at "
               "the front (right) end of the fist"),
    "ready": ("; on the right fist in front of his belt we see the back of the hand, the knuckles along the top, the "
              "thumb at the upper right end of the fist"),
}
SIDE = {  # which side of the sword fist faces us (grip.EYE notes, hands_table.md), said for a glove
    "wind": ("; on the raised right fist we see the back of the hand: its big steel plate with the rivet faces us, the "
             "row of knuckle plates runs along the top of the fist, the fingertips are hidden, and the thumb curls "
             "over at the LEFT end of the fist, toward his back; the bracer runs down his raised forearm to the "
             "brown cuff"),
    "strike": ("; on the right fist low in front of him we see the back of the hand: its big steel plate with the "
               "rivet faces us, the knuckle plates along its front, and the thumb at the front (right) end of the "
               "fist"),
    "ready": ("; on the right fist in front of his belt we see the back of the hand: its big steel plate with the "
              "rivet faces us, the knuckle plates along the top, the thumb at the upper right end of the fist"),
}
OTHER_FIST = {  # where his left fist is (the far arm), so the pair is said outright: s1001 gloved only the sword hand
    "wind": "his left fist, held low in front of his chest",
    "strike": "his left fist, swung back behind him at shoulder height",
    "ready": "his left fist, just below and behind his right fist",
}
PAIR = ("; they are a pair: one on his right fist and the matching one on {other}; both hands are gloved, no "
        "bare hand")
A4_REF = (" (<image2> is this same {noun} cut out of another picture of him: draw exactly this {noun}, the same "
          "design, colours and size, fitted to this pose; take only its look from <image2>, not its place or angle "
          "on the page)")


def a4_ref(piece: str, moment: str):
    """The frozen reference of a step-A4 piece at a moment -> (path, array) or (path, None) when not frozen yet."""
    from refs import REFS
    plain = ON_CLEAN[piece]
    f = REFS / (f"{plain}_near_{moment}.png" if PIECES[piece][0] == "gloves" else f"{plain}_ready.png")
    return f, (load_rgba(f) if f.exists() else None)


FAR_WORDS = {  # the other hand's glove as cut from ready's gloves_far layer (refs/<piece>_far_ready.png), looked at
    "scale_gauntlets": ("; <image3> is the matching gauntlet of his left hand, cut out of the same other picture: his "
                        "left hand wears exactly that one, a brown leather glove with a smooth steel plate on the back "
                        "of the hand, steel plates over the fingers and a brown leather cuff, the same brown leather "
                        "and the same steel as <image3>"),
}


# Local fixes (job <piece>_a4fix@<moment>): an edit of a kept A4 try (its base is the clean drawing, so build.py cuts
# the fix against the clean drawing as usual). The leather armor's wind-up drew no shoulder pad on the raised near
# shoulder twice (s804, s1031); the game's outfit art (char_hero_leather_armor) and ready have one.
ON_CLEAN.update({"leather_armor_a4fix": "leather_armor"})
PIECES["leather_armor_a4fix"] = PIECES["leather_armor"]
FIX = {
    ("leather_armor_a4fix", "wind"): (
        "leather_armor_a4@wind_s1031", "refs/leather_armor_pad.png",
        "Change only the top of his raised right shoulder: put on it the rounded dark brown leather shoulder pad of "
        "<image2>, with its brass buckle, curved over the top of the shoulder and covering the top of his short cream "
        "sleeve, fastened to the leather armor he already wears. The same dark brown leather and brass as his armor "
        "(<image2> is that pad cut out of another picture of him: take only its look, not its place or angle)."),
}
KEEP_FIX = ("Keep everything else of <image1> exactly the same: the same leather armor, the same hero, face, hair and "
            "headband, the same pose with every limb and both fists exactly in place, " + STYLE + ". No sword, no "
            "blade, no ground, no shadow, no text.")


def far_ref(piece: str):
    from refs import REFS
    f = REFS / f"{ON_CLEAN[piece]}_far_ready.png"
    return f if f.exists() and ON_CLEAN[piece] in FAR_WORDS else None


def a4_prompt(piece: str, moment: str) -> str:
    plain = ON_CLEAN[piece]
    slot = PIECES[piece][0]
    noun = ("gauntlet" if plain == "scale_gauntlets" else "glove") + " of his right hand" if slot == "gloves" else "piece"
    what = DESIGN[plain] + A4_REF.format(noun=noun)
    if slot == "gloves":
        what += PAIR.format(other=OTHER_FIST[moment])
        if far_ref(piece) and moment != "ready":
            what += FAR_WORDS[plain]
    rule = SLOT_RULE[slot].format(what=what)
    keep = KEEP.replace("and the green rod in his right fist unchanged, held exactly as before, ",
                        "his right fist an empty closed fist exactly as before, ")
    if slot == "gloves":
        rule = rule.replace(" on both hands, covering each fist", ", covering each fist").replace(
            "the right fist still grips the green rod exactly as before, fingers wrapped around it and the thumb "
            "closed over them", CLEAN_HAND + (SIDE if plain == "scale_gauntlets" else SIDE_PLAIN).get(moment, ""))
    assert "rod" not in rule + keep, rule + keep
    return f"{RGBA_OPEN} Edit <image1>. {rule} {keep} {RGBA_CLOSE}"


def picks() -> dict:
    return json.loads((REDO / "moments.json").read_text())


def ref_image(key: str) -> np.ndarray:
    a = load_rgba(RAW / f"{key}.png")
    if key.startswith("char_hero_"):  # torso only: from below the chin to the belt, shoulders to fists out
        a = a[440:930, 120:900]
    ys, xs = np.nonzero(a[..., 3] > 40)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    im = Image.fromarray(a)
    im.thumbnail((640, 640), Image.LANCZOS)
    pad = Image.new("RGBA", (im.width + 64, im.height + 64), (0, 0, 0, 0))
    pad.alpha_composite(im, (32, 32))
    return np.array(pad)


def sheet_input() -> np.ndarray:
    p = picks()
    can = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    half = CANVAS // 2
    for i, m in enumerate(MOMENT_NAMES):
        im = Image.fromarray(load_rgba(REDO / f"{p[m]}.png")).resize((half, half), Image.LANCZOS)
        can.alpha_composite(im, ((i % 2) * half, (i // 2) * half))
    a = np.array(can)
    a[a[..., 3] == 0, :3] = 0
    return a


FROM_READY = (" (<image2> is this same piece cut out of another picture of him: draw exactly the same piece, the same "
              "design, colours and size, fitted to this pose; take only its look from <image2>, not its shape on the page)")


def ready_piece(piece: str):
    """The piece as cut from its kept ready edit (all its layers merged), alone on a plain ground, or None."""
    d = REDO / "layers" / "ready"
    files = sorted(d.glob(f"{piece}__*.png"))
    if not files:
        return None
    acc = None
    for f in files:
        a = load_rgba(f)
        acc = a if acc is None else np.where(a[..., 3:] > acc[..., 3:], a, acc)
    ys, xs = np.nonzero(acc[..., 3] > 40)
    acc = acc[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    pad = Image.new("RGBA", (acc.shape[1] + 64, acc.shape[0] + 64), (0, 0, 0, 0))
    pad.alpha_composite(Image.fromarray(acc), (32, 32))
    return np.array(pad)


THUMB_NOTE = {  # where the sword hand's thumb is, said outright for a glove on the clean drawing (s933/s934 moved it)
    "wind": ("; on the raised right fist the steel thumb plate curls over the top at the LEFT end of the fist, toward "
             "his back, exactly where his bare thumb was, and the four finger plates sit side by side to the right of "
             "it with the little finger at the right end; there is no thumb at the right end of that fist"),
}


def prompt(piece: str, sheet: bool = False, from_ready: bool = False, moment: str | None = None) -> str:
    slot, what, ref = PIECES[piece]
    if from_ready:
        what = what.split(" (look at <image2>")[0].replace(" of <image2>", "").replace(" in exactly the tan-brown leather", " in the tan-brown leather") + FROM_READY
    rule = SLOT_RULE[slot].format(what=what)
    keep = KEEP
    if piece in ON_CLEAN:  # no stick in the clean drawing: no rod words anywhere
        rule = rule.replace("the right fist still grips the green rod exactly as before, fingers wrapped around it and "
                            "the thumb closed over them", CLEAN_HAND + THUMB_NOTE.get(moment, ""))
        keep = keep.replace("and the green rod in his right fist unchanged, held exactly as before, ",
                            "his right fist an empty closed fist exactly as before, ")
        assert "rod" not in rule + keep, rule + keep
    return f"{RGBA_OPEN} Edit <image1>. {SHEET if sheet else ''}{rule} {keep} {RGBA_CLOSE}"


def base_of(piece: str, moment: str) -> str:
    """The drawing a piece's edit was made on: the clean drawing for step-A3 pieces, else the marker drawing."""
    p = picks()
    return p["clean"][moment] if piece in ON_CLEAN else p[moment]


LAST_REFS = []  # the second pictures of the last job, logged in runs.jsonl by qwen_moments.py


def job(name: str):
    global LAST_REFS
    piece, src = name.split("@")
    if piece.endswith("_a4"):  # step A4: the clean drawing + the frozen design reference, never the icon
        f, rp = a4_ref(piece, src)
        if rp is None:
            raise SystemExit(f"{f}: no frozen reference (refs.py freeze ...)")
        LAST_REFS = [str(f.relative_to(REDO))]
        pics = [load_rgba(REDO / f"{base_of(piece, src)}.png"), rp]
        fr = far_ref(piece) if src != "ready" else None
        if fr is not None:  # the other hand's glove as a third picture (s1013 drew it plain steel)
            LAST_REFS.append(str(fr.relative_to(REDO)))
            pics.append(load_rgba(fr))
        return a4_prompt(piece, src), pics
    if piece.endswith("_a4fix"):  # a local fix on top of a kept A4 try (FIX): its own words and picture
        base_try, ref_file, words = FIX[(piece, src)]
        LAST_REFS = [base_try, ref_file]
        return (f"{RGBA_OPEN} Edit <image1>. {words} {KEEP_FIX} {RGBA_CLOSE}",
                [load_rgba(REDO / f"{base_try}.png"), load_rgba(REDO / ref_file)])
    LAST_REFS = []
    sheet = src == "sheet"
    base = sheet_input() if sheet else load_rgba(REDO / f"{base_of(piece, src)}.png")
    rp = None if sheet or src == "ready" else ready_piece(piece)
    if rp is not None:
        return prompt(piece, False, True, src), [base, rp]
    refs = [base]
    if PIECES[piece][2]:
        refs.append(ref_image(PIECES[piece][2]))
    return prompt(piece, sheet), refs
