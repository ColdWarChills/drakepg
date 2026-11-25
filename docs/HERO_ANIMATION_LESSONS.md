# Hero animation: why our own tries failed and the Dragon Crashers knight works (2026-11-25)

The designer, 2026-11-25, after the two Dragon Crashers clips ("oh, this looks good"): "why were we not able to that ourselves? We need to figure this out for future references and approaches". Read this before any new work on how the hero moves. Numbers marked *measured* come from the files in the Unity project (2026-11-25); the rest is from our records (`docs/IMPROVEMENTS_2026-10.md`, "Jointed hero on Unity's bones" and "Dragon Crashers test") and Unity's own write-up (blog "Get to know Dragon Crashers", 2021-03-19; e-book "2D game art, animation, and lighting for artists").

## How Unity made the knight

- **Two jobs, two people, one shared body.** Unity's animator animated one plain mannequin (`PV_Character_Base_Bipedal`, a grid-patterned body), and a character artist drew the looks for it. Every character is that mannequin with another Sprite Library: the same part names, the same bones, the same clips. This is the designer's own idea ("skeleton animations is one part ... put the clothes on the skeleton"), done by a studio.
- **The drawings are made for the rig** (*measured*). Each body part is its own layer, drawn whole and bent through weights:
  - The sword arm is one drawing (94 x 312 px) on four bones: upper arm, forearm, hand and weapon.
  - Each leg is one drawing from hip to toe on four bones.
  - The torso is one drawing on seven bones, from the shoulders to the pelvis, so the body bends too.
  - Pieces drawn to cover a joint ride on the bones of both sides: the shoulder plate on the shoulder and arm, the neck piece on the chest and both shoulders, the coat flaps on the chest, belly and thigh.

  So a bend shows no cut, no seam and no extra drawing per pose. The build is chunky (big head, short thick limbs), which bends well. Only the face swaps drawings (mouth, eyes).
- **Small and reusable** (*measured*): 19 body drawings on 25 bones, 6 limb solvers (hands and feet are placed, elbows and knees follow), and the same 19 drawings serve every move (idle, attack, run, hit, die).
- **The motion is made by eye.** The attack is 68 frames at 60 fps: 186 animated values with 783 keys on smooth curves (*measured*). The whole body moves (pelvis, chest, head and both legs), and a person scrubbed and adjusted it in Unity's Animation window.
- **The moves suit flat bones.** The knight works in side view and its attack is a raise and a lunge. It never turns its body in depth.

## What we did, and where it broke (tries 2 to 5, 2026-11-21 to 11-23)

- **A picture first, cut up after.** Hero 4 was meant to be drawn for joints: Qwen changed only the clothes on the bare hero's own pose and gave him a plain tube sleeve. But it was still one finished picture, cut into parts afterwards by code:
  - the sleeve was re-cut into a clean tube;
  - the neck was stretched under the collar;
  - the rim light was taken off and painted back by code.

  Nothing was drawn under other parts, and no piece was drawn to cover a joint. The shoulder, which the knight hides under a plate, failed twice on 2026-11-22, and the lifted shoulder looked pinned on (try 2, the plain cut-up hero, got "No, looks off").
- **Only the sleeve bent; the rest needed a new drawing for every pose.** Hero 4's sleeve did bend through weights (two bones, a mesh made by code). His torso, head, hips and legs were stiff pieces, so every pose that moved them needed new drawings. His one attack needed 22 body drawings (3 torsos, 3 heads, 3 hips, 3 armholes, 3 far arms, 2 sleeves, 2 shoulders, 2 tails, a fist), plus 2 smears, a sword and a rim drawing for each. The turned torso fitted the arm only within about 5 degrees of how it was drawn. Our own verdict on 2026-11-23: a pose that needs its own drawing for the stance, the torso and the head is a drawn pose again, with seams added. The knight bends 19 drawings, torso and legs included, for every move.
- **Motion typed as numbers.** Claude wrote the poses as numbers (lean, turn, arm angles) and saw them only after a render: 17 frames at 30 fps, with every value jumping from frame to frame instead of gliding. Nobody adjusted it by eye.
- **The hardest pose for flat bones.** Today's hero coils into his wind-up, twisting until his back is to us. That is a turn in depth, which flat bones cannot do. Every try was judged on exactly that moment.
- **Motion and art made together.** Each try built both at once, so when it failed we could not tell which half failed. The designer's split (motion apart from clothes) is what made the Dragon Crashers tests readable.
- **The tools behind it.** Qwen makes finished pictures, not drawings ready for a rig: it cannot draw the parts hidden under other parts, or keep a part's outline the same across poses. Claude cannot animate by eye in the editor, only by numbers and renders.

## An open finding: the bar

Claude gave hero 4's last clip and the knight the same verdict: a wind-up taller and stiffer than today's crouch and twist. Hero 4 was not shown, because nothing that loses to the game is shown. The knight was shown only because the browser chat's handoff required it, and the designer said "looks good". Perhaps the bar Claude held the tries to (beat today's drawn pose at its strongest moment) was stricter than the designer's. Perhaps the designer liked something hero 4 lacked (a smooth whole body, clothes that swap). We don't know which, so it stays open. Until then, show close calls with a plain line on what is weaker, and let the designer decide.

## Rules for next time (Claude's proposal; the designer has not agreed to them yet)

1. **Start from a body made for animation** (the mannequin), never from a finished picture. Our art is drawn onto the mannequin's parts, outlines and pivots (test 2 proved this works), never cut from a painting.
2. **Motion comes from someone working by eye, on smooth curves:** a ready-made clip, or a person keying it in Unity. Claude does not type poses. When Claude changes motion, it starts from an existing clip and changes timing or placement, not the poses.
3. **Pick moves that suit flat bones:** side view, raise, lunge, step. A twist in depth needs a drawn swap, and few of them.
4. **Judge motion and clothes apart,** on clips at game size, stepping through frames.
5. **Show close calls** with a plain line on what is weaker.

## Still unproven

- Our whole hero redrawn on the mannequin in our flat style. Done on 2026-11-25 as a repaint of the knight's own drawings (flat fills inside its outlines; `docs/IMPROVEMENTS_2026-10.md`, "Our hero on the knight's skeleton"). Still open: a hero drawn fresh on the mannequin's parts, and whether the designer likes the repaint.
- Timing in our fight: the slide is gone (2026-11-25: the run clip at its stride speed, then the attack in place), but the whole move takes about 2.9 s against today's 0.8 s, and the knight's wind-up is still its tall, still hold.
- The licence against our public repo (the sample's files stay on the PC; `docs/PLANS.md`).
- The woman hero, other armors and other moves.

Where things are: the stripped sample (the mannequin, the knight, its sword and shield, the clips) is in `unity/Assets/Test2D/dc/` in the `claude/unity-port` worktree, on the PC only. The clip of the bare mannequin beside the clothed knight is `art/tests/dc/strip/skeleton_and_clothes.mp4` in the same worktree.
