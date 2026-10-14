import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { ELEMENTS, ELEMENT_COLORS, ELEMENT_LABELS, ELEMENT_TEXT, NEUTRAL_COLORS } from '../data/elements';
import type { ConsumableDef, DamageType, Element, MonsterDef, SkillDef, StatusDef, ZoneDef } from '../data/types';
import { INN_SPAWN } from '../data/world';
import { Battle, type BattleEvent, type Effect, type Side } from '../game/Battle';
import { makeMimic } from '../game/Chests';
import { makeElite } from '../game/Elites';
import { makeHeroic } from '../game/Heroic';
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
/** Panels, bars and menus sit above hit effects (which use ~900) so particles never cover numbers. Coins (950) still fly over. */
const HUD_DEPTH = 940;
/** Defend bubble color. */
const GUARD_COLOR = 0x8ec5ff;
/** Claw rakes and lunge trails of monster attacks without an element (their own colors can be too dark to glow). */
const MONSTER_HIT_COLOR = 0xff6b5a;

type Menu = 'main' | 'skills' | 'items';
type MenuButton = { label: string; sub?: string; enabled?: boolean; hover?: string; run: () => void };

/** How a hit is animated. */
interface Style {
  kind: 'slash' | 'heavy' | 'bolt' | 'proc' | 'enemy-melee' | 'enemy-spit';
  colors: number[];
  element?: Element;
  big?: boolean;
}

/**
 * Where a hit sits in a multi-hit move (Flurry, Thousand Cuts): the attacker lunges in on the first hit,
 * stays in reach for the next ones, and goes home after the last.
 */
interface Chain {
  first: boolean;
  last: boolean;
}

const SINGLE: Chain = { first: true, last: true };
/** Where each hit's number pops in a combo (x, y offsets), so quick hits don't cover each other. */
const COMBO_SPOTS = [
  [0, 0],
  [70, -30],
  [-70, -12],
  [36, -58],
  [-36, -44],
];

