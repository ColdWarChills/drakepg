import Phaser from 'phaser';
import { COLORS } from '../config';
import { addText } from './text';

/** HP / MP / XP bar with a "cur / max" label. Origin is the left edge, vertically centered. */
export class Bar extends Phaser.GameObjects.Container {
  private readonly fill: Phaser.GameObjects.Rectangle;
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
    this.fill = scene.add.rectangle(2, 0, width - 4, height - 4, color).setOrigin(0, 0.5);
    this.label = addText(scene, width / 2, 0, '', { size: Math.max(12, height - 8), bold: true, stroke: true })
      .setOrigin(0.5);
    this.add([bg, this.fill, this.label]);
    scene.add.existing(this);
  }

  setValue(current: number, max: number, animate = true): this {
    const ratio = max > 0 ? Phaser.Math.Clamp(current / max, 0, 1) : 0;
    const target = Math.max(0, (this.barWidth - 4) * ratio);
    this.scene.tweens.killTweensOf(this.fill);
    if (animate) {
      this.scene.tweens.add({ targets: this.fill, width: target, duration: 300, ease: 'Cubic.easeOut' });
    } else {
      this.fill.width = target;
    }
    this.label.setText(`${this.prefix}${Math.max(0, Math.round(current))} / ${max}`);
    return this;
  }
}
