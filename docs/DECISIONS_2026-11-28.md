# Decisions made for the designer, 2026-11-28

Seven design calls were waiting for an answer. Each one is decided here for him, from what the code and data do today, and each is his to veto. Call 1 has a full job brief. The run was cut short, so calls 2 to 7 hold the decision and its facts in one paragraph each, without a brief; none of them was checked with `npm run sim`, `npm run pace` or the Unity playthrough. Numbers marked "my pick" are not from the data.

Source tags follow the game-design-advisor skill: `[article]`, `[from memory]`, `[my read]`.

## 1. What the last two bosses open

**Today**
- No feature is opened by Old Snowmaw (`the_old_one`) or The Frostbinder (`the_frozen_roost`): `src/data/features.ts` (whole file, 87 lines). Their cards in the Menu page's "What opens" row hold only a skill book (`src/data/quests.ts:117`, `:132`).
- The switched-off features are `materials` and `forge` (`disabled: true`, `src/data/features.ts`, forge at about lines 69-76). The Menu page leaves switched-off features out (`unity/Assets/Scripts/Rules/Hub.cs:77`).
- The note that raised this call names Heroic rematches as a candidate. That is wrong about the game: Heroic is already offered on every boss's rematch card as soon as its quest is done (`unity/Assets/Scripts/Game/Screens/RoomScreen.cs:2826-2859`, rule `src/game/Heroic.ts:9`). Gating it now would take an option away.
- Sigils open at Stormcoil (`sigils`, `unlockedBy: 'eye_of_the_storm'`), and from that moment all three tiers can drop: `BALANCE.sigilChance` (`src/config.ts:158-163`), rolled with no tier limit by `rollSigil` (`src/game/Items.ts:212-222`, called at `:201`; Unity `Rules/Items.cs:175`, `:185`). The gate is one flag: `src/game/Battle.ts:743`, `Rules/Battle.cs:800`, chests at `RoomScreen.cs:698`.
- The Spire already treats the tiers as chapter steps: the Fen flight pays a Dawn piece, the Frostfang flight (opens after `the_frozen_roost`) a Dusk piece (`src/data/tower.ts`, flights).

**Decision**
Old Snowmaw opens **Dusk Sigils**, The Frostbinder opens **Eclipse Sigils**. Stormcoil's Sigils become Dawn only. A drop that rolls a tier not yet open falls to the best open tier, so no Sigil drop is lost, only its tier. All chances stay as they are (from the data, `src/config.ts:158-163`); the extra lines per tier, 2 and 3, are from the data too (`BALANCE.sigils`, `src/config.ts:124-128`). No new system, no new art.
Rests on: show the next unlock before it can be reached, and the last unlocks should read strongest (`pincus-katkoff`, `[article]` plus `[my read]`); prefer reusing what exists ("minimum representation", `sylvester`, `[from memory]`). It also gives a hero past the last boss something to hunt (call 3).

**What the player sees**
The Menu page's cards for Old Snowmaw and The Frostbinder each show a Sigil picture and a name next to the skill book. After Old Snowmaw falls, gear can drop with a Dusk Sigil; after The Frostbinder falls, with an Eclipse Sigil.

**His to veto**
Whether holding back Dusk and Eclipse feels like a reward or like something taken away (between Stormcoil and Old Snowmaw a Dusk drop is about 1 gear drop in 200 from a roaming monster today).

