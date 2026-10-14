# Plans: discussed or decided, not built yet

Built work is logged in `docs/IMPROVEMENTS_2026-10.md`. Nothing here has been playtested by hand. The combat pass and the prologue road were checked only with the sim and screenshots.

## Prologue road (built as a placeholder)

- Decided: 6 rooms, start with the class weapon only, one-way into Brindlemoor (the shop is found on arrival). Claude drafted the story hook.
- To do: real monsters (names, stats, art) for `burrow_rat`, `dew_slug` and `hedge_pixie`. They are shape stand-ins now.
- To do: background art for the 6 `road_*` rooms (procedural forest now), and art for Odo and Pell. None of it is in `art/QUEUE.md` yet.
- To do: final lines for Odo and Pell.
- Open: is 6 rooms the right length? Tune after a playtest.
- Open: monster HP 20-24 (about 2 hits) and a 35% gear drop rate (`BALANCE.loot.tableMob.road`). Tune after a playtest.
- Open: the hero looks armored with no armor on, because the base class art includes armor. Keep it, or get bare art?
- Note: story text is off by default, so players see only each NPC's first line.

## Combat and balance

- Ch2-3 bosses are too soft: the random-button bot still wins 75-95%.
  - Options: fixed `every` beats, or a per-chapter `bossDamage`.
  - Open: which one.
- Mage early game is weak: Thornheart at level 4 with starter gear is a 3% win.
  - Options: an earlier Fire staff in the shop, or a stronger Staff Bolt.
  - Open: which one.
- Watch: Snowmaw without Fire is a 0-58% win for every class (intended).
- Watch: Elites with stacked affixes on Tough monsters are harsh (they can be fled).
- Keep, decided:
  - Skills stay plain damage (no interrupts or silences).
  - Enrage, not a hard turn limit.
  - Heroic adds no HP.
  - Only specific monsters absorb (resist 150).
  - The element names stay Earth and Energy.

## Parked

- #6 Hunter contracts replacing quests: pick what to hunt; monsters you haven't met yet show as "?".
- #7 A class trial.
- #8 Bad-luck protection kicks in too late: `pityAfter` 80 vs about 40-50 kills per chapter.
- #10 Audit for overlapping systems: hone vs reinforce, mods vs Sigil mods.

## Dropped

- Volt Eel art: the eel was only a sample.
