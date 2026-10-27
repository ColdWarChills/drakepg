import { BALANCE } from '../config';
import { db } from '../data/db';
import { ELEMENTS } from '../data/elements';
import type { Defenses, FlightDef, FloorDef, LootTier, MonsterDef, Resists, TowerFight, VendorDef } from '../data/types';
import { bookId, elementBooks, knowsOrHolds } from './Books';
import { applyAffix } from './Elites';
import { featureOn } from './Features';
import { makeHeroic } from './Heroic';
import { grantFor, isGearDef, modSlots, rollGear, rollMods, type ItemGrant, type SigilTier } from './Items';
import type { Player } from './Player';
import { questDone } from './Quests';
import { randInt, type Rng } from './rng';

/**
 * The tower (docs/TOWER_SPEC.md, data in src/data/tower.ts): floors cleared in order, each one fight against a known
 * monster moved up to the floor's level, with twists. The first clear of a floor pays marks (and stones or a Sigil
 * piece on 5th and 10th floors); marks and gold buy from the Warden's vendor. `PlayerData.tower` (save v17).
 */

export interface TowerState {
  /** Highest floor cleared (0 = none). */
  best: number;
  marks: number;
}

export function emptyTower(): TowerState {
  return { best: 0, marks: 0 };
}

export function lastFloor(): number {
  return db.floors().length;
}

export function floorDef(n: number): FloorDef {
  const f = db.floors()[n - 1];
  if (!f) throw new Error(`Unknown tower floor: ${n}`);
  return f;
}

export function flightOf(n: number): FlightDef {
  const fl = db.flights().find((x) => n >= x.first && n <= x.last);
  if (!fl) throw new Error(`Tower floor ${n} is in no flight`);
  return fl;
}

/** A flight opens with the tower feature and its quest done. */
export function flightOpen(player: Player, flight: FlightDef): boolean {
  return featureOn(player, 'tower') && questDone(player, flight.opensAfter);
}

/** Cleared floors (to fight again) and the next one, if its flight is open. */
export function floorOpen(player: Player, n: number): boolean {
  return n >= 1 && n <= lastFloor() && n <= player.data.tower.best + 1 && flightOpen(player, flightOf(n));
}

/** The floor to climb next, or null when it's locked or the top is cleared. */
export function nextFloor(player: Player): number | null {
  const n = player.data.tower.best + 1;
  return n <= lastFloor() && floorOpen(player, n) ? n : null;
}

/** First-clear marks: the flight's rate, x2 on a 5th floor, x3 on a 10th. */
export function marksFor(n: number): number {
  const m = BALANCE.tower.milestone;
  return flightOf(n).marks * (n % 10 === 0 ? m.tenth : n % 5 === 0 ? m.fifth : 1);
}

/** The floor's monster: moved up to the floor's level (Heroic for bosses), twists applied, no rewards of its own. */
export function floorMonster(n: number): MonsterDef {
  const f = floorDef(n);
  const t = BALANCE.tower;
  let def = f.heroic ? makeHeroic(db.monster(f.monsterId)) : db.monster(f.monsterId);
  const up = Math.max(0, f.level - def.level);
  const range = (r: [number, number], m: number): [number, number] => [Math.round(r[0] * m), Math.round(r[1] * m)];
  const defense: Defenses = {};
  for (const [type, v] of Object.entries(def.defense) as [keyof Defenses, number][]) defense[type] = Math.round(v + t.defensePerLevel * up);
  const hp = curveAt(t.curve.hp, f.level) / curveAt(t.curve.hp, def.level);
  const damage = curveAt(t.curve.damage, f.level) / curveAt(t.curve.damage, def.level);
  def = { ...def, level: f.level, hp: Math.round(def.hp * hp), damage: range(def.damage, damage), defense };
  const tower: TowerFight = { floor: n, twists: [...f.twists] };
  for (const id of f.twists) {
    const tw = db.twist(id);
    if (tw.affix) def = applyAffix(def, tw.affix);
    if (tw.unshakable && !(def.immune ?? []).includes('stun')) def = { ...def, immune: [...(def.immune ?? []), 'stun'] };
    if (tw.veil) {
      const resists: Resists = { ...def.resists };
      for (const e of ELEMENTS) resists[e] = e === tw.veil ? (resists[e] ?? 0) - t.veil.down : Math.min(100, (resists[e] ?? 0) + t.veil.up);
      def = { ...def, resists };
    }
    if (tw.shell) def = { ...def, shell: { name: tw.name, element: tw.shell.element, hits: tw.shell.hits, reduction: t.shell.reduction, reformAt: t.shell.reformAt } };
    if (tw.starts) tower.starts = [...(tower.starts ?? []), ...tw.starts];
    if (tw.shortFuse) tower.enrageAt = t.shortFuse;
    if (tw.noItems) tower.noItems = true;
  }
  return { ...def, xp: 0, gold: [0, 0], drops: [], loot: undefined, tower };
}

