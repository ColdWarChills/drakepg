import Phaser from 'phaser';
import { COLORS } from '../config';
import { db } from '../data/db';
import { DAMAGE_TYPE_LABELS, ELEMENTS, ELEMENT_LABELS } from '../data/elements';
import { ARMOR_TYPES } from '../data/gear';
import { RARITY } from '../data/rarity';
import type { ArmorDef, Defenses, ItemDef, NpcDef, Resists } from '../data/types';
import {
  avgQuality,
  gearDef,
  gearName,
  isGear,
  itemSellPrice,
  qualityText,
  rarityColor,
  resolveGear,
  sellPrice,
  traitText,
  type GearInstance,
} from '../game/Gear';
import { STAT_KEYS, STAT_LABELS, type Player, type Rewards } from '../game/Player';
import { Button } from './Button';
import { Modal } from './Modal';
import { play } from './sfx';
import { addText, toast } from './text';

/** One line summary of an item's numbers, for lists. Pass a resolved def to see rolled numbers. */
export function itemSummary(item: ItemDef): string {
  const bonus = (stats?: Partial<Record<string, number>>) =>
    stats
      ? Object.entries(stats)
          .filter(([, v]) => v)
          .map(([k, v]) => ` +${v} ${k.toUpperCase()}`)
          .join('')
      : '';
  switch (item.type) {
    case 'weapon': {
      const element = item.element ? ` ${ELEMENT_LABELS[item.element]}` : '';
      return `${item.damage[0]}-${item.damage[1]} ${DAMAGE_TYPE_LABELS[item.damageType]}${element}${bonus(item.stats)}${item.proc ? `  ·  ${item.proc.name} ${Math.round(item.proc.chance * 100)}%` : ''}`;
    }
    case 'armor':
      return `${ARMOR_TYPES[item.archetype].name}  ${defenseText(item.defense)}${resistText(item.resists, '  ')}${bonus(item.stats)}`;
    case 'consumable':
    case 'material':
      return item.description;
  }
}

/** "Plate: skills +20% MP" */
export function armorTypeText(armor: ArmorDef): string {
  const parts: string[] = [];
  if (armor.mpCost) parts.push(`skills ${armor.mpCost > 0 ? '+' : ''}${Math.round(armor.mpCost * 100)}% MP`);
  if (armor.walk) parts.push(`walk ${armor.walk > 0 ? '+' : ''}${Math.round(armor.walk * 100)}%`);
  return `${ARMOR_TYPES[armor.archetype].name}: ${parts.join(', ') || 'no extras'}`;
}

/** Description plus the numbers a list line has no room for. */
export function itemDetails(item: ItemDef): string {
  let text = `${item.name}: ${item.description}`;
  if (isGear(item)) text += `  Requires level ${item.level}.`;
  if (item.type === 'armor') text += `  ${armorTypeText(item)}.`;
  if (item.type === 'weapon' && item.proc) text += `  Special: ${item.proc.name}, ${Math.round(item.proc.chance * 100)}% chance on Attack.`;
  return text;
}

/** Summary line for an owned piece of gear (rolled numbers). */
export function gearSummary(inst: GearInstance): string {
  return itemSummary(resolveGear(inst));
}

/** Hover text for owned gear: rarity, quality lines, traits, special. */
export function gearDetails(inst: GearInstance): string {
  const base = gearDef(inst.baseId);
  const r = resolveGear(inst);
  const parts = [`${gearName(inst)} (${RARITY[inst.rarity].name}, Lv ${base.level})`, `Quality: ${qualityText(inst)}`];
  if (inst.traits.length) parts.push(`Traits: ${inst.traits.map(traitText).join(', ')}`);
  if (r.type === 'armor') parts.push(armorTypeText(r));
  if (r.type === 'weapon' && r.proc) parts.push(`Special: ${r.proc.name} ${Math.round(r.proc.chance * 100)}% on Attack`);
  return parts.join('.  ') + '.';
}

/** "Iron Sword (Rare 41%)", "Boar Tusk x2" */
export function lootNames(rewards: Rewards): string[] {
  const gear = rewards.gear.map((g) => `${gearName(g)} (${RARITY[g.rarity].name} ${avgQuality(g)}%)`);
  const counts = new Map<string, number>();
  for (const id of rewards.items) counts.set(id, (counts.get(id) ?? 0) + 1);
  const items = [...counts].map(([id, n]) => `${db.item(id).name}${n > 1 ? ` x${n}` : ''}`);
  return [...gear, ...items];
}

