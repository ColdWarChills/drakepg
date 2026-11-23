# Night runs: what to generate (2026-11-23)

What the local Qwen can make while the designer sleeps or is at work, in the order to run it. Written against the repo at commit 30ba9ff (Nov 17); the designer's Nov 22 work is not pushed and is not covered.

How runs work (from `scripts/asset_batch.py`): each prompt is made twice (two seeds), tries land in `art/redo/<key>/`, and files that exist are skipped, so a stopped run picks up where it left off. The last batch took about 45 s per image (314 images). Qwen stays the model for all of it (model trial verdict, Nov 14).

After a run: review with `python scripts/redo_art.py sheet <keys>`, keep one with `python scripts/redo_art.py pick <key>:<seed>`. Rule from Nov 15: Claude rates the tries and shows the designer only what passes.

## 0. Before any run: one open question (added later on 2026-11-23)

The designer is looking for a more unique look that complements the effects, and has not yet said whether the current art stays (with paper damage on top) or a style trial comes first. Until he answers, do not run section 1: about 3 hours of pictures in a style that may change.

Paper damage (`docs/HANDOFF_2026-11-23.md`) may also replace the 31 hurt poses in `mon_all`. If its Unity proof passes, drop the hurt poses from that group and keep wind-up and strike.

## 1. Ready tonight: groups already in the script

```
python scripts/asset_batch.py bg mon_all unique npc_idle
```

About 3 hours: 133 prompts, 266 images, 30 of which already exist from the 5-monster test.

- `bg` (13 prompts): rooms for the bigger maps - Moss Glade, Mill Stream, Imp Den, Root Tunnel, Boar Run, four Fen rooms, four Frostfang rooms. For "Farm spots and loops in the three regions".
- `mon_all` (93): wind-up, strike and hurt poses for all 31 monsters. The 5-monster test passed on Nov 14. For "Key poses for all 31 monsters".
- `unique` (8): boss item icons. For "Gear that differs in kind" (one named piece per boss).
- `npc_idle` (19): a second idle pose per NPC.

Optional, for features not decided yet (15 prompts, about 25 minutes): `blunt` (maces and hammers) and `flask` (poison flask, oils).

Do not run: `hero`, `hero_outfits`, `helm`. They draw the hero as whole pictures, which the bones rig replaces (`docs/HERO_ANIMATION_BRIEF.md`). `wpn_all` is not needed either: 34 of the 40 weapons already read fine.

I have not run this command; it is taken from the script's own usage text. The style pictures in `art/redo/_style/` are not in the repo, so it only runs on the designer's PC.

## 2. Needs one small script change first

Sections 2 and 3 are not in `asset_batch.py` yet. Adding them is a change to the script (new entries in its lists, one new edit template for 3d). Together: 45 prompts, 90 images, a bit over an hour.

**Rerun of what failed, without the monster style sheet** (decided Nov 15, not started; 8 prompts):
`prop_cart_wheel`, `prop_footprints`, `prop_ice_block`, `prop_ore_vein`, `prop_tally_board`, `prop_trapdoor`, `fx_heal`, `fx_stab`. Same prompts as today, but with no `@monsters.png` reference. What still fails goes to Moss, cut out with the Qwen edit.

## 3. New prompts, in the script's style

One-line subjects; the script adds the style and framing text around them, as it does for the existing groups. Backgrounds get the floor sentence and "No characters, no creatures." added automatically.

### 3a. `home` - the home room (12)

For "A home room at the Brindlemoor inn". Props are separate so they can appear one at a time.

- `bg_home_room`: A small rented attic room at a town inn: a sloped timber ceiling, a narrow bed with a patchwork quilt in one corner, a round window with warm evening light, bare plank floor, and a long empty back wall of wooden pegs and bare shelves.
- `prop_storage_box`: A big iron-bound oak storage trunk with three brass latches and a blank brass plate on the lid, closed.
- `prop_trophy_shelf`: A long empty wooden wall shelf on iron brackets with five small blank brass plates along its edge.
- `prop_trophy_thornheart`: A wall trophy on a wooden plaque: a twisted knot of dark heartwood and black thorns with a small green glowing seed at its centre.
- `prop_trophy_bogmother`: A wall trophy on a wooden plaque: a hanging curtain of dark green marsh weed strung with pale shells and one glowing blue bead.
- `prop_trophy_stormcoil`: A wall trophy on a wooden plaque: a ring of coiled copper-coloured serpent scales with a few small sparks along it.
- `prop_trophy_snowmaw`: A wall trophy on a wooden plaque: a huge white-furred lower jaw with two long ice-blue fangs.
- `prop_trophy_frostbinder`: A wall trophy on a wooden plaque: a broken circlet of clear ice with three upright frost crystals and a pale blue gem.
- `prop_log_stand`: A wooden reading stand holding a thick open leather-bound book with a red ribbon marker, a quill in an inkpot beside it, blank pages.
- `prop_bookshelf_empty`: A narrow wooden bookshelf with four bare shelves and a little dust.
- `prop_bookshelf_half`: The same kind of narrow wooden bookshelf with four shelves, two of them holding a few leaning books in faded colours.
- `prop_bookshelf_full`: The same kind of narrow wooden bookshelf with four shelves, every shelf packed with books in many colours, a small glowing candle on top.

