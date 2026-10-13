import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { RARITY } from '../data/rarity';
import {
  canAfford,
  craft,
  gearDef,
  gearName,
  hone,
  honeCost,
  QUALITY_LINES,
  qualityText,
  rarityColor,
  recipeCost,
  reinforce,
  reinforceChance,
  reinforceCost,
  reinforceRisky,
  salvage,
  salvageYield,
  type Cost,
  type GearInstance,
} from '../game/Gear';
import type { Player } from '../game/Player';
import { Button } from './Button';
import { Modal } from './Modal';
import { addPager, gearDetails, gearSummary, itemDetails, itemSummary } from './panels';
import { play } from './sfx';
import { addText } from './text';

type Tab = 'craft' | 'reinforce' | 'hone' | 'salvage';

const TABS: { id: Tab; label: string }[] = [
  { id: 'craft', label: 'Craft' },
  { id: 'reinforce', label: 'Reinforce' },
  { id: 'hone', label: 'Hone' },
  { id: 'salvage', label: 'Salvage' },
];

const HELP: Record<Tab, string> = {
  craft: `Crafted gear rolls at least ${BALANCE.gear.craftFloor}% quality and has better rarity odds than drops.`,
  reinforce: `Each + adds ${Math.round(BALANCE.gear.plusPower * 100)}% damage or defense. From +${BALANCE.gear.reinforceRiskFrom}, a failure drops the item one +.`,
  hone: `Raises one quality line. ${Math.round(BALANCE.gear.honeDriftChance * 100)}% chance another line drifts down, never below the rarity's floor.`,
  salvage: 'Breaks gear into scrap, dust and essences. Worn gear can\'t be salvaged.',
};

const LIST_ROWS = 7;

/** "Iron Scrap 2/3 · 25 g", red when short. */
function costText(player: Player, cost: Cost): string {
  const parts = cost.items.map((c) => `${db.item(c.itemId).name} ${player.countItem(c.itemId)}/${c.qty}`);
  if (cost.gold) parts.push(`${cost.gold} g`);
  return parts.join(' · ');
}

