# Clean 3D hero, v2 (look test; round 2, 2026-11-21)

The bare hero modelled clean by a Blender script, no generated mesh and no painted texture. Every part is a loft of
superellipse rings (sizes first read off the drawn orthographic views `art/redo/animway/cutB`, 913 px = 1.75 m, then
made lean after `art/raw/char_hero_bare.png` in round 2), subdivided once, one flat colour per garment, the drawn face as
the one small texture. Look = what one simple Unity shader plus one 2D pass can do, all numbers in `hero_look.json`:
- albedo (flat colour or the face texture) with a hard step on N.L from one light fixed to the screen
  (`hero_lib.LIGHT_CAM`, `CUT`), lit = base, else shade;
- a forced-shade mask: colour attribute `shade`, R = 0/1 (neck, under the fringe, inside the cuffs, over/under the belt,
  inner thighs, under the boot tops);
- inner lines as thin flat-coloured geometry lying on the surface (finger lines, four shirt folds, the blade's fuller);
- an inverted-hull outline (`OUTLINE_M` x per-object scale x per-vertex factor = G of `shade`, 0 on inner lines);
- the pale warm edge as a 2D dilate of the finished picture's alpha (`sheets.py rim()`, 0.8 px at 720p, #d2b06a).

Rebuild everything: `powershell -ExecutionPolicy Bypass -File scripts/hero3d_v2/make_all.ps1` (about 2 min). It writes
`art/redo/hero3d_v2/` and `art/tests/hero3d_v2/`, copies both into their `round2\` folders and writes READY.txt last. It
never writes into `round1\` (the archive), `round1_clip\`, `u2\` or `unity\`.

Steps it runs (Blender 5.2 `"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"`, venv python =
`"C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe" -I`):
1. `python make_face.py <cutB/front.png> face.png`: the face, cleaned to flat colours on the exact skin.
2. `blender -b --factory-startup --python build_hero.py -- <out dir> <face.png> [--head 1.24] [--fist 1.08]`:
   model + rig + weights + sword + mask, bakes the action "Stance" (poses.py, frames 1 and 24), writes `hero.fbx`,
   `hero.glb` (no hull), `hero_look.json` (v2, after export) and `hero.blend` (with the hull).
3. `blender -b hero.blend --python render.py -- <look|turnaround|bends|face_turn|overlay|fit> <frames dir>`.
   `look` renders the baked Stance (the same action and meshes as the FBX), with and without the sword, plus masks;
   it asserts that socket_hand_R (his right fist, the sword hand) is nearer the camera than socket_hand_L.
   `fit` renders nothing: it prints the live `pose_stance` landmarks as fractions of height beside the drawn hero's.
4. `python sheets.py <look|turnaround|bends|face_turn|overlay> <frames dir> <out png> <repo>`: `look` writes
   look_gamesize.png, look_3x.png (bare | sword | drawn), silhouette.png and sword_compare.png (the 3D sword beside the drawn one in today's game, x3), and prints the silhouette IoU, the
   landmarks and the sword-over-head/chest pixel counts; `bends` also writes bends_zoom.png.
5. `blender -b --factory-startup --python check_fbx.py -- hero.fbx`: re-imports FBX and GLB, asserts at most 3
   influences and a 0/1 `shade` layer, prints bones, actions, materials.

Poses live in `poses.py` (no side effects; used by build and render). Pose vocabulary works in the body frame.
`pose_stance` was fitted with `render.py fit` to the drawn stance (far left leg forward, near right leg back, hips turned
toward profile and chest turned back so the stride reads wide at yaw 40, far fist raised at chest height, near fist low
by the hip). Every pose is grounded by moving Hips so the lowest body vertex (not the sword) sits on z = 0.

Model facts: faces -Y in Blender (+Z in Unity after FBX), his right = -X. A-pose, arms 45 deg down. Bones named for
Unity's Humanoid (Hips root; Spine, Chest, Neck, Head; Left/Right Shoulder, UpperArm, LowerArm, Hand, UpperLeg,
LowerLeg, Foot, Toes), plus `socket_hand_R` / `socket_hand_L` (head = grip point in the fist, +Y = blade out by the
thumb, +Z = back of the hand; socket_hand_R deforms, the Sword mesh is weighted 100% to it) and `socket_head`, plus the
headband tails `BandA_1..3`, `BandB_1..3` (chains under Head, for sway). Objects per garment so armor can hide them:
Head (face, ears, nose, neck), Hair, Headband, Shirt (torso, collar, sleeves, V-neck, fold lines), Arms (forearms),
Hands (fists, finger lines), Belt, Trousers, Boots; gear: Sword. Weights: smooth blends at each joint (the torso's
armpit follows the upper arm 30%, the rolled cuff follows the forearm 45%), at most 3 bones per vertex, linear skinning.
About 20.6k vertices without the sword (subdivision level 1).

One-off: `clip_round1.py` + `check_clip.py` made `art/redo/hero3d_v2/round1_clip/` (round-1 model, clips Stance and
TestSwing) for the Unity playback test; make_all does not run them.
`measure.py` and `sample_colors.py` print the numbers the round-1 tables were typed from.
`measure_sizes.py <repo> <out dir>` prints the outer dark line width (per 1000 of height) in the drawn hero and the 3D render and writes both scaled to 1000 px tall with a grid (grid_drawn.png, grid_3D.png) for reading head and fist sizes; at head 1.24 / fist 1.08 the head (headband-to-chin, ear-to-nose) is about 1.03-1.12 and the fists about 1.03-1.06 of the drawn hero's, the line 4.1 vs 3.9 per 1000 (17 drawn samples only).
