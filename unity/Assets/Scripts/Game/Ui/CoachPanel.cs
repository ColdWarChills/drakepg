using System;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// A town's driver (role coach, Travel.CoachStops): a card per other town with its picture, area, distance and fare,
    /// nearest first, three to a row. Ride pays the fare and hands the landing spot to the room, which takes you there.
    /// Towns you haven't walked into yet are greyed out.
    /// </summary>
    public static class CoachPanel
    {
        const float W = 1060, CardW = 310, CardH = 318, PicH = 174, Gap = 30, CardsY = 116, PerRow = 3;

        public static UiModal Open(Player player, NpcDef driver, string hereRoomId, Action<Location> ride)
        {
            var stops = Travel.CoachStops(player, hereRoomId);
            var rows = Math.Max(1, (int)Math.Ceiling(stops.Count / PerRow));
            var h = CardsY + rows * (CardH + Gap) + 44;
            var modal = new UiModal(W, h, "Travel");
            var c = modal.Content;

            var line = driver.Dialogue != null && driver.Dialogue.Count > 0 ? driver.Dialogue[0] : "";
            Ui.Text(c, 40, 60, $"{driver.Name}, {driver.Title.ToLowerInvariant()}: \"{line}\"", 16, Palette.TextDim, wrap: 760);
            Ui.Text(c, W - 40, 56, $"{PanelKit.Num(player.Gold)} g", 22, Palette.TextGold, true, ax: 1);

            for (int i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                var inRow = (int)Math.Min(PerRow, stops.Count - i / (int)PerRow * PerRow);
                var x0 = (W - inRow * CardW - (inRow - 1) * Gap) / 2;
                var x = x0 + i % (int)PerRow * (CardW + Gap);
                var y = CardsY + i / (int)PerRow * (CardH + Gap);
                Card(c, x, y, player, hereRoomId, stop, () =>
                {
                    var dest = Travel.Ride(player, hereRoomId, stop.Town.Id);
                    if (dest == null) return;
                    Sfx.Play("coin");
                    modal.Close(() => ride(dest));
                });
            }
            if (stops.Count == 0) Ui.Text(c, W / 2, CardsY + 60, "There is no other town to ride to yet.", 18, Palette.TextDim, ax: 0.5f);
            Ui.Text(c, W / 2, h - 34, "One way: you arrive in the town square. The fare grows with the distance.", 15, Palette.TextDim, ax: 0.5f, ay: 0.5f);
            return modal;
        }

        /// <summary>A town: its picture (greyed until reached), name, area and distance, the fare and Ride.</summary>
        static void Card(VisualElement c, float x, float y, Player player, string hereRoomId, Travel.CoachStop stop, Action ride)
        {
            var reached = player.Data.Visited.Contains(stop.Town.Id);
            var card = Ui.Rect(c, x, y, CardW, CardH, Design.Hex(0x241e2e), 10, 2, PanelKit.WithAlpha(Palette.PanelBorder, 0.45f));
            var pic = Ui.Rect(card, 10, 10, CardW - 20, PicH, Design.Hex(0x15121b), 6);
            var bg = Art.Get(Art.Pick(stop.Town.Background));
            if (bg != null)
            {
                pic.style.backgroundImage = new StyleBackground(bg);
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                if (!reached) pic.style.unityBackgroundImageTintColor = new Color(0.32f, 0.32f, 0.34f);
            }

            Ui.Text(card, 18, PicH + 22, stop.Town.Name, 22, Palette.Text, true);
            var area = Db.Data.MapAreas.FirstOrDefault(a => a.Id == stop.Town.Map.Area)?.Name ?? "";
            var rooms = Travel.RoomsBetween(hereRoomId, stop.Town.Id);
            Ui.Text(card, 18, PicH + 54, $"{area} · {rooms} room{(rooms == 1 ? "" : "s")} away", 14, Palette.TextDim, wrap: CardW - 36);

            var bottom = CardH - 38;
            Ui.Text(card, 18, bottom, $"{PanelKit.Num(stop.Fare)} g", 22, player.Gold >= stop.Fare ? Palette.TextGold : Palette.TextBad, true, ay: 0.5f);
            if (stop.Reason != null) Ui.Text(card, 18, PicH + 76, stop.Reason, 14, reached ? Palette.TextBad : Palette.TextDim);
            new UiButton(card, CardW - 76, bottom, "Ride", ride, 120, 48, 20, null, stop.Reason == null ? Design.Hex(0x2f5d2f) : Palette.Button)
                .SetEnabled(stop.Reason == null);
        }
    }
}
