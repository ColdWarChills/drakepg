# drakepg

Browser turn-based RPG. Phaser 4 + TypeScript + Vite. See README.md for layout and content guides.

## Commands
- `npm run dev` / `npm run build` (build runs `tsc --noEmit` first) / `npm run typecheck`
- `npm run sim`: headless balance check (scripts/sim.ts). Run it after tuning monsters, skills, items or `BALANCE`.

## Conventions
- Phaser 4, not 3. Before using an unfamiliar Phaser API, check `node_modules/phaser/skills/` (incl. `v3-to-v4-migration`) or `node_modules/phaser/types/phaser.d.ts`.
- Game rules in `src/game/` stay Phaser-free so they can be tested/simulated headless.
- Content is data in `src/data/`; add ids there, never hardcode content in scenes.
- Tuning numbers go in `BALANCE` in `src/config.ts`.
- Changing `PlayerData` requires bumping `SAVE_VERSION` and a `migrate()` step.
- Design space is 1280x720 (Scale.FIT). Every scene's `create()` calls `enterScene(this)`; switch scenes with `goTo()`.
- Exploration is single-screen rooms (`RoomScene`, data in `ROOMS`). Depth = feet y. Monsters and chests on a map live in `session` (remembered per room for the play session: the dead stay dead, one wanders back per `BALANCE.respawnSeconds` away); only the player's location is saved. Per-browser preferences (story text) are in `src/game/settings.ts`, not the save.
- One-shot keys use `keyboard.on('keydown-X')` (quick taps get lost with `JustDown` at low FPS); movement keys are polled.
- Effects live in `src/ui/fx.ts`; particle textures are generated at boot (no art needed). Sounds are synthesized in `src/ui/sfx.ts` (`sfx.play(scene, name)`), no audio files.
- Combat: 8 elements (`src/data/elements.ts`), damage types melee/pierce/magic (STR/DEX/INT), statuses in `src/data/statuses.ts`, cooldowns and weapon procs in `src/game/Battle.ts`. Monster resist rule: +own element, -opposite. Every mechanic must be visible in the battle UI or item text (no hidden specials).
- Weapons are drawn in the hero's hands: bare hero art + `look.bare.hands` pixel anchors + weapon `art/grip/size`. Re-measure anchors when bare art changes.
- Draw the hero with `player.look` (not `classDef.look`): armor swaps the outfit to `char_<class>_<armor id>` when that art exists; its fist anchors are the armor's `hands[class]` or the class's.
- Cheats live in `src/game/cheats.ts` (rules, session-only toggles) and `src/ui/cheatMenu.ts` (backtick / tap the room name 5x).
- Gear has a `tier` (data) that caps its modifier lines (rolled lines + mods) via `BALANCE.modifierSlots` (max 2 for now); `rollKeys()`/`modSlots()` in `src/game/Items.ts` apply it. Gear is rolled: weapons/armor are unique `Gear` instances (`uid`, `itemId`, `rolls` 0-100 per line) in `PlayerData.gear`/`equipment`; consumables stack in `inventory`. Shop gear is 0%. Rules in `src/game/Items.ts`; item visuals, grade colors and compare in `src/ui/itemUi.ts`; the grid inventory and shop in `src/ui/inventory.ts`. Gear drops come from loot tables (`src/data/loot.ts`, monster `loot`), rolled as a cascade rare -> uncommon -> common (`BALANCE.loot`, `src/game/Loot.ts`); monster `drops` are for potions/materials only. Materials (`type: 'material'`) stack in `inventory` but take no bag slots; the smith (craft/reinforce/hone/salvage) is `src/game/Forge.ts` + `src/ui/forge.ts`, data in `src/data/crafting.ts`.
- New systems open chapter by chapter: add a `FeatureDef` to `src/data/features.ts` (unlocked by a quest) and gate code with `featureOn(player, id)` (`src/game/Features.ts`); it is announced once in the room's "New!" popup, like new skills.
- Skills come from the player's class (`ClassDef.skills` with unlock levels), not from save data. Quest rules live in `src/game/Quests.ts`; quest state is `PlayerData.quests`.
- Art: raw images in `art/raw/` -> `python3 scripts/prepare_art.py` -> `src/assets/art/<key>.webp`, auto-loaded by key (`src/ui/art.ts`). Missing art falls back to shapes. Sprites face right.
- Original names/art only. No assets or names from Artix Entertainment games.
