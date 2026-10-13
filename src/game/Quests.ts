import { isGear, rollGear } from './Gear';
import { db } from '../data/db';
import type { QuestDef, SkillDef } from '../data/types';
import type { Player, Rewards } from './Player';

/**
 * - locked: prerequisite not done yet
 * - available: can be accepted from the giver
 * - active: accepted, objective not met
 * - ready: objective met, turn in at the giver
 * - done: turned in
 */
export type QuestStatus = 'locked' | 'available' | 'active' | 'ready' | 'done';

export function questStatus(player: Player, quest: QuestDef): QuestStatus {
  const state = player.data.quests[quest.id];
  if (state?.status === 'done') return 'done';
  if (state?.status === 'active') return state.count >= quest.objective.count ? 'ready' : 'active';
  if (quest.requires && player.data.quests[quest.requires]?.status !== 'done') return 'locked';
  return 'available';
}

/** Accepted or finished: used for exits and bosses that open once a quest starts. */
export function questStarted(player: Player, questId: string): boolean {
  return player.data.quests[questId] !== undefined;
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
    .map((quest) => ({ quest, status: questStatus(player, quest), count: player.data.quests[quest.id]?.count ?? 0 }))
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
export function recordKill(player: Player, monsterId: string): QuestProgress[] {
  const updates: QuestProgress[] = [];
  for (const quest of db.quests()) {
    const state = player.data.quests[quest.id];
    if (state?.status !== 'active' || quest.objective.monsterId !== monsterId) continue;
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
  player.data.quests[quest.id] = { status: 'done', count: quest.objective.count };
  const classItem = quest.rewards.classItems?.[player.data.classId];
  const ids = [...(quest.rewards.items ?? []), ...(classItem ? [classItem] : [])];
  const rewards: Rewards = {
    xp: quest.rewards.xp,
    gold: quest.rewards.gold,
    items: ids.filter((id) => !isGear(db.item(id))),
    gear: ids.filter((id) => isGear(db.item(id))).map((id) => rollGear(id, 'quest')),
  };
  const levelBefore = player.level;
  const levelsGained = player.grantRewards(rewards);
  return { rewards, levelsGained, newSkills: player.skillsLearnedSince(levelBefore) };
}
