import { BALANCE, SAVE_KEY, SAVE_VERSION } from '../config';
import { readableBy } from './Books';
import { db } from '../data/db';
import { TOWN_START } from '../data/world';
import type { EquipSlot } from '../data/types';
import { sanitizeGuild } from './Guild';
import { ALL_SLOTS, fitsSlot, isGearDef, sanitizeTraits, type Gear } from './Items';
import { emptyEquipment, Player, STAT_KEYS, type PlayerData } from './Player';
import { lastFloor, type TowerState } from './Tower';
import { areaTown, RECALL_SCROLL } from './Travel';

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

  /**
   * The save as a file download (`drakepg-save.json`), to import in the Unity version. Loaded first, so an old save is
   * upgraded to the current version (Unity reads only that). False if there is none.
   */
  download(): boolean {
    const player = SaveSystem.load();
    if (!player) return false;
    const file: SaveFile = { version: SAVE_VERSION, savedAt: Date.now(), player: player.data };
    const url = URL.createObjectURL(new Blob([JSON.stringify(file)], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'drakepg-save.json';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    return true;
  },
};

/**
 * Upgrade old save files here when PlayerData changes. Bump SAVE_VERSION alongside.
 * v1 -> v2: added `location`; sanitize() fills it with the start position.
 * v2 -> v3: added `classId` (old saves become Warriors), `statPoints`, `quests`; dropped `skills`
 *           (skills now come from the class and level).
 * v3 -> v4: rolled gear. Equipment held item ids, and weapons/armor stacked in `inventory`; they become
 *           Standard (0%) gear instances in `equipment` and `gear`.
 * v4 -> v5: gear can carry a `sigil` and `mods`. Both are optional, so old gear simply has neither.
 * v5 -> v6: gear can carry a smith `plus`, and the bag can hold materials. Both need nothing from old saves.
 * v6 -> v7: `lootPity` (bad-luck protection counters); sanitize() starts it empty.
 * v7 -> v8: `bossGrit` (losses per boss); sanitize() starts it empty.
 * v8 -> v9: `seen` and `freshSkills` (unlock popups, NEW skill tags). Old characters already know their skills,
 *           so those are marked seen; unlocked systems still get announced once.
 * v9 -> v10: `loadout` (skills carried into battle); sanitize() gives old characters the default pick.
 * v10 -> v11: 8 equipment slots. The old `armor` slot becomes `body`; helm, gloves, boots, necklace and rings start empty.
 * v11 -> v12: `visited` rooms (the map) and `lastTown` (Recall Scrolls). The room you stand in counts as visited, your
 *             area's town becomes the last town, and you get the starting Recall Scrolls a new hero has.
 * v12 -> v13: `equipment.spare` (a second weapon to swap to); sanitize() starts it empty.
 * v13 -> v14: `guardsBeaten` (gate guards on the prologue road); sanitize() starts it empty. The road is one way, so
 *             heroes already past it never meet one. Class skill lists changed too (Light Bolt, Gas Shot at level 1):
 *             sanitize() carries newly known skills that haven't been announced yet.
 * v14 -> v15: the Adventurers' Guild (`guild`). The errand quests between the story quests are gone (sanitize()
 *             drops them); the rank they would have earned is given from how far the story got.
 * v15 -> v16: skill books (`learned`, `bookPity`). Levels now teach 6 skills per class; the rest are books. Nobody loses
 *             a skill: every skill the old level lists had taught by the hero's level is learned.
 * v16 -> v17: the tower (`tower`: best floor and marks); sanitize() starts it empty.
 */
/** The level lists before skill books (save v15 and older): what an old character already knows. */
const OLD_LEVEL_SKILLS: Record<string, [string, number][]> = {
  warrior: [['heavy_strike', 1], ['second_wind', 2], ['war_cry', 3], ['skull_splitter', 4], ['shield_bash', 6], ['sunder', 8], ['iron_skin', 10], ['cleave', 12], ['bloodlust', 14], ['earthshaker', 16], ['rallying_roar', 18], ['titans_wrath', 20]],
  mage: [['light_bolt', 1], ['mend', 2], ['ember_bolt', 2], ['frost_lance', 3], ['meteor', 5], ['spark_chain', 6], ['stone_skin', 8], ['gale', 10], ['tidal_wave', 12], ['sunburst', 14], ['shadow_hex', 16], ['thunderstorm', 18], ['cataclysm', 20]],
  rogue: [['gas_shot', 1], ['twin_cut', 2], ['bandage', 2], ['flurry', 3], ['envenom', 4], ['assassinate', 5], ['smoke_bomb', 7], ['expose_weakness', 9], ['shadowstep', 11], ['fan_of_knives', 13], ['hemorrhage', 15], ['death_mark', 18], ['thousand_cuts', 20]],
};

function oldSkillsAt(classId: unknown, level: number): string[] {
  return (OLD_LEVEL_SKILLS[typeof classId === 'string' ? classId : ''] ?? []).filter(([, l]) => l <= level).map(([id]) => id);
}

function migrate(file: SaveFile): SaveFile | null {
  if (!file || typeof file !== 'object' || !file.player) return null;
  if (file.version > SAVE_VERSION) {
    console.warn(`Save version ${file.version} is newer than this build (${SAVE_VERSION})`);
    return null;
  }
  if ((file.version ?? 1) < 4) {
    const old = file.player as unknown as { equipment?: Record<string, unknown>; inventory?: { itemId: string; qty: number }[] };
    let uid = 1;
    const toGear = (itemId: unknown) => (typeof itemId === 'string' ? { uid: `g${uid++}`, itemId, rolls: {} } : null);
    const equipment = { weapon: toGear(old.equipment?.weapon), armor: toGear(old.equipment?.armor) };
    const gear: Gear[] = [];
    const inventory: { itemId: string; qty: number }[] = [];
    for (const e of Array.isArray(old.inventory) ? old.inventory : []) {
      if (!e || !db.hasItem(e.itemId)) continue;
      if (isGearDef(db.item(e.itemId))) for (let i = 0; i < Math.min(50, e.qty); i++) gear.push(toGear(e.itemId)!);
      else inventory.push(e);
    }
    file.player = { ...file.player, equipment, inventory, gear, nextUid: uid } as unknown as PlayerData;
    file.version = 4;
  }
  if (file.version < 5) file.version = 5;
  if (file.version < 6) file.version = 6;
  if (file.version < 7) file.version = 7;
  if (file.version < 8) file.version = 8;
  if (file.version < 9) {
    // Characters from before this already know their skills: no pile of popups on load.
    const p = file.player as Partial<PlayerData> & { level?: number; classId?: string };
    p.seen = oldSkillsAt(p.classId, p.level ?? 1).map((id) => `skill:${id}`);
    p.freshSkills = [];
    file.version = 9;
  }
  // v9 -> v10: `loadout` (skills carried into battle). Old characters get the default pick in sanitize.
  if (file.version < 10) file.version = 10;
  if (file.version < 11) {
    const old = (file.player as unknown as { equipment?: Record<string, unknown> }).equipment ?? {};
    (file.player as unknown as { equipment: Record<string, unknown> }).equipment = { weapon: old.weapon ?? null, body: old.armor ?? null };
    file.version = 11;
  }
  if (file.version < 12) {
    const p = file.player as Partial<PlayerData>;
    const here = p.location && db.hasRoom(p.location.roomId) ? p.location.roomId : null;
    const town = here ? areaTown(db.room(here).map.area) : undefined;
    p.visited = [...new Set([here, town?.id].filter((id): id is string => !!id))];
    p.lastTown = town?.id ?? null;
    const inventory = Array.isArray(p.inventory) ? p.inventory : [];
    if (!inventory.some((e) => e?.itemId === RECALL_SCROLL)) inventory.push({ itemId: RECALL_SCROLL, qty: BALANCE.startingRecallScrolls });
    p.inventory = inventory;
    file.version = 12;
  }
  if (file.version < 13) file.version = 13;
  if (file.version < 14) file.version = 14;
  if (file.version < 15) {
    const p = file.player as Partial<PlayerData>;
    const quests = (p.quests ?? {}) as Record<string, { status?: string } | undefined>;
    const done = (id: string) => quests[id]?.status === 'done';
    const started = (id: string) => quests[id] !== undefined;
    // The old errand quests stood where the rank gates are now; anyone past one gets that rank.
    let rank = 0;
    if (done('bramble_bandits') || started('heart_of_thorns')) rank = 1;
    if (done('a_worthy_foe') || started('mother_of_the_mire')) rank = 2;
    if (done('thaw') || started('the_old_one')) rank = 3;
    p.guild = { rank, points: 0, board: [], hunts: [], done: 0 };
    file.version = 15;
  }
  if (file.version < 16) {
    const p = file.player as Partial<PlayerData>;
    const cls = typeof p.classId === 'string' && db.hasClass(p.classId) ? db.class(p.classId) : null;
    const levelSkills = new Set(cls?.skills.map((s) => s.skillId) ?? []);
    p.learned = oldSkillsAt(p.classId, typeof p.level === 'number' ? p.level : 1).filter((id) => !levelSkills.has(id));
    p.bookPity = {};
    file.version = 16;
  }
  if (file.version < 17) file.version = 17;
  return file;
}

/** Best floor within the tower's floors, whole non-negative marks. */
function sanitizeTower(raw: unknown): TowerState {
  const r = (raw && typeof raw === 'object' ? raw : {}) as Partial<TowerState>;
  const whole = (v: unknown, max: number) => (typeof v === 'number' && Number.isFinite(v) ? Math.max(0, Math.min(max, Math.floor(v))) : 0);
  return { best: whole(r.best, lastFloor()), marks: whole(r.marks, Number.MAX_SAFE_INTEGER) };
}

/** Kill counters for loot tables that still exist. */
function sanitizePity(raw: unknown): Record<string, number> {
  const out: Record<string, number> = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, n] of Object.entries(raw as Record<string, unknown>)) {
    if (db.hasLootTable(id) && typeof n === 'number' && Number.isFinite(n) && n > 0) out[id] = Math.floor(n);
  }
  return out;
}

