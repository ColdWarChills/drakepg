import type Phaser from 'phaser';
import { fitCamera } from '../ui/view';
import type { ItemGrant } from '../game/Items';
import type { Rewards } from '../game/Player';

export const SceneKeys = {
  Boot: 'Boot',
  Title: 'Title',
  Create: 'Create',
  Room: 'Room',
  Battle: 'Battle',
  Result: 'Result',
} as const;

/**
 * How the player arrives in a room:
 * - spawn: at an exact spot (new game, loaded save, waking at the inn)
 * - exit:  through a room edge; enters from the opposite side at height `y`
 * - battle: back from a fight; restores the room as it was
 */
export type RoomSceneData =
  /** `recalled`: arriving by Recall Scroll (a shimmer on arrival). */
  | { roomId: string; via: 'spawn'; x: number; y: number; intro?: boolean; recalled?: boolean }
  /** Arriving through an exit: `side` is where you appear (for a back or down exit, at its `x`). */
  | { roomId: string; via: 'exit'; side: 'left' | 'right' | 'back' | 'down'; x?: number; y: number }
  /** `escaped`: the monster ran away (it's gone from the room). */
  | { roomId: string; via: 'battle'; outcome: 'victory' | 'fled' | 'escaped'; x: number; y: number };

export interface BattleSceneData {
  zoneId: string;
  monsterId: string;
  /** Elite affix id: fight the Elite version. */
  elite?: string;
  /** A mimic sized to the zone (src/game/Chests.ts). */
  mimic?: boolean;
  /** Heroic rematch of a boss (src/game/Heroic.ts). */
  heroic?: boolean;
  /** Where to go back to afterwards. */
  roomId: string;
  x: number;
  y: number;
}

export type ResultSceneData =
  | {
      outcome: 'victory';
      monsterName: string;
      rewards: Rewards;
      /** Loot that didn't fit in the bag. */
      lost: ItemGrant[];
      levelsGained: number;
      /** Names of skills unlocked by this level up. */
      newSkills: string[];
      /** Quest and Guild hunt counters this kill advanced. `turnIn`: who to see when done (default the quest giver). */
      quests: { name: string; goal: string; count: number; target: number; turnIn?: string }[];
      back: RoomSceneData;
    }
  | {
      outcome: 'defeat';
      monsterName: string;
      /** Where you wake up. */
      wake: { roomId: string; x: number; y: number };
      /** Boss grit you now have against this boss (0-1), if it was a boss. */
      grit?: number;
    };

/** Fade out, then start another scene. */
export function goTo(scene: Phaser.Scene, key: string, data?: object, duration = 250): void {
  scene.input.enabled = false;
  scene.cameras.main.fadeOut(duration, 0, 0, 0);
  scene.cameras.main.once('camerafadeoutcomplete', () => scene.scene.start(key, data));
}

/** Call at the top of every create(): re-enables input (goTo disables it) and fades in. */
export function enterScene(scene: Phaser.Scene, duration = 250): void {
  scene.input.enabled = true;
  scene.tweens.timeScale = 1;
  scene.time.timeScale = 1;
  scene.cameras.main.resetFX();
  fitCamera(scene.cameras.main);
  scene.cameras.main.fadeIn(duration, 0, 0, 0);
}
