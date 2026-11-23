# Drawn skin on the clean 3D hero (look test, 2026-11-21)

Question: can the image model's drawing be the 3D hero's surface (one texture, unlit), so armor is made once and
follows every move? The 3D (art/redo/hero3d_v2/round2, read-only) only carries motion and fit; the look comes from a
drawing projected onto it. Pictures: `art/tests/hero3d_skin/`. Working files: `art/redo/hero3d_skin/` (not in git).

Tools: Blender 5.2 `B="C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe"`, venv python
`PY="C:/Users/Epic/Desktop/Animation Workflow/.venv/Scripts/python.exe" -I`, ComfyUI (Qwen-Image 2.1) at :8188.
`R=art/redo/hero3d_skin`.

## Rerun (about 6 min of scripts plus 2 Qwen runs, ~3 min)
1. Scale-mail shell (Shirt torso pushed out 14 mm, Shirt's weights; two flattened-sphere pauldrons on the upper arms):
   `$B -b art/redo/hero3d_v2/round2/hero.blend --python scripts/hero3d_skin/skin_blend.py -- mail $PWD/$R/mailbase.blend`
2. One UV atlas over every body object (smart project + pack, 2048) and the old flat look baked as fallback:
   `$B -b $R/mailbase.blend --python scripts/hero3d_skin/skin_blend.py -- prep $PWD/$R/work.blend 2048`
3. Projection views (texel world position/normal/id baked in that pose; camera position + object-id passes; flat render):
   `... -- view $PWD/$R/v_stance stance 40 5 1400x1600 822.86 0.9` (game view, the stance with the head turned 22 deg
   toward the viewer so both eyes show as in the drawing), `v_back spread 130 ...` (back three-quarter, limbs apart),
   and the same two with `--mail` (`v_stance_mail`, `v_back_mail`).
4. Fill drawings for the back views: render the skinned model from the view (`show <atlas> $R/v_back skinned:spread:130:5:1400x1600:822.86:0.9`),
   then `$PY scripts/hero3d_skin/redraw.py $R/v_back/skinned.png art/raw/char_hero_bare.png $R/q_back_nr 601 --noref`
   (`--outfit` for the scale mail). Only ONE reference: with the drawn hero as `<image2>` Qwen copied him whole and
   ignored the render (q_back_s501.png).
5. Register + project, part by part (`project.py`): global fit by figure height and feet; each drawn pixel gets a
   colour class and goes to the nearest render part that can carry it; per part a DIS optical flow between the signed
   distance fields (smoothed, sigma 10 px); the head is one group with a similarity transform anchored on the
   headband and the chin, the face takes it with no flow (so the drawn eyes/brows/mouth keep their layout). Bands of 5
   px at part edges, depth jumps and the drawing's rim are skipped.
   `$PY project.py $R/v_stance art/raw/char_hero_bare.png $R/L_stance_bare.npz`
   `$PY project.py $R/v_back $R/q_back_nr_s601.png $R/L_back_bare.npz --white`
   `$PY project.py $R/v_stance_mail $R/mail_drawn_cut.png $R/L_stance_mail.npz --fit-from art/raw/char_hero_bare.png`
   (`mail_drawn_cut.png` = char_hero_scale_mail.png cut off its purple field; same canvas as the bare drawing, alpha
   IoU 0.92 with it, so the fit is taken from the bare one. Made by: alpha = max channel distance from (163,55,211)
   > 60, binary opening x2, largest component, holes filled; the same code is `drawn()` in sheets.py.)
   Route (b) test for the game view: `redraw.py $R/v_stance/skinned.png ... $R/q_stance_nr 801 --noref --angle
   "a three-quarter view, he faces right with his chest toward us"`, projected with `--white` as layer 1
   (`atlas_bare_b.png`, `route_ab_compare.png`): body parts register at 0.94-0.99 IoU and the folds come out clean;
   the head failed to register (headband anchor not found), so its face came from the later layers.
6. Atlas, hard priority (first layer wins, never a blend), nearest-colour grow of 24 texels per object, then the old
   flat look for what no view saw: `$PY compose.py $R/atlas_bare.png $R/v_stance/tex_id.npy $R/fallback.npy
   $R/L_stance_bare.npz $R/L_back_bare.npz` (`_holes.png` = unpainted alpha 0, `show --magenta` paints them magenta).
7. Renders (unlit texture + the hull outline) and sheets: `show $R/atlas_bare.png $R/final b_stance:stance:40:5:1300x1200:548.6:0.85:sword ...`
   (`--mail` for the m_* row), then `$PY scripts/hero3d_skin/sheets.py $R/final art/tests/hero3d_skin .`
Poses: `skin_poses.py` (poses.py vocabulary). In this frame `arm_fwd` positive swings the raised arm BACK (found by a
numeric search, the wind-up's fist above/behind the head). Sword hand = socket_hand_R; `show` prints the near-hand margin.

## What one new armor costs by this route
- Geometry: a shell made by script (minutes) or modelled by hand for anything with its own shape (hours).
- The drawing in the game view: one Qwen outfit edit of char_hero_bare (we already have them: char_hero_<armor>.png),
  1-2 runs (~70-100 s each).
- Fill views: one back three-quarter redraw (1-2 runs), plus a far-side view for full coverage (1-2 runs).
- Scripts: ~2 min. Hand steps: picking seeds; checking that the drawing's armor edges match the shell (here the
  far pauldron and the far side of the vest had no drawn source).
So about 4-6 Qwen runs and 15-20 minutes per armor, if the result were good enough (see the verdict in the report).