/** Loss counters for monsters that are still bosses. */
function sanitizeGrit(raw: unknown): Record<string, number> {
  const out: Record<string, number> = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const [id, n] of Object.entries(raw as Record<string, unknown>)) {
    if (db.hasMonster(id) && db.monster(id).boss && typeof n === 'number' && Number.isFinite(n) && n > 0) out[id] = Math.floor(n);
  }
  return out;
}

/** A saved gear piece, or null if it is broken or (for equipment) in the wrong slot. */
function sanitizeGear(raw: unknown, slot?: EquipSlot): Gear | null {
  if (!raw || typeof raw !== 'object') return null;
  const g = raw as Partial<Gear>;
  if (typeof g.itemId !== 'string' || !db.hasItem(g.itemId) || typeof g.uid !== 'string') return null;
  const def = db.item(g.itemId);
  if (!isGearDef(def) || (slot && !fitsSlot(def, slot))) return null;
  const gear: Gear = { uid: g.uid, itemId: g.itemId, ...sanitizeTraits(def, g) };
  if (g.locked === true) gear.locked = true;
  if (g.isNew === true) gear.isNew = true;
  return gear;
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

  const equipment = emptyEquipment();
  const seen = new Set<string>();
  for (const slot of ALL_SLOTS) {
    const g = sanitizeGear(raw.equipment?.[slot], slot);
    if (g && !seen.has(g.uid)) {
      equipment[slot] = g;
      seen.add(g.uid);
    }
  }
  const gear: Gear[] = [];
  for (const g of Array.isArray(raw.gear) ? raw.gear : []) {
    const clean = sanitizeGear(g);
    if (clean && !seen.has(clean.uid)) {
      seen.add(clean.uid);
      gear.push(clean);
    }
  }
  // New uids must not collide with saved ones.
  const highest = Math.max(0, ...[...seen].map((uid) => Number(uid?.slice(1)) || 0));

  const data: PlayerData = {
    name: typeof raw.name === 'string' && raw.name ? raw.name : fresh.name,
    level: Math.max(1, Math.floor(num(raw.level, 1))),
    xp: Math.max(0, num(raw.xp, 0)),
    gold: Math.max(0, Math.floor(num(raw.gold, fresh.gold))),
    baseStats,
    hp: num(raw.hp, 1),
    mp: num(raw.mp, 0),
    equipment,
    inventory: (Array.isArray(raw.inventory) ? raw.inventory : [])
      .filter((e) => e && db.hasItem(e.itemId) && !isGearDef(db.item(e.itemId)) && num(e.qty, 0) > 0)
      .map((e) => ({ itemId: e.itemId, qty: Math.floor(e.qty) })),
    gear,
    nextUid: Math.max(highest + 1, Math.floor(num(raw.nextUid, 1))),
    // An existing hero with no usable location goes to town, not back to the prologue road.
    location:
      raw.location && db.hasRoom(raw.location.roomId)
        ? { roomId: raw.location.roomId, x: num(raw.location.x, TOWN_START.x), y: num(raw.location.y, TOWN_START.y) }
        : { ...TOWN_START },
    classId,
    statPoints: Math.max(0, Math.floor(num(raw.statPoints, 0))),
    quests: sanitizeQuests(raw.quests),
    lootPity: sanitizePity(raw.lootPity),
    bossGrit: sanitizeGrit(raw.bossGrit),
    seen: Array.isArray(raw.seen) ? raw.seen.filter((s): s is string => typeof s === 'string') : [],
    freshSkills: Array.isArray(raw.freshSkills) ? raw.freshSkills.filter((s): s is string => typeof s === 'string' && db.hasSkill(s)) : [],
    loadout: [],
    visited: Array.isArray(raw.visited) ? [...new Set(raw.visited.filter((id): id is string => typeof id === 'string' && db.hasRoom(id)))] : [],
    lastTown: typeof raw.lastTown === 'string' && db.hasRoom(raw.lastTown) && db.room(raw.lastTown).town ? raw.lastTown : null,
    guardsBeaten: Array.isArray(raw.guardsBeaten) ? [...new Set(raw.guardsBeaten.filter((id): id is string => typeof id === 'string' && db.hasRoom(id)))] : [],
    guild: sanitizeGuild(raw.guild),
    learned: Array.isArray(raw.learned)
      ? [...new Set(raw.learned.filter((id): id is string => typeof id === 'string' && db.hasSkill(id) && readableBy(db.skill(id), classId)))]
      : [],
    bookPity: sanitizePity(raw.bookPity),
    tower: sanitizeTower(raw.tower),
  };

  const player = new Player(data);
  const known = new Set(player.knownSkills.map((s) => s.id));
  data.loadout = Array.isArray(raw.loadout)
    ? [...new Set(raw.loadout.filter((s): s is string => typeof s === 'string' && known.has(s)))].slice(0, BALANCE.skillSlots)
    : player.defaultLoadout();
  // Skills a class gained after this save was made (not announced yet) go into a free slot, like a fresh level-up.
  for (const s of player.knownSkills) if (!data.seen.includes(`skill:${s.id}`)) player.carryNew(s.id);
  player.clampVitals();
  if (player.hp <= 0) player.restoreFull();
  return player.data;
}
