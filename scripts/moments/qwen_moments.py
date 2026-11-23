"""Qwen runs for the hero in moments. Every job is one picture: art/redo/moments/<job>_s<seed>.png (skipped if it
exists, so a batch can be re-run after a stop). Every run is logged in art/redo/moments/runs.jsonl.

<venv python> -I -B qwen_moments.py <job>:<seed>[,<seed>] ...       e.g.  bare_ready:101,102 bare_strike:301

Jobs:
  bare_<moment>   a pose edit of the game's bare hero (art/raw/char_hero_bare.png, scaled to HERO_H on the canvas)
  <piece>@<moment> an armor edit of the kept bare moment (picks in moments.json) -- see pieces.py
"""
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CANVAS, FEET_Y, HERO, HERO_H, REDO, RGBA_CLOSE, RGBA_OPEN, RAW, STYLE, Comfy, load_rgba, png_bytes

NEAR = "his right arm, the arm nearer to the viewer,"
FAR = "his left arm, the arm farther from the viewer,"
FLAT = ("The sword arm and the rod stay flat in the picture plane, seen from the side: neither the arm nor the rod "
        "points toward the viewer or away from the viewer.")


def rod(thumb_end: str) -> str:
    return (f"His right fist, the fist of the arm nearer to the viewer, firmly grips a short straight rod like the "
            f"handle of a sword: a plain smooth cylinder of pure bright green, a little thinner than his thumb, the "
            f"same thickness everywhere, about three times as long as his fist is wide, sticking out the same distance "
            f"on both sides of the fist. His four fingers wrap around the rod and his thumb closes over them, so the "
            f"rod passes straight through the closed fist. It is one single straight rod, not a cross and not two "
            f"pieces. The end of the rod that comes out on the thumb side of the "
            f"fist points {thumb_end}. His left hand is a closed empty fist. No sword, no blade, no other object.")


