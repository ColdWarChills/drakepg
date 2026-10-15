# Plans (not built yet)

The one list every chat works from. **Decided** = the designer agreed. **Proposed** = suggested, not approved yet. **Open** = needs an answer. Built work moves to `docs/IMPROVEMENTS_2026-10.md`. Nothing here is in the code yet unless it says so.

Combined on 2026-10-14 from three chats ("Progress review and planning", "Drakepg design pass" and the combat design chat).

## Next up

Build order (all built, see "Done" at the bottom):
1. ~~Recall Scroll~~
2. ~~Room features: quiet rooms, campfire, down exit, saving visited rooms~~
3. ~~Map~~
4. ~~Chapter 1-3 room layouts plus drafted story lines for review~~
5. ~~Sim and pacing check, art queue entries~~

- **Review:** every new line (first-visit scenes, notes, signs) is a draft, listed in `docs/ROOM_LINES.md`. Mark what to change; the next chat edits `src/data/world.ts` and deletes that file once approved. Includes the Hunters' Camp lines.
- **Timed run:** the designer times one run per chapter; compare with the pace bot (see "Longer chapters").
- **Playtest by hand** (only the sim, the pace bot and screenshots have checked these so far):
  - New rooms: one timed run per chapter (the pace bot's minutes are guesses). Do the quiet rooms feel good or slow? Are campfires before every boss too generous?
  - Map, Recall Scroll: is the map readable now that pages have 7-9 rooms in a row?
  - Prologue road: is 6 rooms the right length? Monster HP 20-24 (about 2 hits) and a 35% gear drop (`BALANCE.loot.tableMob.road`)?
  - Does the "Next:" intent line read at a glance? Does enrage (turn 15, bosses 30) feel fair?
  - Is the drop reveal too slow when farming normal monsters?
  - Is the 5-slot skill swap in the Character panel clear?
  - 8 equipment slots, sets, Lucky Impling.
  - Ch2-3 bosses (retuned 2026-10-14): is a big wind-up every 3rd move fair, or too samey across bosses?
  - Battle effects (built 2026-10-14): too much or too little? Do farming fights feel slower? Easy to tone down per effect in `src/ui/fx.ts`.

## Longer chapters (Ch1-3)

- **Decided:** make chapters much longer than now (estimate before the new rooms: Ch1 15-20 min, Ch2 30-40, Ch3 30-40; not playtested).
- **Decided:** early chapters level up a bit faster, so players feel powerful early, but fights stay risky.
- **Decided:** add rooms to Chapters 1-3 (not only from Chapter 4 on).
- **Decided:** not every room has monsters. Some rooms only move the plot along and build anticipation.
- **Decided:** the early game is easy for every class: kill simple monsters, farm, grind, collect gear.
- **Built (2026-10-14) as proposed:** 8/9/10 rooms (+ town) for Chapters 1/2/3; about half combat (2-3 monsters each), the rest quiet or side rooms; 2-3 combat rooms, then a quiet room; a quiet room with a campfire right before every boss. Same quests and XP curve. New rooms reuse their area's backgrounds; 15 background prompts are in `art/QUEUE.md`.
  - Ch1: Thornwood Path (+ Hunters' Camp below), Fallen Oak, Woodcutters' Clearing (quiet), Deep Thornwood (+ Bramble Hideout below: bandits), Briar Wall (quiet, campfire, the thorn wall), Thornheart Hollow.
  - Ch2: Fen Road, Reedwatch, Sunken Causeway (+ Eel Pools below: Tough eels and a sure chest), Whispering Reeds, Stilt Village (quiet), Bog Hollow, Stormlit Flats, Shrine Steps (quiet), Drowned Shrine.
  - Ch3: Frostfang Pass, Hearthold, Windscour Ridge (+ Steamvent Caves, now with a sure chest, + Hot Springs past them: quiet), The High Trail, Ranger's Lookout (quiet), Snowmaw's Den, Whiteout Ledge (the acolytes), Roost Stair (quiet), The Frozen Roost.
  - Quest gates moved to the quiet room before each boss (thorns, reeds, snowdrifts), so you see the pre-boss room before the quest.
- **Pace bot (`npm run pace`, guessed seconds per action; it fights everything it walks past):** old layout -> new: Ch1 28 -> 32 min, Ch2 33 -> 41, Ch3 46 -> 49. Level at each boss barely moved (Thornheart L5 at hero 5-7, Bogmother L8 at 8-10, Stormcoil L10 at 9-12, Snowmaw L12 at 13-18, Frostbinder L14 at 14-19). Most of the time is quest farming, which more rooms don't change.
- **Decided (2026-10-14):** no side quests now. Build the base first (rooms, systems); quests and side quests come with the story pass. The length gap to the targets waits until then.
- **Proposed:** target lengths about 35 / 55 / 70 min (rough estimates).
- **Open:** keep campfires after a playtest? (Built, easy to remove per room.)
- **Open:** sure chests in side rooms (`room.chest`, first fill each session, never a mimic): keep? Only the Eel Pools and Steamvent Caves have one; Chapter 1 has none because chests unlock after it.

## Map (built 2026-10-14)

- Built as proposed: **M** or the Map button; one area per page (Old Road, Brindlemoor and the Thornwood, Mirewater Fen, Frostfang Mountains); rooms as dots, exits as lines; "?" for unvisited neighbours; icons for town, boss, campfire; "You" marker; quest marks (! pick up, ? turn in, gold ring = an active quest's monster lives here); dashed red = blocked; arrows to other pages.
- **Changed:** M used to mute the sound. Sound is now only in the Menu.
- **Open:** sealed back exits (like the mountain gate before its quest) stay hidden on the map, same as in the room. OK?

## Recall Scroll (built 2026-10-14)

- Built with the proposed defaults: one way to the last town you walked into, not in battle, 20g in every town shop, new characters start with 5 (`BALANCE.startingRecallScrolls`). Read it from the Inventory or the Map; it asks first.
- **Open:** confirm price and one-way after a playtest.
- Old saves (before v12) also got 5 scrolls, so existing characters can try it.

## Prologue road (built as a placeholder)

- **Decided:** 6 rooms, start with the class weapon only, one-way into Brindlemoor (the shop is found on arrival). Claude drafted the story hook.
- To do: real monsters (names, stats, art) for `burrow_rat`, `dew_slug` and `hedge_pixie`. They are shape stand-ins now.
- To do: background art for the 6 `road_*` rooms (procedural forest now), and art for Odo and Pell. None of it is in `art/QUEUE.md` yet.
- To do: final lines for Odo and Pell.
- **Open:** the hero looks armored with no armor on, because the base class art includes armor. Keep it, or get bare art?

## Story and theme

- **Decided:** the world becomes an original, IP-free magic world with set rules (like Harry Potter, but its own).
- **Decided:** ancient-god dungeons (Greek, Baltic, Egyptian...) come later as DLC/liveops.
- **Decided:** dragons must be super rare, not a common enemy or a boss every chapter.
- **Decided:** the hero is a wanderer. Keep Warrior, Mage and Rogue.
- **Decided:** no rework of the Ch1-3 story for now.
- **Decided:** rooms stay stationary single screens, with vertical (up/down) exits allowed.
- **Proposed:** story rooms always show their short lines, even with story text off.
- **Open:** turn story text on by default? It's off now (`storyText: false` in `src/game/settings.ts`), so players see only each NPC's first line.
- **Open:** give each quest offer a short in-character line too (offers still use the plain reminder line)?
- **Open:** magic-world story spec (rules of magic, who the wanderer is, how it connects to Ch1-3). Not started.

## Dragon clean-up

- **Decided:** dragons become rare (see above).
- **Open:** remove or replace the common dragons: Stormscale Drakeling (Ch2), Vent Wyrmling (Ch3), the dragon story thread, the dragon on the title art. Waiting for go.
- **Open:** `docs/CHAPTER3_SPEC.md` still asks for the dragon's name ("wakes in Chapter 4?"); revisit with the story spec.

## Skills, weapons and builds (discussed 2026-10-15)

Facts from the code at the time: Warrior and Rogue skills take the equipped weapon's element and damage type (daggers are pierce, so Rogue skills scale on DEX); only 11 skills, mostly the Mage's, have a fixed element. Stormcoil (Energy -> Water) and the Frostbinder (-> Wind) change element mid-fight. Gear has no "+element damage" line, only a resist per element. Stat points can't be refunded (only the cheat menu can). Monster weaknesses are -50 to -75, resists up to +75.

- **Built (2026-10-15):** mid-fight weapon swap (Spare slot, Swap costs the turn), see `docs/IMPROVEMENTS_2026-10.md`. Why: players won't own the best weapon of every element, so it keeps pushing upgrades; a weaker backup in the right element still beats a strong weapon in the wrong one.
- **Open:** the spare slot unlocks after Chapter 1 (with Elites and chests), so the Stormcoil is the first boss it matters for. Keep, or from the start?
- **Playtest:** is the Swap button clear? Does a swap feel worth a turn outside the two-form bosses?
- **Proposed:** skills come from drops and town vendors, not only from levels, even some early ones. Not all of them strong. The strongest ones come from known sources (a boss's first kill, Heroic rematches, a Lucky Impling, a side room's sure chest), not random trash drops (bad-luck protection rarely kicks in).
- **Proposed:** element skill families: the same skill in different elements (beam of fire, of water, of light...), so a player can build e.g. a fire Rogue. The shape is per class (e.g. Warrior strike, Rogue knives, Mage beam), the elements are shared. Damage type follows the class's weapon, so a Rogue's element skills scale on DEX, a Mage's on INT.
- **Proposed:** a spellbook that shows every skill: unknown ones as dark pages with a hint where to find them, tabs per element (HoMM3-like).
- **Proposed:** weapons require a minimum of their class's main stat to equip. Investing in another stat opens another class's weapons (hybrids, used through the swap).
- **Open:** which skills move to drops/vendors, and do the level unlocks stay for the rest? Books for your own class only, or cross-class?
- **Open:** a hybrid needs a payoff: a sword for a Rogue is weak unless STR is invested. Weapons that scale on two stats? (Not strictly better than a pure build.)
- **Open:** a paid respec at a trainer, so a failed hybrid isn't permanent?
- **Open:** a "+element damage" gear line or element sets, so an element build is more than weapon + skills.
- **Open:** weight weapon drops toward the player's class, so stat-locked drops don't pile up?
- Dropped: replacing levels with gear-only power. Levels stay.

## Combat and balance

- **Decided:** monster rebalances are fine and expected often. Keep numbers small (no thousands early), especially weapons.
- **Decided (2026-10-14):** no fix for the Mage vs Thornheart at level 4 with starter gear (3%). Rushing the boss without gear may be hard; with shop gear it's 75%, at level 5 97%.
- **Built (2026-10-14):** Ch2-3 bosses wind up their big hit every 3rd move, like the Thornheart, and hit harder (their damage had fallen behind hero HP: normal hits took 7-13% of max HP vs the Thornheart's 13-18%). Sim, shop gear at the intended level: smart AI wins 83-100%, the random-button bot 52-76% (was 75-98%), close to the Thornheart's 47-71%. Old Snowmaw's test stays Fire (with it: 89-100% smart).
- **Proposed (built as the default, 2026-10-14):** first-kill bosses are tuned for shop gear; a full set + jewelry makes them easy (random bot ~95-100%) as the reward for farming. Trash was left as is (early game easy).
- **Open:** Heroic rematches are still easy with a full set (random bot 94-100%). A bigger Heroic damage multiplier hurts players without a set long before it touches set-geared ones, so it stays x1.25. Needs new Heroic moves (beats, a second phase), or a higher tier later.
- **Open:** Lucky Impling catch rates in the sim: Warrior ~9-85%, Mage ~18-87%, Rogue ~39-100% (worst in late areas). Tune later.
- **Open:** the longer chapters need one timed run by hand (the pace bot is done, see "Longer chapters").
- **Open:** no roll stone for the Power line of rings and necklaces.
- Watch: Old Snowmaw without Fire is a 0-4% win for every class (intended: the quest before her collects Fire Essence). With Fire she's 89-100% for the smart AI. The sim's Mage now carries Meteor and Ember Bolt for her (it had no Fire spell in its default 5, which made her look much harder for Mages).
- Watch: stacked-affix Elites on Tough monsters are harsh (Frenzied Ash Imp at level 3: 6-72%; Ironhide Drakeling: 15-77%). They can be fled.
- Watch: in normal fights the random bot does about as well as the smart AI. Accepted: the tension is in attrition between fights.

## Art

- **Decided:** keep generating with Higgsfield for now; redo the art later with Qwen at home if the polished look reads as AI. Files swap by key, so nothing in code changes.
- **Open:** about 100 images left in `art/QUEUE.md` (NPCs, monsters, 26 backgrounds incl. the 15 new rooms, potions, materials, armor outfits). Road art isn't queued yet (see Prologue road).
- **Open:** look-alike icons: Rimeglass Ring vs Frostward Ring, Tidepearl Ring vs Moonstone Ring. Regenerate one of each (~3 credits)?
- Dropped: Volt Eel art (the eel was only a sample). Its prompt `mon_volt_eel` is still in the queue.

## Parked

- **#6 Hunter contracts:** contracts replace quests; the player picks what to hunt; monsters not met yet show as a blacked-out "?" card. Delayed. Open: who hands them out (a board, an NPC)? Rewards per contract? Do the chapter story beats stay as fixed quests?
- **#7 Class choice before seeing combat:** not important now. Options: a free class swap until the end of Chapter 1, or a one-fight trial per class on the Create screen.
- **#8 Bad-luck protection kicks in too late:** agreed, not urgent. `pityAfter` is 80 dry kills per area; a chapter is roughly 40-50 normal kills, so it may never trigger. Options: lower to 25-30, count across all areas, or cut it.
- **#9 Endgame:** no goal yet, on purpose. Heroic boss tier is built. Open: more tiers later (new moves each, never more HP)?
- **#10 System overlap audit:** agreed in principle. First candidates: hone vs reinforce, and normal mods vs Sigil mods.

## Decisions to keep

- Engine (2026-10-15): stay on Phaser in the browser for now. A Unity port was floated (the designer finds testing easier there) and set aside: it's a full rewrite, and the cloud chats can't run Unity. The game is made for the designer's own enjoyment; selling a few copies (itch.io, or Steam as a wrapped web build) is optional, and disclosing the AI art on a store is fine.
- **Open:** what's hard to test in the browser build? The testing menu (backtick, or tap the room name 5x) covers levels, gold, items, travel, quests and unlocks; add what's missing.
- Skills stay simple (Silkroad style): skills just do damage. No interrupts, silences or similar for now.
- Enrage, not a hard turn limit. Heroic adds no HP. Only specific monsters absorb (resist 150).
- Element names stay Earth and Energy.
- Elemental weapons are part of the puzzle: carrying the wrong element should hurt. Absorbing monsters (Volt Eel, Ember Salamander) are the first case. Open: add the example weapons from the discussion ("leaf blade" as Earth, "lightning dagger" as Energy)?
- Drop reveal: keep the full result screen; reveal timing scales with how good the drop is.

## Smaller open items

- Economy review was skipped: the forge's gold costs and late-game gold flow weren't checked.
- `docs/CHAPTER2_SPEC.md` "Later": player marketplace (needs a backend), Alchemy, mobile touch controls, itch.io release.
- `docs/CHAPTER3_SPEC.md` "Open questions": does Hearthold get its own smith?

## Done since these notes were written

- 2026-10-14: #7 combat pass, #8 prologue road, roll stones + tier caps, and the 8 slots / sets / Lucky Impling branch are all merged and live. `LUCKY_ON_NEW_GAME` is off.
- 2026-10-14: build steps 1-3: Recall Scroll, quiet-room features (first-visit scene, inspect, campfire, down exits), saved visited rooms and last town (save v12), and the map. Placeholder side room: Hunters' Camp.
- 2026-10-14: battle effects pass (wind-ups, element impacts, crits, combos, statuses, deaths), see `docs/IMPROVEMENTS_2026-10.md`.
- 2026-10-14: build steps 4-5: Chapter 1-3 room layouts (8/9/10 rooms), drafted lines (`docs/ROOM_LINES.md`), sure chests in side rooms, `npm run pace`, 15 background prompts in `art/QUEUE.md`.
- 2026-10-15: spare weapon and mid-fight Swap (save v13), see `docs/IMPROVEMENTS_2026-10.md`.
