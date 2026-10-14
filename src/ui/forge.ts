import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import {
  canAfford,
  canSalvage,
  costText,
  craft,
  hone,
  honeCost,
  plusOf,
  recipeCost,
  reinforce,
  reinforceChance,
  reinforceCost,
  reinforceRisky,
  salvage,
  salvageYield,
} from '../game/Forge';
import { EQUIP_SLOTS, gearDef, gradeTier, rollKeys, ROLL_LABELS, type Gear } from '../game/Items';
import type { Player } from '../game/Player';
import { Button } from './Button';
import { GRADE_COLORS, gradeInfo, gradeLabel, itemLines, itemName, lootName } from './itemUi';
import { Modal } from './Modal';
import { play } from './sfx';
import { addText } from './text';

type Tab = 'craft' | 'reinforce' | 'hone' | 'salvage';

const TABS: { id: Tab; label: string }[] = [
  { id: 'craft', label: 'Craft' },
  { id: 'reinforce', label: 'Reinforce' },
  { id: 'hone', label: 'Hone' },
  { id: 'salvage', label: 'Salvage' },
];

const F = BALANCE.forge;
const HELP: Record<Tab, string> = {
  craft: `Crafted gear rolls every line at ${F.craftFloor}% or better.`,
  reinforce: `Each + adds ${Math.round(F.plusPower * 100)}% base damage or defense, up to +${F.maxPlus}. From +${F.reinforceRiskFrom}, a failure drops the item one +.`,
  hone: `Raises one quality line by ${F.honeGain[0]}-${F.honeGain[1]}%. ${Math.round(F.honeDriftChance * 100)}% chance another line slips ${F.honeDrift[0]}-${F.honeDrift[1]}%.`,
  salvage: "Breaks bag gear into scrap, dust and essences. Worn and locked gear can't be salvaged.",
};

const LIST_ROWS = 7;

function pager(scene: Phaser.Scene, c: Phaser.GameObjects.Container, x: number, y: number, page: number, pages: number, go: (p: number) => void): void {
  if (pages <= 1) return;
  c.add(addText(scene, x, y, `${page + 1} / ${pages}`, { size: 16, color: COLORS.textDim }).setOrigin(0.5));
  c.add(new Button(scene, x - 80, y, '<', () => go(Math.max(0, page - 1)), { width: 50, height: 36 }).setEnabled(page > 0));
  c.add(new Button(scene, x + 80, y, '>', () => go(Math.min(pages - 1, page + 1)), { width: 50, height: 36 }).setEnabled(page < pages - 1));
}

