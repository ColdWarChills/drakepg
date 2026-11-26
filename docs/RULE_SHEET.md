# The rule sheet: what the game is checked against (2026-11-26)

The designer, 2026-11-26: "Testing it for now takes too long and have no clear target of what we are testing. We should look for some rulesets there are to see where and how we can improve."

This is the target. It is cut down from the known lists people use to judge games (the sources are at the bottom). Nothing here is scored yet, except the short list under "Known today".

## The short version

1. The first ten minutes are obvious and feel good.
2. The player always knows the goal, and rewards change what the hero can do.
3. Fights are clear, fair and not all the same.
4. Screens and controls never get in the way.
5. It feels alive, and it has something of its own.
6. The demo shows the best 20 to 45 minutes and ends on a high point.

## How testing works with this sheet

- **Most rules are checked without the designer playing.** Checking a game against a list is a known method: one person goes through the game twice with the list, an hour or two in all. Claude does that on the running game, and the bots give the numbers.
- **The designer's part is one short sit-down:** about 20 minutes, on one part of the game, with one question written down before starting. "Is it fun?" is not a question. "Does the first boss make me change what I do?" is.
- **First impressions need a new player.** The designer knows the game too well to see its first ten minutes. A new player plays alone, is watched and not helped, and what they do counts more than what they say.
- **Who checks each rule** is written after it:
  - **Bot:** `npm run sim`, `npm run pace` and the Unity playthrough bot. Numbers only.
  - **Claude:** on the running Unity game, with a picture for every rule the game breaks.
  - **Designer:** the 20-minute sit-down.
  - **New player:** someone who has never seen the game.

## The rules

### 1. The first ten minutes

- **Playing within one minute of pressing Start.** Logos and the intro can be skipped. *Claude. From the demo rules.*
- **It is always obvious where to go and what to press,** and every action gets a clear, good reaction. *New player, Claude. From PLAY ("the first ten minutes of play ... are painfully obvious and should result in immediate and positive feedback").*
- **Nothing has to be read to play.** Each thing is taught at the moment it is needed, not before. *New player, Claude. From PLAY and the onboarding article.*
- **Early choices are few, and one is suggested.** *Claude. From the choice overload study.*
- **The first thing worth telling a friend about happens before minute 15.** *Bot for the minutes, Designer for whether it is worth telling. From the demo numbers.*

### 2. Goals and progress

- **The player can always say what they are doing now and what the big goal is.** *Claude, New player. From PLAY ("presents overriding goals early as well as short term goals throughout").*
- **Rewards and level-ups change what the hero can do,** not only a number. *Claude, Bot. From PLAY and "level-ups must change how you play".*
- **No long stretch with nothing new:** no run of rooms, levels or fights where nothing opens, drops or changes. *Bot. From PLAY (boredom is kept down by varying activities and pacing).*
- **Nothing hard-won is lost.** *Claude. From PLAY.*

### 3. The fight

- **The same action always gives the same kind of result,** and every result shows its reason. *Claude. From Pinelle 1.*
- **The monster's next move, and why you took the damage you took, are on the screen.** *Claude. From Pinelle 3 and 8, and our own rule "no hidden specials".*
- **A loss says why, and trying again is immediate.** *Claude. From PLAY ("should not experience being penalized repetitively for the same failure") and the fast retry article.*
- **Difficulty rises with the player.** Bosses need a change of tactics; ordinary fights are short. *Bot. From PLAY ("the AI is tough enough that the players have to try different tactics against it").*
- **Fights don't all play the same.** *Designer, Bot for how often the same button wins. From PLAY ("no repetitive or boring tasks") and what players complain about in this genre.*

### 4. Screens and controls

- **The screen shows what the next decision needs,** and no more. *Claude. From Pinelle 8.*
- **Words and icons mean the same everywhere.** A number the player can't understand is explained on hover. *Claude. From PLAY and Pinelle 9 and 10.*
- **Menus take few steps:** one button forward, one back. The bag needs no babysitting. *Claude. From PLAY ("navigation is consistent, logical and minimalist"), Pinelle 10 and the genre's complaints.*
- **Things seen many times can be skipped or sped up.** *Claude. From Pinelle 5 and 2.*
- **Keys can be changed, settings are remembered, and sound has its own sliders.** *Claude. From Pinelle 2 and 6, and the accessibility basics.*
- **Nothing is told by colour alone or by sound alone,** and the text is easy to read. *Claude. From the accessibility basics.*

### 5. Feel and world

- **The hit feels good:** it answers at once, the motion eases, other things react, and the screen is never dead still. *Designer, Claude on clips. From the juice article.*
- **Sound and music carry the mood.** *Designer. From PLAY.*
- **The game has something of its own** that a player would describe to a friend. *Designer, New player. From PLAY ("offers something different in terms of attracting and retaining the players' interest").*
- **The player cares about the hero and the world.** *New player, Designer. From PLAY.*

