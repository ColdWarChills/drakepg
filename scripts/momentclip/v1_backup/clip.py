"""The hero's sword attack as a clip in the battle view, from one layers folder.

<venv python> -I -B clip.py [--layers DIR] [--outfits bare,mix1,mix2 | piece+piece+...] [--out NAME] [--reps 2]
                            [--sheets] [--stills] [--no-video] [--anchor moment=x ...]
See README.md."""
import argparse
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.dont_write_bytecode = True

from PIL import Image  # noqa: E402

import contact  # noqa: E402
import fx  # noqa: E402
import scene  # noqa: E402
import timeline as T  # noqa: E402
from common import ENEMY_X, FFMPEG, FPS, GROUND_Y, MIXES, OUT, PLAYER_X, PX, caption  # noqa: E402
from layers import Hero  # noqa: E402

LEAD, BETWEEN, TAIL = 36, 48, 42   # idle frames before, between and after the two attacks of an outfit
DAMAGE = "6"


class Stage:
    def __init__(self, layers, anchors=None):
        self.hero = Hero(layers, anchors)
        self.bg = scene.background()
        self.mon = scene.monster_sprite()
        self.mon_w = self.mon[3]
        self.mon_shadow = scene.soft_shadow(self.mon_w / 0.9 * 0.7 * 0.9)
        rb = self.hero.moments["ready"]["sprites"]["body"]
        self.hero_shadow = scene.soft_shadow((rb[2] - rb[0]) * self.hero.design_k * 0.7)
        self.x_strike, self.hit, self.hit_angle = contact.solve(self.hero, self.mon, ENEMY_X, GROUND_Y, GROUND_Y)
        self.att = T.Attack(self.hero, self.x_strike)
        (gx, gy), r, a0, a1 = contact.arc(self.hero)
        self.arc = ((self.x_strike + gx, GROUND_Y + gy), r, a0, a1)
        self.sparks = fx.Sparks(self.hit)
        a = self.hero.anchor
        k = self.hero.design_k
        self.feet_mid = {m: (sum(v[0] for v in self.hero.feet[m].values()) / len(self.hero.feet[m]) - a[m]) * k
                         for m in self.hero.moments}
        d0 = T.ANTIC_IN + T.ANTIC_HOLD
        self.dusts = [
            (d0, fx.Dust(PLAYER_X + self.att.foot("wind", "back"), GROUND_Y, 3, 11, 12, -40), "push-off dust"),
            (T.HIT, fx.Dust(self.x_strike, GROUND_Y, 3, 12, 14, 30), "landing dust"),
            (T.AFTER + 4, fx.Dust(ENEMY_X + T.PUSH, GROUND_Y, 3, 13, 30, 20), "knock-back dust"),
            (T.LAND_AT, fx.Dust(PLAYER_X + self.att.foot("ready", "back"), GROUND_Y, 3, 14, 12), "home dust"),
            (T.LAND_AT, fx.Dust(PLAYER_X + self.att.foot("ready", "front"), GROUND_Y, 3, 15, 12), "home dust"),
        ]
        print(f"strike stands at x {self.x_strike:.0f} (front foot), hit at ({self.hit[0]:.0f}, {self.hit[1]:.0f}) "
              f"arc angle {self.hit_angle:.0f}; anchors {({m: round(v) for m, v in a.items()})}")

    def frame(self, outfit, f, idle_t, mon_t, label=None, effects=True):
        """One 2560x1440 frame. f: attack frame or None (idle). idle_t: frames since the hero stood still.
        Returns (RGB image, info dict)."""
        att = self.att
        on = []
        c = self.bg.copy()
        af = f if f is not None else -1
        # --- the hero's state
        m, hx, hy, sx, sy, ang, pivot = att.hero(af)
        if f is None:
            sy *= T.breath(idle_t)
        # --- shadows (on the ground; the hero's follows his x and shrinks as he hops)
        mdx, mang, msx, msy, white = att.monster(af)
        ms = self.mon_shadow
        c.alpha_composite(ms, (int(scene.to_px(ENEMY_X + mdx, 0)[0] - ms.width / 2),
                               int(scene.to_px(0, GROUND_Y)[1] - ms.height / 2)))
        lift = max(0.0, GROUND_Y - hy)
        hs = self.hero_shadow
        if lift > 0:
            s = 1 - lift / 120
            hs = hs.resize((max(2, int(hs.width * s)), max(2, int(hs.height * s))))
        c.alpha_composite(hs, (int(scene.to_px(hx + self.feet_mid[m], 0)[0] - hs.width / 2),
                               int(scene.to_px(0, GROUND_Y)[1] - hs.height / 2)))
        # --- monster
        mb = T.breath(mon_t)
        scene.place(c, self.mon, ENEMY_X + mdx, GROUND_Y, msx, msy * mb, mang, (ENEMY_X + mdx, GROUND_Y),
                    tint=(255, 255, 255) if white else None)
        if white:
            on.append("monster white")
        if mdx > 0.5:
            on.append("knock-back")
        # --- slash arc (in front of the monster, behind the hero)
        if not effects:  # a clean still of the pose: the stage and the figures only
            f_fx = None
        else:
            f_fx = f
        t_fx = att.fx_time(af) if f_fx is not None else None
        if f_fx is not None and T.HIT <= f < T.AFTER + 7:
            (cx, cy), r, a0, a1 = self.arc
            if f < T.AFTER:
                tail, width, alpha = 0.0, 1.0, 1.0
            else:
                k = (f - T.AFTER + 1) / 7
                tail, width, alpha = T.ease_quad_in(k), 1 - 0.4 * k, 1 - 0.5 * k
            fx.polys(c, fx.arc_shapes((cx, cy), r, a0, a1, tail, width, alpha))
            on.append("slash arc")
        # --- dash streaks and ghosts (behind the hero)
        if f_fx is not None:
            d0 = T.ANTIC_IN + T.ANTIC_HOLD
            hf = T.held(f)  # the hit-stop holds these too
            st = (hf - d0) / FPS
            if 0 <= st < 0.2:
                fx.polys(c, fx.streak_shapes(PLAYER_X, GROUND_Y, 1, st))
                on.append("streaks")
            gh = att.ghosts(hf)
            for gx, gy, gsx, gsy, gang, ga in gh:
                scene.place(c, self.hero.sprite("wind", outfit), gx, gy, gsx, gsy, gang, None, tint=(255, 246, 224),
                            alpha=ga)
            if gh:
                on.append(f"ghosts x{len(gh)}")
        # --- hero
        scene.place(c, self.hero.sprite(m, outfit), hx, hy, sx, sy, ang, pivot)
        # --- dust (just above the fighters, as Fx.Dust's order)
        if f_fx is not None:
            for at, d, name in self.dusts:
                if at <= f:
                    dt = ((T.held(f) - at) if at < T.HIT else (f - at)) / FPS
                    if T.HIT <= at < T.AFTER and f < T.AFTER:
                        dt = 0.015  # frozen with the hit
                    elif T.HIT <= at < T.AFTER:
                        dt = 0.015 + (f - T.AFTER + 1) / FPS
                    sh = d.shapes(dt)
                    if sh:
                        fx.polys(c, sh)
                        on.append(name)
        # --- impact and sparks
        if t_fx is not None:
            if f < T.AFTER + 8:
                if f == T.HIT:
                    size, a = 0.8, 1.0
                elif f < T.AFTER:
                    size, a = 1.0, 1.0
                else:
                    k = (f - T.AFTER + 1) / 8
                    size, a = 1 + 0.15 * k, 1.0 if k < 0.4 else 1 - (k - 0.4) / 0.6
                fx.polys(c, fx.impact_shapes(self.hit, size, a))
                on.append("impact")
            sp = self.sparks.shapes(t_fx)
            if sp:
                fx.polys(c, sp)
                on.append("sparks")
            # damage number (UI tweens wait out the freeze too)
            if f >= T.AFTER:
                nt = (f - T.AFTER + 1) / FPS
                if nt < 0.11:
                    s = 0.3 + 1.0 * T.ease_back_out(nt / 0.11)
                elif nt < 0.23:
                    s = 1.3 - 0.3 * (nt - 0.11) / 0.12
                else:
                    s = 1.0
                rise = 0.0
                al = 1.0
                if nt > 0.48:
                    v = min(1.0, (nt - 0.48) / 0.8) ** 3
                    rise, al = 80 * v, 1 - v
                if al > 0.01:
                    fx.number(c, ENEMY_X + 12, GROUND_Y - 230 - rise, DAMAGE, 36, s, al)
                    on.append("number")
        # --- camera
        camx, camy = att.camera(af)
        if camx or camy:
            on.append("camera kick/shake")
        x0 = scene.MARGIN - round(camx * PX)
        y0 = scene.MARGIN - round(camy * PX)
        out = c.crop((x0, y0, x0 + scene.W_PX, y0 + scene.H_PX))
        if label is not None:
            fx.caption(out, label)
        info = {"moment": m, "x": hx, "y": hy, "effects": on, "camera": (camx, camy)}
        return out.convert("RGB"), info


