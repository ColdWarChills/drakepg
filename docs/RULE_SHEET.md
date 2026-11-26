# The rule sheet: what the game is checked against (2026-11-26)

The designer, 2026-11-26: "Testing it for now takes too long and have no clear target of what we are testing. We should look for some rulesets there are to see where and how we can improve."

This is the target. It is cut down from the known lists people use to judge games (the sources are at the bottom). The Old Road and Chapter 1 are scored against it (2026-11-26, under "The score"). Chapters 2 and 3 are not scored yet.

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

## The score: the Old Road and Chapter 1 (2026-11-26)

- **What was played:** a new Warrior, from New Game through the Old Road, Brindlemoor and Chapter 1 to the Thornheart, the hand-in after it, and back in through Continue. It was played in the Unity game by script, through the game's own buttons, with a new player's settings and no cheats in the fights that were judged.
- **Why only this part:** it is what a demo player sees.
- **The count:** of 29 rules, 8 are kept, 8 are broken, 6 are kept in one part and broken in another, and 7 could not be checked this way.
- **The pictures** are on the PC in `art/tests/rule_sheet/`, not in the repo. The worst five, with a line of text on each, are in `worst5/` there. The full notes are `EVIDENCE.md` in the same folder. Names in `code` below are pictures in that folder.
- **Nothing in the game was changed.** The fixes are not built.

### The worst five

1. **Handing in hunts is a pile-up.**
   - The rewards, the promotion to Rank E, the level-up and the new skill float as four lines on top of the Guild board's own text.
   - A level gained here gets no level page. The stat points have to be found under Character.
   - Pictures: `2_turnin_0`, `2_character_points`.
   - Rules: the screen shows what the next decision needs; rewards and level-ups.
2. **Chapter 1 ends badly.**
   - The boss's card is the high point (`3_boss_card`). "Chapter Complete!" comes only after the walk back to Maren.
   - It is drawn over a "New!" card, with two buttons at once (Continue and Next).
   - It says "Thanks for playing this early build. More chapters are on the way." while the game carries on into Chapter 2.
   - There is no wishlist ask and no link.
   - Pictures: `6_after_turnin_0`, `6_after_turnin_1`.
   - Rule: it ends on a high point and asks for a wishlist.
   - This is not the summary card the designer said no to. It is how the ending that is already there is shown.
3. **A Tough Ash Imp stands in the first Chapter 1 room.**
   - It is Lv 3 among Lv 1-2 monsters. It killed the Lv 2-3 hero in 4 of 7 meetings, each time in 2 turns.
   - Nearly all of the bot's deaths in Chapter 1 are this monster.
   - The loss offers only "Wake up" and four weak-spot icons with no words.
   - Pictures: `cards/card_002`, `battles_attackonly/ash_imp`.
   - Rules: obvious first ten minutes; difficulty rises with the player; a loss says why.
   - It also goes against the designer's own rule that the early game is easy.
4. **The first goal on the screen is wrong.**
   - In the first road room the goal box says "Next: reach Guild Rank E (you are F)", before the player has met the Guild.
   - Picture: `1_first_room_0s`.
   - Rule: it is always obvious where to go.
5. **Developer leftovers and stand-ins.**
   - "Start at Ch 1 / Ch 2 / Ch 3" on the class screen.
   - "v0.8 · Chapter 3 · Unity" on the title.
   - Flat shapes for the campfire, the old bones and the thorns at the Briar Wall, and a plain box on a post in Woodcutters' Clearing.
   - Pictures: `1_create_class`, `1_title`, `2_briar_rest`, `2_room_woodcutters_clearing`.
   - Rule: only finished things are in it.

### Every rule

Each rule is named by its first words, in the sheet's order.

- **1. The first ten minutes**
  - **Playing within one minute:** kept. No logos, no intro. Three clicks from the title to the first room (four when a save exists), and the room is there in half a second. About 2 seconds in, Odo's talk box opens by itself and the hero can't walk until "Goodbye". `1_title`, `1_create_hero`, `1_first_room_2s`.
  - **Always obvious where to go and what to press:** broken. Good: the big yellow exit arrow with the next room's name, the control hints, and an answer to every action. Broken: the first goal (worst five, 4) and the Ash Imp (worst five, 3).
  - **Nothing has to be read to play:** kept, with one gap. There is one "New!" card per new thing, and the level page names the new skill. The gap: why the story waits for Guild Rank E is told only in writing, by Maren and the guild board. `2_levelup_L2`, `2_maren`, `2_guild_hunts_taken`.
  - **Early choices are few, and one is suggested:** broken, small. Three classes is few. The Warrior is picked already (a green card), but no word says he is the suggested one. `1_create_class`.
  - **The first thing worth telling a friend about:** not checked. Minute 15 falls early in Chapter 1, after the gate guard (about minute 3) and the first Thornwood rooms. Whether anything by then is worth telling is the designer's to say.
