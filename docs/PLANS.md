# Plans (not built yet)

Decisions and open questions from planning sessions. **Decided** = agreed by the designer. **Proposed** = suggested, not yet approved. Nothing here is in the code yet.

## Longer chapters (Ch1-3)

- **Decided:** make chapters much longer than now (estimate today: Ch1 15-20 min, Ch2 30-40, Ch3 30-40; not playtested).
- **Decided:** early chapters level up a bit faster, so players feel powerful early, but fights stay risky.
- **Decided:** add rooms to Chapters 1-3 (not only from Chapter 4 on).
- **Decided:** not every room has monsters. Some rooms only move the plot along and build anticipation.
- **Proposed:** the length comes from rooms and story beats, not grind. Keep the XP curve; spread about the same number of monsters over more rooms (1-3 per combat room instead of 3-4).
- **Proposed:** room count goes from 3/4/5 (+ town) to about 8/9/10 (+ town).
- **Proposed:** room mix: ~50% combat, ~25% quiet/story, ~25% optional side rooms (chest, Elite or lore).
- **Proposed:** rhythm: 2-3 combat rooms, then a quiet room. The room before each boss is always a quiet anticipation room.
- **Proposed:** quiet rooms have a one-time scene on first entry (a few lines, a sound, a camera pan) and things to inspect (a note, claw marks, a wrecked camp) that hint at the next boss.
- **Proposed:** campfire in some quiet rooms: restores HP/MP once per visit.
- **Proposed:** new "down" exit (walk off the bottom edge) next to the existing left/right/back exits, for side rooms.
- **Proposed:** new rooms reuse their area's backgrounds at first; about 20 new background prompts go to `art/QUEUE.md` (~30 credits).
- **Proposed:** target lengths about 35 / 55 / 70 min (rough estimates).
- **Open:** approve the room plan, or change the targets?
- **Open:** campfires yes or no?
- **Open:** I draft the story lines for the new rooms from the existing Ch1-3 story, you review. OK?

## Map

- **Decided:** add a map.
- **Proposed:** opens with **M** or a HUD button. Shows one area at a time as a node graph: rooms as dots, exits as lines.
- **Proposed:** unvisited neighbours show as "?". Icons for town, boss and campfire. "You are here" marker and active quest target markers.
- **Proposed:** each room gets a hand-placed map position in its data.
- **Proposed:** visited rooms are saved (save change, see below).

## Recall Scroll (teleport)

- **Decided:** a scroll that teleports you back to the previous town.
- **Decided:** new characters get 5. After that they are bought in shops.
- **Proposed:** one-way to the last town visited, no return trip (walking back keeps the "push on or turn back" tension).
- **Proposed:** can't be used in battle.
- **Proposed:** sold in every town shop for 20g (a health potion is 15g).
- **Open:** confirm price and one-way.

## Save changes for the above

- **Proposed:** add `visited` rooms and `lastTown` to `PlayerData`, bump `SAVE_VERSION` to 11 with a `migrate()` step. Old saves get their current room marked visited and their area's town as the last town.

## Build order (proposed)

1. Recall Scroll
2. Room features: quiet rooms, campfire, down exit, saving visited rooms
3. Map
4. Ch1-3 room layouts + drafted story lines for review
5. Sim and pacing check, art queue entries

- **Open:** go on everything, or start with 1-3?

## Story and theme

- **Decided:** the world becomes an original, IP-free magic world with set rules (like Harry Potter, but its own).
- **Decided:** ancient-god dungeons (Greek, Baltic, Egyptian...) come later as DLC/liveops.
- **Decided:** dragons must be super rare, not a common enemy or a boss every chapter.
- **Decided:** the hero is a wanderer. Keep Warrior, Mage and Rogue.
- **Decided:** no rework of the Ch1-3 story for now.
- **Decided:** rooms stay stationary single screens, with vertical (up/down) exits allowed.
- **Proposed:** quest story text is off by default (`storyText: false` in `src/game/settings.ts`), so most players never see it. Story rooms should always show their short lines.
- **Open:** turn story text on by default?
- **Open:** magic-world story spec (rules of magic, who the wanderer is, how it connects to Ch1-3). Not started.

## Dragon clean-up

- **Decided:** dragons become rare (see above).
- **Open:** remove or replace the common dragons: Stormscale Drakeling (Ch2), Vent Wyrmling (Ch3), the dragon story thread, the dragon on the title art. Waiting for go.
- **Open:** `docs/CHAPTER3_SPEC.md` still asks for the dragon's name ("wakes in Chapter 4?"); revisit with the story spec.

## Balance

- **Decided:** monster rebalances are fine and expected often. Keep numbers small (no thousands early), especially weapons.
- **Open:** monsters were not rebalanced after the equipment/sets update. Sim: a full set + jewelry gives ~20-30% fewer boss turns and +5-17% HP left.
- **Open:** Lucky Impling catch rates in the sim: Warrior ~31-94%, Mage ~45-97%, Rogue ~35-100%. Tune later.
- **Open:** the longer chapters need a pacing check (sim + one timed run).

## Before release

- Turn off `LUCKY_ON_NEW_GAME` in `src/game/cheats.ts` (testing switch: new characters meet a Lucky Impling).

## Art

- **Decided:** keep generating with Higgsfield for now; redo the art later with Qwen at home if the polished look reads as AI. Files swap by key, so nothing in code changes.
- **Open:** 90 images left in `art/QUEUE.md` (NPCs, monsters, backgrounds, potions, materials, armor outfits).
- **Open:** look-alike icons: Rimeglass Ring vs Frostward Ring, Tidepearl Ring vs Moonstone Ring. Regenerate one of each (~3 credits)?

## Older backlog

- `docs/CHAPTER2_SPEC.md` "Later": player marketplace (needs a backend), Alchemy, mobile touch controls, itch.io release.
- `docs/CHAPTER3_SPEC.md` "Open questions": does Hearthold get its own smith?
