import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { INN_SPAWN } from '../data/world';
import { session } from '../game/session';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type ResultSceneData, type RoomSceneData } from './keys';

/** "Mossling" -> "the Mossling"; names that already start with "The" are left alone. */
function the(name: string): string {
  return /^the /i.test(name) ? name : `the ${name}`;
}

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export class ResultScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Result);
  }

  create(data: ResultSceneData): void {
    enterScene(this);
    const { width, height } = this.scale;
    const cx = width / 2;
    const player = session.player;
    const won = data.outcome === 'victory';

    this.add.rectangle(0, 0, width, height, won ? 0x1d2a1a : 0x2a1418).setOrigin(0);
    // Slow light rays behind the panel.
    const rays = this.add.graphics().setPosition(cx, 170).setBlendMode(Phaser.BlendModes.ADD).setAlpha(won ? 0.12 : 0.06);
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2;
      rays.fillStyle(won ? 0xffe9a0 : 0xff6b6b, 1);
      rays.fillTriangle(0, 0, Math.cos(a - 0.08) * 900, Math.sin(a - 0.08) * 900, Math.cos(a + 0.08) * 900, Math.sin(a + 0.08) * 900);
    }
    this.tweens.add({ targets: rays, rotation: Math.PI * 2, duration: 40000, repeat: -1 });

    const panel = this.add.rectangle(cx, height / 2, 720, 520, COLORS.panel).setRounded(16).setStrokeStyle(3, COLORS.panelBorder);
    panel.setScale(0.9).setAlpha(0);
    this.tweens.add({ targets: panel, scale: 1, alpha: 1, duration: 300, ease: 'Back.easeOut' });

    const title = addText(this, cx, 170, won ? 'Victory!' : 'Defeated', {
      size: 64,
      bold: true,
      color: won ? COLORS.textGold : COLORS.textBad,
      stroke: true,
    })
      .setOrigin(0.5)
      .setScale(2)
      .setAlpha(0);
    this.tweens.add({ targets: title, scale: 1, alpha: 1, duration: 380, ease: 'Back.easeOut', delay: 100 });

    if (data.outcome === 'victory') {
      const { rewards, levelsGained } = data;
      this.time.delayedCall(250, () => fx.confetti(this));
      sfx.play(this, 'victory');

      // Count-up rows.
      const loot = rewards.items.length ? rewards.items.map((id) => db.item(id).name).join(', ') : 'nothing';
      addText(this, cx, 245, `You defeated ${the(data.monsterName)}.`, { size: 24 }).setOrigin(0.5);
      const xpText = addText(this, cx, 300, '+0 XP', { size: 30, bold: true, color: COLORS.textGood }).setOrigin(0.5);
      const goldText = addText(this, cx, 345, '+0 gold', { size: 30, bold: true, color: COLORS.textGold }).setOrigin(0.5);
      const lootText = addText(this, cx, 390, `Loot: ${loot}`, { size: 24 }).setOrigin(0.5).setAlpha(0);
      data.quests.forEach((q, i) => {
        const done = q.count >= q.target;
        const line = addText(this, cx, 428 + i * 28, `${q.name}: ${q.goal} ${q.count}/${q.target}${done ? '  ✔ Return to the quest giver' : ''}`, {
          size: 19,
          bold: done,
          color: done ? COLORS.textGood : COLORS.textGold,
        })
          .setOrigin(0.5)
          .setAlpha(0);
        this.tweens.add({ targets: line, alpha: 1, delay: 1100 + i * 150, duration: 300 });
        if (done) this.time.delayedCall(1100, () => sfx.play(this, 'quest'));
      });
      const shift = data.quests.length * 28;
      this.countUp(xpText, rewards.xp, ' XP', 400);
      this.countUp(goldText, rewards.gold, ' gold', 650);
      this.tweens.add({ targets: lootText, alpha: 1, delay: 1000, duration: 300 });
      if (rewards.items.length) {
        lootText.setColor(COLORS.textGold);
        this.tweens.add({ targets: lootText, scale: 1.15, delay: 1300, duration: 160, yoyo: true, repeat: 1 });
      }

      if (levelsGained > 0) {
        const hero = drawFigure(this, cx - 280, 470 + shift, player.classDef.look, 1).setScale(0.55);
        this.time.delayedCall(1200, () => {
          fx.levelUpBurst(this, hero.x, hero.y);
          sfx.play(this, 'levelup');
          this.cameras.main.shake(200, 0.006);
          const extras = [`+${data.levelsGained * BALANCE.statPointsPerLevel} stat points (press C)`, ...data.newSkills.map((n) => `New skill: ${n}`)];
          addText(this, cx + 40, 485 + shift, extras.join('   ·   '), { size: 18, color: COLORS.textMagic, bold: true }).setOrigin(0.5);
          const banner = addText(this, cx + 40, 450 + shift, `LEVEL UP!  Now level ${player.level}`, {
            size: 32,
            bold: true,
            color: COLORS.textGood,
            stroke: true,
          })
            .setOrigin(0.5)
            .setScale(0);
          this.tweens.chain({
            targets: banner,
            tweens: [
              { scale: 1.2, duration: 200, ease: 'Back.easeOut' },
              { scale: 1, duration: 150 },
              { scale: 1.06, duration: 500, yoyo: true, repeat: -1 },
            ],
          });
        });
      } else {
        addText(this, cx, 455 + shift, `XP ${player.xp} / ${player.xpNeeded}`, { size: 20, color: COLORS.textDim }).setOrigin(0.5);
      }

      this.continueButton('Continue', data.back, 560 + Math.min(shift, 20));
    } else {
      this.cameras.main.shake(300, 0.006);
      const lines = [`${capitalize(the(data.monsterName))} got the better of you.`, '', 'You wake up at the inn, fully rested.'];
      addText(this, cx, 330, lines.join('\n'), { size: 26, align: 'center' }).setOrigin(0.5).setLineSpacing(6);
      this.continueButton('Wake up', { ...INN_SPAWN, via: 'spawn' }, 520);
    }
  }

  private countUp(text: Phaser.GameObjects.Text, to: number, suffix: string, delay: number): void {
    const counter = { v: 0 };
    this.tweens.add({
      targets: counter,
      v: to,
      delay,
      duration: 500,
      ease: 'Cubic.easeOut',
      onUpdate: () => text.setText(`+${Math.round(counter.v)}${suffix}`),
      onComplete: () => {
        if (to > 0) sfx.play(this, 'coin');
        this.tweens.add({ targets: text, scale: 1.25, duration: 90, yoyo: true });
      },
    });
  }

  private continueButton(label: string, back: RoomSceneData, y: number): void {
    let gone = false;
    const go = () => {
      if (gone) return;
      gone = true;
      goTo(this, SceneKeys.Room, back);
    };
    const btn = new Button(this, this.scale.width / 2, y, label, go, { width: 280, height: 64 });
    btn.setAlpha(0);
    this.tweens.add({ targets: btn, alpha: 1, delay: 700, duration: 300 });
    const kb = this.input.keyboard!;
    this.time.delayedCall(700, () => {
      kb.once('keydown-SPACE', go);
      kb.once('keydown-ENTER', go);
      kb.once('keydown-E', go);
    });
  }
}
