using System;
using System.Collections.Generic;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>The Adventurers' Guild board, a port of src/ui/guild.ts.</summary>
    public static class GuildPanel
    {
        /// <summary>Badge colors, F (plain) to S (gold).</summary>
        public static readonly Dictionary<string, Color> RankColors = new Dictionary<string, Color>
        {
            ["F"] = Design.Hex(0x8d8d8d),
            ["E"] = Design.Hex(0x5fae5f),
            ["D"] = Design.Hex(0x3f8fd8),
            ["C"] = Design.Hex(0x9b59b6),
            ["B"] = Design.Hex(0xe08a2c),
            ["A"] = Design.Hex(0xd9534f),
            ["S"] = Design.Hex(0xf1c40f),
        };

        static Color RankColor(string id) => RankColors.TryGetValue(id, out var c) ? c : RankColors["F"];

        /// <summary>A rounded rank letter, centered on (x, y).</summary>
        public static VisualElement RankBadge(VisualElement parent, float x, float y, int rank, float size = 40)
        {
            var id = Guild.RankId(rank);
            const float border = 2;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(box, x, y, 0.5f, 0.5f);
            Ui.Size(box, size, size);
            Ui.Round(box, size * 0.2f);
            Ui.Border(box, border, new Color(0, 0, 0, 0.5f));
            box.style.backgroundColor = RankColor(id);
            // Children sit inside the border: the middle is (size - 2 * border) / 2.
            var mid = (size - 2 * border) / 2;
            Ui.Text(box, mid, mid + 1, id, Mathf.Round(size * 0.6f), id == "S" ? Design.Css("#2b1d00") : Color.white, true, 0.5f, 0.5f, stroke: id != "S");
            parent?.Add(box);
            return box;
        }

        /// <summary>"+225 XP  +94 g  +2 rank pts" (testing rates included).</summary>
        static string RewardText(Player player, Hunt h)
        {
            var pts = Guild.HuntPoints(player, h);
            var ptsText = player.Data.Guild.Rank >= Guild.MaxRank ? "" : pts > 0 ? $"  +{pts} rank pt{(pts > 1 ? "s" : "")}" : "  no rank pts";
            return $"+{PanelKit.Num(h.Xp * Cheats.Rates)} XP  +{PanelKit.Num(h.Gold * Cheats.Rates)} g{ptsText}";
        }

        class Message
        {
            public string Text;
            public Color Color;
        }

        /// <summary>
        /// The Guild Clerk's board: your rank, the hunts you've taken (turn in or drop) and the notices you can take.
        /// `onChange` refreshes the room (HUD, tracker, save); `onTurnIn` lets the room show level-ups and promotions.
        /// </summary>
        public static UiModal OpenGuild(Player player, NpcDef clerk, Action onChange, Action<Guild.TurnInHunt> onTurnIn)
        {
            var G = Db.B.Guild;
            var rng = new SystemRng();
            var modal = new UiModal(1160, 650, "Adventurers' Guild", () => { });
            Message message = null;
            string confirmDrop = null;

            void Changed()
            {
                onChange?.Invoke();
                Render();
            }

            void Render()
            {
                Guild.RefreshBoard(player, rng);
                var c = modal.Content;
                c.Clear();
                var g = player.Data.Guild;

                // Header: rank, points, what the story waits on.
                RankBadge(c, 76, 92, g.Rank, 64);
                Ui.Text(c, 122, 58, $"Rank {Guild.RankId(g.Rank)} adventurer", 24, Palette.TextGold, true);
                var toNext = Guild.PointsToNext(g.Rank);
                const float barW = 260;
                if (toNext > 0)
                {
                    Ui.Rect(c, 122, 100 - 7, barW, 14, Palette.BarBg, 5);
                    if (g.Points > 0) Ui.Rect(c, 122, 100 - 7, Mathf.Max(10, barW * g.Points / toNext), 14, RankColor(Guild.RankId(g.Rank + 1)), 5);
                    Ui.Text(c, 122 + barW + 12, 100, $"{g.Points}/{toNext} rank points to {Guild.RankId(g.Rank + 1)}", 16, Palette.Text, ay: 0.5f);
                }
                else Ui.Text(c, 122, 100, "The highest rank there is.", 16, Palette.Text, ay: 0.5f);
                var waiting = Quests.WaitingOnRank(player);
                var sub = waiting != null
                    ? $"Next story quest ({waiting.Name}) needs Rank {waiting.RequiresRank}."
                    : $"Hunts done: {g.Done}. You may take hunts up to Rank {Guild.RankId(Guild.MaxHuntRank(player))}.";
                Ui.Text(c, 122, 116, sub, 15, waiting != null ? Palette.TextGood : Palette.TextDim);
                var line = clerk.Dialogue != null && clerk.Dialogue.Count > 0 ? clerk.Dialogue[0] : "";
                Ui.Text(c, 1130, 58, $"{clerk.Name}: \"{line}\"", 15, Palette.TextDim, false, 1, 0, 430, align: TextAnchor.UpperRight);
                Ui.Rect(c, 30, 150, 1100, 2, PanelKit.WithAlpha(Palette.PanelBorder, 0.6f));

                // Left: hunts you've taken.
                var mine = Guild.ActiveHunts(player);
                Ui.Text(c, 40, 164, $"Your hunts ({mine.Count}/{G.MaxActive})", 20, bold: true);
                if (mine.Count == 0) Ui.Text(c, 40, 200, "None yet. Take a notice from the board.", 16, Palette.TextDim);
                for (int i = 0; i < mine.Count; i++)
                {
                    var hunt = mine[i].Hunt;
                    var count = mine[i].Count;
                    var ready = mine[i].Ready;
                    var y = 198 + i * 118;
                    Ui.Rect(c, 30, y, 540, 108, Design.Hex(ready ? 0x22361f : 0x241e2e), 10, 2,
                        ready ? Design.Hex(0x7ddc7d) : PanelKit.WithAlpha(Palette.PanelBorder, 0.5f));
                    RankBadge(c, 60, y + 30, hunt.Rank);
                    Ui.Text(c, 92, y + 10, Guild.Title(hunt), 19, bold: true);
                    Ui.Text(c, 92, y + 36, Guild.Where(player, hunt), 14, Palette.TextDim, wrap: 320);
                    // Progress bar.
                    const float pw = 200;
                    Ui.Rect(c, 92, y + 84 - 6, pw, 12, Palette.BarBg, 4);
                    if (count > 0) Ui.Rect(c, 92, y + 84 - 6, Mathf.Max(8, pw * count / hunt.Count), 12, ready ? Design.Hex(0x7ddc7d) : Palette.Xp, 4);
                    Ui.Text(c, 92 + pw + 10, y + 84, ready ? $"{count}/{hunt.Count} Done!" : $"{count}/{hunt.Count}", 15,
                        ready ? Palette.TextGood : Palette.Text, ready, 0, 0.5f);
                    Ui.Text(c, 92, y + 58, RewardText(player, hunt), 14, Palette.TextGold);
                    if (ready)
                    {
                        new UiButton(c, 490, y + 54, "Turn in", () =>
                        {
                            var result = Guild.TurnIn(player, hunt.Key, rng);
                            if (result == null) return;
                            Sfx.Play("quest");
                            message = result.Promotions.Count > 0
                                ? new Message { Text = $"Promoted to Rank {result.Promotions[result.Promotions.Count - 1]}!", Color = Palette.TextGood }
                                : new Message
                                {
                                    Text = $"{Guild.Title(hunt)}: done. +{result.Points} rank point{(result.Points == 1 ? "" : "s")}. New notices are up.",
                                    Color = Palette.TextGood,
                                };
                            confirmDrop = null;
                            onTurnIn?.Invoke(result);
                            Changed();
                        }, 130, 50, 19, color: Design.Hex(0x2f5d2f));
                    }
                    else
                    {
                        var sure = confirmDrop == hunt.Key;
                        new UiButton(c, 490, y + 54, sure ? "Sure?" : "Drop", () =>
                        {
                            if (!sure)
                            {
                                confirmDrop = hunt.Key;
                                message = new Message
                                {
                                    Text = hunt.Kind == "gather" ? "Drop it? Items you gathered stay in your bag." : "Drop it? Its kill count is lost.",
                                    Color = Palette.TextBad,
                                };
                                Render();
                                return;
                            }
                            Guild.DropHunt(player, hunt.Key);
                            confirmDrop = null;
                            message = new Message { Text = $"Dropped: {Guild.Title(hunt)}.", Color = Palette.TextDim };
                            Changed();
                        }, 110, 44, 17, color: sure ? Design.Hex(0x6b2a2a) : Palette.Button);
                    }
                }

                // Right: the board.
                Ui.Text(c, 600, 164, "Notice board", 20, bold: true);
                var full = !Guild.CanTakeHunt(player);
                if (g.Board.Count == 0)
                    Ui.Text(c, 600, 200, "Nothing pinned up right now. Explore further, and come back.", 16, Palette.TextDim, wrap: 520);
                for (int i = 0; i < g.Board.Count; i++)
                {
                    var key = g.Board[i];
                    var hunt = Guild.HuntDef(key);
                    if (hunt == null) continue;
                    var y = 198 + i * 76;
                    var above = hunt.Rank > g.Rank;
                    var rankColor = RankColor(Guild.RankId(hunt.Rank));
                    Ui.Rect(c, 590, y, 540, 68, Design.Hex(0x241e2e), 10, 2, PanelKit.WithAlpha(rankColor, above ? 0.9f : 0.4f));
                    RankBadge(c, 620, y + 34, hunt.Rank, 36);
                    Ui.Text(c, 650, y + 6, Guild.Title(hunt), 18, hunt.Kind == "elite" ? Palette.TextGold : Palette.Text, true);
                    Ui.Text(c, 650, y + 29, Guild.Where(player, hunt), 13, Palette.TextDim, wrap: 340);
                    Ui.Text(c, 650, y + 47, RewardText(player, hunt), 13, Palette.TextGold);
                    new UiButton(c, 1065, y + 34, "Take", () =>
                    {
                        if (!Guild.TakeHunt(player, key)) return;
                        message = new Message { Text = $"Taken: {Guild.Title(hunt)}.", Color = Palette.TextGood };
                        confirmDrop = null;
                        Changed();
                    }, 100, 44, 18, full ? $"{G.MaxActive}/{G.MaxActive}" : null).SetEnabled(!full);
                }

                var help = $"Hunts up to {G.Reach} ranks above yours. Rank points: 1 at your rank, +1 per rank above, none below. Every town's clerk shares this board.";
                Ui.Text(c, 40, 598, message?.Text ?? help, 16, message?.Color ?? Palette.TextDim, message != null, wrap: 1080);
            }

            Render();
            return modal;
        }
    }
}