The trophies reuse the motifs of the boss item icons in the `unique` group, so a boss, its named piece and its trophy read as one family.

### 3b. `master` - the specialization master (9)

For "Specialization master in Brindlemoor". Two takes on the master; the designer picks from the pictures and names the character. Once the pick is in the game data, the `npc` group makes the talk pose and portrait.

- `npc_master_a`: A broad-shouldered veteran trainer woman in late middle age with a grey braid and a calm, amused look, in a worn padded training coat with one leather shoulder guard, a wooden practice sword at her belt, a short wand tucked behind one ear and a throwing knife in her boot, arms folded.
- `npc_master_b`: A lean old veteran trainer man with a white moustache, an eyepatch and a straight back, in a worn padded training coat with one leather shoulder guard, a wooden practice sword at his belt, a short wand tucked behind one ear and a throwing knife in his boot, hands clasped behind him.
- `bg_training_yard`: A fenced training yard behind a town's timber houses: packed dirt, chalk circles on the ground, a low stone wall, a rack of wooden practice weapons at the back, morning light.
- `prop_dummy_warrior`: A straw training dummy on a post wearing a dented pot helm and a battered wooden shield, covered in sword cuts.
- `prop_dummy_mage`: A training target made of stacked grey stones with scorch marks on one side and a rim of frost on the other, a chalk rune circle drawn around its base.
- `prop_dummy_rogue`: A round straw target on a wooden tripod with three throwing knives and two arrows stuck in it.
- `icon_class_warrior`: A crest: a broad sword laid over a round shield.
- `icon_class_mage`: A crest: an upright staff with a glowing crystal over an open book.
- `icon_class_rogue`: A crest: a dagger crossed with an arrow over a hooded mask.

The three crests are optional: the UI kit is drawn by hand, so they may be redrawn in code.

### 3c. `depths` - the Depths (10)

For the three Depths bands. Their look is not specced; these are proposals to pick from, two rooms per band.

- `bg_depths_gate`: The cellar at the foot of an old stone tower: a round chamber with a wide spiral stair going down into darkness through a broken iron grate, lanterns on chains.
- `bg_depths_upper_a`: A buried vaulted cellar hall deep under an old tower: cracked stone arches held up by thick tree roots pushing through the walls, toppled barrels, dim green lantern light, a dark archway at the back.
- `bg_depths_upper_b`: A collapsed crypt corridor underground: rows of empty stone niches, roots hanging from the ceiling, shallow puddles, a thin shaft of pale light from a crack far above.
- `bg_depths_middle_a`: A huge flooded cavern with a still black lake, a narrow natural stone causeway across it, clusters of glowing blue mushrooms and pale stalactites.
- `bg_depths_middle_b`: An underground river gorge crossed by a rope bridge, wet glittering rock, glowing blue moss, mist rising from below.
- `bg_depths_bottom_a`: An ancient hall of black glassy stone far underground: giant pillars scored by huge claw marks, drifts of ash, thin cracks glowing ember-red in the floor.
- `bg_depths_bottom_b`: A vast cavern of fused, melted rock with a long slope of dull blackened coins and broken armour, ember light from below.
- `bg_depths_camp`: A small safe side-chamber underground with a burnt-out fire ring, two bedrolls, a supply crate and rows of chalk tally marks on the wall.
- `prop_depth_stair`: A stone stairhead going down into the ground, worn steps, an iron lantern post beside it.
- `prop_depth_marker`: A squat standing stone with a blank iron plate bolted to it and a faint glow at its base.

The two bottom rooms hint at a dragon. They only fit if the last boss is the old dragon, which is still the designer's call.

### 3d. `depth_test` - can Qwen make a deep variant of a monster? (6)

A test before any set, like the 5-monster pose test. Each is an edit of the monster's existing picture: keep the creature, its pose and size; change only colours and surface. Pass = clearly the same creature in a new look. If it passes, Depths monsters can be variants and need no new designs.

- `mon_mossling_deep`: pale grey fungus instead of green moss, small glowing blue mushrooms on its back.
- `mon_thornback_boar_deep`: ash-grey hide, tusks and thorns of black glass, ember-red eyes.
- `mon_mire_toad_deep`: milky white cave skin, small pale eyes, a faintly glowing blue throat.
- `mon_spark_wisp_deep`: a cold violet glow instead of its usual colour.
- `mon_drift_wolf_deep`: soot-black fur with ember-orange markings.
- `mon_glacier_golem_deep`: black glassy stone instead of ice, thin ember-red cracks.

## 4. Not worth generating yet

- **Hero parts, gear on the rig, creator heads and hair.** They wait for the rig base. The brief allows AI only to paint inside a part's locked outline, and whether Qwen can do that is untested.
- **Chapter 4.** No spec. By the size of Chapters 1-3 it will need about 8-10 rooms, 6-8 monsters, 2 bosses and a town cast.
- **Gate bosses at 30 and 40, and the last boss.** Not designed.
- **Gear looks for levels 20 to 50.** After the rig.
- **Store capsule art.** By hand, not AI (the Oct 26 research: capsules named as AI drew the most thumbs down).
- **Music.** Not an image job; the source is not decided.

## 5. Order

1. Once section 0 is answered: section 1 (about 3 hours).
2. After the script change: section 2 and 3 in one run (a bit over an hour).
3. Rate both, wire in what passes.
