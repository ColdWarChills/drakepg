# Ways to animate the hero: one sword blow, five ways (2026-11-21)

Sword grip fixed (later on 2026-11-21, after "Still the same problem with inverted fists"):
- `fists_old_new.webp`: the sword hand at the wind-up, the hit, after the hit and ready, for today's blow, the
  Nov 18 bones cut, way C's first try and way C fixed. Fixed: the blade leaves the fist by the thumb and first
  finger in every moment; the others show a reverse grip at the wind-up or the handle along the forearm at the hit.
- `fists_half.mp4`, `fists_x1.mp4`, `fists_stills.webp`: the same four as clips.
- `fists_3d_frames_0_19.webp`, `fists_3d_frames_19_38.webp`: the 3D swing's hand in every frame, take 2 over take 3.
  `fists_3d_closeup.webp`: take 3 at 2x (the sword is one piece through the fist).
- `fists_drawn_frames_0_19.webp`, `fists_drawn_frames_19_38.webp`: way C's hand in every frame: first try, fixed,
  and the fixed 3D driver under it. The drawn take holds the wind-up and jumps to the hit in one frame.

Look at these first:
- `gamesize_x1.mp4`, `gamesize_half.mp4`, `gamesize_stills.webp`: way C cut off its backdrop and put on the Fen Road
  fight backdrop at game size (hero 240 px in the 1280 frame), beside today's blow from a real fight; also with the
  scale-mail picture. `gamesize_2x_hit.webp`: the hit frame at 2x (as on a 1440p screen): no grey fringe, but the
  sword blurs almost away in the fastest frames.
- `compare_x1.mp4`, `compare_half.mp4`, `compare_stills.webp`: today's drawn poses (in a real fight) | the 2D-bones
  cut of Nov 18 (the last whole-body bones blow; its panel follows him as he runs) | A: AI video from one picture (LTX) | B: the 3D hero in Blender | C: the 3D swing turned into the
  drawn look (Wan-Animate-2). Every hero at the same height, feet on one line.
- `gear_x1.mp4`, `gear_half.mp4`, `gear_three_rows.webp`, `scale_mail_close.webp`: the same 3D swing with a
  scale-mail picture as the reference: the armor stays on through every frame. Rows: 3D driver, plain shirt,
  scale mail.

The rest:
- `drawn_look_over_3d.webp`: way C (rows 1 and 3) over the 3D frames that drove it (rows 2 and 4).
- `drawn_look_takes_1_2.webp`: two runs of way C (take 1 kept: take 2 smears at the turn, its lunge is shallower).
- Against today's blow: C wins on hands, elbows, the grip, the in-between frames and the armor; at the hit today's
  drawn blow (and the bones cut) is still stronger: deeper lunge, shouting face, smear. C's face stays blank because it
  follows the 3D copy's face, and C stands narrower than the drawn ready stance.
- `ltx_take3_close.webp`, `ltx_take2_sheet.webp`: way A. Looks drawn, but the sword hand is not under control (it
  turns to face the camera and the sword ends in the other hand), fast frames smear, and the blow takes 3 s.
- `blender_3d_sheet.webp`: way B alone. Hands and elbows are right by construction, but the generated 3D model
  looks muddy next to the drawn hero.

Scripts and the how-to: `scripts/hero3d/HOWTO.md`. Frames, mesh and takes: `art/redo/animway/` (not in git).