POSES_V1 = {
    "ready": (f"a ready sword-fighting stance, braced and alive: three-quarter view facing right, feet planted wide "
              f"apart with the front foot forward, both knees bent, his weight sunk low on the legs, his upper body "
              f"leaning a little forward, his chest turned toward the viewer and his shoulders squared; {NEAR} is "
              f"bent, its fist held in front of his belt a forearm's length out from his body, gripping the rod so "
              f"that it slants up and forward to the upper right like a sword held in a low guard; {FAR} is bent "
              f"with a clenched fist held up in front of his chest; a calm, alert face, eyes on the enemy",
              "up and forward to the upper right"),
    "wind": (f"coiled to swing a sword: three-quarter view facing right with his chest still turned toward the "
             f"viewer, a wide low stance with both knees bent, his weight on the rear leg, his upper body leaning back "
             f"and twisted so his near shoulder is pulled back; {NEAR} is raised high and bent, the elbow pointing "
             f"back to the left at the height of his shoulder, the fist just above and behind his near shoulder, to "
             f"the left of his head and not in front of his face, gripping the rod so that it points up and back to "
             f"the upper left like a sword cocked back over his shoulder, ready to swing; {FAR} reaches forward to "
             f"the right in front of his chest with a clenched fist for balance; a focused, determined face with "
             f"gritted teeth", "up and back to the upper left"),
    "strike": (f"the moment a big sword slash hits: three-quarter view facing right, a deep forward lunge with the "
               f"front knee bent over the front foot and the back leg stretched far out behind, his upper body "
               f"leaning forward with his chest turned toward the viewer; {NEAR} is swung forward and stretched out "
               f"straight in front of him at the height of his chest, the fist far out to the right, gripping the rod "
               f"so that it points forward and a little down to the right like a sword cutting through an enemy; "
               f"{FAR} is flung back behind him to the left with a clenched fist at the height of his waist, well "
               f"below his shoulders and clear of his head and body; a fierce battle shout, mouth wide open",
               "forward and a little down, to the right"),
    "follow": (f"the follow-through after a big downward sword slash: three-quarter view facing right, a deep forward "
               f"lunge with the front knee bent and the back leg stretched out behind, his upper body bent far "
               f"forward over the front knee with his chest turned toward the viewer; {NEAR} has swung all the way "
               f"down and through: it hangs down straight in front of his front thigh, the fist low at the height of "
               f"his front knee and a little in front of it, gripping the rod so that it points straight down at the "
               f"ground like a sword whose tip has just swept past the ground; {FAR} is drawn back with a bent elbow "
               f"and a clenched fist beside his far hip; a hard, focused face, teeth gritted, eyes still on the "
               f"enemy", "straight down at the ground, a little forward"),
}
# v2 (after try 1: upright and stiff, the follow-through rod came out as a cross): more lean and weight, the far hand
# relaxed rather than a boxer's guard, every rod on a slant.
POSES = {
    "ready": (f"a ready sword-fighting stance, braced and alive: three-quarter view facing right, feet planted wide "
              f"apart with the front foot forward, both knees deeply bent, his weight sunk low; his upper body leans "
              f"forward over the front leg and his shoulders are turned toward the viewer; {NEAR} which hangs from "
              f"his shoulder on the left side of the picture, is bent across the front of his body, its fist held "
              f"low in front of the middle of his belt, a little out from his body, gripping the rod so that it "
              f"slants up and forward to the upper right at about 45 degrees, like a sword held in a low guard and "
              f"pointing at the enemy; {FAR} on the right side of the picture, is bent with a relaxed empty fist held "
              f"low beside his far hip, holding nothing; a calm, "
              f"alert face, eyes on the enemy", "up and forward to the upper right, at about 45 degrees"),
    "wind": (f"coiled to swing a sword: three-quarter view facing right with his chest still turned toward the "
             f"viewer, a wide low stance with both knees bent, his weight back on the rear leg, his upper body leaning "
             f"back over the rear leg and twisted so his near shoulder is pulled back; {NEAR} is raised high and bent, "
             f"the elbow pointing back to the left at the height of his shoulder, the fist just above and behind his "
             f"near shoulder, to the left of his head and not in front of his face, gripping the rod so that it "
             f"points up and back to the upper left like a sword cocked back over his shoulder, ready to swing; {FAR} "
             f"reaches forward and low in front of his belly with a relaxed fist for balance; a focused, determined "
             f"face with gritted teeth", "up and back to the upper left"),
    "strike": (f"the moment a big diagonal sword slash hits: three-quarter view facing right, a very deep forward "
               f"lunge, the front knee bent far over the front foot, the back leg stretched long and straight behind "
               f"him with the back heel lifted; his whole upper body is thrown forward and leans down along the line "
               f"of the back leg, his head forward; {NEAR} is swung forward and down and stretched out straight in "
               f"front of him, the fist out past his front knee at the height of his hip, gripping the rod so that it "
               f"points forward and down to the lower right at a steep slant, like a sword cutting down through an "
               f"enemy; {FAR} is flung back behind him to the left for balance, below his shoulders, the fist clenched, "
               f"clear of his head and body; a fierce battle shout, mouth wide open",
               "forward and down to the lower right, at a steep slant"),
    "follow": (f"the follow-through after a big downward diagonal sword slash: three-quarter view facing right, a deep "
               f"forward lunge with the front knee bent and the back leg stretched out behind, his upper body bent far "
               f"forward over the front knee with his chest turned toward the viewer; {NEAR} has swung all the way "
               f"down and through and comes back toward his body: its fist is low, just in front of his front thigh "
               f"at the height of his knee, gripping the rod so that it slants down and back to the lower left at "
               f"about 45 degrees, in front of his shins, like a sword whose tip has just swept past the ground; "
               f"{FAR} is drawn back with a bent elbow and a relaxed fist beside his far hip; a hard, focused face, "
               f"teeth gritted, eyes still on the enemy", "down and back to the lower left, at about 45 degrees"),
}