### 6. The demo

- **20 to 45 minutes of the best content.** The middle player stops after about 14 minutes, so the best thing comes early. *Bot for the minutes, Claude. From the demo numbers.*
- **Only finished things are in it:** no stand-in art, no half-built systems. *Claude. From the demo checklists.*
- **It ends on a high point** (a boss) and asks for a wishlist. *Claude.*
- **Saving and continuing work, settings stay, and the menu has a link for feedback.** *Claude.*
- **Five to ten new players play it cold before the fest.** *New player.*

## Known today

Not scored. This is only what the records already say, so nobody starts from nothing.

- **Built last week, and they answer rules above** (from the handoff of 2026-11-26; not looked at again here):
  - the defeat card that says why you lost, with Try again (rule: a loss says why);
  - the Options page: every key can be changed, plus shake, flashes and volume (rule: keys and settings);
  - one "New!" card per new thing, with a picture (rule: taught when needed).
- **A known gap:** the game has no music (rule: sound and music carry the mood).
- **Old findings, from the review of 2026-11-10.** They were true then; the game has changed since. Each one is looked at again before anyone trusts it:
  - about 88% of Victory cards said "No loot this time";
  - the card showed "+95 XP" but no XP bar;
  - a level-up said only "+3 stat points (press C)";
  - levels 13-15 and 17-19 gave only 3 stat points, nothing new;
  - bosses fell on the first try (about 97% of 300 boss fights) and gold piled up.

## The steps

1. **This sheet.** Done when the designer has read it.
2. **Claude scores the Unity game against it, rule by rule.** A picture for every rule the game breaks, the worst five first. What the game does today is checked in the game, not taken from old records. Starts after the designer's yes.
3. **The designer's 20-minute sit-down,** with the one question the scoring could not answer.

This takes the place of the roadmap's "Play the first hour by hand and fix".

## Where the rules come from

- **PLAY** `[study]`: Desurvire and Wiberg, 2009, "Game Usability Heuristics (PLAY) for Evaluating and Designing Better Games". 54 players rated good and bad games against 116 candidate rules; 48 held. It covers fun and ease of use together, and it is the backbone of this sheet. The wording was checked against the paper. [The paper](https://ocw.metu.edu.tr/pluginfile.php/4129/mod_resource/content/0/ceit706_2/10/game_usability-_heuristics.pdf)
- **Pinelle's ten** `[study]`: Pinelle, Wong and Stach, 2008, "Heuristic evaluation for games: usability principles for video game design". Ten rules drawn from the problems named in 108 game reviews. Ease of use only; it leaves fun out on purpose. The wording was checked. [The paper's page](https://dl.acm.org/doi/10.1145/1357054.1357282)
- **The demo numbers** `[article]`: How To Market A Game, 2022, a survey of about 130 demos. The middle demo is played for 14 minutes. Games that went on to earn the least: 7 minutes; the most: 65. Most makers design 30 minutes and players do not finish. [The article](https://howtomarketagame.com/2022/10/26/what-is-a-good-median-play-time-for-a-demo-benchmark/)
- **Steam Next Fest dates** `[article]`, from Steam's own pages, read 2026-11-26:
  - February 22 to March 1, 2027: sign up by January 10; the demo goes in for review by February 1.
  - June 14 to 21, 2027: sign up by April 25; the demo goes in for review by May 24.
  - A game can take part in only one.
  - [Steam's page](https://partner.steamgames.com/doc/marketing/upcoming_events)
- **How to run a sit-down** `[article]`: one or two written questions, a short slice, stay silent, trust what players do over what they say; testers try harder when watched.
- **Onboarding, choice overload, the hit's feel, menus, accessibility basics** `[article]`: the design advisor's reference notes.
  - [Onboarding](https://www.gamedeveloper.com/design/how-onboarding-should-be-applied-to-tutorials)
  - [Choice overload](https://www.gamedeveloper.com/design/spoiled-for-choice-the-psychology-of-choice-overload-in-games-and-how-to-avoid-it)
  - [Six mistakes that drain the juice](https://gamedeveloper.com/design/6-mistakes-that-ll-drain-the-juice-out-of-your-game)
  - [Game Accessibility Guidelines, basic](https://gameaccessibilityguidelines.com/basic/)
- **What players complain about in this genre:** our own market research, `docs/research/2026-10_steam_indie_rpg_market.md`. Every fight the same, bag babysitting, level-ups that give nothing, no directions, too short.
- **Looked at and not used on their own** `[from memory]`: Federoff's 40 rules (2002; PLAY grew out of this line of work), Korhonen and Koivisto's rules for mobile games (2006), Nielsen's ten for software (too general for a game), and Hodent's split into ease of use and pull.
- **Claude's own part** `[my read]`: the cut from 58 rules to 29, the six groups, and who checks which rule.