- **2. Goals and progress**
  - **What they are doing now and the big goal:** kept, with gaps. The goal box is always on in rooms: hunts with their counts, then "Heart of Thorns: Defeat the Thornheart 0/1". The gaps: there is no quest log, the big goal is only in Maren's lines, and a talk box covers the goal box. `2_thornwood_path_3s`, `2_briar_rest`, `4_menu`, `4_map`.
  - **Rewards and level-ups change what the hero can do:** part kept, part broken. Kept: levels 2, 3, 4 and 6 each teach a skill, and handing in the boss quest gives a skill book and a weapon. Levels 5, 7 and 8 give numbers only (7 and 8 read from the Spellbook, not played). Broken: a level gained by handing in hunts (worst five, 1). `2_levelup_L2`, `2_levelup_L3`, `2_levelup_L5`, `4_skills`.
  - **No long stretch with nothing new:** kept by the numbers. Chapter 1 is 47 to 54 fights, with hunts, the promotion, books and drops between them. The longest stretch with nothing new was not measured.
  - **Nothing hard-won is lost:** kept, with one small loss. Losing a fight costs nothing: gold and XP stay, HP is full. The small loss: two Rank F hunts taken at "+1 rank pt" paid "+0 rank points" once the hero was Rank E. `3_retry_room`, `2_turnin_2`.
- **3. The fight**
  - **The same action, the same kind of result, and every result shows its reason:** broken. The log names the move, the number and crits ("You use Heavy Strike: 26 damage. Critical!"). It never says that a weak spot was hit, that a hit was resisted, or what Defend took off. `3_ch1trash_mid`, `3_boss_end`.
  - **The monster's next move, and why you took the damage:** part kept, part broken. Kept: the next move is always shown with its numbers ("Next: Cinder Spit ~31 dmg + Burn 30%"), and Defend says "-60% dmg, +MP". Broken: weak spots and resistances are icons with no words, and two badges are icons only (a star and a bag). The code has hover text for them; hovering could not be tried. `3_road2_menu`, `3_boss_start`.
  - **A loss says why, and trying again is immediate:** part kept, part broken. Kept at bosses: the card names the biggest hit and has "Try again" (three clicks, about 4 seconds). Its weak spot is a hammer icon with no word. Broken in ordinary fights: only "Wake up", then 1 to 4 rooms to walk back. `3_guard_attackonly_card`, `cards/card_002`.
  - **Difficulty rises with the player:** part kept, part broken. Kept: ordinary fights are short (2 to 5 turns, 6 to 15 seconds), and Attack alone lost to both bosses, 12 tries of 12. Broken: the hardest thing in Chapter 1 is the Ash Imp in its first room, not the boss. The bot beats both bosses on the first try, 117 times of 120.
  - **Fights don't all play the same:** broken for ordinary fights. Attack alone won 30 of 30: 6 against the road's Burrow Rat at Lv 1, and 24 in Chapter 1 with a Lv 4-5 hero, a little ahead of where a player first meets those monsters. Whether they feel the same is the designer's to say.
- **4. Screens and controls**
  - **The screen shows what the next decision needs:** broken in two places. The battle screen is clear. Broken: the hunt hand-in (worst five, 1) and "Chapter Complete!" over a "New!" card (worst five, 2).
  - **Words and icons mean the same, numbers explained on hover:** not checked. Hovering could not be tried by script. Bare numbers seen: "Gear 0/9 slots", and "68%" and "83%" on gear in green and blue frames. Each stat has a one-line meaning. `4_character`, `4_bag`.
  - **Menus take few steps:** kept. The shop is two clicks, the guild board one, a recall three. X or Esc closes. The bag held 5 or 6 of 30 and has "Sell junk". `4_shop_buy`, `4_bag`, `2_recall_confirm`.
  - **Things seen many times can be skipped or sped up:** kept. Battles run at x1 or x2, and an ordinary win ends in a slip of about 1.25 seconds, not a card. Two small things: scene lines need one key each and the hint does not say that Esc skips the scene (read from the code), and every level gained in a fight gives a page to click through. `4_options`, `3_ch1trash_slip`, `2_scene_briar_wall_1`.
  - **Keys, settings and sound sliders:** part kept, part broken. Kept: 16 keys can be changed, and speed, sound and shake stayed after save, quit and continue. Broken: there is one volume row, no split between music and effects, and no music. Also: S is both battle speed and Walk down. `4_options`, `4_options_changed`.
  - **Nothing by colour alone or sound alone:** broken, small. A gear's grade is a frame colour and a percent; the word (Fine, Fair) shows only on hover or on the reward card. Weak and resist are told by an icon's colour and shape. The smallest text was readable. `4_bag`, `3_boss_start`.
