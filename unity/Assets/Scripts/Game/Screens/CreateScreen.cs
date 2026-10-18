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
            /// <summary>The card's felt, plain and picked (lighter, gold thread).</summary>
            public Sprite Plain, Picked;
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
            // The title's valley at dusk, darker so the parchment cards and the hero stand out.
            var bg = Backdrop.Draw(World, new[] { "bg_title" });
            if (bg != null) Backdrop.Rect(World, w / 2, h / 2, w, h, new Color(0.05f, 0.03f, 0.02f, 0.5f), -31000);
            else Backdrop.Rect(World, w / 2, h / 2, w, h, Design.Hex(0x1e1610), -32001);
            Fx.Embers(World);
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

        /// <summary>Lettering over the scene: cream Alegreya with an ink outline, like a map's.</summary>
        static readonly Color Lettering = Design.Hex(0xf6e7c1), LetteringDim = Design.Hex(0xb3a283);

        Label Heading(string text, float y)
        {
            var l = Ui.Text(stepUi, Design.Width / 2, y, text, 46, Lettering, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(l);
            l.style.unityTextOutlineColor = Felt.Ink;
            return l;
        }

        /// <summary>"1 Hero · 2 Class" under the title, the current step brighter.</summary>
        void Steps()
        {
            string Part(int n, string label) => Ui.Colored($"{n}  {label}", n == step ? Lettering : LetteringDim);
            var l = Ui.Text(stepUi, Design.Width / 2, 98, Part(1, "Hero") + Ui.Colored("     ·     ", LetteringDim) + Part(2, "Class"), 19, bold: true,
                ax: 0.5f, ay: 0.5f, stroke: true);
            l.style.unityTextOutlineColor = Felt.Ink;
        }

        // --- Step 1: the character ---

        void BuildCharacterStep()
        {
            const float w = Design.Width;
            Heading("Create Your Hero", 50);

            // The hero on a small lit stage, without a weapon: the class gives that.
            const float heroX = 420, feetY = 610;
            Props.Glow(stepWorld, heroX, feetY - 170, Design.Hex(0xffd9a0, 0.1f), 16, 18, -900);
            var floor = Shapes.Create(stepWorld, heroX, feetY, -800, "stage");
            floor.Ellipse(new Color(0, 0, 0, 0.3f), 0, 6, 300, 54);
            floor.Ellipse(Design.Hex(0x2a1d14, 0.75f), 0, 0, 280, 44);
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
            var panel = Ui.Panel(stepUi, panelX, 220, panelW, 220, 0.9f, false);
            var label = Ui.Text(panel, 40, 30, "Name", 26, Palette.TextGold);
            Fonts.Title(label);
            nameInput = new NameBox(panel, 40, 76, 270, 50, "Hero", MaxName) { Value = heroName };
            new UiButton(panel, 40 + 270 + 75, 101, "Random", () =>
            {
                nameInput.Value = Names[Random.Range(0, Names.Length)];
                Sfx.Play("open");
            }, 120, 50, 18);
            Ui.Text(panel, 40, 150, "Click the box to type. Your class comes next: it decides your weapon, skills and starting gear.",
                17, Palette.TextDim, wrap: panelW - 80);

            new UiButton(stepUi, 80, 40, "Back", () => GameRoot.I.Go(new TitleScreen()), 120, 44, 18);
            new UiButton(stepUi, w / 2 + 300, 668, "Next: Class", () => ShowStep(2), 280, 60, 24);
            nameInput.Focus();
        }

        // --- Step 2: the class ---

        void BuildClassStep()
        {
            const float w = Design.Width;
            var title = string.IsNullOrWhiteSpace(heroName) ? "Choose Your Class" : $"Choose {heroName.Trim()}'s Class";
            Heading(title, 50);
            var classes = Db.Data.Classes;
            for (int i = 0; i < classes.Count; i++) BuildCard(classes[i], w / 2 + (i - 1) * (CardW + 30), i);
            Select(selected, false);

            new UiButton(stepUi, w / 2, 668, "Begin Adventure", Begin, 300, 60, 24);
            BuildChapterPicker(40, 668);
            new UiButton(stepUi, 80, 40, "Back", () => ShowStep(1), 120, 44, 18);
        }

        void BuildCard(ClassDef cls, float x, int index)
        {
            var card = new Card { Cls = cls, X = x };
            var cy = CardY + CardH / 2;
            // A felt card in the world, so the hero (a world figure) stands on it; picked, lighter felt with gold thread.
            card.Plain = Felt.PatchSprite(CardW, CardH, 61 + index, new Felt.Patch { Fuzz = 0 });
            card.Picked = Felt.PatchSprite(CardW, CardH, 61 + index, new Felt.Patch { Cloth = Design.Hex(0x34426a), Thread = Felt.Gold, Fuzz = 0 });
            var go = new GameObject("card " + cls.Id);
            go.transform.SetParent(stepWorld, false);
            card.Fill = go.AddComponent<SpriteRenderer>();
            card.Fill.sprite = card.Plain;
            card.Fill.sortingOrder = -1000;
            go.transform.position = Design.World(x, cy, 4);
            card.FigY = CardY + 215;
            card.Fig = Figure.Create(stepWorld, x, card.FigY, cls.Look, 1, Db.Item(cls.Weapon));
            card.Fig.SetScale(0.72f);

            // The frame, texts and click area are UI over the world-drawn fill and hero.
            var box = new VisualElement { pickingMode = PickingMode.Position };
            Ui.Place(box, x - CardW / 2, CardY);
            Ui.Size(box, CardW, CardH);
            box.AddToClassList(FeltPass.Class);
            box.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            box.RegisterCallback<PointerUpEvent>(_ => Select(cls.Id));
            stepUi.Add(box);
            card.Box = box;
            const float mid = CardW / 2, top = CardH / 2;
            Fonts.Title(Ui.Text(box, mid, top + 30, cls.Name, 32, Palette.TextGold, false, 0.5f, 0.5f));
            Ui.Text(box, mid, top + 66, cls.Tagline, 17, Palette.Text, ax: 0.5f, ay: 0.5f, wrap: CardW - 40, align: TextAnchor.MiddleCenter);
            Ui.Text(box, mid, top + 106, string.Join("   ", Stat.Keys.Select(k => $"{Player.StatLabels[k]} {cls.Stats[k]}")), 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
            var early = cls.Skills.Where(s => s.Level <= 4).ToList();
            var later = cls.Skills.Count - early.Count;
            var parts = early.Select(s => Db.Skill(s.SkillId).Name + (s.Level > 1 ? $" (Lv {s.Level})" : "")).ToList();
            if (later > 0) parts.Add($"+{later} more by Lv {cls.Skills.Max(s => s.Level)}");
            Ui.Text(box, mid, top + 150, string.Join("  ·  ", parts), 15, Palette.TextMagic, ax: 0.5f, ay: 0.5f, wrap: CardW - 40, align: TextAnchor.MiddleCenter);
            // Its number key on a sewing button, like the battle buttons.
            Felt.SewnButton(box, -8, -10, $"{index + 1}");
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
                card.Fill.sprite = on ? card.Picked : card.Plain;
                var from = card.Scale;
                var to = on ? 1.04f : 0.96f;
                var alphaTo = on ? 1f : 0.88f;
                var alphaFrom = card.Box.resolvedStyle.opacity;
                var c = card;
                card.Tween = Anim.Float(0, 1, 0.18f, t =>
                {
                    if (c.Fig == null) return;
                    c.Scale = Mathf.LerpUnclamped(from, to, t);
                    var a = Mathf.Lerp(alphaFrom, alphaTo, Mathf.Clamp01(t));
                    c.Box.style.scale = new Scale(Vector3.one * c.Scale);
                    c.Box.style.opacity = a;
                    c.Fill.transform.localScale = new Vector3(c.Scale, c.Scale, 1);
                    c.Fill.color = new Color(1, 1, 1, a);
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
            Ui.Text(stepUi, x, y, "Start at", 19, LetteringDim, true, 0, 0.5f, stroke: true).style.unityTextOutlineColor = Felt.Ink;
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
