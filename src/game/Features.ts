import { db } from '../data/db';
import type { FeatureDef, SkillDef } from '../data/types';
import { cheats } from './cheats';
import type { Player } from './Player';

/**
 * Chapter-by-chapter unlocks (src/data/features.ts) and the "New!" popups that announce them and new skills.
 * What has been announced is kept in `PlayerData.seen` as 'feature:<id>' / 'skill:<id>'.
 */

/** Is this system open yet? (Its unlocking quest has been turned in.) */
export function featureOn(player: Player, id: string): boolean {
  const f = db.feature(id);
  if (f.disabled) return false;
  if (cheats.allFeatures) return true;
  return player.data.quests[f.unlockedBy]?.status === 'done';
}

export interface News {
  features: FeatureDef[];
  skills: SkillDef[];
}

/** Unlocked systems and learned skills that haven't been announced yet. */
export function unseenNews(player: Player): News {
  const seen = new Set(player.data.seen);
  return {
    features: db.features().filter((f) => featureOn(player, f.id) && !seen.has(`feature:${f.id}`)),
    skills: player.knownSkills.filter((s) => !seen.has(`skill:${s.id}`)),
  };
}

/** Mark news as announced. New skills also get a NEW tag in battle until first used. */
export function markSeen(player: Player, news: News): void {
  for (const f of news.features) player.data.seen.push(`feature:${f.id}`);
  for (const s of news.skills) {
    player.data.seen.push(`skill:${s.id}`);
    player.carryNew(s.id);
    if (!player.data.freshSkills.includes(s.id)) player.data.freshSkills.push(s.id);
  }
}

/** Skills a character starts with need no popup. `tag`: still mark them NEW in battle (a brand-new hero's first skill). */
export function markSkillsSeen(player: Player, tag: boolean): void {
  markSeen(player, { features: [], skills: unseenNews(player).skills });
  player.data.loadout = player.defaultLoadout();
  if (!tag) player.data.freshSkills = [];
}