/** A curve's value at a level (1 = the first entry; past the end, the last). */
function curveAt(curve: readonly number[], level: number): number {
  return curve[Math.max(0, Math.min(curve.length - 1, level - 1))];
}

/** A first clear's pay. */
export interface FloorClear {
  floor: number;
  marks: number;
  items: ItemGrant[];
  /** Items that didn't fit the bag. */
  lost: ItemGrant[];
}

/** Record a won floor. Only the next floor counts (a cleared one is practice: null). */
export function clearFloor(player: Player, n: number, rng: Rng): FloorClear | null {
  const tower = player.data.tower;
  if (n !== tower.best + 1) return null;
  tower.best = n;
  const marks = marksFor(n);
  tower.marks += marks;
  const items: ItemGrant[] = [];
  if (n % 10 === 0) {
    const fl = flightOf(n);
    items.push(sigilPiece(fl.lootTable, fl.sigil, rng, false));
  } else if (n % 5 === 0) {
    for (const stone of db.stones()) for (let i = 0; i < BALANCE.tower.stones; i++) items.push({ itemId: stone.id });
  }
  const { lost } = player.grantRewards({ xp: 0, gold: 0, items }, true);
  return { floor: n, marks, items, lost };
}

const TIERS: LootTier[] = ['common', 'uncommon', 'rare'];

/** A random piece of a loot table (any tier, or weapons only) with a set Sigil, rolled like a drop. */
export function sigilPiece(tableId: string, sigil: SigilTier, rng: Rng, weaponsOnly: boolean): ItemGrant {
  const table = db.lootTable(tableId);
  const ids: string[] = [];
  for (const tier of TIERS) {
    for (const e of table.tiers[tier] ?? []) {
      if (ids.includes(e.itemId)) continue;
      if (weaponsOnly && db.item(e.itemId).type !== 'weapon') continue;
      ids.push(e.itemId);
    }
  }
  const itemId = ids[randInt(rng, 0, ids.length - 1)];
  const def = db.item(itemId);
  if (!isGearDef(def)) return { itemId };
  const grant: ItemGrant = { itemId, rolls: rollGear(def, rng, undefined, sigil), sigil };
  const mods = modSlots(def, sigil);
  if (mods) grant.mods = rollMods(def, mods, rng);
  return grant;
}

/** One thing the vendor sells: a set piece, a Sigil cache or a book. */
export interface VendorRow {
  key: string;
  entry: VendorDef;
  /** The item you get (a cache has none: its weapon is random). */
  itemId?: string;
  marks: number;
  gold: number;
}

/** What the vendor shows this hero: rows whose floor is cleared; books your class can read and doesn't know or hold. */
export function vendorStock(player: Player): VendorRow[] {
  const rows: VendorRow[] = [];
  const best = player.data.tower.best;
  for (const v of db.vendor()) {
    if (best < v.minFloor) continue;
    if (v.kind === 'set' && v.set) {
      for (const piece of db.set(v.set).pieces) rows.push({ key: `set:${piece}`, entry: v, itemId: piece, marks: v.marks, gold: v.gold });
    } else if (v.kind === 'cache' && v.lootTable) {
      rows.push({ key: `cache:${v.lootTable}`, entry: v, marks: v.marks, gold: v.gold });
    } else if (v.kind === 'books') {
      for (const e of v.elements ?? []) {
        for (const skillId of elementBooks(player.data.classId, e)) {
          if (knowsOrHolds(player, skillId)) continue;
          const itemId = bookId(skillId);
          rows.push({ key: `book:${skillId}`, entry: v, itemId, marks: v.marks, gold: v.gold + db.item(itemId).price });
        }
      }
    }
  }
  return rows;
}

export type VendorResult = 'ok' | 'marks' | 'gold' | 'full' | 'gone';

/** Buy a vendor row: marks and gold are paid, and the piece is rolled now (set pieces like a boss drop). */
export function buyFromVendor(player: Player, key: string, rng: Rng): VendorResult {
  const row = vendorStock(player).find((r) => r.key === key);
  if (!row) return 'gone';
  const tower = player.data.tower;
  if (tower.marks < row.marks) return 'marks';
  if (player.gold < row.gold) return 'gold';
  if (player.freeSlots === 0) return 'full';
  const v = row.entry;
  const grant: ItemGrant =
    v.kind === 'cache' && v.lootTable && v.sigil ? sigilPiece(v.lootTable, v.sigil, rng, true) : v.kind === 'set' ? grantFor(row.itemId!, rng, 'boss') : { itemId: row.itemId! };
  if (!player.receive(grant)) return 'full';
  tower.marks -= row.marks;
  player.data.gold -= row.gold;
  return 'ok';
}
