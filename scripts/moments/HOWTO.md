# The hero in moments: how to rebuild it, add a moment and add a piece

Each moment of an attack is one whole Qwen drawing of the bare hero, his sword fist drawn round a thin two-colour marker
stick. Code reads the stick (where the fist is, which way the blade leaves it) and puts the real weapon picture there.
From step A3 on, every moment also has a **clean drawing**: the same drawing with the stick gone and the hand a whole
closed fist. Everything the player sees comes from the clean drawing. Each armor piece is an "Edit <image1>. Change
only ..." edit of a drawing; code cuts the changed pixels into a layer. Nothing is rigged or turned: Unity shows plain
sprites stacked in a fixed order.

Python: `"C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe" -I -B <script>` from this folder.
ComfyUI with Qwen-Image 2.1 must answer at http://127.0.0.1:8188 (about 100-180 s a run; two scripts may queue runs at
the same time, ComfyUI does them one after the other).

## Files
- `common.py` canvas (1536 square, the bare hero 1000 px tall, soles at y 1390), the ComfyUI call (RGBA in and out),
  the HERO/STYLE texts.
- `qwen_moments.py <job>:<seeds>` one Qwen run per seed into `art/redo/moments/<job>_s<seed>.png` (skipped if it
  exists), logged in `runs.jsonl`. Jobs: `sleeve_short` (the base drawing), `grip4_<pose>` (a pose of the base with the
  two-colour marker; the poses are `GRIP_POSES`, the marker words `marker4`), `<piece>@<moment>` (an armor edit).
- `cut.py`
  - `read_marker`: the two-colour marker. The long **green** part is the blade side and leaves the fist at the thumb and
    forefinger end; the short **magenta** stub is the pommel side, under the little finger. The fist centre is the
    middle of the gap between the two colours; `d` points from the stub toward the green part; `angle` is 0 = blade
    up, clockwise. It refuses a picture that does not say it clearly (a colour missing, the stub not shorter, the two
    parts not on one line, a cross or a blade-shaped green: "marker is not one straight stick"). Drawings made before
    the marker (ready, strike) have a one-colour rod and their blade end checked by eye (`bare.BLADE_BY_EYE`).
  - `weapon_layer`: the weapon picture with its grip point on the fist centre at the marker's angle.
  - `fist_copy` (step A3): the sword hand copied from the clean drawing, by positive colour: the skin of the sword hand
    (split from any other limb that touches it by `own_arm`: erode until the fist core and the far skin fall apart,
    grow both back), closed, plus its own pale rim and warm dark line within 2 px and the drawing's soft outer edge.
    Where the weapon passes under the forearm beyond the fist, that forearm skin comes too. Cool pixels at its edge
    (shirt, trousers: green at least red) are left out. Edge: the drawing's own
    alpha on the outer contour, a 1 px half-alpha feather inside the figure.
  - `remove_rod`, `fist_mask`: step A2's way (inpaint the stick, hard-cut the fist from the marker drawing); kept only
    for old records and for looking at new pose tries.
- `bare.py` `read_blade` (marker or eye), `build(marker, moment, name, clean=...)`: with a clean drawing the sandwich
  below, without one step A2's way; `normalize()` scales a pose about the soles to the base's head size (`norm_*`).
- `grip.py` the grip check (see below): `grip.py test`, `grip.py try <tries>` (also prints the tip room), `grip.py
  table`.
- `handpass.py <job>:<seeds>` a redraw of the sword fist alone on a 3x crop with a second picture (the ready fist, turned
  on its own picture so its forearm points the same way), pasted back by code. Used for the wind-up's hand.
