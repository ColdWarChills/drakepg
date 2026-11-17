"""The judging kit: pictures of a recorded move, so an attack can be judged without watching video.

Input: a take folder written by the Unity side (unity/Assets/Editor/BonesCapture.cs): frames f000.jpg ... and
track.json (fps, picture size, per frame the tracked points, boxes, values and events), and for the bones a clean
pass (the actor alone on magenta) in a second folder.

HOW TO (record a move and get every picture):

1. In Unity, play mode, through the MCP's execute_code (C# 6), one call per take; wait until BonesCapture.Busy is
   false before the next:

     // the hero on bones, staged by the test (LayeredHero.Folder set first):
     Drakepg.Test.SkeletonSwingDemo.Stage(); var hero = Drakepg.EditorTools.BonesCapture.SwingHero();
     Drakepg.EditorTools.BonesCapture.Reset(); Drakepg.EditorTools.BonesCapture.Bones(hero);
     Drakepg.EditorTools.BonesCapture.At("wind@0.6 strike@0.96 contact@1.04 back@1.40 ready@1.76");
     Drakepg.Test.SkeletonSwingDemo.Play();
     Drakepg.EditorTools.BonesCapture.Take(DIR + "/main", 210, 60, 1920);   // 60 fps: a strike is 2-3 frames at 30
     // then the same setup again with .Clean(DIR + "/clean", hero, 210, 60, 1920) instead of .Take(...): the actor
     // alone on magenta, everything else hidden and put back after (the move must replay the same; game time is exact)

     // a real fight (today's drawn hero): Drive.Fight(...), wait for the hero to walk in, then
     Drakepg.EditorTools.BonesCapture.Reset(); Drakepg.EditorTools.BonesCapture.Battle();
     Drakepg.EditorTools.BonesCapture.Take(DIR, 130, 60, 1280, "0@0.1");   // "1@0.05 0@0.1": Skills, first skill

   Don't recompile scripts in play mode (the domain reload drops the game); close menus first (Drive.Close()).
   Track points: tip, grip, hand_grip (the hand's own grip point), j_<joint> for each bone; drawn hero: tip, grip,
   tip_game, feet. Events: At() schedule, pose:<drawn pose>, hitstop:on/off, face:..., press:<button>.

2. Then (one line):
     python scripts/bones_sheets.py all DIR/main --clean DIR/clean --rig unity/Bones/hero/rig.json
            --boxes hero --out art/tests/sheets/bones/<name>
   For a fight: python scripts/bones_sheets.py all DIR --follow hero --end <last frame> --out ...
   (strip, keys, clip + half-speed clip, timeline, tip and, with a clean pass, check). Each command also runs on its
   own; see --help. Beside the reference:  side "A|name|frames" "B|name|frames" --out x.webp ; reel a.mp4|name ...
   Captioned panels (a tour, a before/after): panels "DIR|@mark:swap|caption" "DIR2|12|caption" --out x.webp
   The new bones core (Drakepg.Bones, unity/Bones/HOWTO.md): Drakepg.Test.BoneStage.Open(...), then
   BonesCapture.BoneCore(Drakepg.Test.BoneStage.Hero.gameObject) instead of Bones(hero).

Caveat: the game's hit-stop ends on real time, so a recording shows it shorter than play (2-3 frames instead of
70-120 ms); everything else runs on game time and is frame-exact.

Frames stay in art/redo/bones_frames/ (git-ignored); only the small pictures go to art/tests/sheets/bones/.
Pillow + numpy + ffmpeg only.
"""
import argparse
import json
import os
import subprocess
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw, ImageFont

FFMPEG = os.environ.get("FFMPEG") or (
    r"C:\Users\Epic\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-9.0-full_build\bin\ffmpeg.exe")
KEY = (255, 0, 255)
# Joint points on the silhouette's edge by design, with their own lowest cover (the neck: ~79% in every frame tonight)
EDGE_JOINTS = {"j_neck": 0.65}
# ffmpeg's drawtext needs a font file on Windows (no fontconfig there)
FONT_FILE = next((f for f in (r"C:/Windows/Fonts/arialbd.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf")
                  if os.path.exists(f)), "").replace(":", "\\:")


# --- reading a take -------------------------------------------------------------------------------------------------

