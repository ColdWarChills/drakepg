# Brief, round 2: the new hero's sword arm on the sleeve that bends, fixed where round 1 failed

W = `C:\Users\Epic\Desktop\dragonfab\.claude\worktrees\unity-port` (work by absolute path).

**Goal (one line):** one clip of the new hero (belt up) cutting with his sword, the near arm on the sleeve that bends at the elbow, good enough to show the designer: a hand that never pops, an arm that reads as a drawn arm in front of the body, a body that moves with the cut.

**Why:** round 1 (this folder, finished 2026-11-22) solved the elbow (kind B: the width stays within 3% at 0-130 degrees) and failed the rest on the orchestrator's look, so nothing was shown:
1. The hand pops where one drawn fist is swapped for another in slow frames (swing frames 15->16 and 85->86). The three borrowed fists differ in size and shape and carry purple and cream fringe; the sleeve ends in a straight cut instead of a cuff.
2. The arm reads as a flat strip laid on the body: a cream sleeve on a cream tunic, one flat colour, a cast shadow too faint to see (darkness 0.2).
3. The body barely moves (lean -3.5..6 degrees, shoulder -5..12 px); the sword drags 3 degrees and overshoots 1; the blade tip steps up to 1028 px in one frame with nothing to carry the eye.

The designer has rejected three jointed heroes for the hand ("the hand is just bad as always"), the elbow and "a stiff paper doll", and the last clip for snapping and for sizes that changed between poses. A "partly" from you or the advisor is a no; "it failed, here is why" is a good result. This is still a test, not a system: hand-set numbers are fine, write them down.

