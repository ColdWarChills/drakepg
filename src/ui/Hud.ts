import Phaser from 'phaser';
import { COLORS } from '../config';
import type { Player } from '../game/Player';
import { Bar } from './Bar';
import { addText } from './text';

/** Top-left status block: name, level, HP, MP, XP, gold. */
export class Hud extends Phaser.GameObjects.Container {
  private readonly nameText: Phaser.GameObjects.Text;
  private readonly goldText: Phaser.GameObjects.Text;
  private readonly hpBar: Bar;
  private readonly mpBar: Bar;
  private readonly xpBar: Bar;

  constructor(scene: Phaser.Scene, x: number, y: number) {
    super(scene, x, y);
    const bg = scene.add
      .rectangle(0, 0, 340, 150, COLORS.panel, 0.85)
      .setOrigin(0)
      .setRounded(10)
      .setStrokeStyle(2, COLORS.panelBorder);
    this.nameText = addText(scene, 16, 10, '', { size: 22, bold: true });
    this.goldText = addText(scene, 324, 12, '', { size: 20, bold: true, color: COLORS.textGold }).setOrigin(1, 0);
    this.hpBar = new Bar(scene, 16, 58, 308, 24, COLORS.hp, 'HP ');
    this.mpBar = new Bar(scene, 16, 90, 308, 24, COLORS.mp, 'MP ');
    this.xpBar = new Bar(scene, 16, 124, 308, 18, COLORS.xp, 'XP ');
    this.add([bg, this.nameText, this.goldText, this.hpBar, this.mpBar, this.xpBar]);
    this.setDepth(100);
    scene.add.existing(this);
  }

  refresh(player: Player, animate = true): void {
    this.nameText.setText(`${player.name}  ·  Lv ${player.level}`);
    this.goldText.setText(`${player.gold} g`);
    this.hpBar.setValue(player.hp, player.maxHp, animate);
    this.mpBar.setValue(player.mp, player.maxMp, animate);
    this.xpBar.setValue(player.xp, player.xpNeeded, animate);
  }
}
