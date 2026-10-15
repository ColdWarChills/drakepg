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

## Battle effects pass

Every hit gets wind-up, contact and reaction, on both sides, with or without monster art. Rules unchanged (no `Battle.ts` edits, sim unaffected).

- **Attacks:** a short lean-back and weapon cock before the lunge, afterimages along the lunge or leap (element-colored; red for monster attacks), speed lines, dust on landing. Heavy skills crouch, leap and shake the screen on landing. Spells draw a spinning magic circle under the caster while charging; the projectile follows the element (orb, ice shard, tumbling rock, spinning gust, instant lightning bolt).
- **Impacts:** each element has its own burst on top of the sparks: fire flames and smoke, ice shards and a frost star, water splash and ripple, wind leaves and gust rings, earth rocks and dust, energy arcs, light pillar and stars, dark wisps pulled in then a pop. The target flashes white, then its element's color; knockback and a recoil tilt scale with damage; hit-stop and shake scale with crit / weak / heavy. Monster melee leaves red claw rakes instead of the hero's sword crescent. Element hits add a sound layer (8 new synthesized sounds).
- **Crits:** a star flash with rays, a small camera punch-in, longer hit-stop. **Weak:** an extra ring. **Resisted/Immune:** grey sparks, no element burst, less shake. **Absorb:** the element is pulled into the target and it glows green.
- **Defend:** aura rings when raised, the bubble shimmers while it holds, and a blow on it flares the bubble and glances off in sparks; the number gets a "Guarded" tag. **Miss:** the dodger leaves an afterimage.
- **Heals and buffs:** green (or blue for MP) flash, rising plus signs, a magic circle for skill heals; lifesteal streams motes from the target back to the attacker. Buffs: circle, rising aura rings and a column of light.
- **Statuses:** rising chevrons for buffs, sinking chevrons plus a puff for debuffs, circling stars for Stun (also when the stunned side loses its turn). Ticks show the status itself (Burn flickers, Poison bubbles green, Bleed puffs red). New optional `tint` on statuses (`src/data/statuses.ts`) sets their color; the default is the damage element's color, else red/green.
- **Deaths:** knocked back on the killing blow, then the body dissolves into motes in its colors. Bosses go down in a chain of three blasts and a white flash (+0.6 s, bosses only).
- **Combos:** multi-hit skills (Flurry, Thousand Cuts, Spark Chain...) lunge once, stay in reach, and hit in quick succession; their numbers fan out so they don't cover each other. Thousand Cuts went from ~6.6 s to ~2.6 s, Spark Chain from ~4.3 s to ~2.2 s. Single hits take the same time as before (~0.8 s per melee action).
- **Readability:** status panels, bars and menus now sit above all hit effects (`HUD_DEPTH`), so particles never cover them; enemy floating text starts below a tall info panel (Elites, bosses).
- Fixed: a scene stopped during a hit-stop came back in slow motion next time (`fx.hitStop` now always restores the time scale).
- Code: effects in `src/ui/fx.ts` (`elementBurst`, `lightning`, `clawMarks`, `critFlare`, `castCircle`, `aura`, `statusMark`, `stunStars`, `dissolve`...), sounds in `src/ui/sfx.ts` (`ELEMENT_SFX`), playback in `src/scenes/BattleScene.ts`.
- Checked in the browser: all 8 element bursts side by side, Warrior heavy crit on a shape monster, monster claws on a guarded hero, Mage magic circle + Meteor + Spark Chain combo, Rogue Thousand Cuts with a miss and Bleed, War Cry, Stun, Burn tick, Second Wind, Thornheart boss death. Real fights for all three classes run to the result screen with no console errors. Typecheck and build pass.

## Chapter 2-3 boss tuning (fixed beats)