class Take:
    def __init__(self, folder):
        self.dir = folder
        self.files = sorted(f for f in os.listdir(folder) if f.startswith("f") and f.endswith(".jpg"))
        path = os.path.join(folder, "track.json")
        self.track = json.load(open(path)) if os.path.exists(path) else None
        self.fps = self.track["fps"] if self.track else 30
        self.frames = self.track["frames"] if self.track else [{"i": i} for i in range(len(self.files))]
        self.n = min(len(self.files), len(self.frames))
        self.size = Image.open(self.path(0)).size if self.files else (0, 0)
        self.follow = None  # (box name, w, h): crop around that box per frame instead of one crop
        self._settle()

    def _settle(self):
        """A fight's drawn hero: add "settled" on the first frame after it is back in its rest pose where it is home
        again (same place, tilt and squash as on frame 0) and stays so for 6 frames."""
        if not self.track or not self.frames or "hero_x" not in self.frames[0].get("values", {}):
            return
        v0 = self.frames[0]["values"]
        moved = next((i for i in range(1, self.n) if any(e.startswith("pose:") and e != "pose:rest" for e in self.events(i))), None)
        if moved is None:
            return
        rest = next((i for i in range(moved, self.n) if "pose:rest" in self.events(i)), moved)

        def home(i):
            v = self.frames[i]["values"]
            return (abs(v.get("hero_x", 0) - v0.get("hero_x", 0)) < 0.5 and abs(v.get("hero_angle", 0)) < 0.2
                    and abs(v.get("hero_sx", 1) - 1) < 0.006 and abs(v.get("hero_sy", 1) - 1) < 0.006)
        for i in range(rest, self.n - 6):
            if all(home(k) for k in range(i, i + 6)):
                self.frames[i].setdefault("events", []).append("settled")
                return

    def path(self, i):
        return os.path.join(self.dir, self.files[i])

    def image(self, i):
        return Image.open(self.path(i)).convert("RGB")

    def ms(self, i):
        return round(i * 1000 / self.fps)

    def points(self, i):
        return self.frames[i].get("points", {}) if i < len(self.frames) else {}

    def events(self, i):
        return self.frames[i].get("events", []) if i < len(self.frames) else []

    def value(self, i, name, default=None):
        return self.frames[i].get("values", {}).get(name, default) if i < len(self.frames) else default

    def event_frames(self, quiet=("hitstop:off", "state:")):
        """[(frame, event)] for the named moments, skipping first-frame watch values and noise."""
        out = []
        for i in range(self.n):
            for e in self.events(i):
                if i == 0 and ":" in e and not e.startswith("press"):
                    continue
                if any(e.startswith(q) for q in quiet):
                    continue
                if e.startswith("foe_hp:"):
                    continue
                out.append((i, e))
        return out

    def box_at(self, i, name, w, y0, y1):
        """Following: the crop moves across with the box (its middle), never up and down (the ground stays put)."""
        b = None
        for k in range(i, -1, -1):
            b = self.frames[k].get("boxes", {}).get(name)
            if b:
                break
        cx = (b[0] + b[2]) / 2
        W = self.size[0]
        x0 = int(min(max(0, cx - w / 2), W - w))
        return (x0, y0, x0 + w, y1)

    def cropped(self, i, crop):
        """Frame i cut to `crop`, or around the followed box when following."""
        if self.follow:
            return self.image(i).crop(self.box_at(i, *self.follow))
        return self.image(i).crop(crop)

    def follow_box(self, name="hero", margin=40, frames=None):
        """Follow `name`'s box frame by frame, at the size of its biggest box + margin (the hero runs across a fight)."""
        frames = frames if frames is not None else range(self.n)
        w, y0, y1 = 0, 1e9, -1e9
        for i in frames:
            b = self.frames[i].get("boxes", {}).get(name)
            if b:
                w, y0, y1 = max(w, b[2] - b[0]), min(y0, b[1]), max(y1, b[3])
        if not w:
            return False
        self.follow = (name, int(min(self.size[0], w + 2 * margin)), int(max(0, y0 - margin)), int(min(self.size[1], y1 + margin)))
        return True

    def crop(self, margin=40, boxes=None, frames=None):
        """One crop for the whole take: the union of the tracked boxes (hero, foe ...) over all frames, plus margin;
        without boxes, the part of the picture that changes."""
        frames = frames if frames is not None else range(self.n)
        x0 = y0 = 1e9
        x1 = y1 = -1e9
        found = False
        for i in frames:
            for name, b in self.frames[i].get("boxes", {}).items() if self.track else []:
                if boxes and name not in boxes:
                    continue
                found = True
                x0, y0, x1, y1 = min(x0, b[0]), min(y0, b[1]), max(x1, b[2]), max(y1, b[3])
        w, h = self.size
        if not found:
            a = np.asarray(self.image(0).convert("L"), dtype=np.int16)
            diff = np.zeros_like(a)
            for i in range(1, self.n, max(1, self.n // 20)):
                diff = np.maximum(diff, np.abs(np.asarray(self.image(i).convert("L"), dtype=np.int16) - a))
            ys, xs = np.nonzero(diff > 30)
            if len(xs) == 0:
                return (0, 0, w, h)
            x0, y0, x1, y1 = xs.min(), ys.min(), xs.max(), ys.max()
        return (int(max(0, x0 - margin)), int(max(0, y0 - margin)), int(min(w, x1 + margin)), int(min(h, y1 + margin)))


def parse_crop(s):
    return tuple(int(v) for v in s.split(",")) if s else None


def font(size):
    for name in ("arialbd.ttf", "DejaVuSans-Bold.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def label(img, text, size=None, at=(4, 4), fill=(255, 255, 255), bg=(0, 0, 0)):
    d = ImageDraw.Draw(img)
    f = font(size or max(12, img.width // 14))
    box = d.textbbox(at, text, font=f)
    d.rectangle((box[0] - 3, box[1] - 2, box[2] + 3, box[3] + 2), fill=bg)
    d.text(at, text, font=f, fill=fill)
    return img


def frame_list(take, every, frames, start=0, end=None):
    if frames:
        out = []
        for part in frames.split(","):
            if "-" in part:
                a, b = part.split("-")
                out.extend(range(int(a), int(b) + 1))
            else:
                out.append(int(part))
        return [i for i in out if 0 <= i < take.n]
    end = take.n if end is None else min(end + 1, take.n)
    return list(range(start, end, every))


def short_events(take, i):
    keep = []
    for e in take.events(i):
        if e.startswith(("foe_hp", "state", "hitstop:off", "face")):
            continue
        keep.append(e.replace("pose:", "").replace("press:0", "press").replace("hitstop:on", "HIT"))
    return " ".join(keep)


def ensure_dir(path):
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)


def save_webp(img, path, max_kb=500):
    ensure_dir(path)
    q = 85
    while True:
        img.save(path, "WEBP", quality=q, method=6)
        if os.path.getsize(path) <= max_kb * 1024 or q <= 40:
            return path
        q -= 10


# --- strip ----------------------------------------------------------------------------------------------------------

def make_strip(take, frames, crop, cols=8, max_width=2000, title=None, mark_events=True):
    cells = []
    for i in frames:
        im = take.cropped(i, crop)
        cells.append((i, im))
    if not cells:
        raise SystemExit("no frames")
    cw, ch = cells[0][1].size
    cols = min(cols, len(cells))
    scale = min(1.0, max_width / (cw * cols))
    tw, th = max(1, int(cw * scale)), max(1, int(ch * scale))
    rows = (len(cells) + cols - 1) // cols
    head = 34 if title else 0
    sheet = Image.new("RGB", (tw * cols, th * rows + head), (24, 24, 28))
    if title:
        label(sheet, title, 20, (8, 6), bg=(24, 24, 28))
    for k, (i, im) in enumerate(cells):
        cell = im.resize((tw, th), Image.LANCZOS)
        ev = short_events(take, i) if mark_events else ""
        label(cell, f"#{i}  {take.ms(i)} ms", max(11, tw // 11))
        if ev:
            label(cell, ev, max(11, tw // 12), (4, th - max(11, tw // 12) - 10), fill=(255, 230, 120))
        sheet.paste(cell, ((k % cols) * tw, (k // cols) * th + head))
    return sheet


def cmd_strip(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    frames = frame_list(t, a.every, a.frames, a.start, a.end)
    sheet = make_strip(t, frames, crop, a.cols, a.max_width, a.title)
    print(save_webp(sheet, a.out), sheet.size, f"{os.path.getsize(a.out) // 1024} KB")


# --- keys -----------------------------------------------------------------------------------------------------------

def key_name(events):
    """One name for a frame's events: a drawn pose or a scheduled moment first, then contact (hit pause), press."""
    names = [e[5:] for e in events if e.startswith("pose:")]
    names += [e for e in events if ":" not in e]
    if any(e == "hitstop:on" for e in events):
        names.append("contact")
    names += [e.split(":")[0] for e in events if e.startswith("press")]
    names += [e.replace(":", "_") for e in events if e.startswith("face:")]
    return names[0] if names else None


def pick_keys(t, end=None):
    """Default key frames from the events: the first frame of each named moment (up to `end`)."""
    out = []
    by = {}
    for i, e in t.event_frames():
        by.setdefault(i, []).append(e)
    for i in sorted(by):
        if end is not None and i > end:
            break
        n = key_name(by[i])
        if n and not n.startswith("face"):
            out.append((i, n))
    return out


def cmd_keys(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    if a.frames:
        keys = []
        for part in a.frames.split(","):
            if "=" in part:
                n, i = part.split("=")
                keys.append((int(i), n))
            else:
                keys.append((int(part), ""))
    else:
        keys = pick_keys(t)[: a.max]
    os.makedirs(a.out, exist_ok=True)
    for i, name in keys:
        im = t.cropped(i, crop)
        label(im, f"{name or 'frame'}  #{i}  {t.ms(i)} ms", max(14, im.width // 30))
        p = os.path.join(a.out, f"key_{i:03d}_{name or 'frame'}.webp".replace(":", "_").replace(" ", "_"))
        im.save(p, "WEBP", quality=90)
        print(p, im.size)


# --- clip -----------------------------------------------------------------------------------------------------------

def run_ffmpeg(args):
    r = subprocess.run([FFMPEG, "-y", "-loglevel", "error"] + args, capture_output=True, text=True)
    if r.returncode:
        raise SystemExit("ffmpeg: " + r.stderr[-2000:])


def write_clip(t, crop, out, width=720, slow=1, frames=None, title=None):
    """Frames to a small mp4: cropped, scaled to `width`, `slow` x slower, each frame labelled with its number."""
    import tempfile
    frames = frames or list(range(t.n))
    tmp = tempfile.mkdtemp(prefix="bones_clip_")
    w = width - width % 2
    for k, i in enumerate(frames):
        im = t.cropped(i, crop)
        h = int(im.height * w / im.width)
        h -= h % 2
        im = im.resize((w, h), Image.LANCZOS)
        label(im, f"#{i} {t.ms(i)} ms" + (f"  {title}" if title else ""), max(12, w // 40))
        im.save(os.path.join(tmp, f"c{k:04d}.png"))
    ensure_dir(out)
    run_ffmpeg(["-framerate", str(t.fps / slow), "-i", os.path.join(tmp, "c%04d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p",
                "-crf", "26", "-preset", "slow", "-movflags", "+faststart", out])
    for f in os.listdir(tmp):
        os.remove(os.path.join(tmp, f))
    os.rmdir(tmp)
    return out


def cmd_clip(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    frames = frame_list(t, 1, None, a.start, a.end)
    out = write_clip(t, crop, a.out, a.width, a.slow, frames, a.title)
    print(out, f"{os.path.getsize(out) // 1024} KB")


# --- timeline -------------------------------------------------------------------------------------------------------

def motion(t, crop, step=2):
    """Per frame: mean absolute change of the picture inside the crop from the frame before (0-255)."""
    prev, out = None, [0.0]
    for i in range(t.n):
        a = np.asarray(t.image(i).crop(crop).convert("L"), dtype=np.int16)[::step, ::step]
        if prev is not None:
            out.append(float(np.abs(a - prev).mean()))
        prev = a
    return out[: t.n]


def speed(t, name):
    """Per frame: how far a tracked point moved since the frame before, in px per second."""
    out, prev = [], None
    for i in range(t.n):
        p = t.points(i).get(name)
        if p is None or prev is None:
            out.append(0.0)
        else:
            out.append(((p[0] - prev[0]) ** 2 + (p[1] - prev[1]) ** 2) ** 0.5 * t.fps)
        prev = p if p is not None else prev
    return out


def beats(t, end=None):
    """Named moments and the time between them, from the events (one per frame, up to `end`)."""
    by = {}
    for i, e in t.event_frames():
        if end is None or i <= end:
            by.setdefault(i, []).append(e)
    ev = [(i, key_name(by[i])) for i in sorted(by)]
    ev = [(i, e) for i, e in ev if e and not e.startswith("face")]
    rows = []
    for k, (i, e) in enumerate(ev):
        nxt = ev[k + 1][0] if k + 1 < len(ev) else None
        rows.append((i, e, (nxt - i) * 1000 / t.fps if nxt is not None else None))
    return rows


def make_timeline(t, crop, tip="tip", width=1600, title=None, start=0, end=None):
    end = t.n - 1 if end is None else min(end, t.n - 1)
    m = motion(t, crop)
    curves = [("picture change", m, (90, 170, 255))]
    if any(tip in t.points(i) for i in range(t.n)):
        curves.append((f"{tip} speed", speed(t, tip), (255, 120, 90)))
    root = "j_waist" if any("j_waist" in t.points(i) for i in range(t.n)) else "feet"
    if any(root in t.points(i) for i in range(t.n)):
        curves.append((f"{root} speed", speed(t, root), (120, 220, 120)))
    H, pad, top = 520, 60, 70
    img = Image.new("RGB", (width, H), (22, 22, 26))
    d = ImageDraw.Draw(img)
    f, fs = font(16), font(13)
    d.text((pad, 14), title or os.path.basename(os.path.normpath(t.dir)), font=font(20), fill=(240, 240, 240))
    x0, x1, y0, y1 = pad, width - 20, top, H - 110
    span = max(1, end - start)

    def X(i):
        return x0 + (i - start) * (x1 - x0) / span

    # hit pause: shade frames where the animation clock is slowed
    for i in range(start, end + 1):
        sc = t.value(i, "anim_scale", 1)
        if isinstance(sc, (int, float)) and sc < 0.5:
            d.rectangle((X(i) - (x1 - x0) / span / 2, y0, X(i) + (x1 - x0) / span / 2, y1), fill=(70, 40, 40))
    # grid: 100 ms ticks
    for ms in range(0, int((end - start + 1) * 1000 / t.fps) + 1, 100):
        x = X(start + ms * t.fps / 1000)
        d.line((x, y0, x, y1), fill=(40, 40, 46) if ms % 500 else (60, 60, 70))
        if ms % 500 == 0:
            d.text((x - 12, y1 + 4), f"{ms + int(start * 1000 / t.fps)}", font=fs, fill=(150, 150, 160))
    d.text((x1 - 40, y1 + 20), "ms", font=fs, fill=(150, 150, 160))
    for k, (name, c, col) in enumerate(curves):
        seg = c[start: end + 1]
        mx = max(seg) or 1
        pts = [(X(start + j), y1 - (v / mx) * (y1 - y0) * 0.95) for j, v in enumerate(seg)]
        d.line(pts, fill=col, width=2)
        unit = "" if name == "picture change" else " px/s"
        d.text((pad + k * 300, 42), f"{name} (top {mx:.0f}{unit})", font=f, fill=col)
    # events
    rows = [r for r in beats(t, end) if start <= r[0]]
    for k, (i, e, dur) in enumerate(rows):
        x = X(i)
        d.line((x, y0, x, y1), fill=(255, 220, 120), width=1)
        txt = e.replace("pose:", "").replace("press:0", "press")
        d.text((x + 3, y0 + 4 + (k % 6) * 16), txt, font=fs, fill=(255, 220, 120))
    # beat lengths
    lines = [f"{e.replace('pose:', '')} @{i * 1000 // t.fps}ms" + (f" -> {dur:.0f} ms" if dur else "") for i, e, dur in rows]
    y = y1 + 30
    line = ""
    for s in lines:
        if len(line) + len(s) > width // 8:
            d.text((pad, y), line, font=fs, fill=(220, 220, 220))
            y += 18
            line = ""
        line += s + "   "
    d.text((pad, y), line, font=fs, fill=(220, 220, 220))
    stop = [i for i in range(start, end + 1) if isinstance(t.value(i, "anim_scale", 1), (int, float)) and t.value(i, "anim_scale", 1) < 0.5]
    if stop:
        d.text((width - 560, 42), f"hit pause (red): {len(stop)} frames = {len(stop) * 1000 / t.fps:.0f} ms recorded "
               "(ends on real time: longer in play)", font=f, fill=(255, 140, 140))
    return img


def cmd_timeline(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    img = make_timeline(t, crop, a.tip, a.width, a.title, a.start, a.end)
    print(save_webp(img, a.out), img.size)
    for i, e, dur in beats(t):
        print(f"  #{i:3d} {t.ms(i):5d} ms  {e}" + (f"   -> {dur:.0f} ms to next" if dur else ""))


# --- tip ------------------------------------------------------------------------------------------------------------

def make_tip(t, crop, at, tip="tip", hand="hand_grip", start=0, end=None, scale=1.0):
    end = t.n - 1 if end is None else min(end, t.n - 1)
    im = t.image(at).crop(crop)
    if scale != 1:
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
    over = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    ox, oy = crop[0], crop[1]

    def P(p):
        return ((p[0] - ox) * scale, (p[1] - oy) * scale)

    for name, col in ((hand, (60, 200, 255, 255)), (tip, (255, 60, 60, 255))):
        if not any(name in t.points(i) for i in range(start, end + 1)) and name == hand:
            name = "grip"
        pts = [(i, t.points(i)[name]) for i in range(start, end + 1) if name in t.points(i)]
        if len(pts) < 2:
            continue
        d.line([P(p) for _, p in pts], fill=col[:3] + (170,), width=2)
        for i, p in pts:
            x, y = P(p)
            r = 4 if i == at else 2.5
            stop = isinstance(t.value(i, "anim_scale", 1), (int, float)) and t.value(i, "anim_scale", 1) < 0.5
            d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, 255) if stop else col, outline=(0, 0, 0, 200))
        # every 5th dot numbered, so the order reads
        for i, p in pts:
            if (i - start) % 5 == 0 and name == tip:
                x, y = P(p)
                d.text((x + 4, y - 6), str(i), font=font(11), fill=(255, 255, 255, 230))
    out = Image.alpha_composite(im.convert("RGBA"), over).convert("RGB")
    label(out, f"tip path (red, one dot per frame {start}-{end}; white = hit pause), grip hand (blue); over frame #{at}", 14)
    return out


def cmd_tip(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    at = a.at
    if at is None:
        hits = [i for i, e in t.event_frames() if e in ("contact", "hitstop:on")]
        at = hits[0] if hits else t.n // 2
    img = make_tip(t, crop, at, a.tip, a.hand, a.start, a.end, a.scale)
    print(save_webp(img, a.out), img.size, "contact frame", at)


# --- check ----------------------------------------------------------------------------------------------------------

def key_mask(img, tol=90):
    """Covered pixels of a clean frame: far from the magenta key (JPG, so a tolerance)."""
    a = np.asarray(img, dtype=np.int16)
    dist = np.abs(a - np.array(KEY, dtype=np.int16)).sum(axis=2)
    return dist > tol


def blobs(mask, min_area):
    """Separate 4-connected blobs of the mask (no scipy): [(area, (x0,y0,x1,y1))], biggest first."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
    ys, xs = np.nonzero(mask)
    for y, x in zip(ys, xs):
        if seen[y, x]:
            continue
        q = deque([(y, x)])
        seen[y, x] = True
        area, bx0, by0, bx1, by1 = 0, x, y, x, y
        while q:
            cy, cx = q.popleft()
            area += 1
            bx0, bx1, by0, by1 = min(bx0, cx), max(bx1, cx), min(by0, cy), max(by1, cy)
            for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    q.append((ny, nx))
        out.append((area, (bx0, by0, bx1, by1)))
    out.sort(reverse=True)
    return [b for b in out if b[0] >= min_area]


def rig_radii(rig_path, take, share):
    """Per joint point (j_<joint>): `share` of the rig's joint radius, in picture px (the scale read off frame 0 as
    the picture distance between two tracked joints over their rig distance)."""
    rig = json.load(open(rig_path))
    J = rig["joints"]
    p = take.points(0)
    import math
    best = None
    for a, b in (("n_shoulder", "n_elbow"), ("n_hip", "n_knee"), ("f_hip", "f_knee")):
        if "j_" + a in p and "j_" + b in p:
            best = math.dist(p["j_" + a], p["j_" + b]) / math.dist(J[a], J[b])
            break
    if not best:
        return {}
    return {"j_" + k: max(3, v * best * share) for k, v in rig.get("radius", {}).items()}


def check(clean, main=None, step=2, speck=0.004, radius=10, cover_min=0.85, grip_max=3.0, joints_prefix="j_",
          grip=("grip", "hand_grip"), radii=None):
    """Every frame: (a) blobs in the clean silhouette, (b) cover around each joint, (c) weapon grip to hand grip."""
    radii = radii or {}
    rows = []
    for i in range(clean.n):
        img = clean.image(i)
        mask = key_mask(img)
        small = mask[::step, ::step]
        total = int(small.sum())
        bl = blobs(small, max(20, int(total * speck)))
        pts = clean.points(i)
        cover = {}
        hh, ww = mask.shape
        for name, p in pts.items():
            if not name.startswith(joints_prefix):
                continue
            rad = int(round(radii.get(name, radius)))
            yy, xx = np.ogrid[-rad:rad + 1, -rad:rad + 1]
            disc = (xx * xx + yy * yy) <= rad * rad
            x, y = int(round(p[0])), int(round(p[1]))
            if x - rad < 0 or y - rad < 0 or x + rad >= ww or y + rad >= hh:
                continue
            win = mask[y - rad: y + rad + 1, x - rad: x + rad + 1]
            cover[name] = float(win[disc].mean())
        gp = (main or clean).points(i)
        d = None
        if grip[0] in gp and grip[1] in gp:
            a, b = gp[grip[0]], gp[grip[1]]
            d = ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5
        rows.append({"i": i, "blobs": len(bl), "blob_areas": [b[0] * step * step for b in bl], "blob_boxes": [[v * step for v in b[1]] for b in bl],
                     "cover": cover, "grip_px": d})
    bad_a = [r["i"] for r in rows if r["blobs"] > 1]
    # A fixed bar for every joint, so a flaw that lasts the whole take (the ready stance) is seen too; joints whose
    # point sits on the outline by design have their own lower bar (EDGE_JOINTS: the neck point is at the collar).
    usual = {}
    for k in {k for r in rows for k in r["cover"]}:
        vals = sorted(r["cover"][k] for r in rows if k in r["cover"])
        usual[k] = vals[len(vals) // 2]
    bad_b = [(r["i"], k, v) for r in rows for k, v in r["cover"].items() if v < EDGE_JOINTS.get(k, cover_min)]
    check.usual = usual
    bad_c = [(r["i"], r["grip_px"]) for r in rows if r["grip_px"] is not None and r["grip_px"] > grip_max]
    return rows, bad_a, bad_b, bad_c


def ranges(frames):
    frames = sorted(set(frames))
    out, s, p = [], None, None
    for f in frames:
        if s is None:
            s = p = f
        elif f == p + 1:
            p = f
        else:
            out.append(f"{s}-{p}" if p > s else str(s))
            s = p = f
    if s is not None:
        out.append(f"{s}-{p}" if p > s else str(s))
    return ",".join(out)


def cmd_check(a):
    clean = Take(a.clean)
    main = Take(a.take) if a.take else None
    radii = rig_radii(a.rig, clean, a.rig_share) if getattr(a, "rig", None) else {}
    rows, bad_a, bad_b, bad_c = check(clean, main, radius=a.radius, cover_min=a.cover, grip_max=a.grip_max, radii=radii)
    lines = []
    lines.append("(a) in one piece: " + ("pass" if not bad_a else f"FLAG {len(bad_a)} frames with >1 big blob: {ranges(bad_a)}"))
    if bad_b:
        by = {}
        for i, k, v in bad_b:
            by.setdefault(k, []).append(i)
        worst = {}
        for i, k, v in bad_b:
            worst[k] = min(worst.get(k, 1), v)
        lines.append("(b) joints covered: FLAG " + "; ".join(f"{k} {ranges(v)} (lowest {worst[k]:.0%})" for k, v in by.items()))
    else:
        lines.append("(b) joints covered: pass" if any(r["cover"] for r in rows) else "(b) joints covered: no joint points")
    if any(r["cover"] for r in rows):
        low = {k: min(r["cover"][k] for r in rows if k in r["cover"]) for k in check.usual}
        lines.append("    joint cover, lowest / usual: " + ", ".join(f"{k[2:]} {low[k]:.0%}/{check.usual[k]:.0%}" for k in sorted(low)))
    have_c = any(r["grip_px"] is not None for r in rows)
    if not have_c:
        lines.append("(c) weapon in hand: no grip points")
    elif bad_c:
        lines.append(f"(c) weapon in hand: FLAG {len(bad_c)} frames, worst {max(d for _, d in bad_c):.1f} px: {ranges([i for i, _ in bad_c])}")
    else:
        lines.append(f"(c) weapon in hand: pass (max {max(r['grip_px'] for r in rows if r['grip_px'] is not None):.2f} px)")
    for l in lines:
        print(l)
    os.makedirs(a.out, exist_ok=True)
    # table
    names = sorted({k for r in rows for k in r["cover"]})
    with open(os.path.join(a.out, "check.tsv"), "w") as f:
        f.write("frame\tms\tblobs\tblob_areas\tgrip_px\t" + "\t".join(names) + "\n")
        for r in rows:
            f.write(f"{r['i']}\t{clean.ms(r['i'])}\t{r['blobs']}\t{'/'.join(map(str, r['blob_areas']))}\t"
                    f"{'' if r['grip_px'] is None else round(r['grip_px'], 2)}\t"
                    + "\t".join(f"{r['cover'].get(n, ''):.2f}" if n in r["cover"] else "" for n in names) + "\n")
    with open(os.path.join(a.out, "check.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    # sheet of only the flagged frames, with what is wrong drawn on
    flagged = sorted(set(bad_a) | {i for i, _, _ in bad_b} | {i for i, _ in bad_c})
    gaps = {(i, k) for i, k, _ in bad_b}
    if flagged:
        pick = flagged if len(flagged) <= a.max else [flagged[int(k * (len(flagged) - 1) / (a.max - 1))] for k in range(a.max)]
        crop = clean.crop(30)
        cells = []
        for i in pick:
            im = clean.image(i)
            d = ImageDraw.Draw(im)
            r = rows[i]
            if r["blobs"] > 1:
                for b in r["blob_boxes"][1:]:
                    d.rectangle((b[0] - 4, b[1] - 4, b[2] + 4, b[3] + 4), outline=(255, 255, 0), width=3)
            for k, v in r["cover"].items():
                if (i, k) in gaps:
                    p = clean.points(i)[k]
                    rr = radii.get(k, a.radius)
                    d.ellipse((p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr), outline=(0, 255, 255), width=2)
                    d.text((p[0] + rr + 2, p[1] - 6), f"{k[2:]} {v:.0%}", font=font(16), fill=(255, 255, 255))
            if r["grip_px"] is not None and r["grip_px"] > a.grip_max:
                gp = (main or clean).points(i)
                for n, col in (("grip", (255, 0, 0)), ("hand_grip", (0, 120, 255))):
                    p = gp[n]
                    d.ellipse((p[0] - 5, p[1] - 5, p[0] + 5, p[1] + 5), outline=col, width=3)
            im = im.crop(crop)
            why = []
            if r["blobs"] > 1:
                why.append(f"{r['blobs']} blobs")
            if any((i, k) in gaps for k in r["cover"]):
                why.append("joint gap")
            if r["grip_px"] is not None and r["grip_px"] > a.grip_max:
                why.append(f"grip {r['grip_px']:.0f}px")
            label(im, f"#{i} {clean.ms(i)} ms: " + ", ".join(why), max(12, im.width // 22))
            cells.append(im)
        cols = min(4, len(cells))
        cw, ch = cells[0].size
        sc = min(1.0, 2000 / (cw * cols))
        tw, th = int(cw * sc), int(ch * sc)
        sheet = Image.new("RGB", (tw * cols, th * ((len(cells) + cols - 1) // cols)), (30, 30, 30))
        for k, c in enumerate(cells):
            sheet.paste(c.resize((tw, th), Image.LANCZOS), ((k % cols) * tw, (k // cols) * th))
        p = save_webp(sheet, os.path.join(a.out, "check_flagged.webp"))
        print(p, f"{len(flagged)} flagged frames, {len(pick)} shown")
    else:
        print("no flagged frames")


# --- side -----------------------------------------------------------------------------------------------------------

def cmd_side(a):
    takes, rows = [], []
    for spec in a.takes:
        # folder[:name[:frames]]   frames like "10,20,30" or "every=4"
        parts = spec.split("|")
        folder = parts[0]
        name = parts[1] if len(parts) > 1 else os.path.basename(os.path.normpath(folder))
        t = Take(folder)
        fr = parts[2] if len(parts) > 2 else None
        if fr and fr.startswith("every="):
            frames = frame_list(t, int(fr[6:]), None)
        elif fr:
            frames = frame_list(t, 1, fr)
        else:
            frames = [i for i, _ in pick_keys(t)][: a.cols]
        crop = t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
        if a.follow:
            t.follow_box(a.follow, a.margin, frames)
        takes.append((name, t, frames, crop))
    # every row's cells the same height
    cell_h = a.cell_height
    out_rows = []
    for name, t, frames, crop in takes:
        cells = []
        for i in frames:
            im = t.cropped(i, crop)
            im = im.resize((int(im.width * cell_h / im.height), cell_h), Image.LANCZOS)
            ev = short_events(t, i)
            label(im, f"#{i} {t.ms(i)} ms", 13)
            if ev:
                label(im, ev, 13, (4, cell_h - 24), fill=(255, 230, 120))
            cells.append(im)
        w = sum(c.width for c in cells) + 180
        row = Image.new("RGB", (w, cell_h), (24, 24, 28))
        import textwrap
        lines = textwrap.wrap(name, 16)
        for k, line in enumerate(lines):
            label(row, line, 18, (6, cell_h // 2 - 12 * len(lines) + 24 * k), bg=(24, 24, 28))
        x = 180
        for c in cells:
            row.paste(c, (x, 0))
            x += c.width
        out_rows.append(row)
    W = max(r.width for r in out_rows)
    sheet = Image.new("RGB", (W, sum(r.height + 6 for r in out_rows) + (34 if a.title else 0)), (24, 24, 28))
    y = 0
    if a.title:
        label(sheet, a.title, 20, (8, 6), bg=(24, 24, 28))
        y = 34
    for r in out_rows:
        sheet.paste(r, (0, y))
        y += r.height + 6
    if sheet.width > a.max_width:
        sheet = sheet.resize((a.max_width, int(sheet.height * a.max_width / sheet.width)), Image.LANCZOS)
    print(save_webp(sheet, a.out), sheet.size)


# --- reel -----------------------------------------------------------------------------------------------------------

def cmd_reel(a):
    """Several clips into one mp4, one after another (default) or side by side (--across), each with its name."""
    clips = []
    for spec in a.clips:
        path, _, name = spec.partition("|")
        clips.append((path, name or os.path.splitext(os.path.basename(path))[0]))
    ensure_dir(a.out)
    inputs, filters = [], []
    for k, (p, name) in enumerate(clips):
        inputs += ["-i", p]
        txt = name.replace(":", "\\:").replace("'", "")
        # one after another: every clip fitted into the same frame (concat needs one size); side by side: same height
        fit = (f"scale={a.width}:{a.height}:force_original_aspect_ratio=decrease,pad={a.width}:{a.height}:(ow-iw)/2:(oh-ih)/2:color=0x18181c"
               if not a.across else f"scale=-2:{a.height}")
        filters.append(f"[{k}:v]{fit},setsar=1,fps=30,"
                       f"drawtext=fontfile='{FONT_FILE}':text='{txt}':x=10:y=h-th-10:fontsize=24:fontcolor=white:box=1:boxcolor=black@0.6[v{k}]")
    n = len(clips)
    if a.across:
        filters.append("".join(f"[v{k}]" for k in range(n)) + f"hstack=inputs={n}[out]" if n > 1 else "[v0]copy[out]")
    else:
        filters.append("".join(f"[v{k}]" for k in range(n)) + f"concat=n={n}:v=1:a=0[out]")
    run_ffmpeg(inputs + ["-filter_complex", ";".join(filters), "-map", "[out]", "-c:v", "libx264", "-pix_fmt", "yuv420p",
                         "-crf", "27", "-preset", "slow", "-movflags", "+faststart", a.out])
    print(a.out, f"{os.path.getsize(a.out) // 1024} KB")


# --- panels ---------------------------------------------------------------------------------------------------------

def cmd_panels(a):
    """Chosen frames from one or more takes, each with its own caption under it (a tour of abilities, a before/after).
    A frame is a number or "@event" (the first frame logging that event, e.g. @mark:swap) or "@event+3"."""
    cells = []
    import textwrap
    for spec in a.panels:
        folder, frame, caption = (spec.split("|", 2) + ["", ""])[:3]
        t = Take(folder)
        if frame.startswith("@"):
            name, _, plus = frame[1:].partition("+")
            hit = next((i for i in range(t.n) if name in t.events(i)), None)
            if hit is None:
                raise SystemExit(f"no event {name} in {folder}")
            i = min(t.n - 1, hit + (int(plus) if plus else 0))
        else:
            i = int(frame)
        crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
        im = t.image(i).crop(crop)
        im = im.resize((int(im.width * a.cell_height / im.height), a.cell_height), Image.LANCZOS)
        if not a.no_tag:
            label(im, f"#{i} {t.ms(i)} ms", 13)
        lines = textwrap.wrap(caption, max(12, im.width // 9))
        cap = Image.new("RGB", (im.width, 8 + 19 * max(1, len(lines))), (24, 24, 28))
        d = ImageDraw.Draw(cap)
        for k, line in enumerate(lines):
            d.text((6, 4 + 19 * k), line, font=font(15), fill=(255, 235, 170))
        cell = Image.new("RGB", (im.width, im.height + cap.height), (24, 24, 28))
        cell.paste(im, (0, 0))
        cell.paste(cap, (0, im.height))
        cells.append(cell)
    cols = min(a.cols, len(cells))
    cw = max(c.width for c in cells)
    ch = max(c.height for c in cells)
    head = 34 if a.title else 0
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new("RGB", (cw * cols + 4 * (cols - 1), ch * rows + 4 * (rows - 1) + head), (24, 24, 28))
    if a.title:
        label(sheet, a.title, 20, (8, 6), bg=(24, 24, 28))
    for k, c in enumerate(cells):
        sheet.paste(c, ((k % cols) * (cw + 4), (k // cols) * (ch + 4) + head))
    if sheet.width > a.max_width:
        sheet = sheet.resize((a.max_width, int(sheet.height * a.max_width / sheet.width)), Image.LANCZOS)
    print(save_webp(sheet, a.out, a.max_kb), sheet.size)


# --- all ------------------------------------------------------------------------------------------------------------

def cmd_all(a):
    t = Take(a.take)
    crop = parse_crop(a.crop) or t.crop(a.margin, a.boxes.split(",") if a.boxes else None)
    os.makedirs(a.out, exist_ok=True)
    if a.follow:
        t.follow_box(a.follow, a.margin, frame_list(t, 1, None, a.start, a.end))
    name = a.title or os.path.basename(os.path.normpath(a.out))
    every = a.every or max(1, t.n // 32)
    frames = frame_list(t, every, None, a.start, a.end)
    sheet = make_strip(t, frames, crop, 8, 2000, f"{name}: every {every} frames ({every * 1000 / t.fps:.0f} ms)")
    print(save_webp(sheet, os.path.join(a.out, "strip.webp")), sheet.size)
    # Every frame through the strike (the fastest part, which every-Nth sampling skips): from the last moment before
    # contact to about 100 ms after it.
    ev = [i for i, e in t.event_frames() if a.end is None or i <= a.end]
    hit = next((i for i, e in t.event_frames() if e in ("contact", "hitstop:on") and (a.end is None or i <= a.end)), None)
    if hit is not None:
        before = [i for i in ev if i < hit]
        s0 = before[-1] if before else max(0, hit - t.fps // 5)
        s1 = min(t.n - 1, hit + max(2, t.fps // 10))
        frames = list(range(s0, s1 + 1))
        sheet = make_strip(t, frames, crop, min(8, len(frames)), 2000, f"{name}: every frame through the strike ({1000 / t.fps:.0f} ms apart)")
        print(save_webp(sheet, os.path.join(a.out, "strike.webp")), sheet.size)
    for f in os.listdir(a.out):
        if f.startswith("key_") and f.endswith(".webp"):
            os.remove(os.path.join(a.out, f))
    keys = pick_keys(t, a.end)
    # "press" is only a key when no drawn pose starts with it (the mage's spell waits a beat)
    keys = [k for k in keys if k[1] != "press" or not any(j == k[0] for j, n in keys if n != "press")]
    if a.keys:
        keys = [(int(p.split("=")[1]), p.split("=")[0]) for p in a.keys.split(",")]
    for i, k in keys[:8]:
        im = t.cropped(i, crop)
        label(im, f"{k}  #{i}  {t.ms(i)} ms", max(14, im.width // 30))
        im.save(os.path.join(a.out, f"key_{i:03d}_{k}.webp".replace(":", "_")), "WEBP", quality=90)
    print("keys:", [f"{i}:{k}" for i, k in keys[:8]])
    print(write_clip(t, crop, os.path.join(a.out, "clip.mp4"), 720, 1, frame_list(t, 1, None, a.start, a.end), name))
    if True:  # half speed too: a blow is over in a blink
        print(write_clip(t, crop, os.path.join(a.out, "clip_half.mp4"), 720, 2, frame_list(t, 1, None, a.start, a.end), name + " (half speed)"))
    img = make_timeline(t, crop, a.tip, 1600, name, a.start, a.end)
    print(save_webp(img, os.path.join(a.out, "timeline.webp")))
    for i, e, dur in beats(t, a.end):
        print(f"  #{i:3d} {t.ms(i):5d} ms  {e}" + (f"   -> {dur:.0f} ms to next" if dur else ""))
    if any(a.tip in t.points(i) for i in range(t.n)):
        hits = [i for i, e in t.event_frames() if e in ("contact", "hitstop:on")]
        at = a.at if a.at is not None else (hits[0] if hits else t.n // 2)
        img = make_tip(t, crop, at, a.tip, a.hand, a.start, a.end if a.end is not None else None)
        print(save_webp(img, os.path.join(a.out, "tip.webp")))
    if a.clean:
        ns = argparse.Namespace(clean=a.clean, take=a.take, out=a.out, radius=a.radius, cover=a.cover, grip_max=a.grip_max, max=12,
                                rig=a.rig, rig_share=a.rig_share)
        cmd_check(ns)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    def common(s, out_help):
        s.add_argument("take", help="take folder (f000.jpg ... + track.json)")
        s.add_argument("--out", required=True, help=out_help)
        s.add_argument("--crop", help="x0,y0,x1,y1 in picture px (default: union of the tracked boxes + margin)")
        s.add_argument("--boxes", help="which tracked boxes make the crop, e.g. hero or hero,foe (default: all)")
        s.add_argument("--margin", type=int, default=40)

    s = sub.add_parser("strip", help="frame strip (contact sheet), cells labelled with frame and ms")
    common(s, "output .webp")
    s.add_argument("--every", type=int, default=3)
    s.add_argument("--frames", help="a list instead: 10,12,20-24")
    s.add_argument("--start", type=int, default=0)
    s.add_argument("--end", type=int)
    s.add_argument("--cols", type=int, default=8)
    s.add_argument("--max-width", type=int, default=2000)
    s.add_argument("--title")
    s.set_defaults(fn=cmd_strip)

    s = sub.add_parser("keys", help="chosen frames at full size, one file each")
    common(s, "output folder")
    s.add_argument("--frames", help="name=frame,... e.g. wind=17,strike=30 (default: the first frame of each event)")
    s.add_argument("--max", type=int, default=8)
    s.set_defaults(fn=cmd_keys)

    s = sub.add_parser("clip", help="frames to a small .mp4 (ffmpeg)")
    common(s, "output .mp4")
    s.add_argument("--width", type=int, default=720)
    s.add_argument("--slow", type=float, default=1, help="2 = half speed")
    s.add_argument("--start", type=int, default=0)
    s.add_argument("--end", type=int)
    s.add_argument("--title")
    s.set_defaults(fn=cmd_clip)

    s = sub.add_parser("timeline", help="motion per frame as curves over time, events and beat lengths marked")
    common(s, "output .webp")
    s.add_argument("--tip", default="tip", help="tracked point whose speed is drawn too")
    s.add_argument("--width", type=int, default=1600)
    s.add_argument("--start", type=int, default=0)
    s.add_argument("--end", type=int)
    s.add_argument("--title")
    s.set_defaults(fn=cmd_timeline)

    s = sub.add_parser("tip", help="weapon tip path (and grip hand path) over the contact frame")
    common(s, "output .webp")
    s.add_argument("--at", type=int, help="frame to draw on (default: the first contact / hit pause)")
    s.add_argument("--tip", default="tip")
    s.add_argument("--hand", default="hand_grip")
    s.add_argument("--start", type=int, default=0)
    s.add_argument("--end", type=int)
    s.add_argument("--scale", type=float, default=1.0)
    s.set_defaults(fn=cmd_tip)

    s = sub.add_parser("check", help="every-frame checks: in one piece, joints covered, weapon in hand")
    s.add_argument("clean", help="clean-pass folder (actor alone on magenta, with its track.json)")
    s.add_argument("--take", help="main take folder (grip points from there; default: the clean pass's)")
    s.add_argument("--out", required=True, help="output folder (check.txt, check.tsv, check_flagged.webp)")
    s.add_argument("--radius", type=int, default=10, help="joint circle radius in picture px")
    s.add_argument("--rig", help="rig.json: circle radius per joint = --rig-share x the rig's joint radius")
    s.add_argument("--rig-share", type=float, default=0.6)
    s.add_argument("--cover", type=float, default=0.85, help="lowest covered share around a joint")
    s.add_argument("--grip-max", type=float, default=3.0, help="largest grip-to-hand distance in px")
    s.add_argument("--max", type=int, default=12, help="flagged frames shown at most")
    s.set_defaults(fn=cmd_check)

    s = sub.add_parser("side", help="takes side by side: one row each, with names")
    s.add_argument("takes", nargs="+", help="folder|name|frames  (frames: 10,20,30 or every=4; default: key frames)")
    s.add_argument("--out", required=True)
    s.add_argument("--boxes", help="which tracked boxes make each crop (default: all)")
    s.add_argument("--margin", type=int, default=30)
    s.add_argument("--follow", help="crop around this tracked box per frame (e.g. hero)")
    s.add_argument("--cols", type=int, default=8)
    s.add_argument("--cell-height", type=int, default=300)
    s.add_argument("--max-width", type=int, default=2000)
    s.add_argument("--title")
    s.set_defaults(fn=cmd_side)

    s = sub.add_parser("reel", help="several clips into one mp4, each with its name")
    s.add_argument("clips", nargs="+", help="clip.mp4|name")
    s.add_argument("--out", required=True)
    s.add_argument("--height", type=int, default=360)
    s.add_argument("--width", type=int, default=640, help="frame width when one after another")
    s.add_argument("--across", action="store_true", help="side by side instead of one after another")
    s.set_defaults(fn=cmd_reel)

    s = sub.add_parser("panels", help="chosen frames with a caption each: folder|frame|caption (frame: 12 or @event)")
    s.add_argument("panels", nargs="+")
    s.add_argument("--out", required=True)
    s.add_argument("--crop", help="x0,y0,x1,y1 for every panel (default: each take's tracked boxes + margin)")
    s.add_argument("--boxes")
    s.add_argument("--margin", type=int, default=30)
    s.add_argument("--cols", type=int, default=4)
    s.add_argument("--cell-height", type=int, default=300)
    s.add_argument("--max-width", type=int, default=2000)
    s.add_argument("--max-kb", type=int, default=580)
    s.add_argument("--title")
    s.add_argument("--no-tag", action="store_true", help="no frame number / time tag (frames not on one clock)")
    s.set_defaults(fn=cmd_panels)

    s = sub.add_parser("all", help="strip, keys, clip, timeline, tip (and check with --clean) in one go")
    common(s, "output folder")
    s.add_argument("--clean", help="clean-pass folder: also run check")
    s.add_argument("--follow", help="strip, keys and clip follow this tracked box (e.g. hero) instead of one fixed crop")
    s.add_argument("--every", type=int)
    s.add_argument("--keys", help="name=frame,... (default: from the events)")
    s.add_argument("--tip", default="tip")
    s.add_argument("--hand", default="hand_grip")
    s.add_argument("--at", type=int)
    s.add_argument("--start", type=int, default=0)
    s.add_argument("--end", type=int)
    s.add_argument("--title")
    s.add_argument("--radius", type=int, default=10)
    s.add_argument("--rig", help="rig.json for the joint circles (see check)")
    s.add_argument("--rig-share", type=float, default=0.6)
    s.add_argument("--cover", type=float, default=0.85)
    s.add_argument("--grip-max", type=float, default=3.0)
    s.set_defaults(fn=cmd_all)

    a = p.parse_args()
    a.fn(a)


if __name__ == "__main__":
    main()
