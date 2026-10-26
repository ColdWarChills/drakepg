# Build variety spec: skill books, element families, spellbook

**Decided 2026-10-26, being built.** Built from the decisions in `docs/PLANS.md` (2026-10-26); the two open calls were settled with the design advisor (designer: "consult with designer and do what's best here"), see "Advisor review" at the bottom. Numbers are starting points: `npm run sim` and `npm run pace` decide the final ones (all in `BALANCE` or `src/data/`). Names are placeholders.

Goal: two heroes of the same class can play differently. Today every Warrior learns the same 12 skills at the same levels.

## In one picture

- Levels teach each class **6 core skills**, all by level 9.
- Every other skill is a **skill book**. One rule for where: **towns sell element books, story bosses teach class skills** (every class gets one at each of the first four bosses), and Elites, guild promotions, Heroic wins and sure chests are extras.
- **Element families**: each class has one skill shape in all 8 elements (Fire Knives, Ice Knives...). Each element adds its own effect, and the skill hits 20% harder when your weapon has the same element.
- **Generic books** any class can read: Play Dead, Meditate, Field Dressing and 8 element Wards.
- A **spellbook** shows every skill your class can get: known ones in color, the rest dark with a hint where to find them.
- You still carry 5 skills into battle. Learning costs gold (shop books) and a slot.

## Level skills (6 per class)

The draft from PLANS. The level-1 skills stay (the gate guards are built around them).

| Class | Level skills |
| --- | --- |
| Warrior | Heavy Strike 1, Second Wind 2, War Cry 3, Skull Splitter 4, Sunder 6 (was 8), Cleave 9 (was 12) |
| Mage | Light Bolt 1, Staff Bolt 2, Mend 2, Frost Lance 3, Spark Chain 5 (was 6), Stone Skin 7 (was 8) |
| Rogue | Gas Shot 1, Twin Cut 2, Bandage 2, Flurry 3, Assassinate 5, Smoke Bomb 7 |

After level 9, levels give stat points only. New skills come from books.

## Skill books

- A book is a bag item (`type: 'book'`, one per skill, generated from the skill data like gear lines). It stacks and takes a bag slot.
- **Read it:** right-click in the bag (double-click works too, like potions). You learn the skill, the book is used up, and the "New!" popup shows it like a level-up skill. It goes into a free battle slot if there is one. Not in battle.
- **Read level:** every book needs a level. Element books follow the story (below); other books use the level of the place they come from. Too low: "Needs level 7".
- **Who can read it:** class books only teach their class. Shops only show books your class can read. Generic books teach anyone.
- **A book you already know:** sell it. (Shelving it in the library comes with the shared box, later.)
- Elites never drop a book you already know or hold.

### Element book levels (follow the story)

| Read level | Elements | Why there |
| --- | --- | --- |
| 3 (Chapter 1) | Fire, Earth, Wind | Thornwood beasts burn and hate wind; the Thornheart is Earth (an Earth Ward helps). Fire also cracks Old Snowmaw later, Earth the Frostbinder's Wind half |
| 7 (Chapter 2) | Water, Energy | The Fen's Water and Energy monsters beat each other (Bogmother, Stormcoil) |
| 11 (Chapter 3) | Ice | The Fire monsters of the Steamvent Caves |
| 16 (Chapter 4 on) | Light, Dark | No source yet: spellbook hint "Later in the story" |

### Price

