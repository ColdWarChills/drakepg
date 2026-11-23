"""The attack's timing: one table (frames at 60 fps) drives the renderer and writes timing.md.

Only what Unity does with plain sprites: hard swaps of the drawn moments, whole-figure moves (x, hop, a lean about a
foot, a few percent of squash/stretch), and effect sprites. Two variants from one timeline (`Beats(moments)`):
3 moments (ready, wind-up, strike: the strike held, then home) and 4 (the follow-through shown after the strike)."""
import math

from common import FPS, GROUND_Y, PLAYER_X

# --- the beats (frames at 60 fps) ---
ANTIC_IN = 8      # swap to the wind-up; he sinks back into it (squash 3%, lean back 3 deg about the back foot)
ANTIC_HOLD = 10   # held: the wind-up reads (the lean creeps on)
CROUCH = 5        # the pull-back right before the dash: squash to 6%, lean back to 7 deg about the back foot
DASH = 6          # in to the monster on a low hop, still wound up
FREEZE = 6        # hit-stop on the strike
STRIKE_HOLD = 16  # 3 moments: the strike held after the freeze (leans on, eases back) while the hit plays out
CARRY = 4         # 4 moments: the strike after the freeze, leaning on about the planted front foot
FOLLOW = 15       # 4 moments: the follow-through held
BACKOFF = 8       # the last pose itself lifts off and moves back (ease-in): the start of the hop home
HOME = 15         # swapped to ready in the air (torso kept in place); the rest of the hop home
LAND = 9          # landing squash settles

SQUASH = 0.03     # "a few percent"
CROUCH_SQUASH = 0.06
LEAN_BACK, LEAN_HOLD, LEAN_CROUCH, LEAN_ON = -3.0, -4.0, -7.0, 3.0
HOP_DASH, HOP_HOME = 30, 24      # hops (design px): feet clear the ground in every travelling frame
BACK_PX = 90                     # how far the last pose moves back before the swap to ready (design px)
KICK, SHAKE = 6, 5               # camera knock along the blow, then a short shake (design px)
PUSH, TILT = 28, 8               # monster knock-back: 18 + weight * 70 px, 6 + weight * 12 deg (weight ~0.15)
WHITE = 3                        # frames the monster shows flat white (then its own colours under the impact)
TORSO_GAP = 14                   # at the end of the dash the wound-up torso is this far behind the strike's

# The cut (contact.py): a crescent on the circle the blade tip travels. Data of this attack, read against the manifest.
CUT = {
    "shoulder": 0.6,  # the circle's centre: on the strike blade's line, this many blade lengths behind the grip
    "width": 0.45,    # full width at the blade, in blade lengths (outer edge on the circle, inward from there)
    "span": 160,      # degrees it covers before the strike angle (the blade's true path is up to ~180 here)
    "depth": 0.37,    # the monster's middle lies this share of the width inside the outer edge (solves the strike x)
    "clear": 48,      # ...but the sword hand's grip stops at least this far short of the monster's front edge (design
                      # px: room for the fist, the cross-guard and a gap), so a wide monster never lies on his hand
    "wipe": 5,        # frames after the freeze in which the tail runs into the blade
    "thin": 0.8,      # width at the end of the wipe
}


def ease_cubic_out(t):
    return 1 - (1 - t) ** 3


def ease_quad_in(t):
    return t * t


def ease_quad_out(t):
    return 1 - (1 - t) ** 2


def ease_sine_inout(t):
    return 0.5 - 0.5 * math.cos(math.pi * t)


def ease_back_out(t, s=1.70158):
    t -= 1
    return t * t * ((s + 1) * t + s) + 1


def hermite(p0, p1, m0, m1, s):
    s2, s3 = s * s, s * s * s
    return (2 * s3 - 3 * s2 + 1) * p0 + (s3 - 2 * s2 + s) * m0 + (-2 * s3 + 3 * s2) * p1 + (s3 - s2) * m1


