import { BALANCE } from '../config';
import { ELEMENTS, ELEMENT_LABELS } from './elements';
import { ELITE_AFFIXES } from './elites';
import type { FloorDef, FlightDef, TwistDef, VendorDef } from './types';

/**
 * The tower (docs/TOWER_SPEC.md): 30 floors in 3 flights, each floor one fight against a known monster moved up to the
 * floor's level, plus twists shown before you go in. Rules: src/game/Tower.ts.
 */

const t = BALANCE.tower;

export const TWISTS: TwistDef[] = [
  // The Elite affixes, without the Elite's own size-up.
  ...ELITE_AFFIXES.map((a) => ({ id: a.id, name: a.name, text: a.description, icon: `twist_${a.id}`, affix: a.id })),
  { id: 'unshakable', name: 'Unshakable', text: "Can't be stunned or frozen.", icon: 'twist_unshakable', unshakable: true },
  { id: 'short_fuse', name: 'Short Fuse', text: `Enrages from turn ${t.shortFuse}.`, icon: 'twist_short_fuse', shortFuse: true },
  { id: 'no_potions', name: 'No Potions', text: 'No items this fight.', icon: 'twist_no_potions', noItems: true },
  { id: 'fury', name: 'Fury', text: 'Starts with War Cry.', icon: 'status_war_cry', starts: [{ status: 'war_cry', turns: 3, self: true }] },
  { id: 'hardened', name: 'Hardened', text: 'Starts with Iron Skin.', icon: 'status_iron_skin', starts: [{ status: 'iron_skin', turns: 3, self: true }] },
  { id: 'regrowth', name: 'Regrowth', text: 'Starts with Regen.', icon: 'status_regen', starts: [{ status: 'regen', turns: 5, self: true }] },
  { id: 'ambush_shock', name: 'Ambush: Shock', text: 'You start Shocked.', icon: 'twist_ambush', starts: [{ status: 'shock', turns: 3 }] },
  { id: 'ambush_burn', name: 'Ambush: Burn', text: 'You start Burning.', icon: 'twist_ambush', starts: [{ status: 'burn', turns: 3 }] },
  ...ELEMENTS.map((e) => ({
    id: `veil_${e}`,
    name: `${ELEMENT_LABELS[e]} Veil`,
    text: `Resists every element but ${ELEMENT_LABELS[e]}.`,
    icon: 'twist_veil',
    veil: e,
  })),
  ...ELEMENTS.map((e) => ({
    id: `shell_${e}`,
    name: `${ELEMENT_LABELS[e]} Shell`,
    text: `Takes ${Math.round(t.shell.reduction * 100)}% less until ${t.shell.hits} ${ELEMENT_LABELS[e]} hits. Regrows at half HP.`,
    icon: 'twist_shell',
    shell: { element: e, hits: t.shell.hits },
  })),
];

export const FLIGHTS: FlightDef[] = [
  { id: 'thornwood', name: 'Thornwood Flight', first: 1, last: 10, opensAfter: 'heart_of_thorns', marks: 10, lootTable: 'thornwood', sigil: 'dawn', background: ['bg_spire_thornwood', 'bg_thornwood'] },
  { id: 'fen', name: 'Fen Flight', first: 11, last: 20, opensAfter: 'eye_of_the_storm', marks: 20, lootTable: 'fen', sigil: 'dawn', background: ['bg_spire_fen', 'bg_fen_road'] },
  { id: 'frostfang', name: 'Frostfang Flight', first: 21, last: 30, opensAfter: 'the_frozen_roost', marks: 30, lootTable: 'frostfang', sigil: 'dusk', background: ['bg_spire_frostfang', 'bg_frostfang_pass'] },
];

const f = (floor: number, monsterId: string, level: number, twists: string[], heroic?: boolean): FloorDef => ({ floor, monsterId, level, twists, ...(heroic ? { heroic } : {}) });

export const FLOORS: FloorDef[] = [
  f(1, 'mossling', 6, ['frenzied']),
  f(2, 'thornback_boar', 6, ['short_fuse']),
  f(3, 'bramble_bandit', 7, ['hulking']),
  f(4, 'mire_toad', 7, ['venomous']),
  f(5, 'barkshell_beetle', 7, ['warded']),
  f(6, 'spark_wisp', 10, ['veil_wind']),
  f(7, 'ash_imp', 7, ['fury']),
  f(8, 'bramble_bandit', 10, ['no_potions']),
  f(9, 'ash_imp', 8, ['unshakable', 'short_fuse']),
  f(10, 'thornheart', 12, ['hardened'], true),
  f(11, 'mire_toad', 12, ['hulking']),
  f(12, 'spark_wisp', 13, ['ambush_shock']),
  f(13, 'bog_lurker', 13, ['ironhide']),
  f(14, 'sunken_revenant', 14, ['veil_light']),
  f(15, 'bogmother', 16, ['no_potions'], true),
  f(16, 'volt_eel', 18, ['unshakable', 'frenzied']),
  f(17, 'stormscale_drakeling', 17, ['shell_water']),
  f(18, 'spark_wisp', 18, ['short_fuse', 'venomous']),
  f(19, 'sunken_revenant', 16, ['regrowth', 'warded']),
  f(20, 'stormcoil', 21, ['fury'], true),
  f(21, 'drift_wolf', 17, ['frenzied']),
  f(22, 'crag_ram', 18, ['ironhide']),
  f(23, 'gust_sprite', 18, ['veil_earth']),
  f(24, 'ember_salamander', 19, ['ambush_burn']),
  f(25, 'old_snowmaw', 19, ['regrowth'], true),
  f(26, 'glacier_golem', 21, ['unshakable', 'hulking']),
  f(27, 'rime_acolyte', 22, ['no_potions', 'warded']),
  f(28, 'vent_wyrmling', 21, ['shell_ice', 'frenzied']),
  f(29, 'drift_wolf', 24, ['short_fuse', 'venomous', 'fury']),
  f(30, 'frostbinder', 23, ['hardened', 'no_potions'], true),
];

export const VENDOR: VendorDef[] = [
  { minFloor: 5, kind: 'set', set: 'thornwarden', marks: 25, gold: 300 },
  { minFloor: 10, kind: 'cache', lootTable: 'thornwood', sigil: 'dawn', marks: 60, gold: 500 },
  { minFloor: 15, kind: 'set', set: 'stormwatch', marks: 40, gold: 900 },
  { minFloor: 20, kind: 'cache', lootTable: 'fen', sigil: 'dawn', marks: 90, gold: 1500 },
  { minFloor: 25, kind: 'set', set: 'frostward', marks: 60, gold: 2000 },
  { minFloor: 25, kind: 'books', elements: ['light', 'dark'], marks: 30, gold: 0 },
  { minFloor: 30, kind: 'cache', lootTable: 'frostfang', sigil: 'dusk', marks: 120, gold: 3000 },
];
