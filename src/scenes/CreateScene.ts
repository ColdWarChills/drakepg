import Phaser from 'phaser';
import { COLORS, FONT } from '../config';
import { CLASSES } from '../data/classes';
import { db } from '../data/db';
import type { ClassDef, ClassId } from '../data/types';
import { START } from '../data/world';
import { Player, STAT_KEYS, STAT_LABELS } from '../game/Player';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys } from './keys';

const NAMES = ['Aldric', 'Brynn', 'Cassia', 'Dorian', 'Elowen', 'Fenwick', 'Gwyn', 'Harlan', 'Isolde', 'Jory', 'Kestrel', 'Lorne', 'Nyx', 'Orrin', 'Perrin', 'Quill', 'Rowan', 'Sable', 'Tamsin', 'Ulric', 'Vesper', 'Wren'];
const MAX_NAME = 14;
const CARD_W = 340;
const CARD_H = 400;
const CARD_Y = 210;

interface Card {
  cls: ClassDef;
  box: Phaser.GameObjects.Rectangle;
  group: Phaser.GameObjects.Container;
  fig: Phaser.GameObjects.Container;
}

/** New game: pick a name and a class. */
export class CreateScene extends Phaser.Scene {
  private nameInput!: HTMLInputElement;
  private cards: Card[] = [];
  private selected: ClassId = 'warrior';
  private starting = false;

  constructor() {
    super(SceneKeys.Create);
  }

