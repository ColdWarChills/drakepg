import type { QuestDef } from './types';

/**
 * The story: each quest unlocks the next, and the boss quests also need an Adventurers' Guild rank (`requiresRank`).
 * Errands between them are the Guild's hunts now (src/game/Guild.ts).
 * Chapter 1: The Restless Thornwood. Chapter 2: Rising Waters. Chapter 3: The Frozen Roost.
 */
export const QUESTS: QuestDef[] = [
  {
    id: 'heart_of_thorns',
    name: 'Heart of Thorns',
    giver: 'elder_maren',
    requiresRank: 'E',
    objective: { kind: 'kill', monsterId: 'thornheart', count: 1 },
    goal: 'Defeat the Thornheart',
    offer: [
      'The moss, the bandits, the imps... it all points to one thing.',
      'An ancient heartwood tree has woken in the hollow past the Deep Thornwood. The Thornheart. The thorn wall that blocks the path will part for you now.',
      'Watch it closely. When it gathers its strength, brace yourself: Defend, or heal.',
    ],
    reminder: 'The Thornheart waits in the hollow past the Deep Thornwood. Stock up on potions first.',
    complete: [
      'You did it. Listen... the forest is already quieter.',
      'This was carved from the Thornheart itself. It belongs with you now.',
      'I fear this is only the beginning. But tonight, Brindlemoor celebrates you.',
    ],
    rewards: {
      xp: 250,
      gold: 150,
      classItems: { warrior: 'thornheart_blade', mage: 'heartwood_staff', rogue: 'briar_fang' },
      // Story bosses teach every class a skill (docs/BUILD_VARIETY_SPEC.md).
      classBooks: { warrior: 'shield_bash', mage: 'meteor', rogue: 'expose_weakness' },
    },
    endsChapter: 'Chapter 1: The Restless Thornwood',
  },

  // Chapter 2: Rising Waters
  {
    id: 'rising_waters',
    name: 'Rising Waters',
    giver: 'elder_maren',
    requires: 'heart_of_thorns',
    objective: { kind: 'kill', monsterId: 'mire_toad', count: 4 },
    goal: 'Mire Toads defeated',
    offer: [
      'The Thornheart is gone, yet the land is still uneasy. Last night the river west of town burst its banks.',
      'The Mirewater Fen is creeping toward our fields, and folk say it crackles like a storm that never breaks.',
      "I've told Garrick to take down the barricade on the west road. Clear the Mire Toads off the Fen Road, four of them, then come back to me.",
    ],
    reminder: 'Four Mire Toads on the Fen Road, west of town. Mind their tongues.',
    complete: [
      'Good. The road is passable again.',
      'Past the Fen Road stands Reedwatch, an old outpost. Captain Hale holds it. Tell him I sent you, and take these: the fen is no place to run dry.',
    ],
    rewards: { xp: 150, gold: 80, items: ['greater_health_potion', 'greater_health_potion'] },
  },
  {
    id: 'mother_of_the_mire',
    name: 'Mother of the Mire',
    giver: 'captain_hale',
    requires: 'rising_waters',
    requiresRank: 'D',
    objective: { kind: 'kill', monsterId: 'bogmother', count: 1 },
    goal: 'Defeat the Bogmother',
    offer: [
      'The toads, the lurkers... they all crawl out of Bog Hollow, past the causeway. Something huge lives in that pool. We call her the Bogmother.',
      "My men will cut a path through the reeds for you. She'll sink into the mud before a big strike: brace yourself.",
    ],
    reminder: 'Bog Hollow is past the Sunken Causeway. When she sinks into the mire, defend or heal.',
    complete: [
      'The Bogmother, dead? The whole outpost will drink to you tonight.',
      'But my scouts saw what lies beyond the hollow. An old shrine, half drowned... and lightning crawling over it.',
    ],
    rewards: { xp: 400, gold: 200, items: ['greater_health_potion', 'greater_health_potion', 'greater_health_potion'], classBooks: { warrior: 'iron_skin', mage: 'sunburst', rogue: 'envenom' } },
  },
  {
    id: 'eye_of_the_storm',
    name: 'Eye of the Storm',
    giver: 'captain_hale',
    requires: 'mother_of_the_mire',
    objective: { kind: 'kill', monsterId: 'stormcoil', count: 1 },
    goal: 'Defeat the Stormcoil',
    offer: [
      'The storms, the wisps, the flood: it all comes from the Drowned Shrine. A great serpent coils there, around something that glows.',
      "We'll clear the reeds past Bog Hollow. Be warned: my scouts swear it changed color while they watched. Bring more than one trick.",
    ],
    reminder: 'The Drowned Shrine lies past Bog Hollow. Lightning first, then water. Bring both, or something neutral.',
    complete: [
      'The rain has stopped. For the first time in a month, I can see the stars over the fen.',
      'And this... a scale as big as a shield, dull and cold. That is no serpent scale. That is a dragon\'s.',
      'Take this, forged in our smithy around a piece of the Stormcoil. Then go tell Maren what you found. She will want to know about that scale.',
    ],
    rewards: {
      xp: 600,
      gold: 300,
      classItems: { warrior: 'thunderhead_maul', mage: 'stormcoil_scepter', rogue: 'coilfang' },
      // Story bosses teach every class a skill (docs/BUILD_VARIETY_SPEC.md).
      classBooks: { warrior: 'bloodlust', mage: 'tidal_wave', rogue: 'shadowstep' },
    },
    endsChapter: 'Chapter 2: Rising Waters',
  },
  // --- Chapter 3: The Frozen Roost ---
  {
    id: 'cold_scale',
    name: 'Cold Scale',
    giver: 'elder_maren',
    requires: 'eye_of_the_storm',
    objective: { kind: 'kill', monsterId: 'drift_wolf', count: 4 },
    goal: 'Drift Wolves defeated',
    offer: [
      'Let me see that scale. Hm. A dragon\'s scale should be warm, even shed. This one is cold as river ice, and look: frost, in my own kitchen.',
      'The old dragon flew north to the Frostfangs, years ago. If something up there is chilling it, I want to know what.',
      'I will have Garrick dig out the north gate. Mind the wolves on the pass: four of them, and the road is yours.',
    ],
    reminder: 'The north gate is open. Four Drift Wolves on the Frostfang Pass, then on to Hearthold.',
    complete: ['Hearthold is just past the pass. Ask for Sabine; she knows those peaks better than anyone.', 'And take these. It is colder than it looks.'],
    rewards: { xp: 300, gold: 120, items: ['superior_health_potion', 'superior_health_potion'] },
  },
  {
    id: 'the_old_one',
    name: 'The Old One in the Den',
    giver: 'ranger_sabine',
    requires: 'cold_scale',
    requiresRank: 'C',
    objective: { kind: 'kill', monsterId: 'old_snowmaw', count: 1 },
    goal: 'Defeat Old Snowmaw',
    offer: [
      'The trail to the roost runs through a den past the ridge. Old Snowmaw lives there. She used to sleep through winter. Now she guards the way like a dog on a chain.',
      'I will clear the drifts. Her hide is packed ice: hit her with fire until it cracks, or you are just knocking snow off.',
      'And when she rears up and the slope starts to slide, Defend.',
    ],
    reminder: "Snowmaw's Den is past Windscour Ridge. Fire cracks her shell; it grows back once. Defend against the Avalanche.",
    complete: ['You beat Old Snowmaw? Then nothing stands between you and the roost but whoever made all this.', 'Rest first. You will need it.'],
    rewards: { xp: 700, gold: 260, items: ['superior_health_potion', 'superior_health_potion', 'superior_health_potion'], classBooks: { warrior: 'earthshaker', mage: 'gale', rogue: 'hemorrhage' } },
  },
  {
    id: 'the_frozen_roost',
    name: 'The Frozen Roost',
    giver: 'ranger_sabine',
    requires: 'the_old_one',
    objective: { kind: 'kill', monsterId: 'frostbinder', count: 1 },
    goal: 'Defeat the Frostbinder',
    offer: [
      'The dragon\'s roost is just past the den. My scouts saw a woman up there, all in white, and a wall of blue ice taller than the watchtower.',
      'Whatever she is doing, it is why winter will not leave. Bring fire. And bring something for the wind: they say when she is angry, she turns into the storm.',
    ],
    reminder: 'The Frozen Roost is past the den. Fire for the ice, Earth for the storm.',
    complete: [
      'The wind just... stopped. I can hear the meltwater running for the first time in a year.',
      'You saw it? The dragon, in the ice? And it looked at you? Then it is alive. Maren will want to hear every word.',
      'Take this. She left it behind when the storm blew itself out.',
    ],
    rewards: {
      xp: 1000,
      gold: 400,
      classItems: { warrior: 'whiteout', mage: 'squall_stave', rogue: 'galecutter' },
    },
    endsChapter: 'Chapter 3: The Frozen Roost',
  },
];
