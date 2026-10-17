using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>After a fight: Victory with rewards counting up and the drops revealed one by one, or Defeated (ResultScene.ts).</summary>
    public class ResultScreen : GameScreen
    {
        /// <summary>How a drop is revealed: plain, by grade (Fine and up), or by Sigil.</summary>
        static readonly string[] RevealOrder = { "plain", "fine", "superior", "masterwork", "dawn", "dusk", "eclipse" };

        readonly BattleResult data;
        Action go;
        float keysFrom = float.MaxValue;

        public ResultScreen(BattleResult data) => this.data = data;

        /// <summary>"Mossling" -> "the Mossling", "The Thornheart" -> "the Thornheart".</summary>
        static string The(string name) => name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? $"the {name.Substring(4)}" : $"the {name}";

        static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public override void Enter()
        {
            const float cx = Design.Width / 2;
            var player = Session.Player;
            var won = data.Outcome == "victory";

            Backdrop.Rect(World, cx, Design.Height / 2, Design.Width, Design.Height, Design.Hex(won ? 0x1d2a1a : 0x2a1418), -32000);
            // Slow light rays behind the panel.
            var rays = Shapes.Create(World, cx, 170, -31000, "rays");
            var rayColor = Design.Hex(won ? 0xffe9a0 : 0xff6b6b, won ? 0.12f : 0.06f);
            for (int i = 0; i < 12; i++)
            {
                var a = i / 12f * Mathf.PI * 2;
                rays.Tri(rayColor, 0, 0, Mathf.Cos(a - 0.08f) * 900, Mathf.Sin(a - 0.08f) * 900, Mathf.Cos(a + 0.08f) * 900, Mathf.Sin(a + 0.08f) * 900);
            }
            Anim.Float(0, -360, 40, r => { if (rays != null) rays.transform.localRotation = Quaternion.Euler(0, 0, r); }, Ease.Linear, repeat: -1);

            // The panel is drawn in the world so effects (confetti, sparkles) can fly over it; the text sits on top.
            var panel = Shapes.Create(World, cx, Design.Height / 2, -30000, "panel");
            panel.RoundRect(Palette.PanelBorder, -363, -263, 726, 526, 19);
            panel.RoundRect(Palette.Panel, -360, -260, 720, 520, 16);
            panel.Alpha = 0;
            Anim.Float(0, 1, 0.3f, t =>
            {
                if (panel == null) return;
                var s = Mathf.LerpUnclamped(0.9f, 1, t);
                panel.transform.localScale = new Vector3(s, s, 1);
                panel.Alpha = Mathf.Clamp01(t);
            }, Ease.BackOut);

            var title = Ui.Text(Root, cx, 170, won ? "Victory!" : "Defeated", 64, won ? Palette.TextGold : Palette.TextBad, true, 0.5f, 0.5f, stroke: true);
            title.style.opacity = 0;
            title.style.scale = new Scale(Vector3.one * 2);
            Anim.Float(0, 1, 0.38f, t =>
            {
                var s = Mathf.LerpUnclamped(2, 1, t);
                title.style.scale = new Scale(new Vector3(s, s, 1));
                title.style.opacity = Mathf.Clamp01(t);
            }, Ease.BackOut, 0.1f);

            if (won)
            {
                var rewards = data.Rewards;
                Anim.After(0.25f, () => Fx.Confetti(World));
                Sfx.Play("victory");

                // Count-up rows.
                Ui.Text(Root, cx, 240, $"You defeated {The(data.MonsterName)}.", 24, ax: 0.5f, ay: 0.5f);
                var xpText = Ui.Text(Root, cx - 120, 288, "+0 XP", 30, Palette.TextGood, true, 0.5f, 0.5f);
                var goldText = Ui.Text(Root, cx + 120, 288, "+0 gold", 30, Palette.TextGold, true, 0.5f, 0.5f);
                DropReveal(cx, 360, rewards.Items, data.Lost);
                for (int i = 0; i < data.Quests.Count; i++)
                {
                    var q = data.Quests[i];
                    var done = q.Count >= q.Target;
                    var line = Ui.Text(Root, cx, 450 + i * 28, $"{q.Name}: {q.Goal} {q.Count}/{q.Target}{(done ? $"  ✔ Return to {q.TurnIn ?? "the quest giver"}" : "")}",
                        19, done ? Palette.TextGood : Palette.TextGold, done, 0.5f, 0.5f);
                    line.style.opacity = 0;
                    Anim.Float(0, 1, 0.3f, a => line.style.opacity = a, Ease.Linear, 1.1f + i * 0.15f);
                    if (done) Anim.After(1.1f, () => Sfx.Play("quest"));
                }
                var shift = data.Quests.Count * 28;
                CountUp(xpText, rewards.Xp, " XP", 0.4f);
                CountUp(goldText, rewards.Gold, " gold", 0.65f);

                if (data.LevelsGained > 0)
                {
                    var hx = cx - 280;
                    var hy = 490 + shift;
                    HeroPortrait(player, hx, hy, 132);
                    Anim.After(1.2f, () =>
                    {
                        Fx.LevelUpBurst(World, hx, hy);
                        Sfx.Play("levelup");
                        GameRoot.I.Shake(0.2f, 8);
                        var extras = new[] { $"+{data.LevelsGained * Db.B.StatPointsPerLevel} stat points (press C)" }.Concat(data.NewSkills.Select(n => $"New skill: {n}"));
                        Ui.Text(Root, cx + 40, 507 + shift, string.Join("   ·   ", extras), 18, Palette.TextMagic, true, 0.5f, 0.5f);
                        var banner = Ui.Text(Root, cx + 40, 474 + shift, $"LEVEL UP!  Now level {player.Level}", 32, Palette.TextGood, true, 0.5f, 0.5f, stroke: true);
                        banner.style.scale = new Scale(Vector3.zero);
                        void Scale(float s) => banner.style.scale = new Scale(new Vector3(s, s, 1));
                        Anim.Float(0, 1.2f, 0.2f, Scale, Ease.BackOut)
                            .OnComplete(() => Anim.Float(1.2f, 1, 0.15f, Scale)
                                .OnComplete(() => Anim.Float(1, 1.06f, 0.5f, Scale, Ease.Linear, repeat: -1, yoyo: true)));
                    });
                }
                else Ui.Text(Root, cx, 480 + shift, $"XP {JsMath.Str(player.Xp)} / {JsMath.Str(player.XpNeeded)}", 20, Palette.TextDim, ax: 0.5f, ay: 0.5f);

                ContinueButton("Continue", data.Back, 568 + Math.Min(shift, 20));
            }
            else
            {
                GameRoot.I.Shake(0.3f, 8);
                var lines = new List<string> { $"{Capitalize(The(data.MonsterName))} got the better of you.", "", $"You wake up in {Db.Room(data.Wake.RoomId).Name}, fully rested." };
                if (data.Grit is double grit && grit > 0)
                {
                    var max = grit >= Db.B.Grit.Max ? " (max)" : "";
                    lines.Add("");
                    lines.Add($"Grit: +{Math.Round(grit * 100)}% damage against {The(data.MonsterName)} next time{max}.");
                }
                Ui.Text(Root, cx, 330, string.Join("\n", lines), 26, ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter);
                ContinueButton("Wake up", RoomArrival.Spawn(data.Wake.RoomId, (float)data.Wake.X, (float)data.Wake.Y), 520);
            }
        }

        /// <summary>The hero's art as a UI picture (feet at x, y): the outfit with the weapon is the world figure's job, so this shows the body.</summary>
        void HeroPortrait(Player player, float x, float y, float height)
        {
            var look = player.Look;
            var key = look.Bare?.Select(b => b.Art).FirstOrDefault(Art.Has) ?? Art.Pick(look.Art);
            if (key == null) return;
            var size = Art.Size(Art.Get(key));
            var w = size.y > 0 ? height * size.x / size.y : height;
            Ui.Image(Root, x, y, w, height, key, 0.5f, 1);
        }

        class Card
        {
            public ItemDef Def;
            public ItemGrant G;
            public int Qty;
            public bool IsLost;
            public ItemUi.GradeData Info;
            public string Tier;
        }

        /// <summary>
        /// The drops as cards (icon and name in its grade or Sigil color) that pop in one by one, best last.
        /// Plain drops just pop; Superior and Masterwork glow first; Sigils get a long charge-up and their fanfare
        /// (timings in `BALANCE.dropReveal`). Loot that didn't fit the bag is greyed out.
        /// </summary>
        void DropReveal(float cx, float y, List<ItemGrant> items, List<ItemGrant> lost)
        {
            var r = Db.B.DropReveal;
            float Ms(string key) => (float)(r.TryGetValue(key, out var v) ? v : 0) / 1000f;
            // Gear one card each; stacking items (potions, materials) as one card "x2".
            var shown = new List<Card>();
            foreach (var g in items)
            {
                var isLost = lost.Contains(g);
                var def = Db.Item(g.ItemId);
                var same = def.IsGear ? null : shown.Find(e => e.G.ItemId == g.ItemId && e.IsLost == isLost);
                if (same != null) same.Qty++;
                else shown.Add(new Card { G = g, Qty = 1, IsLost = isLost, Def = def });
            }
            if (shown.Count == 0)
            {
                var none = Ui.Text(Root, cx, y, "No loot this time", 22, Palette.TextDim, ax: 0.5f, ay: 0.5f);
                none.style.opacity = 0;
                Anim.Float(0, 1, 0.3f, a => none.style.opacity = a, Ease.Linear, Ms("start"));
                return;
            }
            foreach (var c in shown)
            {
                c.Info = ItemUi.GradeInfo(c.Def, c.G.Traits());
                c.Tier = c.IsLost ? "plain" : c.Info.Sigil ?? (c.Def.IsGear && c.Info.Tier != "standard" && c.Info.Tier != "fair" ? c.Info.Tier : "plain");
            }
            var cards = shown.OrderBy(c => Array.IndexOf(RevealOrder, c.Tier)).ToList();

            const float step = 128;
            var scale = Mathf.Min(1, 680 / (cards.Count * step));
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, cx, y);
            row.style.scale = new Scale(new Vector3(scale, scale, 1));
            row.style.transformOrigin = new TransformOrigin(0, 0);
            Root.Add(row);
            var at = Ms("start");
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                var x = (i - (cards.Count - 1) / 2f) * step;
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(card, x, 0);
                card.style.scale = new Scale(Vector3.zero);
                row.Add(card);
                Ui.Rect(card, -32, -42, 64, 64, new Color(0, 0, 0, 0.35f), 8, 3, c.IsLost ? Design.Hex(0x555555) : c.Info.Frame);
                var icon = ItemUi.DrawItemIcon(card, 0, -10, 52, c.Def);
                var name = $"{ItemUi.ItemName(c.Def, c.G.Traits())}{(c.Qty > 1 ? $" x{c.Qty}" : "")}";
                var labelColor = c.IsLost ? Palette.TextDim : c.Info.Sigil != null ? c.Info.NameColor : c.Info.Tier == "standard" && c.Def.IsGear ? Palette.Text : c.Info.TextColor;
                var label = Ui.Text(card, 0, 34, name, 15, labelColor, c.Tier != "plain", 0.5f, 0, 124, align: TextAnchor.UpperCenter);
                // "Fine 52%" / "Sigil of Dusk" under gear names.
                if (c.Def.IsGear)
                {
                    var sub = c.Info.Sigil != null ? Items.SigilNames[c.Info.Sigil] : ItemUi.GradeLabel(c.Info);
                    var subLabel = Ui.Text(card, 0, 54, sub, 13, Palette.TextDim, ax: 0.5f);
                    label.RegisterCallback<GeometryChangedEvent>(_ => subLabel.style.top = 36 + label.layout.height);
                }
                if (c.IsLost) Ui.Text(card, 0, -10, "Bag full", 13, Palette.TextBad, true, 0.5f, 0.5f);
                if (c.Info.Sigil != null && !c.IsLost) ItemUi.AnimateSigilIcon(icon, c.Info.Sigil, 52);

                // Where the card sits on screen (for effects in the world).
                var screenX = cx + x * scale;
                var screenY = y - 10 * scale;
                var windup = Ms(c.Tier);
                var pop = at + windup;
                if (windup > 0) Anim.After(at, () => WindUp(screenX, screenY, c.Tier, windup));
                Anim.After(pop, () => PopCard(card, screenX, screenY, c.Tier, c.Info.Frame, label));
                at = pop + Ms("gap");
            }
        }

        static bool IsSigil(string tier) => tier == "dawn" || tier == "dusk" || tier == "eclipse";

        /// <summary>The build-up before a good drop: a glow (Superior, Masterwork) or a shaking charge in the Sigil's color.</summary>
        void WindUp(float x, float y, string tier, float seconds)
        {
            var sigil = IsSigil(tier);
            var color = sigil ? ItemUi.SigilColors[tier].Color : ItemUi.GradeColors[tier == "masterwork" ? "masterwork" : "superior"].Color;
            Sfx.Play("charge");
            _ = Fx.ChargeUp(World, x, y, ToHex(color), seconds * 1000);
            if (!sigil) return;
            // Heavier tiers rumble harder as they charge.
            GameRoot.I.Shake(seconds, (tier == "eclipse" ? 0.006f : tier == "dusk" ? 0.004f : 0.002f) * Design.Width);
            if (seconds > 0.7f) Anim.After(seconds / 2, () => Sfx.Play("charge"));
        }

        static long ToHex(Color c) => ((long)Mathf.RoundToInt(c.r * 255) << 16) | ((long)Mathf.RoundToInt(c.g * 255) << 8) | (long)Mathf.RoundToInt(c.b * 255);

        void PopCard(VisualElement card, float x, float y, string tier, Color color, Label label)
        {
            var big = tier != "plain" && tier != "fine";
            var peak = big ? 1.3f : 1.12f;
            void Scale(float s) => card.style.scale = new Scale(new Vector3(s, s, 1));
            Anim.Float(0, peak, big ? 0.2f : 0.14f, Scale, Ease.BackOut).OnComplete(() => Anim.Float(peak, 1, 0.14f, Scale));
            if (tier == "plain")
            {
                Sfx.Play("click");
                return;
            }
            if (tier == "fine" || tier == "superior" || tier == "masterwork")
            {
                Sfx.Play(tier == "fine" ? "coin" : "rare");
                var master = tier == "masterwork";
                Fx.Burst(World, x, y, "fx_star", master ? 36 : tier == "superior" ? 20 : 10, 60, master ? 320 : 200, master ? 1 : 0.7f, master ? 1000 : 700, ToHex(color), true);
                if (master) GameRoot.I.Flash(0.25f, new Color(1, 230 / 255f, 140 / 255f));
                return;
            }
            SigilFanfare(label, x, y + 44, tier);
        }

        /// <summary>Burst in the Sigil's color over its name; the best tier also gets a sound, a flash and a banner.</summary>
        void SigilFanfare(Label text, float x, float y, string tier)
        {
            var eclipse = tier == "eclipse";
            Sfx.Play(eclipse ? "eclipse" : "sigil");
            var colors = ItemUi.SigilColors[tier];
            var color = ToHex(colors.Color);
            var big = tier != "dawn";
            Fx.Shockwave(World, x, y, color, big ? 2.4f : 1.6f, 0.35f);
            Fx.Burst(World, x, y, "fx_star", eclipse ? 60 : big ? 36 : 24, 80, eclipse ? 420 : 280, big ? 1.1f : 0.8f, eclipse ? 1400 : 1000, color, true);
            Anim.Float(1, 0.35f, 0.14f, a => text.style.opacity = a, Ease.Linear, repeat: 3, yoyo: true);
            GameRoot.I.Flash(eclipse ? 0.6f : 0.3f, colors.Color);
            if (eclipse) GameRoot.I.Shake(0.4f, 10);
            var banner = Ui.Text(Root, Design.Width / 2, 62, $"{Items.SigilNames[tier].ToUpperInvariant()}!", eclipse ? 46 : 36, colors.TextColor, true, 0.5f, 0.5f, stroke: true);
            banner.style.scale = new Scale(Vector3.zero);
            void Scale(float s) => banner.style.scale = new Scale(new Vector3(s, s, 1));
            Anim.Float(0, 1.25f, 0.22f, Scale, Ease.BackOut)
                .OnComplete(() => Anim.Float(1.25f, 1, 0.16f, Scale)
                    .OnComplete(() => Anim.Float(1, 0, 0.5f, a => banner.style.opacity = a, Ease.Linear, eclipse ? 2.2f : 1.6f)));
        }

        static void CountUp(Label text, double to, string suffix, float delay)
        {
            Anim.Float(0, (float)to, 0.5f, v => text.text = $"+{Math.Round(v)}{suffix}", Ease.CubicOut, delay).OnComplete(() =>
            {
                text.text = $"+{JsMath.Str(to)}{suffix}";
                if (to > 0) Sfx.Play("coin");
                Anim.Float(1, 1.25f, 0.09f, s => text.style.scale = new Scale(new Vector3(s, s, 1)), Ease.Linear, yoyo: true);
            });
        }

        void ContinueButton(string label, RoomArrival back, float y)
        {
            var gone = false;
            go = () =>
            {
                if (gone) return;
                gone = true;
                GameRoot.I.Go(new RoomScreen(back));
            };
            var btn = new UiButton(Root, Design.Width / 2, y, label, go, 280, 64);
            btn.style.opacity = 0;
            Anim.Float(0, 1, 0.3f, a => btn.style.opacity = a, Ease.Linear, 0.7f);
            keysFrom = Time.time + 0.7f;
        }

        public override void Tick(float dt)
        {
            if (go == null || Time.time < keysFrom || !InputEnabled) return;
            if (Keys.Pressed(Key.Space) || Keys.Pressed(Key.Enter) || Keys.Pressed(Key.E)) go();
        }
    }
}
