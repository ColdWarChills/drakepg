import { BALANCE } from '../config';
import { db } from '../data/db';
import type { QuestDef } from '../data/types';
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

/** Fill the kill counts of every active quest, so it can be turned in. */
export function finishActiveQuests(player: Player): number {
  let n = 0;
  for (const q of db.quests()) {
    const state = player.data.quests[q.id];
    if (state?.status === 'active' && state.count < q.objective.count) {
      state.count = q.objective.count;
      n++;
    }
  }
  return n;
}
