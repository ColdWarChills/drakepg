import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { ELEMENTS, ELEMENT_COLORS, ELEMENT_LABELS, ELEMENT_TEXT, NEUTRAL_COLORS } from '../data/elements';
import type { ConsumableDef, DamageType, Element, MonsterDef, SkillDef, ZoneDef } from '../data/types';
import { INN_SPAWN } from '../data/world';
import { Battle, type BattleEvent, type Effect, type Side } from '../game/Battle';
import { makeMimic } from '../game/Chests';
import { makeElite } from '../game/Elites';
import { SIGIL_NAMES, SIGIL_TIERS } from '../game/Items';
import { LOOT_TIERS, pityBonus, tierOdds, type LootSource } from '../game/Loot';
import { isLucky, makeImpling } from '../game/Lucky';
import { questNeedsKill, recordKill } from '../game/Quests';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { drawScenery } from '../ui/backdrops';
import { Bar } from '../ui/Bar';
import { Button } from '../ui/Button';
import { drawFigure } from '../ui/figures';
import * as fx from '../ui/fx';
import * as sfx from '../ui/sfx';
import { restoreText } from '../ui/itemUi';
import { addText, floatText } from '../ui/text';
import { enterScene, goTo, SceneKeys, type BattleSceneData, type ResultSceneData, type RoomSceneData } from './keys';

const GROUND_Y = 500;
const PLAYER_X = 330;
const ENEMY_X = 950;
const LOG_LINES = 4;
/** Height above the feet where hits land. */
const CHEST = 120;
/** Skills shown per page of the Skills menu (the other slots are More / Back). */
const SKILLS_PER_PAGE = 4;

type Menu = 'main' | 'skills' | 'items';
type MenuButton = { label: string; sub?: string; enabled?: boolean; hover?: string; run: () => void };

/** How a hit is animated. */
interface Style {
  kind: 'slash' | 'heavy' | 'bolt' | 'proc' | 'enemy-melee' | 'enemy-spit';
  colors: number[];
  big?: boolean;
}

