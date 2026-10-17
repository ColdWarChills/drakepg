using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>New game: pick a name and a class (CreateScene.ts).</summary>
    public class CreateScreen : GameScreen
    {
        static readonly string[] Names =
        {
            "Aldric", "Brynn", "Cassia", "Dorian", "Elowen", "Fenwick", "Gwyn", "Harlan", "Isolde", "Jory", "Kestrel", "Lorne", "Nyx",
            "Orrin", "Perrin", "Quill", "Rowan", "Sable", "Tamsin", "Ulric", "Vesper", "Wren",
        };
        const int MaxName = 14;
        const float CardW = 340, CardH = 400, CardY = 210;

        class Card
        {
            public ClassDef Cls;
            public VisualElement Box;
            public SpriteRenderer Fill;
            public Figure Fig;
            public float X, FigY, Scale = 1;
            public Anim.Handle Tween, Hop;
        }

        readonly List<Card> cards = new List<Card>();
        string selected = "warrior";
        bool starting;
        int chapter = 1;
        TextField nameInput;

        public override void Enter()
        {
            const float w = Design.Width, h = Design.Height;
            // Night sky gradient: two bands are enough under the stars.
            Backdrop.Rect(World, w / 2, h / 4, w, h / 2, Design.Hex(0x2a2856), -32001);
            Backdrop.Rect(World, w / 2, h * 3 / 4, w, h / 2, Design.Hex(0x0a0e23), -32001);
            Backdrop.Rect(World, w / 2, h / 2, w, 120, Design.Hex(0x1a1b3c), -32000);
            Fx.StarField(World, 60, h);

            Ui.Text(Root, w / 2, 50, "Create Your Hero", 44, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
            Ui.Text(Root, w / 2 - 260, 140, "Name", 22, Palette.TextDim, ax: 1, ay: 0.5f);
            BuildNameInput(w / 2 - 60, 140);
            new UiButton(Root, w / 2 + 230, 140, "Random", () => nameInput.value = Names[Random.Range(0, Names.Length)], 120, 44, 18);

            var classes = Db.Data.Classes;
            for (int i = 0; i < classes.Count; i++) BuildCard(classes[i], w / 2 + (i - 1) * (CardW + 30));
            Select("warrior", false);

            new UiButton(Root, w / 2, 668, "Begin Adventure", Begin, 300, 60, 24);
            BuildChapterPicker(40, 668);
            new UiButton(Root, 80, 40, "Back", () => GameRoot.I.Go(new TitleScreen()), 120, 44, 18);
            Root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target != nameInput && (e.target as VisualElement)?.GetFirstAncestorOfType<TextField>() == null) nameInput.Blur();
            }, TrickleDown.TrickleDown);
        }

        void BuildNameInput(float x, float y)
        {
            const float w = 340, h = 46;
            nameInput = new TextField { maxLength = MaxName, isDelayed = false };
            nameInput.textEdition.placeholder = "Hero";
            Ui.Place(nameInput, x - w / 2, y - h / 2);
            Ui.Size(nameInput, w, h);
            nameInput.style.marginLeft = nameInput.style.marginTop = 0;
            var input = nameInput.Q(TextField.textInputUssName);
            if (input != null)
            {
                input.style.backgroundColor = Palette.Panel;
                Ui.Border(input, 2, Palette.PanelBorder);
                Ui.Round(input, 10);
                input.style.fontSize = 24;
                input.style.color = Palette.Text;
                input.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            nameInput.RegisterCallback<FocusInEvent>(_ => { if (input != null) Ui.Border(input, 2, Palette.Gold); });
            nameInput.RegisterCallback<FocusOutEvent>(_ => { if (input != null) Ui.Border(input, 2, Palette.PanelBorder); });
            Root.Add(nameInput);
        }

        void BuildCard(ClassDef cls, float x)
        {
            var card = new Card { Cls = cls, X = x };
            var cy = CardY + CardH / 2;
            card.Fill = Backdrop.Rect(World, x, cy, CardW, CardH, new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.92f), -1000);
            card.FigY = CardY + 215;
            card.Fig = Figure.Create(World, x, card.FigY, cls.Look, 1, Db.Item(cls.Weapon));
            card.Fig.SetScale(0.72f);

            // The frame, texts and click area are UI over the world-drawn fill and hero.
            var box = new VisualElement { pickingMode = PickingMode.Position };
            Ui.Place(box, x - CardW / 2, CardY);
            Ui.Size(box, CardW, CardH);
            Ui.Round(box, 14);
            Ui.Border(box, 3, Palette.PanelBorder);
            box.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            box.RegisterCallback<PointerUpEvent>(_ => Select(cls.Id));
            Root.Add(box);
            card.Box = box;
            const float mid = CardW / 2, top = CardH / 2;
            Ui.Text(box, mid, top + 30, cls.Name, 30, Palette.TextGold, true, 0.5f, 0.5f);
            Ui.Text(box, mid, top + 66, cls.Tagline, 17, Palette.Text, ax: 0.5f, ay: 0.5f, wrap: CardW - 40, align: TextAnchor.MiddleCenter);
            Ui.Text(box, mid, top + 106, string.Join("   ", Stat.Keys.Select(k => $"{Player.StatLabels[k]} {cls.Stats[k]}")), 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
            var early = cls.Skills.Where(s => s.Level <= 4).ToList();
            var later = cls.Skills.Count - early.Count;
            var parts = early.Select(s => Db.Skill(s.SkillId).Name + (s.Level > 1 ? $" (Lv {s.Level})" : "")).ToList();
            if (later > 0) parts.Add($"+{later} more by Lv {cls.Skills.Max(s => s.Level)}");
            Ui.Text(box, mid, top + 150, string.Join("  ·  ", parts), 15, Palette.TextMagic, ax: 0.5f, ay: 0.5f, wrap: CardW - 40, align: TextAnchor.MiddleCenter);
            cards.Add(card);
        }

        void Step(int dir)
        {
            var classes = Db.Data.Classes;
            var i = classes.FindIndex(c => c.Id == selected);
            Select(classes[(i + dir + classes.Count) % classes.Count].Id);
        }

        void Select(string id, bool feedback = true)
        {
            selected = id;
            foreach (var card in cards)
            {
                var on = card.Cls.Id == id;
                card.Tween?.Stop();
                Ui.Border(card.Box, on ? 4 : 3, on ? Palette.Gold : Palette.PanelBorder);
                var from = card.Scale;
                var to = on ? 1.04f : 0.96f;
                var alphaTo = on ? 1f : 0.7f;
                var alphaFrom = card.Box.resolvedStyle.opacity;
                var c = card;
                card.Tween = Anim.Float(0, 1, 0.18f, t =>
                {
                    c.Scale = Mathf.LerpUnclamped(from, to, t);
                    var a = Mathf.Lerp(alphaFrom, alphaTo, Mathf.Clamp01(t));
                    c.Box.style.scale = new Scale(Vector3.one * c.Scale);
                    c.Box.style.opacity = a;
                    c.Fill.transform.localScale = new Vector3(CardW * c.Scale, CardH * c.Scale, 1);
                    c.Fill.color = new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.92f * a);
                    c.Fig.SetScale(0.72f * c.Scale);
                    c.Fig.SetAlpha(a);
                    PlaceFigure(c, 0);
                }, Ease.BackOut);
                if (on && feedback)
                {
                    card.Hop?.Stop();
                    card.Hop = Anim.Float(0, -16, 0.14f, v => PlaceFigure(c, v), Ease.QuadOut, yoyo: true, repeat: 1);
                    Fx.Burst(World, card.X, CardY + 120, "fx_star", 10, 60, 200, 0.8f, 0.5f, Design.Hex(0xffe066), true);
                }
            }
            if (feedback) Sfx.Play("open");
        }

        /// <summary>The hero sits in the card, scaled with it around the card's center, hopping by `hop`.</summary>
        void PlaceFigure(Card c, float hop)
        {
            var cy = CardY + CardH / 2;
            c.Fig.SetFeet(c.X, cy + (c.FigY - cy) * c.Scale + hop);
        }

        /// <summary>"Start at: Ch 1 / Ch 2 / Ch 3": skip ahead to test a later chapter with a fitting level and gear.</summary>
        void BuildChapterPicker(float x, float y)
        {
            Ui.Text(Root, x, y, "Start at", 18, Palette.TextDim, ax: 0, ay: 0.5f);
            var starts = Db.Data.ChapterStarts;
            var buttons = new List<UiButton>();
            void Paint()
            {
                for (int i = 0; i < buttons.Count; i++) buttons[i].SetColor(starts[i].Chapter == chapter ? Design.Hex(0x6b5320) : Palette.Button);
            }
            for (int i = 0; i < starts.Count; i++)
            {
                var s = starts[i];
                buttons.Add(new UiButton(Root, x + 130 + i * 92, y, $"Ch {s.Chapter}", () =>
                {
                    chapter = s.Chapter;
                    Paint();
                }, 84, 44, 18));
            }
            chapter = 1;
            Paint();
        }

        public override void Tick(float dt)
        {
            if (!InputEnabled || UiModal.OpenCount > 0) return;
            if (Keys.Pressed(Key.Digit1)) Select("warrior");
            if (Keys.Pressed(Key.Digit2)) Select("mage");
            if (Keys.Pressed(Key.Digit3)) Select("rogue");
            if (Keys.Pressed(Key.LeftArrow)) Step(-1);
            if (Keys.Pressed(Key.RightArrow)) Step(1);
            if (Keys.Pressed(Key.Enter) || Keys.Pressed(Key.NumpadEnter)) Begin();
        }

        void Begin()
        {
            if (starting) return;
            starting = true;
            var name = (nameInput.value ?? "").Trim();
            if (name.Length == 0) name = "Hero";
            if (name.Length > MaxName) name = name.Substring(0, MaxName);
            nameInput.Blur();
            Saves.Clear();
            var player = Player.CreateNew(name, selected);
            var start = Db.Data.ChapterStarts.Find(s => s.Chapter == chapter);
            if (start != null && start.Chapter > 1) DevTools.ApplyChapterStart(player, start);
            // A chapter start already knows its skills; a new hero's first skill is tagged NEW.
            Features.MarkSkillsSeen(player, start == null || start.Chapter == 1);
            // Chapter 1 begins on the prologue road; later chapter starts are already in town.
            var at = (chapter == 1 ? Db.Data.Start : Db.Data.TownStart).Clone();
            player.Data.Location = at.Clone();
            Session.Start(player, true);
            Saves.Save(player);
            Sfx.Play("quest");
            GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(at.RoomId, (float)at.X, (float)at.Y, chapter == 1)), 0.4f);
        }
    }
}
