import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { isGearDef, SIGIL_NAMES, SIGIL_TIERS, type ItemGrant, type SigilTier } from '../game/Items';
import { session } from '../game/session';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import { addSigilGlow, drawItemIcon, GRADE_COLORS, gradeInfo, gradeLabel, itemName, SIGIL_COLORS } from '../ui/itemUi';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type ResultSceneData, type RoomSceneData } from './keys';

/** "Mossling" -> "the Mossling", "The Thornheart" -> "the Thornheart" (for use mid-sentence; capitalize() at the start). */
function the(name: string): string {
  return /^the /i.test(name) ? `the ${name.slice(4)}` : `the ${name}`;
}

/** How a drop is revealed: plain, by grade (Fine and up), or by Sigil. */
type RevealTier = 'plain' | 'fine' | 'superior' | 'masterwork' | SigilTier;
const REVEAL_ORDER: RevealTier[] = ['plain', 'fine', 'superior', 'masterwork', 'dawn', 'dusk', 'eclipse'];

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
      addText(this, cx, 240, `You defeated ${the(data.monsterName)}.`, { size: 24 }).setOrigin(0.5);
      const xpText = addText(this, cx - 120, 288, '+0 XP', { size: 30, bold: true, color: COLORS.textGood }).setOrigin(0.5);
      const goldText = addText(this, cx + 120, 288, '+0 gold', { size: 30, bold: true, color: COLORS.textGold }).setOrigin(0.5);
      this.dropReveal(cx, 360, data.rewards.items, data.lost);
      data.quests.forEach((q, i) => {
        const done = q.count >= q.target;
        const line = addText(this, cx, 450 + i * 28, `${q.name}: ${q.goal} ${q.count}/${q.target}${done ? '  ✔ Return to the quest giver' : ''}`, {
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

      if (levelsGained > 0) {
        const hero = drawFigure(this, cx - 280, 490 + shift, player.look, 1, player.weapon, player.weaponSigil).setScale(0.55);
        this.time.delayedCall(1200, () => {
          fx.levelUpBurst(this, hero.x, hero.y);
          sfx.play(this, 'levelup');
          this.cameras.main.shake(200, 0.006);
          const extras = [`+${data.levelsGained * BALANCE.statPointsPerLevel} stat points (press C)`, ...data.newSkills.map((n) => `New skill: ${n}`)];
          addText(this, cx + 40, 507 + shift, extras.join('   ·   '), { size: 18, color: COLORS.textMagic, bold: true }).setOrigin(0.5);
          const banner = addText(this, cx + 40, 474 + shift, `LEVEL UP!  Now level ${player.level}`, {
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
        addText(this, cx, 480 + shift, `XP ${player.xp} / ${player.xpNeeded}`, { size: 20, color: COLORS.textDim }).setOrigin(0.5);
      }

      this.continueButton('Continue', data.back, 568 + Math.min(shift, 20));
    } else {
      this.cameras.main.shake(300, 0.006);
      const lines = [`${capitalize(the(data.monsterName))} got the better of you.`, '', `You wake up in ${db.room(data.wake.roomId).name}, fully rested.`];
      if (data.grit) {
        const max = data.grit >= BALANCE.grit.max ? ' (max)' : '';
        lines.push('', `Grit: +${Math.round(data.grit * 100)}% damage against ${the(data.monsterName)} next time${max}.`);
      }
      addText(this, cx, 330, lines.join('\n'), { size: 26, align: 'center' }).setOrigin(0.5).setLineSpacing(6);
      this.continueButton('Wake up', { ...data.wake, via: 'spawn' }, 520);
    }
  }

  /**
   * The drops as cards (icon and name in its grade or Sigil color) that pop in one by one, best last.
   * Plain drops just pop; Superior and Masterwork glow first; Sigils get a long charge-up and their fanfare
   * (timings in `BALANCE.dropReveal`). Loot that didn't fit the bag is greyed out.
   */
  private dropReveal(cx: number, y: number, items: ItemGrant[], lost: ItemGrant[]): void {
    const r = BALANCE.dropReveal;
    // Gear one card each; stacking items (potions, materials) as one card "x2".
    const shown: { g: ItemGrant; qty: number; isLost: boolean }[] = [];
    for (const g of items) {
      const isLost = lost.includes(g);
      const same = isGearDef(db.item(g.itemId)) ? undefined : shown.find((e) => e.g.itemId === g.itemId && e.isLost === isLost);
      if (same) same.qty++;
      else shown.push({ g, qty: 1, isLost });
    }
    if (!shown.length) {
      const none = addText(this, cx, y, 'No loot this time', { size: 22, color: COLORS.textDim }).setOrigin(0.5).setAlpha(0);
      this.tweens.add({ targets: none, alpha: 1, delay: r.start, duration: 300 });
      return;
    }
    const cards = shown.map(({ g, qty, isLost }) => {
      const def = db.item(g.itemId);
      const info = gradeInfo(def, g);
      const tier: RevealTier = isLost ? 'plain' : (info.sigil ?? (isGearDef(def) && info.tier !== 'standard' && info.tier !== 'fair' ? info.tier : 'plain'));
      return { def, g, qty, isLost, info, tier };
    });
    cards.sort((a, b) => REVEAL_ORDER.indexOf(a.tier) - REVEAL_ORDER.indexOf(b.tier));

    const step = 128;
    const scale = Math.min(1, 680 / (cards.length * step));
    const row = this.add.container(cx, y).setScale(scale);
    let at = r.start;
    cards.forEach((c, i) => {
      const x = (i - (cards.length - 1) / 2) * step;
      const card = this.add.container(x, 0).setScale(0);
      const frame = this.add.rectangle(0, -10, 64, 64, 0x000000, 0.35).setRounded(8).setStrokeStyle(3, c.isLost ? 0x555555 : c.info.frame);
      const icon = drawItemIcon(this, c.def, 52).setPosition(0, -10);
      const name = `${itemName(c.def, c.g)}${c.qty > 1 ? ` x${c.qty}` : ''}`;
      const label = addText(this, 0, 34, name, {
        size: 15,
        bold: c.tier !== 'plain',
        align: 'center',
        wrap: 124,
        color: c.isLost ? COLORS.textDim : c.info.sigil ? c.info.nameCss : c.info.tier === 'standard' && isGearDef(c.def) ? COLORS.text : c.info.css,
      }).setOrigin(0.5, 0);
      card.add([frame, icon, label]);
      // "Fine 52%" / "Sigil of Dusk" under gear names.
      if (isGearDef(c.def)) {
        const sub = c.info.sigil ? SIGIL_NAMES[c.info.sigil] : gradeLabel(c.info);
        card.add(addText(this, 0, 36 + label.height, sub, { size: 13, color: COLORS.textDim }).setOrigin(0.5, 0));
      }
      if (c.isLost) card.add(addText(this, 0, -10, 'Bag full', { size: 13, bold: true, color: COLORS.textBad }).setOrigin(0.5));
      if (c.info.sigil && !c.isLost) addSigilGlow(this, icon, c.info.sigil, 0.6);
      row.add(card);

      const windup = r[c.tier];
      const pop = at + windup;
      if (windup > 0) this.time.delayedCall(at, () => this.windUp(row, x, c.tier, windup));
      this.time.delayedCall(pop, () => this.popCard(row, card, c.tier, c.info.frame, label));
      at = pop + r.gap;
    });
  }

  /** The build-up before a good drop: a glow (Superior, Masterwork) or a shaking charge in the Sigil's color. */
  private windUp(row: Phaser.GameObjects.Container, x: number, tier: RevealTier, ms: number): void {
    const p = row.getWorldTransformMatrix().transformPoint(x, -10);
    const sigil = tier === 'dawn' || tier === 'dusk' || tier === 'eclipse';
    const color = sigil ? SIGIL_COLORS[tier].color : GRADE_COLORS[tier === 'masterwork' ? 'masterwork' : 'superior'].color;
    sfx.play(this, 'charge');
    void fx.chargeUp(this, p.x, p.y, color, ms);
    if (sigil) {
      // Heavier tiers rumble harder as they charge.
      this.cameras.main.shake(ms, tier === 'eclipse' ? 0.006 : tier === 'dusk' ? 0.004 : 0.002);
      if (ms > 700) this.time.delayedCall(ms / 2, () => sfx.play(this, 'charge'));
    }
  }

  private popCard(row: Phaser.GameObjects.Container, card: Phaser.GameObjects.Container, tier: RevealTier, color: number, label: Phaser.GameObjects.Text): void {
    const big = tier !== 'plain' && tier !== 'fine';
    this.tweens.chain({
      targets: card,
      tweens: [
        { scale: big ? 1.3 : 1.12, duration: big ? 200 : 140, ease: 'Back.easeOut' },
        { scale: 1, duration: 140 },
      ],
    });
    const p = row.getWorldTransformMatrix().transformPoint(card.x, -10);
    if (tier === 'plain') {
      sfx.play(this, 'click');
      return;
    }
    if (tier === 'fine' || tier === 'superior' || tier === 'masterwork') {
      sfx.play(this, tier === 'fine' ? 'coin' : 'rare');
      const master = tier === 'masterwork';
      fx.burst(this, p.x, p.y, 'fx_star', master ? 36 : tier === 'superior' ? 20 : 10, {
        speed: { min: 60, max: master ? 320 : 200 },
        scale: { start: master ? 1 : 0.7, end: 0 },
        lifespan: master ? 1000 : 700,
        tint: color,
        blendMode: 'ADD',
      });
      if (master) this.cameras.main.flash(250, 255, 230, 140);
      return;
    }
    this.sigilFanfare([{ text: label, tier }]);
  }

  /** Burst in the Sigil's color over its name; the best tier also gets a sound, a flash and a banner. */
  private sigilFanfare(sigils: { text: Phaser.GameObjects.Text; tier: SigilTier }[]): void {
    const best = sigils.reduce((a, b) => (SIGIL_TIERS.indexOf(b.tier) > SIGIL_TIERS.indexOf(a.tier) ? b : a)).tier;
    const eclipse = best === 'eclipse';
    sfx.play(this, eclipse ? 'eclipse' : 'sigil');
    for (const s of sigils) {
      const b = s.text.getBounds();
      const color = SIGIL_COLORS[s.tier].color;
      const big = s.tier !== 'dawn';
      fx.shockwave(this, b.centerX, b.centerY, color, big ? 2.4 : 1.6, 0.35);
      fx.burst(this, b.centerX, b.centerY, 'fx_star', s.tier === 'eclipse' ? 60 : big ? 36 : 24, {
        speed: { min: 80, max: s.tier === 'eclipse' ? 420 : 280 },
        scale: { start: big ? 1.1 : 0.8, end: 0 },
        lifespan: s.tier === 'eclipse' ? 1400 : 1000,
        tint: [color, 0xffffff],
        blendMode: 'ADD',
      });
      this.tweens.add({ targets: s.text, alpha: 0.35, duration: 140, yoyo: true, repeat: 3 });
    }
    const c = Phaser.Display.Color.IntegerToColor(SIGIL_COLORS[best].color);
    this.cameras.main.flash(eclipse ? 600 : 300, c.red, c.green, c.blue);
    if (eclipse) this.cameras.main.shake(400, 0.008);
    const banner = addText(this, this.scale.width / 2, 62, `${SIGIL_NAMES[best].toUpperCase()}!`, {
      size: eclipse ? 46 : 36,
      bold: true,
      color: SIGIL_COLORS[best].css,
      stroke: true,
    })
      .setOrigin(0.5)
      .setScale(0)
      .setDepth(10);
    this.tweens.chain({
      targets: banner,
      tweens: [
        { scale: 1.25, duration: 220, ease: 'Back.easeOut' },
        { scale: 1, duration: 160 },
        { alpha: 0, delay: eclipse ? 2200 : 1600, duration: 500 },
      ],
    });
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
