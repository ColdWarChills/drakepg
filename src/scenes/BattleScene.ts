import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import type { ConsumableDef, MonsterDef, SkillFx, ZoneDef } from '../data/types';
import { Battle, type BattleEvent, type Side } from '../game/Battle';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { drawScenery } from '../ui/backdrops';
import { Bar } from '../ui/Bar';
import { Button } from '../ui/Button';
import { drawFigure, PLAYER_LOOK } from '../ui/figures';
import * as fx from '../ui/fx';
import { restoreText } from '../ui/panels';
import { addText, floatText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type ResultSceneData, type RoomSceneData } from './keys';

const GROUND_Y = 500;
const PLAYER_X = 330;
const ENEMY_X = 950;
const LOG_LINES = 4;
/** Height above the feet where hits land. */
const CHEST = 120;

const FIRE = [0xfff2a8, 0xffa733, 0xff5a1f, 0x8a1c00];
const ARCANE = [0xe6f0ff, 0x9fb7ff, 0x7a5cff];

type Menu = 'main' | 'skills' | 'items';
type MenuButton = { label: string; sub?: string; enabled?: boolean; run: () => void };

export class BattleScene extends Phaser.Scene {
  private battle!: Battle;
  private zone!: ZoneDef;
  private monster!: MonsterDef;
  private back!: BattleSceneData;
  private playerFig!: Phaser.GameObjects.Container;
  private enemyFig!: Phaser.GameObjects.Container;
  private playerHp!: Bar;
  private playerMp!: Bar;
  private enemyHp!: Bar;
  private logText!: Phaser.GameObjects.Text;
  private log: string[] = [];
  private menu!: Phaser.GameObjects.Container;
  private menuButtons: MenuButton[] = [];
  private busy = false;
  private twinFlip = false;

  constructor() {
    super(SceneKeys.Battle);
  }

  create(data: BattleSceneData): void {
    enterScene(this, 350);
    this.back = data;
    this.zone = db.zone(data.zoneId);
    this.monster = db.monster(data.monsterId);
    this.battle = new Battle(session.player, this.monster);
    this.log = [];
    this.busy = true;

    drawScenery(this, this.zone.background, this.zone.backdrop, GROUND_Y - 40);
    this.playerFig = drawFigure(this, -200, GROUND_Y, PLAYER_LOOK, 1);
    this.enemyFig = drawFigure(this, this.scale.width + 200, GROUND_Y, this.monster.look, -1);

    this.drawStatus();
    this.drawBottomPanel();
    this.refreshBars(false);
    this.pushLog(`A wild ${this.monster.name} (Lv ${this.monster.level}) appears in the ${this.zone.name}!`);

    ['ONE', 'TWO', 'THREE', 'FOUR', 'FIVE', 'SIX'].forEach((key, i) => {
      this.input.keyboard!.on(`keydown-${key}`, () => {
        const b = this.menuButtons[i];
        if (!this.busy && b && b.enabled !== false) b.run();
      });
    });
    this.intro();
  }

  private async intro(): Promise<void> {
    const ease = 'Back.easeOut';
    await Promise.all([
      this.tween({ targets: this.playerFig, x: PLAYER_X, duration: 520, ease }),
      this.tween({ targets: this.enemyFig, x: ENEMY_X, duration: 520, ease }),
    ]);
    fx.dust(this, PLAYER_X, GROUND_Y, 6);
    fx.dust(this, ENEMY_X, GROUND_Y, 6);
    this.idle(this.playerFig, PLAYER_LOOK.scale);
    this.idle(this.enemyFig, this.monster.look.scale);
    this.busy = false;
    this.showMenu('main');
  }

  private idle(fig: Phaser.GameObjects.Container, scale: number): void {
    this.tweens.add({ targets: fig, scaleY: scale * 1.03, yoyo: true, repeat: -1, duration: 900, ease: 'Sine.easeInOut' });
  }

  // --- Layout ---

