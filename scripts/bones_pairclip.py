# Old hand beside new, in step: pairs the frames by where the sword is (the hit pause can be a frame longer in one
# take), and crops both sides with the same window. Usage: pairclip_sync.py <old main dir> <new main dir> <out.mp4|check>
import json, os, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageFont
FF = r"C:\Users\Epic\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0-full_build\bin\ffmpeg.exe"
FONT = ImageFont.truetype("arial.ttf", 20)

def track(d): return json.load(open(os.path.join(d, "track.json"), encoding="utf-8"))["frames"]
def dist(a, b): return ((a[0]-b[0])**2 + (a[1]-b[1])**2) ** 0.5
def cost(fo, fn):
    po, pn = fo["points"], fn["points"]
    return dist(po["tip"], pn["tip"]) + dist(po["grip"], pn["grip"]) + dist(po["j_neck"], pn["j_neck"])

def pairing(to, tn):
    """For every new frame the old frame at the same moment (old index may stay, step 1 or step 2)."""
    out, j = [], 0
    for i, fn in enumerate(tn):
        if i == 0: out.append(0); continue
        cands = [c for c in (j + 1, j + 2, j) if c < len(to)] or [len(to) - 1]
        best = min(cands, key=lambda c: (round(cost(to[c], fn), 1), abs(c - (j + 1))))
        j = best; out.append(j)
    return out

def report(old, new):
    to, tn = track(old), track(new)
    m = pairing(to, tn)
    same = [cost(to[i], tn[i]) for i in range(min(len(to), len(tn)))]
    sync = [cost(to[m[i]], tn[i]) for i in range(len(tn))]
    off = [i for i in range(len(tn)) if m[i] != i]
    print("%s vs %s: frames old %d new %d" % (os.path.basename(os.path.dirname(old)), os.path.basename(os.path.dirname(new)), len(to), len(tn)))
    print("  paired by index: %d frames over 6 px apart, worst %.0f px" % (sum(1 for c in same if c > 6), max(same)))
    print("  paired in step:  %d frames over 6 px apart, worst %.0f px; old index differs on %d frames (%s)" % (
        sum(1 for c in sync if c > 6), max(sync), len(off), ("f%d..f%d" % (off[0], off[-1])) if off else "-"))
    return to, tn, m

def clip(old, new, out, repeats=3, hold=24):
    to, tn, m = report(old, new)
    n = len(tn); w, h, Z, LAB, G = 190, 150, 3, 28, 6
    def centre(i):   # one window for both sides: between the new take's neck and sword grip, smoothed
        js = range(max(0, i - 2), min(n, i + 3))
        x = sum(tn[k]["points"]["j_neck"][0] + tn[k]["points"]["hand_grip"][0] for k in js) / (2 * len(js))
        y = sum(tn[k]["points"]["j_neck"][1] + tn[k]["points"]["hand_grip"][1] for k in js) / (2 * len(js))
        return (x, y + 10)
    def crop(d, i, c):
        im = Image.open(os.path.join(d, "f%03d.jpg" % i)).convert("RGB")
        x, y = round(c[0] - w / 2), round(c[1] - h / 2)
        return im.crop((x, y, x + w, y + h)).resize((w * Z, h * Z), Image.BICUBIC)
    tmp = tempfile.mkdtemp(); k = 0
    frames = []
    for i in range(n):
        c = centre(i)
        im = Image.new("RGB", (w * Z * 2 + G, h * Z + LAB), (16, 16, 20))
        dr = ImageDraw.Draw(im)
        dr.text((8, 3), "BEFORE", font=FONT, fill=(240, 220, 120))
        dr.text((w * Z + G + 8, 3), "NOW (second try)", font=FONT, fill=(140, 230, 150))
        im.paste(crop(old, m[i], c), (0, LAB)); im.paste(crop(new, i, c), (w * Z + G, LAB))
        frames.append(im)
    for r in range(repeats):
        for im in frames + [frames[-1]] * hold:
            im.save(os.path.join(tmp, "c%04d.png" % k)); k += 1
    if os.path.exists(out): raise SystemExit(out + " exists: new names only")
    subprocess.run([FF, "-y", "-loglevel", "error", "-framerate", "30", "-i", os.path.join(tmp, "c%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", out], check=True)
    print(out, frames[0].size, "%d frames, %.1f s" % (k, k / 30.0), os.path.getsize(out) // 1024, "KB")

if __name__ == "__main__":
    if sys.argv[3] == "check": report(sys.argv[1], sys.argv[2])
    else: clip(sys.argv[1], sys.argv[2], sys.argv[3])