**Job brief**
- Goal: the last two bosses each open a Sigil tier. The designer will see the two cards on the Menu page with a Sigil picture and name, and a Dusk drop after Old Snowmaw.
- Data: in `src/data/features.ts` add `sigils_dusk` (`unlockedBy: 'the_old_one'`, name `Dusk Sigils`, short `Dusk Sigils can drop: two extra lines.`, picture `{ items: ['iron_sword'], sigil: 'dusk' }`) and `sigils_eclipse` (`unlockedBy: 'the_frozen_roost'`, name `Eclipse Sigils`, short `Eclipse Sigils can drop: three extra lines.`, picture `{ items: ['iron_sword'], sigil: 'eclipse' }`). These four strings are the only new on-screen words.
- Rule: add `sigilCap(player)` to `src/game/Features.ts`: `'eclipse'` if `sigils_eclipse` is on, else `'dusk'` if `sigils_dusk` is on, else `'dawn'` if `sigils` is on, else none. `rollSigil(rng, source, cap)` in `src/game/Items.ts` rolls as today, then lowers a tier above `cap` to `cap`. Pass the cap from every caller that passes `extras` today (`src/game/Battle.ts:743`, chest loot, the Lucky Impling's Sigil at `Battle.ts:748`). The Spire's floor pieces and vendor caches keep their fixed tier (they are gated by floor).
- Unity twin: `Rules/Features.cs`, `Rules/Items.cs:175-185`, `Rules/Battle.cs:800-808`, `Game/Screens/RoomScreen.cs:698`, and the icon map in `Game/Ui/HubPanel.cs:44` (both new ids use `spark`, like `sigils`).
- Numbers: none new; nothing added to `BALANCE`. `PlayerData` does not change, no `SAVE_VERSION` bump. Gear already held keeps its Sigil.
- Tests: with cap Dawn, 10000 `rollSigil` calls per source give no Dusk or Eclipse and the same count of Sigils as uncapped; with cap Eclipse the result equals today's. `HubTests`: Old Snowmaw's and The Frostbinder's steps each hold one feature.
- Checks: `npm run typecheck`, `npm run export-data`, `npm run sim`, Unity EditMode tests, the Unity playthrough (drops changed).
- Pictures: the Menu page before either boss (two locked cards with the Sigil picture) and after both; one Dusk drop on the Victory card (cheat rates up).
- Not in the job: changing any chance, the forge, showing the Spire's last ten floors on the Frostbinder's card, anything on the hero.
- Time box: 45 minutes.

## 2. A second use for gold in town

**Decision: no new sink now.** Gold already has an open-ended use and two priced goals: rework costs `30 x tier x 1.5^reworks` per try with no ceiling (`src/game/Rework.ts:36-38`, `src/config.ts:86`), the Spire vendor asks 300 to 3000 gold a row on top of marks (`src/data/tower.ts`, `VENDOR`), and the bank being built adds rows for 100 to 3200 gold, 6300 in all (my sum). The small uses are the inn (5, `src/config.ts:18`), the coach (5 a room, `:22`) and books (20 a level, `:48`). In the first chapter gold is short, not piled up (48 gold at the first shop, weapons 100-120: PLANS play check). Rests on "always list skip it" (`sylvester`, `[from memory]`). If a later playthrough shows a hero ending Chapter 3 with more than about 3000 gold unspent (my pick: the price of the dearest bank row), the fallback is the paid inn meal the two outside reads picked. His to veto: whether a meal is wanted for its own sake.

## 3. What roams above level 13

**Decision: no new land and no new monsters; the Spire's cleared floors become the place (call 4) and Eclipse Sigils the reason (call 1).** Today the highest roaming monster is the Vent Wyrmling at level 13 (`src/data/monsters.ts`), heroes end the story at about 16-17 (PLANS bot numbers), Heroic rematches reach level 15 and 17 (`BALANCE.heroic.levels: 3`, `src/config.ts:217`), and the Spire's floors run to level 24 but pay once (`src/data/tower.ts`, `src/game/Tower.ts:113-115`). `floorMonster` already moves a known monster up the level curve (`src/game/Tower.ts:69-96`), so the Spire holds the higher-level fights the world lacks. Not looked at: whether Guild rank S can be reached. His to veto: whether farming belongs in the world's lands instead of the Spire; that would be a bigger job (a land's monsters moved up after the last boss).

## 4. Spire repeat pay

