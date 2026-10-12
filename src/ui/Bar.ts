import Phaser from 'phaser';
import { COLORS } from '../config';
import { addText } from './text';

/** HP / MP / XP bar with a "cur / max" label. Origin is the left edge, vertically centered. */
export class Bar extends Phaser.GameObjects.Container {
  private readonly fill: Phaser.GameObjects.Rectangle;
  /** Pale trail that lags behind the fill when the value drops. */
  private readonly chip: Phaser.GameObjects.Rectangle;
  private readonly label: Phaser.GameObjects.Text;
  private readonly barWidth: number;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    width: number,
    height: number,
    color: number,
    private readonly prefix = '',
  ) {
    super(scene, x, y);
    this.barWidth = width;
    const bg = scene.add.rectangle(0, 0, width, height, COLORS.barBg).setOrigin(0, 0.5).setStrokeStyle(2, 0x000000);
    this.chip = scene.add.rectangle(2, 0, width - 4, height - 4, 0xfff4d6, 0.85).setOrigin(0, 0.5);
    this.fill = scene.add.rectangle(2, 0, width - 4, height - 4, color).setOrigin(0, 0.5);
    const shine = scene.add.rectangle(2, -height / 4, width - 4, (height - 4) / 3, 0xffffff, 0.18).setOrigin(0, 0.5);
    this.label = addText(scene, width / 2, 0, '', { size: Math.max(12, height - 8), bold: true, stroke: true })
      .setOrigin(0.5);
    this.add([bg, this.chip, this.fill, shine, this.label]);
    scene.add.existing(this);
  }

  setValue(current: number, max: number, animate = true): this {
    const ratio = max > 0 ? Phaser.Math.Clamp(current / max, 0, 1) : 0;
    const target = Math.max(0, (this.barWidth - 4) * ratio);
    const dropping = target < this.fill.width;
    this.scene.tweens.killTweensOf([this.fill, this.chip]);
    if (!animate) {
      this.fill.width = target;
      this.chip.width = target;
    } else if (dropping) {
      this.scene.tweens.add({ targets: this.fill, width: target, duration: 120, ease: 'Cubic.easeOut' });
      this.scene.tweens.add({ targets: this.chip, width: target, delay: 350, duration: 450, ease: 'Cubic.easeIn' });
      this.scene.tweens.add({ targets: this, x: this.x + 3, duration: 40, yoyo: true, repeat: 2 });
    } else {
      this.chip.width = target;
      this.scene.tweens.add({ targets: this.fill, width: target, duration: 400, ease: 'Cubic.easeOut' });
    }
    this.label.setText(`${this.prefix}${Math.max(0, Math.round(current))} / ${max}`);
    return this;
  }
}