/** "DEF Melee 9 · Pierce 10 · Magic 4" */
export function defenseText(d: Defenses): string {
  return `DEF ${(['melee', 'pierce', 'magic'] as const).map((t) => `${DAMAGE_TYPE_LABELS[t]} ${d[t] ?? 0}`).join(' · ')}`;
}

/** "  Fire +15, Ice -10" or '' if none. */
export function resistText(r: Resists | undefined, prefix = ''): string {
  if (!r) return '';
  const parts = ELEMENTS.filter((e) => (r[e] ?? 0) + (r.all ?? 0) !== 0).map((e) => {
    const v = (r[e] ?? 0) + (r.all ?? 0);
    return `${ELEMENT_LABELS[e]} ${v > 0 ? '+' : ''}${v}`;
  });
  return parts.length ? `${prefix}${parts.join(', ')}` : '';
}

/** "+40 HP, +20 MP" */
export function restoreText(r: { hp: number; mp: number }): string {
  const parts = [r.hp ? `+${r.hp} HP` : '', r.mp ? `+${r.mp} MP` : ''].filter(Boolean);
  return parts.length ? parts.join(', ') : 'No effect';
}

/** Bag contents: unworn gear first (newest first), then stackables. */
export type BagEntry = { kind: 'gear'; inst: GearInstance } | { kind: 'stack'; itemId: string; qty: number };

export function bagEntries(player: Player): BagEntry[] {
  const gear: BagEntry[] = [...player.bagGear].reverse().map((inst) => ({ kind: 'gear', inst }));
  const stacks: BagEntry[] = player.data.inventory.map((e) => ({ kind: 'stack', itemId: e.itemId, qty: e.qty }));
  return [...gear, ...stacks];
}

/** "< 1 / 3 >" pager. Returns the clamped page. */
export function addPager(
  scene: Phaser.Scene,
  c: Phaser.GameObjects.Container,
  x: number,
  y: number,
  page: number,
  pages: number,
  go: (page: number) => void,
): void {
  if (pages <= 1) return;
  c.add(addText(scene, x, y, `${page + 1} / ${pages}`, { size: 16, color: COLORS.textDim }).setOrigin(0.5));
  c.add(new Button(scene, x - 80, y, '<', () => go(Math.max(0, page - 1)), { width: 50, height: 36 }).setEnabled(page > 0));
  c.add(new Button(scene, x + 80, y, '>', () => go(Math.min(pages - 1, page + 1)), { width: 50, height: 36 }).setEnabled(page < pages - 1));
}

const ROWS_PER_PAGE = 6;

const STAT_HELP: Record<string, string> = {
  str: 'Melee damage',
  int: 'Magic damage, max MP',
  dex: 'Pierce damage, hit, crit',
  end: 'Max HP',
};

const pct = (v: number) => `${Math.round(v * 100)}%`;