# v3 (step A2): the marker says which end is the blade. A toy sword in two flat colours: the long green part leaves the
# fist at the thumb end, the short magenta stub under the little finger; code reads the blade end from the picture
# (cut.read_marker). The pose words name the side of the hand we see and where the thumb is.
def marker(blade_dir: str) -> str:
    return (f"His right fist, the fist of the arm nearer to the viewer, holds a toy sword by its handle: one thin "
            f"straight rod, as thick as a sword handle and the same thickness everywhere, painted in two flat colours. "
            f"The long part is pure bright green, about two and a half times as long as his fist is wide: it is the "
            f"toy blade and it comes out of the fist at the thumb and forefinger end. The short part is pure bright "
            f"magenta, about half as long as his fist is wide: it is the pommel stub and it comes out of the fist at "
            f"the little-finger end. His four fingers wrap around the rod and his thumb closes over them. The green "
            f"part points {blade_dir}. It is one single straight rod, not a cross. No real blade, no crossguard. His "
            f"left hand is a closed empty fist.")


# marker v2 (after grip_wind_s601: "toy sword" made a magenta handle across the fist and a separate green stick, a
# cross): no sword words; one stick through the fist, its two ends coloured.
def marker2(blade_dir: str) -> str:
    return (f"His right fist, the fist of the arm nearer to the viewer, grips one thin straight stick that passes "
            f"straight through the closed fist, very thin like a pencil, much thinner than his thumb, the same "
            f"thickness everywhere. On one "
            f"side of the fist the stick sticks out long, about two and a half times as long as the fist is wide, and "
            f"that long end is pure bright green; it comes out of the fist at the thumb and forefinger end. On the "
            f"other side only a short stub of the same stick sticks out, about half as long as the fist is wide, and "
            f"that short stub is pure bright magenta; it comes out of the fist under his little finger. The green end "
            f"and the magenta stub are the two ends of one single straight stick, in one straight line through the "
            f"fist; the fist covers the middle. The long green end points {blade_dir}. Nothing else in his hand: no "
            f"second stick, no crossguard, not a cross, not a T. His left hand is a closed empty fist.")


# marker v3 (after v1/v2 gave 1 clear marker in 5: magenta handles, crosses, magenta on both sides): one green stick
# like the step-A rod (4 clean rods in 4), but long on the thumb side and only a short stub under the little finger.
# Code reads the blade end as the long end (cut.read_long_end), refusing a stick that is not clearly lopsided.
def marker3(blade_dir: str) -> str:
    return (f"His right fist, the fist of the arm nearer to the viewer, firmly grips one thin straight stick of pure "
            f"bright green, a little thinner than his thumb, the same thickness everywhere, that passes straight "
            f"through the closed fist. The stick is not centred in the fist: on the thumb and forefinger side it "
            f"sticks out long, about three times as long as the fist is wide; on the little-finger side only a short "
            f"stub sticks out, about half as long as the fist is wide. His four fingers wrap around the stick and his "
            f"thumb closes over them. The long end points {blade_dir}. It is one single straight stick, not a cross "
            f"and not two pieces. No sword, no blade, no crossguard, no other object. His left hand is a closed "
            f"empty fist.")


# marker v1 made thin (v1 gave the one clean follow-through hand, grip_followb_s611, but 50 px thick)
def marker4(blade_dir: str) -> str:
    return marker(blade_dir).replace("as thick as a sword handle and the same thickness everywhere",
                                     "very thin like a pencil, much thinner than his thumb, the same thickness "
                                     "everywhere")


