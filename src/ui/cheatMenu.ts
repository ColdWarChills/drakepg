import Phaser from 'phaser';
import { session } from '../game/session';
import { BALANCE, COLORS } from '../config';
import { db } from '../data/db';
import { ITEMS } from '../data/items';
import { ROOMS } from '../data/world';
import type { ItemDef } from '../data/types';
import {
  addLevels,
  cheatGrant,
  cheats,
  completeQuestChain,
  finishActiveQuests,
  resetStats,
  type CheatItemOptions,
} from '../game/cheats';
import { isGearDef, SIGIL_NAMES, SIGIL_TIERS, type SigilTier } from '../game/Items';
import type { Player } from '../game/Player';
import { questStatus } from '../game/Quests';
import { hasArt } from './art';
import { Button } from './Button';
import { drawFigure } from './figures';
import { lootName } from './itemUi';
import { Modal } from './Modal';
import { addText, toast } from './text';

type Tab = 'hero' | 'items' | 'wardrobe' | 'world';
type Category = 'weapon' | 'armor' | 'consumable';

export interface CheatHooks {
  /** HUD, quest tracker, hero figure and save. */
  onChange: () => void;
  /** Go to a room (its middle when no spot is given). */
  travel: (roomId: string, x?: number, y?: number) => void;
  /** Throw the character away and start over (same name and class). */
  resetCharacter: () => void;
}

const W = 1200;
const H = 660;
const ON = 0x5a6b2e;
const PICKED = 0x6b5a2e;
const DANGER = 0x6e1f1f;

/** Remembered while the game runs. */
const state = {
  tab: 'hero' as Tab,
  category: 'weapon' as Category,
  page: 0,
  opts: { quality: 'random', mods: 0 } as CheatItemOptions,
};

/**
 * Testing menu: levels, gold, any item made to order, a wardrobe to try every weapon and armor on,
 * teleports, quest skips and battle toggles. Changes are saved like normal play.
 */
