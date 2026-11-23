# Brief, round 3: the shoulder, and four small faults (keep this run small)

W = `C:\Users\Epic\Desktop\dragonfab\.claude\worktrees\unity-port` (work by absolute path).

**Goal (one line):** the round-2 clip of the hero cutting with his sword, with a shoulder that reads as a drawn shoulder at every arm angle, good enough to show the designer.

**Where round 2 stands (the orchestrator's own look):** the hand (one fist, the sword fixed in it), the cuff and the elbow pass; the ready and strike poses read as the game's hero. Do not touch them. It is not shown because:
1. **Shoulder (the reason for this round).** In the raise and the held wind-up (`r2\look.png`, third panel) the raised arm reads as a bent tube pinned to the side of the chest: the round top of the sleeve shows, the upper arm keeps its full length, nothing of the body covers the join. A code-drawn armhole strip was worse.
2. The arm's cast shadow can hardly be seen on the dark vest.
3. The blade leaves the bottom of the frame from the strike on.
4. A 12 px blade-tip step where idle and swing join.
5. Sleeve and hilt cover the face on 1-2 frames of the cut.

**Read first (only these):** the "Round 2" block of `HOWTO.md`, `W\art\tests\hero_joints\r2\numbers.md`, then `rig_r2.py` and `swing_r2.py`. Copy to `*_r3.py`; reuse `parts_r2` as it is.

## 1. Shoulder: clothes hide the join (the designer's own idea)
- A **drawn shoulder cap** of the vest's own cloth: a short cap of the vest lying over the top 40-50 px of the sleeve, with the vest's drawn edge line and its hard shadow on the sleeve below it. Its own piece, drawn above the sleeve, pinned at the shoulder, turning by about half the upper arm's angle and moving with the shrug, so the top end of the sleeve is never seen at any angle used.
- Source, in this order: (a) cut it from the vest's own drawn shoulder on `r2_vest_teal_s202` and extend it with the vest's two tones and edge line; (b) a Qwen edit of that drawing ("change only: the vest has short cap shoulders that cover the tops of both arms"; at most 4 runs; take only the cap). Never a plain code-drawn strip.
- The upper arm may be drawn shorter while it is raised (down to 0.7 of its length, eased in and out with the raise), and the shoulder may lift further with it: stretching and squashing at joints is the designer's own suggestion.
- The wind-up pose may change to one the arm holds well: the elbow high and a little forward, the fist above and behind the head, the blade up and back and clear of the face. The grip turn stays inside 9-74.
- **Before any clip:** `shoulder_3x.png`: the shoulder at 3x at ready, mid raise, wind-up, mid cut and strike (stills only). Go on to a clip only when every panel reads as a sleeve coming out from under a vest. Two tries; if it still fails, stop and report.

## 2. The four small faults (one pass, no iterating)
- Shadow: arm, fist and hilt cast a hard-edged shadow on the vest that is clearly visible at game size (a tone about 0.6 of the vest's lit tone).
- Frame: lower and wider, so the whole blade is inside on every frame (the legs can stay as drawn, unmoving).
- Join: no blade-tip step at the idle/swing joins larger than its neighbours (one continuous curve or a short blend).
- Face: sleeve or hilt may cross the face only on the single fastest frame of the cut, under the smear; never on a held or slow frame.

## Deliver in `W\art\tests\hero_joints\r3\`
`shoulder_3x.png`, `look.png` (ready, wind-up, strike at game size beside `char_hero_bare`), `motion_strip_raise.png` and `motion_strip_cut.png` (every frame), `arm.mp4` + a half-size `arm.webp`, `numbers.md`. Every sheet at most 1500 px on the long edge and 1.05 MP.

## Limits
- Small on purpose: at most 6 Qwen runs, 2 clip renders, 12 picture looks; do not re-read round 1's code or rebuild what passes.
- Write only in `W\scripts\hero_joints\`, `W\art\redo\hero_joints\`, `W\art\tests\hero_joints\`; add a "Round 3" block to `HOWTO.md`. No edits to `docs/PLANS.md`, CLAUDE.md or memory files; no git commit, stash, checkout or reset; nothing in `W\unity\`; never overwrite a kept picture.
- The sword is in his right hand, which is the near arm (he faces screen-right with his chest toward us); never reason in picture sides.
- A "partly" is a no. "It failed, here is why" is a good result.

## Report (at most 200 words)
Yes or no on shoulder, motion and look, each with its reason; whether hand, cuff and elbow still hold; the advisor calls and what each changed; Qwen runs and minutes; at most 4 pictures to look at first.