MARKERS = {"grip": marker, "grip2": marker2, "grip3": marker3, "grip4": marker4}
FAR_C = FAR[0].upper() + FAR[1:]
GRIP_POSES = {
    # wind-up: right forearm upright, fist beside/above his head, blade back; back of the hand toward us (turn ~ +45..90)
    "wind": (f"coiled to swing a sword: three-quarter view facing right with his chest still turned toward the viewer, "
             f"a wide low stance with both knees bent, his weight back on the rear leg, his upper body leaning back "
             f"and twisted so his near shoulder is pulled back; {NEAR} is raised: the elbow out to the left at the "
             f"height of his shoulder, the forearm standing straight up, the fist beside and a little above his head "
             f"on the left side of his head, toward his back, never in front of his face. We see the back of his right "
             f"hand: the flat back of the hand and the row of four knuckles face the viewer, the fingertips are hidden "
             f"behind the rod. His thumb is at the left end of the fist, toward his back, where the green part comes "
             f"out; the magenta stub comes out at the right end of the fist, under his little finger, toward his head. "
             f"{FAR_C} reaches forward and low in front of his belly with a relaxed fist for balance; a focused, "
             f"determined face with gritted teeth",
             "up and back to the upper left at about 45 degrees, behind his head, like a sword cocked over his right "
             "shoulder ready to swing"),
    # wind-up, level (after grip2_wind_s631: "up and back at 45" drew a second, diagonal stick beside a level one)
    "windl": (f"coiled to swing a sword: three-quarter view facing right with his chest still turned toward the "
              f"viewer, a wide low stance with both knees bent, his weight back on the rear leg, his upper body "
              f"leaning back and twisted so his near shoulder is pulled back; {NEAR} is raised: the elbow out to the "
              f"left at the height of his shoulder, the forearm standing straight up, the fist beside and a little "
              f"above his head on the left side of his head, toward his back, never in front of his face. The back "
              f"of his right hand faces the viewer: we see the flat back of the hand, the back of the wrist and the "
              f"row of four knuckles along the top of the fist; the insides of his fingers and his fingertips face "
              f"away from us and are hidden. His thumb is at the left end of the fist, toward his back, where the "
              f"green end comes out; the magenta stub comes out at the right end of the fist, under his little "
              f"finger, toward his head. {FAR_C} reaches forward and low in front of his belly with a relaxed fist "
              f"for balance; a focused, determined face with gritted teeth",
              "straight back to the left, level, behind the back of his head"),
    # follow (b), deeper (after grip_followb_s611: hand right, but he stood upright)
    "followb2": (f"the follow-through after a big downward sword cut: three-quarter view facing right with his chest "
                 f"turned toward the viewer, a very deep forward lunge, the front knee bent far over the front foot, "
                 f"the back leg stretched long and straight behind him with the back heel lifted, his hips low, his "
                 f"whole upper body thrown forward and down over the front knee, his head forward and low; {NEAR} has "
                 f"swung down and through and hangs straight down from the shoulder in front of his body, the "
                 f"forearm pointing straight down, the fist low in front of his front shin. We see the back of his "
                 f"right hand: the flat back of the hand and the row of knuckles face the viewer, the fingertips are "
                 f"hidden behind the stick. His thumb is at the front end of the fist, toward the right, where the "
                 f"long end comes out; the short stub comes out at the back end, under his little finger, toward the "
                 f"left. {FAR_C} is flung back behind him with a bent elbow and a clenched fist at the height of his "
                 f"waist; a hard, focused face, teeth gritted, eyes still on the enemy",
                 "down and forward to the lower right at about 45 degrees, less steep than his forearm, its tip a "
                 "little above the ground in front of his front foot"),
    # follow (b), flatter blade (after grip_followb_s611: hand right, but with the real, longer sword at game size the
    # tip went into the ground): fist at knee height, the blade only a little below level, tip well above the ground
    "followb3": (f"the follow-through after a big sword cut: three-quarter view facing right with his chest turned "
                 f"toward the viewer, a deep forward lunge, the front knee bent over the front foot and the back leg "
                 f"stretched long behind him, his upper body leaning forward over the front knee; {NEAR} has swung "
                 f"down and through and hangs down from the shoulder in front of his body, the forearm pointing down "
                 f"and a little forward, the fist in front of his front knee at the height of the knee. We see the "
                 f"back of his right hand: the flat back of the hand and the row of knuckles face the viewer, the "
                 f"fingertips are hidden behind the rod. His thumb is at the front end of the fist, toward the right, "
                 f"where the green part comes out; the magenta stub comes out at the back end, under his little "
                 f"finger, toward the left. {FAR_C} is drawn back with a bent elbow and a clenched fist beside his "
                 f"far hip; a hard, focused face, teeth gritted, eyes still on the enemy",
                 "forward to the right and only a little down, about 20 degrees below level, its tip well above the "
                 "ground, far in front of his front foot"),
    # follow (c), step A3: the cut has gone on past the strike. With the real sword (655 px from grip to tip) a blade
    # 45-50 degrees below level keeps its tip on the road only if the fist is about 390+ px above the soles, so the
    # fist is placed by a body landmark (pulled in to his near hip, belt height), never by "the tip on the ground"
    # (the stick is a third of the sword: that wording pulls the fist down to the shin, as followb2 did).
    "followc": (f"the end of a big downward sword cut: three-quarter view facing right with his chest turned toward "
                f"the viewer, a forward lunge with the front knee bent over the front foot and the back leg stretched "
                f"out behind, his upper body leaning forward over the front knee, his weight low and forward; {NEAR} "
                f"has chopped all the way down and is pulled back in close to his body: the elbow is bent a little and "
                f"sits in front of his near hip, the forearm points straight down, and the fist is close beside his "
                f"near hip, just below his belt, in front of the top of his front thigh. We see the back of his right "
                f"hand: the flat back of the hand and the row of knuckles face the viewer, the fingertips are hidden "
                f"behind the stick. His thumb is at the front end of the fist, toward the lower right, where the green "
                f"part comes out; the magenta stub comes out at the back end, under his little finger, toward the "
                f"upper left. {FAR_C} is thrown back behind him for balance, the elbow bent and the clenched fist out "
                f"behind his back at the height of his waist; a hard, focused face, teeth gritted, eyes still on the "
                f"enemy",
                "forward and steeply down to the lower right at about 50 degrees below level, in front of his front "
                "thigh and knee"),
    # follow (d), after followc_s731-734 (fist 279-357 px above the soles, T -24..-33): the fist must sit about 390+ px
    # above the soles for T -43, so the body is less sunk (upright torso over a bent front knee) and the fist is up at
    # the top of the thigh, close to the body; the stick asked steeper than wanted (Qwen draws it flatter).
    "followd": (f"the end of a big downward sword cut: three-quarter view facing right with his chest turned toward "
                f"the viewer, feet wide apart, the front knee bent over the front foot and the back leg stretched "
                f"behind him, his upper body upright over his hips, his weight forward; {NEAR} has chopped all the way "
                f"down and hangs straight down from his shoulder close beside his body, the forearm vertical, the fist "
                f"held high against the top of his front thigh, just below his belt, close to his hip. We see the "
                f"back of his right hand: the flat back of the hand and the row of knuckles face the viewer, the "
                f"fingertips are hidden behind the stick. His thumb is at the lower front end of the fist, where the "
                f"green part comes out; the magenta stub comes out at the upper back end, under his little finger. "
                f"{FAR_C} is thrown back behind him for balance, the elbow bent and the clenched fist out behind his "
                f"back at the height of his waist; a hard, focused face, teeth gritted, eyes still on the enemy",
                "steeply down and forward, about 60 degrees below level, close in front of his front shin, much "
                "steeper than his thigh"),
    # follow (e), after followd_s741-743 (a hanging arm puts the fist 250-360 px up): the arm bent and pulled in, the
    # elbow at his side at the height of his lower ribs and the forearm pointing down, so the fist sits at the belt,
    # about 400+ px above the soles, clearly closer to his body than at the strike
    "followe": (f"the end of a big downward sword cut, the sword pulled back in: three-quarter view facing right with "
                f"his chest turned toward the viewer, feet wide apart, the front knee deeply bent over the front foot "
                f"and the back leg stretched behind him, his hips low, his weight forward; {NEAR} is bent sharply and "
                f"pulled in tight: the upper arm pressed against his side, the elbow beside his lower ribs, the forearm "
                f"pointing down and a little forward, and the fist held tight against the front of his belt, right "
                f"beside the buckle. We see the back of his right hand: the flat back of the hand and the row of "
                f"knuckles face the viewer, the fingertips are hidden behind the stick. His thumb is at the lower "
                f"front end of the fist, where the green part comes out; the magenta stub comes out at the upper back "
                f"end, under his little finger. {FAR_C} is thrown back behind him for balance, the elbow bent and the "
                f"clenched fist out behind his back at the height of his waist; a hard, focused face, teeth gritted, "
                f"eyes still on the enemy",
                "steeply down and forward from his belt, about 50 degrees below level, its far end just above the "
                "road in front of his front foot"),
    # follow (a): the swing carried through: arm swept down and back past the hip, blade trailing low behind
    "followa": (f"the follow-through after a big sword swing that went all the way through: three-quarter view facing "
                f"right with his chest turned toward the viewer, a deep forward lunge with the front knee bent and the "
                f"back leg stretched out behind, his upper body leaning forward; {NEAR} has swept down and back past "
                f"his near hip and is stretched out behind him, pointing back and down to the lower left at a shallow "
                f"slant, the fist behind his hip at the height of his thigh. We see the back of his right hand: the "
                f"flat back of the hand and the row of knuckles face the viewer, the fingertips are hidden behind the "
                f"rod. His thumb is at the lower end of the fist, where the green part comes out; the magenta stub "
                f"comes out at the upper end, under his little finger. {FAR_C} is bent with a relaxed fist held in "
                f"front of his chest; a hard, focused face, teeth gritted, eyes still on the enemy",
                "steeply down and back toward the ground behind his back foot, steeper than his arm, its tip above "
                "the ground"),
    # follow (b): the cut carried through low in front: forearm hanging down, blade down and forward (less steep)
    "followb": (f"the follow-through after a big downward sword cut: three-quarter view facing right with his chest "
                f"turned toward the viewer, a deep forward lunge with the front knee bent far over the front foot and "
                f"the back leg stretched out behind, his upper body bent forward over the front knee; {NEAR} has swung "
                f"down and through and hangs straight down from the shoulder in front of his body, the forearm "
                f"pointing straight down, the fist low in front of his front knee. We see the back of his right hand: "
                f"the flat back of the hand and the row of knuckles face the viewer, the fingertips are hidden behind "
                f"the rod. His thumb is at the front end of the fist, toward the right, where the green part comes "
                f"out; the magenta stub comes out at the back end, under his little finger, toward the left. {FAR_C} is "
                f"drawn back with a bent elbow and a relaxed fist beside his far hip; a hard, focused face, teeth "
                f"gritted, eyes still on the enemy",
                "down and forward to the lower right at about 45 degrees, less steep than his forearm, its tip above "
                "the ground in front of his front foot"),
}