  private drawStatus(): void {
    const p = this.battle.player;
    const m = this.monster;
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
    const buttons: MenuButton[] = [];

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

    // 3 x 2 grid on the right side of the bottom panel. Number keys 1-6 work too.
    this.menuButtons = buttons;
    const cols = 3;
    const bw = 196;
    const bh = 66;
    buttons.forEach((b, i) => {
      const x = 650 + (i % cols) * (bw + 10) + bw / 2;
      const y = 590 + Math.floor(i / cols) * (bh + 10);
      const btn = new Button(this, x, y, b.label, b.run, { width: bw, height: bh, sublabel: b.sub, fontSize: 20 });
      if (b.enabled === false) btn.setEnabled(false);
      btn.add(addText(this, -bw / 2 + 10, -bh / 2 + 6, `${i + 1}`, { size: 13, color: COLORS.textDim }));
      btn.setScale(0.8).setAlpha(0);
      this.tweens.add({ targets: btn, scale: 1, alpha: 1, duration: 180, delay: i * 30, ease: 'Back.easeOut' });
      this.menu.add(btn);
    });
  }

  // --- Turn playback ---

  private async act(action: () => BattleEvent[]): Promise<void> {
    if (this.busy) return;
    const events = action();
    if (events.length === 0) return;
    this.busy = true;
    this.menuButtons = [];
    this.menu.removeAll(true);
    for (const event of events) await this.play(event);
    this.refreshBars();

    if (this.battle.isOver) {
      await this.wait(600);
      this.finish();
    } else {
      this.busy = false;
      this.showMenu('main');
    }
  }

  private figOf(side: Side): Phaser.GameObjects.Container {
    return side === 'player' ? this.playerFig : this.enemyFig;
  }

  private homeX(side: Side): number {
    return side === 'player' ? PLAYER_X : ENEMY_X;
  }

  /** Which visual a hit or miss uses. */
  private styleOf(e: { source: Side; moveId: string; damageType: 'physical' | 'magic' }): SkillFx | 'arcane' | 'enemy-melee' | 'enemy-spit' {
    if (e.source === 'enemy') return e.damageType === 'magic' ? 'enemy-spit' : 'enemy-melee';
    if (e.moveId === 'attack') return e.damageType === 'magic' ? 'arcane' : 'slash';
    const skill = db.skill(e.moveId);
    return skill.fx ?? (skill.damageType === 'magic' ? 'fire' : 'slash');
  }

  private async play(e: BattleEvent): Promise<void> {
    const enemyName = this.monster.name;
    switch (e.kind) {
      case 'hit':
      case 'miss': {
        const style = this.styleOf(e);
        await this.attackAnim(e.source, style, () => (e.kind === 'hit' ? this.impact(e, style) : this.dodge(e.source === 'player' ? 'enemy' : 'player')));
        if (e.kind === 'hit') {
          const crit = e.crit ? ' Critical!' : '';
          this.pushLog(e.source === 'player' ? `You use ${e.move}: ${e.damage} damage.${crit}` : `${enemyName} uses ${e.move}: ${e.damage} damage.${crit}`);
        } else {
          this.pushLog(e.source === 'player' ? `You use ${e.move}, but miss.` : `${enemyName} uses ${e.move}, but misses.`);
        }
        await this.wait(380);
        break;
      }
      case 'heal': {
        const isSkill = db.hasSkill(e.moveId);
        const color = e.mp > 0 && e.hp === 0 ? 0x7ec8ff : isSkill ? 0x7dff9a : 0xff8a8a;
        if (isSkill) await fx.chargeUp(this, PLAYER_X + 30, GROUND_Y - CHEST, color, 280);
        else await this.tween({ targets: this.playerFig, y: GROUND_Y - 20, duration: 120, yoyo: true });
        fx.healSparkles(this, PLAYER_X, GROUND_Y, color);
        floatText(this, PLAYER_X, GROUND_Y - 220, restoreText(e), COLORS.textGood, 30);
        this.refreshBars();
        this.pushLog(`You use ${e.move}: ${restoreText(e)}.`);
        await this.wait(650);
        break;
      }
      case 'flee': {
        const fig = this.playerFig;
        if (e.success) {
          this.pushLog('You got away safely.');
          this.tweens.killTweensOf(fig);
          fig.setScale(-PLAYER_LOOK.scale, PLAYER_LOOK.scale);
          for (let i = 0; i < 4; i++) this.time.delayedCall(i * 90, () => fx.dust(this, fig.x, GROUND_Y, 3));
          await this.tween({ targets: fig, x: -200, duration: 520, ease: 'Cubic.easeIn' });
        } else {
          this.pushLog("You couldn't get away!");
          fig.setScale(-PLAYER_LOOK.scale, PLAYER_LOOK.scale);
          await this.tween({ targets: fig, x: PLAYER_X - 60, duration: 180, ease: 'Quad.easeOut' });
          fig.setScale(PLAYER_LOOK.scale, PLAYER_LOOK.scale);
          fx.dust(this, fig.x, GROUND_Y, 5);
          await this.tween({ targets: fig, x: PLAYER_X, duration: 220, ease: 'Back.easeOut' });
        }
        break;
      }
      case 'defeated': {
        const fig = this.figOf(e.side);
        this.tweens.killTweensOf(fig);
        if (e.side === 'enemy') {
          this.pushLog(`${enemyName} is defeated!`);
          fx.hitStop(this, 160);
          fx.whiteFlash(this, fig, 220);
          await this.wait(200);
          fx.deathPoof(this, fig.x, GROUND_Y - 60, this.monster.look.color);
          this.cameras.main.shake(200, 0.008);
          const gold = this.battle.rewards?.gold ?? 0;
          void fx.coinShower(this, { x: fig.x, y: GROUND_Y - 100 }, { x: 60, y: 40 }, Math.ceil(gold / 2));
          await this.tween({ targets: fig, alpha: 0, scaleY: fig.scaleY * 0.2, duration: 380, ease: 'Cubic.easeIn' });
        } else {
          this.pushLog('You collapse...');
          this.cameras.main.flash(300, 160, 0, 0);
          this.cameras.main.shake(300, 0.01);
          await this.tween({ targets: fig, angle: -80, alpha: 0.5, duration: 600, ease: 'Bounce.easeOut' });
        }
        break;
      }
    }
  }