`BALANCE.bookPrice`: 20 gold x read level; class books x2 (they aren't sold, but a known one sells like any item).

- Chapter 1: element books 60 gold (half a shop sword).
- Chapter 2: element books 140.
- Chapter 3: Ice books 220.

Pace check: a player who buys their class's three Chapter 1 element books still affords a Brindlemoor weapon.

## Element families

One shape per class, 8 elements each. Damage type follows the weapon like other Warrior and Rogue skills, so Rogue knives scale on DEX; Mage bolts are magic (INT).

| Class | Shape | Example | Damage | MP | Cooldown | Effect chance |
| --- | --- | --- | --- | --- | --- | --- |
| Warrior | Strike | Fire Strike | 1.5x | 6 | 1 | 30% |
| Rogue | Knives (thrown) | Fire Knives | 3 x 0.5x | 6 | 1 | 30% |
| Mage | Bolt | Fire Bolt | 1.5x | 6 | 0 | 25% |

- Close to each class's first skill (Heavy Strike 1.6x, Twin Cut 2 x 0.8x, Light Bolt 1.5x): a sidegrade that brings an element, not a stronger skill.
- **Match bonus:** +20% damage when your weapon has the skill's element (`BALANCE.elementMatch`). The skill says it: "+20% with a Fire weapon". In battle the button's element icon lights up when it's on.
- **Light Bolt joins the Mage family:** it keeps its numbers and level 1 and gains the Light effect and the match bonus.
- No new animations: Strike uses the weapon blow, Knives reuse Fan of Knives' throw, Bolt the bolt.

### One effect per element

Built from existing statuses, so every element is useful in its own way, not just damage. Each role appears once: damage over time, skip a turn, heal, amplify, guard, weaken, crit, strip resists.

| Element | Effect | Status |
| --- | --- | --- |
| Fire | Burns for 3 turns | Burn (exists) |
| Ice | Freezes: skips its next turn | Freeze (new, see below) |
| Water | Regen on you for 2 turns | Regen (exists) |
| Energy | Takes 15% more damage for 2 turns | Shock (exists) |
| Earth | Stone Skin on you for 1 turn | Stone Skin (exists) |
| Wind | Deals 25% less damage for 2 turns | Weaken (exists) |
| Light | +25% crit chance on you for 2 turns | Focus (exists) |
| Dark | Shadow Rot for 3 turns | Shadow Rot (exists) |

### Freeze (the designer's Ice idea)

- A stun with an ice look: the monster skips its next turn. Bosses ignore it, like stun.
- Chance: the family's effect chance (25-30%), +1% per 4 points of the stat the skill uses, cut by the target's Ice resist, at most 40%. Examples: a Mage with 30 INT freezes a neutral monster 32% of the time, a Glacier Golem (Ice +75) 8%.
- Never twice in a row: a thawed monster shows a **Thawed** chip ("can't be frozen this turn"). Without that, chained freezes make normal fights trivial.
- The chance is on the skill's card.

## Generic books (any class)

| Book | What it does | MP | Cooldown | Source |
| --- | --- | --- | --- | --- |
| Field Dressing | Heal 12% of max HP (weaker than the class heals) | 4 | 3 | Brindlemoor shop, level 2 |
| Play Dead | A sure flee from any fight you can flee | 3 | 0 | Bramble Hideout's sure chest, level 3 |
| Meditate | Restore 25% of max MP; no attack this turn | 0 | 4 | Eel Pools' sure chest, level 7 |
| Fire Ward ... Dark Ward | +40 resist to that element for 3 turns | 6 | 5 | Town shops, at that element's read level |

- Meditate is the generic MP skill (the name Focus is taken by the +25% crit status).
- Wards fit the telegraphs: when a monster winds up a big hit of an element, cast that Ward first.
- Not picked: the Blunt sling stone and the Poison dart (Blunt and Poison stay class answers, plus the weapon swap).

## Where every book comes from

One rule the player can learn: **towns sell element books, story bosses teach class skills, the rest are extras.** Strong skills only come from sure sources (bosses, guild promotions, Heroic wins, sure chests, the class quest), never from a random spawn.

**Shops** (only your class's books are shown):

| Town | Books |
| --- | --- |
| Brindlemoor (Tobin) | Fire, Earth, Wind family books; Fire, Earth, Wind Wards; Field Dressing |
| Reedwatch (Wren) | Water, Energy family books and Wards |
| Hearthold (Gunnar) | Ice family book; Ice Ward |

**Story bosses** (first defeat, as the quest reward; rematches don't give it again). Read level = the boss's level:

| Boss | Warrior | Mage | Rogue |
| --- | --- | --- | --- |
| The Thornheart (5) | Shield Bash | Meteor | Expose Weakness |
| The Bogmother (8) | Iron Skin | Sunburst | Envenom |
| The Stormcoil (10) | Bloodlust | Tidal Wave | Shadowstep |
| Old Snowmaw (12) | Earthshaker (Earth for the Frostbinder) | Gale | Hemorrhage |

The Frostbinder ends the content for now, so she keeps today's rewards.

**Extras:**

| Source | Warrior | Mage | Rogue |
| --- | --- | --- | --- |
| Guild promotion | Rallying Roar (Rank C, 11) | Thunderstorm (Rank C, 11) | Fan of Knives (Rank D, 7) |
| First Heroic win (6) | (none yet) | Shadow Hex | Death Mark |
| Class quest at level 20 (later) | Titan's Wrath | Cataclysm | Thousand Cuts |

- **Elites drop their area's element books:** your class's family book or a Ward of an element their area's shop sells (Brindlemoor's three for Chapter 1 areas, and so on). 30% per Elite kill while one is missing, sure by the 4th Elite without one (`BALANCE.bookDrop`, wrapped in `boosted()` so the cheat rates reach it). A lucky find saves the gold.
- Boss books come through the quest reward (`classItems`, which then takes a list).
- Level-20 books stay out of reach until the class quest exists (content ends around level 17).

## Spellbook

Replaces the skill list in the Character panel. That spot keeps your 5 carried skills and gets a **Spellbook** button; the spellbook also opens with **K**.

- An open book, two pages, felt style. Tabs down the edge: **All**, **Weapon** (no fixed element), then one per element (HoMM3-like).
- Left page: rows with icon, name, MP and cooldown. Right page: the picked skill's card: description, element effect, match bonus, where it came from.
- **Known:** in color; a filled diamond means carried. Click to carry or put away, like today.
- **Not known yet:** dark row, name shown, with a hint: "Lv 9", "Sold in Brindlemoor", "The Thornheart's first defeat", "Elites in Mirewater Fen", "Guild promotion to Rank C", "Bramble Hideout's chest", "First Heroic win", "Catch a Lucky Impling", "Class quest at level 20", "Later in the story".
- Hints are built from the data (shops, quest rewards, promotions, loot tables, chests), never hand-written, so moving a book fixes its hint.
- Only your class's skills and generic ones: about 30 rows per class. Other classes' books wait for the shared box.

## Icons

- Family skills: one icon per shape (strike, knives, bolt) tinted by element: 3 drawings, not 23.
- Wards: one shield icon tinted by element. Books in the bag: one book icon with the skill's icon on the cover.
- New drawings: 3 shapes, 1 ward, 1 book, Play Dead, Meditate, Field Dressing, Freeze (ComfyUI + Qwen, felt style, like the 58 skill icons).

## Save and unlock

- Save v16: `PlayerData.learned` (skills learned from books) and `bookPity` (Elite kills without the book, per area).
- **Nobody loses a skill:** `migrate()` gives existing characters every skill the old level table had taught them by their level.
- Feature `skill_books` (`src/data/features.ts`), unlocked by Chapter 1's first quest; the "New!" popup announces it before the first shop visit.

## How it's built

1. **Data:** `skills.ts` (families made from a shape table x elements; generic skills), `statuses.ts` (Freeze, Thawed, 8 Wards), books made in `items.ts`, `classes.ts` (6 level skills plus the class's book list), shop stocks in `world.ts`, quest rewards, `PROMOTIONS`, loot tables (`books` per class), sure chests, `BALANCE` (book levels, prices, match bonus, effect chances, freeze, book drops). Then `npm run export-data`.
2. **Rules in both versions** (TypeScript for the sim and golden tests, C# for the game): known skills = level skills + learned; reading a book; the match bonus; Freeze and Thawed; Play Dead; Meditate; book drops and pity; spellbook hints. Regenerate the golden tests (`npm run golden`).
3. **Sim and pace checks** (the checks proposed in PLANS): sim heroes buy books when they reach the level and pick up the sure ones. Report skill use per class (never or always picked = dead or too strong), wins per element per chapter (no element best everywhere), every boss winnable with level skills plus the sure books before it (above all the Mage's Thornheart without Meteor), turns per fight, gold left after books.
4. **Unity UI:** read from the bag (right-click, the first one in the game), level tag in the shop, the spellbook, the lit element icon on battle buttons, the Freeze and Thawed chips, icons.

Steps 1-3 can be checked headless; step 4 is the playtest.

## Not in this build

The shared box and library (and its bonuses), class quests, advanced classes, the off-hand slot, level-20 skills, Light and Dark sources.

## Advisor review (2026-10-26)

The designer handed the two open calls to the design advisor.

- **Element effects: build them.** Without them, 8 family skills are one skill in 8 colors; options that do the same thing add complexity without interest (Sirlin, balancing articles) [designer opinion]. Fixed one overlap: Wind (Weaken), Light (Blind) and Earth (Stone Skin) all meant "you take less damage", so Light became Focus (+25% crit on you).
- **Freeze: lower and capped at 40%.** Mage bolts have no cooldown, so a 37-50% skip-a-turn on the cheapest skill is the dominant strategy players find and keep (Soren Johnson, "water finds a crack") [designer opinion]. Ice monsters resist it, which already limits it in Chapter 3.
- **Bosses teach every class.** Bigger gaps need bigger rewards, landed on milestones (Mike Stout, reward schedules) [designer opinion], and a new skill is the reward that changes how you play (leveling-systems critique) [article]. The draft gave each boss's book to one class, the Rogue no class book in Chapter 3 and the Mage none in shops. The town/boss/extras rule also makes sources easy to remember (Sylvester's player model principle) [designer opinion].
- **No book from the Lucky Impling.** It's a rare random spawn, against "strong skills from sure sources"; Thunderstorm moved to the Rank C promotion.
- **Watch in the sim:** Ward use (Defend already answers telegraphed hits), Play Dead use (a sure flee may be a trash option), and whether levels 10-19 still feel empty between the boss books.
