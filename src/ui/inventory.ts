import Phaser from 'phaser';
import { featureOn } from '../game/Features';
import { reroll, rerollableLines, stoneFor, stonesNeeded } from '../game/Stones';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { DAMAGE_TYPE_LABELS, ELEMENT_LABELS } from '../data/elements';
import type { ConsumableDef, DamageType, EquipSlot, ItemDef, MaterialDef, StoneDef } from '../data/types';
import { EQUIP_SLOTS, gearDef, grade, gradeTier, isGearDef, kindLabel, sellValue, SIGIL_TIERS, ROLL_LABELS, SLOT_LABELS, slotsFor, type Gear, type GearDef, type GearTraits } from '../game/Items';
import { activeSetBonuses, setCounts } from '../game/Sets';
import type { Player } from '../game/Player';
import { Button } from './Button';
import { drawFigure } from './figures';
import {
  animateSigilIcon,
  compareGear,
  defenseText,
  drawItemIcon,
  GRADE_COLORS,
  gradeInfo,
  gradeLabel,
  itemLines,
  itemName,
  MOD_CSS,
  modLines,
  restoreText,
  rollLines,
  setBonusText,
  setCard,
  setCompareLines,
  SIGIL_COLORS,
  sigilLabel,
} from './itemUi';
import { Modal } from './Modal';
import { play } from './sfx';
import { addText, toast } from './text';

/**
 * Grid inventory (I) and the shop. Both share the grid, the tabs and the item card:
 * - bag:  hero with 8 equipment slots around it on the left; equip by double-click, the card's button, or drag.
 * - shop: Buy (the merchant's stock, always Standard) and Sell (your bag) on the left.
 */
type Mode = 'bag' | 'buy' | 'sell';
type Tab = 'all' | 'weapon' | 'armor' | 'accessory' | 'consumable' | 'material';
type SortMode = 'newest' | 'grade' | 'type' | 'value';

type Entry =
  | { kind: 'gear'; key: string; gear: Gear; def: GearDef; slot: EquipSlot | null }
  | { kind: 'stack'; key: string; def: ConsumableDef | MaterialDef | StoneDef; qty: number }
  | { kind: 'stock'; key: string; def: ItemDef };

const W = 1240;
const H = 680;
const CELL = 80;
const GAP = 8;
const COLS = 6;
const ROWS = 5;
const PER_PAGE = COLS * ROWS;
const GRID_X = 316;
const GRID_Y = 132;
const GRID_W = COLS * CELL + (COLS - 1) * GAP;
const CARD_X = 858;
const CARD_Y = 70;
const CARD_W = 358;
const CARD_H = 590;
const SLOT_BG = 0x2a2236;
/** The hero's equipment slots: left column armor, right column weapon and jewelry. */
const EQ = 58;
const EQ_POS: Record<EquipSlot, { x: number; y: number }> = {
  helm: { x: 42, y: 112 },
  body: { x: 42, y: 178 },
  gloves: { x: 42, y: 244 },
  boots: { x: 42, y: 310 },
  weapon: { x: 258, y: 112 },
  necklace: { x: 258, y: 178 },
  ring1: { x: 258, y: 244 },
  ring2: { x: 258, y: 310 },
};
const EMPTY_BORDER = 0x4a3f5a;

const TABS: { tab: Tab; label: string }[] = [
  { tab: 'all', label: 'All' },
  { tab: 'weapon', label: 'Weapons' },
  { tab: 'armor', label: 'Armor' },
  { tab: 'accessory', label: 'Jewelry' },
  { tab: 'consumable', label: 'Items' },
  { tab: 'material', label: 'Materials' },
];
const SORTS: { sort: SortMode; label: string }[] = [
  { sort: 'newest', label: 'Newest' },
  { sort: 'grade', label: 'Grade' },
  { sort: 'type', label: 'Type' },
  { sort: 'value', label: 'Value' },
];
const TYPE_ORDER: Record<ItemDef['type'], number> = { weapon: 0, armor: 1, accessory: 2, consumable: 3, stone: 4, material: 5 };
/** Grades below this are offered by "Sell junk" (Standard and Fair). */
const JUNK_BELOW = 40;

/** Remembered while the game runs, so the inventory reopens the way you left it. */
const remembered = { tab: 'all' as Tab, sort: 'newest' as SortMode };