def outfit_list(arg):
    out = []
    for name in arg.split(","):
        name = name.strip()
        out.append((name, MIXES[name] if name in MIXES else [p for p in name.split("+") if p]))
    return out


def sequence(reps):
    """Per outfit: idle, then the attack `reps` times with idle between, then idle. Yields (attack frame or None,
    frames the hero has stood still)."""
    for i in range(LEAD):
        yield None, i
    for r in range(reps):
        for f in range(T.LENGTH):
            yield f, 0
        for i in range(BETWEEN if r < reps - 1 else TAIL):
            yield T.LENGTH + i, 0  # standing (breathing); the number and dust finish


def render_video(stage, outfits, out_name, reps):
    path = OUT / f"{out_name}.mp4"
    cmd = [FFMPEG, "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{scene.W_PX}x{scene.H_PX}",
           "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "medium", "-crf", "17", "-pix_fmt", "yuv420p",
           "-movflags", "+faststart", str(path)]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = 0
    times = {}
    for name, pieces in outfits:
        t0 = time.time()
        for f, idle in sequence(reps):
            img, _ = stage.frame(pieces, f, idle, n, caption(pieces))
            p.stdin.write(img.tobytes())
            n += 1
        times[name] = time.time() - t0
        print(f"{name}: rendered in {times[name]:.1f} s")
    p.stdin.close()
    p.wait()
    webp = OUT / f"{out_name}.webp"
    subprocess.run([FFMPEG, "-v", "error", "-y", "-i", str(path), "-vf", "fps=30,scale=1280:720:flags=lanczos",
                    "-c:v", "libwebp_anim", "-loop", "0", "-quality", "72", "-compression_level", "4", str(webp)],
                   check=True)
    print(path, webp, f"{n} frames")
    return times


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--layers", default=str(OUT / "layers_snapshot"))
    ap.add_argument("--outfits", default="bare", help="comma list of bare, mix1, mix2 or piece+piece+...")
    ap.add_argument("--out", default="draft_bare")
    ap.add_argument("--reps", type=int, default=2)
    ap.add_argument("--sheets", action="store_true", help="frame sheets, key frames and timing.md (first outfit)")
    ap.add_argument("--stills", default="", help="outfit stills of ready and strike, e.g. mix1,mix2")
    ap.add_argument("--no-video", action="store_true")
    ap.add_argument("--anchor", nargs="*", default=[], help="override a moment's anchor: moment=drawing_x")
    a = ap.parse_args()
    anchors = {kv.split("=")[0]: float(kv.split("=")[1]) for kv in a.anchor}
    stage = Stage(a.layers, anchors)
    outfits = outfit_list(a.outfits)
    if a.sheets:
        import sheets
        sheets.make(stage, outfits[0][1])
    if a.stills:
        import sheets
        sheets.stills(stage, outfit_list(a.stills))
    if not a.no_video:
        render_video(stage, outfits, a.out, a.reps)


if __name__ == "__main__":
    main()
