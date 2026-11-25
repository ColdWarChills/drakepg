"""After the strip (2026-11-25): the sample's bare skeleton | the knight as shipped | the knight with our Leather chest,
all three on one skeleton playing the knight's attack. Frames from DcClothes.Run (PrefabPath set to the bare
mannequin for "bare"), same framing as test 2: art/tests/dc/strip/bare/ and art/tests/dc/strip/clothed/.
<venv python> -I -B strip_clip.py  ->  art/tests/dc/strip/skeleton_and_clothes.mp4 (game speed twice, then quarter speed)
and frames.png (every 6th frame of the three, for stepping through)."""
import shutil
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
S = ROOT / "art/tests/dc/strip"
FFMPEG = r"C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0-full_build/bin/ffmpeg.exe"
FIGS = [(S / "bare", "bare", "The bare skeleton"),
        (S / "clothed", "shipped", "Knight = skeleton + its clothes"),
        (S / "clothed", "leather", "Same skeleton, our Leather chest")]
FRAMES = 68
LABEL_H = 60
FONT = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 30)
SMALL = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 22)


def frame(d, name, f):
    return Image.open(d / f"{name}_f{f:03d}.png").convert("RGB")


def crop_box():
    """Columns/rows any figure covers in any frame, plus a margin, so the three halves keep one framing."""
    bg = np.asarray(frame(*FIGS[0][:2], 0))[0, 0].astype(int)
    x0 = y0 = 10**9
    x1 = y1 = 0
    for d, n, _ in FIGS:
        for f in range(FRAMES):
            a = np.abs(np.asarray(frame(d, n, f)).astype(int) - bg).sum(2) > 24
            ys, xs = np.nonzero(a)
            if len(xs):
                x0, x1, y0, y1 = min(x0, xs.min()), max(x1, xs.max()), min(y0, ys.min()), max(y1, ys.max())
    m = 24
    w, h = frame(*FIGS[0][:2], 0).size
    return max(0, x0 - m), max(0, y0 - m), min(w, x1 + m), min(h, y1 + m)


def main():
    box = crop_box()
    cw, ch = box[2] - box[0], box[3] - box[1]
    cw += cw % 2
    tmp = S / "_v"
    if tmp.exists():
        shutil.rmtree(tmp)
    tmp.mkdir()
    rows = []
    for f in range(FRAMES):
        out = Image.new("RGB", (cw * 3, ch + LABEL_H), (255, 255, 255))
        d = ImageDraw.Draw(out)
        for i, (dd, n, label) in enumerate(FIGS):
            out.paste(frame(dd, n, f).crop(box), (i * cw, LABEL_H))
            d.text((i * cw + 14, 12), label, font=FONT, fill=(0, 0, 0))
            if i:
                d.line([(i * cw, 0), (i * cw, ch + LABEL_H)], fill=(255, 255, 255), width=3)
        out.save(tmp / f"g{f:03d}.png")
        if f % 6 == 0:
            rows.append((f, out))
    seq = list(range(FRAMES)) * 2 + [f for f in range(FRAMES) for _ in range(4)]
    for j, f in enumerate(seq):
        shutil.copyfile(tmp / f"g{f:03d}.png", tmp / f"v{j:04d}.png")
    mp4 = S / "skeleton_and_clothes.mp4"
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "60", "-i", str(tmp / "v%04d.png"),
                    "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", str(mp4)],
                   check=True)
    # stepping sheet: every 6th frame, half size
    sw, sh = rows[0][1].width // 2, rows[0][1].height // 2
    sheet = Image.new("RGB", (sw, sh * len(rows)), (40, 40, 40))
    for k, (f, im) in enumerate(rows):
        im = im.resize((sw, sh), Image.LANCZOS)
        ImageDraw.Draw(im).text((sw - 90, 8), f"f{f}", font=SMALL, fill=(200, 0, 0))
        sheet.paste(im, (0, k * sh))
    sheet.save(S / "frames.png")
    shutil.rmtree(tmp)
    print("crop", box, "->", mp4)


if __name__ == "__main__":
    main()