def grip_prompt(key: str, kind: str = "grip") -> str:
    pose, blade = GRIP_POSES[key]
    hero = HERO_SLEEVE.format(sleeve=SLEEVES["short"])
    mk = MARKERS[kind](blade)
    if kind != "grip":
        pose = pose.replace("behind the rod", "behind the stick")
    if kind == "grip3":  # one green stick: no colour words for its ends
        for a, b in (("the green part", "the long end"), ("the green end", "the long end"),
                     ("the magenta stub", "the short stub")):
            pose = pose.replace(a, b)
        assert "magenta" not in pose and "green part" not in pose, pose
    return (f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {pose}. {FLAT} Keep everything else "
            f"exactly the same: {hero}, his head exactly the same size, {STYLE}. {mk} No ground, no shadow, "
            f"no dust, no motion lines, no effects, no text. {RGBA_CLOSE}")


SLEEVES = {
    "short": ("short plain sleeves that end in one neat straight sewn hem halfway down the upper arm, well above the "
              "elbow, so both elbows and forearms are bare"),
    "long": "plain fitted long sleeves down to the wrists, each ending in a neat narrow cuff",
}
HERO_SLEEVE = ("the same young hero with the same face, messy dark brown hair, red headband with two tails, cream shirt "
               "with {sleeve}, brown belt with a square buckle, dark teal trousers and brown boots, the same size and "
               "body proportions, the same colours")


