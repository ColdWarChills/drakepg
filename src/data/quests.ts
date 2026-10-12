import type { QuestDef } from './types';

/** Chapter 1: The Restless Thornwood. Each quest unlocks the next. */
export const QUESTS: QuestDef[] = [
  {
    id: 'moss_problem',
    name: 'Moss Problem',
    giver: 'elder_maren',
    objective: { kind: 'kill', monsterId: 'mossling', count: 4 },
    goal: 'Mosslings defeated',
    offer: [
      'Before you go far: Mosslings have crept right up to our gate. Harmless alone, a nuisance in numbers.',
      'Clear out four of them on the Thornwood Path, would you?',
    ],
    reminder: 'The Mosslings are just past the gate. Four should send the rest scurrying.',
    complete: ['Well done! Garrick can finally stop poking at the moss with his spear.', 'Take these. You will need them deeper in.'],
    rewards: { xp: 40, gold: 20, items: ['health_potion', 'health_potion'] },
  },
  {
    id: 'ember_trouble',
    name: 'Ember Trouble',
    giver: 'elder_maren',
    requires: 'moss_problem',
    objective: { kind: 'kill', monsterId: 'ash_imp', count: 3 },
    goal: 'Ash Imps defeated',
    offer: [
      'Ash Imps are setting the woods alight. They never leave the deep forest... unless something drives them out.',
      'Put out three of them before the whole Thornwood burns.',
    ],
    reminder: 'Three Ash Imps. Mind the fire they spit.',
    complete: ['The smoke is clearing. But something frightened them, and that worries me more.'],
    rewards: { xp: 70, gold: 30, items: ['mana_potion', 'mana_potion'] },
  },
  {
    id: 'bramble_bandits',
    name: 'Bramble Bandits',
    giver: 'gate_guard',
    requires: 'ember_trouble',
    objective: { kind: 'kill', monsterId: 'bramble_bandit', count: 3 },
    goal: 'Bramble Bandits defeated',
    offer: [
      "Bandits wearing brambles, of all things. They've been ambushing travelers in the Deep Thornwood.",
      "Thin their numbers. Three of them, and I'll see you get proper armor for your trouble.",
    ],
    reminder: 'Three bandits, deep in the forest. Watch your back.',
    complete: ['Ha! That will teach them. Here, this leather has saved my hide more than once.', 'Go tell the Elder. She will want to hear this.'],
    rewards: { xp: 100, gold: 60, items: ['leather_armor'] },
  },
  {
    id: 'heart_of_thorns',
    name: 'Heart of Thorns',
    giver: 'elder_maren',
    requires: 'bramble_bandits',
    objective: { kind: 'kill', monsterId: 'thornheart', count: 1 },
    goal: 'Defeat the Thornheart',
    offer: [
      'The moss, the imps, the bandits... it all points to one thing.',
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
    },
    endsChapter: 'Chapter 1: The Restless Thornwood',
  },
];