const EFFECT_TAGS: Record<Effect, string> = { weak: 'WEAK!', resisted: 'Resisted', immune: 'Immune' };
const ELITE_GLOW = 0xffc23d;
/** Lucky Implings: mint tag and glow. */
const LUCKY_GLOW = 0x7dffb0;
const LUCKY_CSS = '#8dffbe';
/** Loot line colors (Common uses the normal text color). */
const LOOT_CSS = { rare: '#ffd34d', uncommon: '#7ddc7d' };
/** Name color of `tough` monsters: they need a counter. */
const TOUGH_CSS = '#ff9a4d';

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
  private skillPage = 0;
  private busy = false;
  private twinFlip = false;
  /** Red glow while the enemy winds up a telegraphed attack. */
  private chargeAura: Phaser.GameObjects.Image | null = null;
  /** Blue bubble while the player defends. */
  private shield: Phaser.GameObjects.Arc | null = null;
  private statusRows: Record<Side, Phaser.GameObjects.Container> | null = null;
  /** Element, weaknesses and resistances under the enemy's HP bar (redrawn when a boss changes form). */
  private affinityRow: Phaser.GameObjects.Container | null = null;
  private shellLine: Phaser.GameObjects.Text | null = null;
  private shellRegrew = false;
  /** "Runs away in 2 turns" (Lucky Implings). */
  private escapeLine: Phaser.GameObjects.Text | null = null;
  /** Statuses as shown so far during playback (status id -> turns). The battle itself is already a turn ahead. */
  private shown: Record<Side, Map<string, number>> = { player: new Map(), enemy: new Map() };

  private get playerLook() {
    return this.battle.player.look;
  }

  constructor() {
    super(SceneKeys.Battle);
  }

  create(data: BattleSceneData): void {
    enterScene(this, 350);
    this.back = data;
    this.zone = db.zone(data.zoneId);
    const base = data.mimic ? makeMimic(this.zone) : isLucky(data.monsterId) ? makeImpling(this.zone) : db.monster(data.monsterId);
    this.monster = data.elite ? makeElite(base, data.elite) : base;
    this.battle = new Battle(session.player, this.monster);
    if (this.monster.boss && !questNeedsKill(session.player, this.monster.id)) this.battle.rewardScale = BALANCE.bossRepeatRewards;
    this.log = [];
    this.busy = true;
    this.skillPage = 0;
    this.chargeAura = null;
    this.shield = null;
    this.affinityRow = null;
    this.shellLine = null;
    this.shellRegrew = false;
    this.escapeLine = null;
    this.shown = { player: new Map(), enemy: new Map() };

    drawScenery(this, this.zone.background, this.zone.backdrop, GROUND_Y - 40);
    this.playerFig = drawFigure(this, -200, GROUND_Y, this.playerLook, 1, session.player.weapon, session.player.weaponSigil);
    if (this.monster.elite || this.monster.lucky) {
      const glow = this.add.image(ENEMY_X, GROUND_Y - 110, 'fx_soft').setTint(this.monster.lucky ? LUCKY_GLOW : ELITE_GLOW).setBlendMode('ADD').setScale(7).setAlpha(0);
      this.tweens.add({ targets: glow, alpha: 0.4, duration: 600, delay: 400 });
      this.tweens.add({ targets: glow, scale: 8, duration: 1100, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
    }
    this.enemyFig = drawFigure(this, this.scale.width + 200, GROUND_Y, this.monster.look, -1);

    this.drawStatus();
    this.drawBottomPanel();
    this.refreshBars(false);
    const elite = this.monster.elite ? db.affix(this.monster.elite.affix) : null;
    this.pushLog(
      this.monster.boss
        ? `${this.monster.name} awakens! There is no running from this fight.${this.battle.grit > 0 ? ` Your grit from earlier tries: +${Math.round(this.battle.grit * 100)}% damage.` : ''}`
        : elite
          ? `An Elite ${this.monster.name} (Lv ${this.monster.level}) blocks your path! ${elite.name}: ${elite.description}`
          : this.monster.lucky
            ? `A Lucky Impling (Lv ${this.monster.level}) jingles with gold! Catch it quick: it runs away on its ${ordinal(this.monster.escapeAfter ?? 0)} turn.`
          : this.monster.tough
            ? `A tough ${this.monster.name} (Lv ${this.monster.level}) blocks the way! Hit it where it is weak.`
            : `A wild ${this.monster.name} (Lv ${this.monster.level}) appears in the ${this.zone.name}!`,
    );

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
    if (this.monster.boss) {
      sfx.play(this, 'warning');
      this.cameras.main.shake(400, 0.012);
      this.cameras.main.flash(250, 120, 0, 0);
      fx.shockwave(this, ENEMY_X, GROUND_Y, 0xff4d4d, 4, 0.3);
      await this.wait(500);
    }
    this.idle(this.playerFig, this.playerLook.scale);
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
    const weaponEl = p.weaponElement;
    const elText = addText(this, 420, 30, weaponEl ? ELEMENT_LABELS[weaponEl] : 'Neutral', {
      size: 16,
      bold: true,
      color: weaponEl ? ELEMENT_TEXT[weaponEl] : COLORS.textDim,
    }).setOrigin(1, 0);
    // Boss grit from earlier losses, left of the weapon element.
    const grit = this.battle.grit;
    if (grit > 0) {
      addText(this, 420 - elText.width - 14, 30, `Grit +${Math.round(grit * 100)}%`, { size: 16, bold: true, color: COLORS.textGold }).setOrigin(1, 0);
    }
    this.playerHp = new Bar(this, 32, 74, 388, 26, COLORS.hp, 'HP ');
    this.playerMp = new Bar(this, 32, 108, 388, 22, COLORS.mp, 'MP ');

    const ex = this.scale.width - 436;
    // Elites get an extra line explaining their affix; shelled bosses one for the shell.
    const affix = m.elite ? db.affix(m.elite.affix) : null;
    const infoLines = (affix ? 1 : 0) + (m.shell ? 1 : 0) + (m.escapeAfter ? 1 : 0);
    const lootY = 136 + infoLines * 28;
    this.statusPanel(ex, 16, 420, 120 + infoLines * 28 + (m.loot ? 26 : 0));
    // One tag: an Elite or boss already reads as dangerous, and the weakness line shows the counter.
    const tags = [m.boss ? 'BOSS' : affix ? 'ELITE' : m.lucky ? 'LUCKY' : m.tough ? 'TOUGH' : ''].filter(Boolean);
    const head = addText(this, ex + 16, 26, [`${m.name}`, `Lv ${m.level}`, ...tags].join('  ·  '), {
      size: 22,
      bold: true,
      color: m.boss ? COLORS.textBad : affix ? COLORS.textGold : m.lucky ? LUCKY_CSS : m.tough ? TOUGH_CSS : COLORS.text,
    });
    // Long Elite names shrink to fit the panel.
    if (head.width > 388) head.setScale(388 / head.width);
    this.enemyHp = new Bar(this, ex + 16, 68, 388, 26, COLORS.hp, 'HP ');
    this.drawAffinities(ex + 16, 108);
    if (affix) addText(this, ex + 16, 136, `${affix.name}: ${affix.description}`, { size: 15, bold: true, color: COLORS.textGold }).setOrigin(0, 0.5);
    if (m.shell) {
      this.shellLine = addText(this, ex + 16, 136 + (affix ? 28 : 0), '', { size: 15, bold: true, color: '#bfe9ff' }).setOrigin(0, 0.5);
      this.refreshShell();
    }
    if (m.escapeAfter) {
      const y = 136 + ((affix ? 1 : 0) + (m.shell ? 1 : 0)) * 28;
      this.escapeLine = addText(this, ex + 16, y, '', { size: 15, bold: true, color: LUCKY_CSS }).setOrigin(0, 0.5);
      this.refreshEscape();
    }

    if (m.lucky && m.loot) this.drawLuckyLine(ex + 16, lootY);
    else if (m.loot) this.drawLootLine(ex + 16, lootY, m.loot);

    this.statusRows = { player: this.add.container(16, 152), enemy: this.add.container(ex, 152 + infoLines * 28 + (m.loot ? 26 : 0)) };
  }

  /** "Frost Shell ◆◆◇: 80% less damage. Fire hits crack it." */
  private refreshShell(): void {
    const shell = this.monster.shell;
    if (!shell || !this.shellLine) return;
    const left = this.battle.shell;
    const pips = '◆'.repeat(left) + '◇'.repeat(shell.hits - left);
    const regrow = shell.reformAt ? ` Grows back once at ${Math.round(shell.reformAt * 100)}% HP.` : '';
    this.shellLine.setText(
      left > 0
        ? `${shell.name} ${pips}: ${Math.round(shell.reduction * 100)}% less damage. ${ELEMENT_LABELS[shell.element]} hits crack it.`
        : `${shell.name} ${pips}: broken!${this.shellRegrew ? '' : regrow}`,
    );
    this.shellLine.setScale(Math.min(1, 388 / this.shellLine.width));
  }

  /** "Runs away in 2 turns. A stun holds it." */
  private refreshEscape(): void {
    const left = this.battle.escapeIn;
    if (left === undefined || !this.escapeLine) return;
    this.escapeLine.setText(left <= 1 ? 'Runs away on its next turn! A stun holds it.' : `Runs away in ${left} turns. A stun holds it.`);
    this.escapeLine.setColor(left <= 1 ? COLORS.textBad : LUCKY_CSS);
  }

  /** "Loot  ✦ Sigil 10%  ·  Gold x5": a Lucky Impling's payout. Hover or tap for the Sigil odds. */
  private drawLuckyLine(x: number, y: number): void {
    const l = BALANCE.lucky;
    const pct = (v: number) => `${Math.round(v * 1000) / 10}%`;
    const row = this.add.container(x, y);
    const parts = [
      { text: 'Loot ', color: COLORS.textDim },
      { text: ` ✦ Sigil ${pct(l.sigilChance)} `, color: '#ff9550' },
      { text: ` ·  Gold x${l.gold} `, color: COLORS.textGold },
    ];
    let cx = 0;
    for (const part of parts) {
      const t = addText(this, cx, 0, part.text, { size: 14, bold: true, color: part.color }).setOrigin(0, 0.5);
      row.add(t);
      cx += t.width;
    }
    const tiers = SIGIL_TIERS.map((t) => `${SIGIL_NAMES[t]} ${pct(l.sigilTiers[t])}`).join(', ');
    const details = [
      `${pct(l.sigilChance)}: a Sigil piece of this area's gear (no normal gear drops).`,
      `Which Sigil: ${tiers}.`,
      `Always: about ${l.gold}x the gold of the monsters here.`,
    ].join('\n');
    const hit = this.add.rectangle(-4, -12, cx + 8, 24, 0x000000, 0).setOrigin(0).setInteractive({ useHandCursor: true });
    row.addAt(hit, 0);
    const show = () => this.logText?.setFontSize(15).setText(details);
    const hide = () => this.logText?.setFontSize(19).setText(this.log.join('\n'));
    hit.on('pointerover', show).on('pointerdown', show).on('pointerout', hide);
  }

  /** "Gear  Rare 1%  Uncommon 5%  Common 6%": this fight's drop odds, luck included. Hover or tap for the items. */
  private drawLootLine(x: number, y: number, tableId: string): void {
    const m = this.monster;
    const source: LootSource = m.boss ? 'boss' : m.elite ? 'elite' : m.chestLoot ? 'chest' : 'mob';
    const misses = this.battle.player.data.lootPity[tableId] ?? 0;
    const odds = tierOdds(tableId, source, misses);
    const luck = pityBonus(misses);
    const pct = (v: number) => `${v >= 0.1 ? Math.round(v * 100) : (v * 100).toFixed(1)}%`;
    const label = { rare: 'Rare', uncommon: 'Uncommon', common: 'Common' } as const;
    const color = { rare: LOOT_CSS.rare, uncommon: LOOT_CSS.uncommon, common: COLORS.text } as const;
    const row = this.add.container(x, y);
    const parts: { text: string; color: string }[] = [{ text: 'Gear ', color: COLORS.textDim }];
    for (const t of LOOT_TIERS) if (odds[t] > 0) parts.push({ text: ` ${label[t]} ${pct(odds[t])}${t === 'rare' && luck > 0 ? ' (luck)' : ''} `, color: color[t] });
    let cx = 0;
    for (const part of parts) {
      const t = addText(this, cx, 0, part.text, { size: 14, bold: true, color: part.color }).setOrigin(0, 0.5);
      row.add(t);
      cx += t.width;
    }
    if (cx > 388) row.setScale(388 / cx);

    const table = db.lootTable(tableId);
    const names = (t: (typeof LOOT_TIERS)[number]) => table.tiers[t].map((e) => db.item(e.itemId).name).join(', ');
    const pityText =
      luck > 0
        ? `Rolled rarest first. Luck: +${pct(luck)} Rare after ${misses} kills here without one.`
        : `Rolled rarest first. ${misses} kills here without a Rare; after ${BALANCE.loot.pityAfter}, the Rare chance rises.`;
    const details = [...LOOT_TIERS.filter((t) => odds[t] > 0).map((t) => `${label[t]} ${pct(odds[t])}: ${names(t)}`), pityText].join('\n');
    const hit = this.add.rectangle(-4, -12, cx + 8, 24, 0x000000, 0).setOrigin(0).setInteractive({ useHandCursor: true });
    row.addAt(hit, 0);
    // Smaller type so the item lists fit the log box.
    const show = () => this.logText?.setFontSize(15).setText(details);
    const hide = () => this.logText?.setFontSize(19).setText(this.log.join('\n'));
    hit.on('pointerover', show).on('pointerdown', show).on('pointerout', hide);
  }

  /** "Earth · Weak: Fire Wind · Resists: Earth Water", each element in its color. */
  private drawAffinities(x: number, y: number): void {
    const m = this.monster;
    const resists = m.resists ?? {};
    // A bonus to every element (Warded Elites) is shown once as "All +25" instead of listing all eight.
    const all = resists.all ?? 0;
    const weak = ELEMENTS.filter((e) => (resists[e] ?? 0) + all <= -25);
    const strong = ELEMENTS.filter((e) => (resists[e] ?? 0) + all >= 25 && (resists[e] ?? 0) > 0);
    const parts: { text: string; color: string }[] = [];
    if (m.element) parts.push({ text: ELEMENT_LABELS[m.element], color: ELEMENT_TEXT[m.element] });
    const group = (label: string, list: Element[]) => {
      if (list.length === 0) return;
      parts.push({ text: `${parts.length ? '  ·  ' : ''}${label}`, color: COLORS.textDim });
      list.forEach((e) => parts.push({ text: ` ${ELEMENT_LABELS[e]}`, color: ELEMENT_TEXT[e] }));
    };
    group('Weak:', weak);
    group('Resists:', strong);
    if (all) parts.push({ text: `${parts.length ? '  ·  ' : ''}All ${all > 0 ? '+' : ''}${all}`, color: COLORS.textGold });
    this.affinityRow?.destroy();
    this.affinityRow = this.add.container(x, y);
    let cx = 0;
    for (const part of parts) {
      const t = addText(this, cx, 0, part.text, { size: 15, bold: true, color: part.color }).setOrigin(0, 0.5);
      this.affinityRow.add(t);
      cx += t.width;
    }
    // Many weaknesses and resists shrink to fit the panel.
    if (cx > 388) this.affinityRow.setScale(388 / cx);
  }

  private statusPanel(x: number, y: number, w: number, h: number): void {
    this.add.rectangle(x, y, w, h, COLORS.panel, 0.9).setOrigin(0).setRounded(10).setStrokeStyle(2, COLORS.panelBorder);
  }

  /** Catch the chips up with the battle (after a turn has finished playing). */
  private syncStatuses(): void {
    for (const side of ['player', 'enemy'] as Side[]) {
      this.shown[side] = new Map(this.battle.statuses(side).map((s) => [s.def.id, s.turns]));
    }
    this.refreshStatuses();
  }

  /** Chips under each status panel: "Burn 2", "War Cry 3". */
  private refreshStatuses(): void {
    if (!this.statusRows) return;
    for (const side of ['player', 'enemy'] as Side[]) {
      const row = this.statusRows[side];
      row.removeAll(true);
      let x = 0;
      for (const [id, turns] of this.shown[side]) {
        const def = db.status(id);
        const label = addText(this, 0, 0, `${def.name} ${turns}`, { size: 14, bold: true, color: def.harmful ? '#ffd0cc' : '#d4ffd8' }).setOrigin(0, 0.5);
        const w = label.width + 16;
        const chip = this.add.rectangle(x, 0, w, 24, def.harmful ? 0x6e1f1f : 0x1f5a2c, 0.9).setOrigin(0, 0.5).setRounded(8);
        label.setPosition(x + 8, 0);
        row.add([chip, label]);
        x += w + 6;
      }
    }
  }

  private drawBottomPanel(): void {
    const { width, height } = this.scale;
    const top = 540;
    this.add
      .rectangle(8, top, width - 16, height - top - 8, COLORS.panel, 0.95)
      .setOrigin(0)
      .setRounded(12)
      .setStrokeStyle(3, COLORS.panelBorder);
    this.logText = addText(this, 30, top + 18, '', { size: 19, wrap: 590 }).setLineSpacing(6);
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

  private skillButton(skill: SkillDef): MenuButton {
    const cd = this.battle.cooldownLeft(skill.id);
    const element = skill.element && skill.element !== 'weapon' ? ELEMENT_LABELS[skill.element] : null;
    const fresh = this.battle.player.data.freshSkills.includes(skill.id) ? 'NEW · ' : '';
    const sub = cd > 0 ? `Ready in ${cd}` : `${fresh}${skill.mpCost} MP${element ? ` · ${element}` : ''}`;
    const cdText = skill.cooldown > 0 ? ` Cooldown ${skill.cooldown}.` : '';
    return {
      label: skill.name,
      sub,
      enabled: this.battle.canUseSkill(skill.id),
      hover: `${skill.name}: ${skill.description}${cdText}`,
      run: () => this.act(() => this.battle.useSkill(skill.id)),
    };
  }

  private showMenu(menu: Menu): void {
    this.menu.removeAll(true);
    const p = this.battle.player;
    const buttons: MenuButton[] = [];

    if (menu === 'main') {
      buttons.push(
        { label: 'Attack', run: () => this.act(() => this.battle.attack()) },
        { label: 'Skills', sub: p.data.freshSkills.length ? `${p.data.freshSkills.length} NEW` : undefined, run: () => this.showMenu('skills') },
        { label: 'Items', run: () => this.showMenu('items') },
        { label: 'Defend', sub: `-60% dmg, +MP`, run: () => this.act(() => this.battle.defend()) },
        {
          label: 'Flee',
          sub: this.battle.canFlee ? `${Math.round(this.battle.fleeChance * 100)}%` : 'Boss',
          enabled: this.battle.canFlee,
          run: () => this.act(() => this.battle.flee()),
        },
      );
    } else if (menu === 'skills') {
      const skills = p.knownSkills;
      if (skills.length <= SKILLS_PER_PAGE + 1) {
        buttons.push(...skills.map((s) => this.skillButton(s)));
      } else {
        const pages = Math.ceil(skills.length / SKILLS_PER_PAGE);
        this.skillPage %= pages;
        const shown = skills.slice(this.skillPage * SKILLS_PER_PAGE, (this.skillPage + 1) * SKILLS_PER_PAGE);
        buttons.push(...shown.map((s) => this.skillButton(s)));
        buttons.push({
          label: 'More',
          sub: `${this.skillPage + 1} / ${pages}`,
          run: () => {
            this.skillPage += 1;
            this.showMenu('skills');
          },
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
          hover: `${item.name}: ${item.description}`,
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
      const btn = new Button(this, x, y, b.label, b.run, { width: bw, height: bh, sublabel: b.sub, fontSize: b.label.length > 13 ? 17 : 20 });
      if (b.enabled === false) btn.setEnabled(false);
      if (b.hover) btn.onHover(() => this.logText.setText(b.hover!), () => this.logText.setText(this.log.join('\n')));
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
    this.logText.setText(this.log.join('\n'));
    for (const event of events) await this.play(event);
    this.refreshBars();
    this.syncStatuses();
    this.refreshEscape();

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

  private nameOf(side: Side): string {
    return side === 'player' ? 'You' : this.monster.name;
  }

  private colorsOf(element: Element | undefined): number[] {
    return element ? ELEMENT_COLORS[element] : NEUTRAL_COLORS;
  }

  private styleOf(e: { source: Side; moveId: string; damageType: DamageType; element?: Element }): Style {
    const colors = this.colorsOf(e.element);
    if (e.source === 'enemy') {
      return {
        kind: e.damageType === 'magic' ? 'enemy-spit' : 'enemy-melee',
        colors: e.element ? colors : [0xffffff, this.monster.look.color, this.monster.look.accent],
      };
    }
    if (e.moveId === 'proc') return { kind: 'proc', colors };
    if (e.moveId === 'attack') return { kind: e.damageType === 'magic' ? 'bolt' : 'slash', colors };
    const skill = db.skill(e.moveId);
    if (e.damageType === 'magic') return { kind: 'bolt', colors, big: skill.fx === 'heavy' };
    return { kind: skill.fx === 'heavy' ? 'heavy' : 'slash', colors };
  }

  private async play(e: BattleEvent): Promise<void> {
    await this.playEvent(e);
    // An enemy action resolves the player's guard and the boss's wind-up.
    const enemyActed = e.kind === 'windup' || ((e.kind === 'hit' || e.kind === 'miss' || e.kind === 'buff') && e.source === 'enemy');
    if (enemyActed) {
      if (e.kind !== 'windup' && this.chargeAura) {
        this.chargeAura.destroy();
        this.chargeAura = null;
      }
      if (this.shield) {
        const s = this.shield;
        this.shield = null;
        this.tweens.add({ targets: s, alpha: 0, scale: 1.4, duration: 250, onComplete: () => s.destroy() });
      }
    }
  }

  private async playEvent(e: BattleEvent): Promise<void> {
    const enemyName = this.monster.name;
    switch (e.kind) {
      case 'hit':
      case 'miss': {
        const style = this.styleOf(e);
        await this.attackAnim(e.source, style, () => (e.kind === 'hit' ? this.impact(e, style) : this.dodge(e.source === 'player' ? 'enemy' : 'player')));
        const who = e.source === 'player' ? 'You use' : `${enemyName} uses`;
        if (e.kind === 'hit' && e.shellLeft !== undefined && this.monster.shell) {
          const broke = e.shellLeft === 0;
          sfx.play(this, broke ? 'explode' : 'hit');
          if (broke) fx.shockwave(this, ENEMY_X, GROUND_Y, 0xbfe9ff, 3, 0.3);
          floatText(this, ENEMY_X, GROUND_Y - 300, broke ? `${this.monster.shell.name} broken!` : 'Cracked!', '#bfe9ff', broke ? 34 : 26);
          this.refreshShell();
        }
        if (e.kind === 'hit') {
          const notes = [e.crit ? ' Critical!' : '', e.effect === 'weak' ? ' Weak spot!' : '', e.effect === 'resisted' ? ' Resisted.' : '', e.effect === 'immune' ? ' Immune!' : ''].join('');
          this.pushLog(e.moveId === 'proc' ? `${e.move}! ${e.damage} damage.${notes}` : `${who} ${e.move}: ${e.damage} damage.${notes}`);
        } else {
          this.pushLog(e.source === 'player' ? `You use ${e.move}, but miss.` : `${enemyName} uses ${e.move}, but misses.`);
        }
        await this.wait(e.moveId === 'proc' ? 200 : 380);
        break;
      }
      case 'heal': {
        const x = this.homeX(e.source);
        const isSkill = db.hasSkill(e.moveId) || e.moveId === 'proc';
        const color = e.mp > 0 && e.hp === 0 ? 0x7ec8ff : isSkill ? 0x7dff9a : 0xff8a8a;
        // Lifesteal and weapon specials heal right after the blow, without a wind-up.
        const afterHit = e.moveId === 'proc' || (db.hasSkill(e.moveId) && db.skill(e.moveId).kind === 'damage');
        if (!afterHit && isSkill) {
          sfx.play(this, 'charge');
          await fx.chargeUp(this, x + 30, GROUND_Y - CHEST, color, 280);
        } else if (!afterHit) {
          await this.tween({ targets: this.figOf(e.source), y: GROUND_Y - 20, duration: 120, yoyo: true });
        }
        sfx.play(this, 'heal');
        fx.healSparkles(this, x, GROUND_Y, color);
        floatText(this, x, GROUND_Y - 220, restoreText(e), COLORS.textGood, 30);
        this.refreshBars();
        this.pushLog(afterHit ? `${e.move} restores ${restoreText(e)}.` : `You use ${e.move}: ${restoreText(e)}.`);
        await this.wait(afterHit ? 350 : 650);
        break;
      }
      case 'buff': {
        const x = this.homeX(e.source);
        if (e.moveId === 'proc') {
          floatText(this, ENEMY_X, GROUND_Y - 270, `${e.move}!`, COLORS.textGold, 26);
          this.pushLog(`${e.move}!`);
          await this.wait(250);
          break;
        }
        sfx.play(this, 'charge');
        await fx.chargeUp(this, x, GROUND_Y - CHEST, e.source === 'player' ? 0xffe08a : this.monster.look.accent, 320);
        fx.shockwave(this, x, GROUND_Y, 0xffe08a, 2.2, 0.35);
        this.pushLog(`${e.source === 'player' ? 'You use' : `${enemyName} uses`} ${e.move}.`);
        await this.wait(200);
        break;
      }
      case 'status': {
        const def = db.status(e.status);
        const x = this.homeX(e.target);
        floatText(this, x, GROUND_Y - 280, def.name, def.harmful ? COLORS.textBad : COLORS.textGood, 26);
        this.shown[e.target].set(def.id, Math.max(this.shown[e.target].get(def.id) ?? 0, e.turns));
        this.refreshStatuses();
        const target = e.target === 'player' ? 'You are' : `${enemyName} is`;
        this.pushLog(`${target} affected by ${def.name} for ${e.turns} turn${e.turns > 1 ? 's' : ''}.`);
        await this.wait(300);
        break;
      }
      case 'immune': {
        floatText(this, this.homeX(e.target), GROUND_Y - 280, 'Immune', COLORS.textDim, 26);
        this.pushLog(`${this.nameOf(e.target)} shrugs off ${db.status(e.status).name}.`);
        await this.wait(300);
        break;
      }
      case 'tick': {
        const def = db.status(e.status);
        const x = this.homeX(e.target);
        if (e.damage > 0 || e.heal === 0) {
          const color = e.element ? ELEMENT_TEXT[e.element] : COLORS.textBad;
          fx.hitSparks(this, x, GROUND_Y - CHEST, true, false, this.colorsOf(e.element));
          fx.whiteFlash(this, this.figOf(e.target), 70);
          sfx.play(this, 'hit');
          floatText(this, x, GROUND_Y - 220, `${e.damage}`, color, 28, e.effect ? EFFECT_TAGS[e.effect] : def.name);
          this.pushLog(`${def.name}: ${e.damage} damage to ${e.target === 'player' ? 'you' : enemyName}.`);
        } else {
          fx.healSparkles(this, x, GROUND_Y, 0x7dff9a);
          floatText(this, x, GROUND_Y - 220, `+${e.heal}`, COLORS.textGood, 28, def.name);
          this.pushLog(`${def.name}: ${e.target === 'player' ? 'you heal' : `${enemyName} heals`} ${e.heal} HP.`);
        }
        this.refreshBars();
        await this.wait(380);
        break;
      }
      case 'expire': {
        this.shown[e.target].delete(e.status);
        this.refreshStatuses();
        break;
      }
      case 'stunned': {
        const fig = this.figOf(e.side);
        floatText(this, this.homeX(e.side), GROUND_Y - 280, 'Stunned!', COLORS.textGold, 30);
        this.pushLog(e.side === 'player' ? 'You are stunned and lose your turn!' : `${enemyName} is stunned!`);
        this.shown[e.side].delete('stun');
        this.refreshStatuses();
        await this.tween({ targets: fig, angle: e.side === 'player' ? -6 : 6, duration: 90, yoyo: true, repeat: 2 });
        fig.angle = 0;
        await this.wait(250);
        break;
      }
      case 'defend': {
        sfx.play(this, 'open');
        this.shield?.destroy();
        this.shield = this.add.circle(PLAYER_X, GROUND_Y - 110, 120).setStrokeStyle(6, 0x8ec5ff, 0.9).setFillStyle(0x8ec5ff, 0.12);
        this.shield.setScale(0.2);
        this.tweens.add({ targets: this.shield, scale: 1, duration: 220, ease: 'Back.easeOut' });
        floatText(this, PLAYER_X, GROUND_Y - 230, e.mp > 0 ? `Defend  +${e.mp} MP` : 'Defend', COLORS.textMagic, 28);
        this.pushLog('You brace yourself.');
        this.refreshBars();
        await this.wait(350);
        break;
      }
      case 'windup': {
        sfx.play(this, 'warning');
        this.pushLog(e.text);
        this.cameras.main.shake(350, 0.006);
        floatText(this, ENEMY_X, GROUND_Y - 260, `${e.move}!`, COLORS.textBad, 34);
        this.chargeAura = this.add.image(ENEMY_X, GROUND_Y - 120, 'fx_soft').setTint(0xff3b3b).setBlendMode('ADD').setScale(8).setAlpha(0.5);
        this.chargeAura.setDepth(-0.5);
        this.tweens.add({ targets: this.chargeAura, scale: 10, alpha: 0.8, duration: 450, yoyo: true, repeat: -1 });
        this.tweens.add({ targets: this.enemyFig, x: ENEMY_X + 25, duration: 180, yoyo: true, ease: 'Quad.easeOut' });
        await this.wait(700);
        break;
      }
      case 'rage': {
        floatText(this, PLAYER_X + 70, GROUND_Y - 170, `Rage +${e.mp} MP`, COLORS.textMagic, 22);
        this.refreshBars();
        break;
      }
      case 'phase': {
        // The battle already holds the new form; catch the scene's copy up and show it off.
        this.monster = this.battle.enemy.def;
        const colors = ELEMENT_COLORS[e.element];
        sfx.play(this, 'warning');
        this.cameras.main.flash(350, (colors[1] >> 16) & 0xff, (colors[1] >> 8) & 0xff, colors[1] & 0xff);
        this.cameras.main.shake(450, 0.012);
        fx.shockwave(this, ENEMY_X, GROUND_Y, colors[1], 4, 0.4);
        fx.explosion(this, ENEMY_X, GROUND_Y - CHEST, colors);
        for (const part of this.enemyFig.list) if (part instanceof Phaser.GameObjects.Image) part.setTint(colors[1]);
        floatText(this, ENEMY_X, GROUND_Y - 290, `${ELEMENT_LABELS[e.element]} form!`, ELEMENT_TEXT[e.element], 40);
        this.pushLog(e.text);
        this.drawAffinities(this.scale.width - 420, 108);
        await this.wait(1100);
        break;
      }
      case 'shell': {
        this.shellRegrew = true;
        sfx.play(this, 'warning');
        fx.shockwave(this, ENEMY_X, GROUND_Y, 0xbfe9ff, 4, 0.35);
        floatText(this, ENEMY_X, GROUND_Y - 290, `${this.monster.shell?.name ?? 'Shell'} grows back!`, '#bfe9ff', 32);
        this.pushLog(e.text);
        this.refreshShell();
        await this.wait(900);
        break;
      }
      case 'flee': {
        const fig = this.playerFig;
        if (e.success) {
          this.pushLog('You got away safely.');
          sfx.play(this, 'miss');
          this.tweens.killTweensOf(fig);
          fig.setScale(-this.playerLook.scale, this.playerLook.scale);
          for (let i = 0; i < 4; i++) this.time.delayedCall(i * 90, () => fx.dust(this, fig.x, GROUND_Y, 3));
          await this.tween({ targets: fig, x: -200, duration: 520, ease: 'Cubic.easeIn' });
        } else {
          this.pushLog("You couldn't get away!");
          fig.setScale(-this.playerLook.scale, this.playerLook.scale);
          await this.tween({ targets: fig, x: PLAYER_X - 60, duration: 180, ease: 'Quad.easeOut' });
          fig.setScale(this.playerLook.scale, this.playerLook.scale);
          fx.dust(this, fig.x, GROUND_Y, 5);
          await this.tween({ targets: fig, x: PLAYER_X, duration: 220, ease: 'Back.easeOut' });
        }
        break;
      }
      case 'escape': {
        const fig = this.enemyFig;
        this.tweens.killTweensOf(fig);
        this.pushLog(`${enemyName} darts away with its gold!`);
        sfx.play(this, 'miss');
        floatText(this, ENEMY_X, GROUND_Y - 260, 'Got away!', LUCKY_CSS, 34);
        fx.healSparkles(this, fig.x, GROUND_Y, 0xffe066);
        await this.tween({ targets: fig, y: GROUND_Y - 60, duration: 160, yoyo: true, ease: 'Quad.easeOut' });
        for (let i = 0; i < 4; i++) this.time.delayedCall(i * 80, () => fx.dust(this, fig.x, GROUND_Y, 3));
        await this.tween({ targets: fig, x: this.scale.width + 220, duration: 480, ease: 'Cubic.easeIn' });
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
          sfx.play(this, 'explode');
          this.cameras.main.shake(200, 0.008);
          const gold = this.battle.rewards?.gold ?? 0;
          void fx.coinShower(this, { x: fig.x, y: GROUND_Y - 100 }, { x: 60, y: 40 }, Math.ceil(gold / 2), () => sfx.play(this, 'coin'));
          await this.tween({ targets: fig, alpha: 0, scaleY: fig.scaleY * 0.2, duration: 380, ease: 'Cubic.easeIn' });
        } else {
          this.pushLog('You collapse...');
          sfx.play(this, 'defeat');
          this.cameras.main.flash(300, 160, 0, 0);
          this.cameras.main.shake(300, 0.01);
          await this.tween({ targets: fig, angle: -80, alpha: 0.5, duration: 600, ease: 'Bounce.easeOut' });
        }
        break;
      }
    }
  }

  /** Swing the weapons a figure holds (if its art shows them). */
  private swing(fig: Phaser.GameObjects.Container, big = false): void {
    const weapons = (fig.getData('weapons') as Phaser.GameObjects.Image[] | undefined) ?? [];
    for (const w of weapons) {
      const base = w.getData('angle') as number;
      this.tweens.add({ targets: w, angle: base + (big ? 110 : 75), duration: big ? 140 : 110, yoyo: true, ease: 'Quad.easeOut' });
    }
  }

  /** Attacker's animation. `onContact` fires at the moment the blow lands. */
  private async attackAnim(source: Side, style: Style, onContact: () => void): Promise<void> {
    const fig = this.figOf(source);
    const dir = source === 'player' ? 1 : -1;
    const home = this.homeX(source);
    const target = { x: this.homeX(source === 'player' ? 'enemy' : 'player'), y: GROUND_Y - CHEST };

    switch (style.kind) {
      case 'slash':
      case 'enemy-melee': {
        sfx.play(this, 'slash');
        await this.tween({ targets: fig, x: target.x - 170 * dir, duration: 180, ease: 'Quad.easeIn' });
        this.swing(fig);
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
        sfx.play(this, 'slash');
        this.swing(fig, true);
        onContact();
        fx.dust(this, fig.x, GROUND_Y, 10);
        await this.tween({ targets: fig, x: home, duration: 320, ease: 'Quad.easeOut' });
        break;
      }
      case 'bolt':
      case 'enemy-spit': {
        const hand = { x: fig.x + 50 * dir, y: GROUND_Y - CHEST - 10 };
        const spit = style.kind === 'enemy-spit';
        await this.tween({ targets: fig, x: home - 20 * dir, duration: 120 });
        sfx.play(this, 'charge');
        await fx.chargeUp(this, hand.x, hand.y, style.colors[1], spit ? 180 : style.big ? 520 : 300);
        sfx.play(this, 'fireball');
        await Promise.all([
          this.tween({ targets: fig, x: home + 20 * dir, duration: 120, ease: 'Back.easeOut' }),
          fx.projectile(this, hand, target, style.colors, spit ? 360 : style.big ? 520 : 420),
        ]);
        sfx.play(this, 'explode');
        if (style.big) {
          fx.explosion(this, target.x, target.y, style.colors);
          this.cameras.main.shake(260, 0.012);
        }
        onContact();
        await this.tween({ targets: fig, x: home, duration: 200 });
        break;
      }
      case 'proc': {
        // Weapon special: a burst at the target right after the blow.
        fx.explosion(this, target.x, target.y, style.colors);
        onContact();
        break;
      }
    }
  }

  private impact(e: Extract<BattleEvent, { kind: 'hit' }>, style: Style): void {
    const targetSide: Side = e.source === 'player' ? 'enemy' : 'player';
    const fig = this.figOf(targetSide);
    const dir = e.source === 'player' ? 1 : -1;
    const x = this.homeX(targetSide);
    const y = GROUND_Y - CHEST;
    const magic = e.damageType === 'magic';
    const maxHp = targetSide === 'enemy' ? this.battle.enemy.maxHp : this.battle.player.maxHp;
    const weight = Phaser.Math.Clamp(e.damage / maxHp, 0.05, 0.6);
    const palette = e.element ? style.colors : undefined;

    if (style.kind === 'slash' || style.kind === 'enemy-melee') {
      const color = e.element ? style.colors[1] : e.source === 'enemy' ? this.monster.look.accent : 0xffffff;
      this.twinFlip = !this.twinFlip;
      fx.slash(this, x, y, dir as 1 | -1, color, this.twinFlip ? 0.35 : -0.35);
    } else if (style.kind === 'heavy') {
      const color = e.element ? style.colors[1] : 0xffe08a;
      fx.slash(this, x, y, dir as 1 | -1, color, 0.6, true);
      fx.shockwave(this, x, GROUND_Y, color, 3, 0.3);
    }
    fx.hitSparks(this, x, y, magic, e.crit, palette);
    sfx.play(this, e.crit || style.kind === 'heavy' || style.big ? 'crit' : 'hit');
    fx.whiteFlash(this, fig);
    fx.hitStop(this, e.crit || style.kind === 'heavy' ? 120 : 60);
    this.cameras.main.shake(e.crit ? 220 : 140, 0.004 + weight * 0.03);

    // Knockback.
    this.tweens.add({ targets: fig, x: x + 28 * dir, duration: 70, yoyo: true, ease: 'Quad.easeOut' });

    const color = e.element ? ELEMENT_TEXT[e.element] : magic ? COLORS.textMagic : e.source === 'enemy' ? COLORS.textBad : '#ffffff';
    const tags = [e.crit ? 'CRIT!' : '', e.effect ? EFFECT_TAGS[e.effect] : ''].filter(Boolean).join(' ');
    floatText(this, x, GROUND_Y - 230, `${e.damage}`, color, e.crit || e.effect === 'weak' ? 50 : 36, tags || undefined);
    this.refreshBars();
  }

  private dodge(side: Side): void {
    sfx.play(this, 'miss');
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
        const levelBefore = player.level;
        const { levels: levelsGained, lost } = player.grantRewards(rewards);
        const quests = recordKill(player, this.monster).map((q) => ({
          name: q.quest.name,
          goal: q.quest.goal,
          count: q.count,
          target: q.target,
        }));
        if (BALANCE.restoreAfterBattle) player.restoreFull();
        SaveSystem.save(player);
        const newSkills = player.skillsLearnedSince(levelBefore).map((s) => s.name);
        const data: ResultSceneData = { outcome: 'victory', monsterName, rewards, lost, levelsGained, newSkills, quests, back };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'defeat': {
        // No penalty: wake up at the nearest inn.
        player.restoreFull();
        SaveSystem.save(player);
        const data: ResultSceneData = {
          outcome: 'defeat',
          monsterName,
          wake: db.room(this.back.roomId).wakeAt ?? INN_SPAWN,
          grit: this.monster.boss ? this.battle.grit : undefined,
        };
        goTo(this, SceneKeys.Result, data);
        break;
      }
      case 'fled':
        SaveSystem.save(player);
        goTo(this, SceneKeys.Room, { ...back, outcome: 'fled' } satisfies RoomSceneData);
        break;
      case 'escaped':
        SaveSystem.save(player);
        goTo(this, SceneKeys.Room, { ...back, outcome: 'escaped' } satisfies RoomSceneData);
        break;
      case 'player_turn':
        break;
    }
  }
}

/** 1st, 2nd, 3rd, 4th... */
function ordinal(n: number): string {
  const suffix = n % 100 >= 11 && n % 100 <= 13 ? 'th' : (['th', 'st', 'nd', 'rd'][n % 10] ?? 'th');
  return `${n}${suffix}`;
}