  /** Attacker's animation. `onContact` fires at the moment the blow lands. */
  private async attackAnim(source: Side, style: ReturnType<BattleScene['styleOf']>, onContact: () => void): Promise<void> {
    const fig = this.figOf(source);
    const dir = source === 'player' ? 1 : -1;
    const home = this.homeX(source);
    const target = { x: this.homeX(source === 'player' ? 'enemy' : 'player'), y: GROUND_Y - CHEST };

    switch (style) {
      case 'slash':
      case 'enemy-melee': {
        await this.tween({ targets: fig, x: target.x - 170 * dir, duration: 180, ease: 'Quad.easeIn' });
        onContact();
        await this.tween({ targets: fig, x: home, duration: 260, ease: 'Quad.easeOut' });
        break;
      }
      case 'heavy': {
        fx.dust(this, fig.x, GROUND_Y, 5);
        const hop = { t: 0 };
        const startX = fig.x;
        const endX = target.x - 180 * dir;
        await this.tween({
          targets: hop,
          t: 1,
          duration: 340,
          ease: 'Sine.easeIn',
          onUpdate: () => {
            fig.x = Phaser.Math.Linear(startX, endX, hop.t);
            fig.y = GROUND_Y - Math.sin(hop.t * Math.PI) * 130;
            fig.angle = hop.t * 12 * dir;
          },
        });
        fig.y = GROUND_Y;
        fig.angle = 0;
        onContact();
        fx.dust(this, fig.x, GROUND_Y, 10);
        await this.tween({ targets: fig, x: home, duration: 320, ease: 'Quad.easeOut' });
        break;
      }
      case 'fire':
      case 'arcane':
      case 'enemy-spit': {
        const colors =
          style === 'fire' ? FIRE : style === 'arcane' ? ARCANE : [0xffffff, this.monster.look.color, this.monster.look.accent];
        const hand = { x: fig.x + 50 * dir, y: GROUND_Y - CHEST - 10 };
        await this.tween({ targets: fig, x: home - 20 * dir, duration: 120 });
        await fx.chargeUp(this, hand.x, hand.y, colors[1], style === 'enemy-spit' ? 180 : 300);
        await Promise.all([
          this.tween({ targets: fig, x: home + 20 * dir, duration: 120, ease: 'Back.easeOut' }),
          fx.projectile(this, hand, target, colors, style === 'enemy-spit' ? 360 : 420),
        ]);
        onContact();
        await this.tween({ targets: fig, x: home, duration: 200 });
        break;
      }
      case 'heal':
        onContact();
        break;
    }
  }

