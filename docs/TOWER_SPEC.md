# Tower spec: harder floors, first-clear rewards, a vendor

**Built 2026-10-27** (rules in both versions, UI in Unity), for the designer's playtest. From the designer's proposal in `docs/PLANS.md` ("Harder dungeons: a tower", like Summoners War's Trial of Ascension) and the defaults they picked: fresh HP per floor, first-clear rewards, a twist on every floor, a tower-currency vendor. The floor list, levels and scaling below are the built ones (tuned with `npm run sim` and the Unity playthrough bot; see "What the checks said"). Names are placeholders.

Goal: hard content to gear up for. The playthrough bot (2026-10-27) found story bosses fall on the first try, drops beat every shop piece after Chapter 1, and gold piles up (7-9k by the end of Chapter 3). The tower is the exam; zones, Elites and Heroic rematches stay the farm.

## In one picture

- **The Spire** (placeholder name), a tower behind Brindlemoor. **30 floors in 3 flights of 10**; each flight opens after a chapter's last boss and is a few levels above it.
- **Each floor is one fight**: a monster you know, scaled up, plus **one or more twists** shown before you go in ("Unshakable", "No potions", "Short fuse"...). Floor 10, 20 and 30 are Heroic chapter bosses; 15 and 25 are Heroic mini-bosses.
- **Fresh start:** every attempt starts at full HP and MP; afterwards you're back at the HP you came in with. **Free retries**, no penalty for losing.
- **Rewards on the first clear only:** tower marks (a currency), more on every 5th and 10th floor, plus a stone bundle on floors 5/15/25 and a Sigil gear piece on 10/20/30. No XP, gold or drops inside the tower.
- **The Warden's vendor** sells chapter set pieces (you pick the piece, rolled like a boss drop), Sigil weapon caches and the Light and Dark books for **marks plus gold**: the gold sink the playthrough asked for.
- Unlocks with the Thornheart's defeat, like Elites and chests ("New!" popup).

## Where it is

- A new quiet room, **Spire Foot**, through a back exit in Brindlemoor (a door in the town art, like the Fen and Frostfang exits). The **Warden** stands there: "Climb" opens the floor list, "Trade" opens the vendor.
- The door is the cottage door at the left of Brindlemoor's art (no gate: before the Thornheart falls the room is there, the Warden isn't).
- Art: the room background and the Warden (local Qwen, round-15 look). Battles use the flight's area background (Thornwood, Fen, Frostfang); a tower-interior battle background is a later option.

## Flights and floors

| Flight | Floors | Opens after | Floor levels | Hero level when it opens (playthrough) |
| --- | --- | --- | --- | --- |
| Thornwood | 1-10 | The Thornheart | 6-12 | 5-6 |
| Fen | 11-20 | The Stormcoil | 12-21 | 11-12 |
| Frostfang | 21-30 | The Frostbinder | 17-24 | 16-17 |

The first floors of a flight are a step up from the chapter; its last ones need a farming trip. Levelling past an old flight makes it easier, like any old zone (accepted: early floors become quick marks). Levels are not in order: a Tough monster is hard at a lower level (an Ash Imp at 7 is a wall, a Spark Wisp at 10 is not).

**Scaling:** a floor monster keeps its strength against monsters of its own level. HP and damage are multiplied by `curve[floor level] / curve[its level]`, where the curve is a normal monster's HP and average hit per level, fit from the monster list (`BALANCE.tower.curve`; past level 12, where no monsters exist yet, +10% HP and +7% damage a level). Defense +0.5 per level. (The draft's flat +8%/+4% per level left floors trivial; one compound rate made level-8 monsters moved to 15 into 790-HP sponges: the monster list's own curve is steep up to level 8 and flat after.) Bosses use the Heroic version (`makeHeroic`: extra move, x1.25 damage) moved to the floor's level.

### Floors (built)

