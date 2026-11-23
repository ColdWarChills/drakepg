"""The attack's timing: one table (frames at 60 fps) drives the renderer and writes timing.md.

Only what Unity does with plain sprites: hard swaps of the four drawn moments, whole-figure moves (x, hop, a lean
about a foot, a few percent of squash/stretch), and effect sprites."""
import math

from common import FPS, GROUND_Y, PLAYER_X

# --- the beats (frames at 60 fps) ---
ANTIC_IN = 8      # swap to the wind-up; he sinks back into it (squash 3%, lean back 3 deg about the back foot)
ANTIC_HOLD = 16   # held: the wind-up reads
DASH = 6          # in to the monster on a low hop, still wound up
FREEZE = 6        # hit-stop on the strike
CARRY = 4         # strike after the freeze: leans on, about the planted front foot
FOLLOW = 15       # follow-through held
RECOVER = 18      # hop home (swapped to ready on the first airborne frame)
LAND = 11         # landing squash settles

HIT = ANTIC_IN + ANTIC_HOLD + DASH            # contact frame (first strike frame)
AFTER = HIT + FREEZE                          # first frame after the freeze
FOLLOW_AT = AFTER + CARRY
RECOVER_AT = FOLLOW_AT + FOLLOW
LAND_AT = RECOVER_AT + RECOVER
LENGTH = LAND_AT + LAND

SQUASH = 0.03     # "a few percent"
LEAN_BACK, LEAN_ON = -3.0, 3.0
HOP_DASH, HOP_HOME = 30, 24      # hops (design px): feet clear the ground in every travelling frame
KICK, SHAKE = 6, 5               # camera knock along the blow, then a short shake (design px)
PUSH, TILT = 28, 8               # monster knock-back: 18 + weight * 70 px, 6 + weight * 12 deg (weight ~0.15)
WHITE = 3                        # frames the monster shows flat white (then its own colours under the impact)
TORSO_GAP = 14                  # at the end of the dash the wound-up torso is this far behind the strike's

PHASES = [
    # name, start, frames, moment, what moves, Unity
    ("idle", None, None, "ready", "breath: height x1.00-1.03, 1.8 s cycle", "Idle(): Anim.Float yoyo on Breath"),
    ("wind-up", 0, ANTIC_IN, "wind", "swap; squash to 1.03 x 0.97 and lean back 3 deg about the back foot "
     "(CubicOut); back foot stays where ready's was", "SetPose(wind) + Anim.Float on Sx/Sy/Angle (pivot: back foot)"),
    ("wind-up held", ANTIC_IN, ANTIC_HOLD, "wind", "held; lean creeps to -4 deg", "Anim.Wait"),
    ("dash", ANTIC_IN + ANTIC_HOLD, DASH, "wind", "in the air all along (hop 30, sin): x home -> strike spot minus the torso offset (QuadIn), "
     "landing on the hit frame; stretch 1.04 x 0.97, lean -4 -> +4 deg", "Anim.Float on X/Y/Angle/Sx/Sy (BattleScreen.Lunge)"),
    ("hit + freeze", HIT, FREEZE, "strike", "swap on the contact frame; front foot planted at the strike spot; "
     "everything holds", "SetPose(strike); Fx.HitStop(100 ms)"),
    ("carry", AFTER, CARRY, "strike", "leans on +3 deg about the front foot, squash back to 1", "Anim.Float Angle"),
    ("follow-through", FOLLOW_AT, FOLLOW, "follow", "swap; front foot stays where the strike's was; lean +3 -> +1 "
     "deg", "SetPose(follow) + Anim.Float Angle; Anim.Wait"),
    ("recover", RECOVER_AT, RECOVER, "ready", "swap on the first airborne frame (torso kept in place); hop 24 "
     "home (SineInOut)", "SetPose(null) + Anim.Float X/Y (BattleScreen.Recover)"),
    ("land", LAND_AT, LAND, "ready", "squash 1.03 x 0.97 back to 1 (QuadOut)", "Anim.Float Sx/Sy"),
]

