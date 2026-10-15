import { db } from '../data/db';
import type { MonsterDef, QuestDef, SkillDef } from '../data/types';
import { cheats } from './cheats';
import { grantFor } from './Items';
import type { Player, Rewards } from './Player';

/**
 * - locked: prerequisite not done yet
 * - available: can be accepted from the giver
 * - active: accepted, objective not met
 * - ready: objective met, turn in at the giver
 * - done: turned in
 */
export type QuestStatus = 'locked' | 'available' | 'active' | 'ready' | 'done';

/** How far along the objective is: kills counted so far, or items of the kind in the bag (capped at the target). */
export function questProgress(player: Player, quest: QuestDef): number {
  const o = quest.objective;
  if (o.kind === 'collect') return Math.min(o.count, player.countItem(o.itemId));
  return player.data.quests[quest.id]?.count ?? 0;
}

export function questStatus(player: Player, quest: QuestDef): QuestStatus {
  const state = player.data.quests[quest.id];
  if (state?.status === 'done') return 'done';
  if (state?.status === 'active') return questProgress(player, quest) >= quest.objective.count ? 'ready' : 'active';
  if (quest.requires && player.data.quests[quest.requires]?.status !== 'done') return 'locked';
  return 'available';
}

/** Accepted or finished: used for exits and bosses that open once a quest starts. */
export function questStarted(player: Player, questId: string): boolean {
  return player.data.quests[questId] !== undefined;
}

/** True while an active quest still needs this monster killed. */
export function questNeedsKill(player: Player, monsterId: string): boolean {
  return db.quests().some((q) => q.objective.kind === 'kill' && q.objective.monsterId === monsterId && questStatus(player, q) === 'active');
}

/** True while an active quest asks you to bring this item (so it drops even if its kind is still locked). */
export function questWantsItem(player: Player, itemId: string): boolean {
  return db.quests().some((q) => q.objective.kind === 'collect' && q.objective.itemId === itemId && questStatus(player, q) === 'active');
}

/** True while an active quest wants any Elite (they spawn more often then). */
export function questWantsElite(player: Player): boolean {
  return db.quests().some((q) => q.objective.kind === 'kill' && q.objective.elite && !q.objective.monsterId && questStatus(player, q) === 'active');
}

/** Does killing this monster count for the quest? Elites keep their base monster's id. */
function objectiveMatches(quest: QuestDef, monster: MonsterDef): boolean {
  const o = quest.objective;
  if (o.kind !== 'kill') return false;
  return (!o.monsterId || o.monsterId === monster.id) && (!o.elite || monster.elite !== undefined);
}

export function questDone(player: Player, questId: string): boolean {
  return player.data.quests[questId]?.status === 'done';
}

/** The quest this NPC has something to say about, most urgent first: ready > available > active. */
export function questForNpc(player: Player, npcId: string): { quest: QuestDef; status: QuestStatus } | null {
  const mine = db.quests().filter((q) => q.giver === npcId).map((quest) => ({ quest, status: questStatus(player, quest) }));
  for (const wanted of ['ready', 'available', 'active'] as const) {
    const hit = mine.find((q) => q.status === wanted);
    if (hit) return hit;
  }
  return null;
}

/** Quests in progress, for the on-screen tracker. */
export function activeQuests(player: Player): { quest: QuestDef; status: QuestStatus; count: number }[] {
  return db
    .quests()
    .map((quest) => ({ quest, status: questStatus(player, quest), count: questProgress(player, quest) }))
    .filter((q) => q.status === 'active' || q.status === 'ready');
}

export function acceptQuest(player: Player, quest: QuestDef): void {
  if (questStatus(player, quest) !== 'available') return;
  player.data.quests[quest.id] = { status: 'active', count: 0 };
}

export interface QuestProgress {
  quest: QuestDef;
  count: number;
  target: number;
}

/** Count a defeated monster toward every active quest that wants it. */
export function recordKill(player: Player, monster: MonsterDef): QuestProgress[] {
  const updates: QuestProgress[] = [];
  for (const quest of db.quests()) {
    const state = player.data.quests[quest.id];
    if (state?.status !== 'active' || !objectiveMatches(quest, monster)) continue;
    if (state.count >= quest.objective.count) continue;
    state.count += 1;
    updates.push({ quest, count: state.count, target: quest.objective.count });
  }
  return updates;
}

export interface TurnInResult {
  rewards: Rewards;
  levelsGained: number;
  newSkills: SkillDef[];
}

export function turnInQuest(player: Player, quest: QuestDef): TurnInResult | null {
  if (questStatus(player, quest) !== 'ready') return null;
  const o = quest.objective;
  if (o.kind === 'collect' && !player.removeItem(o.itemId, o.count)) return null;
  player.data.quests[quest.id] = { status: 'done', count: o.count };
  const classItem = quest.rewards.classItems?.[player.data.classId];
  const rewards: Rewards = {
    // Testing rates (`cheats.rates`) multiply XP and gold.
    xp: quest.rewards.xp * cheats.rates,
    gold: quest.rewards.gold * cheats.rates,
    items: [...(quest.rewards.items ?? []), ...(classItem ? [classItem] : [])].map((id) => grantFor(id)),
  };
  const levelBefore = player.level;
  // Quest rewards may overflow a full bag rather than be lost.
  const levelsGained = player.grantRewards(rewards, true).levels;
  return { rewards, levelsGained, newSkills: player.skillsLearnedSince(levelBefore) };
}
