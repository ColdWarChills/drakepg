# momentclip: the hero's sword attack as a clip in the battle view

The attack is built from the drawn moments in a layers folder: ready, wind-up and strike, plus the follow-through
when it is shown (`--moments 4`). It uses only what Unity does with plain sprites: hard swaps between the drawings,
moves of the whole figure, and effect sprites. It depends on nothing but the layers folder (its `manifest.json` and
PNGs), `art/raw` (background, monster, sword art) and the game's fonts.

Python: `"C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe" -I -B` (PIL, numpy). ffmpeg path in
`common.py`.

## The one command

```
<python> -I -B C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port/scripts/momentclip/clip.py --layers <layers folder> --outfits bare,mix1,mix2 --moments 3 --sheets --look --stills mix1,mix2
```

- `--layers` a folder like `art/redo/moments/layers` (default: `art/tests/momentclip/layers_snapshot`).
- `--moments 3` ready, wind-up, strike: the strike is held while the hit plays out, then he backs off and hops home.
  `--moments 4` shows the follow-through after the strike. Default: 4 when the folder has a `follow` moment, else 3.
  Both come from one timeline (`timeline.Beats`); they are the same up to the end of the freeze.
- `--outfits` comma list: `bare`, `mix1`, `mix2` (in `common.py`), or your own as `piece+piece+...`
  (e.g. `scale_mail+leather_cap`). The outfits play one after another. Each plays the attack `--reps` times
  (default 2) with a short idle between, and has a small caption of what he wears.
  An outfit needs a layer for every piece at each moment shown (three or four). In today's snapshot only ready and
  strike have them, so only `bare` renders a clip (the script stops before encoding and names the missing layer).
- `--outdir` where everything is written (default `art/tests/momentclip/v2`; the first draft's files one folder up
  are left alone).
- `--out` video name: `<outdir>/<name>.mp4` (2560x1440, 60 fps, H.264 yuv420p, libx264) and `<name>.webp`
  (1280x720, 30 fps). Default `attack<moments>_<first outfit>`.
- `--sheets` frame sheets `frames<moments>_N.png`, `key<moments>_full.png`, `swaps<moments>.png`, `cut_debug.png`
  and `timing.md` (both variants) for the first outfit.
- `--look` native-size crops (1200x800 px of the 2560x1440 frame) into `<outdir>/look/`, named
  `a<moments>_f<frame>_<what>.png`; the three-moment run adds the feet with and without shadows.
- `--stills mix1,mix2` makes `outfit_stills.png`: ready and strike per outfit, rendered by the same code.
- `--no-video` writes the pictures only (about 20 s).
- `--anchor wind=650` overrides a moment's anchor (drawing x) if the foot finder picks the wrong foot. It prints the
  anchors on every run.

Every run prints the strike's spot, the cut's circle and one check: how far the wind-up's blade tip on the last dash
frame lies from the cut's circle, and at which angle (the blade's true path length; the cut shows `CUT["span"]`).

`check_layers.py [folder]` checks the layer path: the placed sword against the folder's own `weapon.png` (IoU), the
feet found, and the outfits at ready and strike (`<outdir>/layers_check.png`).

Render time: about 35-37 s per outfit (three moments: 292 frames; four: 298), plus a few seconds of encode.

`v1_backup/` holds the first draft's scripts as they were (plain copies, not run from there).

## Files
- `common.py` stage numbers from the Unity battle code, outfits, paths.
- `layers.py` manifest -> each moment stacked in the manifest order (shirt hidden under a chest piece, hair under a
  helm; pieces only as the manifest lists them), the sword placed from its art (grip at 0.79 of its height,
  `grip_xy`, `angle`, `scale_px_per_art_px`) on a padded canvas, trimmed and scaled to game size (hero_h -> 240
  design px). It also finds the feet: the body layer's bottom edge near the soles, back/front by side.
- `scene.py` background, monster, soft ground shadows, `place()` (move, lean about a ground point, squash, flat tint),
  `point()` (where a point of a placed figure lands).