EFFECTS = [
    # name, start, frames, what it does, Unity
    ("push-off dust", ANTIC_IN + ANTIC_HOLD, 27, "3 paper puffs at the back foot", "Fx.Dust (flat puffs)"),
    ("speed streaks", ANTIC_IN + ANTIC_HOLD, 12, "7 white streaks shooting back", "Fx.SpeedLines"),
    ("dash ghosts", ANTIC_IN + ANTIC_HOLD + 1, 13, "3 flat cream silhouettes of the wind-up left behind, 35/28/21% "
     "fading", "Figure copies tinted flat (Fx.FadeGhost)"),
    ("slash arc", HIT, FREEZE + 7, "crescent from the wind-up angle over the top to the strike angle around the "
     "strike's grip, radius = blade; whole on the hit frame and the freeze, then wipes toward the blade",
     "one mesh/sprite; Anim.Float on its fill start + alpha"),
    ("impact", HIT, FREEZE + 8, "jagged gold burst with a white one inside on the monster's body; 0.8 -> 1.0 on the "
     "hit frame, held in the freeze, then 1.15 and gone", "Fx.Impact"),
    ("sparks", HIT, FREEZE + 25, "14 flat shards out of the hit, falling and shrinking (frozen in the freeze)",
     "Fx.HitSparks"),
    ("monster white", HIT, WHITE, "whole monster sprite flat white (on real time: it ends inside the freeze)", "Fx.WhiteFlash (50 ms, real time)"),
    ("camera kick", HIT, FREEZE, "the view knocked 6 px along the blow, held in the freeze", "GameRoot.Kick"),
    ("camera shake", AFTER, 8, "5 px decaying shake", "GameRoot.Shake"),
    ("knock-back", AFTER, 4 + 21, "monster snapped 28 px away and tilted 8 deg in 4 frames (CubicOut), squash "
     "0.94 x 1.05, back home over 18 frames after a 3-frame pause (SineInOut)", "Anim.Float on X/Angle/Sx/Sy"),
    ("landing dust", HIT, FREEZE + 27, "3 puffs at the strike's front foot", "Fx.Dust"),
    ("knock-back dust", AFTER + 4, 27, "3 puffs at the monster's feet", "Fx.Dust"),
    ("damage number", AFTER, 77, "'6' in Alegreya Sans ExtraBold, black outline, pops 0.3 -> 1.3 -> 1.0, rises 80 "
     "and fades after 0.48 s", "Ui.FloatText"),
    ("home dust", LAND_AT, 27, "3 puffs at each foot on landing", "Fx.Dust"),
]


def held(f):
    """Effect clock with the hit-stop: frames before the hit run, the freeze holds on the hit frame, then it resumes."""
    if f < HIT:
        return f
    if f < AFTER:
        return HIT
    return f - FREEZE + 1


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


def breath(f):
    """Idle(): Breath 1 -> 1.03 over 0.9 s, SineInOut, yoyo."""
    return 1 + 0.03 * ease_sine_inout(_tri(f / FPS / 0.9))


def _tri(x):
    x = x % 2
    return x if x < 1 else 2 - x


