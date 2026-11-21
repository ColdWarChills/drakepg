# Hero moves: 3D swing + drawn look (test, 2026-11-21)

A move is keyed once on a 3D copy of the hero in Blender (the hands reach for their keys, within a few cm; the render
log prints each miss, up to 10 cm in this take's wind-up; the sword never leaves the fist). That render drives Wan-Animate-2 with a drawn picture of the hero as the reference, which gives a drawn
clip. A new armor is a new reference picture and one more run (~2 min); the 3D move is reused.
Proofs: `art/tests/sheets/animway/` (README there). Local data: `art/redo/animway/` (not in git).

Blender: `"C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe"` (5.2), run headless (`-b`).
The Blender MCP is set up only in the Animation Workflow project, so these scripts don't use it.

## Making the 3D hero (once)
1. `python scripts/hero3d/views.py <out dir> front,side,back 11,12`: Qwen draws the base hero as front (A-pose),
   side and back views (ComfyUI must run).
2. `python scripts/hero3d/cut_views.py ...`: square 1024 views, same body height, feet on one line (`cutB`).
3. Pixal3D (Animation Workflow `work/local_pilot/pixal_pilot.py --images front=.. right=.. back=..`) makes a GLB.
4. `blender -b --python scripts/hero3d/prep_mesh.py -- <in.glb> <mesh dir> --faces 60000`: stand, scale, toon
   material, outline, `verts.npz`.
5. `<Animation Workflow .venv python> -I scripts/hero3d/landmarks.py <mesh dir>`: joints (`joints.json`).
6. `blender -b <mesh dir>/hero_mesh.blend --python scripts/hero3d/project_paint.py -- <cutB> <new mesh dir>`:
   repaints the mesh from the drawn views (front and back; the side view only for the head; a band along each drawn
   edge is skipped, its rim light would paint stripes).

## A move
1. `python scripts/hero3d/move_slash3.py slash3.json`: the keys (grip point + blade direction per hand key, feet,
   body turns; `th` = the blade's angle from square to the forearm). `slash3.json` is kept here (`slash2.json` is
   the first try, with the blade in line with the arm at the hit).
   The sword hand is solved like a wrist: the blade always leaves the fist by the thumb and first finger, tilted
   toward the forearm at most 60 deg (wrist 30 + the handle turning in the fist 30), back toward the elbow 25. For a
   downward cut the forearm must point lower than the blade: at the hit, arm about 45 deg down, blade level. The log
   line `HAND` prints each key's asked and given angle and the forearm and blade angles on screen.
2. `blender -b --factory-startup --python scripts/hero3d/rig_hero.py -- <mesh dir> slash3.json <frames dir>
   --size 832x480 --ortho 3.8 --target 0.62,0,0.86 --outline 0.004 --frames 0-38` (25 s). Prints how far each
   hand misses its key.
3. `python scripts/hero3d/comp.py <frames dir> <out dir>`: outline from the alpha, plain grey backdrop.
4. ffmpeg the frames to a 24 fps mp4, padded to a 4k+1 frame count (41).
5. Reference: the drawn hero with the sword in the near fist, placed at the size and spot of the 3D hero in frame 0.
6. From the Animation Workflow folder: `.venv/Scripts/python.exe -I <repo>/scripts/hero3d/hyb_animate.py driver.mp4
   reference.png out.mp4 --seed 1 --length 41 --size 832x480 [--outfit "a steel scale-mail vest ..."]`.

Check the hand before anything else: add `--track track.json` to step 2 (grip, tip, wrist and elbow pixels per frame), then
`python scripts/hero3d/hand_strip.py track.json strip.jpg "3D=<frames dir>" "drawn=<drawn frames>:1"` shows the hand in every frame;
`fist_sheet.py` puts four moments of four versions side by side; `compare.py --fists` is the same as a clip.

`key_flat.py` cuts a take off its grey backdrop (`<venv python> -I key_flat.py <frames> <out>`);
`gamesize_clip.py` puts it on a fight backdrop at game size beside today's blow. `compare.py` builds the side-by-side clip of all ways (`--speed 0.5` for half speed, `--still t1,t2` for a sheet).
