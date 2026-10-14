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