class Attack:
    """The attack laid out on a given stage: positions solved from the layers, so a re-run on new art moves them."""

    def __init__(self, hero, x_strike, home=PLAYER_X, ground=GROUND_Y):
        self.h = hero
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
        self.x_ready_back = x_strike + torso("follow") - torso("ready")

    def hero(self, f):
        """(moment, x, y, sx, sy, angle, pivot) at attack frame f (outside 0..LENGTH: standing at home)."""
        g, home = self.g, self.home
        if f < 0:
            return "ready", home, g, 1, 1, 0, None
        if f >= LENGTH:  # standing again: the idle breath restarts from 1
            return "ready", home, g, 1, breath(f - LENGTH), 0, None
        if f < ANTIC_IN:
            t = ease_cubic_out((f + 1) / ANTIC_IN)
            px = home + self.foot("wind", "back")
            return "wind", home, g, 1 + SQUASH * t, 1 - SQUASH * t, LEAN_BACK * t, (px, g)
        if f < ANTIC_IN + ANTIC_HOLD:
            t = (f - ANTIC_IN + 1) / ANTIC_HOLD
            px = home + self.foot("wind", "back")
            return "wind", home, g, 1 + SQUASH, 1 - SQUASH, LEAN_BACK - 1.0 * ease_sine_inout(t), (px, g)
        if f < HIT:
            # the hop's landing is the hit frame: every wind-up frame of the dash is in the air
            t = (f - ANTIC_IN - ANTIC_HOLD + 1) / (DASH + 1)
            x = home + (self.x_wind_end - home) * ease_quad_in(t)
            y = g - HOP_DASH * math.sin(math.pi * t)
            ang = -4 + 8 * t
            return "wind", x, y, 1 + 0.04, 1 - 0.03, ang, None
        if f < AFTER:
            return "strike", self.xs, g, 1, 1, 0, None
        if f < FOLLOW_AT:
            t = ease_quad_out((f - AFTER + 1) / CARRY)
            return "strike", self.xs, g, 1, 1, LEAN_ON * t, (self.xs, g)
        if f < RECOVER_AT:
            t = ease_quad_out((f - FOLLOW_AT + 1) / FOLLOW)
            return "follow", self.xs, g, 1, 1, LEAN_ON - 2 * t, (self.xs, g)
        if f < LAND_AT:
            t = (f - RECOVER_AT + 1) / RECOVER
            e = ease_sine_inout(t)
            x = self.x_ready_back + (home - self.x_ready_back) * e
            y = g - HOP_HOME * math.sin(math.pi * t)
            return "ready", x, y, 1, 1, 1 - e, None
        t = ease_quad_out((f - LAND_AT + 1) / LAND)
        return "ready", home, g, 1 + SQUASH * (1 - t), 1 - SQUASH * (1 - t), 0, None

    def ghosts(self, f):
        """Dash afterimages: (x, y, sx, sy, angle, alpha) of each still-visible ghost."""
        out = []
        d0 = ANTIC_IN + ANTIC_HOLD
        for i, at in enumerate((d0 + 1, d0 + 3, d0 + 5)):
            if at <= f < at + 13 and f < LENGTH:
                m, x, y, sx, sy, ang, _ = self.hero(at - 1)
                a = (0.35 - 0.07 * i) * (1 - (f - at) / 13)
                out.append((x, y, sx, sy, ang, a))
        return out

    def monster(self, f):
        """(dx, angle, sx, sy, white) of the monster at attack frame f."""
        if f < HIT or f >= LENGTH + 40:
            return 0, 0, 1, 1, False
        white = f < HIT + WHITE
        if f < AFTER:
            return 0, 0, 1, 1, white
        t = (f - AFTER + 1) / FPS
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
        if HIT <= f < AFTER:
            return KICK, 0
        if AFTER <= f < AFTER + 8:
            i = f - AFTER
            amp = SHAKE * (1 - i / 8)
            pat = [(1, -0.4), (-1, 0.6), (0.8, 0.5), (-0.6, -0.7), (0.5, 0.3), (-0.4, 0.2), (0.2, -0.2), (0, 0)]
            return KICK * (1 - (i + 1) / 8) + amp * pat[i][0], amp * pat[i][1]
        return 0, 0

    def fx_time(self, f):
        """Seconds of effect time since the hit, with the freeze held (0.015 s in) and resumed after it."""
        if f < HIT:
            return None
        if f < AFTER:
            return 0.0 if f == HIT else 0.015
        return 0.015 + (f - AFTER + 1) / FPS