const EFFECT_TAGS: Record<Effect, string> = { weak: 'WEAK!', resisted: 'Resisted', immune: 'Immune', absorb: 'Absorbed' };
const ELITE_GLOW = 0xffc23d;
/** Lucky Implings: mint tag and glow. */
const LUCKY_GLOW = 0x7dffb0;
const LUCKY_CSS = '#8dffbe';
/** Set pieces on the loot line. */
const SET_CSS = '#7fe0c0';
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
  /** "Next: Gore ~9 dmg" and the enrage countdown, under the enemy's info lines. */
  private intentLine: Phaser.GameObjects.Text | null = null;
  private enrageText: Phaser.GameObjects.Text | null = null;
  /** "Runs away in 2 turns" (Lucky Implings). */
  private escapeLine: Phaser.GameObjects.Text | null = null;
  /** The target's knockback in flight, stopped when the next blow lands (combo hits). */
  private recoil: Phaser.Tweens.Tween | null = null;
  /** Hits so far in the current combo (spreads out their numbers). */
  private comboHits = 0;
  /** How far the enemy's floating text starts lower, so it pops under a tall info panel (Elites, bosses). */
  private enemyTextShift = 0;
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
    this.monster = data.elite ? makeElite(base, data.elite) : data.heroic ? makeHeroic(base) : base;
    this.battle = new Battle(session.player, this.monster);
    if (this.monster.isHeroic) this.battle.rewardScale = BALANCE.heroic.rewards;
    else if (this.monster.boss && !questNeedsKill(session.player, this.monster.id)) this.battle.rewardScale = BALANCE.bossRepeatRewards;
    this.log = [];
    this.busy = true;
    this.skillPage = 0;
    this.chargeAura = null;
    this.shield = null;
    this.recoil = null;
    this.affinityRow = null;
    this.shellLine = null;
    this.shellRegrew = false;
    this.intentLine = null;
    this.enrageText = null;
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

    const hudFrom = this.children.list.length;
    this.drawStatus();
    this.drawBottomPanel();
    for (const obj of this.children.list.slice(hudFrom)) (obj as Phaser.GameObjects.Image).setDepth?.(HUD_DEPTH);
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
    // Elites get an extra line explaining their affix, shelled bosses one for the shell, monsters with a
    // signature move one for that; every monster has the "Next:" line.
    const affix = m.elite ? db.affix(m.elite.affix) : null;
    const heroic = m.isHeroic && m.heroic ? `Heroic: ${m.heroic.text}` : '';
    const infoLines = (affix ? 1 : 0) + (m.shell ? 1 : 0) + (m.trait ? 1 : 0) + (heroic ? 1 : 0) + (m.escapeAfter ? 1 : 0) + 1;
    const lootY = 136 + infoLines * 28;
    const panelH = 120 + infoLines * 28 + (m.loot ? 26 : 0);
    this.statusPanel(ex, 16, 420, panelH);
    // Status names (the highest text, at GROUND_Y - 280) start under the panel and its status chips.
    this.enemyTextShift = Phaser.Math.Clamp(16 + panelH + 50 - (GROUND_Y - 280), 0, 90);
    // One tag: an Elite or boss already reads as dangerous, and the weakness line shows the counter.
    const tags = [m.isHeroic ? 'HEROIC' : m.boss ? 'BOSS' : affix ? 'ELITE' : m.lucky ? 'LUCKY' : m.tough ? 'TOUGH' : ''].filter(Boolean);
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
    let lineY = 136 + (affix ? 28 : 0);
    if (m.shell) {
      this.shellLine = addText(this, ex + 16, lineY, '', { size: 15, bold: true, color: '#bfe9ff' }).setOrigin(0, 0.5);
      this.refreshShell();
      lineY += 28;
    }
    if (m.trait) {
      const trait = addText(this, ex + 16, lineY, m.trait, { size: 15, bold: true, color: '#e8c8ff' }).setOrigin(0, 0.5);
      trait.setScale(Math.min(1, 388 / trait.width));
      lineY += 28;
    }
    if (heroic) {
      const line = addText(this, ex + 16, lineY, heroic, { size: 15, bold: true, color: COLORS.textGold }).setOrigin(0, 0.5);
      line.setScale(Math.min(1, 388 / line.width));
      lineY += 28;
    }
    if (m.escapeAfter) {
      this.escapeLine = addText(this, ex + 16, lineY, '', { size: 15, bold: true, color: LUCKY_CSS }).setOrigin(0, 0.5);
      this.refreshEscape();
      lineY += 28;
    }
    this.intentLine = addText(this, ex + 16, lineY, '', { size: 16, bold: true }).setOrigin(0, 0.5);
    this.enrageText = addText(this, ex + 404, lineY, '', { size: 15, bold: true, color: COLORS.textBad }).setOrigin(1, 0.5);
    this.refreshIntent();

    if (m.lucky && m.loot) this.drawLuckyLine(ex + 16, lootY);
    else if (m.loot) this.drawLootLine(ex + 16, lootY, m.loot);

    this.statusRows = { player: this.add.container(16, 152), enemy: this.add.container(ex, 152 + infoLines * 28 + (m.loot ? 26 : 0)) };
  }

  /** "Next: Root Crush ~31 dmg · Defend!" plus "Enrages in 3" / "Enraged +40%" on the right. */
  private refreshIntent(): void {
    if (!this.intentLine || !this.enrageText) return;
    const b = this.battle;
    const a = b.intent;
    const charging = b.charging === a;
    const dmg = b.intentDamage();
    const statuses = (a.applies ?? []).map((x) => `${db.status(x.status).name}${x.chance && x.chance < 1 ? ` ${Math.round(x.chance * 100)}%` : ''}`);
    let detail: string;
    let color: string = COLORS.text;
    if (a.windup && !charging) {
      detail = 'winds up a big attack';
      color = TOUGH_CSS;
    } else if (a.heal) {
      detail = `heals ${Math.round(a.heal * 100)}% HP`;
      color = COLORS.textMagic;
    } else if (dmg === null) {
      const self = (a.applies ?? []).every((x) => x.self);
      detail = statuses.length ? `${self ? 'buffs itself: ' : ''}${statuses.join(', ')}` : 'prepares';
      color = COLORS.textMagic;
    } else {
      detail = [`~${dmg} dmg`, ...statuses.map((x) => `+ ${x}`)].join(' ');
      if (charging) {
        detail += ' · Defend or heal!';
        color = COLORS.textBad;
      } else if (a.power >= 1.5) {
        color = TOUGH_CSS;
      }
    }
    this.intentLine.setText(`Next: ${a.name}  ${detail}`).setColor(color);
    const left = b.turnsToEnrage;
    const warn = BALANCE.enrage.warn;
    this.enrageText.setText(b.enrage > 0 ? `Enraged +${Math.round(b.enrage * 100)}%` : left <= warn ? `Enrages in ${left}` : '');
    const room = 388 - (this.enrageText.text ? this.enrageText.width + 10 : 0);
    this.intentLine.setScale(Math.min(1, room / this.intentLine.width));
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
    // Elites and bosses: a chance at a piece of the area's set on top.
    const table = db.lootTable(tableId);
    const setChance = table.set && (source === 'elite' || source === 'boss') ? BALANCE.setDrop[source] : 0;
    if (setChance) parts.push({ text: ` · Set ${pct(setChance)} `, color: SET_CSS });
    let cx = 0;
    for (const part of parts) {
      const t = addText(this, cx, 0, part.text, { size: 14, bold: true, color: part.color }).setOrigin(0, 0.5);
      row.add(t);
      cx += t.width;
    }
    if (cx > 388) row.setScale(388 / cx);

    const names = (t: (typeof LOOT_TIERS)[number]) => table.tiers[t].map((e) => db.item(e.itemId).name).join(', ');
    const pityText =
      luck > 0
        ? `Rolled rarest first. Luck: +${pct(luck)} Rare after ${misses} kills here without one.`
        : `Rolled rarest first. ${misses} kills here without a Rare; after ${BALANCE.loot.pityAfter}, the Rare chance rises.`;
    const setText = setChance ? [`Set ${pct(setChance)} (extra): ${db.set(table.set!).pieces.map((id) => db.item(id).name).join(', ')}`] : [];
    const details = [...LOOT_TIERS.filter((t) => odds[t] > 0).map((t) => `${label[t]} ${pct(odds[t])}: ${names(t)}`), ...setText, pityText].join('\n');
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
    const strong = ELEMENTS.filter((e) => (resists[e] ?? 0) + all >= 25 && (resists[e] ?? 0) > 0 && (resists[e] ?? 0) + all <= 100);
    const absorbs = ELEMENTS.filter((e) => (resists[e] ?? 0) + all > 100);
    const parts: { text: string; color: string }[] = [];
    if (m.element) parts.push({ text: ELEMENT_LABELS[m.element], color: ELEMENT_TEXT[m.element] });
    const group = (label: string, list: Element[]) => {
      if (list.length === 0) return;
      parts.push({ text: `${parts.length ? '  ·  ' : ''}${label}`, color: COLORS.textDim });
      list.forEach((e) => parts.push({ text: ` ${ELEMENT_LABELS[e]}`, color: ELEMENT_TEXT[e] }));
    };
    group('Weak:', weak);
    group('Resists:', strong);
    group('Absorbs:', absorbs);
    if (all) parts.push({ text: `${parts.length ? '  ·  ' : ''}All ${all > 0 ? '+' : ''}${all}`, color: COLORS.textGold });
    this.affinityRow?.destroy();
    this.affinityRow = this.add.container(x, y).setDepth(HUD_DEPTH);
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
      const fresh = p.carriedSkills.filter((sk) => p.data.freshSkills.includes(sk.id)).length;
      buttons.push(
        { label: 'Attack', run: () => this.act(() => this.battle.attack()) },
        { label: 'Skills', sub: fresh ? `${fresh} NEW` : undefined, run: () => this.showMenu('skills') },
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
      const skills = p.carriedSkills;
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
    for (let i = 0; i < events.length; i++) await this.play(events[i], events[i - 1], events[i + 1]);
    this.refreshBars();
    this.syncStatuses();
    this.refreshIntent();
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

  /** Top of a figure's body (art or shapes) and its width, for marks over the head and death effects. */
  private bodyOf(side: Side): { top: number; width: number } {
    const fig = this.figOf(side);
    let top = GROUND_Y - 120;
    let width = 80;
    for (const part of fig.list) {
      if (!(part instanceof Phaser.GameObjects.Image || part instanceof Phaser.GameObjects.Shape) || !part.visible) continue;
      const b = part.getBounds();
      top = Math.min(top, b.top);
      width = Math.max(width, b.width);
    }
    return { top: Phaser.Math.Clamp(top, GROUND_Y - 300, GROUND_Y - 120), width: Math.min(width, 260) };
  }

  /** Color of a status's visuals: its own tint, else its damage element's, else red or green. */
  private statusColor(def: StatusDef, element?: Element): number {
    if (def.tint !== undefined) return def.tint;
    const el = element ?? (def.dot?.element && def.dot.element !== 'weapon' ? def.dot.element : undefined);
    if (el) return ELEMENT_COLORS[el][1];
    return def.harmful ? 0xff6b6b : 0x7dff9a;
  }

  private styleOf(e: { source: Side; moveId: string; damageType: DamageType; element?: Element }): Style {
    const colors = this.colorsOf(e.element);
    const element = e.element;
    if (e.source === 'enemy') {
      return {
        kind: e.damageType === 'magic' ? 'enemy-spit' : 'enemy-melee',
        colors: e.element ? colors : [0xffffff, this.monster.look.color, this.monster.look.accent],
        element,
      };
    }
    if (e.moveId === 'proc') return { kind: 'proc', colors, element };
    if (e.moveId === 'attack') return { kind: e.damageType === 'magic' ? 'bolt' : 'slash', colors, element };
    const skill = db.skill(e.moveId);
    if (e.damageType === 'magic') return { kind: 'bolt', colors, element, big: skill.fx === 'heavy' };
    return { kind: skill.fx === 'heavy' ? 'heavy' : 'slash', colors, element };
  }

  /** Player hits of the same skill in a row (multi-hit skills); monster moves and weapon specials stand alone. */
  private sameMove(a: BattleEvent | undefined, b: BattleEvent): boolean {
    if (!a || (a.kind !== 'hit' && a.kind !== 'miss') || (b.kind !== 'hit' && b.kind !== 'miss')) return false;
    return a.source === 'player' && b.source === 'player' && a.moveId === b.moveId && a.moveId !== 'proc';
  }

  private async play(e: BattleEvent, prev?: BattleEvent, next?: BattleEvent): Promise<void> {
    await this.playEvent(e, { first: !this.sameMove(prev, e), last: !next || !this.sameMove(e, next) });
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
        this.tweens.killTweensOf(s);
        this.tweens.add({ targets: s, alpha: 0, scale: 1.4, duration: 250, onComplete: () => s.destroy() });
      }
    }
  }

  private async playEvent(e: BattleEvent, chain: Chain = SINGLE): Promise<void> {
    const enemyName = this.monster.name;
    switch (e.kind) {
      case 'hit':
      case 'miss': {
        this.comboHits = chain.first ? 0 : this.comboHits + 1;
        const style = this.styleOf(e);
        const target: Side = e.source === 'player' ? 'enemy' : 'player';
        await this.attackAnim(e.source, style, () => (e.kind === 'hit' ? this.impact(e, style, chain) : this.dodge(target)), chain);
        const who = e.source === 'player' ? 'You use' : `${enemyName} uses`;
        if (e.kind === 'hit' && e.shellLeft !== undefined && this.monster.shell) {
          const broke = e.shellLeft === 0;
          sfx.play(this, broke ? 'explode' : 'hit');
          if (broke) {
            fx.shockwave(this, ENEMY_X, GROUND_Y, 0xbfe9ff, 3, 0.3);
            fx.elementBurst(this, ENEMY_X, GROUND_Y - CHEST, 'ice', 1);
          }
          floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 300), broke ? `${this.monster.shell.name} broken!` : 'Cracked!', '#bfe9ff', broke ? 34 : 26);
          this.refreshShell();
        }
        if (e.kind === 'hit' && e.absorbed) {
          this.pushLog(`${who} ${e.move}, but ${e.source === 'player' ? enemyName : 'you'} absorb${e.source === 'player' ? 's' : ''} it: +${e.damage} HP!`);
        } else if (e.kind === 'hit') {
          const notes = [e.crit ? ' Critical!' : '', e.effect === 'weak' ? ' Weak spot!' : '', e.effect === 'resisted' ? ' Resisted.' : '', e.effect === 'immune' ? ' Immune!' : ''].join('');
          this.pushLog(e.moveId === 'proc' ? `${e.move}! ${e.damage} damage.${notes}` : `${who} ${e.move}: ${e.damage} damage.${notes}`);
        } else {
          this.pushLog(e.source === 'player' ? `You use ${e.move}, but miss.` : `${enemyName} uses ${e.move}, but misses.`);
        }
        // Combo hits follow each other quickly; the last one gets the usual pause.
        await this.wait(!chain.last ? 120 : e.moveId === 'proc' ? 200 : 340);
        break;
      }
      case 'heal': {
        const x = this.homeX(e.source);
        const fig = this.figOf(e.source);
        const isSkill = db.hasSkill(e.moveId) || e.moveId === 'proc' || e.source === 'enemy';
        const color = e.mp > 0 && e.hp === 0 ? 0x7ec8ff : isSkill ? 0x7dff9a : 0xff8a8a;
        // Lifesteal and weapon specials heal right after the blow, without a wind-up.
        const lifesteal = db.hasSkill(e.moveId) && db.skill(e.moveId).kind === 'damage';
        const afterHit = e.moveId === 'proc' || lifesteal;
        if (lifesteal) {
          // Life pulled out of the target into the attacker.
          const from = this.homeX(e.source === 'player' ? 'enemy' : 'player');
          fx.drain(this, { x: from, y: GROUND_Y - CHEST }, { x, y: GROUND_Y - CHEST }, 0xff6b8a);
          await this.wait(260);
        } else if (!afterHit && isSkill) {
          sfx.play(this, 'charge');
          this.children.moveBelow(fx.castCircle(this, x, GROUND_Y, color, 280), fig);
          await fx.chargeUp(this, x + 30, GROUND_Y - CHEST, color, 280);
        } else if (!afterHit) {
          // A potion: a little hop.
          await this.tween({ targets: fig, y: GROUND_Y - 20, duration: 120, yoyo: true });
        }
        sfx.play(this, 'heal');
        fx.healSparkles(this, x, GROUND_Y, color);
        fx.healPluses(this, x, GROUND_Y, color);
        fx.whiteFlash(this, fig, 140, color);
        floatText(this, x, this.textY(e.source, GROUND_Y - 220), restoreText(e), COLORS.textGood, 30);
        this.refreshBars();
        const user = e.source === 'player' ? 'You use' : `${enemyName} uses`;
        this.pushLog(afterHit ? `${e.move} restores ${restoreText(e)}.` : `${user} ${e.move}: ${restoreText(e)}.`);
        await this.wait(afterHit ? 350 : 600);
        break;
      }
      case 'buff': {
        const x = this.homeX(e.source);
        if (e.moveId === 'proc') {
          floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 270), `${e.move}!`, COLORS.textGold, 26);
          this.pushLog(`${e.move}!`);
          await this.wait(250);
          break;
        }
        const color = e.source === 'player' ? 0xffe08a : this.monster.look.accent;
        sfx.play(this, 'charge');
        this.children.moveBelow(fx.castCircle(this, x, GROUND_Y, color, 320), this.figOf(e.source));
        await fx.chargeUp(this, x, GROUND_Y - CHEST, color, 320);
        fx.shockwave(this, x, GROUND_Y, color, 2.2, 0.35);
        fx.aura(this, x, GROUND_Y, color);
        this.pushLog(`${e.source === 'player' ? 'You use' : `${enemyName} uses`} ${e.move}.`);
        await this.wait(200);
        break;
      }
      case 'status': {
        const def = db.status(e.status);
        const x = this.homeX(e.target);
        const color = this.statusColor(def);
        const head = this.bodyOf(e.target).top;
        floatText(this, x, this.textY(e.target, GROUND_Y - 280), def.name, def.harmful ? COLORS.textBad : COLORS.textGood, 26);
        if (def.stun) {
          sfx.play(this, 'stun');
          fx.stunStars(this, x, head - 10);
        } else {
          if (def.harmful) sfx.play(this, 'debuff');
          fx.statusMark(this, x, head - 20, color, !def.harmful);
          if (def.harmful) fx.statusPuff(this, x, GROUND_Y - CHEST, color);
        }
        this.shown[e.target].set(def.id, Math.max(this.shown[e.target].get(def.id) ?? 0, e.turns));
        this.refreshStatuses();
        const target = e.target === 'player' ? 'You are' : `${enemyName} is`;
        this.pushLog(`${target} affected by ${def.name} for ${e.turns} turn${e.turns > 1 ? 's' : ''}.`);
        await this.wait(300);
        break;
      }
      case 'immune': {
        sfx.play(this, 'block');
        floatText(this, this.homeX(e.target), this.textY(e.target, GROUND_Y - 280), 'Immune', COLORS.textDim, 26);
        this.pushLog(`${this.nameOf(e.target)} shrugs off ${db.status(e.status).name}.`);
        await this.wait(300);
        break;
      }
      case 'tick': {
        const def = db.status(e.status);
        const x = this.homeX(e.target);
        const fig = this.figOf(e.target);
        if (e.damage > 0 || e.heal === 0) {
          const color = e.element ? ELEMENT_TEXT[e.element] : COLORS.textBad;
          const tint = this.statusColor(def, e.element);
          // Burn flickers with flames, Spores puff earth, Poison bubbles, Bleed drips.
          if (e.element && def.tint === undefined) fx.elementBurst(this, x, GROUND_Y - CHEST, e.element, 0.25);
          else fx.statusPuff(this, x, GROUND_Y - CHEST, tint);
          fx.hitSparks(this, x, GROUND_Y - CHEST, true, false, def.tint !== undefined ? [0xffffff, tint, tint] : this.colorsOf(e.element));
          fx.whiteFlash(this, fig, 100, 0xffffff, tint);
          sfx.play(this, 'hit');
          this.tweens.add({ targets: fig, angle: e.target === 'player' ? -4 : 4, duration: 60, yoyo: true, onComplete: () => fig.setAngle(0) });
          floatText(this, x, this.textY(e.target, GROUND_Y - 220), `${e.damage}`, color, 28, e.effect ? EFFECT_TAGS[e.effect] : def.name);
          this.pushLog(`${def.name}: ${e.damage} damage to ${e.target === 'player' ? 'you' : enemyName}.`);
        } else {
          fx.healSparkles(this, x, GROUND_Y, 0x7dff9a);
          fx.healPluses(this, x, GROUND_Y, 0x7dff9a);
          floatText(this, x, this.textY(e.target, GROUND_Y - 220), `+${e.heal}`, COLORS.textGood, 28, def.name);
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
        sfx.play(this, 'stun');
        fx.stunStars(this, this.homeX(e.side), this.bodyOf(e.side).top - 10, 700);
        floatText(this, this.homeX(e.side), this.textY(e.side, GROUND_Y - 280), 'Stunned!', COLORS.textGold, 30);
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
        if (this.shield) {
          this.tweens.killTweensOf(this.shield);
          this.shield.destroy();
        }
        this.shield = this.add.circle(PLAYER_X, GROUND_Y - 110, 120).setStrokeStyle(6, GUARD_COLOR, 0.9).setFillStyle(GUARD_COLOR, 0.12);
        this.shield.setScale(0.2);
        this.tweens.add({ targets: this.shield, scale: 1, duration: 220, ease: 'Back.easeOut' });
        // A slow shimmer while it holds.
        this.tweens.add({ targets: this.shield, fillAlpha: 0.22, duration: 500, delay: 220, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
        fx.aura(this, PLAYER_X, GROUND_Y, GUARD_COLOR);
        floatText(this, PLAYER_X, GROUND_Y - 290, e.mp > 0 ? `Defend  +${e.mp} MP` : 'Defend', COLORS.textMagic, 28);
        this.pushLog('You brace yourself.');
        this.refreshBars();
        await this.wait(350);
        break;
      }
      case 'windup': {
        sfx.play(this, 'warning');
        this.pushLog(e.text);
        this.cameras.main.shake(350, 0.006);
        floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 260), `${e.move}!`, COLORS.textBad, 34);
        this.chargeAura = this.add.image(ENEMY_X, GROUND_Y - 120, 'fx_soft').setTint(0xff3b3b).setBlendMode('ADD').setScale(8).setAlpha(0.5);
        this.chargeAura.setDepth(-0.5);
        this.tweens.add({ targets: this.chargeAura, scale: 10, alpha: 0.8, duration: 450, yoyo: true, repeat: -1 });
        this.tweens.add({ targets: this.enemyFig, x: ENEMY_X + 25, duration: 180, yoyo: true, ease: 'Quad.easeOut' });
        await this.wait(700);
        break;
      }
      case 'enrage': {
        sfx.play(this, 'warning');
        this.cameras.main.flash(200, 140, 0, 0);
        floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 290), `Enraged! +${Math.round(e.bonus * 100)}% damage`, COLORS.textBad, 30);
        this.pushLog(`The fight drags on: ${enemyName} is enraged (+${Math.round(e.bonus * 100)}% damage).`);
        this.refreshIntent();
        await this.wait(500);
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
        fx.elementBurst(this, ENEMY_X, GROUND_Y - CHEST, e.element, 1);
        for (const part of this.enemyFig.list) if (part instanceof Phaser.GameObjects.Image) part.setTint(colors[1]);
        floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 290), `${ELEMENT_LABELS[e.element]} form!`, ELEMENT_TEXT[e.element], 40);
        this.pushLog(e.text);
        this.drawAffinities(this.scale.width - 420, 108);
        this.refreshIntent();
        await this.wait(1100);
        break;
      }
      case 'shell': {
        this.shellRegrew = true;
        sfx.play(this, 'warning');
        fx.shockwave(this, ENEMY_X, GROUND_Y, 0xbfe9ff, 4, 0.35);
        floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 290), `${this.monster.shell?.name ?? 'Shell'} grows back!`, '#bfe9ff', 32);
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
        floatText(this, ENEMY_X, this.textY('enemy', GROUND_Y - 260), 'Got away!', LUCKY_CSS, 34);
        fx.healSparkles(this, fig.x, GROUND_Y, 0xffe066);
        await this.tween({ targets: fig, y: GROUND_Y - 60, duration: 160, yoyo: true, ease: 'Quad.easeOut' });
        for (let i = 0; i < 4; i++) this.time.delayedCall(i * 80, () => fx.dust(this, fig.x, GROUND_Y, 3));
        await this.tween({ targets: fig, x: this.scale.width + 220, duration: 480, ease: 'Cubic.easeIn' });
        break;
      }
      case 'defeated': {
        const fig = this.figOf(e.side);
        this.tweens.killTweensOf(fig);
        this.recoil = null;
        if (e.side === 'enemy') {
          const m = this.monster;
          const body = this.bodyOf('enemy');
          const colors = m.element ? ELEMENT_COLORS[m.element] : [0xffffff, m.look.color, m.look.accent];
          this.pushLog(`${enemyName} is defeated!`);
          fx.hitStop(this, m.boss ? 260 : 160);
          fx.whiteFlash(this, fig, 260);
          // Knocked back off its feet.
          await this.tween({ targets: fig, x: ENEMY_X + 40, angle: 10, duration: 200, ease: 'Quad.easeOut' });
          if (m.boss) {
            // A boss goes down in a chain of blasts.
            for (let i = 0; i < 3; i++) {
              const bx = ENEMY_X + Phaser.Math.Between(-body.width / 2, body.width / 2);
              const by = Phaser.Math.Between(body.top + 20, GROUND_Y - 40);
              fx.explosion(this, bx, by, colors);
              fx.whiteFlash(this, fig, 90);
              sfx.play(this, 'explode');
              this.cameras.main.shake(160, 0.01);
              await this.wait(200);
            }
            this.cameras.main.flash(260, 255, 255, 255);
            fx.shockwave(this, ENEMY_X, GROUND_Y, colors[1], 5, 0.35);
          }
          fx.deathPoof(this, fig.x, GROUND_Y - 60, m.look.color);
          fx.dissolve(this, fig.x, GROUND_Y, body.width * 0.8, GROUND_Y - body.top, colors);
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

  /** Cock the weapons a figure holds back (if its art shows them), ready to swing. */
  private cock(fig: Phaser.GameObjects.Container, ms: number): void {
    for (const w of (fig.getData('weapons') as Phaser.GameObjects.Image[] | undefined) ?? []) {
      this.tweens.killTweensOf(w);
      this.tweens.add({ targets: w, angle: (w.getData('angle') as number) - 40, duration: ms, ease: 'Quad.easeOut' });
    }
  }

  /** Swing the weapons a figure holds through the target and settle back. */
  private swing(fig: Phaser.GameObjects.Container, big = false): void {
    for (const w of (fig.getData('weapons') as Phaser.GameObjects.Image[] | undefined) ?? []) {
      const base = w.getData('angle') as number;
      this.tweens.killTweensOf(w);
      this.tweens.chain({
        targets: w,
        tweens: [
          { angle: base + (big ? 110 : 80), duration: big ? 110 : 80, ease: 'Quad.easeIn' },
          { angle: base, duration: big ? 220 : 160, ease: 'Quad.easeOut' },
        ],
      });
    }
  }

  /** Lean back (and cock the weapon) before a lunge: the anticipation that sells the blow. */
  private windUp(fig: Phaser.GameObjects.Container, dir: number, ms: number, big = false): Promise<void> {
    this.cock(fig, ms);
    return this.tween({ targets: fig, x: fig.x - (big ? 30 : 20) * dir, angle: -(big ? 9 : 6) * dir, duration: ms, ease: 'Quad.easeOut' });
  }

  /** A fading silhouette of a figure where it stands now (behind it). */
  private ghost(side: Side, color: number, alpha = 0.4, ms = 240): void {
    const fig = this.figOf(side);
    const copy = side === 'player' ? drawFigure(this, fig.x, fig.y, this.playerLook, 1, session.player.weapon) : drawFigure(this, fig.x, fig.y, this.monster.look, -1);
    copy.setScale(fig.scaleX, fig.scaleY).setAngle(fig.angle);
    this.children.moveBelow(copy, fig);
    fx.fadeGhost(this, copy, color, alpha, ms);
  }

  /** Afterimages trailing a lunge that takes `ms`. */
  private afterimages(side: Side, ms: number, color: number): void {
    for (let i = 0; i < 3; i++) this.time.delayedCall((ms / 3) * i + 20, () => this.ghost(side, color, 0.35 - i * 0.07, 220));
  }

  /** Attacker's animation. `onContact` fires at the moment the blow lands. */
  private async attackAnim(source: Side, style: Style, onContact: () => void, chain: Chain = SINGLE): Promise<void> {
    const fig = this.figOf(source);
    const dir = source === 'player' ? 1 : -1;
    const home = this.homeX(source);
    const target = { x: this.homeX(source === 'player' ? 'enemy' : 'player'), y: GROUND_Y - CHEST };
    const trail = style.element ? style.colors[1] : source === 'player' ? 0x9fd3ff : MONSTER_HIT_COLOR;
    const reach = target.x - 170 * dir;

    // Combo hits after the first: already in reach, a quick step back and in, then the next blow.
    if (!chain.first && style.kind !== 'bolt' && style.kind !== 'proc') {
      await this.tween({ targets: fig, x: reach - 26 * dir, duration: 60, yoyo: true, ease: 'Quad.easeOut' });
      sfx.play(this, 'slash');
      this.swing(fig);
      onContact();
      if (chain.last) await this.tween({ targets: fig, x: home, angle: 0, duration: 240, ease: 'Quad.easeOut' });
      return;
    }

    switch (style.kind) {
      case 'slash':
      case 'enemy-melee': {
        await this.windUp(fig, dir, 80);
        sfx.play(this, 'whoosh');
        this.afterimages(source, 150, trail);
        fx.speedLines(this, fig.x, GROUND_Y, dir as 1 | -1, trail);
        await this.tween({ targets: fig, x: reach, angle: 4 * dir, duration: 150, ease: 'Quad.easeIn' });
        fx.dust(this, reach, GROUND_Y, 3);
        sfx.play(this, 'slash');
        this.swing(fig);
        onContact();
        if (chain.last) await this.tween({ targets: fig, x: home, angle: 0, duration: 240, ease: 'Quad.easeOut' });
        break;
      }
      case 'heavy': {
        // Crouch, leap, crash down.
        await this.windUp(fig, dir, 130, true);
        fx.dust(this, fig.x, GROUND_Y, 5);
        sfx.play(this, 'whoosh');
        this.afterimages(source, 300, trail);
        const hop = { t: 0 };
        const startX = fig.x;
        const endX = target.x - 180 * dir;
        await this.tween({
          targets: hop,
          t: 1,
          duration: 300,
          ease: 'Sine.easeIn',
          onUpdate: () => {
            fig.x = Phaser.Math.Linear(startX, endX, hop.t);
            fig.y = GROUND_Y - Math.sin(hop.t * Math.PI) * 130;
            fig.angle = -9 * dir + hop.t * 21 * dir;
          },
        });
        fig.y = GROUND_Y;
        sfx.play(this, 'slash');
        this.swing(fig, true);
        onContact();
        fx.dust(this, fig.x, GROUND_Y, 10);
        this.cameras.main.shake(180, 0.01);
        if (chain.last) await this.tween({ targets: fig, x: home, angle: 0, duration: 300, ease: 'Quad.easeOut' });
        break;
      }
      case 'bolt':
      case 'enemy-spit': {
        const spit = style.kind === 'enemy-spit';
        if (chain.first) {
          await this.tween({ targets: fig, x: home - 20 * dir, duration: 120 });
          const charge = spit ? 180 : style.big ? 520 : 300;
          sfx.play(this, 'charge');
          this.children.moveBelow(fx.castCircle(this, home, GROUND_Y, style.colors[1], charge), fig);
          await fx.chargeUp(this, fig.x + 50 * dir, GROUND_Y - CHEST - 10, style.colors[1], charge);
        }
        const hand = { x: fig.x + 50 * dir, y: GROUND_Y - CHEST - 10 };
        sfx.play(this, style.element === 'energy' ? 'zap' : 'fireball');
        // Combo bolts fly faster, without a new wind-up.
        const flight = !chain.first ? 260 : spit ? 360 : style.big ? 520 : 420;
        await Promise.all([
          chain.first ? this.tween({ targets: fig, x: home + 20 * dir, duration: 120, ease: 'Back.easeOut' }) : Promise.resolve(),
          fx.projectile(this, hand, target, style.colors, flight, style.element),
        ]);
        sfx.play(this, 'explode');
        if (style.big) {
          fx.explosion(this, target.x, target.y, style.colors);
          this.cameras.main.shake(260, 0.012);
        }
        onContact();
        if (chain.last) await this.tween({ targets: fig, x: home, duration: 200 });
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

  private impact(e: Extract<BattleEvent, { kind: 'hit' }>, style: Style, chain: Chain = SINGLE): void {
    const targetSide: Side = e.source === 'player' ? 'enemy' : 'player';
    const fig = this.figOf(targetSide);
    const dir = e.source === 'player' ? 1 : -1;
    const x = this.homeX(targetSide);
    const y = GROUND_Y - CHEST;
    const magic = e.damageType === 'magic';
    const maxHp = targetSide === 'enemy' ? this.battle.enemy.maxHp : this.battle.player.maxHp;
    const weight = Phaser.Math.Clamp(e.damage / maxHp, 0.05, 0.6);
    const palette = e.element ? style.colors : undefined;
    if (e.absorbed) {
      // The element soaks in and heals it.
      fx.absorbInto(this, x, y, style.colors);
      fx.healSparkles(this, x, GROUND_Y, 0x7dff9a);
      fx.whiteFlash(this, fig, 160, 0x7dff9a);
      sfx.play(this, 'heal');
      floatText(this, x, this.textY(targetSide, GROUND_Y - 230), `+${e.damage}`, COLORS.textGood, 40, EFFECT_TAGS.absorb);
      this.refreshBars();
      return;
    }
    const dull = e.effect === 'resisted' || e.effect === 'immune';
    const guarded = targetSide === 'player' && this.shield !== null;
    const big = e.crit || e.effect === 'weak' || style.kind === 'heavy' || !!style.big;
    // Combo hits in the middle of a chain land lighter, so long skills stay snappy.
    const quick = !chain.last;

    // The blow itself: a crescent from the hero's weapon, claw rakes from a monster.
    if (style.kind === 'slash') {
      const color = e.element ? style.colors[1] : 0xffffff;
      this.twinFlip = !this.twinFlip;
      fx.slash(this, x, y, dir as 1 | -1, color, this.twinFlip ? 0.35 : -0.35);
    } else if (style.kind === 'enemy-melee') {
      fx.clawMarks(this, x, y, dir as 1 | -1, e.element ? style.colors[1] : MONSTER_HIT_COLOR);
    } else if (style.kind === 'heavy') {
      const color = e.element ? style.colors[1] : 0xffe08a;
      fx.slash(this, x, y, dir as 1 | -1, color, 0.6, true);
      fx.shockwave(this, x, GROUND_Y, color, 3, 0.3);
    }

    // Sparks, then the element on top: flames, shards, splash, gust, rocks, crackle, light or shadow.
    fx.hitSparks(this, x, y, magic, e.crit, dull ? [0xdddddd, 0x999999, 0x666666] : palette);
    if (e.element && !dull) fx.elementBurst(this, x, y, e.element, big ? 1 : 0.35 + weight);
    if (e.effect === 'weak') fx.shockwave(this, x, y, palette ? palette[1] : 0xffe08a, 3.2, 1);
    if (e.crit) {
      fx.critFlare(this, x, y, palette ? palette[1] : 0xffe08a);
      const cam = this.cameras.main;
      cam.zoomTo(1.03, 60, 'Quad.easeOut', true, (_c: Phaser.Cameras.Scene2D.Camera, progress: number) => {
        if (progress === 1) cam.zoomTo(1, 180, 'Quad.easeOut', true);
      });
    }
    if (guarded && this.shield) {
      // The Defend bubble takes the blow.
      fx.deflect(this, x - 90 * dir, y, dir as 1 | -1, GUARD_COLOR);
      this.shield.setStrokeStyle(10, 0xffffff, 1);
      const s = this.shield;
      this.time.delayedCall(90, () => s.active && s.setStrokeStyle(6, GUARD_COLOR, 0.9));
      sfx.play(this, 'block');
    }

    sfx.play(this, big ? 'crit' : 'hit');
    if (e.element && !dull) sfx.play(this, sfx.ELEMENT_SFX[e.element]);
    fx.whiteFlash(this, fig, big ? 140 : 100, 0xffffff, palette && !dull ? palette[1] : undefined);
    fx.hitStop(this, quick ? 35 : e.crit ? 130 : big ? 110 : 60);
    this.cameras.main.shake(e.crit ? 220 : 140, (guarded || dull ? 0.002 : 0.004) + weight * (quick ? 0.012 : 0.03));

    // Knockback and a recoil tilt, bigger for heavier blows. A new blow cuts the last one's short.
    this.recoil?.stop();
    fig.setPosition(x, GROUND_Y).setAngle(0);
    const push = guarded ? 8 : 16 + weight * 60 + (e.crit ? 18 : 0);
    this.recoil = this.tweens.add({
      targets: fig,
      x: x + push * dir,
      angle: (guarded ? 2 : 5 + weight * 10) * dir,
      duration: 80,
      yoyo: true,
      ease: 'Quad.easeOut',
      onComplete: () => fig.setAngle(0),
    });

    const color = e.element ? ELEMENT_TEXT[e.element] : magic ? COLORS.textMagic : e.source === 'enemy' ? COLORS.textBad : '#ffffff';
    const tags = [e.crit ? 'CRIT!' : '', e.effect ? EFFECT_TAGS[e.effect] : '', guarded ? 'Guarded' : ''].filter(Boolean).join(' ');
    const [ox, oy] = this.numberSpot();
    floatText(this, x + ox, this.textY(targetSide, GROUND_Y - 230) + oy, `${e.damage}`, color, e.crit || e.effect === 'weak' ? 50 : 36, tags || undefined);
    this.refreshBars();
  }

  private dodge(side: Side): void {
    sfx.play(this, 'miss');
    const fig = this.figOf(side);
    const away = side === 'enemy' ? 1 : -1;
    // An afterimage stays where it stood as it sidesteps.
    this.ghost(side, 0x9fd3ff, 0.45, 300);
    fx.slash(this, this.homeX(side) - 60 * away, GROUND_Y - CHEST, -away as 1 | -1, 0xffffff, 0);
    fx.dust(this, this.homeX(side), GROUND_Y, 4);
    this.tweens.add({ targets: fig, x: this.homeX(side) + 60 * away, duration: 120, yoyo: true, hold: 120, ease: 'Quad.easeOut' });
    const [ox, oy] = this.numberSpot();
    floatText(this, this.homeX(side) + ox, this.textY(side, GROUND_Y - 220) + oy, 'Miss', COLORS.textDim, 30);
  }

  /** Where floating text over a side starts: the enemy's starts lower under a tall info panel. */
  private textY(side: Side, y: number): number {
    return side === 'enemy' ? y + this.enemyTextShift : y;
  }

  /** Offset for this hit's number: the usual spot, or the next free one in a combo. */
  private numberSpot(): number[] {
    return COMBO_SPOTS[this.comboHits % COMBO_SPOTS.length];
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
