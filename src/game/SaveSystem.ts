import { BALANCE, SAVE_KEY, SAVE_VERSION } from '../config';
import { db } from '../data/db';
import { RARITY } from '../data/rarity';
import type { EquipSlot } from '../data/types';
import { isGear, rollGear, traitDef, type GearInstance } from './Gear';
import { Player, STAT_KEYS, type PlayerData } from './Player';

interface SaveFile {
  version: number;
  savedAt: number;
  player: PlayerData;
}

/**
 * Saves live in localStorage. Some browsers block storage inside iframes (itch.io embeds),
 * so every call is guarded and failures are reported instead of thrown.
 */
export const SaveSystem = {
  hasSave(): boolean {
    try {
      return localStorage.getItem(SAVE_KEY) !== null;
    } catch {
      return false;
    }
  },

  save(player: Player): boolean {
    const file: SaveFile = { version: SAVE_VERSION, savedAt: Date.now(), player: player.data };
    try {
      localStorage.setItem(SAVE_KEY, JSON.stringify(file));
      return true;
    } catch (err) {
      console.warn('Save failed', err);
      return false;
    }
  },

  load(): Player | null {
    try {
      const raw = localStorage.getItem(SAVE_KEY);
      if (!raw) return null;
      const file = migrate(JSON.parse(raw));
      return file ? new Player(sanitize(file.player)) : null;
    } catch (err) {
      console.warn('Load failed', err);
      return null;
    }
  },

  clear(): void {
    try {
      localStorage.removeItem(SAVE_KEY);
    } catch {
      // Nothing to clear if storage is blocked.
    }
  },
};

/**
 * Upgrade old save files here when PlayerData changes. Bump SAVE_VERSION alongside.
 * v1 -> v2: added `location`; sanitize() fills it with the start position.
 * v2 -> v3: added `classId` (old saves become Warriors), `statPoints`, `quests`; dropped `skills`
 *           (skills now come from the class and level).
 * v3 -> v4: gear became rolled instances (`gear`, `nextUid`); `equipment` holds uids. Old equipment and
 *           bagged weapons/armor become 0% Common instances.
 */
function migrate(file: SaveFile): SaveFile | null {
  if (!file || typeof file !== 'object' || !file.player) return null;
  if (file.version > SAVE_VERSION) {
    console.warn(`Save version ${file.version} is newer than this build (${SAVE_VERSION})`);
    return null;
  }
  if ((file.version ?? 1) < 4) {
    const old = file.player as unknown as { equipment?: Record<string, unknown>; inventory?: { itemId: string; qty: number }[] };
    const gear: GearInstance[] = [];
    let uid = 1;
    const make = (id: unknown): number | null => {
      if (typeof id !== 'string' || !db.hasItem(id) || !isGear(db.item(id))) return null;
      gear.push({ ...rollGear(id, 'shop'), uid });
      return uid++;
    };
    const equipment = { weapon: make(old.equipment?.weapon), armor: make(old.equipment?.armor) };
    const inventory = [];
    for (const e of Array.isArray(old.inventory) ? old.inventory : []) {
      if (e && typeof e.itemId === 'string' && db.hasItem(e.itemId) && isGear(db.item(e.itemId))) {
        for (let i = 0; i < Math.min(50, Math.floor(e.qty) || 0); i++) make(e.itemId);
      } else inventory.push(e);
    }
    file.player = { ...file.player, equipment, inventory, gear, nextUid: uid } as PlayerData;
    file.version = 4;
  }
  return file;
}

