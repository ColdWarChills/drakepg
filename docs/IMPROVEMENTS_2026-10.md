# Design pass, October 2026

The 15-point review, what was built, and the calls made along the way (the user asked for the design advisor's judgment).

## Built

| # | What | Where |
| --- | --- | --- |
| 1 | Every monster shows its next move and rough damage; normal monsters hit 2x (`mobDamage`), bosses 1.3x (`bossDamage`) | `Battle.intent`, `BattleScene.refreshIntent` |
| 2 | Absorb (resist over 100 heals). Volt Eel (Ch2, absorbs Energy, weak to Earth) and Ember Salamander (absorbs Fire). Mage's Ember Bolt became Staff Bolt (staff's element) | `Battle.strike`, `monsters.ts`, `skills.ts` |
| 3 | Enrage: +20% enemy damage per turn from turn 15 (bosses 30), countdown on the panel | `BALANCE.enrage` |
| 4 | Skill loadout: 5 carried skills, swapped in the Character panel. Skills stay plain damage (no interrupts or silences) | `PlayerData.loadout`, save v10 |
| 5 | Drop reveal: cards pop in by tier, best last; Sigils get a long charge-up | `ResultScene.dropReveal`, `BALANCE.dropReveal` |
| 9 | Heroic boss rematches: +3 levels, +25% damage, a new move, full rewards, 2 gear rolls | `Heroic.ts` |
| 11 | Signature moves for every normal monster (opener / every Nth / once when hurt / heal), shown as a trait line | `monsters.ts` |
| 12 | One test per boss: Thornheart = Defend timing, Bogmother = burst, Stormcoil = element switch, Snowmaw = bring Fire, Frostbinder = endurance | `monsters.ts` |
| 13 | Sim: random-button bot column | `scripts/sim.ts` |
| 14 | ▲▲ / ▲ / ▼ next to monster levels in rooms (not color alone) | `RoomScene.addMob` |
| 15 | With story text off, finishing a quest keeps the giver's own thank-you line | `RoomScene.interact` |

Parked: 6 (hunter contracts), 7 (class trial), 8 (bad-luck threshold), 10 (system audit).

## Calls made

- **Element names stay Earth and Energy** (no Nature or Lightning): existing data, colors and the opposite-element rule all use them.
- **Only specific monsters absorb**, and say so in their trait and affinity line. If every monster absorbed its own element, the player would have to read the whole board every fight.
- **Absorb = resist 150**: the hit heals half what it would have dealt. Strong enough to read as "don't", not an instant loss.
- **Turn cap is an enrage, not a hard loss**: you can still win a slow fight, but it costs more each turn.
- **5 skill slots, swap anywhere outside battle**: fits the battle menu with no paging, and a wrong pick costs nothing but a menu visit.
- **Heroic does not add HP**: more damage and a new move only, so rematches don't turn into longer fights.

## Sim notes (300 battles per cell)

- Thornheart at the intended level (5, shop gear): smart AI wins 94-98% with ~45% HP left; the random bot wins 33-71%. Decisions now matter there.
- Normal fights: smart AI wins ~100% with 50-70% HP left, so a rest or potion every couple of fights. The random bot does about as well: normal fights are the short loop, and the attrition between fights is where they bite.
- Chapter 2-3 bosses: smart AI wins 92-100%, but the random bot still wins 75-95%. Next tuning lever if they feel soft in playtests: their own `every` beats, or raise `bossDamage`.
- Old Snowmaw without Fire: 0-58% for every class (Mage too, since Staff Bolt). That's intended (the quest before her has you collect Fire Essence), but watch it in playtests.
- Elites with stacked affixes on Tough monsters are still hard (Frenzied Ash Imp at level 3: 6-72%; Ironhide Drakeling: 15-77%). They can be fled.
- Mage early game is weaker without a Fire spam (Thornheart at level 4 with starter gear: 3%, 20% after 5 losses).

## Prologue road (after the merge)

- New heroes start at **Overturned Cart** with their class weapon only (no armor) and walk six one-way rooms west into Brindlemoor's east gate, so the town and its shop are something you arrive at.
- Placeholder pests (`burrow_rat`, `dew_slug`, `hedge_pixie`): damage 0, so every hit floors to 1; 20-24 HP, so about 2 turns (sim: 1.7-2.3). 5-6 XP each, ~16 on the road: level 2 by town.
- Loot table `road` (starter armor weighted 2x, starter weapons) drops on 35% of kills via `BALANCE.loot.tableMob`, a per-table override of the normal-monster odds.
- Chapter 2/3 starts and saves with a broken location go to `TOWN_START`. The new-hero greeting now waits for the "New!" popup to close instead of being skipped.
- Not final: monster names, stats and look (shape stand-ins, no art), room backdrops (procedural forest), Odo/Pell lines.

