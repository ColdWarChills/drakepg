import type Phaser from 'phaser';
import type { Rewards } from '../game/Player';

export const SceneKeys = {
  Boot: 'Boot',
  Title: 'Title',
  Town: 'Town',
  Battle: 'Battle',
  Result: 'Result',
} as const;

export interface BattleSceneData {
  zoneId: string;
}

export type ResultSceneData =
  | { outcome: 'victory'; zoneId: string; monsterName: string; rewards: Rewards; levelsGained: number }
  | { outcome: 'defeat'; zoneId: string; monsterName: string };

/** Fade out, then start another scene. */
export function goTo(scene: Phaser.Scene, key: string, data?: object, duration = 250): void {
  scene.input.enabled = false;
  scene.cameras.main.fadeOut(duration, 0, 0, 0);
  scene.cameras.main.once('camerafadeoutcomplete', () => scene.scene.start(key, data));
}

/** Call at the top of every create(): re-enables input (goTo disables it) and fades in. */
export function enterScene(scene: Phaser.Scene, duration = 250): void {
  scene.input.enabled = true;
  scene.cameras.main.fadeIn(duration, 0, 0, 0);
}