export function openCheatMenu(scene: Phaser.Scene, player: Player, hooks: CheatHooks): Modal {
  const modal = new Modal(scene, W, H, 'Cheats', () => {});
  const c = modal.content;

  const changed = (message?: string) => {
    hooks.onChange();
    if (message) toast(scene, message, COLORS.textGood);
    render();
  };

  const button = (x: number, y: number, label: string, run: () => void, w = 170, color?: number, enabled = true) => {
    const b = new Button(scene, x + w / 2, y, label, run, { width: w, height: 40, fontSize: label.length > 18 ? 15 : 17, color });
    if (!enabled) b.setEnabled(false);
    c.add(b);
    return b;
  };
  const label = (x: number, y: number, text: string, color: string = COLORS.textDim, size = 18) =>
    c.add(addText(scene, x, y, text, { size, bold: true, color }).setOrigin(0, 0.5));

  /** One row of mutually exclusive choices. */
  const choices = <T,>(x: number, y: number, title: string, options: { label: string; value: T }[], current: T, pick: (v: T) => void, w = 104) => {
    label(x, y, title);
    options.forEach((o, i) => button(x + 90 + i * (w + 8), y, o.label, () => (pick(o.value), render()), w, o.value === current ? PICKED : undefined));
  };

  const itemOptions = (y: number) => {
    const o = state.opts;
    choices(40, y, 'Quality', [
      { label: 'Random', value: 'random' as CheatItemOptions['quality'] },
      { label: '0%', value: 0 },
      { label: '50%', value: 50 },
      { label: '90%', value: 90 },
      { label: '100%', value: 100 },
    ], o.quality, (v) => (o.quality = v));
    choices(40, y + 50, 'Sigil', [
      { label: 'None', value: undefined as SigilTier | undefined },
      ...SIGIL_TIERS.map((t) => ({ label: SIGIL_NAMES[t].replace('Sigil of ', ''), value: t as SigilTier | undefined })),
    ], o.sigil, (v) => (o.sigil = v));
    choices(620, y + 50, 'Mods', [0, 1, 2, 3].map((n) => ({ label: `${n}`, value: n })), o.mods, (v) => (o.mods = v), 60);
  };

  const render = () => {
    c.removeAll(true);
    const tabs: { tab: Tab; label: string }[] = [
      { tab: 'hero', label: 'Hero' },
      { tab: 'items', label: 'Give item' },
      { tab: 'wardrobe', label: 'Wardrobe' },
      { tab: 'world', label: 'World' },
    ];
    tabs.forEach((t, i) => button(40 + i * 180, 78, t.label, () => ((state.tab = t.tab), (state.page = 0), render()), 170, t.tab === state.tab ? PICKED : undefined));
    if (state.tab === 'hero') renderHero();
    else if (state.tab === 'items') renderItems();
    else if (state.tab === 'wardrobe') renderWardrobe();
    else renderWorld();
  };

  const renderHero = () => {
    label(40, 140, `Level ${player.level}   ·   ${player.gold} gold   ·   HP ${player.hp}/${player.maxHp}   ·   ${player.data.statPoints} stat points`, COLORS.text, 20);
    const max = BALANCE.maxLevel;
    label(40, 200, 'Level');
    button(150, 200, '+1', () => (addLevels(player, 1), changed(`Level ${player.level}`)), 90, undefined, player.level < max);
    button(250, 200, '+5', () => (addLevels(player, 5), changed(`Level ${player.level}`)), 90, undefined, player.level < max);
    for (const [i, lv] of [5, 10, 15, 20].entries()) {
      button(350 + i * 100, 200, `To ${lv}`, () => (addLevels(player, lv - player.level), changed(`Level ${player.level}`)), 90, undefined, player.level < lv);
    }
    label(40, 260, 'Gold');
    for (const [i, g] of [100, 1000, 10000].entries()) {
      button(150 + i * 130, 260, `+${g.toLocaleString('en-US')}`, () => ((player.data.gold += g), changed(`+${g} gold`)), 120);
    }
    button(40, 330, 'Full heal', () => (player.restoreFull(), changed('Fully healed')));
    button(220, 330, 'Reset stat points', () => (resetStats(player), changed('Stat points refunded')), 200);
    button(430, 330, 'Clear bag (keeps locked)', () => {
      const before = player.data.gear.length;
      player.data.gear = player.data.gear.filter((g) => g.locked);
      changed(`Removed ${before - player.data.gear.length} pieces`);
    }, 240);

    label(40, 410, 'Toggles (until reload, not saved)');
    const toggle = (i: number, name: string, key: keyof typeof cheats, note: string) => {
      const x = 40 + i * 212;
      button(x, 460, `${name}: ${cheats[key] ? 'ON' : 'off'}`, () => ((cheats[key] = !cheats[key]), render()), 202, cheats[key] ? ON : undefined);
      c.add(addText(scene, x, 492, note, { size: 14, color: COLORS.textDim, wrap: 202 }));
    };
    toggle(0, 'God mode', 'god', 'You take no damage.');
    toggle(1, 'One-hit kills', 'oneHit', 'Any hit that lands finishes the enemy.');
    toggle(2, 'Elites everywhere', 'elites', 'Every roaming monster spawns as an Elite (re-enter a room or Respawn).');
    toggle(3, 'Lucky everywhere', 'lucky', 'A Lucky Impling in every monster room (re-enter a room or Respawn).');
    toggle(4, 'All unlocks', 'allFeatures', 'Forge, Sigils, chests and the rest without finishing chapters.');

    button(40, 600, 'Reset character...', () => confirmReset(), 220, DANGER);
  };

  const confirmReset = () => {
    const m = new Modal(scene, 640, 260, 'Start over?');
    m.content.add(addText(scene, 320, 100, `${player.name} goes back to level 1 with starting gear.\nThis replaces your save.`, { size: 20, align: 'center' }).setOrigin(0.5));
    m.content.add(new Button(scene, 200, 200, 'Reset', () => (m.close(), modal.close(), hooks.resetCharacter()), { width: 220, color: DANGER }));
    m.content.add(new Button(scene, 440, 200, 'Cancel', () => m.close(), { width: 220 }));
  };

  const listOf = (cat: Category) => ITEMS.filter((i) => i.type === cat);

  const renderItems = () => {
    itemOptions(140);
    choices(40, 240, 'Type', [
      { label: 'Weapons', value: 'weapon' as Category },
      { label: 'Armor', value: 'armor' as Category },
      { label: 'Potions', value: 'consumable' as Category },
    ], state.category, (v) => ((state.category = v), (state.page = 0)), 130);
    const items = listOf(state.category);
    const cols = 4;
    const rows = 6;
    const per = cols * rows;
    const pages = Math.max(1, Math.ceil(items.length / per));
    state.page = Math.min(state.page, pages - 1);
    items.slice(state.page * per, (state.page + 1) * per).forEach((item, i) => {
      const x = 40 + (i % cols) * 282;
      const y = 300 + Math.floor(i / cols) * 48;
      button(x, y, item.name, () => give(item), 272);
    });
    if (pages > 1) {
      button(40, 600, 'Prev', () => ((state.page = (state.page + pages - 1) % pages), render()), 120);
      label(180, 600, `${state.page + 1} / ${pages}`);
      button(260, 600, 'Next', () => ((state.page = (state.page + 1) % pages), render()), 120);
    }
    label(440, 600, 'Gear goes to your bag (even past 30 slots). Potions come in fives.', COLORS.textDim, 15);
  };

  const give = (item: ItemDef) => {
    if (!isGearDef(item)) {
      player.addItem(item.id, 5);
      changed(`+5 ${item.name}`);
      return;
    }
    const grant = cheatGrant(item.id, state.opts);
    player.receive(grant, true);
    changed(`Added ${lootName(item, grant)}`);
  };

  /** Equip a piece: one from the bag if you have it, otherwise a new one made with the item options. */
  const wear = (item: ItemDef) => {
    const owned = player.data.gear.find((g) => g.itemId === item.id);
    const gear = owned ?? player.makeGear(item.id, cheatGrant(item.id, state.opts));
    if (!owned) player.data.gear.push(gear);
    player.equip(gear.uid);
    changed(`Wearing ${item.name}`);
  };

  const renderWardrobe = () => {
    // Big hero preview on the left.
    c.add(scene.add.rectangle(30, 120, 300, 510, 0x15111c, 0.8).setOrigin(0).setStrokeStyle(2, COLORS.panelBorder));
    const hero = drawFigure(scene, 180, 560, player.look, 1, player.weapon, player.weaponSigil);
    hero.setScale(hero.scaleX * 1.25, hero.scaleY * 1.25);
    c.add(hero);
    const { weapon, armor } = player.data.equipment;
    const worn = [weapon ? lootName(db.item(weapon.itemId), weapon) : 'No weapon', armor ? lootName(db.item(armor.itemId), armor) : 'No armor'];
    c.add(addText(scene, 180, 600, worn.join('\n'), { size: 15, align: 'center', wrap: 280 }).setOrigin(0.5));

    const classId = player.data.classId;
    // A class's starting armor is its default outfit.
    const hasLook = (item: ItemDef) =>
      item.type === 'weapon' ? hasArt(scene, item.art) : item.id === player.classDef.armor || hasArt(scene, `char_${classId}_${item.id}`);
    const cols = 4;
    const grid = (items: ItemDef[], top: number) =>
      items.forEach((item, i) => {
        const x = 350 + (i % cols) * 206;
        const y = top + Math.floor(i / cols) * 44;
        const wearing = weapon?.itemId === item.id || armor?.itemId === item.id;
        const b = button(x, y, item.name, () => wear(item), 198, wearing ? PICKED : undefined);
        if (!hasLook(item)) b.setAlpha(0.6);
      });
    const weapons = listOf('weapon');
    label(350, 128, 'Weapons');
    grid(weapons, 160);
    const armorTop = 160 + Math.ceil(weapons.length / cols) * 44 + 10;
    label(350, armorTop - 4, 'Armor');
    grid(listOf('armor'), armorTop + 28);
    label(350, 640, 'Faded = no art yet (falls back to the default look). New pieces use the Give item options.', COLORS.textDim, 14);
  };

  const renderWorld = () => {
    label(40, 140, 'Teleport');
    ROOMS.forEach((r, i) => {
      const x = 40 + (i % 4) * 190;
      const y = 180 + Math.floor(i / 4) * 46;
      button(x, y, r.name, () => (modal.close(), hooks.travel(r.id)), 180);
    });
    const loc = player.data.location;
    button(800, 180, 'Respawn monsters', () => (modal.close(), session.forgetRooms(), hooks.travel(loc.roomId, loc.x, loc.y)), 220);

    const top = 180 + Math.ceil(ROOMS.length / 4) * 46 + 30;
    label(40, top, 'Quests (click one to mark it and everything before it done)');
    db.quests().forEach((q, i) => {
      const x = 40 + (i % 3) * 300;
      const y = top + 40 + Math.floor(i / 3) * 46;
      const status = questStatus(player, q);
      const tag = status === 'done' ? 'done' : status === 'locked' ? 'locked' : status;
      button(x, y, `${q.name} (${tag})`, () => (completeQuestChain(player, q), changed(`${q.name}: done`)), 290, status === 'done' ? ON : undefined);
    });
    const qTop = top + 40 + Math.ceil(db.quests().length / 3) * 46 + 10;
    button(40, qTop, 'Finish active quests', () => changed(`${finishActiveQuests(player)} quest(s) ready to turn in`), 220);
    button(280, qTop, 'Reset all quests', () => ((player.data.quests = {}), changed('Quests reset')), 200, DANGER);
  };

  render();
  return modal;
}
