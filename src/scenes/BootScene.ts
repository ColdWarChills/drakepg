import Phaser from 'phaser';
import { COLORS } from '../config';
import { validateData } from '../data/db';
import { preloadArt } from '../ui/art';
import { addText } from '../ui/text';
import { SceneKeys } from './keys';

/** Loads art and checks game data before anything else runs. */
export class BootScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Boot);
  }

  preload(): void {
    const { width, height } = this.scale;
    const label = addText(this, width / 2, height / 2, 'Loading...', { size: 28, color: COLORS.textDim }).setOrigin(0.5);
    this.load.on('progress', (p: number) => label.setText(`Loading... ${Math.round(p * 100)}%`));
    preloadArt(this);
  }

  create(): void {
    validateData();
    this.scene.start(SceneKeys.Title);
  }
}