/** Character sheet + equipment + bag. Calls `onChange` after anything that changes the player. */
export function openCharacterPanel(scene: Phaser.Scene, player: Player, onChange: () => void): Modal {
  const modal = new Modal(scene, 1100, 600, 'Character', () => {});
  let page = 0;

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    const s = player.stats;

    // --- Stats column ---
    const [minDmg, maxDmg] = player.attackRange;
    const info = addText(scene, 40, 540, '', { size: 16, color: COLORS.textDim, wrap: 1020 });
    c.add(info);
    c.add(addText(scene, 40, 64, player.name, { size: 26, bold: true }));
    c.add(addText(scene, 40, 98, `Level ${player.level} ${player.classDef.name}   ·   ${player.xp} / ${player.xpNeeded} XP`, { size: 18, color: COLORS.textDim }));
    c.add(addText(scene, 40, 130, `HP  ${player.hp} / ${player.maxHp}      MP  ${player.mp} / ${player.maxMp}`, { size: 20 }));

    const points = player.data.statPoints;
    if (points > 0) {
      const label = addText(scene, 40, 168, `${points} stat point${points > 1 ? 's' : ''} to spend`, { size: 19, bold: true, color: COLORS.textGold });
      scene.tweens.add({ targets: label, alpha: 0.55, duration: 600, yoyo: true, repeat: -1 });
      c.add(label);
    }
    STAT_KEYS.forEach((k, i) => {
      const y = 214 + i * 40;
      c.add(addText(scene, 40, y, STAT_LABELS[k], { size: 21, bold: true }).setOrigin(0, 0.5));
      c.add(addText(scene, 110, y, `${s[k]}`, { size: 21 }).setOrigin(0, 0.5));
      const hover = addText(scene, 165, y, STAT_HELP[k], { size: 15, color: COLORS.textDim }).setOrigin(0, 0.5);
      c.add(hover);
      if (points > 0) {
        hover.setX(205);
        const plus = new Button(scene, 175, y, '+', () => {
          player.spendStatPoint(k);
          onChange();
          render();
        }, { width: 40, height: 34, fontSize: 22 });
        c.add(plus);
      }
    });
    const element = player.weaponElement;
    const armor = player.armor;
    const lines = [
      `Attack  ${minDmg}-${maxDmg} ${DAMAGE_TYPE_LABELS[player.weaponDamageType]} · ${element ? ELEMENT_LABELS[element] : 'Neutral'}`,
      defenseText(armor?.defense ?? {}),
      resistText(player.resists, 'Resists  ') || 'Resists  none',
      `Gear  Hit +${pct(player.hitBonus)} · Crit +${pct(player.critBonus)} · Evade +${pct(player.evasion)}`,
      armor ? armorTypeText(armor) : 'No armor',
      `Gold  ${player.gold}`,
    ];
    c.add(addText(scene, 40, 375, lines.join('\n'), { size: 16 }).setLineSpacing(6));

    // --- Skills (under the equipment column) ---
    c.add(addText(scene, 380, 330, 'Skills', { size: 22, bold: true, color: COLORS.textGold }));
    const perColumn = Math.ceil(player.classDef.skills.length / 2);
    player.classDef.skills.forEach((entry, i) => {
      const skill = db.skill(entry.skillId);
      const known = entry.level <= player.level;
      const x = 380 + Math.floor(i / perColumn) * 180;
      const y = 366 + (i % perColumn) * 27;
      const t = addText(scene, x, y, known ? skill.name : `Lv ${entry.level}  ${skill.name}`, {
        size: 15,
        color: known ? COLORS.textMagic : COLORS.textDim,
      });
      const cd = skill.cooldown > 0 ? `, cooldown ${skill.cooldown}` : '';
      t.setInteractive()
        .on('pointerover', () => info.setText(`${skill.name} (${player.skillCost(skill)} MP${cd}): ${skill.description}`))
        .on('pointerout', () => info.setText(''));
      c.add(t);
    });

    // --- Equipment column ---
    c.add(addText(scene, 380, 70, 'Equipped', { size: 22, bold: true, color: COLORS.textGold }));
    (['weapon', 'armor'] as const).forEach((slot, i) => {
      const y = 112 + i * 108;
      const inst = player.equipped(slot);
      c.add(addText(scene, 380, y, slot === 'weapon' ? 'Weapon' : 'Armor', { size: 16, color: COLORS.textDim }));
      const name = addText(scene, 380, y + 20, inst ? gearName(inst) : '(none)', { size: 21, bold: true, color: inst ? rarityColor(inst) : COLORS.text });
      c.add(name);
      if (!inst) return;
      const summary = addText(scene, 380, y + 48, gearSummary(inst), { size: 14, color: COLORS.textDim, wrap: 340 });
      c.add(summary);
      c.add(addText(scene, 380, summary.y + summary.height + 2, qualityText(inst), { size: 14, color: COLORS.textDim }));
      name.setInteractive().on('pointerover', () => info.setText(gearDetails(inst))).on('pointerout', () => info.setText(''));
      c.add(
        new Button(scene, 660, y + 22, 'Unequip', () => {
          player.unequip(slot);
          onChange();
          render();
        }, { width: 100, height: 36, fontSize: 16 }),
      );
    });

    // --- Bag column ---
    c.add(addText(scene, 740, 70, 'Bag', { size: 22, bold: true, color: COLORS.textGold }));
    const entries = bagEntries(player);
    const pages = Math.max(1, Math.ceil(entries.length / ROWS_PER_PAGE));
    page = Math.min(page, pages - 1);
    const shown = entries.slice(page * ROWS_PER_PAGE, (page + 1) * ROWS_PER_PAGE);
    if (entries.length === 0) c.add(addText(scene, 740, 115, 'Empty', { size: 18, color: COLORS.textDim }));

    shown.forEach((entry, i) => {
      const y = 130 + i * 64;
      if (entry.kind === 'gear') {
        const inst = entry.inst;
        const ok = player.canEquip(inst);
        c.add(addText(scene, 740, y - 22, gearName(inst), { size: 18, color: rarityColor(inst) }));
        c.add(addText(scene, 740, y + 2, `${gearSummary(inst)}`, { size: 13, color: COLORS.textDim, wrap: 220 }));
        const btn = new Button(scene, 1020, y, ok ? 'Equip' : `Lv ${gearDef(inst.baseId).level}`, () => {
          player.equip(inst.uid);
          onChange();
          render();
        }, { width: 100, height: 40, fontSize: 17 }).setEnabled(ok);
        btn.onHover(() => info.setText(gearDetails(inst)), () => info.setText(''));
        c.add(btn);
        return;
      }
      const item = db.item(entry.itemId);
      c.add(addText(scene, 740, y - 22, `${item.name}${entry.qty > 1 ? ` x${entry.qty}` : ''}`, { size: 18 }));
      c.add(addText(scene, 740, y + 2, item.type === 'material' ? 'Material' : itemSummary(item), { size: 13, color: COLORS.textDim, wrap: 220 }));
      if (item.type !== 'consumable') {
        const t = addText(scene, 1020, y, 'i', { size: 18, color: COLORS.textDim }).setOrigin(0.5).setInteractive();
        t.on('pointerover', () => info.setText(itemDetails(item))).on('pointerout', () => info.setText(''));
        c.add(t);
        return;
      }
      const btn = new Button(scene, 1020, y, 'Use', () => {
        const r = player.useConsumable(item.id);
        if (r) {
          play(scene, 'heal');
          toast(scene, restoreText(r), COLORS.textGood);
        } else toast(scene, 'Already full', COLORS.textDim);
        onChange();
        render();
      }, { width: 100, height: 40, fontSize: 17 });
      btn.onHover(() => info.setText(itemDetails(item)), () => info.setText(''));
      c.add(btn);
    });

    addPager(scene, c, 900, 518, page, pages, (p) => {
      page = p;
      render();
    });
  };

  render();
  return modal;
}

