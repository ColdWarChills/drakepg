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

## What's in the game

- **Title**: New Game / Continue (save in browser localStorage, including where you're standing).
- **Rooms** (DragonFable-style screens): walk with **WASD / arrows** or **click to walk**. Walk off an edge with an arrow to change room.
  - **Brindlemoor** (town): talk with **E / Space** or click. Elder (story), Tobin (shop), Ilsa (inn, rest for gold), Garrick (tips).
  - **Thornwood Path** and **Deep Thornwood**: monsters roam and chase you when you get close. Touch one to fight it. Label color = level vs yours.
- **Battle**: turn-based. Attack / Skills / Items / Flee (mouse or keys **1-6**). Slashes, fireballs, hit-stop, screen shake, crits.
- **Result**: XP and gold count-up, loot, level-up burst. You return to the same spot; the monster is gone. Losing wakes you at the inn with no penalty.
- **Character panel** (button or **C**): stats, equipment, bag. **Esc** closes windows.

## Where things live

| Path | What |
| --- | --- |
| `src/config.ts` | Screen size, colors, **`BALANCE`** design knobs |
| `src/data/` | Content: items, skills, monsters, zones, rooms and NPCs. Edit these to add content. |
| `src/game/` | Rules, no Phaser: `Player`, `Battle`, `SaveSystem`, `rng` |
| `src/scenes/` | Screens: Boot, Title, Room (exploration), Battle, Result |
| `src/ui/` | Buttons, bars, modals, panels, figures, scenery, `fx.ts` (particles and effects) |

### Art

1. Generate images with the prompts in [`art/PROMPTS.md`](art/PROMPTS.md) and put them in `art/raw/`, named by key (e.g. `mon_mossling.png`).
2. Run `python3 scripts/prepare_art.py` (needs Pillow + numpy). It removes flat backgrounds from sprites, trims, resizes, and writes WebP files to `src/assets/art/`.
3. Every file in `src/assets/art/` is loaded at boot by its file name. Point data at it with `look.art`, `zone.background` or `room.background`.

### Adding content

- **Room**: add to `ROOMS` in `src/data/world.ts` with its walkable strip (`walk`), `exits` (left/right to another room), `npcs`, and optionally a `zoneId` + `monsterCount` for roaming monsters. Link it from a neighbor's `exits`.
- **Monster**: add to `src/data/monsters.ts`, then add it to a zone's `encounters` in `src/data/world.ts`.
- **Item**: add to `src/data/items.ts`, then to a shop's `stock` or a monster's `drops`.
- **Skill**: add to `src/data/skills.ts` and to `STARTING_SKILLS` (until skill learning exists). `fx` picks its battle visual (`slash`, `heavy`, `fire`, `heal`).

Broken references (e.g. a drop pointing at a missing item) throw at startup in `validateData()`.

### Saves

Saved in `localStorage` under `drakepg.save`. If you change `PlayerData`, bump `SAVE_VERSION` in `src/config.ts` and handle the old format in `migrate()` in `src/game/SaveSystem.ts`.

## Playing on GitHub Pages

Every push to the default branch deploys to `https://coldwarchills.github.io/drakepg/`.

One-time setup: repo **Settings → Pages → Build and deployment → Source: GitHub Actions**. Until that's set, the workflow skips the Pages step. After enabling, re-run it from **Actions → Build & deploy → Run workflow** (or just push).

## Deploying to itch.io

GitHub Actions builds every push. To also upload to itch.io from the default branch:

1. On itch.io create a project, **Kind of project: HTML**. Set the embed size to 1280×720 (or tick "automatically start on page load" / fullscreen button as you like).
2. itch.io → Settings → API keys → generate a key.
3. GitHub repo → Settings → Secrets and variables → Actions:
   - **Secret** `BUTLER_API_KEY` = the key.
   - **Variable** `ITCH_GAME` = `yourname/your-game` (from the project URL `yourname.itch.io/your-game`).
4. Push. The workflow uploads `dist/` to the `html5` channel. On the first upload, open the itch.io edit page and tick "This file will be played in the browser" for that upload.

Manual alternative: run `npm run build`, zip the **contents** of `dist/` (so `index.html` is at the zip root) and upload the zip on itch.io.
