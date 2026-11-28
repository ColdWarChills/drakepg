# The game's loops

Written on the night of 2026-11-27 to 28, after the designer's direction: "more of RPG game, where we have big goals and less small goals. so we don't handhold players all the way. they can do whatever they want. They can farm wherever they want and so on."

This page says what the player does again and again, at five sizes, and what each size gives to the next. The build list and its numbers are in `docs/PLANS.md`.

## The five loops

| Loop | How long | What the player does | What it gives |
| --- | --- | --- | --- |
| Fight | 30 to 90 s | Reads the monster's shown next move, picks a button | XP, gold, sometimes a piece of gear |
| Spot | 5 to 10 min | Stays in a room or walks a land, fights what roams, opens chests | A bag of gear and gold, a level now and then |
| Town | every 20 to 30 min | Sells, reworks gear, buys potions and books, takes hunts, rests | A stronger hero for the same level |
| Big goal | 1 to 2 h | Picks a boss, gets ready for it (level, gear, its weak element), wins | Something new opens: a system, the rest of a land, a set piece |
| Long | 10 h and more | Spire floors, Guild rank, Heroic rematches, sets, level 50 | A reason to keep the hero |

## How one feeds the next

A fight is easy on purpose and short. Its job is to pay: XP always, gold always, gear often enough that a bag fills in one spot visit.

A spot is where the player chooses. Any land is open from the first town, and the level on a monster's name is the only warning. A room refills while the hero stays, so nobody has to walk in and out. Each monster tends to drop one kind of piece, so "I need boots" has an answer: hunt the monster that drops boots.

Town turns the bag into power. Selling pays for reworking the lines on the gear that is kept; potions and skill books are the other spending. This is where a second use for gold is still missing.

A boss is the big goal and the only hard fight. It stands in its room whether or not anyone asked for it, and a kill counts. An ordinary fight asks for nothing; a boss asks for preparation, and says what kind on its sheet (the Bogmother: bring Energy and Defend the drag). A hero who walks in unprepared at the boss's level should lose most tries; one who farmed for it should win most.

A boss kill opens something, not only the next boss: the second weapon and the Spire, mimics, Sigils. The long game then asks for the same loops at a higher level.

## Rules these loops hold to

- No quest is needed to go anywhere or to fight anything. Quests are a reward waiting at the giver, not a key.
- The monster's level is the warning. No other gate between lands.
- Ordinary fights stay near a sure win for every class. Only bosses, Tough monsters and Elites test the hero.
- Everything a monster does and drops is shown on its sheet. No hidden rules.
- Farming is never punished: no gear is destroyed, a room never runs dry.

## Where each loop stood before the night, and what was built

| Loop | Thin before | Built this night |
| --- | --- | --- |
| Fight | Fine | Nothing changed |
| Spot | A room stayed empty while the hero stood in it; most wins dropped nothing; no spot was better than another; Elites and chests came late | Rooms refill (one monster per 25 s); Elites and chests from the first town on; gear on about 1 win in 5; each roaming monster favours one kind of gear, named on its sheet |
| Town | Gold has one real use | Nothing (left for the designer after he has played) |
| Big goal | Fixed order by quest; bosses 2 to 5 asked for nothing | Every land and boss room open from the first town; a boss kill counts without its quest; the Bogmother asks for preparation (Energy skills or farmed Fen gear); Guild hunts reach one rank up, so a new hero's board stays in the Thornwood |
| Long | Nothing roams above level 13; the Spire pays once | Nothing |

All of it is in the Unity game on the branch `claude/unity-port`, each change its own commit, and stays off the live branch until the designer has played and said yes. Checked with the rules' tests, screenshots and the bot: all three classes beat every boss on the first try, with about 2 to 2.5 hours of story. That is the bot, which spends its stat points and buys gear. By hand, a level 9 hero with nine levels of stat points unspent lost the Bogmother's rematch twice, so the points come first. Both hand checks (a new warrior's first half hour, then the title screen, a mage's start and the level 9 save) are in `docs/PLANS.md`.

## Still open, for the designer

- A second use for gold in town (two outside reads picked a paid inn meal that lasts a number of fights).
- A list of the bosses, their levels and what each opens, somewhere the player can find it (the Guild clerk was suggested).
- What roams above level 13, and what the Spire pays on a repeat.
- Whether high lands need more than the level number as a warning.