  private impact(e: Extract<BattleEvent, { kind: 'hit' }>, style: ReturnType<BattleScene['styleOf']>): void {
    const targetSide: Side = e.source === 'player' ? 'enemy' : 'player';
    const fig = this.figOf(targetSide);
    const dir = e.source === 'player' ? 1 : -1;
    const x = this.homeX(targetSide);
    const y = GROUND_Y - CHEST;
    const magic = e.damageType === 'magic';
    const maxHp = targetSide === 'enemy' ? this.battle.enemy.maxHp : this.battle.player.maxHp;
    const weight = Phaser.Math.Clamp(e.damage / maxHp, 0.05, 0.6);

    if (style === 'slash' || style === 'enemy-melee') {
      const color = e.source === 'enemy' ? this.monster.look.accent : 0xffffff;
      this.twinFlip = !this.twinFlip;
      fx.slash(this, x, y, dir as 1 | -1, color, this.twinFlip ? 0.35 : -0.35);
    } else if (style === 'heavy') {
      fx.slash(this, x, y, dir as 1 | -1, 0xffe08a, 0.6, true);
      fx.shockwave(this, x, GROUND_Y, 0xffe08a, 3, 0.3);
    }
    fx.hitSparks(this, x, y, magic, e.crit);
    fx.whiteFlash(this, fig);
    fx.hitStop(this, e.crit || style === 'heavy' ? 120 : 60);
    this.cameras.main.shake(e.crit ? 220 : 140, 0.004 + weight * 0.03);

    // Knockback.
    this.tweens.add({ targets: fig, x: x + 28 * dir, duration: 70, yoyo: true, ease: 'Quad.easeOut' });

    floatText(
      this,
      x,
      GROUND_Y - 230,
      `${e.damage}`,
      magic ? COLORS.textMagic : e.source === 'enemy' ? COLORS.textBad : '#ffffff',
      e.crit ? 50 : 36,
      e.crit ? 'CRIT!' : undefined,
    );
    this.refreshBars();
  }

  private dodge(side: Side): void {
    const fig = this.figOf(side);
    const away = side === 'enemy' ? 1 : -1;
    fx.slash(this, this.homeX(side) - 60 * away, GROUND_Y - CHEST, -away as 1 | -1, 0xffffff, 0);
    this.tweens.add({ targets: fig, x: this.homeX(side) + 60 * away, duration: 120, yoyo: true, hold: 120, ease: 'Quad.easeOut' });
    floatText(this, this.homeX(side), GROUND_Y - 220, 'Miss', COLORS.textDim, 30);
  }

  private tween(config: Phaser.Types.Tweens.TweenBuilderConfig): Promise<void> {
    return new Promise((resolve) => {
      const onComplete = config.onComplete;
      this.tweens.add({
        ...config,
        onComplete: (...args: unknown[]) => {
          (onComplete as ((...a: unknown[]) => void) | undefined)?.(...args);
          resolve();
        },
      });
    });
  }

  private wait(ms: number): Promise<void> {
    return new Promise((resolve) => this.time.delayedCall(ms, resolve));
  }

  // --- Outcome ---

  private finish(): void {
    const player = this.battle.player;
    const monsterName = this.monster.name;
    const back: RoomSceneData = { roomId: this.back.roomId, via: 'battle', outcome: 'victory', x: this.back.x, y: this.back.y };

    switch (this.battle.state) {
      case 'victory': {
        const rewards = this.battle.rewards ?? { xp: 0, gold: 0, items: [] };
        const levelsGained = player.grantRewards(rewards);
        if (BALANCE.restoreAfterBattle) player.restoreFull();
        SaveSystem.save(player);
        const data: ResultSceneData = { outcome: 'victory', monsterName, rewards, levelsGained, back };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'defeat': {
        // No penalty: wake up at the inn.
        player.restoreFull();
        SaveSystem.save(player);
        const data: ResultSceneData = { outcome: 'defeat', monsterName };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'fled':
        SaveSystem.save(player);
        goTo(this, SceneKeys.Room, { ...back, outcome: 'fled' } satisfies RoomSceneData);
        break;
      case 'player_turn':
        break;
    }
  }
}
