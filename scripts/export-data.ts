/**
 * Writes the game's content for the Unity port (docs/UNITY_PORT.md, step 2): every data table, BALANCE and the
 * world constants, as one JSON file the C# side loads (`Drakepg.Rules.Db`). Content stays written once, in
 * src/data/; run `npm run export-data` after changing it.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { BALANCE, SAVE_VERSION } from '../src/config';
import { CLASSES } from '../src/data/classes';
import { RECIPES } from '../src/data/crafting';
import { validateData } from '../src/data/db';
import { DAMAGE_TYPE_LABELS, ELEMENT_COLORS, ELEMENT_LABELS, ELEMENT_TEXT, ELEMENTS, NEUTRAL_COLORS, OPPOSITE } from '../src/data/elements';
import { ELITE_AFFIXES } from '../src/data/elites';
import { FEATURES } from '../src/data/features';
import { PROMOTIONS } from '../src/data/guild';
import { ITEMS } from '../src/data/items';
import { LOOT_TABLES } from '../src/data/loot';
import { MAP_AREAS } from '../src/data/map';
import { MONSTERS } from '../src/data/monsters';
import { QUESTS } from '../src/data/quests';
import { SETS } from '../src/data/sets';
import { SKILLS } from '../src/data/skills';
import { STATUSES } from '../src/data/statuses';
import { INN_SPAWN, ROOMS, START, TOWN_START, ZONES } from '../src/data/world';
import { CHAPTER_STARTS } from '../src/game/cheats';

const OUT = process.argv[2] ?? 'unity/Assets/Resources/Data/game.json';

validateData();

const data = {
  saveVersion: SAVE_VERSION,
  balance: BALANCE,
  elements: ELEMENTS,
  elementLabels: ELEMENT_LABELS,
  opposite: OPPOSITE,
  elementColors: ELEMENT_COLORS,
  neutralColors: NEUTRAL_COLORS,
  elementText: ELEMENT_TEXT,
  damageTypeLabels: DAMAGE_TYPE_LABELS,
  start: START,
  townStart: TOWN_START,
  innSpawn: INN_SPAWN,
  classes: CLASSES,
  items: ITEMS,
  skills: SKILLS,
  statuses: STATUSES,
  monsters: MONSTERS,
  zones: ZONES,
  rooms: ROOMS,
  quests: QUESTS,
  affixes: ELITE_AFFIXES,
  recipes: RECIPES,
  lootTables: LOOT_TABLES,
  features: FEATURES,
  sets: SETS,
  mapAreas: MAP_AREAS,
  promotions: PROMOTIONS,
  chapterStarts: CHAPTER_STARTS,
};

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, JSON.stringify(data, null, 1));
const counts = Object.entries(data)
  .filter(([, v]) => Array.isArray(v))
  .map(([k, v]) => `${k} ${(v as unknown[]).length}`)
  .join(', ');
console.log(`${OUT}: ${counts}`);
