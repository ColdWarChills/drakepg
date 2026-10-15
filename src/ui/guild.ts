import Phaser from 'phaser';
import { BALANCE, COLORS } from '../config';
import type { NpcDef, RankId } from '../data/types';
import { cheats } from '../game/cheats';
import {
  activeHunts,
  canTakeHunt,
  dropHunt,
  huntDef,
  huntPoints,
  huntTitle,
  huntWhere,
  maxHuntRank,
  MAX_RANK,
  pointsToNext,
  rankId,
  refreshBoard,
  takeHunt,
  turnInHunt,
  type Hunt,
  type TurnInHunt,
} from '../game/Guild';
import type { Player } from '../game/Player';
import { questWaitingOnRank } from '../game/Quests';
import { Button } from './Button';
import { Modal } from './Modal';
import { play } from './sfx';
import { addText } from './text';

/** Badge colors, F (plain) to S (gold). */
export const RANK_COLORS: Record<RankId, number> = {
  F: 0x8d8d8d,
  E: 0x5fae5f,
  D: 0x3f8fd8,
  C: 0x9b59b6,
  B: 0xe08a2c,
  A: 0xd9534f,
  S: 0xf1c40f,
};

/** A rounded rank letter. */
export function rankBadge(scene: Phaser.Scene, x: number, y: number, rank: number, size = 40): Phaser.GameObjects.Container {
  const id = rankId(rank);
  const box = scene.add.rectangle(0, 0, size, size, RANK_COLORS[id]).setRounded(size * 0.2).setStrokeStyle(2, 0x000000, 0.5);
  const letter = addText(scene, 0, 1, id, { size: Math.round(size * 0.6), bold: true, color: id === 'S' ? '#2b1d00' : '#ffffff', stroke: id !== 'S' }).setOrigin(0.5);
  return scene.add.container(x, y, [box, letter]);
}

const G = BALANCE.guild;

/** "+225 XP  +94 g  +2 rank pts" (testing rates included). */
function rewardText(player: Player, h: Hunt): string {
  const pts = huntPoints(player, h);
  const ptsText = player.data.guild.rank >= MAX_RANK ? '' : pts > 0 ? `  +${pts} rank pt${pts > 1 ? 's' : ''}` : '  no rank pts';
  return `+${h.xp * cheats.rates} XP  +${h.gold * cheats.rates} g${ptsText}`;
}

/**
 * The Guild Clerk's board: your rank, the hunts you've taken (turn in or drop) and the notices you can take.
 * `onTurnIn` lets the room show level-ups and promotions.
 */