The draft used the gate guards (Millstone Crab, Dusk Wraith, Hedge Troll), but each is weak to one class's first skill only, so their floors were one-class floors (the Wraith: Light, which only the Mage has then). Flight 1 now takes two Fen monsters as a preview of Chapter 2 instead.

| Floor | Monster | Lv | Twists | Question it asks |
| --- | --- | --- | --- | --- |
| 1 | Mossling | 6 | Frenzied | Warm-up |
| 2 | Thornback Boar | 6 | Short Fuse | Kill it fast |
| 3 | Bramble Bandit | 7 | Hulking | Sustain |
| 4 | Mire Toad | 7 | Venomous | Poison it first, or outlast it |
| 5 | Barkshell Beetle (Tough) | 7 | Warded | Fire or Wind through +25 resists |
| 6 | Spark Wisp | 10 | Wind Veil | Bring Wind |
| 7 | Ash Imp (Tough) | 7 | Fury | Ice, Water, Blunt or Poison |
| 8 | Bramble Bandit | 10 | No Potions | Heals and Wards instead of potions |
| 9 | Ash Imp (Tough) | 8 | Unshakable, Short Fuse | No stun; burst it |
| 10 | Heroic Thornheart | 12 | Hardened | Boss with Iron Skin |
| 11 | Mire Toad | 12 | Hulking | Sustain |
| 12 | Spark Wisp | 13 | Ambush: Shock | Start hurt |
| 13 | Bog Lurker | 13 | Ironhide | Magic or Sunder |
| 14 | Sunken Revenant | 14 | Light Veil | Bring Light |
| 15 | Heroic Bogmother | 16 | No Potions | Mini-boss on your own heals |
| 16 | Volt Eel (Tough) | 18 | Unshakable, Frenzied | No stun, hits hard |
| 17 | Stormcrest Heron (Tough) | 17 | Water Shell | Water hits to crack it |
| 18 | Spark Wisp | 18 | Short Fuse, Venomous | Race |
| 19 | Sunken Revenant | 16 | Regrowth, Warded | Out-damage its Regen |
| 20 | Heroic Stormcoil | 21 | Fury | Boss with War Cry |
| 21 | Drift Wolf | 17 | Frenzied | Take a hit |
| 22 | Crag Ram | 18 | Ironhide | Magic or Sunder |
| 23 | Gust Sprite | 18 | Earth Veil | Bring Earth |
| 24 | Ember Salamander | 19 | Ambush: Burn | Start burning |
| 25 | Heroic Old Snowmaw | 19 | Regrowth | Fire through the shell, fast |
| 26 | Glacier Golem (Tough) | 21 | Unshakable, Hulking | Long fight without stuns |
| 27 | Rime Acolyte | 22 | No Potions, Warded | Sustain without potions |
| 28 | Vent Tortoise (Tough) | 21 | Ice Shell, Frenzied | Ice hits first |
| 29 | Drift Wolf | 24 | Short Fuse, Venomous, Fury | Everything at once |
| 30 | Heroic Frostbinder | 23 | Hardened, No Potions | The final exam |

## Twists

Data (`TWISTS` in `src/data/tower.ts`), each with a name and one line of text. Shown as chips on the floor card and in battle under the monster's "Weak:" line (hover for the text), so nothing is hidden.

| Twist | Effect | Built from |
| --- | --- | --- |
| Hulking, Frenzied, Warded, Venomous, Ironhide | The Elite affixes | Existing affixes |
| Unshakable | Immune to stun and Freeze | Existing `immune` |
| Veil (element) | Every element is resisted 50 more, except one, which is resisted 25 less (on top of its own resists) | Existing resists |
| Shell (element, hits) | Takes half damage until hit N times by that element; grows back at half HP | Old Snowmaw's existing shell |
| Fury / Hardened / Regrowth | Starts the fight with War Cry / Iron Skin / Regen | Existing statuses; **new:** starting statuses |
| Ambush (status) | You start with Shock or Burn | Existing statuses; **new:** starting statuses |
| Short fuse | Enrages from turn 6 (normally 15, bosses 30) | Existing enrage; **new:** per-fight enrage turn |
| No potions | Items can't be used in this fight | **New:** a fight with items off |

