# Chapter 3 spec: The Frozen Roost

**Draft.** Direction chosen 2026-10-13: frozen north; Boss grit, Back exits and Collect quests; the dragon is seen asleep. Numbers are starting points: `npm run sim` decides the final ones (all live in `BALANCE` or `src/data/`). Names are placeholders.

Ships in two PRs: **1. Systems** (boss grit, back exits, collect quests; they also help Chapters 1-2), **2. Chapter 3 content**.

## Story

Maren turns the Stormcoil's dull scale over in her hands. A dragon's scale should be warm; this one is cold as river ice, and frost creeps over it at night. The cold comes from the north, from the Frostfang peaks behind Brindlemoor, where the old dragon was last seen flying home.

The mountain folk of Hearthold say winter came early and never left. A sorceress, the **Frostbinder**, has sealed the sleeping dragon in ice at its roost and draws on its strength to keep the peaks frozen. Beat her and the ice cracks: the dragon opens one eye, looks at you, and goes back to sleep. Teaser for Chapter 4: why did it come home to sleep, and who taught the Frostbinder that spell?

Tone: short, a little funny, loop first (see the AQ lessons: lunch-break quests, a few lines of text).

## Map

Single-screen rooms. Chapter 3 goes **north**: Brindlemoor gets a **back exit** (the mountain road gate in the background), the first use of the new back exits.

| Room | Level | Notes |
| --- | --- | --- |
| Brindlemoor | | New back exit: the north gate, snowed shut until quest 1 is accepted |
| Frostfang Pass | 10-11 | Drift Wolves, Crag Rams |
| Hearthold | | Mountain village: Ranger Sabine (quests), Gunnar (shop), Hearthkeeper Moll (inn). Hulda's forge is two rooms back in Brindlemoor |
| Windscour Ridge | 11-13 | Gust Sprites, Rime Acolytes, **Glacier Golem** (tough). A cave mouth in the back leads to the Steamvent Caves |
| Steamvent Caves | 12 | Side cave warmed by hot springs: Ember Salamanders, **Vent Wyrmling** (tough). The source of Fire Essence |
| Snowmaw's Den | 12 | Mini-boss: Old Snowmaw. Blocked by snowdrifts until quest 5 |
| The Frozen Roost | 14 | Final boss: the Frostbinder, with the dragon frozen in a wall of ice behind her. Blocked until quest 6 |

## Elements

Ice and Wind on the mountain, Fire in the caves. Two loops that feed each other:

- Mountain monsters drop **Ice Essence** -> craft Ice weapons -> beat the Fire monsters in the Steamvent Caves.
- Cave monsters drop **Fire Essence** -> craft Fire weapons -> beat the Glacier Golem, Snowmaw and the Frostbinder.

Older gear stays useful: Chapter 2's Energy weapons crack the Glacier Golem, and Chapter 1's Earth weapons (Thornheart drops) answer the Frostbinder's Wind phase.

## Monsters

Following the difficulty rule: most monsters are easy, neutral-ish and weak to several things; two are **Tough** with one or two big weaknesses that this chapter gives you a counter for.

| Monster | Lv | Element | Type | Hook |
| --- | --- | --- | --- | --- |
| Drift Wolf | 10 | none | melee | weak Fire, Earth, Energy; Howl buffs its next bite |
| Crag Ram | 10 | none | melee | weak Fire, Wind; **Headlong Charge** telegraphed a turn ahead |
| Gust Sprite | 11 | Wind | magic | evasive; weak Earth, Ice |
| Rime Acolyte | 12 | Ice | magic | Frostbinder cultist; heals itself once at low HP; weak Fire, Light |
| **Glacier Golem** (Tough) | 12 | Ice | melee | steel barely scratches it, resists Ice, Water, Earth; **weak Fire -75, Energy -50** (lightning shatters ice) |
| Ember Salamander | 12 | Fire | pierce | cave trash, drops Fire Essence; weak Ice, Water |
| **Vent Wyrmling** (Tough) | 13 | Fire | pierce | cave; Scald may Burn, resists Fire, Earth, Wind; **weak Ice -75, Water -50** |
| **Old Snowmaw** (mini-boss) | 12 | Ice | | huge shaggy mountain beast. **Frost Shell**: takes almost no damage until 3 Fire hits break it (shown as shell pips on the panel); re-forms once at 50% HP. **Avalanche** telegraphed |
| **The Frostbinder** (boss) | 14 | Ice | | **Glacial Prison** telegraphed (Defend!). **At 50% HP she draws on the dragon and becomes a Wind blizzard** (resists flip, like the Stormcoil): bring Fire and Earth |

