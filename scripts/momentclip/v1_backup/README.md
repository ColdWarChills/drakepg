# momentclip: the hero's sword attack as a clip in the battle view

The attack is built from the four drawn moments in a layers folder (ready, wind-up, strike, follow-through). It uses
only what Unity does with plain sprites: hard swaps between the drawings, moves of the whole figure, and effect sprites.
It depends on nothing but the layers folder (its `manifest.json` and PNGs), `art/raw` (background, monster, sword art)
and the game's fonts.

Python: `"C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe" -I -B` (PIL, numpy). ffmpeg path in
`common.py`.

## The one command

```
<python> -I -B C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port/scripts/momentclip/clip.py --layers <layers folder> --outfits bare,mix1,mix2 --out hero_attack --sheets --stills mix1,mix2
```

- `--layers` a folder like `art/redo/moments/layers` (default: `art/tests/momentclip/layers_snapshot`).
- `--outfits` comma list: `bare`, `mix1`, `mix2` (in `common.py`), or your own as `piece+piece+...`
  (e.g. `scale_mail+leather_cap`). The outfits play one after another. Each plays the attack `--reps` times
  (default 2) with a short idle between, and has a small caption of what he wears.
  An outfit needs a layer for every piece at all four moments. In today's snapshot only ready and strike have them,
  so only `bare` renders a clip (the script stops and names the missing layer).
- `--out` name of `art/tests/momentclip/<name>.mp4` (2560x1440, 60 fps, H.264 yuv420p, libx264) and `<name>.webp`
  (1280x720, 30 fps).
- `--sheets` frame sheets `frames_N.png`, `key_full.png` and `timing.md` (for the first outfit).
- `--stills mix1,mix2` makes `outfit_stills.png`: ready and strike per outfit, rendered by the same code.
- `--no-video` writes the pictures only (about 15 s).
- `--anchor wind=650` overrides a moment's anchor (drawing x) if the foot finder picks the wrong foot. It prints the
  anchors on every run.

`check_layers.py [folder]` checks the layer path: the placed sword against the folder's own `weapon.png` (IoU), the
feet found, and the outfits at ready and strike (`layers_check.png`).

Render time: about 40-45 s per outfit (294 frames), plus a few seconds of encode.

## Files
- `common.py` stage numbers from the Unity battle code, outfits, paths.
- `layers.py` manifest -> each moment stacked in the manifest order (shirt hidden under a chest piece, hair under a
  helm; pieces only as the manifest lists them), the sword placed from its art (grip at 0.79 of its height,
  `grip_xy`, `angle`, `scale_px_per_art_px`) on a padded canvas, trimmed and scaled to game size (hero_h -> 240
  design px). It also finds the feet: the body layer's bottom edge near the soles, back/front by side.
- `scene.py` background, monster, soft ground shadows, `place()` (move, lean about a ground point, squash, flat tint).
- `contact.py` where the strike stands: the slash arc (the blade's path from the wind-up angle over the top to the
  strike angle, around the strike's grip) goes through the monster's middle. The hit shows there.
- `timeline.py` the timing table (beats, effects, their Unity names) and the per-frame state.
- `fx.py` the effects, flat and hard-edged: arc, impact, sparks, dust, streaks, number, caption.
- `sheets.py` the pictures for judging and `timing.md`.

## Positions (design px, from BattleScreen.cs / Figure.cs / Backdrop.cs)
Ground y 500, hero home x 330, monster x 950. The background covers the screen, lifted 90 px, with a dark strip
below. Figures are 240 px tall; the monster is 300 px wide max x look scale 0.9 (mire_toad, the Fen Road's common
encounter), mirrored to face left. Shadows are Art.Soft at 0.7 x width, 26 px tall, 45% black.

How a moment sits on the figure's x (the anchor, measured from the layers, never hard-coded):
- ready: the middle between its feet.
- wind: its back foot on ready's back foot, so the swap keeps the planted foot.
- strike: its front foot. The strike's x is solved by `contact.py`.
- follow: its front foot on the strike's front foot.
- The dash is a hop: every wind-up frame is in the air, and the landing is the strike on the hit frame (the last wind-up
  frame is about 120 px short of it; the swap covers the rest, bridged by the slash arc). Recovery swaps to ready on
  the first airborne frame.

## What each part is in Unity
| here | Unity |
|---|---|
| a moment shown | the figure's sprite stack for that pose (`SetPose`); sprites per layer at their manifest order |
| hard swap | `SetPose(name)` on one frame, no ghost of the old pose (BattleScreen.SwapPose minus its `Ghost`) |
| x / hop / lean / squash | `Anim.Float` on `Fighter.X/Y/Angle/Sx/Sy` (lean pivot = a foot: move X by the pivot offset) |
| idle breath | `Idle()`: Breath 1 -> 1.03, 0.9 s yoyo |
| freeze | `Fx.HitStop(100)` |
| camera kick / shake | `GameRoot.Kick` / `GameRoot.Shake` |
| slash arc | one generated mesh (crescent with vertex alpha), its start angle tweened toward the blade |
| impact, sparks, dust, streaks | `Fx.Impact`, `Fx.HitSparks`, `Fx.Dust`, `Fx.SpeedLines`, drawn flat |
| dash ghosts | figure copies tinted flat cream, fading (`Fx.FadeGhost`) |
| monster white, knock-back | `Fx.WhiteFlash` (60 ms), `Anim.Float` on its X/Angle/Sx/Sy |
| damage number | `Ui.FloatText` |

The exact frames and values are in `art/tests/momentclip/timing.md` (written from `timeline.py`).