const SHOP_ROWS = 7;

/** Buy from `stock`, or sell anything in the bag. Shop gear is always Common at 0% quality. */
export function openShop(scene: Phaser.Scene, player: Player, title: string, stock: string[], onChange: () => void): Modal {
  const modal = new Modal(scene, 860, 620, title, () => {});
  let tab: 'buy' | 'sell' = 'buy';
  let page = 0;
  /** Uid of a precious item waiting for a second click before it is sold. */
  let confirmSell: number | null = null;

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    c.add(addText(scene, 40, 60, `Gold: ${player.gold}`, { size: 22, bold: true, color: COLORS.textGold }));
    const info = addText(scene, 40, 560, tab === 'buy' ? 'Shop gear is plain (0% quality). Drops and the smith roll better.' : 'Worn gear can\'t be sold.', {
      size: 16,
      color: COLORS.textDim,
      wrap: 780,
    });
    c.add(info);
    (['buy', 'sell'] as const).forEach((t, i) => {
      const b = new Button(scene, 560 + i * 130, 72, t === 'buy' ? 'Buy' : 'Sell', () => {
        tab = t;
        page = 0;
        render();
      }, { width: 120, height: 42, fontSize: 18 });
      if (tab === t) b.setEnabled(false);
      c.add(b);
    });

    const rowY = (i: number) => 140 + i * 56;
    if (tab === 'buy') {
      const pages = Math.max(1, Math.ceil(stock.length / SHOP_ROWS));
      page = Math.min(page, pages - 1);
      stock.slice(page * SHOP_ROWS, (page + 1) * SHOP_ROWS).forEach((id, i) => {
        const item = db.item(id);
        const y = rowY(i);
        const needs = isGear(item) && player.level < item.level ? `  (Lv ${item.level})` : '';
        c.add(addText(scene, 40, y - 20, item.name + needs, { size: 19, bold: true, color: needs ? COLORS.textBad : COLORS.text }));
        c.add(addText(scene, 40, y + 4, itemSummary(item), { size: 14, color: COLORS.textDim }));
        const btn = new Button(scene, 760, y, 'Buy', () => {
          if (player.buy(id)) {
            play(scene, 'coin');
            toast(scene, `Bought ${item.name}`, COLORS.textGood);
            onChange();
          } else {
            play(scene, 'denied');
            toast(scene, 'Not enough gold', COLORS.textBad);
          }
          render();
        }, { width: 120, height: 48, sublabel: `${item.price} g`, fontSize: 18 });
        btn.setEnabled(player.gold >= item.price);
        btn.onHover(() => info.setText(itemDetails(item)));
        c.add(btn);
      });
      addPager(scene, c, 430, 530, page, pages, (p) => ((page = p), render()));
      return;
    }

    const entries = bagEntries(player);
    const pages = Math.max(1, Math.ceil(entries.length / SHOP_ROWS));
    page = Math.min(page, pages - 1);
    if (entries.length === 0) c.add(addText(scene, 40, 120, 'Nothing to sell.', { size: 18, color: COLORS.textDim }));
    entries.slice(page * SHOP_ROWS, (page + 1) * SHOP_ROWS).forEach((entry, i) => {
      const y = rowY(i);
      if (entry.kind === 'gear') {
        const inst = entry.inst;
        const price = sellPrice(inst);
        c.add(addText(scene, 40, y - 20, gearName(inst), { size: 19, bold: true, color: rarityColor(inst) }));
        c.add(addText(scene, 40, y + 4, `${RARITY[inst.rarity].name} · ${qualityText(inst)}`, { size: 14, color: COLORS.textDim }));
        const precious = inst.rarity !== 'common' || inst.plus > 0;
        const asking = confirmSell === inst.uid;
        const btn = new Button(scene, 760, y, asking ? 'Sure?' : 'Sell', () => {
          if (precious && !asking) {
            confirmSell = inst.uid;
            render();
            return;
          }
          confirmSell = null;
          const gold = player.sellGear(inst.uid);
          play(scene, 'coin');
          toast(scene, `Sold ${gearName(inst)} for ${gold} g`, COLORS.textGold);
          onChange();
          render();
        }, { width: 120, height: 48, sublabel: `${price} g`, fontSize: 18 });
        btn.onHover(() => info.setText(gearDetails(inst)));
        c.add(btn);
        return;
      }
      const item = db.item(entry.itemId);
      const price = itemSellPrice(item.id);
      c.add(addText(scene, 40, y - 20, `${item.name} x${entry.qty}`, { size: 19, bold: true }));
      c.add(addText(scene, 40, y + 4, item.description, { size: 14, color: COLORS.textDim }));
      const btn = new Button(scene, 760, y, 'Sell 1', () => {
        const gold = player.sellItem(item.id);
        play(scene, 'coin');
        toast(scene, `Sold ${item.name} for ${gold} g`, COLORS.textGold);
        onChange();
        render();
      }, { width: 120, height: 48, sublabel: `${price} g`, fontSize: 18 });
      c.add(btn);
    });
    addPager(scene, c, 430, 530, page, pages, (p) => ((page = p), render()));
  };

  render();
  return modal;
}

