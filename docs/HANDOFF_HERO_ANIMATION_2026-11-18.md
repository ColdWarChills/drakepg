# Handoff: find a way to draw and move the hero that works (2026-11-18, evening)

From the chat that built the "hero on bones" tests (Claude Fable, out of budget) to a new Opus session.
Everything marked **(Claude's reading)** is the old chat's view, not the designer's words. Check it, don't trust it.

## 1. What the designer said

Latest, 2026-11-18 about 20:00, after the newest attack clip (`art/tests/sheets/bones/stand/cut_j5c_pair.mp4`):

> write a handoff to new opus session since fable is about to max out. animations are still off, the hands, the elbows etc. we need to rework it somehow, redo a single character and find a way that works.  that will be the purpose of the other session on opus

Earlier, on the same subject (newest first):

- 2026-11-18 about 13:40, with arrows drawn on every arm of ten stances Claude had judged good: "So nothing was good. only the 1 image, I added arrows to all of the issue spots. thinking the approach might be bad since the results passed your judgement"
- 2026-11-18 about 12:35, on one sword hand (`r7`): "the hand is correct just could be a more epic position, the hand seems to more or less fine now."
- 2026-11-18 about 07:40, on the first swing on bones: "ooh, you did a cool animation on bones, though still have weird hand shape, but the animation it self looked smooth and nice.  Yeah do more bones"
- Older, on the drawn hero in the game: "Character poses look weird, weapon holding looks weird", "the we hold staff and dual daggers are weird", "terrible hand holdings"
- 2026-11-15: "I need you to think for yourself. We're aiming for the best results. currently you provided me with worse post fixes. terrible hand holdings. please make a session, do research of poses how it looks, looks for references etc. this is unacceptable"
- The goal for attacks: "feel epic, tasty, playful and powerful".

What has been liked so far: the base drawing of the hero, the smooth motion of the bones swing, the `r7` sword hand ("more or less fine"), and the standing pose `stand_sword_j6` (enough for "Yes, build it").

## 2. The job

**(Claude's reading of "redo a single character and find a way that works")** Stop polishing the numbers of the current kit. Take one character (the hero, bare, with the plain iron sword) and rebuild how it is drawn and how it is moved, until hands and elbows look right in every frame. Only after that: gear, other weapons, other attacks.

Done looks like: the designer watches one sword attack of that hero, old beside new, and has nothing left to say about hands or elbows.

Steps Claude would propose (yours to change; write the ones you choose into `docs/PLANS.md`, each starts after the designer's yes to the one before):

1. Find the way with small still tests, then show the held moments of one sword cut (standing, wound up, the hit, after the hit) made the new way, old beside new, with close-ups of hands and elbows.
2. Make it move: the whole cut as a clip, old | new, normal and half speed.
3. Say what the way costs per armor piece and per weapon, then the next attacks.

## 3. Where things stand

- Folder `C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port`, branch `claude/unity-port`. About 30 commits are local only: nothing is pushed (the designer was asked twice and did not answer, so no push without a yes).
- The game still uses the drawn hero: one picture, the weapon placed at hand anchors, drawn key poses for top skills. Nothing in the game calls the bones hero. Everything below is test work.
- The Unity editor (6000.0.57f1) is open on this folder, play mode off.
- Plans and history: `docs/PLANS.md`, "Next up", from "Overnight runs on the hero on bones" down to "Where it stands" (about lines 9-48). The lines are very long: read them with `sed -n '9,48p' docs/PLANS.md | cut -c1-900`.
- PLANS holds a morning decision: "bones go on, for the big attacks too; drawn poses are not mixed in at the big moments". **(Claude's reading)** the evening verdict reopens how the hero is made. If the way you pick mixes drawn arms or frames in, say so plainly when you show it.

## 4. What exists

- Kit code (editor only, `#if UNITY_EDITOR`): `unity/Assets/Scripts/Game/Bones/BoneHero.cs`, `BoneMove.cs`; the stage `unity/Assets/Scripts/Game/Test/BoneStage.cs`; capture `unity/Assets/Editor/BonesCapture.cs`. Older tests beside the stage: `LayeredHero.cs`, `SkeletonSwing.cs`, `LayeredFight.cs`.
- What the kit can do: stiff part pictures on joints, a draw order, arms and legs that reach for a point, squeeze and slide per part, swap pictures keyed in time (torso, hands, faces), gear as skins, a weapon in either hand, smear trails, events.
- Kit data: `unity/Bones/hero/` (`rig.json`, `poses.json`, `fight.json`, `swaps.json`; part pictures in `bare/` and `bare_v2/`; one folder per gear id; `iron_sword.png`; `moves/*.json`). Rules: `unity/Bones/README.md`.
- Moves to know: `sword_cut.json` (the first cut, kept), `stand_sword_j6.json` (the accepted standing pose), `sword_cut_j5c.json` (the rejected attack from it).
- Scripts: `scripts/bones_parts.py` (cuts the parts), `bones_sheets.py`, `bones_pairclip.py` (old | new clips in step), `hero_layers.py`, `hero_layers_rig.py`, `hero_layers_fight.py`, `hero_edits.py`, `hero_poses.py` (drawn key poses through Qwen), `redo_art.py` (ComfyUI + Qwen-Image 2.1 on this PC).
- Drawings: `art/raw/char_hero_bare.png` (the base) and `art/raw/char_hero_bare_<pose>.png` (drawn key poses: `axe_wind`, `axe_strike`, `charge`, `dagger_ready`, ...).
- Proofs (in git): `art/tests/sheets/bones/`. `morning/reel.mp4` is the overnight reel. In `stand/`: `e_stands_064.png` (the ten rejected stances), `j_show_3x.png` (the fifth batch's stance as shown), `cut_j5c_pair.mp4` and `cut_j5c_pair_halfspeed.mp4` (the rejected attack, old | new).
- Local working kit (git-ignored): `art/redo/stand/`. `joints.py` (joint numbers against the limits), `cropsheet.py` (close-up sheets), `mktrial_j.py` (writes move takes), `pairclip_j.py`, and the notes `JUDGE_cut_j5.md`, `REPORT_cut_j3.md`, `REPORT_cut_j4.md`, `BRIEF_cut_j4.md`. `SESSION_NOTES_2026-11-18.md` there is 96k characters in very long lines: grep it with a bounded pattern, never read it whole.

## 5. What was tried, and how each ended

1. **Drawn hero, weapon placed at hand anchors** (in the game now). "Character poses look weird, weapon holding looks weird", "terrible hand holdings".
2. **One hero picture bent by a bone rig** (2026-10-18). Sleeves and outlines smeared. Dropped.
3. **Drawn key poses** (Qwen edits of the bare hero, `scripts/hero_poses.py`; in the game for top skills). Limbs are drawn, the weapon is still placed at anchors afterwards.
4. **Layered hero test** (parts cut from the drawing, gear per slot). Won on gear and on holds, lost clearly in the blows.
5. **Hero on bones** (2026-11-17/18, our own code on those parts). The motion was liked ("smooth and nice"). Hands and elbows were rejected in every batch: the first swing ("weird hand shape"), ten stances (arrows on every arm), and the attack `sword_cut_j5c` (section 1).

## 6. Why the current way keeps failing (Claude's reading)

1. **The arm pictures were cut from one standing drawing**, both arms nearly straight, about 45 degrees below level. Each piece keeps that drawing's outline, sleeve folds and light. Measured: an arm reads right only within about 25 degrees of how it was drawn. The elbow is a hinge between two stiff pictures with a rolled cuff right on it: folded past about 45 degrees it shows a notch or a square corner.
2. **Side view, turning in the flat only.** A real swing brings the arm toward the viewer, away from them and across the chest. Stiff pieces cannot shorten, so an arm across the body looks short, or "grows out of the head" (a blind reviewer's words). A cream sleeve on a cream shirt makes it worse.
3. **Too few hands.** Five hand pictures (`n_hand`, `n_hand_grip`, `n_hand_grip2`, `n_hand_hammer`, `f_hand`) serve every wrist angle of a swing, and the sword is a separate picture pinned at a grip point.
4. **Moves are numbers in a file**, written by Claude and judged from stills. Many rounds went into the numbers. None changed the pictures.
5. **The checks were too soft.** Claude's own look passed ten stances the designer rejected in seconds. The blind reviewer (an Opus helper told nothing) never scored anything from this kit above 3 of 5, the kept moves included. That ceiling belongs to the pictures and the rig, not to the numbers.
6. **The fix PLANS already names was never built.** "Changed method" (arms drawn for a hold, cut as swap pictures) is written down, but the last batches stayed inside the arm limits instead, and the attack still uses the old wound-up pose with its stubby raised arm.

## 7. Ways worth testing (Claude's reading)

- **A. Arms drawn for each held moment.** Keep bones for body, legs, head and timing (the part that was liked). For each moment the eye rests on (standing, wound up, the hit, after the hit), draw the whole hero in that pose with the sword in the hand (a Qwen edit of `char_hero_bare` from a pose reference), judge it as a drawing, then cut both arms with hand and hilt and pin them at the shoulders. Between holds the arm is on screen for 2-4 frames: swap at the fastest frame, under the smear. Fixes elbows, shortening and the grip. Risks: the seam at the shoulder, drawings drifting from the base look, the torso under the arm has to match (torso swaps exist). Gear cost: each drawn arm needs a version per sleeve and per glove (PLANS estimates about 11 and 9).
- **B. Parts redrawn for turning.** Upper arm and forearm drawn straight, with round ends that overlap at the joint, even light, the cuff off the elbow; plus a set of hands drawn for the grips a swing needs (and the fist in two layers, fingers over the hilt and palm under it, if it is not already). Keeps everything else. Fixes notches, square elbows and hand shapes. Does not fix shortening (the kit's squeeze per part may help, or A for those moments). Cheapest for gear: the same pictures per slot as now.
- **C. One arm picture bent smoothly over two bones** (a small skinned strip in our own code, no package). Fixes the elbow notch only. The 2026-10-18 smear came from bending the whole hero picture; an arm drawn straight for bending is a different case and untested. The Unity 2D Animation package does the same but touches the four editor files (section 9) and needs the designer's yes.
- **D. Whole drawn frames for the attack** (4-6 full drawings with the sword in the hand, played with holds and smears; bones only for standing). Best hands and elbows if the drawings are good. Loses the smooth in-between motion that was liked, and costs a full set of drawings per weapon type and per outfit.
- **E. Where the poses come from** (for A, D and B's hands): real references first. Photos or frames of a person swinging a one-handed sword, well-made 2D game animation, or a simple jointed dummy built from plain shapes in the Unity editor and captured from the side camera, used as the pose reference for the Qwen edit. Study others, copy nothing.
- **F. A video model** for whole moves: only as a pose reference, and only with the designer's yes (an outside service, may cost credits).

**First test Claude would run:** one still, not a move. The wound-up moment (sword raised behind the head), where the current kit looks worst. Make it by A and by B, put both beside the kit's frame and the base drawing, hands and elbows at 3x. If neither clearly wins as a still, no animation built on it will. Then the other three moments, then motion.

For any way you recommend, write down what it costs per armor piece and per weapon. The pilot is bare, but the game has 8 gear slots and swappable weapons.

## 8. How to judge before showing

- The bar is the designer's eye. Twice on 2026-11-18 work passed Claude's checks and a blind check, and was rejected in seconds. Treat a pass as "not yet disproved".
- Yardsticks: a real reference of the same pose next to ours (private study, never in the repo or the game), and the base drawing (its own hands and arms are right as drawn).
- Every hand, wrist, elbow and shoulder of every held frame at 2-3x. The whole figure at 0.64 of the 1280 frame (the size the designer sees).
- A fresh helper that is told nothing: letter-named sheets, frames numbered, a known-bad control mixed in (for example the hop of `sword_cut_j3`, where the sword hangs like a walking stick). Trust it only if it catches the control. Ask it to list every hand and elbow that looks wrong. A 3 of 5 is not a pass.
- Clips: old on the left, new on the right, same framing, in step at the hit (`scripts/bones_pairclip.py` pairs by where the sword is), normal and half speed. The designer's look at the moving clip outranks any rating of stills.
- What `sword_cut_j5c` still got wrong by Claude's own notes: `art/redo/stand/JUDGE_cut_j5.md` (the arm across the face for a frame, the stiff straight-up sword in the air, a sweep behind him that reads as a swing at nothing, the stubby raised arm in the wound-up pose).

## 9. Hard rules (the designer's, still in force)

- "All work happens in C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port (branch claude/unity-port), by absolute path. The open Unity editor uses this folder. Don't make another worktree."
- "Never stage or commit these four files (the editor version question is still open): unity/ProjectSettings/ProjectVersion.txt, unity/ProjectSettings/ProjectSettings.asset, unity/Packages/packages-lock.json, unity/Assets/UniversalRenderPipelineGlobalSettings.asset. git add with explicit paths only." They show as modified in `git status`: never stage, commit, stash or revert them.
- "Never merge into or push claude/sweet-cray-w6r64t (GitHub Pages deploys from it)." Pushing `claude/unity-port` has not been allowed either. No PR.
- No new Unity package without the designer's yes (it would touch the four files).
- "my yes is still needed before anything goes into the game."
- "Study others, copy nothing: original work only." No assets or names from Artix Entertainment games.
- "Only one worker at a time on the Unity editor, and one at a time on ComfyUI."
- `unity/Bones/README.md`: "Never overwrite a kept picture. A new try gets a new name; the old one stays until the designer's yes." The same goes for committed move files, `rig.json` and `swaps.json`. Part pictures are never drawn or repainted by hand.
- `CLAUDE.md`, "Working with the designer": one job at a time; say in one plain line what is being made and what the designer will see; every step ends in something to look at, old beside new; judge it first; at most one or two questions, only about something the designer has seen, in everyday words, with your pick; small and technical choices are never asked.
- Short replies: short lines, examples over jargon.
- `CLAUDE.md`'s start step (fetch, merge the default branch into this branch) applies. If that merge wants to touch the four editor files, stop it (`git merge --abort`) and carry on without it.

## 10. Practical

- Start every shell command with `cd "C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port" &&` (the shell's folder can reset). Foreground `sleep` is blocked.
- Unity MCP: `manage_editor` play / stop; `execute_code` with `action: "execute"`, `compiler: "codedom"` (C# 6). `Drakepg.Test.BoneStage.Open()`, wait until ready, then `BoneStage.Stills("move@ms=label ...", dir)` for stills; takes for clips are in the same class. `Stills` wipes `f*.jpg` in its target: always a new folder. Calls over about 30 s time out the MCP but keep running. Leave play mode off when done.
- Move data: `keys` in time; `n.hand` is the sword grip from the waist (rig pixels, y down); `n.blade` in degrees clockwise (0 at the enemy, 90 down, 180 back, -90 up); `n.sweep`, `n.bend`; the near arm is the sword arm; `f.hand` is the free wrist. Rig waist [780,690], scale 0.2321.
- Drawing with Qwen: `scripts/hero_poses.py`, `scripts/redo_art.py`, and the memory notes `comfyui-qwen-setup`, `hero-key-poses`, `visual-fixes-need-references`.
- ffmpeg 9: `C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0-full_build/bin/ffmpeg.exe` (no `-vsync`; use `-fps_mode passthrough`). Python 3.12 with PIL and numpy.
- `docs/PLANS.md` and `docs/IMPROVEMENTS_2026-10.md` have CRLF line endings: change them with a small python script on bytes.
- Advisor: `CLAUDE.md` asks an Opus main session to call the advisor (Fable) before choosing an approach, when the same failure comes back, and before saying the work is done. Fable's budget is nearly used up. Try it at those points; if it is not available, carry on, lean on references and the blind check, and say in one line that it was skipped.
- Keep tool output small (`| cut -c1-170`, `| tail -3`), pictures under 1500 px wide.
