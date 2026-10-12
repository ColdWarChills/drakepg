import { SAVE_KEY, SAVE_VERSION } from '../config';
import { db } from '../data/db';
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
 */
function migrate(file: SaveFile): SaveFile | null {
  if (!file || typeof file !== 'object' || !file.player) return null;
  if (file.version > SAVE_VERSION) {
    console.warn(`Save version ${file.version} is newer than this build (${SAVE_VERSION})`);
    return null;
  }
  return file;
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

  const equip = (id: unknown) => (typeof id === 'string' && db.hasItem(id) ? id : null);

  const data: PlayerData = {
    name: typeof raw.name === 'string' && raw.name ? raw.name : fresh.name,
    level: Math.max(1, Math.floor(num(raw.level, 1))),
    xp: Math.max(0, num(raw.xp, 0)),
    gold: Math.max(0, Math.floor(num(raw.gold, fresh.gold))),
    baseStats,
    hp: num(raw.hp, 1),
    mp: num(raw.mp, 0),
    equipment: {
      weapon: equip(raw.equipment?.weapon),
      armor: equip(raw.equipment?.armor),
    },
    inventory: (Array.isArray(raw.inventory) ? raw.inventory : [])
      .filter((e) => e && db.hasItem(e.itemId) && num(e.qty, 0) > 0)
      .map((e) => ({ itemId: e.itemId, qty: Math.floor(e.qty) })),
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