## Map, Recall Scroll and quiet rooms (PLANS.md steps 1-3)

- **Recall Scroll** (`recall_scroll`, new item type `scroll`): one way to the last town you walked into (`PlayerData.lastTown`), landing at the town's `town` spot. Not in battle, asks first, 20g in all three town shops; new heroes start with 5 (`BALANCE.startingRecallScrolls`). Read from the Inventory (Read button, double-click) or the Map. Rules in `src/game/Travel.ts`.
- **Visited rooms** (`PlayerData.visited`) are recorded on every room entry (`visitRoom`). Save v12 migrates old saves: current room visited, the area's town as last town, plus 5 scrolls.
- **Map** (M / Map button, `src/ui/mapPanel.ts`, rules in `src/game/WorldMap.ts`): one page per area (`src/data/map.ts`), hand-placed `map` spots on every room. Visited rooms show name and icons, neighbours show "?", blocked ways are dashed red, exits to other pages are arrows (tap to turn the page). Quest marks: ! / ? / gold ring on the room where an active kill quest's monster lives. M no longer mutes (sound stays in the Menu).
- **Quiet-room features** (room data): `firstVisit` lines play once as a letterboxed scene (second camera for the bars so the room can zoom toward `panTo`; HUD hidden meanwhile); `inspect` props (note, sign, marks, bones; glint until read) open a narration box; `campfire` restores HP/MP once per visit (`session.restedAt`); `side: 'down'` exits lead off the bottom edge to side rooms. Props are shapes in `src/ui/props.ts`. New sounds: `recall`, `omen`, `page`.
- **Hunters' Camp**: a placeholder side room below the Thornwood Path that uses all of it (lines are drafts for review).
- Checked in the browser: new hero on the road (map shows the road page, recall greyed out), Brindlemoor sets the last town, down exit to the camp and back, first-visit scene, note, campfire rest, recall to Brindlemoor (scroll used, location saved), v11 -> v12 save upgrade and bad values cleaned. Build and sim pass (sim unchanged).

## Chapter 1-3 room layouts (PLANS.md steps 4-5)

- **Rooms:** Chapters 1/2/3 now have 8/9/10 rooms plus the town (were 4/4/5). New: Fallen Oak, Woodcutters' Clearing, Bramble Hideout, Briar Wall (Ch1); Eel Pools, Whispering Reeds, Stilt Village, Stormlit Flats, Shrine Steps (Ch2); Hot Springs, The High Trail, Ranger's Lookout, Whiteout Ledge, Roost Stair (Ch3). Old room ids are unchanged, so saves keep working; combat rooms now hold 2-3 monsters.
- **Rhythm:** 2-3 combat rooms, then a quiet room (first-visit scene, notes); the room before every boss is quiet with a campfire, and the boss's quest gate (thorns, reeds, snowdrift) moved onto its exit.
- **New zones:** `bramble_hideout` (bandits), `eel_pools` (Tough Volt Eels, Bog Lurkers), `stormlit_flats` (wisps and revenants before the Stormcoil), `whiteout` (Rime Acolytes, Gust Sprites, a few golems before the Frostbinder). Drakelings and wyrmlings were left out of new zones (dragon clean-up is still open).
- **Sure chests:** `RoomDef.chest` puts a real chest (never a mimic) at a fixed spot the first time a side room fills in a session; refills roll as usual. Used in the Eel Pools and Steamvent Caves. Needs the chests unlock.
- **Map:** room names wrap to the cell width, and side arrows put their label under the tip (kept inside the panel), so 9-room pages fit.
- **Lines:** all drafts, listed in `docs/ROOM_LINES.md` for review.
- **`npm run pace`** (`scripts/pace.ts`): a bot plays the road and every quest through the real rooms (shortest walk, fights what roams, rooms refill on the game's timer, farms next door, carries a spare weapon against absorbers) and prints rooms per chapter, fights, losses, level at each boss and rough minutes. It also warns about exits with no way back. Old -> new layout: Ch1 28 -> 32 min, Ch2 33 -> 41, Ch3 46 -> 49; levels at bosses about the same.
- **Art:** 15 background prompts added to `art/QUEUE.md` (the new rooms plus `bg_hunters_camp`); rooms fall back to their area's art until then.
- Checked in the browser: first-visit scenes (Woodcutters' Clearing, Stilt Village, Briar Wall with the thorn wall and campfire), all three map pages, the down exit from the Sunken Causeway into the Eel Pools (arrive at the back exit, chest in place). Typecheck, sim and pace pass.