- **Why:** a random-button bot beat the Ch2-3 bosses 75-98% of the time. Their hits had fallen behind hero HP: normal hits took 7-13% of max HP and big hits 16-38%, vs the Thornheart's 13-18% and 34-46%.
- **Beats:** every boss now winds up its big hit every 3rd move, in both forms (Drag Under, Thunderhead / Tidal Crush, Avalanche, Glacial Prison / Cyclone). The big hit wins ties with other timed moves (Wallow, Blizzard). Traits say so on the battle panel.
- **Numbers:** Bogmother damage 15-20 -> 20-26, Drag Under 2.4 -> 3.2. Stormcoil HP 480 -> 430, damage 18-24 -> 23-31, big hits 2.2/2.3 -> 3.0. Old Snowmaw damage 20-26 -> 28-36, Avalanche 2.4 -> 3.4. Frostbinder HP 760 -> 650, damage 22-28 -> 26-34, big hits 2.3 -> 2.8.
- **Sim, shop gear at the intended level** (smart / random): Bogmother 99-100% / 57-76%, Stormcoil 91-99% / 52-72%, Old Snowmaw with Fire 89-100% / 34-95%, Frostbinder 83-99% / 68-76%. Thornheart for comparison: 97-99% / 47-71%. With a full set + jewelry they're easy again (the farming reward). Pace bot: bosses still rarely beat it (it arrives over-leveled and defends on the beat).
- **Sim:** `npm run sim -- 400 <word>` prints only matching tables; loadouts can `carry` skills (the Fire loadout gives the Mage Meteor and Ember Bolt); new table: Heroic rematches with the area set.
- Heroic stays x1.25: a bigger multiplier crushes players without a set before it challenges set-geared ones (see PLANS.md).
- Checked in the browser: Old Snowmaw's panel (shell line + new trait line) and the Stormcoil's trait fit.

## Spare weapon and mid-fight Swap (2026-10-15)

