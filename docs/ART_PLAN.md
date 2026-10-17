# Art plan: everything to generate

Goal (designer, 2026-10-17): redo **all** graphics in one stylized, unique look. Pick the style together first, then Claude generates the rest in long runs without check-ins.

Counts are from the game data on 2026-10-17. "Missing" = the game draws a placeholder shape today.

## Step 1: style (the designer picks)

1. **Whole-screen mock:** one battle screen (UI, background, base hero, a monster) so the look of everything is judged at once. In round 12 the characters were fine but the AI-generated UI read as AI. Now: the round-12 characters and background with a hand-made UI on top (round 14, `art/ui/battle_mock.html`).
2. **Check it holds:** the same look on a room screen and the inventory, plus one proportion rule for heroes and monsters.
3. **Lock it:** train our own LoRA on the best ~20 images (characters, monsters, backgrounds, item icons), so every later image matches and is sell-safe.

Everything below waits for step 1.

## Step 2: UI kit (~50 pieces)

Designed by hand, never generated (2026-10-17): flat shapes, the game palette, a free Google font (Fredoka in the mock). Drawn in code or as SVG 9-slices so they stretch to any size.

| Piece | Count |
|---|---|
| Panels: window, small panel, tooltip, NPC dialog box, name plate | 5 |
| Buttons: big, small, icon, tab, each normal / hover / pressed / disabled | 16 |
| Bars: HP, MP, XP, enemy HP (frame + fill) | 8 |
| Item slot frame per grade, empty equipment slot outlines (9 slots) | ~15 |
| Skill button frame, cooldown cover, "Weak!" tag, "New!" badge | 4 |
| Title logo, map page (paper) | 2 |
| Font: a free Google font that fits the theme | 1 |

Code: `Button.ts`, `Bar.ts`, `panels.ts`, `Modal.ts` draw these when the art exists, shapes otherwise (same fallback rule as all art).

## Step 3: icons (~230)

| Set | Count | Now |
|---|---|---|
| Skills | 38 | none (new; buttons are text only) |
| Statuses | 20 | none (new) |
| Elements 8, damage types 3, Blunt/Poison weak spots 2, stats 4, gold, XP | 19 | none (new) |
| Map and quest marks (town, boss, campfire, chest, you, !, ?) | 8 | drawn |
| Guild rank badges F-S | 7 | drawn |
| Items | 133 | 118 have art (redo in the new style), 15 missing: 4 roll stones, Recall Scroll, 10 hunt items |

Skills, statuses, elements, stats, map marks and rank badges are hand-made flat SVG (like the 5 in the mock); only the item icons are generated, in the character look.
Code: skill buttons and status chips need to show `icon_skill_<id>` / `icon_status_<id>`.

## Step 4: backgrounds (~35, at 2560x1440)

- 33 rooms (30 have art, redo all; missing: the 3 `road_*` rooms) + the Old Road battle background + the title art.
- Generate at 2048+ wide (the old raws are 1344 wide, which is why they look soft).

## Step 5: monsters and NPCs (~46)

- Monsters: 30 (31 minus the dropped Volt Eel). 21 have art (redo), missing: Millstone Crab, Dusk Wraith, Hedge Troll (gate guards), Mimic, and the 3 road stand-ins (Burrow Rat, Dew Slug, Hedge Pixie: need a design first), plus the 2 dragon replacements below.
- NPCs: 16. 11 have art (redo), missing: Odo, Pell, the 3 Guild clerks (Tamsin, Ivo, Edda).

## Step 6: hero (~25 + outfits)

- One base hero for every class (decided 2026-10-16). Already made in the poster look: base body, 7 hairstyles, a headband combo per hairstyle (`art/tests/layers/`). Redo if the picked style changes the character look.
- Still open: body types, faces, skin tone and eye colour (recolours in code).
- Outfits: 12 body armors, now drawn once on the base body (not once per class). Helms, gloves and boots: icons only for now.
- Code: one base hero instead of per-class art (`ClassDef.look`, `char_<class>_<armor>`), weapon anchors on the base art.

## Step 7: weapons (40)

- 40 weapon sprites held in the hand (all have art, redo). Their item icons can come from the same sprites.

## Dragon clean-up (go, 2026-10-17)

Defaults (change any):
- Stormscale Drakeling (Ch2, Tough, Energy) becomes a **Stormcrest Heron**: a big marsh bird with a crackling crest.
- Vent Wyrmling (Ch3, Tough, Fire) becomes a **Vent Tortoise**: a lava-shelled tortoise, weak to Blunt (the "shelled things weak to Blunt" idea).
- Storm Scale (material) becomes Storm Feather; the drakeling-fang dagger's text changes.
- The one old dragon in the story stays: it is the rare one. The title art and other screens show no dragon.

## Totals and order

About 430 images. At 20-50 s each with 2-3 tries per image: roughly 10-15 hours of generation, plus cutting and checking.

Order of the long runs after the style is locked: UI kit, skill and status icons, backgrounds, monsters and NPCs, item icons, weapons, hero outfits. Effects stay code-made particles.

Unity: make every master as a large PNG (Unity can't import webp), kept in `art/raw/`; the game's webp files are made from those.