export function openGuild(scene: Phaser.Scene, player: Player, clerk: NpcDef, onChange: () => void, onTurnIn: (r: TurnInHunt) => void): Modal {
  const modal = new Modal(scene, 1160, 650, "Adventurers' Guild", () => {});
  let message: { text: string; color: string } | null = null;
  let confirmDrop: string | null = null;
  const changed = () => {
    onChange();
    render();
  };

  const render = () => {
    refreshBoard(player);
    const c = modal.content;
    c.removeAll(true);
    const g = player.data.guild;

    // Header: rank, points, what the story waits on.
    c.add(rankBadge(scene, 76, 92, g.rank, 64));
    c.add(addText(scene, 122, 58, `Rank ${rankId(g.rank)} adventurer`, { size: 24, bold: true, color: COLORS.textGold }));
    const toNext = pointsToNext(g.rank);
    const barW = 260;
    if (toNext > 0) {
      c.add(scene.add.rectangle(122, 100, barW, 14, COLORS.barBg).setOrigin(0, 0.5).setRounded(5));
      if (g.points > 0) c.add(scene.add.rectangle(122, 100, Math.max(10, (barW * g.points) / toNext), 14, RANK_COLORS[rankId(g.rank + 1)]).setOrigin(0, 0.5).setRounded(5));
      c.add(addText(scene, 122 + barW + 12, 100, `${g.points}/${toNext} rank points to ${rankId(g.rank + 1)}`, { size: 16, color: COLORS.text }).setOrigin(0, 0.5));
    } else c.add(addText(scene, 122, 100, 'The highest rank there is.', { size: 16, color: COLORS.text }).setOrigin(0, 0.5));
    const waiting = questWaitingOnRank(player);
    const sub = waiting
      ? `Next story quest (${waiting.name}) needs Rank ${waiting.requiresRank}.`
      : `Hunts done: ${g.done}. You may take hunts up to Rank ${rankId(maxHuntRank(player))}.`;
    c.add(addText(scene, 122, 116, sub, { size: 15, color: waiting ? COLORS.textGood : COLORS.textDim }));
    c.add(addText(scene, 1130, 58, `${clerk.name}: "${clerk.dialogue[0]}"`, { size: 15, color: COLORS.textDim, wrap: 430, align: 'right' }).setOrigin(1, 0));
    c.add(scene.add.rectangle(30, 150, 1100, 2, COLORS.panelBorder, 0.6).setOrigin(0));

    // Left: hunts you've taken.
    const mine = activeHunts(player);
    c.add(addText(scene, 40, 164, `Your hunts (${mine.length}/${G.maxActive})`, { size: 20, bold: true }));
    if (mine.length === 0) c.add(addText(scene, 40, 200, 'None yet. Take a notice from the board.', { size: 16, color: COLORS.textDim }));
    mine.forEach(({ hunt, count, ready }, i) => {
      const y = 198 + i * 118;
      c.add(
        scene.add
          .rectangle(30, y, 540, 108, ready ? 0x22361f : 0x241e2e)
          .setOrigin(0)
          .setRounded(10)
          .setStrokeStyle(2, ready ? 0x7ddc7d : COLORS.panelBorder, ready ? 1 : 0.5),
      );
      c.add(rankBadge(scene, 60, y + 30, hunt.rank));
      c.add(addText(scene, 92, y + 10, huntTitle(hunt), { size: 19, bold: true }));
      c.add(addText(scene, 92, y + 36, huntWhere(player, hunt), { size: 14, color: COLORS.textDim, wrap: 320 }));
      // Progress bar.
      const pw = 200;
      c.add(scene.add.rectangle(92, y + 84, pw, 12, COLORS.barBg).setOrigin(0, 0.5).setRounded(4));
      if (count > 0) c.add(scene.add.rectangle(92, y + 84, Math.max(8, (pw * count) / hunt.count), 12, ready ? 0x7ddc7d : COLORS.xp).setOrigin(0, 0.5).setRounded(4));
      c.add(addText(scene, 92 + pw + 10, y + 84, ready ? `${count}/${hunt.count} Done!` : `${count}/${hunt.count}`, { size: 15, bold: ready, color: ready ? COLORS.textGood : COLORS.text }).setOrigin(0, 0.5));
      c.add(addText(scene, 92, y + 58, rewardText(player, hunt), { size: 14, color: COLORS.textGold }));
      if (ready) {
        c.add(
          new Button(scene, 490, y + 54, 'Turn in', () => {
            const result = turnInHunt(player, hunt.key);
            if (!result) return;
            play(scene, 'quest');
            message = result.promotions.length
              ? { text: `Promoted to Rank ${result.promotions[result.promotions.length - 1]}!`, color: COLORS.textGood }
              : { text: `${huntTitle(hunt)}: done. +${result.points} rank point${result.points === 1 ? '' : 's'}. New notices are up.`, color: COLORS.textGood };
            confirmDrop = null;
            onTurnIn(result);
            changed();
          }, { width: 130, height: 50, fontSize: 19, color: 0x2f5d2f }),
        );
      } else {
        const sure = confirmDrop === hunt.key;
        c.add(
          new Button(scene, 490, y + 54, sure ? 'Sure?' : 'Drop', () => {
            if (!sure) {
              confirmDrop = hunt.key;
              message = { text: hunt.kind === 'gather' ? 'Drop it? Items you gathered stay in your bag.' : 'Drop it? Its kill count is lost.', color: COLORS.textBad };
              render();
              return;
            }
            dropHunt(player, hunt.key);
            confirmDrop = null;
            message = { text: `Dropped: ${huntTitle(hunt)}.`, color: COLORS.textDim };
            changed();
          }, { width: 110, height: 44, fontSize: 17, color: sure ? 0x6b2a2a : COLORS.button }),
        );
      }
    });

    // Right: the board.
    c.add(addText(scene, 600, 164, 'Notice board', { size: 20, bold: true }));
    const full = !canTakeHunt(player);
    if (g.board.length === 0) {
      c.add(addText(scene, 600, 200, 'Nothing pinned up right now. Explore further, and come back.', { size: 16, color: COLORS.textDim, wrap: 520 }));
    }
    g.board.forEach((key, i) => {
      const hunt = huntDef(key);
      if (!hunt) return;
      const y = 198 + i * 76;
      const above = hunt.rank > g.rank;
      c.add(scene.add.rectangle(590, y, 540, 68, 0x241e2e).setOrigin(0).setRounded(10).setStrokeStyle(2, RANK_COLORS[rankId(hunt.rank)], above ? 0.9 : 0.4));
      c.add(rankBadge(scene, 620, y + 34, hunt.rank, 36));
      c.add(addText(scene, 650, y + 6, huntTitle(hunt), { size: 18, bold: true, color: hunt.kind === 'elite' ? COLORS.textGold : COLORS.text }));
      c.add(addText(scene, 650, y + 29, huntWhere(player, hunt), { size: 13, color: COLORS.textDim, wrap: 340 }));
      c.add(addText(scene, 650, y + 47, rewardText(player, hunt), { size: 13, color: COLORS.textGold }));
      c.add(
        new Button(scene, 1065, y + 34, 'Take', () => {
          if (!takeHunt(player, key)) return;
          message = { text: `Taken: ${huntTitle(hunt)}.`, color: COLORS.textGood };
          confirmDrop = null;
          changed();
        }, { width: 100, height: 44, fontSize: 18, sublabel: full ? `${G.maxActive}/${G.maxActive}` : undefined }).setEnabled(!full),
      );
    });

    const help = `Hunts up to ${G.reach} ranks above yours. Rank points: 1 at your rank, +1 per rank above, none below. Every town's clerk shares this board.`;
    c.add(addText(scene, 40, 598, message?.text ?? help, { size: 16, color: message?.color ?? COLORS.textDim, bold: !!message, wrap: 1080 }));
  };

  render();
  return modal;
}
