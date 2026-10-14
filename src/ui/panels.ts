import Phaser from 'phaser';
import { COLORS } from '../config';
import { db } from '../data/db';
import { DAMAGE_TYPE_LABELS, ELEMENT_LABELS } from '../data/elements';
import type { NpcDef } from '../data/types';
import { EQUIP_SLOTS, gearDef } from '../game/Items';
import { activeSetBonuses, setCounts } from '../game/Sets';
import { STAT_KEYS, STAT_LABELS, type Player } from '../game/Player';
import { Button } from './Button';
import { defenseText, gradeInfo, lootName, resistText, setBonusText } from './itemUi';
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
    const rage = player.classDef.mpOnHit;
    if (rage) c.add(addText(scene, 40, 152, `Rage: +${rage} MP whenever an enemy hit lands on you`, { size: 15, color: COLORS.textMagic }));

    const points = player.data.statPoints;
    if (points > 0) {
      const label = addText(scene, 40, 176, `${points} stat point${points > 1 ? 's' : ''} to spend`, { size: 19, bold: true, color: COLORS.textGold });
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
      `Crit  ${(player.critChance * 100).toFixed(1)}%${player.damageBonus ? `  ·  Damage +${Math.round(player.damageBonus * 100)}%` : ''}`,
      defenseText(d),
      resistText(player.resists, 'Resists  ') || 'Resists  none',
      `Gold  ${player.gold}`,
      ...activeSetBonuses(player.data.equipment).map((a) => `${a.set.name} (${a.bonus.pieces}): ${setBonusText(a.bonus)}`),
    ];
    c.add(addText(scene, 40, 380, lines.join('\n'), { size: 17 }).setLineSpacing(6));

    // --- Equipped (details and changes in the inventory) ---
    c.add(addText(scene, 380, 70, 'Equipped', { size: 22, bold: true, color: COLORS.textGold }));
    // The weapon by name; the other 7 slots as a count, plus the sets being worn.
    const weapon = player.data.equipment.weapon;
    c.add(addText(scene, 380, 108, 'Weapon', { size: 17, color: COLORS.textDim }));
    if (weapon) {
      const def = gearDef(weapon);
      c.add(addText(scene, 470, 108, lootName(def, weapon), { size: 17, bold: true, color: gradeInfo(def, weapon).nameCss }));
    } else c.add(addText(scene, 470, 108, '(none)', { size: 17, color: COLORS.textDim }));
    const worn = EQUIP_SLOTS.filter((s) => s !== 'weapon' && player.data.equipment[s]).length;
    const sets = [...setCounts(player.data.equipment)].map(([id, n]) => `${db.set(id).name} ${n}/${db.set(id).pieces.length}`);
    c.add(addText(scene, 380, 138, [`Gear  ${worn}/${EQUIP_SLOTS.length - 1} slots`, ...sets].join('  ·  '), { size: 17, color: COLORS.textDim }));
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
