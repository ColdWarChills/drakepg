# drakepg

A browser turn-based RPG inspired by classic Flash-era adventure RPGs. Original names, story and art only.

Built with [Phaser 4](https://phaser.io), TypeScript and Vite. Runs in any modern browser (WebGL), desktop or mobile landscape.

## Run it

```bash
npm install
npm run dev      # local dev server with hot reload
npm run build    # production build into dist/
npm run preview  # serve the dist/ build locally
```

## What's in Milestone 0

- **Title**: New Game / Continue (save in browser localStorage).
- **Town (Brindlemoor)**: click NPCs. Elder (story), Shop (buy gear and potions), Inn (rest for gold), Gate (go fight).
- **Battle**: turn-based. Attack / Skills / Items / Flee. Hit, crit, defense and resist.
- **Result**: XP, gold, loot, level up. Defeat sends you back to town with no penalty.
- **Character panel** (button or `C` key): stats, equipment, bag. Equip gear, use potions.

Art is placeholder shapes (`src/ui/figures.ts`).

## Where things live

| Path | What |
| --- | --- |
| `src/config.ts` | Screen size, colors, **`BALANCE`** design knobs |
| `src/data/` | Content: items, skills, monsters, zones, towns/NPCs. Edit these to add content. |
| `src/game/` | Rules, no Phaser: `Player`, `Battle`, `SaveSystem`, `rng` |
| `src/scenes/` | Screens: Boot, Title, Town, Battle, Result |
| `src/ui/` | Buttons, bars, modals, panels, placeholder figures |

### Adding content

- **Monster**: add to `src/data/monsters.ts`, then add it to a zone's `encounters` in `src/data/world.ts`.
- **Item**: add to `src/data/items.ts`, then to a shop's `stock` or a monster's `drops`.
- **Skill**: add to `src/data/skills.ts` and to `STARTING_SKILLS` (until skill learning exists).

Broken references (e.g. a drop pointing at a missing item) throw at startup in `validateData()`.

### Saves

Saved in `localStorage` under `drakepg.save`. If you change `PlayerData`, bump `SAVE_VERSION` in `src/config.ts` and handle the old format in `migrate()` in `src/game/SaveSystem.ts`.

## Deploying to itch.io

GitHub Actions builds every push. To also upload to itch.io from the default branch:

1. On itch.io create a project, **Kind of project: HTML**. Set the embed size to 1280×720 (or tick "automatically start on page load" / fullscreen button as you like).
2. itch.io → Settings → API keys → generate a key.
3. GitHub repo → Settings → Secrets and variables → Actions:
   - **Secret** `BUTLER_API_KEY` = the key.
   - **Variable** `ITCH_GAME` = `yourname/your-game` (from the project URL `yourname.itch.io/your-game`).
4. Push. The workflow uploads `dist/` to the `html5` channel. On the first upload, open the itch.io edit page and tick "This file will be played in the browser" for that upload.

Manual alternative: run `npm run build`, zip the **contents** of `dist/` (so `index.html` is at the zip root) and upload the zip on itch.io.
