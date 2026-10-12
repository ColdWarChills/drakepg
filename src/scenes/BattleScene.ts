import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import type { ConsumableDef, ZoneDef } from '../data/types';
import { Battle, rollEncounter, type BattleEvent, type Side } from '../game/Battle';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { addBackground } from '../ui/art';
import { Bar } from '../ui/Bar';
import { Button } from '../ui/Button';
import { drawFigure, PLAYER_LOOK } from '../ui/figures';
import { restoreText } from '../ui/panels';
import { addText, floatText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type ResultSceneData } from './keys';

const GROUND_Y = 500;
const PLAYER_X = 330;
const ENEMY_X = 950;
const LOG_LINES = 4;

type Menu = 'main' | 'skills' | 'items';

export class BattleScene extends Phaser.Scene {
  private battle!: Battle;
  private zone!: ZoneDef;
  private playerFig!: Phaser.GameObjects.Container;
  private enemyFig!: Phaser.GameObjects.Container;
  private playerHp!: Bar;
  private playerMp!: Bar;
  private enemyHp!: Bar;
  private logText!: Phaser.GameObjects.Text;
  private log: string[] = [];
  private menu!: Phaser.GameObjects.Container;
  private busy = false;

  constructor() {
    super(SceneKeys.Battle);
  }

  create(data: BattleSceneData): void {
    enterScene(this);
    this.zone = db.zone(data.zoneId);
    const player = session.player;
    const monster = rollEncounter(this.zone, player.level);
    this.battle = new Battle(player, monster);
    this.log = [];
    this.busy = false;

    this.drawBackdrop();
    this.playerFig = drawFigure(this, PLAYER_X, GROUND_Y, PLAYER_LOOK, 1);
    this.enemyFig = drawFigure(this, ENEMY_X, GROUND_Y, monster.look, -1);
    this.tweens.add({
      targets: this.enemyFig,
      scaleY: monster.look.scale * 1.03,
      yoyo: true,
      repeat: -1,
      duration: 900,
      ease: 'Sine.easeInOut',
    });

    this.drawStatus();
    this.drawBottomPanel();
    this.refreshBars(false);
    this.pushLog(`A wild ${monster.name} (Lv ${monster.level}) appears in the ${this.zone.name}!`);
    this.showMenu('main');
  }

  // --- Layout ---

  private drawBackdrop(): void {
    if (addBackground(this, this.zone.background)) return;
    const { width, height } = this.scale;
    this.add.rectangle(0, 0, width, height, this.zone.skyColor).setOrigin(0);
    // Tree line.
    for (let i = 0; i < 14; i++) {
      const x = i * 100 + (i % 2) * 30;
      const h = 200 + ((i * 37) % 80);
      this.add.rectangle(x, GROUND_Y - 40, 26, h, 0x3b2a1e).setOrigin(0.5, 1);
      this.add.circle(x, GROUND_Y - 40 - h, 70 + ((i * 13) % 30), 0x1e3a24, 0.95);
    }
    this.add.rectangle(0, GROUND_Y - 40, width, height, this.zone.groundColor).setOrigin(0);
  }

  private drawStatus(): void {
    const p = this.battle.player;
    const m = this.battle.enemy.def;
    this.statusPanel(16, 16, 420, 120);
    addText(this, 32, 26, `${p.name}  ·  Lv ${p.level}`, { size: 22, bold: true });
    this.playerHp = new Bar(this, 32, 74, 388, 26, COLORS.hp, 'HP ');
    this.playerMp = new Bar(this, 32, 108, 388, 22, COLORS.mp, 'MP ');

    const ex = this.scale.width - 436;
    this.statusPanel(ex, 16, 420, 90);
    addText(this, ex + 16, 26, `${m.name}  ·  Lv ${m.level}`, { size: 22, bold: true });
    this.enemyHp = new Bar(this, ex + 16, 76, 388, 26, COLORS.hp, 'HP ');
  }

  private statusPanel(x: number, y: number, w: number, h: number): void {
    this.add.rectangle(x, y, w, h, COLORS.panel, 0.9).setOrigin(0).setRounded(10).setStrokeStyle(2, COLORS.panelBorder);
  }

