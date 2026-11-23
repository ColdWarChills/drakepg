"""Build every sprite of the hero in moments from the kept Qwen tries (art/redo/moments/moments.json) and check them.

<venv python> -I -B build.py            -> art/redo/moments/layers/<moment>/*.png, layers/manifest.json, checks.json

moments.json: {"ready": "<bare try>", ..., "pieces": {"<piece>": {"<moment>": "<edit try>" | "sheet:<edit try>"}}}
A "sheet:" edit is an edit of the 2x2 sheet of all four moments; its quarter is scaled back onto the moment canvas.
Every sprite is a full canvas PNG (fixed spot 0,0; the manifest has the trimmed box). Order: layers.ORDER.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CANVAS, FEET_Y, HERO_H, REDO, load_rgba  # noqa: E402
import grip  # noqa: E402
import bare as bare_mod  # noqa: E402
import cut  # noqa: E402
import layers as L  # noqa: E402
import place  # noqa: E402
from pieces import MOMENT_NAMES, ON_CLEAN, PIECES  # noqa: E402

OUT = REDO / "layers"


def picks():
    return json.loads((REDO / "moments.json").read_text())


def load_edit(ref: str, moment: str) -> np.ndarray:
    if ref.startswith("sheet:"):
        a = load_rgba(REDO / f"{ref[6:]}.png")
        i = MOMENT_NAMES.index(moment)
        h = a.shape[0] // 2
        q = a[(i // 2) * h:(i // 2 + 1) * h, (i % 2) * h:(i % 2 + 1) * h]
        return np.array(Image.fromarray(q).resize((CANVAS, CANVAS), Image.LANCZOS))
    return load_rgba(REDO / f"{ref}.png")


def save(a: np.ndarray, path: Path) -> list:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(a).save(path)
    ys, xs = np.nonzero(a[..., 3] > 0)
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1] if len(xs) else None


def moment_kit(m: str, p: dict):
    """a = the marker drawing (weapon spot and angle, the slot territories); with a clean drawing in moments.json
    (`clean`, clean.py) every sprite the player sees comes from it (bare.build's sandwich)."""
    a = load_rgba(REDO / f"{p[m]}.png")
    cn = p.get("clean", {}).get(m)
    c = load_rgba(REDO / f"{cn}.png") if cn else None
    parts = bare_mod.build(a, m, p[m], clean=c)
    hair = L.hair_mask(a)
    terr = L.territories(a, parts["rod"])
    terr["shirt"] = L.shirt_mask(a, parts["body"], hair)
    parts["clean_name"] = cn
    parts["clean_img"] = c
    return a, parts, hair, terr


MOMENTS3 = ["ready", "wind", "strike"]  # step A4: the follow-through is dropped (its drawings stay on disk)


def job_id(try_name: str) -> str:
    return try_name.split("@")[0]


OLD_WIND = "norm_plain_short_wind_s212"  # step A's wind-up body; today's = it with only the fist redrawn (windkeep.py)


def step_a_tries() -> set:
    return {t for by_m in json.loads((REDO / "moments_stepA.json").read_text())["pieces"].values() for t in by_m.values()}


def keep_zone(m: str, p: dict, a: np.ndarray, rod: dict, old_wind: bool = False) -> np.ndarray:
    """Where an edit made on the marker drawing may not reach (step A4 keep rule): the stick's footprint grown by 3 px
    and the region the clean drawing changed (clean.py's paste region). For a step-A wind-up edit (made on the old
    wind-up body): where the clean wind-up differs from that body, grown by 3 px."""
    import clean as clean_mod
    if old_wind:
        o = load_rgba(REDO / f"{OLD_WIND}.png")
        c = load_rgba(REDO / f"{p['clean'][m]}.png")
        pm = lambda x: x[..., :3].astype(int) * x[..., 3:].astype(int) // 255  # noqa: E731
        d = (np.abs(pm(o) - pm(c)).max(-1) > 3) | (np.abs(o[..., 3].astype(int) - c[..., 3].astype(int)) > 3)
        return ndi.binary_dilation(d, iterations=3)
    zone = ndi.binary_dilation(rod["mask"] | cut.marker_mask(a), iterations=3)
    return zone | clean_mod.region(p["clean"][m], a.shape)


def new_holes(dressed: np.ndarray, bare_stack: np.ndarray, layer_alpha: np.ndarray) -> int:
    """See-through bits the dressed figure encloses that the bare one does not, next to the piece (px)."""
    def holes(x):
        s = x[..., 3] > 128
        return ndi.binary_fill_holes(s) & ~s
    h = holes(dressed) & ~holes(bare_stack) & ndi.binary_dilation(layer_alpha, iterations=3)
    lab, n = ndi.label(h)
    if not n:
        return 0
    sizes = ndi.sum(h, lab, range(1, n + 1))
    return int(sum(s for s in sizes if s >= 6))


def specks(layer_alpha: np.ndarray, min_px: int = 24) -> int:
    """Detached bits smaller than min_px (8-connected) in a piece's layers."""
    lab, n = ndi.label(layer_alpha, structure=np.ones((3, 3), bool))
    if not n:
        return 0
    return int(sum(1 for s in ndi.sum(layer_alpha, lab, range(1, n + 1)) if s < min_px))


def main():
    import refs
    import same
    p = picks()
    manifest = {"canvas": CANVAS, "hero_h": HERO_H, "order": ["weapon(back)", "body", "shirt (hidden under chest)", "hair (hidden under helm)"] + L.ORDER,
                "moments": {}}
    checks = {}
    chosen = p.get("pieces_a4", {})
    ready_metrics = {}
    for m in MOMENTS3:
        a, parts, hair, terr = moment_kit(m, p)
        d = OUT / m
        if d.exists():  # the folder holds only what the manifest lists (layers_A2 keeps step A2's)
            for f in d.glob("*.png"):
                f.unlink()
        rod = parts["rod"]
        manifest["moments"][m] = {
            "from": p[m], "clean": parts["clean_name"], "weapon_order": parts["order"],
            # his soles on the body layer (no weapon); shift every sprite of the moment by offset_y to stand on FEET_Y
            "soles_y": grip.soles_y(parts["body"]), "offset_y": FEET_Y - grip.soles_y(parts["body"]),
            "weapon": {"art": cut.SWORD["art"], "grip_xy": [round(float(rod["center"][0]), 1),
                                                           round(float(rod["center"][1]), 1)],
                       "angle": round(rod["angle"], 1),
                       "scale_px_per_art_px": round(cut.SWORD["size"] * HERO_H * cut.BASE_CANVAS_PER_HERO / 1536, 4)},
            "sprites": {"body": save(L.body_sprite(parts["body"], hair, terr["shirt"]), d / "body.png"),
                        "shirt": save(cut.masked(parts["body"], terr["shirt"]), d / "shirt.png"),
                        "hair": save(cut.masked(parts["body"], L.hair_split(parts["body"], hair)), d / "hair.png"),
                        "fist": save(parts["fist_layer"], d / "fist.png"),
                        "weapon": save(parts["weapon"], d / "weapon.png")},
            "pieces": {}}
        bare_stack = L.dress(parts, hair, {}, terr["shirt"])
        own, other = refs.sword_arm(m, parts["clean_img"])
        # the sword hand's side: nearer its own bare skin than the other arm's, or within 25 px of it (a glove is bulkier
        # than the bare fist; at ready the other forearm lies under the sword fist, so the plain nearest-skin line
        # would cross the sword glove and show as a pale line and teeth)
        sword_side = refs.nearest_side(own, other) | ndi.binary_dilation(own, iterations=25)
        other_hand = ndi.binary_dilation(other, iterations=int(0.03 * HERO_H)) & ~sword_side
        old_wind = m == "wind"
        zone = keep_zone(m, p, a, rod, old_wind)
        face_inner = ndi.binary_erosion(L.face_mask(parts["clean_img"]), iterations=4)
        sa = step_a_tries()
        for piece, by_m in chosen.items():  # plain piece name -> {moment: try}
            if m not in by_m:
                continue
            tr = by_m[m]
            jid = job_id(tr)
            slot = PIECES[jid][0]
            on_clean = jid in ON_CLEAN
            base = parts["clean_img"] if on_clean else a
            if not on_clean and old_wind:
                if tr not in sa:
                    raise SystemExit(f"{tr}: a wind-up edit not on the clean drawing must be a step-A one")
                base = load_rgba(REDO / f"{OLD_WIND}.png")
            e, t = place.align(load_edit(tr, m), base)
            pc = place.check(base, e)
            ch = L.changed(base, e)
            tkey = "gloves" if slot == "gloves" else slot
            leak_share = float((ch & ~terr[tkey]).sum() / max(1, ch.sum()))
            t2 = dict(terr, fist_copy=parts["fist"], sword_side=sword_side, other_hand=other_hand) if on_clean else terr
            lay = L.cut_piece(base, e, slot, t2, hair)
            hide = "gloves_near" in lay and "fist_copy" in t2
            names = {}
            alpha = np.zeros(a.shape[:2], bool)
            for k, v in lay.items():
                names[k] = {"file": f"{piece}__{k}.png", "box": save(v, d / f"{piece}__{k}.png")}
                if k == "gloves_near" and hide:
                    names[k]["hides_fist"] = True  # this near glove hides the fist copy (it carries its footprint)
                alpha |= v[..., 3] > 0
            # one piece on == its edit? compared where neither the rod/weapon/fist sit
            dressed = L.dress(parts, hair, lay, terr["shirt"], hide_fist=hide)
            skip = (parts["weapon"][..., 3] > 0) | parts["fist"] | ndi.binary_dilation(
                cut.green_mask(e) | rod["mask"], iterations=6)
            both = ((dressed[..., 3] > 128) | (e[..., 3] > 128)) & ~skip
            diff = np.abs(dressed.astype(int) - e.astype(int)).max(-1)
            eq = {"mean_abs": round(float(diff[both].mean()), 2), "share_over_60": round(float((diff[both] > 60).mean()), 4)}
            manifest["moments"][m]["pieces"][piece] = names
            row ={"edit": tr, "base": "clean" if on_clean else ("old wind-up body" if old_wind else "marker"),
                   "align": {k: round(v, 3) for k, v in t.items()}, "place": pc,
                   "leak_outside_slot": round(leak_share, 3), "equals_edit": eq}
            if not on_clean:  # kept from the marker drawing: must stay clear of the stick and the clean region (0)
                row["keep_zone_px"] = int((alpha & zone).sum())
                row["keep"] = "pass" if row["keep_zone_px"] == 0 else "FAIL: reaches the stick or the redrawn fist"
            boxes = {k: v["box"] for k, v in names.items()}
            if "gloves_far" in boxes:
                b = boxes["gloves_far"]
                row["far_box"] = None if b is None else [b[2] - b[0], b[3] - b[1]]
            if slot == "helm":  # the face comes from the bare drawing: changed share of the inner face in the edit
                raw = L.raw_changed(base, e) & terr["helm"]
                # the middle of the face (eyes, nose, mouth): 25 px in from its edge, out of a hood rim's reach
                fi = ndi.binary_erosion(L.face_mask(base), iterations=25)
                row["face_changed"] = round(float((raw & fi).sum() / max(1, fi.sum())), 3)
                row["face_islands_dropped"] = int(L.face_islands(base, raw).sum())
                row["face"] = "redrawn: re-roll" if row["face_changed"] > 0.08 else "kept"
            row["new_holes_px"] = new_holes(dressed, bare_stack, alpha)
            # see-through where the edit is solid, next to the piece (an open notch, not only enclosed holes)
            st = (e[..., 3] > 200) & (dressed[..., 3] < 128) & ndi.binary_dilation(alpha, iterations=4) & ~skip
            row["see_through_px"] = int(ndi.binary_opening(st, iterations=1).sum())
            row["specks"] = specks(alpha)
            mt = same.metrics(same.merged(d, piece), plate=(piece == "scale_mail"))
            row["metrics"] = mt
            if m == "ready":
                ready_metrics[piece] = mt
            elif piece in ready_metrics:
                row["vs_ready"] = same.compare(mt, ready_metrics[piece])
            checks[f"{piece}@{m}"] = row
            print(piece, m, json.dumps({k: v for k, v in row.items() if k not in ("place", "align")}), flush=True)
    manifest["note_A4"] = ("step A4: three moments (ready, wind, strike); every piece under its plain name, made on the "
                           "clean drawing unless its check says base 'marker'; a near glove with hides_fist carries "
                           "the whole gloved fist")
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1))
    (REDO / "checks.json").write_text(json.dumps(checks, indent=1))


if __name__ == "__main__":
    main()