/** The smith: craft from recipes, reinforce (+), hone a quality line, salvage into materials. */
export function openForge(scene: Phaser.Scene, player: Player, title: string, recipes: string[], onChange: () => void): Modal {
  const modal = new Modal(scene, 1120, 640, title, () => {});
  let tab: Tab = 'craft';
  let page = 0;
  let selected: string | null = null;
  let confirmSalvage = false;
  /** Result of the last action, shown on the bottom line (toasts would cover the tabs). */
  let message: { text: string; color: string } | null = null;
  const say = (text: string, color: string) => (message = { text, color });
  const changed = () => {
    onChange();
    render();
  };

  const render = () => {
    const c = modal.content;
    c.removeAll(true);
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
      }, { width: 140, height: 42, fontSize: 18, color: tab === t.id ? 0x6b5320 : COLORS.button });
      c.add(b);
    });

    if (tab === 'craft') {
      const pages = Math.max(1, Math.ceil(recipes.length / LIST_ROWS));
      page = Math.min(page, pages - 1);
      recipes.slice(page * LIST_ROWS, (page + 1) * LIST_ROWS).forEach((id, i) => {
        const recipe = db.recipe(id);
        const def = db.item(recipe.result);
        const cost = recipeCost(recipe);
        const ok = canAfford(player, cost);
        const y = 150 + i * 60;
        c.add(addText(scene, 40, y - 26, def.name, { size: 19, bold: true }));
        c.add(addText(scene, 40, y - 2, itemLines(def).join('  ·  '), { size: 14, color: COLORS.textDim }));
        c.add(addText(scene, 40, y + 16, costText(player, cost), { size: 14, color: ok ? COLORS.textGood : COLORS.textBad }));
        c.add(
          new Button(scene, 1000, y, 'Craft', () => {
            const made = craft(player, recipe);
            if (made) {
              play(scene, 'quest');
              say(`Made ${lootName(def, made)}.`, gradeInfo(def, made).nameCss);
            } else say('Your bag is full.', COLORS.textBad);
            changed();
          }, { width: 140, height: 48, fontSize: 18 }).setEnabled(ok),
        );
      });
      pager(scene, c, 560, 555, page, pages, (p) => ((page = p), render()));
      return;
    }

    // Gear list on the left, the selected piece on the right. Worn gear first.
    const worn = EQUIP_SLOTS.map((s) => player.data.equipment[s]).filter((g): g is Gear => !!g);
    const list = tab === 'salvage' ? player.data.gear.filter((g) => canSalvage(player, g)) : [...worn, ...player.data.gear];
    if (selected && !list.some((g) => g.uid === selected)) selected = null;
    if (!selected && list.length) selected = list[0].uid;
    const pages = Math.max(1, Math.ceil(list.length / LIST_ROWS));
    page = Math.min(page, pages - 1);
    if (list.length === 0) c.add(addText(scene, 40, 130, 'No gear to work on.', { size: 18, color: COLORS.textDim }));
    list.slice(page * LIST_ROWS, (page + 1) * LIST_ROWS).forEach((g, i) => {
      const def = gearDef(g);
      const gi = gradeInfo(def, g);
      const y = 140 + i * 58;
      const isSel = g.uid === selected;
      const row = scene.add
        .rectangle(30, y - 26, 500, 54, isSel ? 0x3a2e4a : 0x241e2e)
        .setOrigin(0)
        .setRounded(8)
        .setStrokeStyle(2, isSel ? COLORS.panelBorder : gi.frame, isSel ? 1 : 0.5)
        .setInteractive({ useHandCursor: true });
      row.on('pointerdown', () => {
        selected = g.uid;
        confirmSalvage = false;
        message = null;
        render();
      });
      c.add(row);
      const wornTag = worn.includes(g) ? '  (worn)' : g.locked ? '  (locked)' : '';
      c.add(addText(scene, 44, y - 22, itemName(def, g) + wornTag, { size: 18, bold: true, color: gi.nameCss }));
      c.add(addText(scene, 44, y + 2, gradeLabel(gi), { size: 13, color: COLORS.textDim }));
    });
    pager(scene, c, 280, 555, page, pages, (p) => ((page = p), render()));

    const gear = list.find((g) => g.uid === selected);
    if (!gear) return;
    const def = gearDef(gear);
    const gi = gradeInfo(def, gear);
    const x = 570;
    c.add(addText(scene, x, 120, itemName(def, gear), { size: 24, bold: true, color: gi.nameCss }));
    c.add(addText(scene, x, 152, gradeLabel(gi), { size: 16, color: COLORS.textDim }));
    c.add(addText(scene, x, 176, itemLines(def, gear).join('\n'), { size: 15, wrap: 520 }).setLineSpacing(4));

    if (tab === 'reinforce') renderReinforce(gear, x);
    else if (tab === 'hone') renderHone(gear, x);
    else renderSalvage(gear, x);
  };

  const renderReinforce = (gear: Gear, x: number) => {
    const c = modal.content;
    const plus = plusOf(gear);
    if (plus >= F.maxPlus) {
      c.add(addText(scene, x, 290, `Fully reinforced (+${F.maxPlus}).`, { size: 20, color: COLORS.textGood }));
      return;
    }
    const cost = reinforceCost(gear);
    const ok = canAfford(player, cost);
    const lines = [
      `Next: +${plus + 1}  (+${Math.round(F.plusPower * 100 * (plus + 1))}% base in total)`,
      `Success chance: ${Math.round(reinforceChance(gear) * 100)}%`,
      reinforceRisky(gear) ? `On failure: drops to +${plus - 1}` : 'On failure: only the materials are lost',
      `Cost: ${costText(player, cost)}`,
    ];
    c.add(addText(scene, x, 290, lines.join('\n'), { size: 17, wrap: 520, color: ok ? COLORS.text : COLORS.textBad }).setLineSpacing(10));
    c.add(
      new Button(scene, x + 120, 480, 'Reinforce', () => {
        const before = itemName(gearDef(gear), gear);
        const result = reinforce(player, gear);
        if (result === 'success') {
          play(scene, 'levelup');
          say(`Success! ${before} is now +${plusOf(gear)}.`, COLORS.textGood);
        } else if (result === 'dropped') {
          play(scene, 'denied');
          say(`It cracked. Down to +${plusOf(gear)}.`, COLORS.textBad);
        } else if (result === 'fail') {
          play(scene, 'denied');
          say('It failed. The materials are gone.', COLORS.textBad);
        }
        changed();
      }, { width: 220, height: 56, fontSize: 20 }).setEnabled(ok),
    );
  };

  const renderHone = (gear: Gear, x: number) => {
    const c = modal.content;
    const cost = honeCost(gear);
    const ok = canAfford(player, cost);
    c.add(addText(scene, x, 290, `Cost per hone: ${costText(player, cost)}`, { size: 16, color: ok ? COLORS.text : COLORS.textBad }));
    rollKeys(gearDef(gear)).forEach((key, i) => {
      const y = 350 + i * 64;
      const q = gear.rolls[key] ?? 0;
      c.add(addText(scene, x, y, ROLL_LABELS[key], { size: 18, bold: true }).setOrigin(0, 0.5));
      c.add(scene.add.rectangle(x + 150, y, 200, 16, 0x111111).setOrigin(0, 0.5).setRounded(4));
      if (q > 0) c.add(scene.add.rectangle(x + 150, y, 2 * q, 16, GRADE_COLORS[gradeTier(q).tier].color).setOrigin(0, 0.5).setRounded(4));
      c.add(addText(scene, x + 362, y, `${q}%`, { size: 17 }).setOrigin(0, 0.5));
      c.add(
        new Button(scene, x + 470, y, q >= 100 ? 'Max' : 'Hone', () => {
          const r = hone(player, gear, key);
          if (r) {
            play(scene, 'heal');
            const drift = r.drift ? `  ${ROLL_LABELS[r.drift.key]} slipped -${r.drift.loss}%.` : '';
            say(`${ROLL_LABELS[key]} +${r.gain}%.${drift}`, r.drift ? COLORS.textGold : COLORS.textGood);
          }
          changed();
        }, { width: 100, height: 44, fontSize: 17 }).setEnabled(ok && q < 100),
      );
    });
  };

  const renderSalvage = (gear: Gear, x: number) => {
    const c = modal.content;
    const yields = salvageYield(gear).map((y) => `${db.item(y.itemId).name} x${y.qty}`);
    c.add(addText(scene, x, 290, `Salvaging gives:\n${yields.join('\n')}`, { size: 18 }).setLineSpacing(8));
    const gi = gradeInfo(gearDef(gear), gear);
    const precious = !!gear.sigil || plusOf(gear) > 0 || gi.grade >= 40;
    c.add(
      new Button(scene, x + 120, 480, confirmSalvage ? 'Really break it?' : 'Salvage', () => {
        if (precious && !confirmSalvage) {
          confirmSalvage = true;
          render();
          return;
        }
        const name = itemName(gearDef(gear), gear);
        if (salvage(player, gear)) {
          play(scene, 'hit');
          say(`Salvaged ${name}: ${yields.join(', ')}.`, COLORS.textGood);
        }
        confirmSalvage = false;
        selected = null;
        changed();
      }, { width: 240, height: 56, fontSize: 20, color: confirmSalvage ? 0x7a2e2e : undefined }),
    );
  };

  render();
  return modal;
}
