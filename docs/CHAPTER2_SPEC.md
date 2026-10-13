# Chapter 2 spec: Rising Waters

Living checklist. Approved 2026-10-13; adjust freely. Numbers are starting points: `npm run sim` decides the final ones (all live in `BALANCE` or `src/data/`).

Ships in two PRs: **1. Elites** (also improves Chapter 1), **2. Chapter 2 content**.

## Story

After the Thornheart falls, the river west of Brindlemoor floods and the Mirewater Fen crackles with storm energy. A great serpent, the Stormcoil, coils around a scale the old dragon shed long ago and feeds on its power. Teaser for later chapters: where is the dragon, and why did it go quiet?

## Map

Single-screen rooms, left/right exits, like Chapter 1.

| Room | Level | Notes |
| --- | --- | --- |
| Brindlemoor | | New west exit, barricaded until quest 1 is accepted |
| Fen Road | 6-7 | Mire Toads, Spark Wisps |
| Reedwatch | | Outpost town: Captain Hale (quests), Quartermaster Wren (shop), Sister Ottilie (healer = inn) |
| Sunken Causeway | 7-9 | Bog Lurkers, Sunken Revenants, Stormscale Drakelings |
| Bog Hollow | 8 | Mini-boss: the Bogmother. Blocked by reeds until quest 4 |
| Drowned Shrine | 10 | Final boss: the Stormcoil. Blocked until quest 5 |

## Elements

Water, Energy, Dark. Chapter 1's Earth boss weapons are neutral here, so the new Water and Energy drops matter: Water monsters are weak to Energy and the other way round.

## Monsters

| Monster | Lv | Element | Type | Hook |
| --- | --- | --- | --- | --- |
| Mire Toad | 6 | Water | melee | Tongue Lash may poison |
| Spark Wisp | 7 | Energy | magic | Static Bolt may Shock; hard to hit with spells |
| Bog Lurker | 7 | Water | melee | heavy, Clammy Grip may Chill |
| Sunken Revenant | 8 | Dark | pierce | Grave Touch may Shadow Rot; weak to Light |
| Stormscale Drakeling | 9 | Energy | pierce | Skitter: Evasive buff |
| **The Bogmother** (mini-boss) | 8 | Water | | Weaken spit, **Drag Under** telegraphed a turn ahead |
| **The Stormcoil** (boss) | 10 | Energy | | **Thunderhead** telegraphed; **at 50% HP sheds its skin and turns Water** (resists flip, shown in the battle UI) |

Bosses stay for rematches (half XP/gold, boss Sigil odds).

## Elites (PR 1)

- [x] Any roaming monster (Chapter 1 too) can spawn as an Elite: **8%** per spawn, max **1 per room**. While a quest wants an Elite: **25%**.
- [x] Look: 1.2x size, gold aura, "ELITE" tag over the name.
- [x] Stats: +1 level (shown), **2x HP**, **1.25x damage**.
- [x] One affix, named in the monster's name and explained in the battle UI:
  - Hulking: +50% HP
  - Frenzied: +25% damage
  - Warded: +25 to all resistances
  - Venomous: attacks may poison
  - Ironhide: +50% defense
- [x] Rewards: **3x XP and gold**, **1 guaranteed gear drop** from the zone's loot (gear the zone's monsters drop).
- [x] Elite Sigil odds per gear drop: Dawn **8%**, Dusk **2%**, Eclipse **0.3%**.
- [x] Quest objective "defeat any Elite". Elite kills also count for their base monster's quests.
- [x] `BALANCE.elite` holds every number; affixes are data in `src/data/elites.ts`.
- [x] `npm run sim` rows for Elites.

## Quests (PR 2)

Kill quests, each unlocks the next.

1. [ ] **Rising Waters** (Elder Maren): 4 Mire Toads. Accepting opens the west road.
2. [ ] **Sparks in the Reeds** (Captain Hale): 4 Spark Wisps.
3. [ ] **A Worthy Foe** (Captain Hale): defeat any Elite.
4. [ ] **Mother of the Mire** (Captain Hale): the Bogmother. Opens Bog Hollow.
5. [ ] **Eye of the Storm** (Captain Hale): the Stormcoil. Class reward, ends Chapter 2.

## Gear (PR 2)

Tiers are about x1.5 apart; items in one tier are sidegrades.

- [ ] Reedwatch shop, tier 2 Standard: Steel Broadsword, Yew Staff, Fenwatch Stiletto, Scale Mail, Reedweave Robe, plus Greater Health/Mana Potions (old potions heal too little by now).
- [ ] Monster drops, tier 2 elemental (one Water and one Energy weapon per class) with weapon specials, plus 2 armors with Water/Energy resists.
- [ ] Bogmother drops the 3 Water weapons at 15% each (boss Sigil odds).
- [ ] Stormcoil drops 3 tier-3 Energy weapons at 15% each; the quest gives your class's one.

## Code (PR 2)

- [ ] Boss phase: `phase` on `MonsterDef` (HP threshold, new element/resists, message).
- [ ] Exit blockers other than thorns (barricade, reeds), per-boss intro text.
- [ ] `marsh` procedural backdrop until the art lands.
- [ ] Title shows v0.7 · Chapter 2. Sim rows for every new monster and boss.
- [ ] No `SAVE_VERSION` bump: quest state is keyed by id, Elites aren't saved.

## Pacing

Chapter 1 ends around level 5-6 (estimate from the XP curve). Level 6 to 10 is 4,140 XP; Chapter 2 monsters give about 2.5-3x Chapter 1 XP, so roughly 50 fights plus quests, close to Chapter 1's length.

Sim targets (level-appropriate tier-2 shop gear, full HP, 1 potion): trash wins 100% with 55-75% HP left in 4-6 turns. Bosses (3 potions): 85-100% win at their level.

## Art (Higgsfield, free mode)

Prompts go in `art/PROMPTS.md`. The game runs before any of it lands (drawn-shape stand-ins).

- 5 backgrounds: `bg_fen_road`, `bg_reedwatch`, `bg_sunken_causeway`, `bg_bog_hollow`, `bg_drowned_shrine`
- 7 monsters: `mon_mire_toad`, `mon_spark_wisp`, `mon_bog_lurker`, `mon_sunken_revenant`, `mon_stormscale_drakeling`, `mon_bogmother`, `mon_stormcoil`
- 3 NPCs: `npc_captain_hale`, `npc_wren`, `npc_ottilie`
- 12 weapons: 3 shop, 6 elemental drops, 3 Stormcoil

## Later

- D: player marketplace (needs a backend). E: Alchemy after more chapters.
- Ideas: mobile touch controls, itch.io release, armor/potion icon art.
