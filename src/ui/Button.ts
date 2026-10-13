import Phaser from 'phaser';
import { COLORS } from '../config';
import { play } from './sfx';
import { addText } from './text';

export interface ButtonOptions {
  width?: number;
  height?: number;
  fontSize?: number;
  color?: number;
  /** Optional second line, e.g. a price or MP cost. */
  sublabel?: string;
}

/** Holding a button with a hover text this long on a touch screen reads the text instead of pressing it. */
const LONG_PRESS_MS = 450;

/** Rounded rectangle button. Works with mouse and touch. */
export class Button extends Phaser.GameObjects.Container {
  private readonly bg: Phaser.GameObjects.Rectangle;
  private readonly label: Phaser.GameObjects.Text;
  private readonly sub?: Phaser.GameObjects.Text;
  private readonly baseColor: number;
  private enabled = true;
  private hasHover = false;
  private downAt = 0;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    text: string,
    private readonly onClick: () => void,
    opts: ButtonOptions = {},
  ) {
    super(scene, x, y);
    const width = opts.width ?? 200;
    const height = opts.height ?? 56;
    this.baseColor = opts.color ?? COLORS.button;

    this.bg = scene.add
      .rectangle(0, 0, width, height, this.baseColor)
      .setRounded(10)
      .setStrokeStyle(2, COLORS.panelBorder);
    this.label = addText(scene, 0, opts.sublabel ? -9 : 0, text, {
      size: opts.fontSize ?? 22,
      bold: true,
      align: 'center',
    }).setOrigin(0.5);
    this.add([this.bg, this.label]);

    if (opts.sublabel) {
      this.sub = addText(scene, 0, 14, opts.sublabel, { size: 15, color: COLORS.textDim, align: 'center' }).setOrigin(
        0.5,
      );
      this.add(this.sub);
    }

    this.setSize(width, height);
    this.bg.setInteractive({ useHandCursor: true });
    this.bg.on('pointerover', () => this.enabled && this.bg.setFillStyle(COLORS.buttonHover));
    this.bg.on('pointerout', () => {
      if (this.enabled) this.bg.setFillStyle(this.baseColor);
      this.squash(1);
    });
    this.bg.on('pointerdown', () => {
      this.downAt = scene.time.now;
      if (!this.enabled) return;
      this.bg.setFillStyle(COLORS.buttonDown);
      this.squash(0.93);
    });
    this.bg.on('pointerup', (pointer: Phaser.Input.Pointer) => {
      if (!this.enabled) return;
      this.bg.setFillStyle(COLORS.buttonHover);
      this.squash(1);
      // Touch "hover" lasts while the finger is down (Phaser fires over on touch start, out on touch end),
      // so a long press was for reading the text, not for pressing.
      if (this.hasHover && pointer.wasTouch && scene.time.now - this.downAt >= LONG_PRESS_MS) return;
      play(this.scene, 'click');
      this.onClick();
    });

    scene.add.existing(this);
  }

  private squash(scale: number): void {
    if (!this.scene) return;
    this.scene.tweens.killTweensOf(this);
    this.scene.tweens.add({ targets: this, scale, duration: scale < 1 ? 60 : 180, ease: scale < 1 ? 'Quad.easeOut' : 'Back.easeOut' });
  }

  setEnabled(enabled: boolean): this {
    this.enabled = enabled;
    this.bg.setFillStyle(enabled ? this.baseColor : COLORS.buttonDisabled);
    this.label.setAlpha(enabled ? 1 : 0.45);
    this.sub?.setAlpha(enabled ? 1 : 0.45);
    if (this.bg.input) this.bg.input.cursor = enabled ? 'pointer' : 'default';
    return this;
  }

  setLabel(text: string): this {
    this.label.setText(text);
    return this;
  }

  setSublabel(text: string): this {
    this.sub?.setText(text);
    return this;
  }

  /** Forward hover so lists can show item descriptions. On touch screens, holding the button shows it. */
  onHover(over: () => void, out?: () => void): this {
    this.hasHover = true;
    this.bg.on('pointerover', over);
    if (out) this.bg.on('pointerout', out);
    return this;
  }
}
