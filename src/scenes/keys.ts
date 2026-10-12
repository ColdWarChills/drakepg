import type Phaser from 'phaser';
import type { Rewards } from '../game/Player';

export const SceneKeys = {
  Boot: 'Boot',
  Title: 'Title',
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
  | { roomId: string; via: 'spawn'; x: number; y: number; intro?: boolean }
  | { roomId: string; via: 'exit'; side: 'left' | 'right'; y: number }
  | { roomId: string; via: 'battle'; outcome: 'victory' | 'fled'; x: number; y: number };

export interface BattleSceneData {
  zoneId: string;
  monsterId: string;
  /** Where to go back to afterwards. */
  roomId: string;
  x: number;
  y: number;
}

export type ResultSceneData =
  | { outcome: 'victory'; monsterName: string; rewards: Rewards; levelsGained: number; back: RoomSceneData }
  | { outcome: 'defeat'; monsterName: string };

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
  scene.cameras.main.setZoom(1).setRotation(0).setScroll(0, 0);
  scene.cameras.main.fadeIn(duration, 0, 0, 0);
}
