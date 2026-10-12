# drakepg

Browser turn-based RPG. Phaser 4 + TypeScript + Vite. See README.md for layout and content guides.

## Commands
- `npm run dev` / `npm run build` (build runs `tsc --noEmit` first) / `npm run typecheck`

## Conventions
- Phaser 4, not 3. Before using an unfamiliar Phaser API, check `node_modules/phaser/skills/` (incl. `v3-to-v4-migration`) or `node_modules/phaser/types/phaser.d.ts`.
- Game rules in `src/game/` stay Phaser-free so they can be tested/simulated headless.
- Content is data in `src/data/`; add ids there, never hardcode content in scenes.
- Tuning numbers go in `BALANCE` in `src/config.ts`.
- Changing `PlayerData` requires bumping `SAVE_VERSION` and a `migrate()` step.
- Design space is 1280x720 (Scale.FIT). Every scene's `create()` calls `enterScene(this)`; switch scenes with `goTo()`.
- Exploration is single-screen rooms (`RoomScene`, data in `ROOMS`). Depth = feet y. Monsters on a map are transient (`session`), only the player's location is saved.
- One-shot keys use `keyboard.on('keydown-X')` (quick taps get lost with `JustDown` at low FPS); movement keys are polled.
- Effects live in `src/ui/fx.ts`; particle textures are generated at boot (no art needed). Sounds are synthesized in `src/ui/sfx.ts` (`sfx.play(scene, name)`), no audio files.
- Skills come from the player's class (`ClassDef.skills` with unlock levels), not from save data. Quest rules live in `src/game/Quests.ts`; quest state is `PlayerData.quests`.
- Art: raw images in `art/raw/` -> `python3 scripts/prepare_art.py` -> `src/assets/art/<key>.webp`, auto-loaded by key (`src/ui/art.ts`). Missing art falls back to shapes. Sprites face right.
- Original names/art only. No assets or names from Artix Entertainment games.
