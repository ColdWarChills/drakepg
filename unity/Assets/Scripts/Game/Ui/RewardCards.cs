using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The level-up page: the level, what grew (before -> after), new skills, and the stat points to place right here
    /// (buttons, or keys 1-4); what's left over waits in the Character panel. One page for a fight's result and a
    /// hand-in's cards. Drawn in screen coordinates, as on the result screen's 720 x 520 sheet; the caller adds the
    /// fanfare and the Continue button.
    /// </summary>
    public static class LevelUpPage
    {
        /// <summary>Where the hero's feet stand (the level burst goes here).</summary>
        public const float HeroX = Design.Width / 2 - 235, HeroY = 428;

        static readonly Dictionary<string, string> StatHelp = new Dictionary<string, string>
        {
            [Stat.Str] = "Melee damage", [Stat.Int] = "Magic, max MP", [Stat.Dex] = "Pierce, hit, crit", [Stat.End] = "Max HP",
        };

        /// <summary>Draws the page into `root`; returns its "Level Up!" title (for the intro).</summary>
        public static Label Draw(VisualElement root, Player player, HeroBefore before, List<string> newSkills, Action<string> placePoint)
        {
            const float cx = Design.Width / 2;
            var title = Ui.Text(root, cx, 150, "Level Up!", 56, Palette.TextGold, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(title);
            Ui.Text(root, cx, 204, $"Level {before.Level}  →  {player.Level}", 26, bold: true, ax: 0.5f, ay: 0.5f);
            HeroPortrait(root, player, HeroX, HeroY, 190);

            // What grew. Stat points placed here show up at once.
            var rows = new List<(string label, double from, double to)> { ("Max HP", before.MaxHp, player.MaxHp), ("Max MP", before.MaxMp, player.MaxMp) };
            var stats = player.Stats;
            foreach (var k in Stat.Keys)
                if (stats[k] != before.Stats[k]) rows.Add((Player.StatLabels[k], before.Stats[k], stats[k]));
            const float x0 = cx - 100;
            var y = 244f;
            foreach (var (label, from, to) in rows)
            {
                Ui.Text(root, x0, y, label, 20, ay: 0.5f);
                Ui.Text(root, x0 + 170, y, JsMath.Str(from), 20, Palette.TextDim, false, 1, 0.5f);
                Ui.Text(root, x0 + 196, y, "→", 20, Palette.TextDim, false, 0.5f, 0.5f);
                Ui.Text(root, x0 + 222, y, JsMath.Str(to), 20, Palette.TextGood, true, 0, 0.5f);
                y += 28;
            }
            Ui.Text(root, x0, y, "HP and MP restored", 18, Palette.TextGood, false, 0, 0.5f);
            y += 28;
            foreach (var skill in newSkills)
            {
                Ui.Text(root, x0, y, $"New skill: {skill}", 20, Palette.TextMagic, true, 0, 0.5f);
                y += 28;
            }
            if (Hud.NextUnlock(player) is string next) Ui.Text(root, x0, y, $"Coming up  ·  {next}", 16, Palette.TextDim, false, 0, 0.5f);

            // The stat points, placed right here.
            var points = player.Data.StatPoints;
            Ui.Text(root, cx, 448, points > 0 ? $"{points} stat point{(points > 1 ? "s" : "")} to place" : "All stat points placed", 19,
                points > 0 ? Palette.TextGold : Palette.TextDim, points > 0, 0.5f, 0.5f);
            for (int i = 0; i < Stat.Keys.Length; i++)
            {
                var k = Stat.Keys[i];
                var x = cx + (i - 1.5f) * 158;
                var text = $"{Player.StatLabels[k]}  {JsMath.Str(stats[k])}";
                if (points > 0) new UiButton(root, x, 488, $"{text}   +", () => placePoint(k), 146, 44, 19);
                else Ui.Text(root, x, 488, text, 20, bold: true, ax: 0.5f, ay: 0.5f);
                Ui.Text(root, x, 529, StatHelp[k], 13, Palette.TextDim, false, 0.5f, 0.5f);
            }
            return title;
        }

        /// <summary>The hero's art as a UI picture (feet at x, y): the outfit with the weapon is the world figure's job, so this shows the body.</summary>
        static void HeroPortrait(VisualElement root, Player player, float x, float y, float height)
        {
            var look = player.Look;
            var key = look.Bare?.Select(b => b.Art).FirstOrDefault(Art.Has) ?? Art.Pick(look.Art);
            if (key == null) return;
            var size = Art.Size(Art.Get(key));
            var w = size.y > 0 ? height * size.x / size.y : height;
            Ui.Image(root, x, y, w, height, key, 0.5f, 1);
        }

        /// <summary>The title's entrance: from twice its size, fading in.</summary>
        public static void TitleIntro(Label title)
        {
            title.style.scale = new Scale(Vector3.one * 2);
            title.style.opacity = 0;
            Anim.Float(0, 1, 0.38f, t =>
            {
                var s = Mathf.LerpUnclamped(2, 1, t);
                title.style.scale = new Scale(new Vector3(s, s, 1));
                title.style.opacity = Mathf.Clamp01(t);
            }, Ease.BackOut);
        }
    }

    /// <summary>What a hand-in paid, for its reward cards (a Guild hunt now; a story quest can fill the same).</summary>
    public class RewardRun
    {
        /// <summary>The first card's heading: the hunt's (or quest's) name.</summary>
        public string Title;
        public double Xp, Gold;
        /// <summary>Guild rank points from this hand-in (null: none to show, e.g. a story quest or the top rank).</summary>
        public RankGain Rank;
        /// <summary>One card each, in order.</summary>
        public List<Guild.PromotionPay> Promotions = new List<Guild.PromotionPay>();
        public int LevelsGained;
        /// <summary>The hero before the pay landed (the level page shows before -> after).</summary>
        public HeroBefore Before;
        public List<string> NewSkills = new List<string>();
    }

    public class RankGain
    {
        /// <summary>The rank and its points before the hand-in.</summary>
        public int Rank, PointsBefore;
        public int Points;
    }

    /// <summary>
    /// The cards after a hand-in, over the dimmed screen, one at a time: what it paid (with the coins), each Guild
    /// promotion (with confetti), then the fight's level page (with its burst). Each has one button; Space, Enter or E
    /// press it, Esc too. Hand-ins made while cards are up wait their turn. One shade stays behind every card of the
    /// run, so nothing flashes between them; effects fly on a layer over the paper and under the text.
    /// </summary>
    public static class RewardCards
    {
        class Job
        {
            public RewardRun Run;
            public Action Save, Done;
        }

        class Card
        {
            public UiModal Modal;
            public Action Press;
            public Action<string> Place;
            public float KeysFrom;
        }

        static readonly Queue<Job> waiting = new Queue<Job>();
        static Card card;
        static VisualElement shade;
        static GameScreen owner;

        /// <summary>Whether a run's cards are up on this screen.</summary>
        public static bool Active => owner != null && owner == GameRoot.I.Screen && shade?.panel != null;

        /// <summary>Show a run's cards (after any run still showing). `save` after a stat point is placed; `done` when its last card closes.</summary>
        public static void Show(RewardRun run, Action save, Action done)
        {
            if (!Active)
            {
                waiting.Clear();
                card = null;
                shade = null;
            }
            waiting.Enqueue(new Job { Run = run, Save = save, Done = done });
            if (card == null) Next();
        }

        /// <summary>The card's button, as the button and the keys press it.</summary>
        public static void Press()
        {
            if (Active) card?.Press?.Invoke();
        }

        /// <summary>
        /// Keys while a card is on top: Space, Enter and E press its button (after a moment, so a mashed key can't skip a
        /// card unseen); on the level page 1-4 place a stat point. True when the card took the key press.
        /// </summary>
        public static bool HandleKeys()
        {
            if (!Active || card == null || UiModal.Top != card.Modal) return false;
            if (card.Place != null)
                for (int i = 0; i < 4; i++)
                    if (Controls.Pressed("act" + (i + 1)))
                    {
                        card.Place(Stat.Keys[i]);
                        return true;
                    }
            if (Keys.Pressed(Key.Space) || Keys.Pressed(Key.Enter) || Controls.Pressed("talk"))
            {
                if (Time.time >= card.KeysFrom) card.Press?.Invoke();
                return true;
            }
            // Nothing else (the bag, the map) opens over a card.
            return true;
        }

        static void Next()
        {
            card = null;
            if (waiting.Count == 0)
            {
                shade?.RemoveFromHierarchy();
                shade = null;
                owner = null;
                return;
            }
            var job = waiting.Peek();
            var screen = GameRoot.I.Screen;
            if (shade == null || shade.panel == null)
            {
                owner = screen;
                shade = Ui.Rect(screen.Root, 0, 0, Design.Width, Design.Height, new Color(0.11f, 0.08f, 0.05f, 0.62f), blocksClicks: true);
                shade.style.opacity = 0;
                Anim.Float(0, 1, 0.15f, v => shade.style.opacity = v, Ease.Linear);
            }
            var steps = new List<Action<Action>>();
            var run = job.Run;
            steps.Add(next => PayCard(run, next, run.Promotions.Count > 0 ? "Next" : run.LevelsGained > 0 ? "Level up!" : "Continue"));
            for (int i = 0; i < run.Promotions.Count; i++)
            {
                var p = run.Promotions[i];
                var last = i == run.Promotions.Count - 1;
                steps.Add(next => PromotionCard(p, next, !last ? "Next" : run.LevelsGained > 0 ? "Level up!" : "Continue"));
            }
            if (run.LevelsGained > 0 && run.Before != null) steps.Add(next => LevelCard(run, job.Save, next));
            void Step(int i)
            {
                if (i >= steps.Count)
                {
                    waiting.Dequeue();
                    job.Done?.Invoke();
                    Next();
                    return;
                }
                steps[i](() => Step(i + 1));
            }
            Step(0);
        }

        /// <summary>A card: a modal on the shared shade, its own shade clear; Esc does what its button does.</summary>
        static Card Open(float w, float h, string title, Action next, string button, float buttonY)
        {
            var c = new Card();
            var gone = false;
            // Once, whether the button, a key or Esc (which closes the modal first) got here.
            void Go()
            {
                if (gone) return;
                gone = true;
                if (!c.Modal.Closed) c.Modal.Close();
                next();
            }
            c.Modal = new UiModal(w, h, title, Go, closeButton: false);
            c.Modal.Root.style.backgroundColor = Color.clear;
            c.Press = Go;
            c.KeysFrom = Time.time + 0.45f;
            new UiButton(c.Modal.Content, w / 2, buttonY, button, Go, 280, 56);
            card = c;
            return c;
        }

        /// <summary>The effects layer: the whole screen, over the card's paper and under its text.</summary>
        static VisualElement FxLayer(UiModal modal)
        {
            var left = (Design.Width - modal.Width) / 2;
            var top = (Design.Height - modal.Height) / 2;
            var layer = new VisualElement { pickingMode = PickingMode.Ignore, name = "card fx" };
            Ui.Place(layer, -left, -top);
            Ui.Size(layer, Design.Width, Design.Height);
            layer.AddToClassList(FeltPass.Keep);
            modal.Panel.Insert(1, layer);
            return layer;
        }

        // --- The pay ---

        static void PayCard(RewardRun run, Action next, string button)
        {
            const float w = 620;
            var rank = run.Rank;
            var h = rank != null ? 380f : 290f;
            var c = Open(w, h, run.Title, next, button, h - 54);
            var box = c.Modal.Content;
            var fx = FxLayer(c.Modal);
            var left = (Design.Width - w) / 2;
            var top = (Design.Height - h) / 2;

            var xp = Ui.Text(box, w / 2 - 130, 116, "+0 XP", 34, Palette.TextGood, true, 0.5f, 0.5f);
            // The gold behind a coin; the coins pop out of the card's middle and drop into it (not onto the number).
            const float coinX = w / 2 + 44, coinY = 118;
            var coin = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(coin, coinX - 19, coinY - 19);
            Ui.Size(coin, 38, 38);
            coin.style.backgroundImage = new StyleBackground(Fx.Get(Fx.Tex.Coin));
            coin.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            box.Add(coin);
            var gold = Ui.Text(box, coinX + 28, 116, "+0 gold", 34, Palette.TextGold, true, 0, 0.5f);
            CountUp(xp, run.Xp, " XP", 0.25f);
            CountUp(gold, run.Gold, " gold", 0.5f);
            if (run.Gold > 0)
                Anim.After(0.2f, () => Coins(fx, new Vector2(left + w / 2, top + 160), new Vector2(left + coinX, top + coinY), (int)Math.Ceiling(run.Gold / 6),
                    () => Anim.Float(1, 1.2f, 0.06f, s => coin.style.scale = new Scale(new Vector3(s, s, 1)), Ease.Linear, yoyo: true)));

            if (rank == null) return;
            // Rank points: your rank's badge, the bar to the next rank, the next rank's badge.
            var toNext = Guild.PointsToNext(rank.Rank);
            const float barW = 300;
            var bx = w / 2 - barW / 2;
            GuildPanel.RankBadge(box, bx - 46, 236, rank.Rank, 52);
            if (rank.Points <= 0 || toNext <= 0)
            {
                Ui.Text(box, bx, 236, rank.Points > 0 ? $"+{rank.Points} rank point{(rank.Points == 1 ? "" : "s")}" : "No rank points", 24,
                    rank.Points > 0 ? Palette.TextGood : Palette.TextDim, true, 0, 0.5f);
                return;
            }
            Ui.Text(box, w / 2, 190, $"+{rank.Points} rank point{(rank.Points == 1 ? "" : "s")}", 24, Palette.TextGood, true, 0.5f, 0.5f);
            var nextBadge = GuildPanel.RankBadge(box, bx + barW + 46, 236, rank.Rank + 1, 40);
            var after = Math.Min(rank.PointsBefore + rank.Points, toNext);
            var bar = new UiBar(box, bx, 236, barW, 26, GuildPanel.RankColors.TryGetValue(Guild.RankId(rank.Rank + 1), out var rc) ? rc : Palette.Xp);
            bar.SetValue(rank.PointsBefore, toNext, false);
            Anim.After(0.6f, () => bar.SetValue(after, toNext));
            if (after >= toNext)
            {
                // Filled to the brim: the next rank's badge pops.
                nextBadge.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                Anim.After(1.05f, () =>
                {
                    Sfx.Play("coin");
                    Anim.Float(1, 1.35f, 0.12f, s => nextBadge.style.scale = new Scale(new Vector3(s, s, 1)), Ease.Linear, yoyo: true);
                    Stars(fx, left + bx + barW + 46, top + 236, 10, 160, new[] { Design.Hex(0xffd75e), Color.white });
                });
            }
        }

        static void CountUp(Label text, double to, string suffix, float delay)
        {
            text.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            Anim.Float(0, (float)to, 0.5f, v => text.text = $"+{Math.Round(v)}{suffix}", Ease.CubicOut, delay).OnComplete(() =>
            {
                text.text = $"+{JsMath.Str(to)}{suffix}";
                Anim.Float(1, 1.2f, 0.09f, s => text.style.scale = new Scale(new Vector3(s, s, 1)), Ease.Linear, yoyo: true);
            });
        }

        // --- A promotion ---

        static void PromotionCard(Guild.PromotionPay p, Action next, string button)
        {
            const float w = 640;
            var gifts = p.Items.Count > 0 || p.Gold > 0;
            var h = gifts ? 500f : 360f;
            var c = Open(w, h, "Guild promotion", next, button, h - 54);
            var box = c.Modal.Content;
            var fx = FxLayer(c.Modal);
            var left = (Design.Width - w) / 2;
            var top = (Design.Height - h) / 2;
            var rank = Guild.RankIndex(p.Rank);

            var badge = GuildPanel.RankBadge(box, w / 2, 148, rank, 112);
            badge.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            badge.style.scale = new Scale(Vector3.zero);
            Anim.Float(0, 1, 0.35f, s => badge.style.scale = new Scale(new Vector3(s, s, 1)), Ease.BackOut, 0.1f);
            var name = Ui.Text(box, w / 2, 244, $"Rank {p.Rank}", 38, Palette.TextGold, false, 0.5f, 0.5f);
            Fonts.Title(name);
            Sfx.Play("levelup");
            Anim.After(0.15f, () => Confetti(fx));
            Anim.After(0.3f, () => Stars(fx, left + w / 2, top + 148, 18, 240, new[] { GuildPanel.RankColors.TryGetValue(p.Rank, out var rc) ? rc : Color.white, Design.Hex(0xffd75e) }));
            if (!gifts) return;

            // The gifts: the gold, then each item (icon, name in its grade color), stacking ones as "x2".
            var shown = new List<(ItemGrant g, int qty)>();
            foreach (var g in p.Items)
            {
                var def = Db.Item(g.ItemId);
                var same = def.IsGear ? -1 : shown.FindIndex(e => e.g.ItemId == g.ItemId);
                if (same >= 0) shown[same] = (shown[same].g, shown[same].qty + 1);
                else shown.Add((g, 1));
            }
            var slots = shown.Count + (p.Gold > 0 ? 1 : 0);
            const float step = 150;
            var y = 340f;
            for (int i = 0; i < slots; i++)
            {
                var x = w / 2 + (i - (slots - 1) / 2f) * step;
                var gift = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(gift, x, y);
                box.Add(gift);
                gift.style.opacity = 0;
                Anim.Float(0, 1, 0.25f, a => gift.style.opacity = a, Ease.Linear, 0.45f + i * 0.15f);
                if (p.Gold > 0 && i == 0)
                {
                    Ui.Rect(gift, -32, -42, 64, 64, new Color(0, 0, 0, 0.35f), 8, 3, Design.Hex(0xc9a227));
                    var coin = new VisualElement { pickingMode = PickingMode.Ignore };
                    Ui.Place(coin, -22, -32);
                    Ui.Size(coin, 44, 44);
                    coin.style.backgroundImage = new StyleBackground(Fx.Get(Fx.Tex.Coin));
                    gift.Add(coin);
                    Ui.Text(gift, 0, 34, $"+{JsMath.Str(p.Gold)} gold", 17, Palette.TextGold, true, 0.5f, 0, 140, align: TextAnchor.UpperCenter);
                    continue;
                }
                var (grant, qty) = shown[i - (p.Gold > 0 ? 1 : 0)];
                var itemDef = Db.Item(grant.ItemId);
                var info = ItemUi.GradeInfo(itemDef, grant.Traits());
                Ui.Rect(gift, -32, -42, 64, 64, new Color(0, 0, 0, 0.35f), 8, 3, info.Frame);
                var icon = ItemUi.DrawItemIcon(gift, 0, -10, 52, itemDef);
                if (info.Sigil != null) ItemUi.AnimateSigilIcon(icon, info.Sigil, 52);
                var color = info.Sigil != null ? info.NameColor : info.Tier == "standard" && itemDef.IsGear ? Palette.Text : info.TextColor;
                var label = Ui.Text(gift, 0, 34, $"{ItemUi.ItemName(itemDef, grant.Traits())}{(qty > 1 ? $" x{qty}" : "")}", 17, color, true, 0.5f, 0, 140,
                    align: TextAnchor.UpperCenter);
                if (itemDef.IsGear)
                {
                    var sub = Ui.Text(gift, 0, 56, info.Sigil != null ? Items.SigilNames[info.Sigil] : ItemUi.GradeLabel(info), 14, Palette.TextDim, ax: 0.5f);
                    label.RegisterCallback<GeometryChangedEvent>(_ => sub.style.top = 36 + label.layout.height);
                }
            }
        }

        // --- The level page ---

        static void LevelCard(RewardRun run, Action save, Action next)
        {
            // The result screen's sheet: 720 x 520, so the page's screen coordinates land as they do after a fight.
            var c = Open(720, 520, "", next, "Continue", 478);
            var modal = c.Modal;
            var page = new VisualElement { pickingMode = PickingMode.Ignore, name = "level page" };
            Ui.Place(page, -(Design.Width - 720) / 2, -(Design.Height - 520) / 2);
            Ui.Size(page, Design.Width, Design.Height);
            modal.Content.Insert(0, page);
            var fx = FxLayer(modal);
            var player = Session.Player;

            void Draw(bool intro)
            {
                page.Clear();
                var title = LevelUpPage.Draw(page, player, run.Before, run.NewSkills, Place);
                if (!intro) return;
                LevelUpPage.TitleIntro(title);
                LevelBurst(fx, LevelUpPage.HeroX, LevelUpPage.HeroY);
                Confetti(fx);
                Sfx.Play("levelup");
            }
            void Place(string stat)
            {
                if (modal.Closed || !player.SpendStatPoint(stat)) return;
                save?.Invoke();
                Sfx.Play("equip");
                Draw(false);
            }
            c.Place = Place;
            Draw(true);
        }

        // --- Effects (fx.ts's coin shower, confetti and level burst, as UI pieces so they show over the board) ---

        /// <summary>An effect texture at `scale` times its own size (as Fx draws it: its texels / 2), so a confetti leaf stays a thin oval.</summary>
        static VisualElement Bit(VisualElement layer, Fx.Tex tex, float scale, Color tint)
        {
            var sprite = Fx.Get(tex);
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            Ui.Size(e, sprite.rect.width / 2 * scale, sprite.rect.height / 2 * scale);
            e.style.backgroundImage = new StyleBackground(sprite);
            e.style.unityBackgroundImageTintColor = tint;
            e.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            layer.Add(e);
            return e;
        }

        /// <summary>Center an effect piece on (x, y).</summary>
        static void Put(VisualElement e, float x, float y)
        {
            e.style.left = x - e.style.width.value.value / 2;
            e.style.top = y - e.style.height.value.value / 2;
        }

        /// <summary>Coins pop out of `from`, then fly into `to`, a coin sound and a twinkle as each lands.</summary>
        static void Coins(VisualElement layer, Vector2 from, Vector2 to, int count, Action onLand = null)
        {
            if (layer.panel == null) return;
            var n = Mathf.Clamp(count, 1, 14);
            for (int i = 0; i < n; i++)
            {
                var coin = Bit(layer, Fx.Tex.Coin, 1, Color.white);
                Put(coin, from.x, from.y);
                coin.style.scale = new Scale(Vector3.zero);
                float popX = from.x + UnityEngine.Random.Range(-110f, 110f), popY = from.y + UnityEngine.Random.Range(-70f, -10f);
                Anim.Float(0, 1, 0.26f, t =>
                {
                    Put(coin, Mathf.LerpUnclamped(from.x, popX, t), Mathf.LerpUnclamped(from.y, popY, t));
                    var s = Mathf.LerpUnclamped(0, 1.3f, t);
                    coin.style.scale = new Scale(new Vector3(s, s, 1));
                }, Ease.BackOut);
                Anim.Float(0, 1, 0.48f, t =>
                {
                    Put(coin, Mathf.LerpUnclamped(popX, to.x, t), Mathf.LerpUnclamped(popY, to.y, t));
                    var s = Mathf.LerpUnclamped(1.3f, 0.7f, t);
                    coin.style.scale = new Scale(new Vector3(s, s, 1));
                }, Ease.CubicIn, 0.38f + i * 0.045f).OnComplete(() =>
                {
                    coin.RemoveFromHierarchy();
                    if (layer.panel == null) return;
                    Sfx.Play("coin");
                    onLand?.Invoke();
                    Stars(layer, to.x, to.y, 3, 100, new[] { Design.Hex(0xffd75e) }, 0.3f, 0.6f);
                });
            }
        }

        /// <summary>Stars flung out of (x, y), falling a little and shrinking away.</summary>
        static void Stars(VisualElement layer, float x, float y, int count, float speed, Color[] colors, float life = 0.9f, float scale = 1,
            float angleFrom = 0, float angleTo = 360, float gravity = 200)
        {
            if (layer.panel == null) return;
            for (int i = 0; i < count; i++)
            {
                var star = Bit(layer, Fx.Tex.Star, scale, colors[UnityEngine.Random.Range(0, colors.Length)]);
                var a = UnityEngine.Random.Range(angleFrom, angleTo) * Mathf.Deg2Rad;
                var v = UnityEngine.Random.Range(speed * 0.35f, speed);
                float vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v;
                var spin = UnityEngine.Random.Range(-360f, 360f);
                var lifeI = life * UnityEngine.Random.Range(0.75f, 1.1f);
                Put(star, x, y);
                Anim.Float(0, 1, lifeI, t =>
                {
                    var s = t * lifeI;
                    Put(star, x + vx * s, y + vy * s + gravity * s * s / 2);
                    star.style.scale = new Scale(Vector3.one * (1 - t));
                    star.style.rotate = new Rotate(spin * t);
                }, Ease.Linear).OnComplete(star.RemoveFromHierarchy);
            }
        }

        /// <summary>Confetti rain over the whole screen.</summary>
        static void Confetti(VisualElement layer)
        {
            if (layer.panel == null) return;
            long[] tints = { 0xf1c40f, 0xe74c3c, 0x3498db, 0x2ecc71, 0x9b59b6, 0xffffff };
            for (int i = 0; i < 60; i++)
            {
                var bit = Bit(layer, Fx.Tex.Leaf, UnityEngine.Random.Range(0.6f, 1.1f), Design.Hex(tints[UnityEngine.Random.Range(0, tints.Length)]));
                float x = UnityEngine.Random.Range(0f, Design.Width), vx = UnityEngine.Random.Range(-60f, 60f), vy = UnityEngine.Random.Range(120f, 320f);
                const float life = 2.6f;
                Put(bit, x, -20);
                Anim.Float(0, 1, life, t =>
                {
                    var s = t * life;
                    Put(bit, x + vx * s, -20 + vy * s);
                    bit.style.rotate = new Rotate(720 * t);
                    bit.style.opacity = t > 0.85f ? (1 - t) / 0.15f : 1;
                }, Ease.Linear).OnComplete(bit.RemoveFromHierarchy);
            }
        }

        /// <summary>A golden pillar of light on the hero, a ring and stars thrown up.</summary>
        static void LevelBurst(VisualElement layer, float x, float y)
        {
            if (layer.panel == null) return;
            var pillar = new VisualElement { pickingMode = PickingMode.Ignore };
            pillar.style.position = Position.Absolute;
            pillar.style.backgroundImage = new StyleBackground(Fx.Get(Fx.Tex.Soft));
            pillar.style.unityBackgroundImageTintColor = Design.Hex(0xffd75e);
            layer.Add(pillar);
            void Shape(float sx, float sy, float alpha)
            {
                float pw = 32 * sx, ph = 32 * sy;
                pillar.style.left = x - pw / 2;
                pillar.style.top = y - 180 - ph / 2;
                Ui.Size(pillar, pw, ph);
                pillar.style.opacity = alpha;
            }
            Shape(0.5f, 0.5f, 0);
            Anim.Float(0, 1, 0.26f, t => Shape(Mathf.LerpUnclamped(0.5f, 4, t), Mathf.LerpUnclamped(0.5f, 18, t), 0.9f * t), Ease.CubicOut)
                .OnComplete(() => Anim.Float(0, 1, 0.9f, t => Shape(Mathf.LerpUnclamped(4, 2.5f, t), 18, 0.9f * (1 - t)), Ease.Linear)
                    .OnComplete(pillar.RemoveFromHierarchy));
            var ring = Bit(layer, Fx.Tex.Ring, 1, Design.Hex(0xffc93c));
            Put(ring, x, y);
            Anim.Float(0, 1, 0.5f, t =>
            {
                ring.style.scale = new Scale(new Vector3(1 + 3 * t, (1 + 3 * t) * 0.35f, 1));
                ring.style.opacity = 1 - t;
            }, Ease.CubicOut).OnComplete(ring.RemoveFromHierarchy);
            Stars(layer, x, y - 120, 30, 420, new[] { Design.Hex(0xffe066), Design.Hex(0xffb300), Design.Hex(0xf5a623) }, 1.2f, 1.2f, 200, 340, 300);
        }
    }
}
