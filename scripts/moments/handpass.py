"""Hand pass: redraw only the sword fist of a kept pose drawing, then put it back by code.

The fist, the whole stick, the wrist and some forearm are cropped from the 1536 px drawing, enlarged 4x and edited by
Qwen ("change only the hand and the stick"), with a second picture of a right fist seen from the back: the ready
moment's own fist (kept, checked by eye), turned on its own picture so its forearm points the way this pose's does.
Only that reference picture is turned, never a sprite. The edit is shrunk back and pasted inside a mask round the old
and new fist and stick; everything else of the drawing stays as it was.

<venv python> -I -B handpass.py <job>:<seed>[,<seed>]      job = a key of JOBS -> art/redo/moments/<job>_s<seed>.png
Each run also keeps <job>_s<seed>_edit.png (the 4x edit) and is logged in runs.jsonl.
"""
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, RGBA_CLOSE, RGBA_OPEN, STYLE, Comfy, load_rgba, png_bytes  # noqa: E402
import bare  # noqa: E402
import cut  # noqa: E402

ZOOM = 4
JOBS = {
    # step A's wind-up body (forearm upright, life in the pose); its fist showed the palm side with the thumb toward
    # his front. New: the back of the hand, thumb toward his back where the long end leaves, level to the left.
    "handwind": {
        # box widened to the left after handwind_s701: its long end ran off the crop, so the ends could not be told apart
        "body": "plain_short_wind_s212", "box": 460, "shift": (-110, 60), "zoom": 3,
        "ref": "plain_short_ready_s112", "ref_box": 280, "ref_shift": (-30, 0), "ref_turn": 94, "ref_drop_below": 55,
        "words": ("his right fist, raised beside his head with the forearm standing straight up. Redraw the fist so "
                  "that we see the back of his right hand, exactly like the fist in <image2>: the flat back of the "
                  "hand, the back of the wrist and the row of four knuckles face the viewer; the insides of the "
                  "fingers and the fingertips face away and are hidden behind the stick. His thumb is at the left end "
                  "of the fist. One thin straight stick of pure bright green, a little thinner than his thumb, passes "
                  "straight through the closed fist and lies level: on the left, on the thumb side, it sticks out "
                  "long, about three times as long as the fist is wide, and ends well inside the picture; on the right, under his little finger, only a short "
                  "stub sticks out, about half as long as the fist is wide."),
    },
}
# the same with the stub coloured (after handwind_s701/702/704: back of the hand right, but the stick came out long on
# both sides, so its ends could not be told apart)
JOBS["handmwind"] = dict(JOBS["handwind"], shift=(-80, 60), words=(
    "his right fist, raised beside his head with the forearm standing straight up. Redraw the fist so that we see the "
    "back of his right hand, exactly like the fist in <image2>: the flat back of the hand, the back of the wrist and "
    "the row of four knuckles face the viewer; the insides of the fingers and the fingertips face away and are hidden "
    "behind the stick. His thumb is at the left end of the fist. One thin straight stick, a little thinner than his "
    "thumb, passes straight through the closed fist and lies level. On the left, on the thumb side, it sticks out "
    "long, about three times as long as the fist is wide, and that long end is pure bright green and ends well inside "
    "the picture. On the right, under his little finger, only a short stub sticks out, about half as long as the fist "
    "is wide, and that short stub is pure bright magenta. One single straight stick: green on the left of the fist, "
    "magenta on the right."))


def crop(a, c, box, shift):
    x0 = int(round(c[0] + shift[0] - box / 2))
    y0 = int(round(c[1] + shift[1] - box / 2))
    return (x0, y0, x0 + box, y0 + box)


