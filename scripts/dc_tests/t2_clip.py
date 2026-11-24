"""Dragon Crashers test 2, step 3: the clip and the 3x stills from DcClothes.Run's frames (art/tests/dc/t2/final).
<venv python> -I -B t2_clip.py
Clip: the three knights side by side, labelled, at game size (each knight 472 px head top to feet, the hero's size in a
2560x1440 fight), the attack twice at game speed then once at quarter speed (each frame shown 4 times), 60 fps:
art/tests/dc/t2/dc_t2_clothes.mp4. Stills: the torso area at 3x (real 3x renders) at clip frames 20 (wind-up hold) and
33 (the hit): art/tests/dc/t2/still_f020_3x.png and still_f033_3x.png, cropped to where the torsos differ, plus margin."""
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
T2 = ROOT / "art/tests/dc/t2"
SRC = T2 / "final"
FFMPEG = r"C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0-full_build/bin/ffmpeg.exe"
KNIGHTS = [("shipped", "As shipped"), ("scale", "Our Scale Mail (Qwen)"), ("leather", "Our Leather Armor (Qwen)")]
FRAMES = 68
LABEL_H = 64


def font(size):
    for f in ("C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/arialbd.ttf"):
        if Path(f).exists():
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def load(name):
    return np.asarray(Image.open(SRC / name).convert("RGB"))


def extent():
    """Rows/cols any knight covers in any frame (differs from the backdrop)."""
    bg = load("shipped_f000.png")[0, 0].astype(int)
    rows, cols = [10**9, 0], [10**9, 0]
    for k, _ in KNIGHTS:
        for f in range(FRAMES):
            m = np.abs(load(f"{k}_f{f:03d}.png").astype(int) - bg).sum(-1) > 6
            ys, xs = np.nonzero(m)
            rows = [min(rows[0], ys.min()), max(rows[1], ys.max())]
            cols = [min(cols[0], xs.min()), max(cols[1], xs.max())]
    return rows, cols, bg


def labelled_row(images, labels, bg, tag=None):
    w, h = images[0].width, images[0].height
    out = Image.new("RGB", (w * len(images), h + LABEL_H), tuple(int(c) for c in bg))
    d = ImageDraw.Draw(out)
    fnt = font(34)
    for i, (im, lab) in enumerate(zip(images, labels)):
        out.paste(im, (i * w, LABEL_H))
        tw = d.textlength(lab, font=fnt)
        d.text((i * w + (w - tw) / 2, 12), lab, font=fnt, fill=(245, 245, 240))
    if tag:
        d.text((16, LABEL_H + 10), tag, font=font(26), fill=(230, 230, 220))
    return out


def main():
    rows, cols, bg = extent()
    print("knight extent rows", rows, "cols", cols)
    h_img = load("shipped_f000.png").shape[0]
    w_img = load("shipped_f000.png").shape[1]
    top, bottom = max(0, rows[0] - 30), min(h_img, rows[1] + 30)
    left, right = max(0, cols[0] - 30), min(w_img, cols[1] + 30)
    if rows[0] == 0 or cols[0] == 0 or rows[1] == h_img - 1 or cols[1] == w_img - 1:
        print("WARNING: a knight touches the cell edge")
    seq = T2 / "seq"
    seq.mkdir(exist_ok=True)
    for p in seq.glob("*.png"):
        p.unlink()
    made = {}
    for f in range(FRAMES):
        ims = [Image.open(SRC / f"{k}_f{f:03d}.png").convert("RGB").crop((left, top, right, bottom)) for k, _ in KNIGHTS]
        made[("game", f)] = labelled_row(ims, [l for _, l in KNIGHTS], bg, "game speed")
        made[("slow", f)] = labelled_row(ims, [l for _, l in KNIGHTS], bg, "1/4 speed")
    order = [("game", f) for f in range(FRAMES)] * 2 + [("slow", f) for f in range(FRAMES) for _ in range(4)]
    for i, key in enumerate(order):
        made[key].save(seq / f"s{i:04d}.png")
    out = T2 / "dc_t2_clothes.mp4"
    w, h = made[("game", 0)].size
    vf = f"pad={w + w % 2}:{h + h % 2}"
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "60", "-i", str(seq / "s%04d.png"), "-vf", vf,
                    "-c:v", "libx264", "-crf", "14", "-pix_fmt", "yuv420p", str(out)], check=True)
    print("clip", out, f"{w}x{h}", len(order), "frames")

    # stills: crop to where the swapped torsos differ from the shipped one, plus margin for the joins
    for f in (20, 33):
        ims = {k: load(f"{k}_3x_f{f:03d}.png").astype(int) for k, _ in KNIGHTS}
        diff = np.zeros(ims["shipped"].shape[:2], bool)
        for k in ("scale", "leather"):
            diff |= np.abs(ims[k] - ims["shipped"]).sum(-1) > 30
        diff = ndimage.binary_opening(diff, np.ones((7, 7), bool))  # drop the thin sub-pixel edge noise on every part
        ys, xs = np.nonzero(diff)
        m = 120
        box = (max(0, xs.min() - m), max(0, ys.min() - m), min(diff.shape[1], xs.max() + m), min(diff.shape[0], ys.max() + m))
        crops = [Image.fromarray(ims[k].astype(np.uint8)).crop(box) for k, _ in KNIGHTS]
        still = labelled_row(crops, [l for _, l in KNIGHTS], bg, f"3x, clip frame {f}")
        still.save(T2 / f"still_f{f:03d}_3x.png")
        print("still", f, box, still.size)


if __name__ == "__main__":
    main()
