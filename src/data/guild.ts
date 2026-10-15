import type { ClassId, MaterialDef, RankId } from './types';

/**
 * Adventurers' Guild content. The rules are in src/game/Guild.ts, the numbers in `BALANCE.guild`.
 * Hunts aren't written here: the board builds them from the monsters and areas you can reach.
 */

/**
 * Gather hunt items, one per area (`ZoneDef.gather`). They take no bag slots, like materials,
 * and drop only while a gather hunt for their area is on.
 */
export const HUNT_ITEMS: MaterialDef[] = [
  { id: 'briarberry', type: 'material', name: 'Briarberry', description: 'Guild hunt item. Dark berries off the Thornwood brambles; the healers pay for them.', price: 2, iconColor: 0x6a1b4d },
  { id: 'scorched_twig', type: 'material', name: 'Scorched Twig', description: 'Guild hunt item. Proof of where the Ash Imps have been.', price: 3, iconColor: 0x4e342e },
  { id: 'stolen_trinket', type: 'material', name: 'Stolen Trinket', description: "Guild hunt item. Something the bandits took from a traveler. The Guild sends it home.", price: 4, iconColor: 0xc9a227 },
  { id: 'bog_lily', type: 'material', name: 'Bog Lily', description: 'Guild hunt item. A pale lily that only grows in fen water.', price: 5, iconColor: 0xe1f5c4 },
  { id: 'drowned_coin', type: 'material', name: 'Drowned Coin', description: 'Guild hunt item. Old coin from the sunken road, green with age.', price: 6, iconColor: 0x5d8a6b },
  { id: 'storm_glass_shard', type: 'material', name: 'Storm Glass Shard', description: 'Guild hunt item. Sand fused by lightning.', price: 7, iconColor: 0x9ad3ff },
  { id: 'lichen_tuft', type: 'material', name: 'Lichen Tuft', description: 'Guild hunt item. Frost lichen, scraped off the pass rocks.', price: 8, iconColor: 0x9fb8a0 },
  { id: 'binding_rune', type: 'material', name: 'Binding Rune', description: 'Guild hunt item. A chip of ice carved with a rune of cold.', price: 9, iconColor: 0x81d4fa },
  { id: 'cinder_cap', type: 'material', name: 'Cinder Cap', description: 'Guild hunt item. A mushroom that grows on hot rock.', price: 9, iconColor: 0xff7043 },
  { id: 'frost_seal', type: 'material', name: 'Frost Seal', description: "Guild hunt item. White wax seals the Frostbinder's acolytes stamp on everything.", price: 10, iconColor: 0xeceff1 },
];

/** What a promotion gives, on top of the new rank. Gear is shop quality, like quest rewards. */
export interface PromotionDef {
  gold: number;
  items?: string[];
  classItems?: Partial<Record<ClassId, string>>;
}

/** Keyed by the rank you reach. The armor ones are what the Chapter 1-3 errand quests used to give. */
export const PROMOTIONS: Partial<Record<RankId, PromotionDef>> = {
  E: { gold: 30, items: ['leather_armor', 'health_potion', 'health_potion'] },
  D: { gold: 150, classItems: { warrior: 'scale_mail', mage: 'reedweave_robe', rogue: 'scale_mail' }, items: ['greater_health_potion', 'greater_health_potion'] },
  C: { gold: 200, classItems: { warrior: 'frostpelt_vest', mage: 'galeweave_robe', rogue: 'frostpelt_vest' }, items: ['superior_health_potion', 'superior_health_potion'] },
  B: { gold: 400, items: ['superior_health_potion', 'superior_health_potion', 'superior_mana_potion'] },
  A: { gold: 800 },
  S: { gold: 2000 },
};
