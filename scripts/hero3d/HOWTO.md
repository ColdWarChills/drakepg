# Hero moves: 3D swing + drawn look (test, 2026-11-21)

A move is keyed once on a 3D copy of the hero in Blender (hands go where the keys say, the sword never leaves the
fist). That render drives Wan-Animate-2 with a drawn picture of the hero as the reference, which gives a drawn
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
1. `python scripts/hero3d/move_slash2.py slash2.json`: the keys (grip point + blade direction per hand key, feet,
   body turns). `slash2.json` is kept here.
2. `blender -b --factory-startup --python scripts/hero3d/rig_hero.py -- <mesh dir> slash2.json <frames dir>
   --size 832x480 --ortho 3.8 --target 0.62,0,0.86 --outline 0.004 --frames 0-38` (25 s). Prints how far each
   hand misses its key.
3. `python scripts/hero3d/comp.py <frames dir> <out dir>`: outline from the alpha, plain grey backdrop.
4. ffmpeg the frames to a 24 fps mp4, padded to a 4k+1 frame count (41).
5. Reference: the drawn hero with the sword in the near fist, placed at the size and spot of the 3D hero in frame 0.
6. From the Animation Workflow folder: `.venv/Scripts/python.exe -I <repo>/scripts/hero3d/hyb_animate.py driver.mp4
   reference.png out.mp4 --seed 1 --length 41 --size 832x480 [--outfit "a steel scale-mail vest ..."]`.

`compare.py` builds the side-by-side clip of all ways (`--speed 0.5` for half speed, `--still t1,t2` for a sheet).