- **5. Feel and world**
  - **The hit feels good:** not judged; facts only. An ordinary hit: the hero is already moving in the first picture, a white flash on contact, the number rises, paper bits fly, and the HP bar drains with a lighter trail. The boss kill: a whole-screen flash, "FINISH!", slow motion, petals. The background moves in every room and fight. `clips/hit.mp4`, `clips/boss_kill.mp4`.
  - **Sound and music carry the mood:** not checked. There is no music, and the sounds were not listened to.
  - **Something of its own:** not checked. For the designer and a new player.
  - **The player cares about the hero and the world:** not checked. For a new player and the designer.
- **6. The demo**
  - **20 to 45 minutes of the best content:** kept for length. The Old Road and Chapter 1 take 29 to 33 minutes by the bot's rough clock. Whether the best thing comes early could not be checked.
  - **Only finished things are in it:** broken (worst five, 5).
  - **It ends on a high point and asks for a wishlist:** broken (worst five, 2).
  - **Saving, continuing, settings and a feedback link:** part kept, part broken. Kept: save and continue work (the same hero, level, gold and settings; back at the title in 1.5 seconds). Broken: the menu has Save, Options and "Save and quit to title", and no feedback link. `4_menu`, `6_title_after_quit`, `6_continue_room`.
  - **Five to ten new players:** not done yet.

### The old findings, looked at again

These came from the review of 2026-11-10.

- **"About 88% of Victory cards said No loot this time":** no longer true as written. An ordinary win now shows a short slip, not a card. Of 34 wins, 17 gave no item, 7 gave gear, and the rest gave potions or berries. 34 wins is a small count.
- **"The card showed +95 XP but no XP bar":** fixed. `3_boss_card`.
- **"A level-up said only +3 stat points (press C)":** fixed for fights: the level page names the new skill and what comes next. Still true for a level gained by handing in hunts.
- **"Levels 13-15 and 17-19 gave only 3 stat points":** not checked; they are past Chapter 1. The same thing starts earlier, at levels 5, 7 and 8.
- **"Bosses fell on the first try and gold piled up":** still true. The bot won 117 of 120 first tries against the two bosses. Gold went from 30 to 534 in this run; skill books at 60 g now use some of it.

### What this run could not tell

- **It was a script, not a person.** It shows what is on the screen, not what a new player understands or feels.
- **Hover text was never tried.** Every "icon with no words" above may be softened by a line on hover.
- **Sound was not listened to.**
- **Two steps went through the game's rules instead of the screen:** putting on two body armors and buying five potions.
- **The boss-kill clip** used a boss set to 1 HP.
- **"New!" cards 2 to 4** at the end of Chapter 1 were closed unseen.
- **The bot's minutes are rough,** and its first-try share is a bot's, not players'.
- **Not opened:** the forge, the coach, the Spire, the cheat menu and "Import save".
- **The gate guard's "fight the boss?" box** did not open under the script. The Thornheart's did.
- **The build:** the Unity editor's game on `claude/unity-port`. It differs from the live branch in the benched hero bone tests and about 60 lines on how the hero's hands are read. The findings were not checked a second time on the live branch.

## The steps

1. **This sheet.** Done when the designer has read it.
2. **Claude scores the Unity game against it.** Done 2026-11-26 for the Old Road and Chapter 1 (the designer: "okay, if you say so, do that."). The result is "The score" above. Chapters 2 and 3 are not scored.
3. **The designer's 20-minute sit-down,** with the one question the scoring could not answer. Claude's pick for the question: "Does any ordinary fight in Chapter 1 make me press something other than Attack?"
4. **Fixing what the score found.** Not started. It starts after the designer has seen the worst five, one fix at a time.

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
