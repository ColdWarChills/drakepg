# The hero as drawn pieces (hero2d): bare pieces, stance, bend tests, first armor

The idea: draw the hero once in a "cutting pose" (arms out, legs apart, nothing overlapping) and cut that drawing into
pieces on joints. Then an armor is the same drawing with only the clothing changed, cut by the same zones, so it
follows every pose.

Python: `PY="C:/Users/Epic/Desktop/Animation Workflow/.venv/Scripts/python.exe"`, always run with `-I -B`.
Step 1b (current) files: `art/redo/hero2d/r2/` (Qwen tries, `pieces/` + `pieces.json`, `scale_mail/` +
`scale_mail.json`). Sheets: `art/tests/hero2d/r2/`. Step 1's files (`art/redo/hero2d/`, `art/tests/hero2d/`) are kept
as they were.

## Rebuild (about 3 min of scripts plus Qwen time)
1. Cutting-pose drawing (one reference only: char_hero_bare composited on flat purple on a 1536x1536 canvas):
   `$PY -I -B scripts/hero2d/qwen_pose.py cut2 21,22` -> `r2/cut2_s<seed>.png` (about 80-95 s each). Picked `cut2_s21`
   (arms 45 out with a gap, neat rolled bands, legs as an upside-down V, both boots to the right). The prompt `fix`
   (step 1's drawing with details fixed, pose kept) was tried too and not used.
2. Cut: `$PY -I -B scripts/hero2d/cut.py` (default `cut2_s21` -> `pieces`). Joint points and zone lines in cut.py were
   read by eye with `look.py` (grid over a zoomed crop); a new drawing needs them read again.
   Clean-up done once at cut time (cutlib): round joint ends of the limb's own section (`round_joint`), a whole round
   shoulder brought by the near sleeve (a disc about the pivot, so no wedge opens when the arm turns), the far sleeve
   completed under the torso (it swings clear), the torso cut on a plain trunk line with a drawn-colour line on it,
   hips and knees flattened to one cloth colour and their outlines smoothed, a soft feather on the fist's cut, purple
   spill fixed only at the silhouette, and solid drawn colour everywhere well inside the figure.
3. Sheets: `stance.py` (stance_2x, game_1280, judge_3x, stance_overlay, `r2/stance_master.png` at 960 px),
   `arm_test.py`, `leg_test.py`, `far_arm_test.py`, `pieces_sheet.py`, `rest_check.py <out.png>`.
   `arm_test.py` / `far_arm_test.py` take `[kit json] [out name]` (arm_test also `[elbow folds]`).

## The rig (`rig.py`)
- Angles in degrees, y down, 0 = screen right, 90 = down. Pose = waist position + absolute angles per bone.
- Elbow: forearm = upper arm - flex. Knee: shin = thigh + flex. `arm_ik` / `leg_ik` / `two_bone`.
- Sword: blade = forearm - wrist, wrist kept in 30..115; the fist turns with the sword about the wrist; the sword is
  drawn under the near fist (his right hand = the near arm).
- The near shoulder rises `SHOULDER_LIFT` (16 kit px) with the arm above the horizontal.
- Layers, back to front: far leg, near leg, hips, far arm (sleeve, forearm, cuff, fist), torso, [far plate], head,
  near arm (sleeve, [near plate], forearm, cuff band again, sword, fist).
- Armor plates turn `PAD_FOLLOW` (0.35) of the near upper arm's turn (a plate sits on the shoulder, it does not swing
  under the arm); the far plate, seen edge-on, stays put (`FAR_PAD_FOLLOW` 0).
- Rim: one pass over the finished figure, #f9de7a, 1.4 px at 720p on top and sides, fading on edges that face down
  (measured on char_hero_bare_sword_ready).
- The stance (`stance.py`): joint spots read off char_hero_bare_sword_ready, reached with the rig's IK; the blade at
  the game's sword_ready angle.

## One armor by this route (done once: Scale Mail, body armor)
1. Edit: `$PY -I -B scripts/hero2d/qwen_pose.py mail 31,32 art/redo/hero2d/r2/cut2_s21.png` ("Edit <image1>. Change
   only the clothing on his upper body ..."). 2 runs, both kept the pose.
2. Pose check: `$PY -I -B scripts/hero2d/armor_check.py mail_s31 mail_s32` -> `armor_overlay.png`: overlap and
   shift of head, headband, both forearms with fists, trousers and boots against cut2_s21. Both tries: overlap
   0.96-0.99 (headband 0.83-0.86, its tails flutter), shift at most 1.5 px. Picked `mail_s32` (bigger, simpler
   scales that still read at 480 px; s31's small scales turn to noise).
3. Cut: `$PY -I -B scripts/hero2d/cut.py mail_s32 scale_mail` -> `r2/scale_mail/` + `r2/scale_mail.json`. Same zones and
   joint points. Armor-only parts in cut.py: `PADS[<drawing>]` = an outline per shoulder plate (15 + 13 points read
   by eye) and the padding colour shown under the plates; the near plate and the far plate become their own pieces
   (`n_pad`, `f_pad`); the sleeve under a plate keeps only its round top; the collar under the chin goes to the torso.
4. Sheets: `$PY -I -B scripts/hero2d/arm_test.py art/redo/hero2d/r2/scale_mail.json armor_bend_3x.png 45,120`,
   `far_arm_test.py <same json> armor_far_arm_3x.png`, `armor_sheets.py` (armor_stance_2x, armor_judge_3x,
   candidate_2x).

Real cost of Scale Mail: 2 Qwen runs (about 3 min); hand-set numbers: 28 outline points (the two plates), 1 padding
colour, the collar rule (below y 415, right of x 705) and the two plate follow factors; about 30-40 minutes of cutter
work the first time (estimate; plates as pieces, slivers, the green tint on steel). The code is now general, so the
next armor of this shape should be the 2 runs, about 30 points and 20-30 minutes.
The armor kit is a whole second kit (head, fists, legs cut from the edited drawing too); armor pieces were not tried
on the bare kit's other pieces.
An armor that hangs below the belt (a skirt, tassets) still needs its own pieces on the hips and a lunge check.

## Known flaws (step 1b)
- Bare: limb lengths are not corrected to the drawn hero (measured upper arm 0.132 H vs about 0.145 drawn, forearm
  0.128 vs about 0.136; legs hip to ankle 0.451 vs 0.445, close); the far thigh rotated back shows the drawn thigh's bulge at the hip; headband tails shorter than drawn;
  a small sliver under the far cuff; a small shoulder corner when the far arm is far back.
- Armor: with the arm raised the sleeve comes out from behind a still plate (reads as a plate, but nothing joins
  them); a faint plate-edge sliver beside the far sleeve with the far arm forward and up; with the near arm forward or
  raised a ragged dark patch (the padding under the plate) shows beside the plate, with a spur by the collar; the
  plates turn with the arms only, not with the torso (fine at the stance's 5 degrees, not in a strong lean).