export interface DialogueAction {
  label: string;
  sublabel?: string;
  enabled?: boolean;
  /** `close` shuts the dialogue. */
  run: (close: () => void) => void;
}

/** NPC dialogue box. Pages through `lines` (default: the NPC's own), then shows the actions. */
export function openDialogue(scene: Phaser.Scene, npc: NpcDef, actions: DialogueAction[], lines: string[] = npc.dialogue): Modal {
  const modal = new Modal(scene, 860, 320, `${npc.name}, ${npc.title}`);
  let line = 0;

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    c.add(addText(scene, 50, 80, lines[line], { size: 23, wrap: 760 }).setLineSpacing(6));

    const isLast = line >= lines.length - 1;
    const buttons: DialogueAction[] = isLast
      ? [...actions, { label: 'Goodbye', run: () => modal.close() }]
      : [{ label: 'Next', run: () => { line += 1; render(); } }];

    const width = 220;
    const gap = 16;
    const total = buttons.length * width + (buttons.length - 1) * gap;
    buttons.forEach((b, i) => {
      const x = (860 - total) / 2 + width / 2 + i * (width + gap);
      const btn = new Button(scene, x, 250, b.label, () => b.run(() => modal.close()), { width, height: 60, sublabel: b.sublabel, fontSize: 20 });
      if (b.enabled === false) btn.setEnabled(false);
      c.add(btn);
    });
  };

  render();
  return modal;
}