Bosses stay for rematches (half XP/gold, boss loot odds). Elites work everywhere as before.

## Quests

1. **Cold Scale** (Elder Maren): 4 Drift Wolves. Accepting opens the north gate.
2. **Hearthold Holds** (Ranger Sabine): 4 Crag Rams.
3. **Fire for the Hearth** (Hearthkeeper Moll): *bring* 5 Fire Essence (first collect quest; sends you into the Steamvent Caves).
4. **Thaw** (Ranger Sabine): 2 Glacier Golems.
5. **The Old One in the Den** (Ranger Sabine): Old Snowmaw. Opens the Roost.
6. **The Frozen Roost** (Ranger Sabine): the Frostbinder. Class reward, ends Chapter 3.

## Gear

Tiers stay about x1.5 apart; items in one tier are sidegrades.

- Hearthold shop, tier 3 Standard: 3 weapons (one per class), 2 armors (one heavy, one robe), Superior potions.
- Loot table `frostfang`: common = the shop models; uncommon = 3 Ice weapons + 3 Fire weapons + 2 armors (Ice resist, Wind resist); rare = the Frostbinder's 3 weapons (tier 4).
- Loot table `snowmaw`: rare = 3 Ice weapons of its own (one per class).
- New materials: Frost Pelt (wolf), Ram Horn (ram), Rime Shard (golem), Salamander Scale (salamander, wyrmling). Hulda gets recipes for the Ice and Fire weapons and both armors.

## New systems (PR 1)

- **Boss grit** (from the AQ review's "copy" list): every loss to a boss adds +5% damage against that boss, up to +25%, shown on the battle panel ("Grit +10%"). Saved per boss; resets when you win. Works for the Thornheart, Bogmother and Stormcoil too.
- **Back exits**: a door, gate or cave mouth in the room art (`side: 'back'` with an x position and a label). Walk up to it and press E (or tap it). Can be blocked by a quest like side exits (new blocker: snowdrift).
- **Collect quests**: `objective.kind: 'collect'` (bring N of an item; taken on turn-in). The quest tracker and NPC reminders show "3/5", counting what is in the bag.
- Save version bump for grit counters.

## Pacing

Chapter 2 ends around level 10. Level 10 to 14 is about 7,800 XP; Chapter 3 monsters give about 1.5x Chapter 2 XP, so roughly 45 fights plus quests, close to Chapter 2's length. Skills that arrive in this range: Iron Skin, Cleave, Bloodlust / Gale, Tidal Wave, Sunburst / Shadowstep, Fan of Knives.

Sim targets (level-appropriate tier-3 shop gear, full HP, 1 potion): trash wins 100% with 55-75% HP left in 4-6 turns. Tough monsters: with the counter 4-5 turns, without it about twice as long and half the HP. Bosses (3 potions): 85-100% at their level; Snowmaw noticeably faster with Fire. New sim rows: every Chapter 3 monster, both bosses with shop / Ice / Fire / Earth gear, and boss grit at +0/+15/+25%.

## Art (Higgsfield, free mode)

About 31 images; the game runs before any of it lands. Prompts get added to `art/QUEUE.md`.

- 6 backgrounds: Frostfang Pass, Hearthold, Windscour Ridge, Steamvent Caves, Snowmaw's Den, the Frozen Roost (dragon frozen in ice behind)
- Brindlemoor background: a version with a visible north gate in the back (or the gate is drawn over the current art)
- 9 monsters, 3 NPCs
- 15 weapons: 3 shop, 6 Ice/Fire, 3 Snowmaw, 3 Frostbinder

## Open questions

- The dragon's name (it stays unnamed until it wakes in Chapter 4?).
- Does Hearthold get its own smith later, or is Hulda two rooms back enough?