def ref_picture(job) -> np.ndarray:
    """The ready fist (back of the hand, thumb by the blade) on a plain light ground, turned so its forearm points the
    way this pose's forearm does."""
    a = load_rgba(REDO / f"{job['ref']}.png")
    rod = bare.read_blade(a, job["ref"])
    c = rod["center"]
    b = crop(a, c, job["ref_box"], job["ref_shift"])
    # only the sword fist, its forearm skin and the stick (with their outline): no shirt, belt or free fist
    skin = cut.skin_mask(a)
    lab, _ = ndi.label(skin)
    ids = set(np.unique(lab[cut.fist_mask(a, rod)])) - {0}
    arm = np.isin(lab, list(ids))
    if job.get("ref_drop_below"):  # the ready pose's free fist touches the sword fist from below-right: drop it
        YY, XX = np.mgrid[0:a.shape[0], 0:a.shape[1]]
        arm &= ~((YY > c[1] + job["ref_drop_below"]) & (XX > c[0] - 20))
    keep = ndi.binary_dilation(arm | rod["mask"], iterations=4) & (a[..., 3] > 0)
    a = cut.masked(a, keep)
    im = Image.new("RGBA", (b[2] - b[0], b[3] - b[1]), (236, 232, 224, 255))
    im.alpha_composite(Image.fromarray(a).crop(b))
    im = im.rotate(job["ref_turn"], Image.BICUBIC, expand=False, fillcolor=(236, 232, 224, 255))
    return np.array(im.resize((im.width * ZOOM // 2, im.height * ZOOM // 2), Image.LANCZOS))


def prompt(job) -> str:
    return (f"{RGBA_OPEN} Edit <image1>, a close-up of a cartoon hero's arm and fist. Change only the hand and the "
            f"stick: {job['words']} Keep everything else of <image1> exactly the same: the forearm, the wrist, its "
            f"place and size, the hair and headband at the edge, the transparent background, {STYLE}. Do not copy "
            f"the ground or the angle of <image2>, only how the back of the fist looks. No sword, no blade, no "
            f"crossguard, no other object, no text. {RGBA_CLOSE}")


def paste(a: np.ndarray, b, edit: np.ndarray, old_rod: dict) -> np.ndarray:
    """The edit shrunk back into box b, taken inside a mask: a disc round the fist plus the old and new stick."""
    box = b[2] - b[0]
    small = np.array(Image.fromarray(edit).resize((box, box), Image.LANCZOS))
    small[..., 3] = np.where(small[..., 3] < 10, 0, small[..., 3])
    H, W = a.shape[:2]
    new = np.zeros_like(a)
    new[b[1]:b[3], b[0]:b[2]] = small
    inbox = np.zeros((H, W), bool)
    inbox[b[1] + 2:b[3] - 2, b[0] + 2:b[2] - 2] = True
    cx, cy = old_rod["center"]
    YY, XX = np.mgrid[0:H, 0:W]
    disc = (XX - cx) ** 2 + (YY - cy) ** 2 < (0.9 * max(old_rod["gap"], 60)) ** 2
    m = disc | ndi.binary_dilation(old_rod["mask"] | cut.marker_mask(new), iterations=8)
    m &= inbox
    soft = ndi.gaussian_filter(m.astype(float), 2.0)[..., None]
    out = a.astype(float) * (1 - soft) + new.astype(float) * soft
    return np.clip(out + 0.5, 0, 255).astype(np.uint8)


def main():
    c = Comfy()
    c.check()
    for arg in sys.argv[1:]:
        name, seeds = arg.split(":")
        job = JOBS[name]
        a = load_rgba(REDO / f"{job['body']}.png")
        rod = bare.read_blade(a, job["body"])
        b = crop(a, rod["center"], job["box"], job["shift"])
        im = Image.fromarray(a).crop(b)
        z = job.get("zoom", ZOOM)
        big = np.array(im.resize((im.width * z, im.height * z), Image.LANCZOS))
        big[big[..., 3] == 0, :3] = 0
        ref = ref_picture(job)
        Image.fromarray(big).save(REDO / f"{name}_in.png")
        Image.fromarray(ref).save(REDO / f"{name}_ref.png")
        up = [c.upload(f"moments_{name}_in.png", png_bytes(big)), c.upload(f"moments_{name}_ref.png", png_bytes(ref))]
        p = prompt(job)
        for s in [int(x) for x in seeds.split(",")]:
            out = REDO / f"{name}_s{s}.png"
            if out.exists():
                continue
            t = time.time()
            data = c.generate(p, up, s)
            sec = round(time.time() - t)
            (REDO / f"{name}_s{s}_edit.png").write_bytes(data)
            edit = load_rgba(REDO / f"{name}_s{s}_edit.png")
            Image.fromarray(paste(a, b, edit, rod)).save(out)
            with open(REDO / "runs.jsonl", "a") as f:
                f.write(json.dumps({"file": out.name, "sec": sec, "utc": datetime.now(timezone.utc).isoformat(
                    timespec="seconds"), "prompt": p, "body": job["body"], "box": b}) + "\n")
            print("QWEN", out.name, sec, "s", flush=True)


if __name__ == "__main__":
    main()