- `contact.py` the cut's circle (centre on the strike blade's line `CUT["shoulder"]` blade lengths behind the grip,
  radius to the strike's blade tip) and where the strike stands, the further back of two rules: the monster's middle
  lies `CUT["depth"]` of the cut's width inside its outer edge (the thick end of the cut runs through a small
  monster's body), and the sword hand's grip stops `CUT["clear"]` px short of the monster's front edge (a wide
  monster never lies on his hand; the blade and the cut's thick end still reach into its front). The hit shows where
  the middle of the cut's band crosses the monster's middle height.
- `timeline.py` the timing table (beats, effects, their Unity names), the cut's data (`CUT`) and the per-frame state.
- `fx.py` the effects, flat and hard-edged: the cut, impact, sparks, dust, streaks, number, caption.
- `sheets.py` the pictures for judging and `timing.md`.

## Positions (design px, from BattleScreen.cs / Figure.cs / Backdrop.cs)
Ground y 500, hero home x 330, monster x 950. The background covers the screen, lifted 90 px, with a dark strip
below. Figures are 240 px tall (at most 300 wide) times the look's scale; the monster is mire_toad (the Fen Road's
common encounter), look scale 0.9, mirrored to face left: 270 x 204, standing on the ground. Its raw art carries
faint alpha noise (values 1-2) over the whole canvas, so it is cut as `prepare_art.py` cuts the game's sprite
(alpha under 10 cleared, trimmed, 6 px pad); the first draft trimmed the whole canvas and drew it at 64% size,
40 px above its shadow. Shadows are Figure.AddShadow: Art.Soft (alpha (1 - d)^2) at 0.7 x the drawn width, 26 px
tall, 45% black, under the feet; the hero's follows his x and shrinks as he hops.

How a moment sits on the figure's x (the anchor, measured from the layers, never hard-coded):
- ready: the middle between its feet.
- wind: its back foot on ready's back foot, so the swap keeps the planted foot; through the settle, hold and crouch
  the squash is corrected so the back foot stays exactly planted.
- strike: its front foot. The strike's x is solved by `contact.py`.
- follow: its front foot on the strike's front foot.
- The dash is a hop: every wind-up frame is in the air, and the landing is the strike on the hit frame.
- The way home: the last pose (strike, or follow-through) lifts off and moves back `BACK_PX` (the start of the hop),
  then swaps to ready in the air with the torso kept in place; ready carries the speed on and lands at home.

## What each part is in Unity
| here | Unity |
|---|---|
| a moment shown | the figure's sprite stack for that pose (`SetPose`); sprites per layer at their manifest order |
| hard swap | `SetPose(name)` on one frame, no ghost of the old pose (BattleScreen.SwapPose minus its `Ghost`) |
| x / hop / lean / squash | `Anim.Float` on `Fighter.X/Y/Angle/Sx/Sy` (lean pivot = a foot: move X by the pivot offset) |
| idle breath | `Idle()`: Breath 1 -> 1.03, 0.9 s yoyo |
| freeze | `Fx.HitStop(100)` |
| camera kick / shake | `GameRoot.Kick` / `GameRoot.Shake` |
| the cut | one generated mesh (an opaque crescent: ink under-shape, cream body, gold outer band) placed at the circle's centre; whole on the hit and through the freeze, then its start angle is tweened toward the blade (`Anim.Float`, width scaled a little); no alpha tween |
| impact, sparks, dust, streaks | `Fx.Impact`, `Fx.HitSparks`, `Fx.Dust`, `Fx.SpeedLines`, drawn flat |
| dash ghosts | figure copies tinted flat cream, fading (`Fx.FadeGhost`) |
| monster white, knock-back | `Fx.WhiteFlash` (60 ms), `Anim.Float` on its X/Angle/Sx/Sy |
| damage number | `Ui.FloatText` |
| shadows | `Figure.AddShadow` (already in the game) |

Draw order: shadows < monster (with its white flash) < the cut < dash streaks and ghosts < impact and sparks < hero
and sword < dust < number.

The exact frames and values are in `<outdir>/timing.md` (written from `timeline.py`).
