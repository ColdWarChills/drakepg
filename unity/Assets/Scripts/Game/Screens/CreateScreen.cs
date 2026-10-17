using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// New game in two steps (designer, 2026-10-17): first the character (name; looks come with the creator art),
    /// then the class. Class cards and the chapter picker are from CreateScene.ts.
    /// </summary>
    public class CreateScreen : GameScreen
    {
        static readonly string[] Names =
        {
            "Aldric", "Brynn", "Cassia", "Dorian", "Elowen", "Fenwick", "Gwyn", "Harlan", "Isolde", "Jory", "Kestrel", "Lorne", "Nyx",
            "Orrin", "Perrin", "Quill", "Rowan", "Sable", "Tamsin", "Ulric", "Vesper", "Wren",
        };
        const int MaxName = 14;
        const float CardW = 340, CardH = 400, CardY = 180;

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
        string heroName = "";
        bool starting;
        int chapter = 1;
        /// <summary>1: the character, 2: the class.</summary>
        int step;
        NameBox nameInput;
        Transform stepWorld;
        VisualElement stepUi;

        public override void Enter()
        {
            const float w = Design.Width, h = Design.Height;
            // Night sky gradient: two bands are enough under the stars.
            Backdrop.Rect(World, w / 2, h / 4, w, h / 2, Design.Hex(0x2a2856), -32001);
            Backdrop.Rect(World, w / 2, h * 3 / 4, w, h / 2, Design.Hex(0x0a0e23), -32001);
            Backdrop.Rect(World, w / 2, h / 2, w, 120, Design.Hex(0x1a1b3c), -32000);
            Fx.StarField(World, 60, h);
            // Clicking anywhere else stops typing, so number keys pick classes again.
            Root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (nameInput != null && e.target != nameInput) nameInput.Unfocus();
            }, TrickleDown.TrickleDown);
            ShowStep(1);
        }

        void ShowStep(int which)
        {
            if (nameInput != null) heroName = nameInput.Value;
            stepUi?.RemoveFromHierarchy();
            if (stepWorld != null) Object.Destroy(stepWorld.gameObject);
            cards.Clear();
            nameInput = null;
            step = which;
            stepWorld = new GameObject("step " + which).transform;
            stepWorld.SetParent(World, false);
            stepUi = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(stepUi, 0, 0);
            Ui.Size(stepUi, Design.Width, Design.Height);
            Root.Add(stepUi);
            Steps();
            if (which == 1) BuildCharacterStep();
            else BuildClassStep();
            stepUi.style.opacity = 0;
            Anim.Float(0, 1, 0.2f, a => stepUi.style.opacity = a, Ease.Linear);
        }

        /// <summary>"1 Hero · 2 Class" under the title, the current step in gold.</summary>
        void Steps()
        {
            string Part(int n, string label) => Ui.Colored($"{n}  {label}", n == step ? Palette.TextGold : Palette.TextDim);
            Ui.Text(stepUi, Design.Width / 2, 96, Part(1, "Hero") + Ui.Colored("     ·     ", Palette.TextDim) + Part(2, "Class"), 18, bold: true,
                ax: 0.5f, ay: 0.5f);
        }

        // --- Step 1: the character ---

        void BuildCharacterStep()
        {
            const float w = Design.Width;
            Ui.Text(stepUi, w / 2, 50, "Create Your Hero", 44, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);

            // The hero on a small lit stage, without a weapon: the class gives that.
            const float heroX = 420, feetY = 610;
            Props.Glow(stepWorld, heroX, feetY - 170, Design.Hex(0x8ec5ff, 0.12f), 16, 18, -900);
            var floor = Shapes.Create(stepWorld, heroX, feetY, -800, "stage");
            floor.Ellipse(new Color(0, 0, 0, 0.35f), 0, 6, 300, 54);
            floor.Ellipse(Design.Hex(0x2f2a4a, 0.9f), 0, 0, 280, 44);
            var look = Db.Data.Classes[0].Look;
            var hero = Figure.Create(stepWorld, heroX, feetY, look, 1);
            hero.SetScale(1.55f);
            // A slow breath so the preview isn't a still.
            Anim.Float(1, 1.02f, 1.1f, b =>
            {
                if (hero != null) hero.transform.localScale = new Vector3(1.55f, 1.55f * b, 1);
            }, Ease.SineInOut, repeat: -1, yoyo: true);

            // Right side: the name.
            const float panelX = 700, panelW = 480;
            Ui.Panel(stepUi, panelX, 220, panelW, 220, 0.9f, false);
            Ui.Text(stepUi, panelX + 40, 252, "Name", 24, Palette.TextGold, true);
            nameInput = new NameBox(stepUi, panelX + 40, 296, 270, 50, "Hero", MaxName) { Value = heroName };
            new UiButton(stepUi, panelX + 40 + 270 + 75, 321, "Random", () =>
            {
                nameInput.Value = Names[Random.Range(0, Names.Length)];
                Sfx.Play("open");
            }, 120, 50, 18);
            Ui.Text(stepUi, panelX + 40, 370, "Click the box to type. Your class comes next: it decides your weapon, skills and starting gear.",
                16, Palette.TextDim, wrap: panelW - 80);

            new UiButton(stepUi, 80, 40, "Back", () => GameRoot.I.Go(new TitleScreen()), 120, 44, 18);
            new UiButton(stepUi, w / 2 + 300, 668, "Next: Class", () => ShowStep(2), 280, 60, 24);
            nameInput.Focus();
        }

        // --- Step 2: the class ---

        void BuildClassStep()
        {
            const float w = Design.Width;
            var title = string.IsNullOrWhiteSpace(heroName) ? "Choose Your Class" : $"Choose {heroName.Trim()}'s Class";
            Ui.Text(stepUi, w / 2, 50, title, 44, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
            var classes = Db.Data.Classes;
            for (int i = 0; i < classes.Count; i++) BuildCard(classes[i], w / 2 + (i - 1) * (CardW + 30));
            Select(selected, false);

            new UiButton(stepUi, w / 2, 668, "Begin Adventure", Begin, 300, 60, 24);
            BuildChapterPicker(40, 668);
            new UiButton(stepUi, 80, 40, "Back", () => ShowStep(1), 120, 44, 18);
        }

        void BuildCard(ClassDef cls, float x)
        {
            var card = new Card { Cls = cls, X = x };
            var cy = CardY + CardH / 2;
            card.Fill = Backdrop.Rect(stepWorld, x, cy, CardW, CardH, new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.92f), -1000);
            card.FigY = CardY + 215;
            card.Fig = Figure.Create(stepWorld, x, card.FigY, cls.Look, 1, Db.Item(cls.Weapon));
            card.Fig.SetScale(0.72f);

            // The frame, texts and click area are UI over the world-drawn fill and hero.
            var box = new VisualElement { pickingMode = PickingMode.Position };
            Ui.Place(box, x - CardW / 2, CardY);
            Ui.Size(box, CardW, CardH);
            Ui.Round(box, 14);
            Ui.Border(box, 3, Palette.PanelBorder);
            box.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            box.RegisterCallback<PointerUpEvent>(_ => Select(cls.Id));
            stepUi.Add(box);
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

        void StepClass(int dir)
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
                    if (c.Fig == null) return;
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
                    card.Hop = Anim.Float(0, -16, 0.14f, v => { if (c.Fig != null) PlaceFigure(c, v); }, Ease.QuadOut, yoyo: true, repeat: 1);
                    Fx.Burst(stepWorld, card.X, CardY + 120, "fx_star", 10, 60, 200, 0.8f, 0.5f, Design.Hex(0xffe066), true);
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
            Ui.Text(stepUi, x, y, "Start at", 18, Palette.TextDim, ax: 0, ay: 0.5f);
            var starts = Db.Data.ChapterStarts;
            var buttons = new List<UiButton>();
            void Paint()
            {
                for (int i = 0; i < buttons.Count; i++) buttons[i].SetColor(starts[i].Chapter == chapter ? Design.Hex(0x6b5320) : Palette.Button);
            }
            for (int i = 0; i < starts.Count; i++)
            {
                var s = starts[i];
                buttons.Add(new UiButton(stepUi, x + 130 + i * 92, y, $"Ch {s.Chapter}", () =>
                {
                    chapter = s.Chapter;
                    Paint();
                }, 84, 44, 18));
            }
            Paint();
        }

        public override void Tick(float dt)
        {
            nameInput?.Tick(dt);
            if (!InputEnabled || UiModal.OpenCount > 0) return;
            if (step == 1)
            {
                if (Keys.Pressed(Key.Enter) || Keys.Pressed(Key.NumpadEnter)) ShowStep(2);
                return;
            }
            if (Keys.Pressed(Key.Digit1)) Select("warrior");
            if (Keys.Pressed(Key.Digit2)) Select("mage");
            if (Keys.Pressed(Key.Digit3)) Select("rogue");
            if (Keys.Pressed(Key.LeftArrow)) StepClass(-1);
            if (Keys.Pressed(Key.RightArrow)) StepClass(1);
            if (Keys.Pressed(Key.Escape)) ShowStep(1);
            else if (Keys.Pressed(Key.Enter) || Keys.Pressed(Key.NumpadEnter)) Begin();
        }

        void Begin()
        {
            if (starting) return;
            starting = true;
            var name = (heroName ?? "").Trim();
            if (name.Length == 0) name = "Hero";
            if (name.Length > MaxName) name = name.Substring(0, MaxName);
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
