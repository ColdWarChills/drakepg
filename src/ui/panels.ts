import Phaser from 'phaser';
import { COLORS } from '../config';
import { db } from '../data/db';
import { DAMAGE_TYPE_LABELS, ELEMENT_LABELS } from '../data/elements';
import type { EquipSlot, NpcDef } from '../data/types';
import { gearDef } from '../game/Items';
import { STAT_KEYS, STAT_LABELS, type Player } from '../game/Player';
import { Button } from './Button';
import { defenseText, gradeInfo, gradeLabel, resistText } from './itemUi';
import { Modal } from './Modal';
import { addText } from './text';

const STAT_HELP: Record<string, string> = {
  str: 'Melee damage',
  int: 'Magic damage, max MP',
  dex: 'Pierce damage, hit, crit',
  end: 'Max HP',
};

/** Character sheet: stats, stat points and skills. Gear lives in the inventory (`openInventory`). */
export function openCharacterPanel(scene: Phaser.Scene, player: Player, onChange: () => void, openInventory: () => void): Modal {
  const modal = new Modal(scene, 1100, 600, 'Character', () => {});

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    const s = player.stats;

    // --- Stats column ---
    const [minDmg, maxDmg] = player.attackRange;
    const info = addText(scene, 40, 545, '', { size: 18, color: COLORS.textDim, wrap: 1020 });
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
    const d = { melee: player.defenseFor('melee'), pierce: player.defenseFor('pierce'), magic: player.defenseFor('magic') };
    const lines = [
      `Attack  ${minDmg}-${maxDmg} ${DAMAGE_TYPE_LABELS[player.weaponDamageType]} · ${element ? ELEMENT_LABELS[element] : 'Neutral'}`,
      `Crit  ${(player.critChance * 100).toFixed(1)}%`,
      defenseText(d),
      resistText(player.resists, 'Resists  ') || 'Resists  none',
      `Gold  ${player.gold}`,
    ];
    c.add(addText(scene, 40, 380, lines.join('\n'), { size: 17 }).setLineSpacing(6));

    // --- Equipped (details and changes in the inventory) ---
    c.add(addText(scene, 380, 70, 'Equipped', { size: 22, bold: true, color: COLORS.textGold }));
    (['weapon', 'armor'] as EquipSlot[]).forEach((slot, i) => {
      const g = player.data.equipment[slot];
      const y = 108 + i * 30;
      c.add(addText(scene, 380, y, slot === 'weapon' ? 'Weapon' : 'Armor', { size: 17, color: COLORS.textDim }));
      if (!g) {
        c.add(addText(scene, 470, y, '(none)', { size: 17, color: COLORS.textDim }));
        return;
      }
      const def = gearDef(g);
      const gi = gradeInfo(def, g.rolls);
      c.add(addText(scene, 470, y, `${def.name}  (${gradeLabel(gi)})`, { size: 17, bold: true, color: gi.css }));
    });
    c.add(new Button(scene, 960, 122, 'Inventory (I)', () => modal.close(openInventory), { width: 190, height: 48, fontSize: 19 }));

    // --- Skills ---
    c.add(addText(scene, 380, 190, 'Skills', { size: 22, bold: true, color: COLORS.textGold }));
    const perColumn = Math.ceil(player.classDef.skills.length / 3);
    player.classDef.skills.forEach((entry, i) => {
      const skill = db.skill(entry.skillId);
      const known = entry.level <= player.level;
      const x = 380 + Math.floor(i / perColumn) * 230;
      const y = 230 + (i % perColumn) * 32;
      const t = addText(scene, x, y, known ? skill.name : `Lv ${entry.level}  ${skill.name}`, {
        size: 17,
        color: known ? COLORS.textMagic : COLORS.textDim,
      });
      const cd = skill.cooldown > 0 ? `, cooldown ${skill.cooldown}` : '';
      t.setInteractive()
        .on('pointerover', () => info.setText(`${skill.name} (${skill.mpCost} MP${cd}): ${skill.description}`))
        .on('pointerout', () => info.setText(''));
      c.add(t);
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
