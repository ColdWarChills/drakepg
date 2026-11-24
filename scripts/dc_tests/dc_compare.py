"""Test 1 (2026-11-24, the designer's "motion only"): today's hero | the Dragon Crashers knight, untouched, in one fight.

python -I -B dc_compare.py <old take dir> <new take dir> <out dir>

Both take dirs hold lossless game frames pNNN.png (2560x1440, 60 fps) and track.json from BonesCapture.Take. The two
takes are lined up on the game's contact frame (the first "hitstop:on" after the press). Writes to <out dir>:
  attack_hero_knight.mp4   left today's hero, right the knight, one fixed framing for both, at game speed twice, then
                           once at quarter speed (60 fps file)
  frames_hero.png / frames_knight.png   every 3rd frame of the attack in rows, game size, for stepping through
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FFMPEG = shutil.which("ffmpeg") or str(next(Path.home().glob(
    "AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg*/ffmpeg-*/bin/ffmpeg.exe")))
FONT = ImageFont.truetype("arial.ttf", 34)
SMALL = ImageFont.truetype("arial.ttf", 22)


def load(d):
    t = json.loads((d / "track.json").read_text())
    frames = t["frames"]
    press = next(f["i"] for f in frames if any(e.startswith("press:") for e in f.get("events", [])))
    contact = next(f["i"] for f in frames if f["i"] >= press and "hitstop:on" in f.get("events", []))
    return frames, press, contact


def box_union(frames, names, lo, hi):
    x0 = y0 = 1e9
    x1 = y1 = -1e9
    for f in frames[lo:hi]:
        for n in names:
            b = f.get("boxes", {}).get(n)
            if b:
                x0, y0, x1, y1 = min(x0, b[0]), min(y0, b[1]), max(x1, b[2]), max(y1, b[3])
    return x0, y0, x1, y1


def label(im, text, font=FONT):
    d = ImageDraw.Draw(im)
    w = d.textlength(text, font=font)
    d.rectangle([0, 0, w + 20, font.size + 16], fill=(255, 255, 255))
    d.text((10, 7), text, font=font, fill=(0, 0, 0))


def main():
    old, new, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    out.mkdir(parents=True, exist_ok=True)
    fo, po, co = load(old)
    fn, pn, cn = load(new)
    shift = cn - co                      # new frame = old frame + shift
    # the knight's clip starts on the arm frame (clip_t first >= 0); it ends 68 frames (1.133 s at x1) later
    k0 = next(f["i"] for f in fn if f["values"].get("clip_t", -1) >= 0)
    k1 = next((f["i"] for f in fn if f["i"] > k0 and f["values"].get("clip_t", 0) >= 1.1333), len(fn) - 1)
    start = min(k0, po + shift) - 6     # before the old take's first frame, the hero (still idle) holds frame 0
    end = min(max(k1, po + shift + 40) + 10, len(fn) - 1, len(fo) - 1 + shift)
    # one framing for both: hero, knight and foe boxes over the attack, with a margin
    a = box_union(fo, ["hero", "foe"], max(start - shift, 0), end - shift)
    b = box_union(fn, ["knight", "foe"], start, end)
    x0, y0 = min(a[0], b[0]) - 60, min(a[1], b[1]) - 60
    x1, y1 = max(a[2], b[2]) + 60, max(a[3], b[3]) + 80
    w, h = x1 - x0, y1 - y0
    if w / h < 16 / 9 * 0.5:           # keep each half no narrower than 8:9
        cx = (x0 + x1) / 2
        w = h * 8 / 9
        x0, x1 = cx - w / 2, cx + w / 2
    crop = tuple(int(round(v)) for v in (x0, y0, x1, y1))
    half_w = 960
    half_h = int(round((crop[3] - crop[1]) * half_w / (crop[2] - crop[0])))
    half_h += half_h % 2
    print(f"old press {po} contact {co}; new press {pn} contact {cn}; shift {shift}; knight clip frames {k0}..{k1}")
    print(f"frames new {start}..{end} ({end - start + 1}), crop {crop}, half {half_w}x{half_h}")

    tmp = out / "_frames"
    if tmp.exists():
        shutil.rmtree(tmp)
    tmp.mkdir()
    pairs = []
    for i, fnew in enumerate(range(start, end + 1)):
        fold = max(fnew - shift, 0)
        L = Image.open(old / f"p{fold:03d}.png").convert("RGB").crop(crop).resize((half_w, half_h), Image.LANCZOS)
        R = Image.open(new / f"p{fnew:03d}.png").convert("RGB").crop(crop).resize((half_w, half_h), Image.LANCZOS)
        pairs.append((fnew, L.copy(), R.copy()))
        label(L, "Today's hero")
        label(R, "Dragon Crashers knight, untouched")
        frame = Image.new("RGB", (half_w * 2, half_h), (0, 0, 0))
        frame.paste(L, (0, 0))
        frame.paste(R, (half_w, 0))
        d = ImageDraw.Draw(frame)
        d.line([(half_w, 0), (half_w, half_h)], fill=(255, 255, 255), width=3)
        frame.save(tmp / f"g{i:03d}.png")
    n = end - start + 1
    # game speed twice, then quarter speed (each frame 4x), one 60 fps file
    seq = list(range(n)) + list(range(n)) + [i for i in range(n) for _ in range(4)]
    for j, i in enumerate(seq):
        shutil.copyfile(tmp / f"g{i:03d}.png", tmp / f"v{j:04d}.png")
    mp4 = out / "attack_hero_knight.mp4"
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", "60", "-i", str(tmp / "v%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", str(mp4)], check=True)

    # stepping sheets: every 3rd frame, game size (the crop at 1x of the 2560 take = 2x game px), 6 per row
    step = pairs[::3]
    cw, ch = (crop[2] - crop[0]) // 2, (crop[3] - crop[1]) // 2
    for side, name in ((1, "frames_hero.png"), (2, "frames_knight.png")):
        cols = 6
        rows = (len(step) + cols - 1) // cols
        sheet = Image.new("RGB", (cw * cols, ch * rows), (40, 40, 40))
        for k, p in enumerate(step):
            im = p[side].resize((cw, ch), Image.LANCZOS)
            ImageDraw.Draw(im).text((8, 8), f"f{p[0] - cn:+d}", font=SMALL, fill=(255, 255, 0))
            sheet.paste(im, ((k % cols) * cw, (k // cols) * ch))
        sheet.save(out / name)
    shutil.rmtree(tmp)
    print("wrote", mp4)


if __name__ == "__main__":
    main()
