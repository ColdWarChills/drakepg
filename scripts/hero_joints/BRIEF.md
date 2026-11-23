# Brief: a new hero drawn for joints, and his sword arm tested with three kinds of joint

**Goal (one line):** draw a new hero made to be cut onto joints, build his sword arm three ways (stiff parts; a sleeve that bends at the elbow; a soft arm), and render the arm swinging a sword so the three kinds can be compared in motion.

**Why (this is the whole point):** the designer has rejected three jointed heroes for the same reasons: the sword hand ("the hand is just bad as always"), the near elbow (a knot where a rolled sleeve ended on the joint) and stiffness ("a stiff paper doll"). The last attempt (drawn poses switched in one frame) was rejected for snapping and for sizes that changed between poses. Their words for this step: "try new character that is generated for this. maybe some sprites should be stretched at joints, test couple of such types maybe that would fix and help the flow". A "partly" from you or the advisor is a no. Nothing reaches the designer unless the orchestrator passes it, so report failures plainly: "this kind failed, here is why" is a good result. This is a test, not a system: hand-set numbers are fine, write them down.

**Where:** everything by absolute path under `C:\Users\Epic\Desktop\dragonfab\.claude\worktrees\unity-port` (W below). New files only in `W\scripts\hero_joints\`, `W\art\redo\hero_joints\` (drawings, parts) and `W\art\tests\hero_joints\` (sheets, clips). Everything else is read-only (copy what you need from `W\scripts\hero2d\`, `W\scripts\moments\`, `W\scripts\momentclip\`). Do not edit `docs/PLANS.md`, CLAUDE.md or memory files; no git commit, stash, checkout or reset; nothing in `W\unity\`. Never overwrite a kept picture: a new try gets a new name.

**Tools on this PC:** Python = `"C:/Users/Epic/Desktop/Animation Workflow/.venv/Scripts/python.exe" -I -B <script>` (plain `python` is a stub; write scripts to files, heredocs with apostrophes fail). ffmpeg = `C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0-full_build/bin/ffmpeg.exe` (no `-vsync`; `drawtext` crashes, write labels with PIL). ComfyUI with Qwen-Image 2.1 at http://127.0.0.1:8188 is free and you are the only one on it; its rules: `C:\Users\Epic\.claude\projects\C--Users-Epic-Desktop-dragonfab\memory\comfyui-qwen-setup.md`; a working caller: `W\scripts\hero2d\qwen_pose.py`. Pictures you open: at most 1500 px on the long edge and 1.05 MP; make contact sheets instead of opening many; aim for under 30 looks in all.

**Read first:** `W\scripts\hero2d\HOWTO.md` (the last jointed try: cutting pose, cutter, rig, known flaws), `W\art\tests\moments\HOWTO.md` and `W\scripts\momentclip\layers.py` (how the sword sits in a drawn fist: body, sword, a copy of the fist on top), `W\art\redo\moments\layers_A3\` with `moments_A3.json` (three whole-figure drawings of the old hero at ready, wind-up and strike, with clean closed sword fists the designer did not fault).

## Part 1: the new hero
One Qwen drawing of a hero made for cutting, with `W\art\raw\char_hero_bare.png` as the style and identity reference: same face, hair, red headband, colours, sturdy cartoon proportions and big fists; the game's look is flat cel colours with hard shadows, NO ink outlines, darker inner lines only. He faces screen-right in the same 3/4 front view. Cutting pose: arms straight and held clear of the body, legs straight and apart, nothing overlapping, fists closed, no weapon, flat purple background, no ground shadow, no rim or glow (the thin cream rim is added by code over the finished figure; measure its colour and width on `char_hero_bare` and match it: the last try's was too thick and too even; if the drawing comes with a rim anyway, take it off the parts).
Clothes that hide joints: a long plain sleeve to the wrist as a simple tube, loose enough to be about as thick as the old hero's bare forearm, ending in a plain cuff the fist joins under; no folds, seams, bands or shadow edges near the elbow, the shoulder or the knee; a belt or sash wide enough to hide a split between chest and hips; a collar that hides the neck join; plain trousers; plain boots whose top hides the ankle (no big clunky cuffs).
Make 6-10 tries, pick one by these rules on one contact sheet, keep prompt and seeds in `W\scripts\hero_joints\HOWTO.md`. If 12 tries give nothing usable, stop and report.

## Part 2: the sword arm, three kinds
The sword is in HIS right hand, which is the near arm (he faces screen-right with his chest toward us; never reason in picture sides). Cut only what the test needs: the near sleeve, the fists, the far arm, and the body as a backdrop (paint the body in where the arm covered it; flaws that stay hidden behind the arm do not matter). The far arm must not stick out as in the cutting pose: hold it in a natural guard near the body with small motion.
Build the near arm three ways with the same pivots and lengths:
- **A "stiff parts"**: upper and lower sleeve as two rigid pictures with round ends in the flat cloth colour, turning about the elbow.
- **B "bends at the elbow"**: the whole sleeve as ONE straight picture bent by code along a round arc over a short band at the elbow (about one sleeve width). Cross-sections stay perpendicular to the centre line so the tube keeps its width; do not blend two matrices, that pinches.
- **C "soft arm"**: as B with the bend spread over about half the arm, like a rubber hose.
On B and C also try stretch: the arm 10-25% longer for the 1-3 fastest frames.
In Unity this must be doable with plain sprites plus a small strip mesh per limb (no new package).
Fists are never bent or stretched: rigid drawn pictures, swapped per grip. First try the three clean fists from `layers_A3` (cut at the wrist, scaled to the new hero's fist, each with its own grip spot and blade angle from the json); draw new ones (a whole-figure pose edit of the new hero, then cut the fist) only if those do not fit. Sword: `W\art\raw\wpn_training_sword.png`, between the body and a copy of the fist's front. No cut edge may show at the wrist at any wrist angle used.

## Part 3: the swing
One sword cut, 60 fps, about 1.5 s, played twice with a short idle between: ready -> raise to the wind-up (ease, small overshoot) -> a held wind-up that keeps drifting slightly -> the cut in 4-5 frames -> overshoot and settle -> a held strike that keeps drifting -> back to ready, eased. Take the three key arm poses from the `layers_A3` drawings as arm angles relative to the chest (upper arm, elbow bend, blade angle). Move the wrist along a smooth arc through the key spots plus one or two in-between spots and solve the elbow from it; never switch a pose in one frame; the fist swaps happen inside the fast frames; the sword drags a little behind the hand at the start of the cut and overshoots at its end.
The body is not a statue: the chest leans a few degrees with the swing, the near shoulder shifts with the reach, the head counters a little, the headband tails lag if they are cheap to cut.
Frame: from just below the belt up, with room for the whole swing, on a crop of `W\art\raw\bg_fen_road.png`, the hero at his drawing's own size (no upscaling).

## Checks, in this order (crops from full-size frames)
1. **Hand:** each fist on the arm at its key frame at 4x, beside the hands the designer approved (`W\art\tests\sheets\layered_hero_hands.webp`). Passes only if it is as good as those: a whole closed fist with the handle through it; no mitten, no stray colour. Grip rule (angles on screen; F = forearm direction elbow to wrist, T = direction the blade leaves the fist): back of the hand showing: T is counter-clockwise from F by 25-120 degrees; palm side showing: clockwise by 25-120. The three A3 fists measured about 41 (ready), 85-90 (wind-up) and 20 (strike, accepted).
2. **Elbow:** each kind at bends 0, 45, 90 and 130 degrees at 3x: it reads as a bent sleeve; no knot, lump, notch, gap or double edge; the width holds.
3. **Motion:** per kind, a strip of EVERY frame from the last wind-up frame to the settled strike and every 3rd frame elsewhere: nothing jumps, no pop when the elbow passes straight, the swaps cannot be seen, the arm does not read as a stick pinned to the body.
4. **Look:** the ready pose beside `char_hero_bare` at the same height: the same game, the same hero; the rim thin and cream.

## Deliver in `W\art\tests\hero_joints\`
`character_pick.png`; per kind `arm_A.mp4`, `arm_B.mp4`, `arm_C.mp4` (60 fps) and a half-size `.webp` of each; `arm_kinds.mp4` + `.webp` with the kinds side by side, labelled in plain words ("stiff parts", "bends at the elbow", "soft arm"); `fists_4x.png`, `elbows_3x.png`, `motion_strips.png` (one per kind if needed), `look.png`; `numbers.md` (Qwen runs, PC minutes, hand-set numbers). Cap: 30 Qwen runs.

## Report (at most 250 words)
Per kind a yes or no on hand, elbow, motion and look, each with its reason; which kind you would keep and why; what failed; the advisor calls and what each changed; runs and minutes; the paths of at most 4 pictures the orchestrator should look at first.

## Additions
- Pick a drawing whose arms are as long and as thick as the old hero (upper arm about 0.145 of his height, forearm about 0.136); the last try read as a thin, short arm.
- Keep scripts/hero_joints/HOWTO.md current as you go (what is done, what is picked), so a run that is cut off can be picked up.
