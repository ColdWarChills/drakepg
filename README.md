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

**Chapter 1: The Restless Thornwood** and **Chapter 2: Rising Waters**

- **Title**: New Game / Continue (save in browser localStorage, including where you're standing).
- **Create**: type a name (or Random) and pick a class: **Warrior**, **Mage** or **Rogue** (click or **1-3**). Each has its own stats, gear and skills.
- **Rooms** (DragonFable-style screens): walk with **WASD / arrows** or **click to walk**. Walk off an edge to change room.
  - **Brindlemoor** (town): talk with **E / Space** or click. Elder Maren and Garrick give quests (**!** = new, **?** = ready to turn in), Tobin runs the shop, Ilsa the inn.
  - **Thornwood Path** and **Deep Thornwood**: monsters roam and chase you when you get close. Touch one to fight it. Label color = level vs yours.
  - **Elites**: now and then (8% per roaming monster, at most one per room) a monster spawns as an Elite: bigger, glowing gold, tagged **ELITE**, one level higher, with 2x HP, 1.25x damage and one affix in its name (**Hulking** +50% HP, **Frenzied** +25% damage, **Warded** +25 all resistances, **Venomous** attacks may poison, **Ironhide** +50% defense; the battle panel spells it out). They give 3x XP and gold, always drop a piece of the area's gear, and roll Sigils far more often than normal monsters. An Elite still counts as its base monster for quests.
  - **Thornheart Hollow**: sealed by thorns until the Elder sends you there. Home of the chapter boss, who stays for **rematches** after its quest (half XP and gold, full loot; bosses drop Sigils far more often).
  - **Chapter 2** (level 6-10) runs west of Brindlemoor once the Elder asks: **Fen Road** (Mire Toads, Spark Wisps), the outpost **Reedwatch** (Captain Hale's quests, Wren's tier-2 shop with Greater potions, Sister Ottilie the healer), the rainy **Sunken Causeway** (Bog Lurkers, Sunken Revenants, Stormscale Drakelings), **Bog Hollow** (mini-boss: the Bogmother) and the **Drowned Shrine** (the Stormcoil). Water monsters drop Water weapons and Energy monsters Energy ones; each is the other's weakness. Lose past Reedwatch and you wake at its healer.
  - **The Stormcoil changes form** at half HP: Energy becomes Water, its resistances flip, and the battle panel and log say so. Bring a neutral weapon, or one of each.
- **Quests**: a quest chain per chapter (tracker on the left). Kill counts (one asks for any Elite), then turn in for XP, gold and items. The last one ends the chapter.
- **Battle**: turn-based. Attack / Skills / Items / **Defend** (-60% damage, regain MP) / Flee (mouse or keys **1-6**). The boss winds up big attacks a turn ahead: defend or heal.
  - **Elements**: Fire, Water, Ice, Wind, Earth, Energy, Light, Dark. Weapons, spells and monster attacks carry one (or none: neutral). Every monster's weaknesses and resistances are shown under its name; hits show **WEAK!** / **Resisted**. Rule of thumb: a creature resists its own element and is weak to the opposite (Fire/Ice, Water/Energy, Earth/Wind, Light/Dark).
  - **Attack types**: Melee (STR), Pierce (DEX), Magic (INT). Each has its own defense on armor and monsters.
  - **Skills** cost MP and most have a **cooldown** (turns until reuse, shown on the button). Hover a skill for its description.
  - **Warrior Rage**: Warriors gain 3 MP whenever an enemy hit lands on them ("Rage +3 MP"), so they can keep using skills in long fights.
  - **Statuses**: burn, poison, bleed, stun, weaken, expose, buffs and more, shown as chips under each status panel with turns left.
  - **Weapon specials**: some weapons have a chance to trigger an extra effect when an Attack lands (shown in the item line).
- **Leveling**: each level gives **stat points** (spend with **+** in the Character panel) and unlocks class skills at set levels (12 per class, up to level 20).
- **Result**: XP and gold count-up, loot, quest progress, level-up. Losing wakes you at the inn with no penalty.
- **Rolled gear**: every weapon and armor piece rolls a quality per line (Damage, Crit, Special chance / Defense, Max HP), 0-100%. Shop gear is always **Standard (0%)**; monster drops and quest rewards roll, and high rolls are rare. A 100% Damage or Defense line adds +50%, so a perfect roll is about as strong as the next tier's 0% item (each weapon tier is about x1.5 the one before; items in the same tier are sidegrades). The average is the item's **grade**: Standard (grey), Fair (white), Fine (green, 40%+), Superior (blue, 70%+), Masterwork (gold, 90%+). Superior and Masterwork drops sparkle on the result screen.
- **Mods** (blue lines): extra lines like +STR, +Crit chance, +Max HP/MP or +resist to one element, each with its own 0-100% quality. About 1 in 10 normal gear drops has one.
- **Sigils**: rare versions of any dropped weapon or armor, shown with a **✦ Sigil of ...** line under the name, a glowing icon and a glow on the weapon in hand.

  | Tier | Base damage/defense | Mods | Normal monster | Elite | Boss |
  | --- | --- | --- | --- | --- | --- |
  | Sigil of Dawn (orange) | +20% | 1 | 2% | 8% | 20% |
  | Sigil of Dusk (violet) | +35% | 2 | 0.5% | 2% | 7% |
  | Sigil of Eclipse (crimson) | +60% | 3 | 0.05% | 0.3% | 1.5% |

  The chance is per gear drop. The Sigil raises the base first, then the white rolls apply on top (a 100% Eclipse Iron Sword hits 17-26 vs 7-11 for a shop one). Sigil drops arrive locked, sort first, sell for 3x/6x/12x, and get a fanfare on the result screen. Quest rewards and shop gear never have Sigils or mods.
- **Inventory** (button or **I**): 30-slot grid with tabs (All / Weapons / Armor / Potions / Materials) and sorting. Materials sit in their own pouch and take no bag slots. Hover or tap an item for its card: rolls with quality bars, and a comparison with what you wear. Equip by double-click, the Equip button, or dragging onto the hero; drag worn gear back to the grid to take it off. **Lock** keeps an item from being sold. Loot that doesn't fit a full bag is left behind (quest rewards always fit).
- **Shop**: Buy (Standard gear and potions) and Sell (better grades sell for more; **Sell junk** sells unlocked Standard/Fair gear, never Sigils or reinforced gear).
- **Materials and the forge**: monsters drop parts (Moss Clump, Boar Tusk, Fen Hide, Storm Scale), elemental **essences**, Iron Scrap and Honing Dust. **Hulda** the smith in Brindlemoor:
  - **Craft** gear from recipes: every line rolls at least 25%. Boss weapons stay drop-only.
  - **Reinforce** to +10: each + adds 5% base damage/defense (stacks with Sigils). Odds fall from +3; from +4 a failure drops a +.
  - **Hone** one quality line by 8-15%; 35% chance another line slips 3-8%.
  - **Salvage** bag gear into scrap, dust (more for better grades and Sigils) and its element's essence. Fine-or-better, Sigil or reinforced gear asks twice.
- **Phones and tablets** (landscape): tap the ground to walk and tap people to talk; **hold** a battle button (or a skill in the Character panel) to read its description without using it. Names in rooms grow on small screens, a **Fullscreen** button appears where the browser supports it (not on iPhone Safari), and holding the device upright shows a "turn sideways" screen.
- **Character panel** (button or **C**): stats, stat points, skills, what you have equipped. **M** toggles sound. **Esc** closes windows.
- **Gear you can see**: the weapon is drawn in the hero's hands, and armor changes the hero's outfit once its art exists (`char_<class>_<armor id>`; see the Art section).
- **Cheats** (for testing; hidden): in any room press **`** (backtick), or tap the room name 5 times on a touch screen.
  - **Hero**: levels (+1, +5, to 5/10/15/20), gold, full heal, refund stat points, clear the bag, and toggles for god mode, one-hit kills and Elites everywhere (toggles last until the page reloads).
  - **Give item**: any item with a chosen quality, Sigil and number of mods (potions in fives).
  - **Wardrobe**: try on every weapon and armor and see it on the hero right away (faded = no art yet).
  - **World**: teleport to any room, respawn monsters, mark quests done (with everything before them), finish active quests, reset quests, and reset the character.
  - Everything except the toggles changes your real save.
- **Sound**: all effects are synthesized in code (WebAudio), no audio files.

## Where things live

| Path | What |
| --- | --- |
| `src/config.ts` | Screen size, colors, **`BALANCE`** design knobs |
| `src/data/` | Content: classes, items, skills, monsters, Elite affixes, quests, zones, rooms and NPCs. Edit these to add content. |
| `src/game/` | Rules, no Phaser: `Player`, `Battle`, `Items` (rolled gear), `Elites`, `Quests`, `SaveSystem`, `rng` |
| `docs/` | Design specs, e.g. [`CHAPTER2_SPEC.md`](docs/CHAPTER2_SPEC.md) |
| `src/scenes/` | Screens: Boot, Title, Create, Room (exploration), Battle, Result |
| `src/ui/` | Buttons, bars, modals, panels, figures, scenery, `fx.ts` (particles and effects), `sfx.ts` (sounds) |

### Art

1. Generate images with the prompts in [`art/PROMPTS.md`](art/PROMPTS.md) and put them in `art/raw/`, named by key (e.g. `mon_mossling.png`).
2. Run `python3 scripts/prepare_art.py` (needs Pillow + numpy). It removes flat backgrounds from sprites, trims, resizes, and writes WebP files to `src/assets/art/`.
3. Every file in `src/assets/art/` is loaded at boot by its file name. Point data at it with `look.art`, `zone.background` or `room.background`.

Armor outfits: wearing an armor, a hero is drawn from `char_<class>_<armor id>` (empty-handed like the bare art) when it exists, else the default look. Its fists default to the class's `bare.hands`; if they moved, set `hands` per class on the armor in `items.ts`. Prompts are at the end of `art/PROMPTS.md`.

Weapons in hand: heroes have weaponless art (`char_<class>_bare`, made by editing the hero image to empty fists). `look.bare.hands` in `classes.ts` holds each fist's pixel position in that processed image and the weapon's idle angle; **re-measure them if the bare art is regenerated**. Weapon images are `wpn_<id>`, drawn upright with the handle at the bottom (the script makes them 320px tall); a weapon's `art`, `grip` (0 = top, 1 = bottom of the image: where the fist holds it) and `size` (length relative to the hero) are set in `items.ts`. Weapons are drawn behind the body so the fist covers the grip. A weapon without art falls back to the hero's original picture.

### Adding content

- **Room**: add to `ROOMS` in `src/data/world.ts` with its walkable strip (`walk`), `exits` (left/right to another room), `npcs`, and optionally a `zoneId` + `monsterCount` for roaming monsters, `ambient` particles (dust, leaves, fireflies, rain, mist) and `wakeAt` (where you wake after losing there; default the Brindlemoor inn). Link it from a neighbor's `exits`. Without art, `backdrop.kind` draws a town, forest, marsh or outpost.
- **Monster**: add to `src/data/monsters.ts` (with an `element`, `resists` in percent, `defense` per attack type, and `immune` statuses if any), then add it to a zone's `encounters` in `src/data/world.ts`. An attack can `applies` statuses; `power: 0` makes it a pure buff.
- **Material / recipe**: add to `MATERIALS` / `RECIPES` in `src/data/crafting.ts` (Hulda lists every recipe); forge numbers are in `BALANCE.forge`, rules in `src/game/Forge.ts`.
- **Item**: add to `src/data/items.ts`, then to a shop's `stock` or a monster's `drops`. Weapons take a `damageType`, an optional `element` and an optional `proc` (special); armor takes `defense` per type and `resists`. Items without art get a drawn icon tinted with `iconColor`. Roll ranges, drop curve, bag size and sell prices are in `BALANCE` (`gearRolls`, `rollCurve`, `bagSlots`, `sellRate`, `sellGradeBonus`), and so are Sigils and mods (`sigils`, `sigilChance`, `modChance`, `modRanges`); roll rules and the mod pools live in `src/game/Items.ts`. Drops from a `boss: true` monster use the boss Sigil chances.
- **Skill**: add to `src/data/skills.ts`, then to a class's `skills` in `src/data/classes.ts` with the level that unlocks it. Set `cooldown`, and `element`/`damageType` if it shouldn't use the weapon's. `applies` adds statuses (`self: true` for buffs). `fx: 'heavy'` gives a leap (or a big spell).
- **Status**: add to `src/data/statuses.ts` (damage over time, regen, stun, damage/defense/resist/hit/crit modifiers).
- **Elite affix**: add to `src/data/elites.ts` (`hp`, `damage`, `defense` multipliers, `resists`, `applies` added to every attack) with a `description` the battle panel shows. Spawn chance, stat and reward multipliers are in `BALANCE.elite`; Elite Sigil odds in `BALANCE.sigilChance.elite`. Any monster can become an Elite; its guaranteed drop comes from the gear its zone's monsters drop.
- **Class**: add to `src/data/classes.ts` (stats, per-level `growth`, starting gear, skills, look, optional `mpOnHit` for Rage) and to `ClassId` in `src/data/types.ts`.
- **Quest**: add to `src/data/quests.ts`. `giver` is an NPC id, `requires` chains it after another quest, the objective kills a `monsterId` or any Elite (`elite: true`, which also makes Elites spawn more often while it's active), `classItems` gives a different reward per class, `endsChapter` shows the chapter-complete screen. A room exit with `requiresQuest` stays blocked (by thorns, or its `blocker`: barricade or reeds) until that quest is accepted; a room `boss` appears once its quest is accepted and stays for rematches (`BALANCE.bossRepeatRewards` XP and gold).
- **Boss**: a monster with `boss: true` (can't flee). An attack with `windup` text is telegraphed one turn before it hits. A `phase` changes its element, resistances and attacks once its HP drops below a share (the Stormcoil at 50%). The room's `boss.intro` is the line shown before the fight.

Broken references (e.g. a drop pointing at a missing item) throw at startup in `validateData()`.

### Balance check

`npm run sim` plays thousands of battles headless (same rules as the game) with a simple AI and prints win rate, HP left, turns and potions used per class and monster. Run it after changing monsters, skills, items or `BALANCE`. `npm run sim -- 2000` for more battles per cell.

### Saves

Saved in `localStorage` under `drakepg.save`. Older saves are migrated (pre-class saves become Warriors; pre-v4 gear becomes Standard; v5 added Sigils and mods, which old gear simply doesn't have; v6 added the smith's + and materials). If you change `PlayerData`, bump `SAVE_VERSION` in `src/config.ts` and handle the old format in `migrate()` in `src/game/SaveSystem.ts`.

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