## Rewards (first clear only)

| | Thornwood flight | Fen flight | Frostfang flight |
| --- | --- | --- | --- |
| Marks per floor | 10 | 20 | 30 |
| Floor 5/15/25 | x2 marks + 3 of each roll stone | x2 + 3 of each | x2 + 3 of each |
| Floor 10/20/30 | x3 marks + a Dawn Sigil piece (Thornwood table) | x3 + a Dawn Sigil piece (Fen table) | x3 + a Dusk Sigil piece (Frostfang table) |
| Marks per flight | 130 | 260 | 390 |

- Cleared floors can be fought again for practice, without rewards (no farming the easiest floor).
- Marks are a number on the hero (`PlayerData.tower.marks`), not a bag item.

## The Warden's vendor (marks + gold)

Stock opens with your best floor. Gear is rolled like a boss drop (not shop 0%), so you pick the piece and still roll the numbers.

| Opens at floor | Item | Price |
| --- | --- | --- |
| 5 | Thornwarden set piece (any of the 4) | 25 marks + 300 g |
| 10 | Thornwood Sigil cache (a random Thornwood weapon, Dawn Sigil) | 60 marks + 500 g |
| 15 | Stormwatch set piece | 40 marks + 900 g |
| 20 | Fen Sigil cache (Dawn) | 90 marks + 1,500 g |
| 25 | Frostward set piece | 60 marks + 2,000 g |
| 25 | Light and Dark family books and Wards (your class's) | 30 marks + their gold price (320 g) |
| 30 | Frostfang Sigil cache (Dusk) | 120 marks + 3,000 g |

The gold side is sized from the playthrough's gold left at each chapter's end (~400 g, ~2-3k, ~7-9k): a full Frostward set from the vendor costs 8,000 g. The Light and Dark books get their first source this way (they have none today, read level 16).

## What the checks said (2026-10-27)

**Targets** (sim, smart bot): with what a careful player has when a flight opens, floors 1-4 90%+, 5-8 55-85%, 9 30-60%, the boss floor 20-45%; after farming (the area set and jewelry, Dawn Sigils, 75% rolls) 100% / 90%+ / 75%+ / 65%+. A floor nobody beats with farmed gear is a bug.

**Sim** (`npm run sim`, "Tower floors", win % Warrior / Mage / Rogue, opening gear -> farmed gear):

| Floor | Opening gear | Farmed gear |
| --- | --- | --- |
| 1-4 | 89-100% | 100% |
| 5 Barkshell Beetle | 86 / 97 / 61 | 100% |
| 7 Ash Imp | 29 / 93 / 16 | 99-100% |
| 9 Ash Imp | 13 / 39 / 4 | 93 / 100 / 95 |
| 10 Heroic Thornheart | 13 / 5 / 20 | 100% |
| 17 Stormcrest Heron | 8 / 23 / 69 | 97-100% |
| 19 Sunken Revenant | 13 / 31 / 14 | 99-100% |
| 20 Heroic Stormcoil | 1 / 0 / 14 | 97-99% |
| 26 Glacier Golem | 30 / 95 / 1 | 100% |
| 28 Vent Tortoise | 4 / 2 / 19 | 90 / 98 / 100 |
| 30 Heroic Frostbinder | 0 / 2 / 1 | 74 / 97 / 91 |

The random-button bot loses most top floors even with farmed gear (Frostbinder 28-56%): the twists reward reading them. Some floors are walls for one class (the Golem's armor for a Rogue, Light Veil for a Warrior): those are the farming targets.

**Unity playthrough bot** (6 heroes per class; after each chapter's boss it climbs, 3 tries a floor, farms the area twice when stuck, buys from the vendor):

| | After Chapter 1 | After Chapter 2 | After Chapter 3 |
| --- | --- | --- | --- |
| Best floor (median) | 9-10 (range 4-10) | 19-20 (13-20) | 30 (25-30) |
| Tower fights / losses | 14-20 / 4-11 | 16-18 / 6-9 | 14-21 / 4-10 |
| Farming trips | 1-2 | 1-2 | 1-2 |
| Gold at the vendor | ~50 | 2,000-2,500 | 2,900-3,600 |

The walls it hit most: the Heroic bosses, the Ash Imp floors (Warriors and Rogues), the Stormcrest Heron's Water Shell, the Vent Tortoise's Ice Shell. The vendor takes 2-3.5k gold a chapter from Chapter 2 on, but heroes still end Chapter 3 with 7-11k: the gold surplus (PLANS, "Playthrough") is eased, not solved. The bot plays carefully (heals first, swaps weapons, rerolls with stones), so real players will need more farming.

## Save, unlock, rules

- **Save v17:** `PlayerData.tower = { best, marks }` (best = highest floor cleared; floors are cleared in order). Migration: `{ best: 0, marks: 0 }`.
- **Feature** `tower` (`src/data/features.ts`), unlocked by the Thornheart quest; flights 2 and 3 also need the Stormcoil and Frostbinder quests done.
- **Rules** in both versions, like everything in `src/game`: `src/game/Tower.ts` / `Rules/Tower.cs`: `floorMonster(floor)` (scaled, twists applied), `floorOpen`, `clearFloor` (rewards), `vendorStock`. Battle gets three small options: starting statuses (either side), an enrage turn per fight, items off.
- **Data:** `src/data/tower.ts` (floors, twists, flights, vendor), numbers in `BALANCE.tower`.

## UI (Unity)

- **Floor list** (Warden, "Climb"): three flight tabs; locked ones say "Opens after The Stormcoil". Each floor is a card: number, monster picture and level, twist chips, first-clear reward, a check when cleared. The next floor is highlighted with an **Enter** button.
- **Battle:** a line per twist on the monster panel (with starting statuses as chips from the first moment), "Floor 7" next to the level, "FLOOR 10 · HEROIC BOSS" on the boss banner.
- **Result:** win: "Floor 7 cleared!", marks and items, then **Next floor** or **Back**. Loss: what beat you and the twists again (losses must say why), then **Retry** or **Back**.
- **Vendor** (the Trade tab): rows with the item, what it is, its price in marks and gold, and Buy.
- Phaser: no tower (like the spellbook).

## Checks done

- Golden tests: 12 tower floors per class (every kind of twist, Heroic bosses moved up) plus a climb through all 30 floors and vendor purchases: 1728 fights match TypeScript and C#.
- `npm run sim`: the "Tower floors" table (above).
- Playthrough bot (`Editor/Playthrough.cs`): climbs after each chapter (above).
- In Unity: the "New!" popup, Spire Foot and the Warden, the floor list, a floor fight with its twist line and starting-status chip, a first-clear win, a boss-floor win with its Sigil piece, a loss with Retry, the vendor.

## Not in this build

- **The Depths:** endless generated floors after 30 (seeded per floor, a best-depth record). The advisor's answer to "hand-made floors get used up"; next step if the tower works.
- Monthly resets, titles and cosmetics, leaderboards, a tower story.
- Multi-monster floors (fights are 1 vs 1).

## Advisor notes (2026-10-27)

- The tower is the exam, the zones are the farm (Summoners War: the Trial of Ascension tests gear farmed in the Cairos dungeons; from memory). So rewards feed the farm: set pieces you choose, Sigils, stones.
- Hand-made floors are used up once (Daniel Cook, loops vs arcs); the Depths are the loop, later.
- Each floor asks one clear question, shown before you go in; difficulty from twists and damage, not HP (Ghost of Tsushima's lethality contract).
- First-clear rewards only, so no "safest floor" to farm (players optimize the fun out: "water finds a crack").
- Fresh HP and free retries keep a loss cheap and readable; a run with HP carried between floors is harder to tune and can come later as a mode.
