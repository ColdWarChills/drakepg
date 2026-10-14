# Plans (not built yet)

The one list every chat works from. **Decided** = the designer agreed. **Proposed** = suggested, not approved yet. **Open** = needs an answer. Built work moves to `docs/IMPROVEMENTS_2026-10.md`. Nothing here is in the code yet unless it says so.

Combined on 2026-10-14 from three chats ("Progress review and planning", "Drakepg design pass" and the combat design chat).

## Next up

Build order (steps 1-3 are built, see "Done" at the bottom):
1. ~~Recall Scroll~~
2. ~~Room features: quiet rooms, campfire, down exit, saving visited rooms~~
3. ~~Map~~
4. Chapter 1-3 room layouts plus drafted story lines for review
5. Sim and pacing check, art queue entries

- **Next:** step 4. Use the new room data (`campfire`, `inspect`, `firstVisit`, `side: 'down'` exits, `map` spots) to lay out ~8/9/10 rooms per chapter. Draft the lines, then the designer reviews them.
- **Review:** the Hunters' Camp (below the Thornwood Path) is a placeholder side room showing every feature. Its lines (first-visit scene, the torn note, the scored log) are drafts.
- **Playtest by hand** (only the sim and screenshots have checked these so far):
  - Map, Recall Scroll, Hunters' Camp: does the first-visit scene feel good or slow? Is the campfire too generous?
  - Prologue road: is 6 rooms the right length? Monster HP 20-24 (about 2 hits) and a 35% gear drop (`BALANCE.loot.tableMob.road`)?
  - Does the "Next:" intent line read at a glance? Does enrage (turn 15, bosses 30) feel fair?
  - Is the drop reveal too slow when farming normal monsters?
  - Is the 5-slot skill swap in the Character panel clear?
  - 8 equipment slots, sets, Lucky Impling.

## Longer chapters (Ch1-3)

- **Decided:** make chapters much longer than now (estimate today: Ch1 15-20 min, Ch2 30-40, Ch3 30-40; not playtested).
- **Decided:** early chapters level up a bit faster, so players feel powerful early, but fights stay risky.
- **Decided:** add rooms to Chapters 1-3 (not only from Chapter 4 on).
- **Decided:** not every room has monsters. Some rooms only move the plot along and build anticipation.
- **Decided:** the early game is easy for every class: kill simple monsters, farm, grind, collect gear.
- **Proposed:** the length comes from rooms and story beats, not grind. Keep the XP curve; spread about the same number of monsters over more rooms (1-3 per combat room instead of 3-4).
- **Proposed:** room count goes from 3/4/5 (+ town) to about 8/9/10 (+ town).
- **Proposed:** room mix: ~50% combat, ~25% quiet/story, ~25% optional side rooms (chest, Elite or lore).
- **Proposed:** rhythm: 2-3 combat rooms, then a quiet room. The room before each boss is always a quiet anticipation room.
- **Built:** quiet rooms with a one-time scene on first entry (dark bars, a low sound, the camera drifts toward `panTo`, a few lines; shown even with story text off) and things to inspect (note, sign, marks, bones) that hint at the next boss.
- **Built:** campfire in a room restores HP/MP once per visit (a fight in the room doesn't end the visit).
- **Built:** "down" exit (walk off the bottom edge at `x`) for side rooms; the side room's way back is a back exit.
- **Proposed:** new rooms reuse their area's backgrounds at first; about 20 new background prompts go to `art/QUEUE.md` (~30 credits).
- **Proposed:** target lengths about 35 / 55 / 70 min (rough estimates).
- **Open:** approve the room plan, or change the targets?
- **Open:** keep campfires after a playtest? (Built, easy to remove per room.)
- **Open:** Claude drafts the story lines for the new rooms from the existing Ch1-3 story, you review. OK?

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

## Combat and balance

- **Decided:** monster rebalances are fine and expected often. Keep numbers small (no thousands early), especially weapons.
- **Decided (2026-10-14):** no fix for the Mage vs Thornheart at level 4 with starter gear (3%). Rushing the boss without gear may be hard; with shop gear it's 75%, at level 5 97%.
- **Open:** Ch2-3 bosses are too soft: the random-button bot still wins 75-95%. Options: fixed `every` beats on their big attacks (worked for the Thornheart), or a per-chapter `bossDamage`.
- **Open:** monsters were not rebalanced after the equipment/sets update. Sim: a full set + jewelry gives ~20-30% fewer boss turns and +5-17% HP left.
- **Open:** Lucky Impling catch rates in the sim: Warrior ~9-85%, Mage ~18-87%, Rogue ~39-100% (worst in late areas). Tune later.
- **Open:** the longer chapters need a pacing check (sim + one timed run).
- **Open:** no roll stone for the Power line of rings and necklaces.
- Watch: Old Snowmaw without Fire is a 0-58% win for every class (intended: the quest before her collects Fire Essence).
- Watch: stacked-affix Elites on Tough monsters are harsh (Frenzied Ash Imp at level 3: 6-72%; Ironhide Drakeling: 15-77%). They can be fled.
- Watch: in normal fights the random bot does about as well as the smart AI. Accepted: the tension is in attrition between fights.

## Art

- **Decided:** keep generating with Higgsfield for now; redo the art later with Qwen at home if the polished look reads as AI. Files swap by key, so nothing in code changes.
- **Open:** about 88 images left in `art/QUEUE.md` (NPCs, monsters, backgrounds, potions, materials, armor outfits). Road art isn't queued yet (see Prologue road).
- **Open:** look-alike icons: Rimeglass Ring vs Frostward Ring, Tidepearl Ring vs Moonstone Ring. Regenerate one of each (~3 credits)?
- Dropped: Volt Eel art (the eel was only a sample). Its prompt `mon_volt_eel` is still in the queue.

## Parked

- **#6 Hunter contracts:** contracts replace quests; the player picks what to hunt; monsters not met yet show as a blacked-out "?" card. Delayed. Open: who hands them out (a board, an NPC)? Rewards per contract? Do the chapter story beats stay as fixed quests?
- **#7 Class choice before seeing combat:** not important now. Options: a free class swap until the end of Chapter 1, or a one-fight trial per class on the Create screen.
- **#8 Bad-luck protection kicks in too late:** agreed, not urgent. `pityAfter` is 80 dry kills per area; a chapter is roughly 40-50 normal kills, so it may never trigger. Options: lower to 25-30, count across all areas, or cut it.
- **#9 Endgame:** no goal yet, on purpose. Heroic boss tier is built. Open: more tiers later (new moves each, never more HP)?
- **#10 System overlap audit:** agreed in principle. First candidates: hone vs reinforce, and normal mods vs Sigil mods.

## Decisions to keep

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