export function openInventory(scene: Phaser.Scene, player: Player, onChange: () => void): Modal {
  return new InventoryView(scene, player, 'bag', 'Inventory', [], onChange).modal;
}

export function openShop(scene: Phaser.Scene, player: Player, title: string, stock: string[], onChange: () => void): Modal {
  return new InventoryView(scene, player, 'buy', title, stock, onChange).modal;
}

class InventoryView {
  readonly modal: Modal;
  private page = 0;
  private selected: string | null = null;
  private card!: Phaser.GameObjects.Container;
  private entries: Entry[] = [];
  private equipSlots: { slot: EquipSlot; bg: Phaser.GameObjects.Rectangle }[] = [];
  private gridZone!: Phaser.GameObjects.Rectangle;
  private dragging = false;
  private lastDragEnd = 0;
  private lastClick = { key: '', time: 0 };
  private readonly oldDragThreshold: number;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly player: Player,
    private mode: Mode,
    title: string,
    private readonly stock: string[],
    private readonly onChange: () => void,
  ) {
    this.modal = new Modal(scene, W, H, title, () => {});
    // A small move turns a press into a drag; less than that is a click.
    this.oldDragThreshold = scene.input.dragDistanceThreshold;
    scene.input.dragDistanceThreshold = 10;
    this.modal.once(Phaser.GameObjects.Events.DESTROY, () => {
      // Everything seen while open is no longer new.
      for (const g of player.data.gear) delete g.isNew;
      // Skip when the whole scene is shutting down.
      if (!scene.sys.isActive()) return;
      scene.input.dragDistanceThreshold = this.oldDragThreshold;
      onChange();
    });
    this.render();
  }

  // --- Data ---

  private bagEntries(): Entry[] {
    const p = this.player;
    const gear: Entry[] = p.data.gear.map((g) => ({ kind: 'gear', key: g.uid, gear: g, def: gearDef(g), slot: null }));
    const stacks: Entry[] = p.data.inventory.map((e) => ({ kind: 'stack', key: `stack:${e.itemId}`, def: db.item(e.itemId) as ConsumableDef | MaterialDef | StoneDef, qty: e.qty }));
    // Newest first; uids count up.
    gear.reverse();
    return [...gear, ...stacks];
  }

  private equippedEntry(slot: EquipSlot): Entry | null {
    const g = this.player.data.equipment[slot];
    return g ? { kind: 'gear', key: g.uid, gear: g, def: gearDef(g), slot } : null;
  }

  private shown(): Entry[] {
    const all: Entry[] =
      this.mode === 'buy' ? this.stock.map((id) => ({ kind: 'stock', key: `stock:${id}`, def: db.item(id) })) : this.bagEntries();
    // Materials live in their own pouch (no bag slots), so only their tab shows them.
    // Stones sit with the potions under Items.
    const tabOf = (t: ItemDef['type']) => (t === 'stone' ? 'consumable' : t);
    const filtered = remembered.tab === 'all' ? all.filter((e) => e.def.type !== 'material') : all.filter((e) => tabOf(e.def.type) === remembered.tab);
    // Sigils rank above every grade: Eclipse 300+, Dusk 200+, Dawn 100+.
    const sigilRank = (e: Entry) => (e.kind === 'gear' && e.gear.sigil ? (SIGIL_TIERS.indexOf(e.gear.sigil) + 1) * 100 : 0);
    const gradeOf = (e: Entry) => (e.kind === 'gear' ? sigilRank(e) + grade(e.def, e.gear.rolls, e.gear.sigil) : -1);
    const valueOf = (e: Entry) => (e.kind === 'gear' ? sellValue(e.def, e.gear) : e.kind === 'stack' ? sellValue(e.def) : e.def.price);
    const byType = (a: Entry, b: Entry) => TYPE_ORDER[a.def.type] - TYPE_ORDER[b.def.type];
    switch (remembered.sort) {
      case 'grade':
        return filtered.sort((a, b) => gradeOf(b) - gradeOf(a) || byType(a, b));
      case 'type':
        return filtered.sort((a, b) => byType(a, b) || a.def.name.localeCompare(b.def.name) || gradeOf(b) - gradeOf(a));
      case 'value':
        return filtered.sort((a, b) => valueOf(b) - valueOf(a));
      case 'newest':
        return filtered;
    }
  }

  private find(key: string | null): Entry | null {
    if (!key) return null;
    return this.entries.find((e) => e.key === key) ?? EQUIP_SLOTS.map((s) => this.equippedEntry(s)).find((e) => e?.key === key) ?? null;
  }

  // --- Layout ---

  private render(): void {
    const { scene } = this;
    const c = this.modal.content;
    c.removeAll(true);
    this.equipSlots = [];
    this.entries = this.shown();
    if (this.selected && !this.find(this.selected)) this.selected = null;

    if (this.mode === 'bag') this.renderHero();
    else this.renderShopSide();

    // Tabs
    // Materials tab once materials drop, or as soon as you carry one (a quest item).
    const hasMaterials = this.player.data.inventory.some((e) => db.item(e.itemId).type === 'material');
    const tabs = TABS.filter((t) => t.tab !== 'material' || hasMaterials || featureOn(this.player, 'materials'));
    if (!tabs.some((t) => t.tab === remembered.tab)) remembered.tab = 'all';
    tabs.forEach((t, i) => {
      const active = remembered.tab === t.tab;
      const b = new Button(scene, GRID_X + 42 + i * 88, 96, t.label, () => {
        remembered.tab = t.tab;
        this.page = 0;
        this.render();
      }, { width: 84, height: 40, fontSize: 14, color: active ? 0x6b5320 : COLORS.button });
      c.add(b);
    });

    // Grid area (also the drop target for unequipping).
    this.gridZone = scene.add.rectangle(GRID_X - 6, GRID_Y - 6, GRID_W + 12, ROWS * CELL + (ROWS - 1) * GAP + 12, 0x000000, 0.18).setOrigin(0).setRounded(10);
    c.add(this.gridZone);
    const pages = Math.max(1, Math.ceil(this.entries.length / PER_PAGE));
    this.page = Math.min(this.page, pages - 1);
    const onPage = this.entries.slice(this.page * PER_PAGE, (this.page + 1) * PER_PAGE);
    for (let i = 0; i < PER_PAGE; i++) {
      const x = GRID_X + (i % COLS) * (CELL + GAP) + CELL / 2;
      const y = GRID_Y + Math.floor(i / COLS) * (CELL + GAP) + CELL / 2;
      c.add(this.slot(x, y, onPage[i] ?? null));
    }

    // Bottom row: sort, pages, capacity.
    const sortIndex = SORTS.findIndex((s) => s.sort === remembered.sort);
    c.add(
      new Button(scene, GRID_X + 85, 606, `Sort: ${SORTS[sortIndex].label}`, () => {
        remembered.sort = SORTS[(sortIndex + 1) % SORTS.length].sort;
        this.render();
      }, { width: 170, height: 42, fontSize: 17 }),
    );
    if (pages > 1) {
      const mid = GRID_X + GRID_W / 2 + 40;
      c.add(addText(scene, mid, 606, `${this.page + 1} / ${pages}`, { size: 16, color: COLORS.textDim }).setOrigin(0.5));
      c.add(new Button(scene, mid - 60, 606, '<', () => { this.page -= 1; this.render(); }, { width: 44, height: 38 }).setEnabled(this.page > 0));
      c.add(new Button(scene, mid + 60, 606, '>', () => { this.page += 1; this.render(); }, { width: 44, height: 38 }).setEnabled(this.page < pages - 1));
    }
    if (this.mode !== 'buy') {
      const used = this.player.usedSlots;
      const full = used >= BALANCE.bagSlots;
      c.add(addText(scene, GRID_X + GRID_W, 606, `Bag ${used} / ${BALANCE.bagSlots}`, { size: 18, bold: full, color: full ? COLORS.textBad : COLORS.textDim }).setOrigin(1, 0.5));
    }

    this.card = scene.add.container(0, 0);
    c.add(this.card);
    this.showCard(this.find(this.selected));
  }

  private renderHero(): void {
    const { scene, player } = this;
    const c = this.modal.content;
    const look = player.look;
    const hero = drawFigure(scene, 150, 352, look, 1, player.weapon, player.weaponSigil);
    hero.setScale(hero.scaleX * 0.8, hero.scaleY * 0.8);
    c.add(hero);

    for (const slot of EQUIP_SLOTS) c.add(this.slot(EQ_POS[slot].x, EQ_POS[slot].y, this.equippedEntry(slot), slot, EQ));

    const [min, max] = player.attackRange;
    const element = player.weaponElement;
    const d = { melee: player.defenseFor('melee'), pierce: player.defenseFor('pierce'), magic: player.defenseFor('magic') };
    const lines = [
      `Attack ${min}-${max} ${DAMAGE_TYPE_LABELS[player.weaponDamageType]} · ${element ? ELEMENT_LABELS[element] : 'Neutral'}`,
      `Crit ${(player.critChance * 100).toFixed(1)}%${player.damageBonus ? ` · Damage +${Math.round(player.damageBonus * 100)}%` : ''}`,
      defenseText(d),
      `Max HP ${player.maxHp} · Max MP ${player.maxMp}`,
      `Gold ${player.gold}`,
    ];
    const stats = addText(scene, 20, 378, lines.join('\n'), { size: 15, wrap: 284 }).setLineSpacing(5);
    c.add(stats);
    // Sets being worn, and what they switch on.
    let y = stats.y + stats.height + 8;
    const active = activeSetBonuses(player.data.equipment);
    for (const [id, count] of setCounts(player.data.equipment)) {
      const set = db.set(id);
      const t = addText(scene, 20, y, `${set.name} set ${count}/${set.pieces.length}`, { size: 15, bold: true, color: COLORS.textGold });
      c.add(t);
      y += t.height + 2;
      for (const b of set.bonuses) {
        const on = active.some((a) => a.set.id === id && a.bonus === b);
        const bt = addText(scene, 28, y, `(${b.pieces}) ${setBonusText(b)}`, { size: 13, color: on ? COLORS.textGood : COLORS.textDim, wrap: 270 });
        c.add(bt);
        y += bt.height + 2;
      }
    }
  }

  private renderShopSide(): void {
    const { scene, player } = this;
    const c = this.modal.content;
    c.add(addText(scene, 30, 70, `Gold: ${player.gold}`, { size: 26, bold: true, color: COLORS.textGold }));
    (['buy', 'sell'] as const).forEach((m, i) => {
      c.add(
        new Button(scene, 90 + i * 130, 150, m === 'buy' ? 'Buy' : 'Sell', () => {
          this.mode = m;
          this.page = 0;
          this.selected = null;
          this.render();
        }, { width: 120, height: 48, color: this.mode === m ? 0x6b5320 : COLORS.button }),
      );
    });
    const hint =
      this.mode === 'buy'
        ? 'Shop gear is always Standard (0%). Gear from monsters can roll better.'
        : 'Better grades sell for more, Sigils far more. Locked items are never sold.';
    c.add(addText(scene, 30, 196, hint, { size: 16, color: COLORS.textDim, wrap: 260 }));

    if (this.mode === 'sell') {
      const junk = player.data.gear.filter((g) => !g.locked && !g.sigil && !g.plus && grade(gearDef(g), g.rolls, g.sigil) < JUNK_BELOW);
      const value = junk.reduce((sum, g) => sum + sellValue(gearDef(g), g), 0);
      const btn = new Button(scene, 150, 310, 'Sell junk', () => {
        let gold = 0;
        for (const g of junk) gold += player.sellGear(g.uid);
        play(scene, 'coin');
        toast(scene, `Sold ${junk.length} item${junk.length > 1 ? 's' : ''} for ${gold} g`, COLORS.textGold);
        this.changed();
      }, { width: 250, height: 60, sublabel: junk.length ? `${junk.length} Standard/Fair · ${value} g` : 'Nothing to sell' });
      btn.setEnabled(junk.length > 0);
      c.add(btn);
    }
  }

  /** One grid cell. `equipSlot` marks the hero's equipment slots (drawn at `size`). */
  private slot(x: number, y: number, entry: Entry | null, equipSlot?: EquipSlot, size = CELL): Phaser.GameObjects.Container {
    const { scene } = this;
    const s = scene.add.container(x, y);
    const info = entry ? gradeInfo(entry.def, entry.kind === 'gear' ? entry.gear : undefined) : null;
    const bg = scene.add
      .rectangle(0, 0, size, size, SLOT_BG)
      .setRounded(8)
      .setStrokeStyle(entry ? 3 : 2, info ? info.frame : EMPTY_BORDER);
    s.add(bg);
    if (equipSlot) this.equipSlots.push({ slot: equipSlot, bg });
    if (!entry) {
      if (equipSlot) s.add(addText(scene, 0, 0, SLOT_LABELS[equipSlot], { size: 12, align: 'center', color: COLORS.textDim }).setOrigin(0.5));
      return s;
    }

    if (entry.key === this.selected) s.add(scene.add.rectangle(0, 0, size + 8, size + 8).setRounded(11).setStrokeStyle(3, 0xffffff));
    const iconSize = Math.round(size * 0.75);
    const icon = drawItemIcon(scene, entry.def, iconSize);
    s.add(icon);
    if (info?.sigil) {
      animateSigilIcon(scene, icon, info.sigil, iconSize);
      s.add(addText(scene, -size / 2 + 5, size / 2 - 2, '✦', { size: 16, bold: true, color: SIGIL_COLORS[info.sigil].css, stroke: true }).setOrigin(0, 1));
    }
    if (entry.kind === 'stack' && entry.qty > 1) {
      s.add(addText(scene, CELL / 2 - 6, CELL / 2 - 4, `${entry.qty}`, { size: 17, bold: true, stroke: true }).setOrigin(1, 1));
    }
    if (entry.kind === 'stock') {
      s.add(addText(scene, 0, CELL / 2 - 3, `${entry.def.price} g`, { size: 14, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5, 1));
    }
    let newDot: Phaser.GameObjects.Arc | null = null;
    if (entry.kind === 'gear') {
      if (entry.gear.locked) s.add(this.lockBadge(-size / 2 + 13, -size / 2 + 13));
      if (entry.gear.isNew) {
        newDot = scene.add.circle(size / 2 - 10, -size / 2 + 10, 6, 0xffd34d).setStrokeStyle(2, 0x000000);
        s.add(newDot);
      }
      if (!equipSlot && info && info.tier !== 'standard') {
        s.add(addText(scene, CELL / 2 - 5, CELL / 2 - 3, `${info.grade}%`, { size: 12, bold: true, color: info.css, stroke: true }).setOrigin(1, 1));
      }
    }

    const draggable = this.mode === 'bag' && entry.kind === 'gear';
    bg.setInteractive({ useHandCursor: true, draggable });
    bg.on('pointerover', () => {
      if (this.dragging) return;
      bg.setFillStyle(0x3a3050);
      if (entry.kind === 'gear' && entry.gear.isNew) {
        delete entry.gear.isNew;
        newDot?.destroy();
      }
      this.showCard(entry);
    });
    bg.on('pointerout', () => {
      bg.setFillStyle(SLOT_BG);
      if (!this.dragging) this.showCard(this.find(this.selected));
    });
    bg.on('pointerup', () => {
      if (this.dragging || scene.time.now - this.lastDragEnd < 80) return;
      this.click(entry);
    });
    if (draggable) this.makeDraggable(bg, entry as Extract<Entry, { kind: 'gear' }>);
    return s;
  }

  private lockBadge(x: number, y: number): Phaser.GameObjects.Graphics {
    const g = this.scene.add.graphics().setPosition(x, y);
    g.lineStyle(2.5, 0xe8d9a8).strokeCircle(0, -3, 5);
    g.fillStyle(0xe8d9a8).fillRoundedRect(-7, -2, 14, 10, 2);
    g.fillStyle(0x2a2236).fillRect(-1, 1, 2, 4);
    return g;
  }

  /** Drag bag gear onto the hero side to equip it; drag equipped gear onto the grid to take it off. */
  private makeDraggable(bg: Phaser.GameObjects.Rectangle, entry: Extract<Entry, { kind: 'gear' }>): void {
    const { scene } = this;
    let ghost: Phaser.GameObjects.Container | null = null;
    bg.on('dragstart', (pointer: Phaser.Input.Pointer) => {
      this.dragging = true;
      ghost = drawItemIcon(scene, entry.def, 70).setPosition(pointer.x, pointer.y).setDepth(5200).setAlpha(0.85);
      bg.setAlpha(0.4);
      const fits = slotsFor(entry.def);
      for (const t of this.equipSlots) if (!entry.slot && fits.includes(t.slot)) t.bg.setStrokeStyle(4, 0xffffff);
    });
    bg.on('drag', (pointer: Phaser.Input.Pointer) => ghost?.setPosition(pointer.x, pointer.y));
    bg.on('dragend', (pointer: Phaser.Input.Pointer) => {
      this.dragging = false;
      this.lastDragEnd = scene.time.now;
      ghost?.destroy();
      ghost = null;
      bg.setAlpha(1);
      const heroSide = pointer.x < this.modal.content.x + 300;
      // Dropped on a slot it fits (a ring on the second ring slot): there. Anywhere else on the hero side: its usual slot.
      const target = this.equipSlots.find((t) => t.bg.getBounds().contains(pointer.x, pointer.y))?.slot;
      if (!entry.slot && heroSide) this.equip(entry, target);
      else if (entry.slot && this.gridZone.getBounds().contains(pointer.x, pointer.y)) this.unequip(entry.slot);
      else this.render();
    });
  }

  private click(entry: Entry): void {
    const now = performance.now();
    const double = this.lastClick.key === entry.key && now - this.lastClick.time < 350;
    this.lastClick = { key: entry.key, time: now };
    this.selected = entry.key;
    if (double && this.mode === 'bag') {
      this.primary(entry);
      return;
    }
    this.render();
  }

  /** Double-click action in the bag: equip, unequip or use. */
  private primary(entry: Entry): void {
    if (entry.kind === 'gear') {
      if (entry.slot) this.unequip(entry.slot);
      else this.equip(entry);
    } else if (entry.kind === 'stack' && entry.def.type === 'consumable') this.use(entry.def);
  }

  // --- Actions ---

  private changed(): void {
    this.onChange();
    this.render();
  }

  private equip(entry: Extract<Entry, { kind: 'gear' }>, slot?: EquipSlot): void {
    if (!this.player.equip(entry.gear.uid, slot)) return;
    play(this.scene, 'equip');
    this.selected = entry.gear.uid;
    this.changed();
  }

  private unequip(slot: EquipSlot): void {
    if (!this.player.unequip(slot)) {
      play(this.scene, 'denied');
      toast(this.scene, 'Your bag is full', COLORS.textBad);
      return;
    }
    play(this.scene, 'equip');
    this.changed();
  }

  private use(def: ConsumableDef): void {
    const r = this.player.useConsumable(def.id);
    if (r) {
      play(this.scene, 'heal');
      toast(this.scene, restoreText(r), COLORS.textGood);
    } else toast(this.scene, 'Already full', COLORS.textDim);
    this.changed();
  }

  private buy(def: ItemDef): void {
    const result = this.player.buy(def.id);
    if (result === 'ok') {
      play(this.scene, 'coin');
      toast(this.scene, `Bought ${def.name}`, COLORS.textGood);
    } else {
      play(this.scene, 'denied');
      toast(this.scene, result === 'gold' ? 'Not enough gold' : 'Your bag is full', COLORS.textBad);
    }
    this.changed();
  }

  private sell(entry: Entry): void {
    const gold = entry.kind === 'gear' ? this.player.sellGear(entry.gear.uid) : entry.kind === 'stack' ? this.player.sellItem(entry.def.id) : 0;
    if (gold <= 0) return;
    play(this.scene, 'coin');
    toast(this.scene, `Sold ${entry.def.name} for ${gold} g`, COLORS.textGold);
    this.changed();
  }

  // --- Item card ---

  private showCard(entry: Entry | null): void {
    const { scene } = this;
    const card = this.card;
    if (!card?.scene) return;
    card.removeAll(true);
    const info = entry ? gradeInfo(entry.def, entry.kind === 'gear' ? entry.gear : undefined) : null;
    card.add(
      scene.add
        .rectangle(CARD_X, CARD_Y, CARD_W, CARD_H, 0x241d30)
        .setOrigin(0)
        .setRounded(12)
        .setStrokeStyle(info?.sigil ? 3 : 2, info && (info.sigil || info.tier !== 'standard') ? info.frame : COLORS.panelBorder),
    );
    if (!entry || !info) {
      const hint = this.mode === 'bag' ? 'Hover or tap an item.\n\nDouble-click or drag gear\nonto the hero to equip it.' : 'Hover or tap an item.';
      card.add(addText(scene, CARD_X + CARD_W / 2, CARD_Y + CARD_H / 2, hint, { size: 18, align: 'center', color: COLORS.textDim }).setOrigin(0.5));
      return;
    }

    const left = CARD_X + 20;
    const iconBg = scene.add.rectangle(left + 38, CARD_Y + 56, 76, 76, SLOT_BG).setRounded(8).setStrokeStyle(3, info.frame);
    const icon = drawItemIcon(scene, entry.def, 58).setPosition(left + 38, CARD_Y + 56);
    card.add([iconBg, icon]);
    if (info.sigil) animateSigilIcon(scene, icon, info.sigil, 58);
    const name = addText(scene, left + 92, CARD_Y + 18, itemName(entry.def, entry.kind === 'gear' ? entry.gear : undefined), { size: 22, bold: true, color: info.nameCss, wrap: CARD_W - 120 });
    card.add(name);
    let headY = name.y + name.height + 2;
    if (info.sigil) {
      const sig = addText(scene, left + 92, headY, sigilLabel(info.sigil), { size: 17, bold: true, color: SIGIL_COLORS[info.sigil].css });
      card.add(sig);
      headY += sig.height + 2;
    }
    const kind = isGearDef(entry.def) ? kindLabel(entry.def) : { material: 'Material', stone: 'Roll stone', consumable: 'Potion' }[entry.def.type];
    // Gear shows its tier, which also sets how many modifier lines it can carry.
    const tiered = isGearDef(entry.def) ? `Tier ${entry.def.tier} ${kind}` : kind;
    const sub = entry.kind === 'gear' ? `${tiered} Â· ${gradeLabel(info)}${entry.slot ? ' Â· Equipped' : ''}` : entry.kind === 'stack' ? `${kind} Â· x${entry.qty}` : tiered;
    const subText = addText(scene, left + 92, headY, sub, { size: 15, color: COLORS.textDim });
    card.add(subText);

    let y = Math.max(CARD_Y + 112, subText.y + subText.height + 10);
    const line = (text: string, color: string = COLORS.text, size = 16) => {
      const t = addText(scene, left, y, text, { size, color, wrap: CARD_W - 40 });
      card.add(t);
      y += t.height + 4;
    };

    const traits: GearTraits = entry.kind === 'gear' ? entry.gear : { rolls: {} };
    for (const l of itemLines(entry.def, traits)) line(l);

    if (isGearDef(entry.def)) {
      y += 6;
      for (const r of rollLines(entry.def, traits.rolls, traits.sigil)) {
        card.add(addText(scene, left, y, r.label, { size: 15, color: COLORS.textDim }));
        card.add(addText(scene, left + 118, y, r.effect, { size: 15, bold: true }));
        const barX = left + 178;
        card.add(scene.add.rectangle(barX, y + 9, 100, 8, 0x111111).setOrigin(0, 0.5));
        if (r.quality > 0) card.add(scene.add.rectangle(barX, y + 9, r.quality, 8, GRADE_COLORS[gradeTier(r.quality).tier].color).setOrigin(0, 0.5));
        card.add(addText(scene, CARD_X + CARD_W - 20, y, `${r.quality}%`, { size: 15, color: COLORS.textDim }).setOrigin(1, 0));
        y += 24;
      }
      // Roll stones: re-roll a line right here (in the bag only).
      if (this.mode === 'bag' && entry.kind === 'gear') {
        const gear = entry.gear;
        const need = stonesNeeded(gear);
        for (const key of rerollableLines(gear)) {
          const stone = stoneFor(key)!;
          const have = this.player.countItem(stone.id);
          const b = new Button(scene, left + 159, y + 15, `Re-roll ${ROLL_LABELS[key]}: ${need} ${stone.name}${need > 1 ? 's' : ''} (have ${have})`, () => {
            const r = reroll(this.player, gear, key);
            if (!r) return;
            play(scene, r.after > r.before ? 'levelup' : 'click');
            toast(scene, `${ROLL_LABELS[key]}: ${r.before}% -> ${r.after}%`, r.after >= r.before ? COLORS.textGood : COLORS.textBad);
            this.changed();
          }, { width: 318, height: 30, fontSize: 13 }).setEnabled(have >= need);
          card.add(b);
          y += 36;
        }
      }
      // Mods: the blue lines.
      for (const m of modLines(traits)) {
        card.add(addText(scene, left, y, m.text, { size: 15, bold: true, color: MOD_CSS }));
        const barX = left + 178;
        card.add(scene.add.rectangle(barX, y + 9, 100, 8, 0x111111).setOrigin(0, 0.5));
        if (m.quality > 0) card.add(scene.add.rectangle(barX, y + 9, m.quality, 8, 0x4a9cf0).setOrigin(0, 0.5));
        card.add(addText(scene, CARD_X + CARD_W - 20, y, `${m.quality}%`, { size: 15, color: COLORS.textDim }).setOrigin(1, 0));
        y += 24;
      }
    }

    y += 6;
    line(entry.def.description, COLORS.textDim, 15);

    // The item's set: pieces (worn ones ticked) and bonuses (active ones lit).
    const set = isGearDef(entry.def) ? setCard(entry.def.id, this.player.data.equipment) : null;
    if (set) {
      y += 4;
      line(set.title, COLORS.textGold, 15);
      for (const l of set.lines) line(l.text, l.on ? COLORS.textGood : COLORS.textDim, 14);
    }

    // Compare with what is worn in the slot it would go to, for gear that isn't worn.
    if (entry.kind !== 'stack' && isGearDef(entry.def) && !(entry.kind === 'gear' && entry.slot)) {
      const slot = this.player.slotFor(entry.def);
      const worn = this.player.data.equipment[slot];
      const statBonus = (t: DamageType) => Math.floor(this.player.statFor(t) * BALANCE.statDamageScale);
      const piece: Gear = entry.kind === 'gear' ? entry.gear : { uid: '', itemId: entry.def.id, rolls: {} };
      const diff = [
        ...compareGear(entry.def, traits, worn ? { def: gearDef(worn), gear: worn } : null, statBonus),
        ...setCompareLines(this.player.data.equipment, slot, piece),
      ];
      y += 6;
      line(worn ? `Compared to your ${gearDef(worn).name}:` : 'Nothing equipped in that slot.', COLORS.textGold, 15);
      // As many lines as fit above the price.
      const room = Math.max(1, Math.floor((CARD_Y + CARD_H - 100 - y) / 20));
      const shown = diff.slice(0, Math.min(6, room));
      if (worn && shown.length === 0) line('Same as equipped.', COLORS.textDim, 15);
      for (const d of shown) line(d.text, d.tone === 'good' ? COLORS.textGood : d.tone === 'bad' ? COLORS.textBad : COLORS.textMagic, 15);
    }

    // Value and buttons.
    const value = entry.kind === 'stock' ? `Price ${entry.def.price} g` : `Sells for ${entry.kind === 'gear' ? sellValue(entry.def, entry.gear) : sellValue(entry.def)} g`;
    card.add(addText(scene, left, CARD_Y + CARD_H - 92, value, { size: 16, color: COLORS.textGold }));
    const buttons: { label: string; run: () => void; enabled?: boolean }[] = [];
    if (this.mode === 'bag') {
      if (entry.kind === 'gear') {
        buttons.push(entry.slot ? { label: 'Unequip', run: () => this.unequip(entry.slot!) } : { label: 'Equip', run: () => this.equip(entry) });
      } else if (entry.kind === 'stack' && entry.def.type === 'consumable') {
        const def = entry.def;
        buttons.push({ label: 'Use', run: () => this.use(def) });
      }
    } else if (this.mode === 'buy' && entry.kind === 'stock') {
      buttons.push({ label: `Buy ${entry.def.price} g`, run: () => this.buy(entry.def), enabled: this.player.gold >= entry.def.price });
    } else if (this.mode === 'sell' && entry.kind !== 'stock') {
      const locked = entry.kind === 'gear' && !!entry.gear.locked;
      buttons.push({ label: locked ? 'Locked' : 'Sell', run: () => this.sell(entry), enabled: !locked });
    }
    if (entry.kind === 'gear') {
      buttons.push({
        label: entry.gear.locked ? 'Unlock' : 'Lock',
        run: () => {
          this.player.toggleLock(entry.gear.uid);
          play(scene, 'click');
          this.changed();
        },
      });
    }
    const bw = 150;
    buttons.forEach((b, i) => {
      const x = CARD_X + CARD_W / 2 + (i - (buttons.length - 1) / 2) * (bw + 12);
      const btn = new Button(scene, x, CARD_Y + CARD_H - 40, b.label, b.run, { width: bw, height: 48, fontSize: 18 });
      if (b.enabled === false) btn.setEnabled(false);
      card.add(btn);
    });
  }
}

