# Balance spec: one table, a few rules

**Draft (2026-11-10), rewritten short after the designer's "you are overcomplicating this ... simplify and make it more streamlined". Nothing built.** The idea is the designer's (`docs/PLANS.md`, "One baseline"): stop rebalancing for every class each time a monster is added.

## The rules

1. **Monsters get their numbers from one table by level.** Three kinds: Normal, Tough, Boss. The table starts from what the game has today (measured below), so nothing changes on day one.
2. **Classes are checked against the table, once.** A new monster needs no check per class.
3. **Defenses stay as they are** (designer, 2026-11-10). One limit: no monster makes one class more than 1.5 times slower than another.
4. **A weak spot is a bonus: 1.5 times on a normal monster, 1.75 times only for a monster's one special weak spot** (designer: yes). Missing it never blocks you.
5. **Strong resists are rare.** Per region at most two monsters that resist something by 75% or more and at most one that is immune. It shows before the fight, and the town sells something that works.
6. **"Bring the right thing" fights go where they are optional:** the Spire, Heroic rematches. Good for a second character later.

## What the game has today (`npm run sim -- 300 baseline`)

A hero of the monster's level, in shop gear, no potions:

| Kind | Hits to kill it | Its turns to kill you |
| --- | --- | --- |
| Normal (15 monsters) | about 4 | about 13 |
| Tough (6) | about 5 | about 11 |
| Boss (8) | about 10 | about 12 |

These become the table. Nothing is deadly and no class falls under 80% against a normal monster.

## The first fixes (built 2026-11-10)

Nine numbers on eight monsters, in `src/data/monsters.ts` and the Unity data (details in `docs/IMPROVEMENTS_2026-10.md`, "Balance: first fixes").

| Monster | Change | Before | After |
| --- | --- | --- | --- |
| Barkshell Beetle | defenses 24/24/3 to 18/18/6 | slowest class 1.63 times the fastest | 1.32 |
| The Thornheart | magic defense 18 to 12 | 1.50 | 1.33 |
| Spark Wisp | magic defense 16 to 10, Energy resist 75% to 50% | Mage 4.2 hits | 4.0 (Warrior 3.8) |
| Glacier Golem | defenses 26/26/8 to 20/20/10 | 2.09 | 1.74 in plain shop gear, 1.41 with a Fire weapon |
| Mire Toad | HP 170 to 145 | 4.7 hits | 4.0 |
| Bog Lurker | HP 240 to 195 | 5.0 hits | 3.8 |
| Sunken Revenant | HP 220 to 175 | 5.8 hits | 4.0 |
| Vent Tortoise | Fire resist 75% to 50% | Frostfang one strong resist over | within the limit |

- Normal monsters now take 3.6 hits at the median (was 3.9); every region is within the resist limit.
- What is left is about classes, not monsters (rule 2, the next step): the Rogue kills a Spark Wisp in 2.4 hits where the others need 4, and the Warrior needs 5 for a Mire Toad where the Mage needs 3.
- The Glacier Golem still favours the Mage in plain gear because Fire is its special weak spot and the Mage has Fire spells; with a Fire weapon the others catch up, which is the point of a Tough monster.
- Side effect: the Thornheart is easier for a Mage (level 4 in shop gear 96%, was 75%; level 5 with only the starter staff 71%).
- Weak spots (rule 4) needed no change: only Tough monsters have a 1.75 one, and each has just one.

## Then

- Move Chapter 1's monsters onto the table, check with the sim and both bots, then Chapters 2 and 3, before Chapter 4.
- Later ideas, not part of this: Blunt and Poison from weapons and flasks so every class has them; one gate guard instead of three.

## Where the rules come from

- **Pathfinder 2e** builds monsters from one table by level, not per class ([rules](https://2e.aonprd.com/Rules.aspx?ID=2874)).
- **Diablo II's developers** called immunities a problem: they hit players after dozens of hours in a build ([Blizzard, 2023](https://news.blizzard.com/en-us/article/23935220/inside-battle-nethow-the-diablo-ii-resurrected-team-terrorized-sanctuary)). Diablo III dropped them and then got called "dumbed down" for dropping elements too ([wiki](http://www.diablowiki.net/Immunity)).
- **Octopath Traveler and Persona 5** treat a weak spot as a reward; bosses have few ([Octopath](https://www.switchaboo.com/the-ins-and-outs-of-octopath-traveler-combat), [Persona](https://www.gamedeveloper.com/design/learn-how-to-make-turn-based-battles-from-persona-5)).