**Decision: a cleared floor pays XP, gold and one gear roll each time; marks stay first-clear only.** Today a repeat pays nothing: `clearFloor` returns `null` unless it is the next floor (`src/game/Tower.ts:113-115`), the floor's monster carries `xp: 0, gold: [0, 0]` and no loot (`:95`), and the panel says "Cleared: fight it again for practice (no reward)." (`unity/Assets/Scripts/Game/Ui/TowerPanel.cs:132`). So repeating floors is not worth an evening. Rule: XP and gold are the base monster's, times floor level over the monster's own level, times `BALANCE.tower.repeat.pay` = 1 (all my picks); one `rollLoot(flight.lootTable, 'mob', ...)` (`src/game/Loot.ts:45`). New panel words: "Cleared: fight it again for XP, gold and a gear roll (no marks)." Twin: `Rules/Tower.cs:130`, `Game/Screens/BattleScreen.cs:2973`. Not checked with the sim or the playthrough. His to veto: the pay size.

## 5. The road before the first town

**Decision: no rest spot and no potion seller on the road; every win gives back 10% of max HP (my pick), shown on the Victory card as "+N HP"; no extra warning on higher lands.** The road is three rooms with no campfire or shop (`src/data/world.ts:220`, `:251`, `:268`), its pests have `damage: [0, 0]` (`src/data/monsters.ts`), and a new hero carries 2 Health Potions (`src/game/Player.ts:164`), so the road does not need a seller. Wins do not heal (`restoreAfterBattle: false`, `src/config.ts:16`) while a loss costs nothing and fully rests the hero (PLANS play check), so today losing heals more than winning; a small heal on wins removes that without ending the need for potions (40 HP for 15 gold, `src/data/items.ts:39-46`), campfires and inns. Global, not first-land only, so there is no cliff at the Fen. Rests on "reward over punishment" (`balance-and-systems`). The number goes in `BALANCE` beside `restoreAfterBattle`; not checked with the sim. Lands above the hero already show red names, two warning marks and the level (PLANS play check). His to veto: wins not healing is his design; this is the call I am least sure he wants.

## 6. The first town

**Decision: the elder's first talk shows her existing first line before the quest line; the level-up stat buttons stay as they are; the "Start at" picker is hidden from players.** With story text off (the default) the first talk shows only "New quest: ..." because the lines are replaced (`unity/Assets/Scripts/Game/Screens/RoomScreen.cs:2612`, first talk called at `:217`); the fix shows `Dialogue[0]` ('Ah, a new face. Welcome to Brindlemoor.', `src/data/world.ts` about `:329-341`) first, no new words. The stat buttons (`unity/Assets/Scripts/Game/Ui/RewardCards.cs:60-72`) already have reminders on the room tag, the boss card and the loss card (`RoomScreen.cs:2737-2752`), and the evidence for a problem is one test save: skip it (`sylvester`, `[from memory]`). The picker is drawn for everyone (`unity/Assets/Scripts/Game/Screens/CreateScreen.cs:157`, `:251-262`; web `src/scenes/CreateScene.ts:171`); show it only when cheats are on. Not looked at: the exact cheat switch in Unity. His to veto: whether unspent stat points should be placed for the player.

## 7. Pacing spots: the Spark Wisp and the Fen

**Decision: the Spark Wisp's Flicker makes it Evasive for 1 turn instead of 2; the Fen gets no new rest spot.** The Spark Wisp is common Fen trash (weight 3 of 8, zone `fen`, `src/data/world.ts:68`) but uses Flicker every 3rd move for 2 turns of Evasive, -40% to hit it (`src/data/monsters.ts:338-362`, `src/data/statuses.ts:34`), which stretches fights into the enrage (`BALANCE.enrage`, `src/config.ts:208`): the lost fight in PLANS ran 23 turns. Its trait text stays true. The Fen already has a campfire room and an inn (`src/data/world.ts:863`, `:762`), and call 5's win heal eases the rest. How many hero attacks 1 turn covers is my reading of the data and must be tested; not checked with the sim. His to veto: whether the Spark Wisp should instead be marked Tough and made rarer.