- `clean.py` the clean drawing (below). `clean_checks.json` holds its numbers per try.
- `fetch.py <prompt id> <name>` saves a finished ComfyUI run whose Python caller was stopped.
- `layers.py` the cut of armor edits (changed pixels after a 1.5 px blur, threshold 42, 16 inside the slot's own bare
  colours, small blobs dropped, kept inside the slot's territory from the bare drawing's colours), the order, `dress()`.
  Hair and shirt are their own sprites so a helm or a chest piece can hide them (that piece's layer then carries every
  edit pixel in the hidden sprite's footprint).
- `pieces.py` the ten pieces, slot rules and prompts. `ON_CLEAN`: pieces made on the clean drawing (from step A3 on,
  under their own ids, e.g. `scale_gauntlets_a3`); their prompts say "the right fist stays an empty closed fist", no rod
  words anywhere, and `build.py` cuts them against the clean drawing. Such a near glove hides the fist copy
  (`hides_fist` in the manifest) and carries every edit pixel in its footprint, plus the changed pixels within 14 px of
  it (the glove is bigger than the bare hand, so the near/far split by nearest bare skin would notch its edge); pixels
  the edit left unchanged (outside the fist footprint) and 1 px spurs are left out. `THUMB_NOTE` says outright where
  the sword hand's thumb is for a moment whose glove tries moved it (wind-up: the left end).
- `build.py` everything for the kept tries -> `art/redo/moments/layers/<moment>/*.png` (each folder holds only what the
  manifest lists; it is emptied first), `layers/manifest.json`, `checks.json`.
- `fistlook.py try <moment> <clean tries>` / `fistlook.py all` / `fistlook.py follow <list.json>`: the look files in
  `art/tests/moments/` (fist4x, fists_game, glove4x, bare_strip_1/2, follow_tries).
- `masks.py`, `sheets.py`, `make_numbers.py`, `gate1.py`: older sheets and checks.

## Step A4: the ten pieces on three moments (2026-11-22)
Three moments only: ready, wind, strike (the follow-through is dropped; its files stay on disk, `build.py` and the
manifest no longer list it). Picks: `moments.json` `pieces_a4` = {plain piece name: {moment: try}}; the layers and the
manifest use the plain names (`scale_gauntlets`, not `scale_gauntlets_a3`).
- **The design is the ready layer.** `refs.py freeze <piece> <ready try> [clean|marker]` / `refs.py layer <piece>
  <layer png>` save the piece alone, cut from its ready layer and enlarged to ~900 px, into `art/redo/moments/refs/`
  (frozen: `build.py` empties the layers folders, so a reference never comes from there). A glove's reference is the
  sword-hand glove alone (split from the other hand by `cut.own_arm`), plus one copy per moment turned on its own
  picture to that moment's forearm (`grip.ARMS`). A helm's reference has its face taken out (Qwen copied the calm
  mouth). Gloves may add the other hand's glove (`refs/<piece>_far_ready.png`) as a third picture (`FAR_WORDS`).
- **Jobs** `<piece>_a4@<moment>`: an edit of the clean drawing, second picture = the frozen reference, words =
  `pieces.DESIGN` (written after looking at the ready piece: material, colours, where plates and rivets are), for a
  glove which side of the fist faces us (`SIDE`, `SIDE_PLAIN`) and where the other fist is (`OTHER_FIST`, `PAIR`:
  without it Qwen gloved only the sword hand). `<piece>_a4fix@<moment>`: a local fix on top of a kept A4 try
  (`FIX`, e.g. the leather armor's missing shoulder pad). `runs.jsonl` logs each run's pictures under `refs`.
- **Kept step-A edits** (made on the marker drawing, or on the old wind-up body `norm_plain_short_wind_s212`) are allowed
  only when their layer stays clear of the stick's footprint grown 3 px and of `clean.region()` (wind-up: of where the
  clean wind-up differs from the old body, `windkeep.py`); `checks.json` `keep` says pass or FAIL.
- **Checks** (`checks.json`, per piece@moment): `same.py` numbers against ready (steel share, steel tone, leather/cloth
  colour, the Scale Mail's shoulder plate area) -> `vs_ready.verdict`; calibrated on the known failures
  (`same.py calib`: the s932/s936 glove pair and step A's Scale Mail must fail). Helms: `face_changed` (share of the
  middle of the face the edit changed; over 0.08 = re-roll) and the cut rule `layers.face_islands` (changed bits
  inside the face not joined to the helm are dropped). Gloves: `far_box` (the other hand's layer box; the near glove
  never grows into the other hand: `sword_side` in build.py), every layer `despeck`ed (8-connected bits < 24 px),
  `new_holes_px`, `see_through_px`, `specks`.
- **Proof**: `ident.py` (the bare hero stacked from `layers/` equals `layers_A3/`, pixel for pixel, per moment).
- **Look files**: `trylook.py <out> <fist|chest|head|legs|boots> <tries...>` (raw tries, quick); `look.py
  [strips|game|pieces|heads|gloves|seams|all|stats]` stacks the built layers in the clip renderer's order.

## The grip check (`grip.py`)
Angles on screen: 0 = his front (screen-right), 90 = up, 180 = his back, 270 = down. F = forearm, elbow -> wrist (read
by hand on the drawing: `ARMS`); T = where the blade leaves the fist (marker); turn = T - F, + = counter-clockwise.
Back of his right hand drawn -> turn +25..+120; palm side drawn -> -25..-120. The eye notes (`EYE`: side drawn, the
screen direction of the thumb end, near arm) are written from the raw crop before or beside the numbers; any
disagreement fails. The sword tip (the real training sword) may be up to 60 px below the soles line (3/4 view: the road
goes on toward the viewer); deeper is in the ground. `grip.py try` prints the tip room: the real blade reaches 657 px
from the grip, so a blade at T needs the fist at least 657 x sin|T| - 60 px above the soles (T -43: 388 px; T -50:
443 px). Qwen draws a hanging sword arm's fist 250-360 px above the soles. The `followe` pose (fist at the belt, arm
bent and pulled in) got the fist high enough but drew the stick as a hilt beside the fist, 3 of 3 (step A3).
`fistlook.py follow` lays out up to 8 tries in 4 columns, more in 6.

`grip.py table` rewrites only the block between its two `<!-- grip.py table ... -->` lines in
`art/tests/moments/hands_table.md`; the hand-written notes outside it stay.

## The clean drawing (`clean.py`) and the sandwich
`clean.py run <moment>:<seeds>`: a square crop round the marker (with its coloured fringe) and the fist, plus 48 px
(from the marker's extent: no hand-placed numbers), enlarged to about 1300 px and edited by Qwen: "remove the stick
completely ... his hand stays exactly as it is: the same closed fist, empty ... the same side toward the viewer, the
thumb exactly where it is ... where the stick lay over his body draw his body, over nothing transparent". The edit is
shrunk back, shifted onto the drawing (best whole-pixel shift at the enlarged scale on the unchanged ring), colour
matched on that ring (gain and offset per channel), and taken only inside the paste region (old and new sword fist and
the marker footprint with its fringe, grown 6-8 px, 1.5 px soft edge); marker-tinted pixels left there are scrubbed
(green or pink tint, Qwen's warm red redraw of the magenta fringe, and plain red or pink specks: `red_speck`; they
take the nearest clean pixel's colour and keep their alpha), and semi-transparent edge pixels redder or pinker than
the solid pixel beside them take its colour (`edge_tint`; the pale yellow rim is left alone).
Outside the region the clean drawing equals the marker drawing pixel for pixel. `clean.py repaste <try>` pastes a kept
edit again with today's code (no run). Checks (`clean_checks.json`): fist skin overlap marker vs clean >= 0.85, its
centre moved <= 3 px, no marker colour or fringe left, nothing changed outside the region; then by eye at 4x: the same
side of the hand toward us, the thumb at the same end, no seam at the wrist.

The sprites of a moment, bottom to top: [weapon if `back`], body = the whole clean drawing (minus the hair and shirt
sprites, which sit right above it), [weapon if `front`], fist = the copy of the sword hand from the clean drawing. Under
the fist copy lies the very same hand, so its inner edges and the wrist cannot show a seam; only the two places where
the weapon comes out from under the hand show its edge.

## Add a new moment
1. Pose: add a pose to `GRIP_POSES` in `qwen_moments.py` (place the fist by body landmarks; say which side of the hand
   we see and at which end the thumb is), run 3-4 seeds `python qwen_moments.py grip4_<pose>:<seeds>`.
2. Grip: `python grip.py try grip4_<pose>_s<seed>` (T, auto F, tip room). For a try you would keep: read elbow and wrist
   by hand into `ARMS`, write the eye note into `EYE` from the raw crop, `python bare.py norm <try>`, put `norm_<try>`
   in `moments.json`. If the hand shows the wrong side, `handpass.py` on that body with the ready fist as reference.
3. Clean: `python clean.py run <moment>:<seed>,<seed>`, `python fistlook.py try <moment> clean_<moment>_s<seed> ...`,
   pick one into `moments.json` `clean`, then `python build.py`.
Runs per accepted hand in step A3: the clean drawing held on the first or second run (see the report in
`art/tests/moments/hands_table.md`); a pose with a right grip took 4-10 runs in step A2.

## Add one armor piece (example: Ridgeplate Helm)
1. Add it to `PIECES` in `pieces.py` (slot, words, icon key) and, for a piece made on the clean drawings, to `ON_CLEAN`.
2. `python qwen_moments.py ridgeplate_helm@ready:801`, add the kept try under `pieces` in moments.json, `python
   build.py` (the ready layer becomes the reference for the other moments).
3. The other moments the same way, then `checks.json` (leak small, layer vs edit small) and the look files.
   About 4-5 runs, ~8 minutes of PC time.

## Unity
Per moment, bottom to top: weapon (only in a moment whose `weapon_order` is `back`), `body`, `shirt` (hidden when a
chest piece is worn), `hair` (hidden when a helm is worn), legs, boots, chest, gloves far hand, weapon (`front`
moments), `fist`, gloves near hand, helm. Every sprite is a full-canvas PNG at spot 0,0 (the manifest has each trimmed
box). The weapon sprite: the weapon picture with its grip point at `grip_xy`, turned by `angle` (0 = blade up,
clockwise), scaled by `scale_px_per_art_px`. `clean` in the manifest names the clean drawing a moment's sprites come
from.
