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

**Chapter 1: The Restless Thornwood**

- **Title**: New Game / Continue (save in browser localStorage, including where you're standing).
- **Create**: type a name (or Random) and pick a class: **Warrior**, **Mage** or **Rogue** (click or **1-3**). Each has its own stats, gear and skills.
- **Rooms** (DragonFable-style screens): walk with **WASD / arrows** or **click to walk**. Walk off an edge to change room.
  - **Brindlemoor** (town): talk with **E / Space** or click. Elder Maren and Garrick give quests (**!** = new, **?** = ready to turn in), Tobin runs the shop (buy and sell), Hulda the forge, Ilsa the inn.
  - **Thornwood Path** and **Deep Thornwood**: monsters roam and chase you when you get close. Touch one to fight it. Label color = level vs yours.
  - **Thornheart Hollow**: sealed by thorns until the Elder sends you there. Home of the chapter boss.
- **Quests**: a 4-quest chain (tracker on the left). Kill counts, then turn in for XP, gold and items. The last one ends the chapter.
- **Battle**: turn-based. Attack / Skills / Items / **Defend** (-60% damage, regain MP) / Flee (mouse or keys **1-6**). The boss winds up big attacks a turn ahead: defend or heal.
  - **Elements**: Fire, Water, Ice, Wind, Earth, Energy, Light, Dark. Weapons, spells and monster attacks carry one (or none: neutral). Every monster's weaknesses and resistances are shown under its name; hits show **WEAK!** / **Resisted**. Rule of thumb: a creature resists its own element and is weak to the opposite (Fire/Ice, Water/Energy, Earth/Wind, Light/Dark).
  - **Attack types**: Melee (STR), Pierce (DEX), Magic (INT). Each has its own defense on armor and monsters.
  - **Skills** cost MP and most have a **cooldown** (turns until reuse, shown on the button). Hover a skill for its description.
  - **Statuses**: burn, poison, bleed, stun, weaken, expose, buffs and more, shown as chips under each status panel with turns left.
  - **Weapon specials**: some weapons have a chance to trigger an extra effect when an Attack lands (shown in the item line).
- **Leveling**: each level gives **stat points** (spend with **+** in the Character panel) and unlocks class skills at set levels (12 per class, up to level 20).
- **Gear you can see**: the equipped weapon is drawn in the hero's hands.
- **Gear lines**: every weapon and armor comes in a stronger version every 5 levels (Iron Sword, Iron Sword II, ...), generated from a level budget. Gear has a level requirement.
- **Rolled gear**: each piece rolls three **quality** lines from 0% to 100% (weapons: Damage, Hit, Crit; armor: Physical, Magic, Evasion). Shop gear is always 0%, so drops are worth finding. Drops can roll **Fine / Rare / Mythic**: a quality floor, 1-3 bonus traits (+STR, element resist, crit...) and a little extra power.
- **Armor types**: **Plate** (strong vs Melee/Pierce, weak vs Magic, walk -10%), **Leather** (balanced, skills -10% MP, walk +10%), **Cloth** (strong vs Magic, skills -20% MP, walk +20%).
- **Materials and the forge**: monsters drop parts and elemental essences. Hulda **crafts** gear from recipes (at least 25% quality, better rarity odds), **reinforces** it to +10 (+5% each; from +4 a failure drops a +), **hones** one quality line (may make another line drift down, never below the rarity floor) and **salvages** gear into scrap, dust and essences.
- **Result**: XP and gold count-up, loot, quest progress, level-up. Losing wakes you at the inn with no penalty.
- **Character panel** (button or **C**): stats, stat points, skills, equipment, bag. **M** toggles sound. **Esc** closes windows.
- **Sound**: all effects are synthesized in code (WebAudio), no audio files.

## Where things live

| Path | What |
| --- | --- |
| `src/config.ts` | Screen size, colors, **`BALANCE`** design knobs |
| `src/data/` | Content: classes, items, skills, monsters, quests, zones, rooms and NPCs. Edit these to add content. |
| `src/game/` | Rules, no Phaser: `Player`, `Battle`, `Quests`, `SaveSystem`, `rng` |
| `src/scenes/` | Screens: Boot, Title, Create, Room (exploration), Battle, Result |
| `src/ui/` | Buttons, bars, modals, panels, figures, scenery, `fx.ts` (particles and effects), `sfx.ts` (sounds) |

### Art

1. Generate images with the prompts in [`art/PROMPTS.md`](art/PROMPTS.md) and put them in `art/raw/`, named by key (e.g. `mon_mossling.png`).
2. Run `python3 scripts/prepare_art.py` (needs Pillow + numpy). It removes flat backgrounds from sprites, trims, resizes, and writes WebP files to `src/assets/art/`.
3. Every file in `src/assets/art/` is loaded at boot by its file name. Point data at it with `look.art`, `zone.background` or `room.background`.

Weapons in hand: heroes have weaponless art (`char_<class>_bare`, made by editing the hero image to empty fists). `look.bare.hands` in `classes.ts` holds each fist's pixel position in that processed image and the weapon's idle angle; **re-measure them if the bare art is regenerated**. Weapon images are `wpn_<id>`, drawn upright with the handle at the bottom (the script makes them 320px tall); a weapon's `art`, `grip` (0 = top, 1 = bottom of the image: where the fist holds it) and `size` (length relative to the hero) are set in `items.ts`. Weapons are drawn behind the body so the fist covers the grip. A weapon without art falls back to the hero's original picture.

### Adding content

- **Room**: add to `ROOMS` in `src/data/world.ts` with its walkable strip (`walk`), `exits` (left/right to another room), `npcs`, and optionally a `zoneId` + `monsterCount` for roaming monsters. Link it from a neighbor's `exits`.
- **Monster**: add to `src/data/monsters.ts` (with an `element`, `resists` in percent, `defense` per attack type, and `immune` statuses if any), then add it to a zone's `encounters` in `src/data/world.ts`. An attack can `applies` statuses; `power: 0` makes it a pure buff.
- **Weapon / armor line**: add a family to `src/data/gear.ts` with its first level (`from`) and `power` (share of the level budget; lower it to pay for `stats`, an `element` or a `proc`). Versions every `BALANCE.gear.versionStep` levels are generated: the first keeps the family id, later ones are `<id>_<level>`. Armor picks an `archetype` (plate / leather / cloth, see `ARMOR_TYPES`). Then reference the id in a shop's `stock`, a monster's `drops` or a recipe. `npm run sim -- gear` lists everything generated.
- **Potion**: add to `CONSUMABLES` in `src/data/items.ts`. **Material**: add to `MATERIALS` in `src/data/crafting.ts`.
- **Recipe**: add to `RECIPES` in `src/data/crafting.ts` and to a forge NPC's `recipes` (`role: { kind: 'forge' }`).
- **Rarity / traits**: `src/data/rarity.ts`. Quality, reinforce and hone numbers are in `BALANCE.gear`.
- **Skill**: add to `src/data/skills.ts`, then to a class's `skills` in `src/data/classes.ts` with the level that unlocks it. Set `cooldown`, and `element`/`damageType` if it shouldn't use the weapon's. `applies` adds statuses (`self: true` for buffs). `fx: 'heavy'` gives a leap (or a big spell).
- **Status**: add to `src/data/statuses.ts` (damage over time, regen, stun, damage/defense/resist/hit/crit modifiers).
- **Class**: add to `src/data/classes.ts` (stats, per-level `growth`, starting gear, skills, look) and to `ClassId` in `src/data/types.ts`.
- **Quest**: add to `src/data/quests.ts`. `giver` is an NPC id, `requires` chains it after another quest, `classItems` gives a different reward per class, `endsChapter` shows the chapter-complete screen. A room exit with `requiresQuest` stays blocked by thorns until that quest is accepted; a room `boss` appears while its quest is active.
- **Tough monster**: `tough: true` shows a "Tough" tag in rooms and battle. Design rule: trash is neutral with several small weaknesses; a tough one hits hard, resists a lot and has one or two big weaknesses the player should bring a counter for.
- **Boss**: a monster with `boss: true` (can't flee). An attack with `windup` text is telegraphed one turn before it hits.

Broken references (e.g. a drop pointing at a missing item) throw at startup in `validateData()`.

### Balance check

`npm run sim` plays thousands of battles headless (same rules as the game) with a simple AI and prints win rate, HP left, turns and potions used per class and monster. Run it after changing monsters, skills, items or `BALANCE`. `npm run sim -- 2000` for more battles per cell.

### Saves

Saved in `localStorage` under `drakepg.save`. Older saves are migrated (pre-class saves become Warriors; pre-v4 gear becomes 0% Common pieces). If you change `PlayerData`, bump `SAVE_VERSION` in `src/config.ts` and handle the old format in `migrate()` in `src/game/SaveSystem.ts`.

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
