import { BALANCE } from '../config';
import { db } from '../data/db';
import type { MonsterDef, ZoneDef } from '../data/types';
import { zoneLevel, zoneLootTable } from './Chests';
import { cheats } from './cheats';
import { modSlots, rollGear, rollMods, SIGIL_TIERS, type GearDef, type ItemGrant } from './Items';
import { rollLoot } from './Loot';
import { chance, defaultRng, weightedPick, type Rng } from './rng';

/**
 * Lucky Implings: a rare roaming spawn in any monster room, from the very start. It runs from you, is hard to hit
 * and escapes after a few turns, but pays lots of gold and has a shot at a Sigil (`BALANCE.lucky`).
 */
export const LUCKY_ID = 'lucky_impling';

export function isLucky(monsterId: string): boolean {
  return monsterId === LUCKY_ID;
}

/** Should this roaming spawn be a Lucky Impling? */
export function rollLucky(rng: Rng = defaultRng): boolean {
  return cheats.lucky || chance(rng, BALANCE.lucky.chance);
}

/** The Impling sized to the area: a share of its monsters' HP and damage, quicker than all of them. */
export function makeImpling(zone: ZoneDef): MonsterDef {
  const base = db.monster(LUCKY_ID);
  const l = BALANCE.lucky;
  const mobs = zone.encounters.map((e) => db.monster(e.monsterId));
  const avg = (f: (m: MonsterDef) => number) => mobs.reduce((sum, m) => sum + f(m), 0) / mobs.length;
  const loot = zoneLootTable(zone);
  const dmg = (i: 0 | 1) => Math.max(1, Math.round(avg((m) => m.damage[i]) * l.damage));
  const gold = avg((m) => (m.gold[0] + m.gold[1]) / 2) * l.gold;
  return {
    ...base,
    level: zoneLevel(zone),
    hp: Math.round(avg((m) => m.hp) * l.hp),
    stats: {
      str: Math.round(avg((m) => m.stats.str) * 0.5),
      int: Math.round(avg((m) => m.stats.int) * 0.5),
      dex: Math.max(...mobs.map((m) => m.stats.dex)) + l.dex,
      end: Math.round(avg((m) => m.stats.end) * 0.5),
    },
    damage: [dmg(0), dmg(1)],
    defense: {
      melee: Math.round(avg((m) => m.defense.melee ?? 0)),
      pierce: Math.round(avg((m) => m.defense.pierce ?? 0)),
      magic: Math.round(avg((m) => m.defense.magic ?? 0)),
    },
    xp: Math.round(avg((m) => m.xp) * l.xp),
    gold: [Math.round(gold * 0.8), Math.round(gold * 1.2)],
    escapeAfter: l.escapeAfter,
    ...(loot ? { loot } : {}),
  };
}

/** The Impling's jackpot: a piece of the area's gear (better tiers stay rarer) as a Sigil with its mods. */
export function luckySigil(tableId: string, rng: Rng = defaultRng): ItemGrant {
  const table = db.lootTable(tableId);
  const itemId =
    rollLoot(tableId, 'elite', rng, 0, false)?.grant.itemId ??
    weightedPick(rng, [...table.tiers.common, ...table.tiers.uncommon, ...table.tiers.rare], (e) => e.weight ?? 1).itemId;
  const tier = weightedPick(rng, SIGIL_TIERS, (t) => BALANCE.lucky.sigilTiers[t]);
  const def = db.item(itemId) as GearDef;
  const grant: ItemGrant = { itemId, rolls: rollGear(def, rng, undefined, tier), sigil: tier };
  const mods = modSlots(def, tier);
  if (mods) grant.mods = rollMods(def, mods, rng);
  return grant;
}
