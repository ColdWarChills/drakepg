import { BALANCE } from '../config';
import { db } from '../data/db';
import type { ClassId, QuestDef, StatKey } from '../data/types';
import { isGearDef, rollGear, rollMods, type ItemGrant, type SigilTier } from './Items';
import { xpToNext, type Player } from './Player';
import { defaultRng, type Rng } from './rng';

/**
 * Testing cheats (the hidden menu in rooms: backtick, or tap the room name 5 times).
 * Toggles last until the page reloads and are never saved; everything else changes the real save.
 */
export const cheats = {
  /** The player takes no damage. */
  god: false,
  /** Any hit that lands on the enemy finishes it. */
  oneHit: false,
  /** Every roaming monster spawns as an Elite. */
  elites: false,
  /** Every chapter unlock (forge, Sigils, chests...) is open. */
  allFeatures: false,
};

/** Level up `count` times (stat points and skills as usual). */
export function addLevels(player: Player, count: number): void {
  for (let i = 0; i < count && player.level < BALANCE.maxLevel; i++) player.addXp(xpToNext(player.level) - player.xp);
}

/** Give back every spent stat point. */
export function resetStats(player: Player): void {
  const cls = player.classDef;
  const levels = player.level - 1;
  for (const key of ['str', 'int', 'dex', 'end'] as const) player.data.baseStats[key] = cls.stats[key] + (cls.growth[key] ?? 0) * levels;
  player.data.statPoints = BALANCE.statPointsPerLevel * levels;
  player.clampVitals();
}

export interface CheatItemOptions {
  /** Quality of every white line (and mod), or random like a drop. */
  quality: number | 'random';
  sigil?: SigilTier;
  mods: number;
}

/** A gear piece made to order (consumables ignore the options). */
export function cheatGrant(itemId: string, opts: CheatItemOptions, rng: Rng = defaultRng): ItemGrant {
  const def = db.item(itemId);
  if (!isGearDef(def)) return { itemId };
  const fixed = opts.quality === 'random' ? undefined : opts.quality;
  const grant: ItemGrant = { itemId, rolls: rollGear(def, rng, fixed) };
  if (opts.sigil) grant.sigil = opts.sigil;
  if (opts.mods > 0) {
    grant.mods = rollMods(def, opts.mods, rng);
    if (fixed !== undefined) for (const m of grant.mods) m.roll = fixed;
  }
  return grant;
}

/** Mark a quest done, along with every quest it requires. */
export function completeQuestChain(player: Player, quest: QuestDef): void {
  for (let q: QuestDef | undefined = quest; q; q = q.requires ? db.quest(q.requires) : undefined) {
    player.data.quests[q.id] = { status: 'done', count: q.objective.count };
  }
}

/** Fill the kill counts (or hand over the items) of every active quest, so it can be turned in. */
export function finishActiveQuests(player: Player): number {
  let n = 0;
  for (const q of db.quests()) {
    const state = player.data.quests[q.id];
    if (state?.status !== 'active') continue;
    const o = q.objective;
    if (o.kind === 'collect') {
      const missing = o.count - player.countItem(o.itemId);
      if (missing > 0) {
        player.addItem(o.itemId, missing);
        n++;
      }
    } else if (state.count < o.count) {
      state.count = o.count;
      n++;
    }
  }
  return n;
}

/**
 * Start a new character at the beginning of a later chapter, for testing it without replaying the earlier ones:
 * level, stat points spent, the earlier quests done, and the gear and potions you'd likely have by then.
 */
export interface ChapterStart {
  chapter: number;
  level: number;
  /** Last quest of the chapter before (marked done with everything it requires). */
  after?: string;
  weapon?: Record<ClassId, string>;
  armor?: Record<ClassId, string>;
  potions?: [string, number][];
  gold?: number;
}

export const CHAPTER_STARTS: ChapterStart[] = [
  { chapter: 1, level: 1 },
  {
    chapter: 2,
    level: 6,
    after: 'heart_of_thorns',
    weapon: { warrior: 'thornheart_blade', mage: 'heartwood_staff', rogue: 'briar_fang' },
    armor: { warrior: 'leather_armor', mage: 'apprentice_robe', rogue: 'leather_armor' },
    potions: [['health_potion', 5], ['mana_potion', 3]],
    gold: 300,
  },
  {
    chapter: 3,
    level: 10,
    after: 'eye_of_the_storm',
    weapon: { warrior: 'thunderhead_maul', mage: 'stormcoil_scepter', rogue: 'coilfang' },
    armor: { warrior: 'scale_mail', mage: 'reedweave_robe', rogue: 'scale_mail' },
    potions: [['greater_health_potion', 5], ['greater_mana_potion', 3]],
    gold: 800,
  },
];

/** Where free stat points go for a chapter start, repeating (same plan as the balance sim). */
const STAT_PLAN: Record<ClassId, StatKey[]> = {
  warrior: ['str', 'str', 'end'],
  mage: ['int', 'int', 'end'],
  rogue: ['dex', 'dex', 'str'],
};

export function applyChapterStart(player: Player, start: ChapterStart): void {
  const cls = player.data.classId;
  addLevels(player, start.level - player.level);
  const plan = STAT_PLAN[cls];
  for (let i = 0; player.data.statPoints > 0; i++) player.spendStatPoint(plan[i % plan.length]);
  if (start.after) completeQuestChain(player, db.quest(start.after));
  // Gear rolled like a decent drop (40% lines), worn right away; the starter gear goes.
  for (const id of [start.weapon?.[cls], start.armor?.[cls]]) {
    if (!id) continue;
    const def = db.item(id);
    if (!isGearDef(def)) continue;
    const gear = player.makeGear(id, { rolls: rollGear(def, defaultRng, 40) });
    player.data.gear.push(gear);
    player.equip(gear.uid);
  }
  player.data.gear = [];
  for (const [id, qty] of start.potions ?? []) player.addItem(id, qty);
  if (start.gold) player.data.gold = start.gold;
  player.restoreFull();
}
