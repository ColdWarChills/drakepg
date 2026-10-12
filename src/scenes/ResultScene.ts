import Phaser from 'phaser';
import { COLORS } from '../config';
import { db } from '../data/db';
import { session } from '../game/session';
import { Button } from '../ui/Button';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type ResultSceneData } from './keys';

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
    this.add
      .rectangle(cx, height / 2, 720, 520, COLORS.panel)
      .setRounded(16)
      .setStrokeStyle(3, COLORS.panelBorder);

    addText(this, cx, 170, won ? 'Victory!' : 'Defeated', {
      size: 64,
      bold: true,
      color: won ? COLORS.textGold : COLORS.textBad,
      stroke: true,
    }).setOrigin(0.5);

    if (data.outcome === 'victory') {
      const { rewards, levelsGained } = data;
      const loot = rewards.items.length ? rewards.items.map((id) => db.item(id).name).join(', ') : 'nothing';
      const lines = [
        `You defeated the ${data.monsterName}.`,
        '',
        `+${rewards.xp} XP`,
        `+${rewards.gold} gold`,
        `Loot: ${loot}`,
      ];
      addText(this, cx, 320, lines.join('\n'), { size: 26, align: 'center' }).setOrigin(0.5).setLineSpacing(6);

      if (levelsGained > 0) {
        const banner = addText(this, cx, 445, `LEVEL UP!  You are now level ${player.level}`, {
          size: 32,
          bold: true,
          color: COLORS.textGood,
          stroke: true,
        }).setOrigin(0.5);
        this.tweens.add({ targets: banner, scale: 1.08, yoyo: true, repeat: -1, duration: 500 });
      } else {
        addText(this, cx, 445, `XP ${player.xp} / ${player.xpNeeded}`, { size: 20, color: COLORS.textDim })
          .setOrigin(0.5);
      }

      new Button(this, cx - 150, 540, 'Fight again', () => goTo(this, SceneKeys.Battle, { zoneId: data.zoneId }), {
        width: 260,
        height: 64,
      });
      new Button(this, cx + 150, 540, 'Return to town', () => goTo(this, SceneKeys.Town), { width: 260, height: 64 });
    } else {
      const lines = [`The ${data.monsterName} got the better of you.`, '', 'You wake up at the inn, fully rested.'];
      addText(this, cx, 330, lines.join('\n'), { size: 26, align: 'center' }).setOrigin(0.5).setLineSpacing(6);
      new Button(this, cx, 520, 'Return to town', () => goTo(this, SceneKeys.Town), { width: 280, height: 64 });
    }
  }
}
