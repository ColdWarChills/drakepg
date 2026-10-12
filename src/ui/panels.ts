import Phaser from 'phaser';
import { COLORS } from '../config';
import { db } from '../data/db';
import type { ItemDef, NpcDef } from '../data/types';
import { STAT_KEYS, STAT_LABELS, type Player } from '../game/Player';
import { Button } from './Button';
import { Modal } from './Modal';
import { addText, toast } from './text';

/** One line summary of an item's numbers, for lists. */
export function itemSummary(item: ItemDef): string {
  const bonus = (stats?: Partial<Record<string, number>>) =>
    stats
      ? Object.entries(stats)
          .map(([k, v]) => ` +${v} ${k.toUpperCase()}`)
          .join('')
      : '';
  switch (item.type) {
    case 'weapon':
      return `${item.damage[0]}-${item.damage[1]} ${item.damageType === 'magic' ? 'magic' : 'phys'} dmg${bonus(item.stats)}`;
    case 'armor':
      return `DEF ${item.defense}  RES ${item.resist}${bonus(item.stats)}`;
    case 'consumable':
      return item.description;
  }
}

/** "+40 HP, +20 MP" */
export function restoreText(r: { hp: number; mp: number }): string {
  const parts = [r.hp ? `+${r.hp} HP` : '', r.mp ? `+${r.mp} MP` : ''].filter(Boolean);
  return parts.length ? parts.join(', ') : 'No effect';
}

const ROWS_PER_PAGE = 6;

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
    const lines = [
      `${player.name}`,
      `Level ${player.level}   (${player.xp} / ${player.xpNeeded} XP)`,
      '',
      `HP  ${player.hp} / ${player.maxHp}`,
      `MP  ${player.mp} / ${player.maxMp}`,
      '',
      ...STAT_KEYS.map((k) => `${STAT_LABELS[k]}  ${s[k]}`),
      '',
      `Attack  ${minDmg}-${maxDmg}`,
      `Defense  ${player.defense}   Resist  ${player.resist}`,
      `Gold  ${player.gold}`,
    ];
    c.add(addText(scene, 40, 70, lines.join('\n'), { size: 21, color: COLORS.text }).setLineSpacing(4));

    // --- Equipment column ---
    c.add(addText(scene, 380, 70, 'Equipped', { size: 22, bold: true, color: COLORS.textGold }));
    (['weapon', 'armor'] as const).forEach((slot, i) => {
      const y = 120 + i * 110;
      const id = player.data.equipment[slot];
      const item = id ? db.item(id) : null;
      c.add(addText(scene, 380, y, slot === 'weapon' ? 'Weapon' : 'Armor', { size: 16, color: COLORS.textDim }));
      c.add(addText(scene, 380, y + 22, item ? item.name : '(none)', { size: 21, bold: true }));
      c.add(addText(scene, 380, y + 50, item ? itemSummary(item) : '', { size: 16, color: COLORS.textDim }));
      if (item) {
        c.add(
          new Button(scene, 660, y + 34, 'Unequip', () => {
            player.unequip(slot);
            onChange();
            render();
          }, { width: 110, height: 40, fontSize: 17 }),
        );
      }
    });

    // --- Bag column ---
    c.add(addText(scene, 740, 70, 'Bag', { size: 22, bold: true, color: COLORS.textGold }));
    const entries = player.data.inventory;
    const pages = Math.max(1, Math.ceil(entries.length / ROWS_PER_PAGE));
    page = Math.min(page, pages - 1);
    const shown = entries.slice(page * ROWS_PER_PAGE, (page + 1) * ROWS_PER_PAGE);
    const info = addText(scene, 40, 545, '', { size: 18, color: COLORS.textDim, wrap: 1020 });
    c.add(info);

    if (entries.length === 0) c.add(addText(scene, 740, 115, 'Empty', { size: 18, color: COLORS.textDim }));

    shown.forEach((entry, i) => {
      const item = db.item(entry.itemId);
      const y = 130 + i * 64;
      c.add(addText(scene, 740, y - 20, `${item.name}${entry.qty > 1 ? ` x${entry.qty}` : ''}`, { size: 19 }));
      c.add(addText(scene, 740, y + 4, itemSummary(item), { size: 14, color: COLORS.textDim }));
      const isConsumable = item.type === 'consumable';
      const btn = new Button(
        scene,
        1020,
        y,
        isConsumable ? 'Use' : 'Equip',
        () => {
          if (isConsumable) {
            const r = player.useConsumable(item.id);
            if (r) toast(scene, restoreText(r), COLORS.textGood);
            else toast(scene, 'Already full', COLORS.textDim);
          } else {
            player.equip(item.id);
          }
          onChange();
          render();
        },
        { width: 100, height: 40, fontSize: 17 },
      );
      btn.onHover(() => info.setText(`${item.name}: ${item.description}`), () => info.setText(''));
      c.add(btn);
    });

    if (pages > 1) {
      c.add(addText(scene, 900, 520, `${page + 1} / ${pages}`, { size: 16, color: COLORS.textDim }).setOrigin(0.5));
      c.add(new Button(scene, 820, 520, '<', () => { page = Math.max(0, page - 1); render(); }, { width: 50, height: 36 }).setEnabled(page > 0));
      c.add(new Button(scene, 980, 520, '>', () => { page = Math.min(pages - 1, page + 1); render(); }, { width: 50, height: 36 }).setEnabled(page < pages - 1));
    }
  };

  render();
  return modal;
}

export function openShop(scene: Phaser.Scene, player: Player, title: string, stock: string[], onChange: () => void): Modal {
  const modal = new Modal(scene, 820, 600, title, () => {});

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    c.add(addText(scene, 40, 22, `Gold: ${player.gold}`, { size: 22, bold: true, color: COLORS.textGold }));
    const info = addText(scene, 40, 545, 'Hover an item for details.', { size: 18, color: COLORS.textDim, wrap: 740 });
    c.add(info);

    stock.forEach((id, i) => {
      const item = db.item(id);
      const y = 100 + i * 70;
      const owned = player.countItem(id);
      c.add(addText(scene, 40, y - 22, item.name, { size: 21, bold: true }));
      c.add(addText(scene, 40, y + 6, itemSummary(item), { size: 15, color: COLORS.textDim }));
      c.add(addText(scene, 520, y, owned > 0 ? `Owned ${owned}` : '', { size: 16, color: COLORS.textDim }).setOrigin(0, 0.5));
      const btn = new Button(scene, 710, y, 'Buy', () => {
        if (player.buy(id)) {
          toast(scene, `Bought ${item.name}`, COLORS.textGood);
          onChange();
        } else {
          toast(scene, 'Not enough gold', COLORS.textBad);
        }
        render();
      }, { width: 140, height: 52, sublabel: `${item.price} g` });
      btn.setEnabled(player.gold >= item.price);
      btn.onHover(() => info.setText(`${item.name}: ${item.description}`));
      c.add(btn);
    });
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

/** NPC dialogue box. Pages through `npc.dialogue`, then shows the actions. */
export function openDialogue(scene: Phaser.Scene, npc: NpcDef, actions: DialogueAction[]): Modal {
  const modal = new Modal(scene, 860, 320, `${npc.name}, ${npc.title}`);
  let line = 0;

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    c.add(addText(scene, 50, 80, npc.dialogue[line], { size: 23, wrap: 760 }).setLineSpacing(6));

    const isLast = line >= npc.dialogue.length - 1;
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