**Read first:** `HOWTO.md` (what exists and what is picked), `BRIEF.md` (round 1's brief: its Where, Tools on this PC, grip rule and checks all still hold) and `W\art\tests\hero_joints\numbers.md`; then round 1's code (`rig.py`, `meshlib.py`, `swing.py`, `keys.py`, `cut.py`, `fists.py`). Reuse it: copy a script to a new name (`*_r2.py`) rather than changing what made round 1's pictures. Kinds A and C are dropped; build only B.

## 1. Hand: one fist, the sword fixed in it
- One drawn fist for the whole swing, the sword fixed in it as one rigid unit that turns about the wrist under the cuff. Nothing is swapped, so nothing can pop and the blade angle is continuous by construction.
- The three A3 fists all show the back of the hand and differ only in their drawn turn (ready 44.5, wind-up 96.8, strike 21.5). Take the ready fist (the middle one) unless another reads better on the arm. Change the wind-up key so its turn stays inside what that fist can hold: about 60-65 in the hold (blade up and back rather than flat behind the head; the blade must stay clear of his face). Whole range used, drag and overshoot included: about 10-78. Look at the fist on the arm at both extremes at 4x; if it looks broken there, narrow the range until it holds.
- Fallback only if one fist cannot hold the wind-up: two fists, swapped only inside the two fastest frames of the cut, same knuckle size and colours, the grip spot and the blade angle identical on both sides of the swap.
- Clean the fist: no purple or old cream fringe (erode the edge 1-2 px and repaint the edge colours from inside), skin tones matched to the new hero's own fists on `cutj3_s123`, as sharp as the sleeve. If it stays soft or off-style, draw a new one: a Qwen whole-figure edit of `cutj3_s123` holding the training sword in his right hand at the ready, then cut the fist.
- A real cuff: a short band at the sleeve's end, a little wider than the sleeve there and wider than the fist's wrist, with its own darker inner line and a small hard shadow on the fist. The fist's wrist end stays under it at every wrist angle used. No straight cut edge anywhere.
- The sword changes between in front of and behind the body or head only on a frame where the blade overlaps neither.

## 2. Look: an arm that stands out from the body
- Outfit: a plain sleeveless vest or jerkin over the cream shirt, ending under the belt, clearly darker than the sleeve. Make it as a Qwen edit of `cutj3_s123` ("change only": same pose, size, face, hair, headband, long cream sleeves, trousers, boots), in two or three colours (dark teal like the trousers, warm brown leather), 2 seeds each. Pick on one contact sheet at game size (hero about 480 px tall, as on a 1440p screen) beside `W\art\raw\char_hero_bare.png`: the one where the sleeve parts best from the torso and he is still clearly the game's hero. If an edit shifts the figure or changes the sleeves, take only the torso from it. If no edit is usable, recolour the torso by code with the trousers' own colour steps (keeping the drawn cel shadows). Re-cut the torso from the pick.
- Shoulder: a round cream shoulder at the top of the sleeve, centred on the pivot and wide enough to cover the vest's armhole at every arm angle used.
- Sleeve, painted into the straight source picture so the bend carries it: tapered, about 66 px wide at the shoulder to about 56 px above the cuff (round 1: 68-72 px throughout; the old bare forearm is 49 px); a hard-edged shadow band along its under side in the shirt's own shadow colour, about a third of the width; darker inner lines on both edges as now. Check the arm's length against the old hero's (top of the shoulder to the wrist about 0.281 of his height): round 1's numbers disagree with each other (the same silhouette, yet 0.84x in `measure.json`). If it really is shorter, lengthen the tube, which is free.
- A clear, hard-edged shadow of sleeve, fist and hilt on the torso and hips, in the vest's own drawn shadow tone, clipped to them. It must read as the game's cel shadow, not as a soft drop shadow.
- The thin directional cream rim as measured in round 1.

## 3. Motion: a body that swings, a sword that follows
- Body, two to three times round 1's amounts: the chest tips back 6-8 degrees on the wind-up and whips to 10-12 forward on the cut; the near shoulder goes up and back about 15 px on the wind-up and 25-35 px forward on the cut; the whole upper body sinks a few px on the wind-up, rises into the cut and settles; the head counters by about half the chest's angle; the far arm moves against the sword arm (back toward the hip as the sword comes down, returning in the settle); a slight squash and stretch of the torso is allowed (at most 4%). Headband tails cut as their own piece, lagging 3-5 frames with overshoot. Holds never freeze: slow breathing, 1-2 px and about 1.5 degrees.
- Sword: in the first one or two frames of the cut the blade lags behind the arm (the turn rises by 10-15); at the end it overshoots more than the arm does (the turn falls to about 8-12) and settles at about 21.5 within 6-8 frames; it drifts a degree or two in the holds.
- The raise is eased in and out over about 20-24 frames with a small overshoot; it must not read as a snap. The way back to ready is eased the same way.
- Stretch: the sleeve 10-20% longer on the one or two fastest frames.
- A smear on the cut's fast frames: the area the blade swept since the frame before, as a flat pale shape with a hard leading edge and a tapering tail, fading over two more frames; drawn under the sword, fist and sleeve so it never hides the hand; no blur. (`W\scripts\momentclip\fx.py` has a slash arc to borrow from.)
- Print the blade tip's step per frame: the steps rise and fall smoothly, with no one-frame spike outside the cut. Report the largest step inside and outside the cut.

## Frame and deliverables
As round 1: from just below the belt up with room for the whole swing, on the Fen Road crop, the hero at his drawing's own size, 60 fps, the swing twice with a short idle between.
In `W\art\tests\hero_joints\r2\` (new outfit tries in `W\art\redo\hero_joints\` under new names):
`arm.mp4` and a half-size `arm.webp`; `fist_4x.png` (the fist on the arm at ready, wind-up, strike and the two extreme wrist angles, beside a crop of the approved hands `W\art\tests\sheets\layered_hero_hands.webp`); `cuff_4x.png`; `look.png` (ready, wind-up and strike at game size beside `char_hero_bare` at the same height); `motion_strip.png` (every frame of the raise and of the cut through the settle, every 3rd elsewhere); `outfit_pick.png`; `numbers.md`. Every sheet at most 1500 px on the long edge and 1.05 MP, so it opens as it is.

## Checks, in this order, on crops from full-size frames
1. **Hand:** as good as the approved hands at every frame of the strip: a whole closed fist with the handle through it; no pop, no fringe, no cut edge; the grip rule holds.
2. **Cuff and elbow:** a sleeve with a cuff; no knot, notch, gap or double edge; the width holds.
3. **Motion:** step through every frame: nothing jumps, no size changes, the body moves, the arm does not read as a strip pinned to the body.
4. **Look:** at game size beside `char_hero_bare`: the same game, the same hero, the arm clearly in front of the body, the rim thin and cream.

## Limits
- Stop rule: if the hand or the look still fails after two tries at it, stop and report plainly; do not keep iterating.
- At most 20 Qwen runs (you are the only one on ComfyUI); aim for under 30 picture looks; keep `HOWTO.md` current by adding a "Round 2" status block (leave round 1's as it is).
- Write only in `W\scripts\hero_joints\`, `W\art\redo\hero_joints\` and `W\art\tests\hero_joints\`. No edits to `docs/PLANS.md`, CLAUDE.md or memory files; no git commit, stash, checkout or reset; nothing in `W\unity\`; never overwrite a kept picture.
- The sword is in his right hand, which is the near arm (he faces screen-right with his chest toward us); never reason in picture sides.
- In Unity this must stay doable with plain sprites plus a small strip mesh per limb (no new package).

## Report (at most 250 words)
A yes or no on hand, cuff and elbow, motion, and look, each with its reason; what failed; the advisor calls and what each changed; Qwen runs and PC minutes; the paths of at most 4 pictures the orchestrator should look at first.
