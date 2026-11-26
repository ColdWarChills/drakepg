# Handoff from the browser chat (2026-11-24)

For the Claude session on the designer's PC. Written by the browser chat (the design advisor), which cannot push. Checked against `origin/claude/sweet-cray-w6r64t` at 01ddda9 (your handoff of 18:25 UTC is in it).

## Read this first: do not run your tidy-up as written

Your handoff says "take the test out of the Unity project": the `com.unity.2d.animation` line, `unity/Assets/Editor/Test2D*.cs`, `unity/Assets/Test2D/`. Since then the browser chat put a **new, working test** inside `unity/Assets/Test2D/dc/`, and it needs the 2D Animation package. So:

- **Keep:** `unity/Assets/Test2D/dc/`, the `com.unity.2d.animation` line, and the new `com.unity.2d.psdimporter` line (9.1.3) in `unity/Packages/manifest.json`.
- **Still fine to remove:** hero 4's leftovers only (`unity/Assets/Test2D/Art/`, `hero4.prefab`, `hero4_library.asset`, `sword_cut.anim`, `unity/Assets/Editor/Test2D*.cs`).

## Joints are reopened by the designer, for two tests only

After reading your morning note, the designer said (2026-11-24):

- "I feel like we should make skeletons a different part over all. Like skeleton animations is one part and we need to make textures on top as a separate thing. WE should just put the clothes on the skeleton so to say."
- "We did not get any better results at any point."
- On the plan below: "yeah, you can send this to it. do some tests and I'll check when I'm back home".

Write that into PLANS next to "joints are closed".

Why this is not the same try again (the advisor's read): every earlier try made the motion and the art together, with poses typed as numbers. Here the motion is judged apart from the art, and the motion is ready-made: Dragon Crashers, Unity's free 2D sample (the designer on Nov 23: "look really good, we have a little different style, but might be able to adapt"). It is built exactly the way the designer described: one mannequin skeleton, and each character is a variant where only the Sprite Library (the clothes) differs.

## Done today by the browser chat, through the Unity editor on the PC

**Result: one skeleton, one attack clip, four full sets of clothes, working in this project.** Editor 6000.0.57f1, 2D Animation 10.2.4. The knight plays its own attack (1.13 s, 68 frames at 60 fps); the same skeleton then plays the same clip wearing the Witch, Wolfman and Skeleton Sprite Libraries. No part lost its sprite, no console errors. The designer saw stills of this in the chat; he has not seen it move.

**To see it:** open `Assets/Test2D/dc/DcStage.unity`, press Play, wait about 10 seconds. It records the frames, then loops the four looks side by side at game speed. (The game's title screen starts behind it, because the game boots in any scene; the test stands at x 2000, y 2000 to stay clear.)

**What is on the PC, all uncommitted:**

- `unity/Assets/Test2D/dc/`: from Dragon Crashers 1.7.0 only `Art/Characters` (no dragon), `Art/Weapons` and `Animations/Characters`, with their original .meta files. No scripts, scenes or project settings imported. Plus `DcCapture.cs` (the test, editor-only) and `DcStage.unity`.
- `unity/Build/dc_test/`: every frame as PNG (`Knight_000.png` ... `Skeleton_068.png`), `sheet.png`, `looks_windup_strike.png`, `info.txt`.
- `unity/Packages/manifest.json` and `packages-lock.json`: `com.unity.2d.psdimporter` 9.1.3 added (the characters are .psb files; their rigs do not import without it).
- The full package is in the Asset Store cache (`%AppData%\Unity\Asset Store-5.x\Unity Technologies\Complete ProjectsPacks\`). The designer added it to his Unity account himself.

**Found on the way:**

- The knight prefab has one missing script (the sample's gameplay script). Harmless here.
- After a Sprite Library swap, parts the clip does not animate kept the old sprite: their resolvers report an empty category. Fix in `DcCapture.cs`: at start, find each part's category and label by matching its sprite in the knight's library, then `SetCategoryAndLabel` after each swap.
- The characters use `Sprite-Lit-Default`; the test adds one global Light2D.

## What is left (the designer checks tonight or tomorrow)

**Test 1: motion only.** One clip: today's hero | the Dragon Crashers knight, untouched, doing its own attack at the hero's place and size in our battle. Game speed twice, then quarter speed. The designer judges only the motion: is this good enough to build our hero on? Not the art, not the proportions.

**Test 2: clothes only.** One clip of the same attack: the knight as shipped | its chest repainted in our drawn style by Qwen | a second chest, swapped on the same skeleton. Same canvas, pivot and outline as the sample's chest sprite, so the skeleton and clips stay untouched. The untested risk is whether Qwen can paint inside a locked outline; if it cannot, that is the finding.

**Also open:**

- Which hand holds the sword. On the stills it looks like the far arm, with the shield on the near arm. Not confirmed. Report it; do not fix it in these tests.
- Licence: the package is under the Standard Unity Asset Store EULA. Whether its skeleton and clips may ship in a sold game is not checked. Check before anything from it goes into the game; these tests are look-and-learn only.

## Rules for these tests

- Two jobs, reported separately. Test 2 needs only the knight in the project, not test 1's verdict.
- Stop rule: two failed tries per test, then stop and report what failed. No third variant.
- Nothing from earlier tries is reused: not hero 4, not `LayeredHero.cs`, `LayeredFight.cs`, `SkeletonSwing.cs`, not the `Bones/` kit. No new rig code. No pose is typed or edited in test 1.
- Judge on clips at game size, stepping through the motion.
- Show the clips even if they do not beat today's hero. The designer asked to see these; say plainly what is weaker.
- Stop helpers' background commands before the closing message, so the task panel does not look busy (your own note from today).
- Out of scope: our own hero on the rig, a full gear set, the Unity 6.3 move, night art runs, the paper damage proof.

## When done

At the top of `docs/PLANS.md` "Next up": the two clip paths, one plain line per test (what holds, what is weaker), anything that needs the designer's click. Push.
