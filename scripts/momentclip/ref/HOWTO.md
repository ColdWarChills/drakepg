# The hero in moments: how to rebuild it and how to add a piece

Each moment of an attack is one whole Qwen drawing of the bare hero, his sword fist drawn around a thin green rod.
Each armor piece is an "Edit <image1>. Change only ..." edit of that same drawing; code cuts the changed pixels into a
layer. Nothing is rigged or turned: Unity shows plain sprites stacked in a fixed order.

Python: `"C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe" -I -B <script>` from this folder.
ComfyUI with Qwen-Image 2.1 must answer at http://127.0.0.1:8188 (about 100 s a run).

## Files
- `common.py` canvas (1536 square, the bare hero 1000 px tall, soles at y 1390), the ComfyUI call (RGBA in and out),
  the HERO/STYLE texts.
- `qwen_moments.py <job>:<seeds>` one Qwen run per seed into `art/redo/moments/<job>_s<seed>.png` (skipped if it
  exists), logged in `runs.jsonl`. Jobs: `sleeve_short` (the base drawing with a plain short sleeve),
  `plain_short_<moment>` (a pose of the sleeve base; poses and the rod text in `POSES`), `<piece>@<moment>`.
- `pieces.py` the ten pieces, their slot rules and prompts. A piece on ready uses the game's icon (or the torso of
  its outfit picture) as `<image2>`; on the other moments `<image2>` is the piece cut from its ready layer, so the
  design holds.
- `art/redo/moments/moments.json` the kept tries: `raw` (the picked pose tries), `ready/wind/strike/follow` (the same,
  head-size normalised: `norm_*`), `pieces` -> piece -> moment -> kept edit.
- `bare.py` reads the rod (fist centre = the gap between the two green ends, angle from the green pixels, blade on the
  thumb side declared per moment in `MOMENTS`), takes the rod out, places the weapon (grip point and size from the
  weapon data) and cuts the sword fist, which goes above the weapon. `normalize()` scales a pose to the base's head.
- `place.py <bare> <edit>...` the place check: one shift for the whole picture (median of the zones' shifts), then
  per zone the silhouette overlap and how far its drawing moved.
- `layers.py` the cut (changed pixels after a 1.5 px blur, threshold 42, 16 inside the slot's own bare colours, small
  blobs dropped, kept only inside the slot's territory found from the bare drawing's colours; detached crumbs
  dropped), the order and `dress()`. Two body parts are their own sprites so a piece can take them away:
  - hair (`hair_mask` + `hair_split`): hair, headband, every red tail near the head (not an open mouth), its rim, and
    any thin or detached leftover next to it. Hidden under a helm; the helm layer carries every edit pixel there.
  - shirt (`shirt_mask`): the shirt in light and shade with its sleeves and outline. Hidden under a chest piece; the
    chest layer carries every edit pixel there, so an armor drawn narrower than the shirt leaves nothing behind.
  Detached bits of a layer are dropped as crumbs (Qwen's ghost lines), except in the footprint of the sprite the
  piece hides: under a helm, bits that touch the face (eyes, brows); under a chest piece, the whole shirt footprint.
  The head is found from the headband: the red blob with the largest area x share of dark hair round it
  (`place.red_blobs`/`head_box`). This rests on the bare hero's red band.
- `build.py` everything above for all kept tries -> `art/redo/moments/layers/<moment>/*.png`, `layers/manifest.json`
  (weapon spot, angle, scale, order, sprite boxes) and `checks.json` (place check, leak outside the slot, layer vs edit).
- `masks.py` 10 seconds, before any build on new moments: head box, hair and shirt sprites drawn over the four bare
  moments (`art/tests/moments/masks_check.png`). A new pose can break the head finding (it did at the strike).
- `sheets.py all` the sheets in `art/tests/moments/`; `make_numbers.py` writes `numbers.md`; `gate1.py` the hand check.

## Rebuild everything from the kept tries
```
python build.py
python sheets.py all
python make_numbers.py
```

## Add one armor piece (example: Ridgeplate Helm)
1. Add it to `PIECES` in `pieces.py`: slot, words, icon key (`icon_<id>` in art/raw; None for legs).
2. `python qwen_moments.py ridgeplate_helm@ready:801` and add the kept try under `pieces` in moments.json, then
   `python build.py` (the ready layer becomes the reference for the other moments).
3. `python qwen_moments.py ridgeplate_helm@wind:802 ridgeplate_helm@strike:803 ridgeplate_helm@follow:804`, add them
   to moments.json, `python build.py`, look at the row in `checks.json` (leak small, layer vs edit small) and at the
   sheets. About 4-5 runs, ~8 minutes of PC time.

## Unity
Per moment, bottom to top: weapon (only in a moment whose `weapon_order` is `back`), `body`, `shirt` (hidden when a
chest piece is worn), `hair` (hidden when a helm is worn), legs, boots, chest, gloves far hand, weapon (`front` moments), `fist`, gloves near hand, helm. Every
sprite is a full-canvas PNG at spot 0,0 (the manifest has each trimmed box). The weapon sprite: the weapon picture
with its grip point at `grip_xy`, turned by `angle` (0 = blade up, clockwise), scaled by `scale_px_per_art_px`.