  private drawBottomPanel(): void {
    const { width, height } = this.scale;
    const top = 540;
    this.add
      .rectangle(8, top, width - 16, height - top - 8, COLORS.panel, 0.95)
      .setOrigin(0)
      .setRounded(12)
      .setStrokeStyle(3, COLORS.panelBorder);
    this.logText = addText(this, 30, top + 18, '', { size: 19, wrap: 560 }).setLineSpacing(6);
    this.menu = this.add.container(0, 0);
  }

  private refreshBars(animate = true): void {
    const p = this.battle.player;
    const e = this.battle.enemy;
    this.playerHp.setValue(p.hp, p.maxHp, animate);
    this.playerMp.setValue(p.mp, p.maxMp, animate);
    this.enemyHp.setValue(e.hp, e.maxHp, animate);
  }

  private pushLog(line: string): void {
    this.log.push(line);
    if (this.log.length > LOG_LINES) this.log.shift();
    this.logText.setText(this.log.join('\n'));
  }

  // --- Menus ---

  private showMenu(menu: Menu): void {
    this.menu.removeAll(true);
    const p = this.battle.player;
    const buttons: { label: string; sub?: string; enabled?: boolean; run: () => void }[] = [];

    if (menu === 'main') {
      buttons.push(
        { label: 'Attack', run: () => this.act(() => this.battle.attack()) },
        { label: 'Skills', run: () => this.showMenu('skills') },
        { label: 'Items', run: () => this.showMenu('items') },
        { label: 'Flee', sub: `${Math.round(this.battle.fleeChance * 100)}%`, run: () => this.act(() => this.battle.flee()) },
      );
    } else if (menu === 'skills') {
      for (const skill of p.knownSkills) {
        buttons.push({
          label: skill.name,
          sub: `${skill.mpCost} MP`,
          enabled: this.battle.canUseSkill(skill.id),
          run: () => this.act(() => this.battle.useSkill(skill.id)),
        });
      }
      buttons.push({ label: 'Back', run: () => this.showMenu('main') });
    } else {
      const usable = p.data.inventory.filter((e) => db.item(e.itemId).type === 'consumable').slice(0, 5);
      for (const entry of usable) {
        const item = db.item(entry.itemId) as ConsumableDef;
        buttons.push({
          label: item.name,
          sub: `x${entry.qty}`,
          run: () => {
            if (!this.battle.canUseItem(item.id)) this.pushLog(`${item.name} would have no effect right now.`);
            else this.act(() => this.battle.useItem(item.id));
          },
        });
      }
      if (usable.length === 0) this.pushLog('You have no usable items.');
      buttons.push({ label: 'Back', run: () => this.showMenu('main') });
    }

    // 3 x 2 grid on the right side of the bottom panel.
    const cols = 3;
    const bw = 196;
    const bh = 66;
    const x0 = 650;
    const y0 = 590;
    buttons.forEach((b, i) => {
      const x = x0 + (i % cols) * (bw + 10) + bw / 2;
      const y = y0 + Math.floor(i / cols) * (bh + 10);
      const btn = new Button(this, x, y, b.label, b.run, { width: bw, height: bh, sublabel: b.sub, fontSize: 20 });
      if (b.enabled === false) btn.setEnabled(false);
      this.menu.add(btn);
    });
  }

  // --- Turn playback ---

  private async act(action: () => BattleEvent[]): Promise<void> {
    if (this.busy) return;
    const events = action();
    if (events.length === 0) return;
    this.busy = true;
    this.menu.removeAll(true);
    for (const event of events) await this.play(event);
    this.refreshBars();

    if (this.battle.isOver) {
      await this.wait(700);
      this.finish();
    } else {
      this.busy = false;
      this.showMenu('main');
    }
  }

