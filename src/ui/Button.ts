import Phaser from 'phaser';
import { COLORS } from '../config';
import { addText } from './text';

export interface ButtonOptions {
  width?: number;
  height?: number;
  fontSize?: number;
  color?: number;
  /** Optional second line, e.g. a price or MP cost. */
  sublabel?: string;
}

/** Rounded rectangle button. Works with mouse and touch. */
export class Button extends Phaser.GameObjects.Container {
  private readonly bg: Phaser.GameObjects.Rectangle;
  private readonly label: Phaser.GameObjects.Text;
  private readonly sub?: Phaser.GameObjects.Text;
  private readonly baseColor: number;
  private enabled = true;

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
    this.bg.on('pointerout', () => this.enabled && this.bg.setFillStyle(this.baseColor));
    this.bg.on('pointerdown', () => this.enabled && this.bg.setFillStyle(COLORS.buttonDown));
    this.bg.on('pointerup', () => {
      if (!this.enabled) return;
      this.bg.setFillStyle(COLORS.buttonHover);
      this.onClick();
    });

    scene.add.existing(this);
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

  /** Forward hover so lists can show item descriptions. */
  onHover(over: () => void, out?: () => void): this {
    this.bg.on('pointerover', over);
    if (out) this.bg.on('pointerout', out);
    return this;
  }
}
