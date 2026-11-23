# Jointed hero, step 1: a hero drawn for joints, his sword arm in three kinds of joint

Brief: `BRIEF.md` (this folder). Python: `"C:/Users/Epic/Desktop/Animation Workflow/.venv/Scripts/python.exe" -I -B <script>`
run from this folder. Drawings and parts: `art/redo/hero_joints/`; sheets and clips: `art/tests/hero_joints/`.
Frames are written to `$HJ_TMP/<kind>/` (default `art/redo/hero_joints/_frames`).

## Status (keep current)
- DONE Part 1: 10 Qwen runs. `cutj` (s101-104) and `cutj2` (s111-113): an A pose; Qwen turned him to face us, taller
  and slimmer, every time. `cutj3` (s121-123): "change only his clothes" on char_hero_bare's own pose: same face, view,
  size; near arm hangs straight with a gap; far arm already a guard; legs below the test frame. **Picked cutj3_s123**
  (plainest tube sleeve, no rim, collar, cuff). Arm = the old hero's own arm (same silhouette): shoulder pivot to wrist
  258.5 px at H 1028 (0.25 H; brief 0.281 H measured to the top of the shoulder), sleeve 68 px wide (1.4x the old bare
  forearm's 49 px). `measure.py` numbers in `measure.json`.
- DONE cut (`cut.py` -> `parts/` + `parts.json`): near sleeve re-cut as a clean tube (its two edges fitted with straight
  lines along its own two-part centre line; the drawn sleeve stepped out at the elbow = a notch), head (neck extended
  under the collar), torso, far arm, hips (belt and legs). Drawn rim taken off; code adds a directional rim (measured on
  char_hero_bare by `rim_measure.py`: ~4.5 px, on edges facing up and to his back, none facing down/front).
- DONE fists (`fists.py`): the three A3 fists, rim stripped, cut 12 px behind the wrist (the cuff covers it), scale 1.0.
- DONE kinds (`rig.py`, `meshlib.py`): A two rigid pictures with round cloth ends; B fixed radius 0.6 w (band = r x
  bend); C band = half the arm (radius = band / bend). Source sampled along the drawn centre line (one resample).
- DONE checks so far: `fistcheck.py` (fists at 4x beside the approved hands), `elbows.py` -> elbows_3x.png (widths 0.97-
  1.02 for B/C, no flipped triangles, no holes), `sheets.py look` -> look.png.
- DONE clips: `swing.py ABC` (HJ_TMP set to the scratchpad frames folder) -> arm_A/B/C.mp4 + .webp, arm_kinds.mp4 +
  .webp, swing_numbers.json (262 frames per kind, 0 gate fails, 0 flipped triangles). `sheets.py strips ABC` ->
  motion_strip_A/B/C.png + motion_strips.png; `make_numbers.py 17` -> numbers.md; `pick_sheet.py` -> character_pick.png.
- DONE `final_checks.py` -> swaps_4x.png (slow fist swaps at swing frames 15->16 and 85->86, 4x) and look_kinds.png
  (A/B/C at ready and strike beside char_hero_bare, game size).
- Verdicts (all NO for showing): hand no in motion (fists fine at the three keys, but the swaps on the slow raise and
  recovery pop: a different fist shape between two slow frames; purple fringe at the fist's cut). Elbow A no (joint line,
  double edge at 130), B/C yes. Motion: arm arc reads for all, but the blade tip jumps up to 1028 px in one frame, the
  drag is 3 deg and the blade overshoot 1 deg (the arm overshoots 12 deg). Look no for all: cream sleeve on cream tunic,
  arm flat on the body at strike; the three kinds look the same at game size. Body: lean -3.5..6 deg, head -3..1.8 deg,
  shoulder dx -5..12 px. Arm (measure.json): 0.84x the old arm's length, sleeve 1.51x the old forearm (thicker).
- Final advisor check done; report sent. Next step if picked up: fix the slow swaps (swap only in fast frames, or add an
  in-between fist), a sleeve colour that parts from the tunic, sword smear/drag.

## Round 2 (brief `BRIEF_round2.md`): kind B only, fixing hand, look and motion
Outputs in `art/tests/hero_joints/r2/`; parts in `art/redo/hero_joints/parts_r2/` + `parts_r2.json`; Qwen log
`runs_r2.jsonl`. Round 1's files are untouched (every r2 script and output has its own name).
- Arm length kept (L 258.5): `r2_armlen.py` overlay shows the new arm on the old hero's own arm.
- Outfit: `qwen_r2.py vest_teal|vest_brown 2xx` (edits of cutj3_s123, aligned within 1 px); `r2_outfit_sheet.py` ->
  `outfit_pick.png`. Picked **r2_vest_teal_s202** (torso only). `qwen_r2.py fist 221,222`: not used.
- `cut_r2.py r2_vest_teal_s202` -> parts_r2 (torso from the vest, carried under the belt; hips cut on the belt's top
  edge; tails own piece; far arm behind the torso; purple left-overs removed).
- `fist_r2.py`: the one fist (A3 ready), recoloured, smoothed, round wrist stub. `rig_r2.py`: painted sleeve + cuff,
  round shoulder, body transforms, cast shadow, smear. `r2_stills.py [tag]` -> fist_4x, cuff_4x, look.
- `swing_r2.py dry` (numbers only) / `swing_r2.py render` (HJ_TMP = frames folder; ~10 CPU min) -> arm.mp4, arm.webp,
  swing_r2.json. `r2_sheets.py <frames dir>` -> motion strips. `make_numbers_r2.py <minutes>` -> numbers.md.
- Tries: try 1 (raise carried arm and pommel across the face) kept as `*_try1`; try 2 = the backswing route
  (a1 unwrapped +360), now `arm.mp4` / strips. An armhole strip over the shoulder (`ARMHOLE` in rig_r2) was worse
  (wavy line, dark notch) and is off.
- Status 2026-11-22: hand yes (one fist, rigid with the sword, no pop, cleaned); cuff and elbow yes (widths
  0.97-1.03, no flips); look NO after two shoulder tries (landed: darker vest parts the arm, tapered sleeve with
  hard band, rim; failed: the round shoulder reads as a pinned tube end in the backswing, clip ~26-34, and the cel
  cast shadow is faint on the dark vest); motion NO (body, lag, overshoot, eased raise, stretch, smear and smooth tip
  steps are in, but the sleeve and hilt cross the face on 1-2 fast cut frames, the blade leaves the frame bottom,
  and there is a 12 px tip step at each idle/swing seam: PCHIP end slopes; flat end keys only moved it).

## Round 3 (brief `BRIEF_round3.md`): the shoulder (stopped at the gate, no clip)
Outputs in `art/tests/hero_joints/r3/`; the cap part in `art/redo/hero_joints/parts_r3/` + `parts_r3.json`; Qwen log
`runs_r3.jsonl`. Round 2's scripts and files are untouched (`rig_r3.py`, `swing_r3.py`, `r3_sheets.py` are copies).
- `qwen_r3.py cap 301,302`: edits of r2_vest_teal_s202 with short cap shoulders (s302 used). `r3_look_shoulder_src.py
  [key]`: 3x grid crop of a drawing's near shoulder (+ what the edit changed). `cap_r3.py`: cuts the near cap of s302
  (left of a raglan seam, teal only, tone-matched) -> parts_r3/cap.png, cap_seam.png.
- `rig_r3.py`: round 2 + the upper arm shortened while raised (`ua_of`/`lift_of`, periodic in a1; `smap` keeps the
  top 60 px), the Qwen cap turned about its collar end by the shrug with the arm's pivot riding in its frame, the cap's
  shadow on the sleeve, a seam line only where the cap borders the sleeve, `cap_uncovered()` (sleeve root outside the
  cap), cast shadow 0.6 / (22, 25), the torso's baked cream sliver filled. `HJ_CAP_REST` turns the cap at rest.
- `swing_r3.py dry` also prints the face overlap per frame (`face_overlap`), the blade's bounds and the tip steps at the
  idle joins (zero end slopes now). `r3_shoulder.py [tag]` -> shoulder_3x (the clip's own poses);
  `r3_shoulder.py pre a,b` -> a 2x sheet per cap rest turn.
- Status 2026-11-22: shoulder NO after two tries (try 1: a code cap rigid with the arm = a teal tube end, because the
  pivot sits on the chest's outline; try 2: the Qwen cap rigid with the torso = an empty short sleeve with the arm out of
  its top when raised). Next try needs the cap to change shape with the arm (drawn key poses, or hem-on-arm +
  shoulder-on-torso with a deformed middle and the pivot moved into the chest). Joins fixed in numbers; frame (blade
  below the soles on swing 14-17) and face (swing 49 and 50) not fixed; `swing_r3.py render`, `r3_sheets.py` and
  `r3_shoulder.py pre` never run; try 1 is not re-makable from rig_r3.py (`paint_cap` unwired). See `r3/numbers.md`.

## Hand-set numbers
See `parts.json` (points read on grid crops: `gridlook.py`), `keys.py` (key poses), `swing.py` `K` (timeline keys),
`rig.py` constants (radius, band, rim, shadow, fist and sword scale).

## Round 5: hero 4 on Unity's bones (2026-11-23; failed twice, joints closed; kept as a record)
Verdict and record: `docs/IMPROVEMENTS_2026-10.md`, "Jointed hero on Unity's bones". Not in the game. Do not start it
again without the designer asking.

- Python: `r4_export.py` (parts with pivots for Unity, also the r5 parts and `hips_wind`), `r5_keys.py back` (the clip
  `sword_cut` as keys.json, with its checks; `r4_keys.py` overwrites keys.json with attempt 1's clip, so never run it
  after), `r5_compare.py OLD NEW` (clips and sheets from two raw takes at one framing), `r5w_*.py` (the drawn wind-up
  stance: prep, Qwen, gate, cut, sheet, studio check).
- Unity: `unity_test2d/Test2DBuild.cs` and `Test2DRun.cs` are copies kept outside the Unity project. To run them, copy
  both into `unity/Assets/Editor/` and add `"com.unity.2d.animation": "10.2.4"` to `unity/Packages/manifest.json`
  (editor 6000.0.57f1). `Test2DBuild.BuildAll()` writes the rig, the Sprite Library (body sets front, windup, strike,
  wind) and the clip into `unity/Assets/Test2D/` (generated, not in git).
- Clip (17 frames at 30 fps, stepped; take frame = clip frame + 5): f0 ready; f1 wind-up swap; f2-4 wind-up held; f5 cut
  swap with smear; f6 contact; f7-10 strike held; f11 swap back; f12-13 recover; f14-16 ready. The back heel is the
  planted foot (rear edge canvas x 484, sole y 1283-1284).
- To record (editor open on `C:/Users/Epic/dk`, play mode, each call through the Unity MCP's `execute_code`, all in
  `Drakepg.EditorTools`):
  1. `Drive.NewGame("warrior");` then `Drive.Fight("burrow_rat", "road", "road_cart");` then wait about 5 s.
  2. Today's hero: `BonesCapture.Reset(); BonesCapture.Battle(); BonesCapture.Take(OLD, 45, 30, 2560, "0@0.2"); Test2DRun.Png(OLD, 4, 24);`
  3. A new fight as in 1 (`Test2DRun.Remove();` first), then hero 4: `BonesCapture.Reset(); Test2DRun.Battle(); Test2DRun.Track(); BonesCapture.Take(NEW, 45, 30, 2560, "0@0.2"); Test2DRun.Png(NEW, 4, 24);` Check `Drive.State()`; retake on a crit.
  4. `r5_compare.py OLD NEW`, then `Test2DRun.Remove(); Test2DRun.Clear();` and stop play mode.