  private async play(e: BattleEvent): Promise<void> {
    const name = (side: Side) => (side === 'player' ? 'You' : this.battle.enemy.def.name);
    const figOf = (side: Side) => (side === 'player' ? this.playerFig : this.enemyFig);
    const other = (side: Side): Side => (side === 'player' ? 'enemy' : 'player');

    switch (e.kind) {
      case 'hit': {
        await this.lunge(e.source);
        const target = figOf(other(e.source));
        this.flash(target);
        if (e.crit) this.cameras.main.shake(150, 0.006);
        floatText(
          this,
          target.x,
          GROUND_Y - 200,
          e.crit ? `${e.damage}!` : `${e.damage}`,
          e.damageType === 'magic' ? COLORS.textMagic : COLORS.textBad,
          e.crit ? 44 : 34,
        );
        this.refreshBars();
        const crit = e.crit ? ' Critical!' : '';
        this.pushLog(
          e.source === 'player'
            ? `You use ${e.move}: ${e.damage} damage.${crit}`
            : `${name('enemy')} uses ${e.move}: ${e.damage} damage.${crit}`,
        );
        await this.wait(450);
        break;
      }
      case 'miss': {
        await this.lunge(e.source);
        floatText(this, figOf(other(e.source)).x, GROUND_Y - 200, 'Miss', COLORS.textDim);
        this.pushLog(`${name(e.source)} ${e.source === 'player' ? 'use' : 'uses'} ${e.move}, but miss${e.source === 'player' ? '' : 'es'}.`);
        await this.wait(450);
        break;
      }
      case 'heal': {
        floatText(this, figOf(e.source).x, GROUND_Y - 200, restoreText(e), COLORS.textGood, 28);
        this.refreshBars();
        this.pushLog(`You use ${e.move}: ${restoreText(e)}.`);
        await this.wait(600);
        break;
      }
      case 'flee': {
        if (e.success) {
          this.pushLog('You got away safely.');
          await this.tween({ targets: this.playerFig, x: -150, duration: 500, ease: 'Cubic.easeIn' });
        } else {
          this.pushLog("You couldn't get away!");
          await this.wait(450);
        }
        break;
      }
      case 'defeated': {
        const fig = figOf(e.side);
        this.tweens.killTweensOf(fig);
        this.pushLog(e.side === 'enemy' ? `${name('enemy')} is defeated!` : 'You collapse...');
        await this.tween({ targets: fig, alpha: 0, y: fig.y + 30, duration: 600 });
        break;
      }
    }
  }

  private lunge(side: Side): Promise<void> {
    const fig = side === 'player' ? this.playerFig : this.enemyFig;
    const dir = side === 'player' ? 1 : -1;
    return this.tween({ targets: fig, x: fig.x + 70 * dir, duration: 140, yoyo: true, ease: 'Quad.easeOut' });
  }

  private flash(fig: Phaser.GameObjects.Container): void {
    this.tweens.add({ targets: fig, alpha: 0.3, duration: 70, yoyo: true, repeat: 2 });
  }

  private tween(config: Phaser.Types.Tweens.TweenBuilderConfig): Promise<void> {
    return new Promise((resolve) => this.tweens.add({ ...config, onComplete: () => resolve() }));
  }

  private wait(ms: number): Promise<void> {
    return new Promise((resolve) => this.time.delayedCall(ms, resolve));
  }

  // --- Outcome ---

  private finish(): void {
    const player = this.battle.player;
    const monsterName = this.battle.enemy.def.name;
    const zoneId = this.zone.id;

    switch (this.battle.state) {
      case 'victory': {
        const rewards = this.battle.rewards ?? { xp: 0, gold: 0, items: [] };
        const levelsGained = player.grantRewards(rewards);
        if (BALANCE.restoreAfterBattle) player.restoreFull();
        SaveSystem.save(player);
        const data: ResultSceneData = { outcome: 'victory', zoneId, monsterName, rewards, levelsGained };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'defeat': {
        // No penalty: wake up at the inn.
        player.restoreFull();
        SaveSystem.save(player);
        const data: ResultSceneData = { outcome: 'defeat', zoneId, monsterName };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'fled':
        SaveSystem.save(player);
        goTo(this, SceneKeys.Town);
        break;
      case 'player_turn':
        break;
    }
  }
}
