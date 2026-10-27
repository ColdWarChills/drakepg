# Tower spec: harder floors, first-clear rewards, a vendor

**Draft 2026-10-27, for the designer's review before building.** From the designer's proposal in `docs/PLANS.md` ("Harder dungeons: a tower", like Summoners War's Trial of Ascension) and the defaults they picked: fresh HP per floor, first-clear rewards, a twist on every floor, a tower-currency vendor. Numbers are starting points: `npm run sim` and the Unity playthrough bot decide the final ones (all in `BALANCE` or `src/data/`). Names are placeholders.

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
- Art: one room background, the Warden, one tower-interior battle background (local Qwen, round-15 look). Until it exists: shapes and the monster's own zone background.

## Flights and floors

| Flight | Floors | Opens after | Floor levels | Hero level when it opens (playthrough) |
| --- | --- | --- | --- | --- |
| Thornwood | 1-10 | The Thornheart | 6-10 | 5-6 |
| Fen | 11-20 | The Stormcoil | 12-16 | 10-13 |
| Frostfang | 21-30 | The Frostbinder | 17-21 | 14-17 |

A flight's first floors are a step up from the chapter; its last ones need farmed gear. Levelling past an old flight makes it easier, like any old zone (accepted: early floors become quick marks).

**Scaling (no HP sponges):** a floor monster is its normal self moved up to the floor's level: damage +8% per level, HP +4% per level, defenses +1 per level (`BALANCE.tower.perLevel`). The difficulty comes from twists and damage, not long fights (the advisor's "lethality contract" note). Bosses use the Heroic version (`makeHeroic`: extra move, x1.25 damage) moved to the floor's level.

### Floors (draft)

One twist per floor up to floor 8, two from floor 9, three on 29.

| Floor | Monster | Lv | Twists | Question it asks |
| --- | --- | --- | --- | --- |
| 1 | Bramble Bandit | 6 | Frenzied | Can you take a hit? |
| 2 | Thornback Boar | 6 | Short fuse | Kill it fast |
| 3 | Ash Imp | 7 | Fury | Ice, Water, Blunt or Poison |
| 4 | Mossling | 7 | Hulking | Sustain |
| 5 | Barkshell Beetle | 8 | Warded | Fire or Wind through +25 resists |
| 6 | Hedge Troll | 8 | Venomous | Poison it before it poisons you |
| 7 | Millstone Crab | 9 | Veil (Wind) | Bring Wind |
| 8 | Dusk Wraith | 9 | No potions | Heals and Wards instead |
| 9 | Ash Imp | 10 | Unshakable, Short fuse | No stun; burst |
| 10 | Heroic Thornheart | 10 | Hardened | Boss with Iron Skin |
| 11 | Mire Toad | 12 | Hulking | Sustain |
| 12 | Spark Wisp | 12 | Ambush (Shock) | Start hurt |
| 13 | Bog Lurker | 13 | Ironhide | Magic or Sunder |
| 14 | Sunken Revenant | 13 | Veil (Light) | Bring Light |
| 15 | Heroic Bogmother | 14 | No potions | Mini-boss on your own heals |
| 16 | Volt Eel | 14 | Unshakable, Frenzied | No stun, hits hard |
| 17 | Stormscale Drakeling | 15 | Shell (Water, 3 hits) | Water hits to crack it |
| 18 | Spark Wisp | 15 | Short fuse, Venomous | Race |
| 19 | Sunken Revenant | 16 | Regrowth, Warded | Out-damage its Regen |
| 20 | Heroic Stormcoil | 16 | Fury | Boss with War Cry |
| 21 | Drift Wolf | 17 | Frenzied | Take a hit |
| 22 | Crag Ram | 17 | Ironhide | Magic or Sunder |
| 23 | Gust Sprite | 18 | Veil (Earth) | Bring Earth |
| 24 | Ember Salamander | 18 | Ambush (Burn) | Start burning |
| 25 | Heroic Old Snowmaw | 19 | Regrowth | Fire through the shell, fast |
| 26 | Glacier Golem | 19 | Unshakable, Hulking | Long fight without stuns |
| 27 | Rime Acolyte | 20 | No potions, Warded | Sustain without potions |
| 28 | Vent Wyrmling | 20 | Shell (Ice, 3 hits), Frenzied | Ice hits first |
| 29 | Drift Wolf | 21 | Short fuse, Venomous, Fury | Everything at once |
| 30 | Heroic Frostbinder | 21 | Hardened, No potions | The final exam |

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

## Difficulty targets (sim, smart bot)

| Kit | Floors 1-4 | 5-8 | 9 | Boss floor |
| --- | --- | --- | --- | --- |
| What a careful player has at the flight's opening (the playthrough bot's gear) | 90%+ | 55-85% | 30-60% | 20-45% |
| After farming: the area set, a Dawn Sigil weapon, 75%+ rolls | 100% | 90%+ | 75%+ | 65%+ |

The random-button bot should lose most boss floors even after farming: twists reward reading them. Each floor's right counter (element, Ward, spare weapon) should move its win rate clearly; a floor no class can beat with farmed gear is a bug.

## Save, unlock, rules

- **Save v17:** `PlayerData.tower = { best, marks }` (best = highest floor cleared; floors are cleared in order). Migration: `{ best: 0, marks: 0 }`.
- **Feature** `tower` (`src/data/features.ts`), unlocked by the Thornheart quest; flights 2 and 3 also need the Stormcoil and Frostbinder quests done.
- **Rules** in both versions, like everything in `src/game`: `src/game/Tower.ts` / `Rules/Tower.cs`: `floorMonster(floor)` (scaled, twists applied), `floorOpen`, `clearFloor` (rewards), `vendorStock`. Battle gets three small options: starting statuses (either side), an enrage turn per fight, items off.
- **Data:** `src/data/tower.ts` (floors, twists, flights, vendor), numbers in `BALANCE.tower`.

## UI (Unity)

- **Floor list** (Warden, "Climb"): three flight tabs; locked ones say "Opens after The Stormcoil". Each floor is a card: number, monster picture and level, twist chips, first-clear reward, a check when cleared. The next floor is highlighted with an **Enter** button.
- **Battle:** twist chips on the monster panel; "Floor 7" in the boss-style name banner.
- **Result:** win: "Floor 7 cleared!", marks and items, then **Next floor** or **Back**. Loss: what beat you and the twists again (losses must say why), then **Retry** or **Back**.
- **Vendor** ("Trade"): the shop panel with prices in marks and gold.
- Phaser: no tower (like the spellbook).

## Checks before calling it done

- Golden tests: a tower fight per twist kind (1296 fights still match).
- `npm run sim`: a "Tower floors" table per class and kit (opening kit, farmed kit), smart and random bot.
- Playthrough bot: after each chapter's boss it climbs its flight with what it has, farms Elites and Heroic rematches if stuck, and buys from the vendor; report the best floor per chapter and the gold the vendor took.
- In Unity: every twist chip, a win and a loss result, the vendor, a save from v16 loading.

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
