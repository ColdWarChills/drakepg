# Plans: not built yet

Everything discussed or decided that isn't in the game yet. What was built is in [`IMPROVEMENTS_2026-10.md`](IMPROVEMENTS_2026-10.md).

## Next up

- Open a PR for `claude/trusting-wozniak-fq7yr7` and merge it (GitHub Pages only deploys the default branch).
- Playtest by hand. So far only the sim and screenshots have checked it. Things to judge:
  - Does the "Next:" intent line read at a glance?
  - Does enrage (turn 15, bosses 30) feel fair?
  - Is the drop reveal too slow when farming normal monsters?
  - Is the 5-slot skill swap in the Character panel clear?
- Volt Eel art: the prompt is in `art/QUEUE.md` (`mon_volt_eel`).

## Tuning

- **Ch2-3 bosses are too soft**: a random-button bot still wins 75-95%.
  - Options: fixed `every` beats on their big attacks (worked for the Thornheart), or a per-chapter `bossDamage`.
- **Mage early game is weak** since Staff Bolt uses the staff's element. Thornheart at level 4 with starter gear: 3% (20% after 5 losses).
  - Options: an earlier Fire staff in Tobin's shop, or a stronger Staff Bolt.
- **Old Snowmaw without Fire**: 0-58% for every class. Intended (the quest before her collects Fire Essence). Watch it in playtests.
- **Stacked-affix Elites on Tough monsters** are harsh (Frenzied Ash Imp at level 3: 6-72%; Ironhide Drakeling: 15-77%). They can be fled. Revisit if playtests complain.
- **Normal fights**: the random bot does about as well as the smart AI. Accepted: the tension is in attrition between fights. Revisit if single fights feel flat.

## Parked features

### Hunter contracts (#6)
- Delayed: quest testing isn't happening at this stage.
- Idea: contracts replace quests. The player chooses what to hunt.
- Monsters not met yet show as a blacked-out card with "?".
- Open: who hands them out (a board, an NPC)? Rewards per contract? Do the chapter story beats stay as fixed quests?

### Class choice before seeing combat (#7)
- Not important at this stage.
- Options: a free class swap until the end of Chapter 1, or a one-fight trial per class on the Create screen.

### Bad-luck protection kicks in too late (#8)
- Agreed to do, not urgent.
- `pityAfter` is 80 dry kills per area; a chapter is roughly 40-50 normal kills (rough math, not measured), so it may never trigger.
- Options: lower to 25-30, count across all areas, or cut it.

### System overlap audit (#10)
- Agreed in principle.
- First candidates to merge: hone vs reinforce, and normal mods vs Sigil mods.

### Endgame (#9)
- No endgame goal yet, on purpose: the chapters are still being built.
- Heroic boss tier is built. Open: more tiers later (new moves each, never more HP)?

## Decisions to keep

- **Skills stay simple** (Silkroad style): skills just do damage. No interrupts, silences or similar for now.
- **Elemental weapons are part of the puzzle**: carrying the wrong element should hurt. Absorbing monsters (Volt Eel, Ember Salamander) are the first case.
  - Open: add the example weapons from the discussion? "Leaf blade" would be Earth and "lightning dagger" Energy, since the game has no Nature or Lightning element.
- **Drop reveal**: keep the fun full result screen; reveal timing scales with how good the drop is.

## Not discussed yet (need details)

- **Map**: mentioned as a plan, but there's no discussion of it in the notes. Need: what it shows, when it opens, and whether it's a world map or a per-chapter map.
- **Scrolls**: mentioned as a plan, but there's no discussion of them in the notes. Need: what they do (consumable spells? lore? teleport?), and where they come from.

## Smaller open items

- Economy review was skipped: the forge's gold costs and late-game gold flow weren't checked.
- Story text off: quest *offers* still use the plain reminder line. Open: give each offer a short in-character line too?