def sleeve_prompt(kind: str) -> str:
    return (f"{RGBA_OPEN} Edit <image1>. Change only the sleeves of his shirt: instead of the rolled-up sleeves his "
            f"cream shirt now has {SLEEVES[kind]}, in the same cream cloth with the same cel shading. Keep everything "
            f"else of <image1> exactly the same: the same hero, the same face, hair, headband, shirt body, belt, "
            f"trousers and boots, exactly the same pose with both arms, both fists and both legs in exactly the same "
            f"place, {STYLE}. No ground, no shadow, no text. {RGBA_CLOSE}")


def bare_prompt(m: str, sleeve: str | None = None) -> str:
    pose, thumb = POSES[m]
    hero = HERO if sleeve is None else HERO_SLEEVE.format(sleeve=SLEEVES[sleeve])
    return (f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {pose}. {FLAT} Keep everything else "
            f"exactly the same: {hero}, his head exactly the same size, {STYLE}. {rod(thumb)} No ground, no shadow, no "
            f"dust, no motion lines, no effects, no text. {RGBA_CLOSE}")


def bare_input() -> np.ndarray:
    """The game's bare hero, alpha-trimmed, scaled to HERO_H, feet at FEET_Y, centred on a transparent canvas."""
    im = load_rgba(RAW / "char_hero_bare.png")
    al = im[..., 3] > 40
    ys, xs = np.nonzero(al)
    fig = Image.fromarray(im).crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    s = HERO_H / fig.height
    fig = fig.resize((round(fig.width * s), HERO_H), Image.LANCZOS)
    can = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    can.alpha_composite(fig, ((CANVAS - fig.width) // 2, FEET_Y - HERO_H))
    a = np.array(can)
    a[a[..., 3] == 0, :3] = 0
    return a


def job(name: str):
    """-> (prompt, [ref arrays])"""
    if name.startswith("bare_"):
        return bare_prompt(name[5:]), [bare_input()]
    if name.startswith("sleeve_"):  # sleeve_short / sleeve_long: the base drawing with another sleeve
        return sleeve_prompt(name[7:]), [bare_input()]
    if name.startswith("grip"):  # grip[2]_<pose key>: a v3 pose (two-colour marker) of the kept short-sleeve base
        kind, key = name.split("_", 1)
        base = load_rgba(REDO / f"{json.loads((REDO / 'moments.json').read_text())['sleeve_short']}.png")
        return grip_prompt(key, kind), [base]
    if name.startswith("plain_"):  # plain_<sleeve>_<moment>: a pose edit of the kept sleeve base (moments.json)
        _, sleeve, m = name.split("_")
        base = load_rgba(REDO / f"{json.loads((REDO / 'moments.json').read_text())['sleeve_' + sleeve]}.png")
        return bare_prompt(m, sleeve), [base]
    import pieces  # armor edits
    return pieces.job(name)


def main():
    c = Comfy()
    c.check()
    for arg in sys.argv[1:]:
        name, seeds = arg.split(":")
        prompt, refs = job(name)
        up = [c.upload(f"moments_{name}_ref{i}.png", png_bytes(r)) for i, r in enumerate(refs)]
        for s in [int(x) for x in seeds.split(",")]:
            out = REDO / f"{name}_s{s}.png"
            if out.exists():
                continue
            t = time.time()
            out.write_bytes(c.generate(prompt, up, s))
            sec = round(time.time() - t)
            with open(REDO / "runs.jsonl", "a") as f:
                import pieces
                f.write(json.dumps({"file": out.name, "sec": sec, "utc": datetime.now(timezone.utc).isoformat(
                    timespec="seconds"), "prompt": prompt, "refs": list(pieces.LAST_REFS)}) + "\n")
            print("QWEN", out.name, sec, "s", flush=True)


if __name__ == "__main__":
    main()
