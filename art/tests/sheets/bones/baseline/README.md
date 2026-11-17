# Baseline: today's drawn hero, recorded in real fights

Recorded 2026-11-17 in the Unity editor (`BonesCapture.Battle()` + `Take(...)`), level 6 heroes from the chapter 2
start against a Bog Lurker (Mirewater Fen, not a boss), 60 frames per second of game time, frame-exact
(`Time.captureFramerate`), 1280 px wide (the hero is about 240 px tall in the key poses). Raw frames and track
files: `art/redo/bones_frames/baseline/<move>_x1|x2/` (not in git). Pictures per move in the folders here:
`strip.webp` (every 4th frame), `strike.webp` (every frame through the strike), `key_*.webp` (full size),
`clip.mp4` + `clip_half.mp4`, `timeline.webp`, `tip.webp` (blade tip path, one dot per frame).
`reel_half.mp4`: all four at half speed, then the bones swing of tonight.

Timed from the button press to "settled" (back home, upright, squash gone; the rest pose comes earlier). Beats
are read off the frames (the drawn pose swaps and the hero's position) at x1.

**Hit pause:** a hit on a monster that is not a boss asks for a 70 ms freeze (`BattleScreen`: `Fx.HitStop(min(stop,
70))`; 35 ms at x2, since it is divided by the battle speed). The freeze ends on real time, so the recorder shows it
shorter (33-50 ms). The "in play" totals add back the difference; the recorded ones are what the frames show.

| Move | Weapon | In play x1 | Recorded x1 | Recorded x2 | Wind-up | Step / lunge | Strike to rest pose | Hit pause (asked / recorded) | Recovery (rest pose to settled) |
|---|---|---|---|---|---|---|---|---|---|
| Warrior everyday blow | Thornheart Blade (sword) | **~920 ms** | 883 ms | 483 ms | 200 ms | 100 ms | 217 ms | 70 / 33 ms | 367 ms |
| Rogue everyday blow | Briar Fang (two daggers) | **~750 ms** | 733 ms | 383 ms | 117 ms | 83 ms | 233 ms | 70 / 50 ms | 300 ms |
| Mage everyday blow | Heartwood Staff | **~1320 ms** | 1300 ms | 667 ms | 117 ms beat, then 300 ms | 83 ms (no run: the staff casts a bolt from home) | 433 ms (the bolt lands at the end) | 70 / 50 ms | 367 ms |
| Warrior Earthshaker (top skill, drawn key poses charge / leap / land) | sword | **~1920 ms** | 1900 ms | 983 ms | 783 ms charge (power gathers, glint) | 350 ms leap | 350 ms (the hit is on landing) | 70 / 50 ms | 417 ms |

Notes
- x2 is a little over half of x1 (483 against 883 ms for the sword): the beats halve, the frame-steps between
  tweens and the real-time hit pause do not.
- The everyday sword and dagger blows are one run-in: wind-up pose, a step pose while lunging (~110 ms), the strike
  pose on contact, the hero carried on a little, then a hop home. The designer's "everyday blows about 0.75 s" is met
  by the dagger and close for the sword; the mage's bolt takes 1.3 s.
- Game-time beats are exact: the sword's wind-up measured 200 ms against 196 ms from `BlowOf("sword")`
  (Antic 0.14 + Hold 0.04 s, plus a frame per await).
- Tip-speed spikes at a pose name in the timelines are picture swaps (the drawn weapon jumps to the next drawing),
  not real speed. Tip speeds are in picture px per second: only compare takes of the same width.
- Two daggers: only the first is tracked (the tip path follows one dagger).