  create(): void {
    enterScene(this);
    this.cards = [];
    this.starting = false;
    // Room scenes capture WASD for movement; let them be typed into the name box here.
    this.input.keyboard?.disableGlobalCapture();
    const { width, height } = this.scale;

    const sky = this.add.graphics();
    sky.fillGradientStyle(0x2a2856, 0x2a2856, 0x0a0e23, 0x0a0e23, 1);
    sky.fillRect(0, 0, width, height);
    fx.starField(this, 60, height);

    addText(this, width / 2, 50, 'Create Your Hero', { size: 44, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5);
    addText(this, width / 2 - 260, 140, 'Name', { size: 22, color: COLORS.textDim }).setOrigin(1, 0.5);
    this.buildNameInput(width / 2 - 60, 140);
    new Button(this, width / 2 + 230, 140, 'Random', () => this.randomName(), { width: 120, height: 44, fontSize: 18 });

    CLASSES.forEach((cls, i) => this.buildCard(cls, width / 2 + (i - 1) * (CARD_W + 30)));
    this.select('warrior', false);

    new Button(this, width / 2, 668, 'Begin Adventure', () => this.begin(), { width: 300, height: 60, fontSize: 24 });
    new Button(this, 80, 40, 'Back', () => goTo(this, SceneKeys.Title), { width: 120, height: 44, fontSize: 18 });

    // Clicking anywhere in the game takes focus out of the name box, so number keys pick classes again.
    this.input.on('pointerdown', () => this.nameInput.blur());
    const kb = this.input.keyboard!;
    const typing = () => document.activeElement === this.nameInput;
    kb.on('keydown-ONE', () => !typing() && this.select('warrior'));
    kb.on('keydown-TWO', () => !typing() && this.select('mage'));
    kb.on('keydown-THREE', () => !typing() && this.select('rogue'));
    kb.on('keydown-ENTER', () => this.begin());
    kb.on('keydown-LEFT', () => !typing() && this.step(-1));
    kb.on('keydown-RIGHT', () => !typing() && this.step(1));
  }

  private buildNameInput(x: number, y: number): void {
    const el = document.createElement('input');
    el.type = 'text';
    el.maxLength = MAX_NAME;
    el.placeholder = 'Hero';
    el.autocomplete = 'off';
    el.spellcheck = false;
    el.style.cssText = [
      'width: 340px',
      'height: 46px',
      'box-sizing: border-box',
      `font: 24px ${FONT}`,
      'text-align: center',
      'color: #f3e9d2',
      'background: #1d1826',
      'border: 2px solid #8a6d3b',
      'border-radius: 10px',
      'outline: none',
    ].join(';');
    el.addEventListener('focus', () => (el.style.borderColor = '#f1c40f'));
    el.addEventListener('blur', () => (el.style.borderColor = '#8a6d3b'));
    // Keep Phaser's key handlers from reacting to typing.
    el.addEventListener('keydown', (e) => {
      e.stopPropagation();
      if (e.key === 'Enter') this.begin();
    });
    this.nameInput = el;
    this.add.dom(x, y, el);
  }

  private randomName(): void {
    this.nameInput.value = Phaser.Utils.Array.GetRandom(NAMES);
  }

  private buildCard(cls: ClassDef, x: number): void {
    const group = this.add.container(x, CARD_Y + CARD_H / 2);
    const box = this.add.rectangle(0, 0, CARD_W, CARD_H, COLORS.panel, 0.92).setRounded(14).setStrokeStyle(3, COLORS.panelBorder);
    box.setInteractive({ useHandCursor: true }).on('pointerup', () => this.select(cls.id));
    const fig = drawFigure(this, 0, -CARD_H / 2 + 215, cls.look, 1).setScale(cls.look.scale * 0.72);
    const name = addText(this, 0, 30, cls.name, { size: 30, bold: true, color: COLORS.textGold }).setOrigin(0.5);
    const tag = addText(this, 0, 66, cls.tagline, { size: 17, align: 'center', wrap: CARD_W - 40, color: COLORS.text }).setOrigin(0.5);
    const stats = addText(this, 0, 106, STAT_KEYS.map((k) => `${STAT_LABELS[k]} ${cls.stats[k]}`).join('   '), {
      size: 16,
      color: COLORS.textDim,
    }).setOrigin(0.5);
    const skills = cls.skills.map((s) => `${db.skill(s.skillId).name}${s.level > 1 ? ` (Lv ${s.level})` : ''}`).join('  ·  ');
    const skillText = addText(this, 0, 150, skills, { size: 15, align: 'center', wrap: CARD_W - 40, color: COLORS.textMagic }).setOrigin(0.5);
    group.add([box, fig, name, tag, stats, skillText]);
    this.cards.push({ cls, box, group, fig });
  }

  private step(dir: number): void {
    const i = CLASSES.findIndex((c) => c.id === this.selected);
    this.select(CLASSES[(i + dir + CLASSES.length) % CLASSES.length].id);
  }

  private select(id: ClassId, feedback = true): void {
    this.selected = id;
    for (const card of this.cards) {
      const on = card.cls.id === id;
      this.tweens.killTweensOf([card.group, card.fig]);
      card.box.setStrokeStyle(on ? 4 : 3, on ? 0xf1c40f : COLORS.panelBorder);
      this.tweens.add({ targets: card.group, scale: on ? 1.04 : 0.96, alpha: on ? 1 : 0.7, duration: 180, ease: 'Back.easeOut' });
      if (on && feedback) {
        this.tweens.add({ targets: card.fig, y: card.fig.y - 16, duration: 140, yoyo: true, ease: 'Quad.easeOut' });
        fx.burst(this, card.group.x, CARD_Y + 120, 'fx_star', 10, {
          speed: { min: 60, max: 200 },
          scale: { start: 0.8, end: 0 },
          lifespan: 500,
          tint: 0xffe066,
          blendMode: 'ADD',
        });
      }
    }
    if (feedback) sfx.play(this, 'open');
  }

  private begin(): void {
    if (this.starting) return;
    this.starting = true;
    const name = (this.nameInput.value.trim() || this.nameInput.placeholder).slice(0, MAX_NAME);
    this.nameInput.blur();
    SaveSystem.clear();
    const player = Player.createNew(name, this.selected);
    session.start(player);
    SaveSystem.save(player);
    sfx.play(this, 'quest');
    goTo(this, SceneKeys.Room, { ...START, via: 'spawn', intro: true }, 400);
  }
}
