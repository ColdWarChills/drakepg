import Phaser from 'phaser';
import { validateData } from '../data/db';
import { SceneKeys } from './keys';

/** Loads assets (none yet) and checks game data before anything else runs. */
export class BootScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Boot);
  }

  create(): void {
    validateData();
    this.scene.start(SceneKeys.Title);
  }
}