def breath(f):
    """Idle(): Breath 1 -> 1.03 over 0.9 s, SineInOut, yoyo."""
    return 1 + 0.03 * ease_sine_inout(_tri(f / FPS / 0.9))


def _tri(x):
    x = x % 2
    return x if x < 1 else 2 - x


class Beats:
    """Frame numbers of one variant. moments: 3 (no follow-through) or 4."""

    def __init__(self, moments=3):
        if moments not in (3, 4):
            raise SystemExit("--moments is 3 or 4")
        self.moments = moments
        self.ANTIC_IN, self.ANTIC_HOLD, self.CROUCH, self.DASH, self.FREEZE = ANTIC_IN, ANTIC_HOLD, CROUCH, DASH, FREEZE
        self.CROUCH_AT = ANTIC_IN + ANTIC_HOLD
        self.DASH_AT = self.CROUCH_AT + CROUCH
        self.HIT = self.DASH_AT + DASH                 # contact frame (first strike frame)
        self.AFTER = self.HIT + FREEZE                 # first frame after the freeze
        if moments == 3:
            self.last = "strike"                       # the pose that backs off
            self.FOLLOW_AT = None
            self.BACKOFF_AT = self.AFTER + STRIKE_HOLD
        else:
            self.last = "follow"
            self.FOLLOW_AT = self.AFTER + CARRY
            self.BACKOFF_AT = self.FOLLOW_AT + FOLLOW
        self.SWAP_AT = self.BACKOFF_AT + BACKOFF       # first ready frame (in the air)
        self.LAND_AT = self.SWAP_AT + HOME
        self.LENGTH = self.LAND_AT + LAND
        self.PHASES = self._phases()
        self.EFFECTS = self._effects()

    def held(self, f):
        """Effect clock with the hit-stop: frames before the hit run, the freeze holds on the hit frame, then it
        resumes."""
        if f < self.HIT:
            return f
        if f < self.AFTER:
            return self.HIT
        return f - FREEZE + 1

    def _phases(self):
        b = self
        out = [
            # name, start, frames, moment, what moves, Unity
            ("idle", None, None, "ready", "breath: height x1.00-1.03, 1.8 s cycle", "Idle(): Anim.Float yoyo on Breath"),
            ("wind-up", 0, ANTIC_IN, "wind", "swap; squash to 1.03 x 0.97 and lean back 3 deg about the back foot "
             "(CubicOut); back foot stays where ready's was", "SetPose(wind) + Anim.Float on Sx/Sy/Angle (pivot: back foot)"),
            ("wind-up held", ANTIC_IN, ANTIC_HOLD, "wind", "held; lean creeps to -4 deg", "Anim.Wait (+ Anim.Float Angle)"),
            ("crouch", b.CROUCH_AT, CROUCH, "wind", "pull-back: squash 1.03 x 0.97 -> 1.06 x 0.94, lean -4 -> -7 deg "
             "about the back foot (QuadOut); back foot planted", "Anim.Float on Sx/Sy/Angle (pivot: back foot)"),
            ("dash", b.DASH_AT, DASH, "wind", "in the air all along (hop 30, sin): x home -> strike spot minus the "
             "torso offset (QuadIn), landing on the hit frame; stretch 1.04 x 0.97, lean -4 -> +4 deg",
             "Anim.Float on X/Y/Angle/Sx/Sy (BattleScreen.Lunge)"),
            ("hit + freeze", b.HIT, FREEZE, "strike", "swap on the contact frame; front foot planted at the strike "
             "spot; everything holds", "SetPose(strike); Fx.HitStop(100 ms)"),
        ]
        if self.moments == 3:
            out.append(("strike held", b.AFTER, STRIKE_HOLD, "strike", "leans on to +3 deg about the front foot in 4 "
                        "frames (QuadOut), eases back to +0.5 (SineInOut) while the knock-back, sparks, number and "
                        "shake play", "Anim.Float Angle (pivot: front foot)"))
        else:
            out += [("carry", b.AFTER, CARRY, "strike", "leans on +3 deg about the front foot", "Anim.Float Angle"),
                    ("follow-through", b.FOLLOW_AT, FOLLOW, "follow", "swap; front foot stays where the strike's "
                     "was; lean +3 -> +1 deg", "SetPose(follow) + Anim.Float Angle; Anim.Wait")]
        out += [
            ("back-off", b.BACKOFF_AT, BACKOFF, self.last, f"the {self.last} pose itself lifts off and moves back "
             f"{BACK_PX} px (QuadIn): the first {BACKOFF} frames of the hop home (hop {HOP_HOME}, sin over "
             f"{BACKOFF + HOME} frames); lean eases to 0", "Anim.Float on X/Y/Angle"),
            ("home", b.SWAP_AT, HOME, "ready", "swap to ready in the air, torso kept where it was; the rest of the "
             "hop home (speed carried over from the back-off, easing into the landing)",
             "SetPose(null) + Anim.Float X/Y (BattleScreen.Recover)"),
            ("land", b.LAND_AT, LAND, "ready", "squash 1.03 x 0.97 back to 1 (QuadOut)", "Anim.Float Sx/Sy"),
        ]
        return out

    def _effects(self):
        b = self
        return [
            # name, start, frames, what it does, Unity
            ("push-off dust", b.DASH_AT, 27, "3 paper puffs at the back foot", "Fx.Dust (flat puffs)"),
            ("speed streaks", b.DASH_AT, 12, "7 white streaks shooting back", "Fx.SpeedLines"),
            ("dash ghosts", b.DASH_AT + 1, 13, "3 flat cream silhouettes of the wind-up left behind, 35/28/21% "
             "fading", "Figure copies tinted flat (Fx.FadeGhost)"),
            ("cut", b.HIT, FREEZE + CUT["wipe"], f"opaque crescent on the circle the blade tip travels (centre "
             f"{CUT['shoulder']} blade lengths behind the grip, radius {1 + CUT['shoulder']:.1f} blade lengths), "
             f"{CUT['span']} deg ending at the strike blade, from a point to {CUT['width']} blade lengths wide; "
             f"cream body, gold outer band, ink under-shape; whole on the hit frame and the freeze, then the tail "
             f"runs into the blade in {CUT['wipe']} frames (thinning to {CUT['thin']})",
             "one generated mesh (crescent); Anim.Float on its start angle (and width), no alpha"),
            ("impact", b.HIT, FREEZE + 8, "jagged gold burst with a white one inside on the monster's body; 0.8 -> "
             "1.0 on the hit frame, held in the freeze, then 1.15 and gone", "Fx.Impact"),
            ("sparks", b.HIT, FREEZE + 25, "14 flat shards out of the hit, falling and shrinking (frozen in the "
             "freeze)", "Fx.HitSparks"),
            ("monster white", b.HIT, WHITE, "whole monster sprite flat white (on real time: it ends inside the "
             "freeze)", "Fx.WhiteFlash (50 ms, real time)"),
            ("camera kick", b.HIT, FREEZE, "the view knocked 6 px along the blow, held in the freeze", "GameRoot.Kick"),
            ("camera shake", b.AFTER, 8, "5 px decaying shake", "GameRoot.Shake"),
            ("knock-back", b.AFTER, 4 + 21, "monster snapped 28 px away and tilted 8 deg in 4 frames (CubicOut), "
             "squash 0.94 x 1.05, back home over 18 frames after a 3-frame pause (SineInOut)",
             "Anim.Float on X/Angle/Sx/Sy"),
            ("landing dust", b.HIT, FREEZE + 27, "3 puffs at the strike's front foot", "Fx.Dust"),
            ("knock-back dust", b.AFTER + 4, 27, "3 puffs at the monster's feet", "Fx.Dust"),
            ("damage number", b.AFTER, 77, "'6' in Alegreya Sans ExtraBold, black outline, pops 0.3 -> 1.3 -> 1.0, "
             "rises 80 and fades after 0.48 s", "Ui.FloatText"),
            ("home dust", b.LAND_AT, 27, "3 puffs at each foot on landing", "Fx.Dust"),
        ]


