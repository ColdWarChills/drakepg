import { BALANCE } from '../config';
import { db } from '../data/db';
import type { MonsterDef, ZoneDef } from '../data/types';
import { boosted, cheats } from './cheats';
import type { ItemGrant } from './Items';
import { rollLoot } from './Loot';
import type { Rewards } from './Player';
import { chance, defaultRng, randInt, type Rng } from './rng';

/**
 * Treasure chests that sometimes stand in monster rooms. Most hold gold, materials and often gear from the
 * area's loot table; some are mimics (`BALANCE.chest.mimicChance`) that fight back and drop chest loot.
 */

/** Snapshot uid of the mimic you're fighting (roaming monsters use positive uids). */
export const MIMIC_UID = -2;

/** The loot table of an area: the one its monsters use. */
export function zoneLootTable(zone: ZoneDef): string | undefined {
  for (const e of zone.encounters) {
    const loot = db.monster(e.monsterId).loot;
    if (loot) return loot;
  }
  return undefined;
}

/** The level chests and mimics in this area are pitched at. */
export function zoneLevel(zone: ZoneDef): number {
  return zone.recommendedLevel[1];
}

/** Does a chest appear in a freshly filled room? Mimic or not is decided right away (and shown by small tells). */
export function rollChest(rng: Rng = defaultRng): { mimic: boolean } | null {
  if (!chance(rng, BALANCE.chest.chance)) return null;
  return { mimic: chance(rng, BALANCE.chest.mimicChance) };
}

/** What a chest holds: gold, a material or two from the area's monsters, maybe a potion, often gear. `extras`: Sigils/mods. */
export function rollChestLoot(zone: ZoneDef, rng: Rng = defaultRng, extras = true, withMaterials = true): Rewards {
  const level = zoneLevel(zone);
  const items: ItemGrant[] = [];
  const drops = zone.encounters.flatMap((e) => db.monster(e.monsterId).drops);
  const materials = withMaterials ? drops.filter((d) => db.item(d.itemId).type === 'material') : [];
  const potions = drops.filter((d) => db.item(d.itemId).type === 'consumable');
  for (let i = randInt(rng, 1, 2); i > 0 && materials.length; i--) {
    const m = materials[randInt(rng, 0, materials.length - 1)];
    for (let n = randInt(rng, 1, 2); n > 0; n--) items.push({ itemId: m.itemId });
  }
  if (potions.length && chance(rng, boosted(0.35))) items.push({ itemId: potions[randInt(rng, 0, potions.length - 1)].itemId });
  const table = zoneLootTable(zone);
  const gear = table ? rollLoot(table, 'chest', rng, 0, extras) : null;
  if (gear) items.push(gear.grant);
  return { xp: 0, gold: randInt(rng, 8, 14) * level * cheats.rates, items };
}

/** The mimic, sized to the area: a Tough fight that pays out like a chest. */
export function makeMimic(zone: ZoneDef): MonsterDef {
  const base = db.monster('mimic');
  const level = zoneLevel(zone);
  const grow = (v: number) => Math.round(v + (level - 1) * 1.2);
  const loot = zoneLootTable(zone);
  return {
    ...base,
    level,
    hp: Math.round(30 + 28 * level),
    stats: { str: grow(base.stats.str), int: grow(base.stats.int), dex: grow(base.stats.dex), end: grow(base.stats.end) },
    damage: [Math.round(3 + 1.5 * level), Math.round(6 + 1.9 * level)],
    defense: { melee: Math.round(2 + 0.9 * level), pierce: Math.round(2 + 0.9 * level), magic: Math.round(1 + 0.6 * level) },
    xp: Math.round(10 * level + 1.2 * level * level),
    gold: [4 * level, 8 * level],
    ...(loot ? { loot } : {}),
    drops: rollChestLoot(zone).items.filter((g) => !g.rolls).map((g) => ({ itemId: g.itemId, chance: 1 })),
  };
}