- **What:** a 9th slot, **Spare**, next to the weapon (Inventory). Any weapon fits; its lines (stats, mods, special) only count while it's in hand. Put one there by dragging it onto the slot or with **As spare** on its card (double-click still equips to the hand). **Swap** on the card or dragging the weapon onto the other slot switches them outside battle.
- **Battle:** a 6th button, **Swap** (key 6): puts the spare in hand and **costs the turn**, like a potion. The sub-label shows its element and how it would land now ("Water · Weak!"); hover shows its damage. The hero panel's top right shows the weapon in hand the same way ("Energy · Resisted") and updates after swaps, form changes and resist debuffs. The hero turns and comes back holding the other weapon. Without a spare the button is greyed ("No spare") and its hover says how to get one. Skills that use the weapon's element and damage type follow the swap.
- **Unlock:** feature `weapon_swap`, after Chapter 1 (with Elites and chests), announced in the New! popup. Before that the slot and button are hidden.
- **Rules:** `EquipSlot` gains `spare`; `EQUIP_SLOTS` (stats, sets, crit, HP) stay the 8 worn slots, `ALL_SLOTS` adds the spare where owned gear is listed (save, uid lookup, forge list). `fitsSlot()` lets weapons into it. `player.swapWeapons()`, `player.spareAttackRange` (exact, by swapping in place and back), `Battle.swapWeapon()`, `Battle.effectOn(element)`. Save v13 (old saves get an empty spare slot).
- **Calls made:** the swap costs the turn (the user's pick: a free swap would mean always hitting the weakness). It persists after the fight (no auto-swap back). Unlocks after Chapter 1 (an assumption, see PLANS.md "Open").
- **Sim:** loadouts can carry a `spare`; the smart bot swaps when the spare's attack (damage x element) is 30%+ better, the random bot sometimes presses Swap. Stormcoil L10 with a Water weapon + Energy spare: smart 98-100% (Water only: 73-97%), Warrior and Rogue fights ~12-13 turns instead of ~19-23 (Mage spells have their own elements, so no change). Frostbinder L14 Fire + Earth spare: 98-100% (Fire only: 93-100%). The random bot does worse with a spare (27-45% vs Stormcoil): swapping at the wrong time wastes turns.
- Checked in the browser: inventory with and without a spare (Warrior, Rogue), spare and bag weapon cards (3 buttons fit), Swap from the card, a v12 save loading with an empty spare slot, and the Stormcoil fight before and after a swap (tag, button, log, hero redrawn, turn passes). Typecheck, build, `npm run sim` and `npm run pace` pass.

## Testing rates (2026-10-15)

- **Why:** to see lots of drops (and the drop screen) and level fast while testing, the designer asked for a "10x run".
- **What:** testing menu, Hero tab, next to Full heal: **Rates x1 / x2 / x5 / x10**. Until the page reloads, never saved, like the other toggles. Multiplies XP and gold from kills, chests and quests, and every drop chance: gear tiers, Sigil tiers, set pieces, monster potion/material drops, a chest's potion and the Lucky Impling's Sigil, each capped at 100%. Bad-luck protection is added on top unchanged. The battle panel's Gear/Set odds show the boosted numbers.
- **Code:** `cheats.rates` and `boosted(p)` in `src/game/cheats.ts`, applied in `Loot.tiersFor`, `Items.rollSigil`, `Battle.rollRewards`, `Chests.rollChestLoot`, `Quests.turnInQuest`.
- **Measured** (20k Thornwood kills): x1 gear 11.4% per kill, Rare 0.9%, Sigil share 2.4%; x10 gear 82.2%, Rare 10.3%, Sigil share 25.5%.
- Checked in the browser: the menu row and toast, and a x10 Stormcoil win (3000 XP, 1155 gold, Rare 100%, level up on the result screen). Typecheck, build and the sim pass (rates default to x1).

## Higgsfield art run (2026-10-15)

- **What:** 85 images from `art/QUEUE.md` on Higgsfield's free Unlimited Nano Banana: the 3 Hearthold NPCs (Sabine, Gunnar, Moll), 16 monsters (every one except the two dragons and the dropped Volt Eel), 25 backgrounds (every room except `bg_frozen_roost`, whose sleeping dragon waits for the dragon clean-up), all 24 potion/material/essence icons and 17 armor outfits (Warrior 9, Mage 8). Ottilie, made in an earlier run, is committed with them.
- **Style refs:** Ch1 rooms used `bg_thornwood`, Ch2 rooms `bg_fen_road`, Ch3 rooms `bg_frostfang_pass`, icons `icon_leather_armor`, outfits the class's bare art.
- **Retries kept:** Crag Ram (the first had odd horns), Drowned Shrine ("scale" came out as a weighing balance; the new prompt says a shed creature scale, and avoids "dragon"), Roost Stair (flat ground to the bottom edge), Salamander Scale (the first read as an egg). Windscour Ridge came with black letterbox bars (cropped off) and its cave sits on the exit at x 640 (`BG_FOCUS`). Hand fixes on the processed files: Moll's steam and the Spark Wisp's glow (green spill), the Revenant's feet and the Rime Acolyte's crystal (magenta), the Mana/Superior Mana potions (magenta seen through glass), Light Essence (green in the neck).
- **Lesson:** glass and glows pick up the key color. Ask for a green background for glass items (the essence vials came out clean that way).
- **`prepare_art.py`:** outfits (`char_<class>_<armor>`) are cut out and cropped exactly like their bare art, so the fists land on the class's hand anchors with no re-measuring; inside the bare art's body only near-exact key colors are removed (the Mage's muted green key is close to skin and parchment shading, and one soft outline let it eat a fist). Every other file processes byte for byte as before. `MAGENTA_SPILL` gained the Stormcoil, Rime Acolyte and Frostbinder.
- **Outfits paused** at 17 of 30: they get redrawn if the hero art changes, and they paint their own gloves and boots (see PLANS.md, Art). The Warrior's Galeweave billowed far out of the frame and was not added.
- Checked in the browser: a Mage in the Fleecewool robe and a Warrior in the Frostpelt vest in battle, each holding the class weapon in the right fist; NPC and background art load.

## Local art: ComfyUI + Qwen-Image 2.1 (2026-10-15)

- **Setup** (designer's PC, not in the repo): ComfyUI 0.40 portable at `C:\Users\Epic\ComfyUI_windows_portable` with the Qwen-Image 2.1 int8 files from Comfy-Org (image model 7.26 GB, Qwen3-VL 8B text encoder 9.35 GB, VAE 0.68 GB). RTX 5070 Ti 16 GB: ~10 s at 1024 px, ~35 s at 1536 px, 2K backgrounds a bit more.
- **Why:** it writes a real alpha channel (no magenta/green keying, so none of the glass/glow/fist problems of the Higgsfield run), takes up to 10 reference images, edits with the same weights (outfits), and is ~15-20x faster than Higgsfield's queue.
- **`scripts/qwen_batch.py`:** reads `art/QUEUE.md`, rewrites each prompt for Qwen (sprites: RGBA wrapper instead of the flat-background sentence; backgrounds stay opaque) and adds style references with "style only, none of their subjects" wording: NPCs `npc_elder_maren` + `npc_hulda`, monsters `mon_thornback_boar` + `mon_mossling`, a room's background from its area's fallback backgrounds in `world.ts` (`bg_thornwood` when it has none), an outfit from its class's bare art. Icons and weapons get no reference: Qwen copied any reference object outright (a dust pouch came back as the leather armor, the potion, then inside the ring), while the prompt alone gave the right subject in a matching style (an edit at the bare art's own canvas, so `prepare_art.py` crops it like the bare art). Writes `art/raw/<key>.png` (never over an existing file without `--force`), or `--variants N` into `art/review/<key>/` (git-ignored). `--dry` prints the prompts, `--prepare` processes the results.
- **`prepare_art.py`:** a source PNG with real transparency skips the key-color removal; alpha below 10 is cleared first (Qwen leaves 1-9 over about a third of the empty canvas). Opaque sources process byte for byte as before. New `HAND_EDITED` list: the 8 processed files touched up by hand in the Higgsfield run are skipped unless `--force`, so a full run no longer undoes those fixes.
- **Test** (into `art/review/`, nothing replaced): the Mire Toad with the Boar + Mossling as references matched the game's look at 1536 px; Gunnar matched too. A Rogue Leather Armor outfit kept the same face and pose, and both fists landed on the class's hand anchors after `prepare_art.py`. Fen Road first copied Brindlemoor's buildings (its only reference then); with `bg_thornwood` and the stronger wording it came out as a swamp road in the game's style.

## Shorter road, class gate guards and weak spots (2026-10-15)

- **Why:** the designer played the start: "it takes a little too long to get to the city, it's uneventful". Ask: each class gets its own tutorial boss, weak to its first skill, to teach weak spots. Their picks: Warrior's first skill is blunt and the boss is weak to blunt; the Rogue gets a gas shot and the boss is weak to poison; the Mage's first skill is Light and the boss is a dark thing weak to Light. So each class learns a different kind of weak spot (damage kind, status, element).
- **Road:** 3 rooms instead of 6, 3 pests instead of 16 (a first try at 4 rooms with a campfire room was still too long for the designer): Overturned Cart (Odo, 1 pest) -> Old Mill Road (2 pests) -> Brindlemoor Gate (Pell, a first-visit scene, the guard; the gate is barricaded until it's beaten). A loss to the guard wakes you by Pell in the same room, at full HP. Guards give 35 XP so the hero reaches level 2 right after one. Pace bot: 2 min, 4 fights.
- **Gate guards** (level 2 bosses, one per class, `room.boss.byClass`): Millstone Crab (Weak: Blunt, stone shell), Dusk Wraith (Dark, Weak: Light), Hedge Troll (Weak: Poison, thick hide). Each trait line names the skill ("Use Light Bolt!"). They always drop the class's starting armor (`gate_garb` / `gate_robe` loot) and a potion, and give full XP (no rematch: a guard is gone for good once beaten).
- **Weak spots besides elements:** `MonsterDef.weakTo { blunt, poison }` as % extra: Blunt skills (`SkillDef.blunt`: Heavy Strike, Shield Bash) hit harder, Poison ticks are bigger (Poison still ignores resists and armor). Shown on the battle panel's "Weak:" line next to elements; hits and ticks get the WEAK! tag and "Weak spot!" in the log. Skill buttons now say "Weak!" (or "Resisted"...) when the skill would land that way, and name Blunt/Poison like an element.
- **Skills:** Mage: Light Bolt (1.5x Light, 6 MP) at level 1; Staff Bolt moved to level 2 next to Mend (at level 4 it pushed Frost Lance out of the default pick and the sim's mid-game Mage dropped 9-15 points). Rogue: Gas Shot (0.6x + Poison 3 turns, 4 MP, cooldown 2) at level 1; Twin Cut moved to level 2 next to Bandage. Heavy Strike is unchanged except for Blunt.
- **Rules:** gate guard = a room `boss` with no `questId`; `roomBoss()`, `isGuard()`, `recordGuard()` and a shared `exitOpen()` (room and map) in `src/game/Travel.ts`; exits marked `guarded` stay shut while it stands. `PlayerData.guardsBeaten` (save v14). On load, skills a class gained after the save was made go into a free slot.
- **Sim** (level 1, no armor, 2 potions; smart / random bot, with the weak spot vs. the same fight with it switched off): Warrior vs Crab 100% / 87% in 8.7 turns (without: 81% / 22%, 14.1 turns); Mage vs Wraith 99% / 89% in 6.0 (without: 59% / 27%, 12.9); Rogue vs Troll 100% / 100% in 9.3 (without: 94% / 65%, 13.6). Elsewhere only level-1 Rogue rows moved (no Twin Cut at level 1: Ash Imp L1 37% -> 15%, Lucky Impling L1 81% -> 61%).
- Checked in the browser: a new Mage on the road, the Wraith at the barricaded gate, "Weak: Light" and the trait line, Light Bolt's "Weak!" button and WEAK! 30, the win (robe drop, full XP after the fix below), the gate open and the guard gone after a reload; the Rogue's Troll (Gas Shot "Poison · Weak!", 9-damage weak-spot ticks) and the Warrior's Crab (Heavy Strike WEAK! 24). Fixed on the way: the gate room needed a zone for battle scenery, and guards got rematch (half) rewards.

## Adventurers' Guild (2026-10-15)

The designer asked to remove most quests and replace them with ranked adventurer hunts from a guild NPC (PLANS.md, Adventurers' Guild). Was parked as #6 Hunter contracts.

- **Rules** (`src/game/Guild.ts`, no Phaser): ranks F-S (`BALANCE.guild.ranks`). Hunts are built from data, not written by hand: every monster in a reachable area gives a kill hunt (5, or 3 if Tough) and, once Elites unlock, an Elite bounty; every area with a `ZoneDef.gather` item gives a gather hunt (4 items, any monster there drops one at 50% while the hunt is on, through `Battle.zoneId`). Reachable = rooms you can walk to from a town you've been to, through open exits, up to one room past where you've been; the one-way road never counts. Hunt rank = its monster's level band, +1 for Tough or Elite; you may take hunts up to 2 ranks above yours. Rewards: XP and gold of the kills it takes (`xp`, `gold` multipliers) plus rank points (1 at your rank, +1 per rank above, 0 below). Promotions carry left-over points and give `PROMOTIONS` (`src/data/guild.ts`): gold, potions, and the armor the removed errands gave (E Leather Armor, D Scale Mail / Reedweave Robe, C Frostpelt Vest / Galeweave Robe).
- **Board:** 5 notices shared by every clerk, refilled when you open it (at your rank or above first, an Elite bounty with 20% per slot, lower hunts only to fill gaps), rerolled after each turn-in. Hold 3; drop any (gather items stay in the bag). Hunts are saved by key (`kill:mire_toad`, `gather:fen`, `elite:mire_toad`), so retuning applies to hunts already taken.
- **NPCs:** Tamsin (Brindlemoor), Ivo (Reedwatch), Edda (Hearthold), `role: { kind: 'guild' }`; talking opens the board directly.
- **Board UI** (`src/ui/guild.ts`): rank badge and points bar, the next story quest's rank, your hunts with progress bars and Turn in / Drop (asks twice), notices with rank badge, where to go, rewards and points.
- **Quests:** 15 -> 7. Removed: Moss Problem, Bramble Bandits, Ember Trouble, Sparks in the Reeds, A Worthy Foe, Hearthold Holds, Fire for the Hearth, Thaw. New `QuestDef.requiresRank` and quest status `'rank'`: Heart of Thorns needs E, Mother of the Mire D, The Old One C. The giver says to come back at that rank; the tracker shows "Next: reach Guild Rank E (you are F)". Exits sealed by a rank-gated quest now show from the start (blocked).
- **Everywhere else:** tracker rows "Hunt (D): Mire Toads 3/5", result screen lines ("Return to a Guild Clerk"), the battle panel's loot line adds "· Bog Lily 50%" (hover for details), map gold rings where hunt monsters roam and **?** on a clerk's room when a hunt is done, Elites spawn more often during an Elite bounty, 10 hunt items (materials: no bag slots).
- **Save v15** (v14 is the gate guards, which landed first): `PlayerData.guild`. Old saves get the rank their story progress implies (past Bramble Bandits or Heart of Thorns: E; past A Worthy Foe or Mother of the Mire: D; past Thaw or The Old One: C); removed quests are dropped.
- **Testing:** chapter starts get the rank their skipped quests need; the testing menu has Guild rank +1, Reset guild, and "Finish quests + hunts".
- **Pace bot** (`npm run pace`) takes hunts when a story quest waits on a rank, and reports hunts and rank per chapter. Ch1 23 min, Ch2 52, Ch3 62 (was 32/41/49). `npm run sim` is unchanged.
- Checked in the browser: Rank F board in Brindlemoor, taking hunts, a gather drop and kill count on the result screen, turn-in, promotion to E with Leather Armor, the Rank E board in Reedwatch (D hunts, an Elite bounty, a gather hunt), Hale's "come back at Rank D" line, and a v13 save migrating to Rank E.