class Attack:
    """The attack laid out on a given stage: positions solved from the layers, so a re-run on new art moves them."""

    def __init__(self, hero, x_strike, beats, home=PLAYER_X, ground=GROUND_Y):
        self.h = hero
        self.b = beats
        self.home = home
        self.g = ground
        self.xs = x_strike
        k = hero.design_k
        A = hero.anchor

        def torso(m):
            b = hero.moments[m]["sprites"]["shirt"]
            return ((b[0] + b[2]) / 2 - A[m]) * k

        def foot(m, side):
            return (hero.feet[m][side][0] - A[m]) * k

        self.foot = foot
        self.x_wind_end = x_strike + torso("strike") - TORSO_GAP - torso("wind")
        # the last pose's x at the end of the back-off, and where ready stands with the torso kept in place
        self.x_back = x_strike - BACK_PX
        self.x_ready_swap = self.x_back + torso(beats.last) - torso("ready")
        self.v_back = 2 * BACK_PX / BACKOFF  # px per frame at the end of the back-off (QuadIn)

    def _wound(self, sq, ang):
        """The wind-up squashed `sq` and leaning `ang` with the back foot planted where ready's was."""
        fb = self.foot("wind", "back")
        sx = 1 + sq
        # place() scales about the anchor: shift so the back foot stays on home + fb
        x = self.home + fb * (1 - sx)
        return "wind", x, self.g, sx, 1 - sq, ang, (self.home + fb, self.g)

    def _hop_y(self, i):
        """The hop home: frame i of BACKOFF + HOME."""
        return self.g - HOP_HOME * math.sin(math.pi * (i + 1) / (BACKOFF + HOME))

    def hero(self, f):
        """(moment, x, y, sx, sy, angle, pivot) at attack frame f (outside 0..LENGTH: standing at home)."""
        b, g, home = self.b, self.g, self.home
        if f < 0:
            return "ready", home, g, 1, 1, 0, None
        if f >= b.LENGTH:  # standing again: the idle breath restarts from 1
            return "ready", home, g, 1, breath(f - b.LENGTH), 0, None
        if f < ANTIC_IN:
            t = ease_cubic_out((f + 1) / ANTIC_IN)
            return self._wound(SQUASH * t, LEAN_BACK * t)
        if f < b.CROUCH_AT:
            t = (f - ANTIC_IN + 1) / ANTIC_HOLD
            return self._wound(SQUASH, LEAN_BACK + (LEAN_HOLD - LEAN_BACK) * ease_sine_inout(t))
        if f < b.DASH_AT:
            t = ease_quad_out((f - b.CROUCH_AT + 1) / CROUCH)
            return self._wound(SQUASH + (CROUCH_SQUASH - SQUASH) * t, LEAN_HOLD + (LEAN_CROUCH - LEAN_HOLD) * t)
        if f < b.HIT:
            # the hop's landing is the hit frame: every wind-up frame of the dash is in the air
            t = (f - b.DASH_AT + 1) / (DASH + 1)
            x = home + (self.x_wind_end - home) * ease_quad_in(t)
            y = g - HOP_DASH * math.sin(math.pi * t)
            ang = -4 + 8 * t
            return "wind", x, y, 1 + 0.04, 1 - 0.03, ang, None
        if f < b.AFTER:
            return "strike", self.xs, g, 1, 1, 0, None
        if f < b.BACKOFF_AT:
            if b.moments == 3:
                i = f - b.AFTER
                if i < 4:
                    ang = LEAN_ON * ease_quad_out((i + 1) / 4)
                else:
                    ang = LEAN_ON - (LEAN_ON - 0.5) * ease_sine_inout((i - 3) / (STRIKE_HOLD - 4))
                return "strike", self.xs, g, 1, 1, ang, (self.xs, g)
            if f < b.FOLLOW_AT:
                t = ease_quad_out((f - b.AFTER + 1) / CARRY)
                return "strike", self.xs, g, 1, 1, LEAN_ON * t, (self.xs, g)
            t = ease_quad_out((f - b.FOLLOW_AT + 1) / FOLLOW)
            return "follow", self.xs, g, 1, 1, LEAN_ON - 2 * t, (self.xs, g)
        if f < b.SWAP_AT:
            i = f - b.BACKOFF_AT
            t = (i + 1) / BACKOFF
            x = self.xs - BACK_PX * ease_quad_in(t)
            y = self._hop_y(i)
            a0 = 0.5 if b.moments == 3 else 1.0
            return b.last, x, y, 1, 1, a0 * (1 - t), None
        if f < b.LAND_AT:
            j = f - b.SWAP_AT
            s = (j + 1) / HOME
            x = hermite(self.x_ready_swap, home, -self.v_back * HOME, 0.0, s)
            return "ready", x, self._hop_y(BACKOFF + j), 1, 1, 0, None
        t = ease_quad_out((f - b.LAND_AT + 1) / LAND)
        return "ready", home, g, 1 + SQUASH * (1 - t), 1 - SQUASH * (1 - t), 0, None

    def ghosts(self, f):
        """Dash afterimages: (x, y, sx, sy, angle, alpha) of each still-visible ghost."""
        out = []
        d0 = self.b.DASH_AT
        for i, at in enumerate((d0 + 1, d0 + 3, d0 + 5)):
            if at <= f < at + 13 and f < self.b.LENGTH:
                m, x, y, sx, sy, ang, _ = self.hero(at - 1)
                a = (0.35 - 0.07 * i) * (1 - (f - at) / 13)
                out.append((x, y, sx, sy, ang, a))
        return out

    def monster(self, f):
        """(dx, angle, sx, sy, white) of the monster at attack frame f."""
        b = self.b
        if f < b.HIT or f >= b.LENGTH + 40:
            return 0, 0, 1, 1, False
        white = f < b.HIT + WHITE
        if f < b.AFTER:
            return 0, 0, 1, 1, white
        t = (f - b.AFTER + 1) / FPS
        sq = max(0.0, 1 - t / 0.26)
        sqe = sq * sq
        sx, sy = 1 - 0.06 * sqe, 1 + 0.05 * sqe
        if t <= 0.06:
            k = ease_cubic_out(t / 0.06)
        elif t <= 0.11:
            k = 1
        elif t <= 0.41:
            k = 1 - ease_sine_inout((t - 0.11) / 0.3)
        else:
            k = 0
        return PUSH * k, TILT * k, sx, sy, white

    def camera(self, f):
        b = self.b
        if b.HIT <= f < b.AFTER:
            return KICK, 0
        if b.AFTER <= f < b.AFTER + 8:
            i = f - b.AFTER
            amp = SHAKE * (1 - i / 8)
            pat = [(1, -0.4), (-1, 0.6), (0.8, 0.5), (-0.6, -0.7), (0.5, 0.3), (-0.4, 0.2), (0.2, -0.2), (0, 0)]
            return KICK * (1 - (i + 1) / 8) + amp * pat[i][0], amp * pat[i][1]
        return 0, 0

    def fx_time(self, f):
        """Seconds of effect time since the hit, with the freeze held (0.015 s in) and resumed after it."""
        b = self.b
        if f < b.HIT:
            return None
        if f < b.AFTER:
            return 0.0 if f == b.HIT else 0.015
        return 0.015 + (f - b.AFTER + 1) / FPS
