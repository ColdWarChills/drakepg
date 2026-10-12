import Phaser from 'phaser';
import { COLORS } from '../config';
import { Player } from '../game/Player';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { Button } from '../ui/Button';
import { Modal } from '../ui/Modal';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys } from './keys';

export class TitleScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Title);
  }

  create(): void {
    enterScene(this);
    const { width, height } = this.scale;

    // Simple layered backdrop: night sky, moon, distant hills.
    this.add.rectangle(0, 0, width, height, 0x1a1428).setOrigin(0);
    this.add.circle(1020, 150, 70, 0xf5e6c4, 0.9);
    this.add.ellipse(300, height, 900, 320, 0x2b2140);
    this.add.ellipse(1000, height, 1000, 260, 0x221a33);

    addText(this, width / 2, 170, 'DRAKEPG', { size: 96, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5);
    addText(this, width / 2, 250, 'a turn-based adventure', { size: 26, color: COLORS.textDim }).setOrigin(0.5);

    const hasSave = SaveSystem.hasSave();

    new Button(this, width / 2, 380, 'New Game', () => {
      if (hasSave) this.confirmOverwrite();
      else this.startNew();
    }, { width: 280, height: 64, fontSize: 26 });

    new Button(this, width / 2, 465, 'Continue', () => this.continueGame(), {
      width: 280,
      height: 64,
      fontSize: 26,
    }).setEnabled(hasSave);

    addText(this, width / 2, height - 30, 'Placeholder art · v0.1', { size: 16, color: COLORS.textDim }).setOrigin(0.5);
  }

  private startNew(): void {
    SaveSystem.clear();
    const player = Player.createNew();
    session.start(player);
    SaveSystem.save(player);
    goTo(this, SceneKeys.Town, { intro: true });
  }

  private continueGame(): void {
    const player = SaveSystem.load();
    if (!player) {
      this.startNew();
      return;
    }
    session.start(player);
    goTo(this, SceneKeys.Town);
  }

  private confirmOverwrite(): void {
    const modal = new Modal(this, 640, 260, 'Start over?');
    modal.content.add(
      addText(this, 320, 100, 'This deletes your current save.', { size: 22, align: 'center' }).setOrigin(0.5),
    );
    modal.content.add(new Button(this, 200, 190, 'Delete & start', () => this.startNew(), { width: 220 }));
    modal.content.add(new Button(this, 440, 190, 'Cancel', () => modal.close(), { width: 220 }));
  }
}