/** Keep gear entries that still point at real items, with values in range. */
function sanitizeGear(raw: unknown): GearInstance[] {
  if (!Array.isArray(raw)) return [];
  const out: GearInstance[] = [];
  const seen = new Set<number>();
  for (const g of raw as Partial<GearInstance>[]) {
    if (!g || typeof g.uid !== 'number' || seen.has(g.uid)) continue;
    if (typeof g.baseId !== 'string' || !db.hasItem(g.baseId) || !isGear(db.item(g.baseId))) continue;
    const rarity = g.rarity && g.rarity in RARITY ? g.rarity : 'common';
    const q = [0, 1, 2].map((i) => {
      const v = Array.isArray(g.q) ? g.q[i] : 0;
      return typeof v === 'number' && Number.isFinite(v) ? Math.max(0, Math.min(100, Math.round(v))) : 0;
    });
    const traits = (Array.isArray(g.traits) ? g.traits : []).filter((t) => {
      try {
        traitDef(t.id);
        return typeof t.value === 'number';
      } catch {
        return false;
      }
    });
    const plus = typeof g.plus === 'number' ? Math.max(0, Math.min(BALANCE.gear.maxPlus, Math.floor(g.plus))) : 0;
    seen.add(g.uid);
    out.push({ uid: g.uid, baseId: g.baseId, rarity, q, plus, traits });
  }
  return out;
}

function sanitizeQuests(raw: unknown): PlayerData['quests'] {
  const out: PlayerData['quests'] = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, q] of Object.entries(raw as Record<string, { status?: unknown; count?: unknown }>)) {
    if (!db.quests().some((d) => d.id === id) || !q) continue;
    if (q.status !== 'active' && q.status !== 'done') continue;
    out[id] = { status: q.status, count: typeof q.count === 'number' ? Math.max(0, Math.floor(q.count)) : 0 };
  }
  return out;
}

/** Fill missing fields and drop references to items, rooms and quests that no longer exist. */
function sanitize(raw: Partial<PlayerData>): PlayerData {
  const classId = typeof raw.classId === 'string' && db.hasClass(raw.classId) ? raw.classId : 'warrior';
  const fresh = Player.createNew('Hero', classId).data;
  const num = (v: unknown, fallback: number) => (typeof v === 'number' && Number.isFinite(v) ? v : fallback);

  const baseStats = { ...fresh.baseStats };
  for (const key of STAT_KEYS) baseStats[key] = num(raw.baseStats?.[key], baseStats[key]);

  const gear = sanitizeGear(raw.gear);
  const equip = (uid: unknown, slot: EquipSlot) => {
    const g = gear.find((x) => x.uid === uid);
    return g && db.item(g.baseId).type === slot ? g.uid : null;
  };

  const data: PlayerData = {
    name: typeof raw.name === 'string' && raw.name ? raw.name : fresh.name,
    level: Math.max(1, Math.floor(num(raw.level, 1))),
    xp: Math.max(0, num(raw.xp, 0)),
    gold: Math.max(0, Math.floor(num(raw.gold, fresh.gold))),
    baseStats,
    hp: num(raw.hp, 1),
    mp: num(raw.mp, 0),
    equipment: {
      weapon: equip(raw.equipment?.weapon, 'weapon'),
      armor: equip(raw.equipment?.armor, 'armor'),
    },
    inventory: (Array.isArray(raw.inventory) ? raw.inventory : [])
      .filter((e) => e && db.hasItem(e.itemId) && !isGear(db.item(e.itemId)) && num(e.qty, 0) > 0)
      .map((e) => ({ itemId: e.itemId, qty: Math.floor(e.qty) })),
    gear,
    nextUid: Math.max(num(raw.nextUid, 1), ...gear.map((g) => g.uid + 1)),
    location:
      raw.location && db.hasRoom(raw.location.roomId)
        ? { roomId: raw.location.roomId, x: num(raw.location.x, fresh.location.x), y: num(raw.location.y, fresh.location.y) }
        : fresh.location,
    classId,
    statPoints: Math.max(0, Math.floor(num(raw.statPoints, 0))),
    quests: sanitizeQuests(raw.quests),
  };

  const player = new Player(data);
  player.clampVitals();
  if (player.hp <= 0) player.restoreFull();
  return player.data;
}