/** The smith: craft from recipes, reinforce (+), hone quality, salvage into materials. */
export function openForge(scene: Phaser.Scene, player: Player, title: string, recipes: string[], onChange: () => void): Modal {
  const modal = new Modal(scene, 1120, 640, title, () => {});
  let tab: Tab = 'craft';
  let page = 0;
  let selected: number | null = null;
  let confirmSalvage = false;
  /** Result of the last action, shown in the bottom line (toasts would cover the tabs). */
  let message: { text: string; color: string } | null = null;
  const say = (text: string, color: string) => (message = { text, color });

  const render = () => {
    modal.content.removeAll(true);
    const c = modal.content;
    const info = addText(scene, 40, 590, message?.text ?? HELP[tab], { size: 17, color: message?.color ?? COLORS.textDim, wrap: 1040, bold: !!message });
    c.add(info);
    c.add(addText(scene, 40, 62, `Gold: ${player.gold}`, { size: 20, bold: true, color: COLORS.textGold }));
    TABS.forEach((t, i) => {
      const b = new Button(scene, 420 + i * 150, 72, t.label, () => {
        tab = t.id;
        page = 0;
        confirmSalvage = false;
        message = null;
        render();
      }, { width: 140, height: 42, fontSize: 18 });
      if (tab === t.id) b.setEnabled(false);
      c.add(b);
    });

    if (tab === 'craft') {
      const pages = Math.max(1, Math.ceil(recipes.length / LIST_ROWS));
      page = Math.min(page, pages - 1);
      recipes.slice(page * LIST_ROWS, (page + 1) * LIST_ROWS).forEach((id, i) => {
        const recipe = db.recipe(id);
        const item = db.item(recipe.result);
        const cost = recipeCost(recipe);
        const ok = canAfford(player, cost);
        const y = 145 + i * 62;
        c.add(addText(scene, 40, y - 24, item.name, { size: 19, bold: true }));
        c.add(addText(scene, 40, y, itemSummary(item), { size: 14, color: COLORS.textDim }));
        c.add(addText(scene, 40, y + 18, costText(player, cost), { size: 14, color: ok ? COLORS.textGood : COLORS.textBad }));
        const btn = new Button(scene, 1000, y, 'Craft', () => {
          const made = craft(player, recipe);
          if (made) {
            play(scene, 'quest');
            say(`Made: ${gearDetails(made)}`, rarityColor(made));
          }
          onChange();
          render();
        }, { width: 140, height: 48, fontSize: 18 }).setEnabled(ok);
        btn.onHover(() => info.setText(itemDetails(item)).setColor(COLORS.textDim), () => info.setText(message?.text ?? HELP.craft).setColor(message?.color ?? COLORS.textDim));
        c.add(btn);
      });
      addPager(scene, c, 560, 560, page, pages, (p) => ((page = p), render()));
      return;
    }

    // Gear list on the left, the selected piece on the right.
    const list = tab === 'salvage' ? player.bagGear : player.data.gear;
    if (selected !== null && !list.some((g) => g.uid === selected)) selected = null;
    if (selected === null && list.length > 0) selected = list[0].uid;
    const pages = Math.max(1, Math.ceil(list.length / LIST_ROWS));
    page = Math.min(page, pages - 1);
    if (list.length === 0) c.add(addText(scene, 40, 130, 'No gear to work on.', { size: 18, color: COLORS.textDim }));
    list.slice(page * LIST_ROWS, (page + 1) * LIST_ROWS).forEach((inst, i) => {
      const y = 140 + i * 58;
      const isSel = inst.uid === selected;
      const row = scene.add
        .rectangle(30, y - 26, 500, 54, isSel ? 0x3a2e4a : 0x241e2e)
        .setOrigin(0)
        .setRounded(8)
        .setStrokeStyle(isSel ? 2 : 0, COLORS.panelBorder)
        .setInteractive({ useHandCursor: true });
      row.on('pointerdown', () => {
        selected = inst.uid;
        confirmSalvage = false;
        message = null;
        render();
      });
      c.add(row);
      const worn = player.isEquipped(inst.uid) ? '  (worn)' : '';
      c.add(addText(scene, 44, y - 22, gearName(inst) + worn, { size: 18, bold: true, color: rarityColor(inst) }));
      c.add(addText(scene, 44, y + 2, qualityText(inst), { size: 13, color: COLORS.textDim }));
    });
    addPager(scene, c, 280, 560, page, pages, (p) => ((page = p), render()));

    const inst = selected === null ? undefined : player.gearByUid(selected);
    if (!inst) return;
    const x = 570;
    c.add(addText(scene, x, 120, gearName(inst), { size: 24, bold: true, color: rarityColor(inst) }));
    c.add(addText(scene, x, 152, `${RARITY[inst.rarity].name} · Lv ${gearDef(inst.baseId).level}`, { size: 16, color: COLORS.textDim }));
    c.add(addText(scene, x, 176, gearSummary(inst), { size: 15, color: COLORS.text, wrap: 520 }));

    if (tab === 'reinforce') renderReinforce(inst, x);
    else if (tab === 'hone') renderHone(inst, x);
    else renderSalvage(inst, x);
  };

  const renderReinforce = (inst: GearInstance, x: number) => {
    const c = modal.content;
    const g = BALANCE.gear;
    if (inst.plus >= g.maxPlus) {
      c.add(addText(scene, x, 240, `Fully reinforced (+${g.maxPlus}).`, { size: 20, color: COLORS.textGood }));
      return;
    }
    const cost = reinforceCost(inst);
    const ok = canAfford(player, cost);
    const next = { ...inst, plus: inst.plus + 1 };
    const lines = [
      `Next: +${inst.plus + 1} (+${Math.round(BALANCE.gear.plusPower * 100 * (inst.plus + 1))}% total)   ${gearSummary(next)}`,
      `Success chance: ${Math.round(reinforceChance(inst) * 100)}%`,
      reinforceRisky(inst) ? `On failure: drops to +${inst.plus - 1}` : 'On failure: only the materials are lost',
      `Cost: ${costText(player, cost)}`,
    ];
    c.add(addText(scene, x, 240, lines.join('\n'), { size: 17, wrap: 520 }).setLineSpacing(10));
    c.add(
      new Button(scene, x + 120, 430, 'Reinforce', () => {
        const before = gearName(inst);
        const result = reinforce(player, inst);
        if (result === 'success') {
          play(scene, 'levelup');
          say(`Success! ${before} is now +${inst.plus}.`, COLORS.textGood);
        } else if (result === 'dropped') {
          play(scene, 'explode');
          say(`It cracked. Down to +${inst.plus}.`, COLORS.textBad);
        } else if (result === 'fail') {
          play(scene, 'denied');
          say('It failed. The materials are gone.', COLORS.textBad);
        }
        onChange();
        render();
      }, { width: 220, height: 56, fontSize: 20 }).setEnabled(ok),
    );
  };

  const renderHone = (inst: GearInstance, x: number) => {
    const c = modal.content;
    const cost = honeCost(inst);
    const ok = canAfford(player, cost);
    const floor = RARITY[inst.rarity].qualityFloor;
    const names = QUALITY_LINES[gearDef(inst.baseId).type];
    c.add(addText(scene, x, 236, `Cost per hone: ${costText(player, cost)}${floor ? `   ·   Floor ${floor}%` : ''}`, { size: 16, color: ok ? COLORS.text : COLORS.textBad }));
    names.forEach((name, i) => {
      const y = 300 + i * 70;
      const q = inst.q[i];
      c.add(addText(scene, x, y - 12, `${name}`, { size: 20, bold: true }));
      // Quality bar.
      c.add(scene.add.rectangle(x + 120, y - 2, 220, 18, COLORS.barBg).setOrigin(0, 0.5).setRounded(4));
      if (q > 0) c.add(scene.add.rectangle(x + 120, y - 2, 2.2 * q, 18, q >= 100 ? COLORS.gold : COLORS.mp).setOrigin(0, 0.5).setRounded(4));
      c.add(addText(scene, x + 350, y - 2, `${q}%`, { size: 18 }).setOrigin(0, 0.5));
      c.add(
        new Button(scene, x + 470, y - 2, q >= 100 ? 'Max' : 'Hone', () => {
          const r = hone(player, inst, i);
          if (r) {
            play(scene, 'heal');
            const drift = r.drift ? `  ${names[r.drift.line]} drifted -${r.drift.loss}%.` : '';
            say(`${name} +${r.gain}%.${drift}`, r.drift ? COLORS.textGold : COLORS.textGood);
          }
          onChange();
          render();
        }, { width: 100, height: 44, fontSize: 17 }).setEnabled(ok && q < 100),
      );
    });
  };

  const renderSalvage = (inst: GearInstance, x: number) => {
    const c = modal.content;
    const yields = salvageYield(inst).map((y) => `${db.item(y.itemId).name} x${y.qty}`);
    c.add(addText(scene, x, 240, `Salvaging gives:\n${yields.join('\n')}`, { size: 18 }).setLineSpacing(8));
    const precious = inst.rarity !== 'common' || inst.plus > 0;
    c.add(
      new Button(scene, x + 120, 430, confirmSalvage ? 'Really break it?' : 'Salvage', () => {
        if (precious && !confirmSalvage) {
          confirmSalvage = true;
          render();
          return;
        }
        const name = gearName(inst);
        if (salvage(player, inst)) {
          play(scene, 'hit');
          say(`Salvaged ${name}: ${yields.join(', ')}`, COLORS.textGood);
        }
        confirmSalvage = false;
        selected = null;
        onChange();
        render();
      }, { width: 240, height: 56, fontSize: 20, color: confirmSalvage ? 0x7a2e2e : undefined }),
    );
  };

  render();
  return modal;
}
