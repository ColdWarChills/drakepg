using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>
    /// A turn-based fight (BattleScene.ts). The rules (Rules.Battle) resolve a whole turn at once; this screen plays
    /// its events back one by one: lunges, projectiles, numbers, statuses, the log.
    /// </summary>
    public class BattleScreen : GameScreen
    {
        const float GroundY = 500;
        /// <summary>The stage at its normal size: where the fighters stand, and how high above the feet hits land.</summary>
        const float PlayerHome = 330, EnemyHome = 950, ChestHome = 120;
        /// <summary>
        /// This fight's stage (Stage): where the fighters stand and where hits land. A monster too tall for the room
        /// under its sheet pulls the camera back: everything shrinks toward the middle of the ground line.
        /// </summary>
        static float PlayerX = PlayerHome, EnemyX = EnemyHome, Chest = ChestHome, StageScale = 1;
        /// <summary>The smallest the stage gets (the biggest bosses).</summary>
        const float MinStage = 0.66f;
        const int LogLines = 4;
        /// <summary>Painted backgrounds keep their ground low, mostly under the bottom panel: lift them so the fighters stand on it.</summary>
        const float BgRaise = 90;
        /// <summary>Skills shown per page of the Skills menu (the other slots are More / Back).</summary>
        const int SkillsPerPage = 4;
        const long GuardColor = 0x8ec5ff;
        /// <summary>Claw rakes and lunge trails of monster attacks without an element.</summary>
        const long MonsterHitColor = 0xff6b5a;
        const long EliteGlow = 0xffc23d, LuckyGlow = 0x7dffb0;
        /// <summary>How far off screen the HUD pieces start and leave to (the side sheets sideways, the runner down).</summary>
        static readonly Vector2 HudOff = new Vector2(470, 200);
        static readonly Color LuckyText = Design.Css("#8dffbe");
        static readonly Color ToughText = Design.Css("#ff9a4d");
        static readonly Color SetText = Design.Css("#7fe0c0");
        static readonly Color MpFelt = Design.Hex(0x3f6fb0), HeartInk = Design.Hex(0x3a0f08), DropInk = Design.Hex(0x0f1f3a);

        static readonly Dictionary<string, string> EffectLabels = new Dictionary<string, string>
        {
            ["weak"] = "Weak!", ["resisted"] = "Resisted", ["immune"] = "Immune", ["absorb"] = "Absorbs!",
        };
        static readonly Dictionary<string, string> EffectTags = new Dictionary<string, string>
        {
            ["weak"] = "WEAK!", ["resisted"] = "Resisted", ["immune"] = "Immune", ["absorb"] = "Absorbed",
        };
        static Color EffectColor(string effect) => effect == "weak" ? Palette.TextGood : Palette.TextBad;
        /// <summary>Where each hit's number pops in a combo, so quick hits don't cover each other.</summary>
        static readonly int[][] ComboSpots = { new[] { 0, 0 }, new[] { 70, -30 }, new[] { -70, -12 }, new[] { 36, -58 }, new[] { -36, -44 } };

        /// <summary>How a hit is animated.</summary>
        class Style
        {
            /// <summary>slash, heavy, bolt, proc, enemy-melee or enemy-spit.</summary>
            public string Kind;
            public long[] Colors;
            public string Element;
            public bool Big;
        }

        /// <summary>Where a hit sits in a multi-hit move: lunge in on the first, stay in reach, go home after the last.</summary>
        struct Chain
        {
            public bool First, Last;
            /// <summary>The blow that ends the fight (slow motion, the camera closes in, a gush).</summary>
            public bool Finisher;
            public static readonly Chain Single = new Chain { First = true, Last = true };
        }

        class MenuButton
        {
            public string Label, Sub, Hover;
            public bool Enabled = true;
            public Action Run;
            /// <summary>Skills and items: a color of their own (the paper's tint) so they read apart at a glance.</summary>
            public Color? Accent;
            /// <summary>An ink icon (Resources/UiIcons), or an item's own art.</summary>
            public string Icon, ArtIcon;
            /// <summary>The red button (Attack).</summary>
            public bool Main;
        }

        /// <summary>A fighter on screen: position, tilt and squash applied to its figure every frame.</summary>
        class Fighter
        {
            public Figure Fig;
            public float X, Y = GroundY, Angle, Sx = 1, Sy = 1, Breath = 1, Base = 1;
            public int Facing = 1;
            public readonly List<Anim.Handle> Tweens = new List<Anim.Handle>();
            public float[] WeaponAngles = new float[0];
            public readonly List<Anim.Handle> WeaponTweens = new List<Anim.Handle>();

            public void Apply()
            {
                if (Fig == null) return;
                Fig.transform.position = Design.World(X, Y);
                Fig.transform.localRotation = Quaternion.Euler(0, 0, -Angle);
                Fig.transform.localScale = new Vector3(Base * Sx * Facing, Base * Sy * Breath, 1);
                Fig.Order = Design.Depth(GroundY);
            }

            public Anim.Handle Track(Anim.Handle h)
            {
                Tweens.RemoveAll(t => t.IsDone);
                Tweens.Add(h);
                return h;
            }

            /// <summary>Bend the picture: lean (degrees, + = toward the foe) and crouch (0-1); `hold` > 0 springs back after it.</summary>
            public void Pose(float lean, float crouch, float hold = -1) => Fig?.Bend?.Pose(lean, crouch, hold);

            /// <summary>Shake with strain (degrees; 0 stops).</summary>
            public void Strain(float degrees)
            {
                if (Fig != null && Fig.Bend != null) Fig.Bend.Tremble = degrees;
            }

            public void Kill()
            {
                foreach (var t in Tweens) t.Stop();
                Tweens.Clear();
            }

            public void ReadWeapons()
            {
                WeaponAngles = Fig.Weapons.Select(w => w.localEulerAngles.z).ToArray();
            }

            Anim.Handle shaping;

            /// <summary>Ease the body's squash/stretch to (sx, sy) and hold it (feet stay planted: the pivot is at the feet).</summary>
            public void Shape(float sx, float sy, float seconds, Ease ease = Ease.QuadOut)
            {
                shaping?.Stop();
                float fromX = Sx, fromY = Sy;
                shaping = Track(Anim.Float(0, 1, seconds, t =>
                {
                    Sx = Mathf.LerpUnclamped(fromX, sx, t);
                    Sy = Mathf.LerpUnclamped(fromY, sy, t);
                }, ease));
            }

            /// <summary>Snap to (sx, sy), then spring back to normal: landings, impacts, getting hit.</summary>
            public void Squash(float sx, float sy, float seconds = 0.22f)
            {
                shaping?.Stop();
                Sx = sx;
                Sy = sy;
                Shape(1, 1, seconds, Ease.BackOut);
            }
        }

        readonly BattleSetup back;
        Battle battle;
        ZoneDef zone;
        MonsterDef monster;
        Fighter hero, foe;
        FeltBar playerHp, playerMp, enemyHp;
        /// <summary>The HUD in three pieces that slide on and off together: the hero's patch, the enemy's sheet, the bottom runner.</summary>
        VisualElement heroHud, enemyHud, bottomHud;
        /// <summary>The log on the scroll: the last lines, newest darkest (or a hover note in their place).</summary>
        VisualElement logBox;
        readonly List<string> log = new List<string>();
        VisualElement menu;
        List<MenuButton> menuButtons = new List<MenuButton>();
        /// <summary>The holder of the button just used (it punches out while the others drop away).</summary>
        VisualElement pressedHolder;
        /// <summary>Status chips already on screen (side -> ids), so only new ones pop in.</summary>
        readonly Dictionary<string, HashSet<string>> chipsShown = new Dictionary<string, HashSet<string>>
        {
            ["player"] = new HashSet<string>(), ["enemy"] = new HashSet<string>(),
        };
        int skillPage;
        bool busy;
        bool twinFlip;
        /// <summary>Red glow while the enemy winds up a telegraphed attack.</summary>
        SpriteRenderer chargeAura;
        /// <summary>Blue bubble while the player defends.</summary>
        Shapes shield;
        VisualElement playerStatusRow, enemyStatusRow;
        Label shellLine, escapeLine;
        VisualElement affinityRow, weaponTag;
        float affinityY;
        /// <summary>The monster's next move on a parchment tag above it (hidden while a turn plays).</summary>
        VisualElement intentTag, intentMarker;
        Label intentText;
        float intentW, intentTop, intentRight;
        /// <summary>The tag hangs beside the monster's head (no room above it under the sheet).</summary>
        bool intentBeside;
        bool shellRegrew;
        /// <summary>Red power rising around a monster winding up a big attack.</summary>
        Fx.Flow chargeFlow;
        /// <summary>A dark veil over the backdrop for big moments (Dim).</summary>
        SpriteRenderer dim;
        Anim.Handle dimTween;
        /// <summary>This turn's killing blows (the last hit before each "defeated").</summary>
        readonly HashSet<BattleEvent> finishers = new HashSet<BattleEvent>();
        Anim.Handle recoil;
        int comboHits;
        float enemyTextShift;
        /// <summary>Statuses as shown so far during playback (status id -> turns). The battle itself is already a turn ahead.</summary>
        readonly Dictionary<string, List<KeyValuePair<string, int>>> shown = new Dictionary<string, List<KeyValuePair<string, int>>>
        {
            ["player"] = new List<KeyValuePair<string, int>>(), ["enemy"] = new List<KeyValuePair<string, int>>(),
        };

        static IRng Rng => Dice.Rng;
        Look PlayerLook => battle.Player.Look;
        /// <summary>What spills from the monster (null: red blood).</summary>
        Blood MonsterBlood => monster.Look.Blood;
        /// <summary>Sorting order of things lying on the ground (splats, pools): under the fighters.</summary>
        static int GroundOrder => Design.Depth(GroundY) - 60;

        public BattleScreen(BattleSetup setup) => back = setup;

        public override void Enter()
        {
            zone = Db.Zone(back.ZoneId);
            var baseDef = back.Mimic ? Monsters.MakeMimic(zone, Rng) : Monsters.IsLucky(back.MonsterId) ? Monsters.MakeImpling(zone) : Db.Monster(back.MonsterId);
            monster = back.Elite != null ? Monsters.MakeElite(baseDef, back.Elite) : back.Heroic ? Monsters.MakeHeroic(baseDef) : baseDef;
            battle = new Battle(Session.Player, monster, Rng) { ZoneId = back.ZoneId };
            if (monster.IsHeroic) battle.RewardScale = Db.B.Heroic.Rewards;
            // A boss no quest wants is a rematch, except a gate guard: that's only ever fought until beaten.
            else if (monster.Boss && !Quests.NeedsKill(Session.Player, monster.Id) && !Travel.IsGuard(Db.Room(back.RoomId))) battle.RewardScale = Db.B.BossRepeatRewards;
            busy = true;

            Backdrop.Draw(World, zone.Background, zone.Backdrop, BgRaise);
            var foeFigure = Figure.Create(World, Design.Width + 200, GroundY, monster.Look, -1);
            Stage(foeFigure);
            hero = new Fighter { Fig = Figure.Create(World, -200, GroundY, PlayerLook, 1, Session.Player.Weapon, Session.Player.WeaponSigil), X = -200, Base = (float)PlayerLook.Scale * StageScale };
            hero.Fig.EnableBend();
            hero.ReadWeapons();
            if (Session.Player.WeaponSigil != null) Fx.SigilGlow(hero.Fig, Session.Player.WeaponSigil);
            if (monster.Elite != null || monster.Lucky)
            {
                var color = monster.Lucky ? LuckyGlow : EliteGlow;
                var glow = Props.Glow(World, EnemyX, GroundY - 110 * StageScale, Design.Hex(color, 0), 7, 7, Design.Depth(GroundY) - 1);
                float alpha = 0;
                Anim.Float(0, 0.4f, 0.6f, a => alpha = a, Ease.Linear, 0.4f);
                Anim.Float(7, 8, 1.1f, s =>
                {
                    if (glow == null) return;
                    glow.transform.localScale = new Vector3(s / 2 * StageScale, s / 2 * StageScale, 1);
                    glow.color = Design.Hex(color, alpha);
                }, Ease.SineInOut, repeat: -1, yoyo: true);
            }
            foe = new Fighter { Fig = foeFigure, X = Design.Width + 200, Facing = -1, Base = (float)monster.Look.Scale * StageScale };
            foe.Fig.EnableBend();
            foe.ReadWeapons();
            hero.Apply();
            foe.Apply();

            heroHud = UiMotion.Layer(Root, "hero-hud");
            enemyHud = UiMotion.Layer(Root, "enemy-hud");
            bottomHud = UiMotion.Layer(Root, "bottom-hud");
            // Off screen until the intro slides them in.
            heroHud.style.translate = new Translate(HudOff.x * -1, 0);
            enemyHud.style.translate = new Translate(HudOff.x, 0);
            bottomHud.style.translate = new Translate(0, HudOff.y);
            DrawStatus();
            DrawBottomPanel();
            RefreshBars(false);
            var elite = monster.Elite != null ? Db.Affix(monster.Elite.Affix) : null;
            PushLog(monster.Boss
                ? $"{monster.Name} awakens! There is no running from this fight.{(battle.Grit > 0 ? $" Your grit from earlier tries: +{Math.Round(battle.Grit * 100)}% damage." : "")}"
                : elite != null
                    ? $"An Elite {monster.Name} (Lv {monster.Level}) blocks your path! {elite.Name}: {elite.Description}"
                    : monster.Lucky
                        ? $"A Lucky Impling (Lv {monster.Level}) jingles with gold! Catch it quick: it runs away on its {Ordinal(monster.EscapeAfter ?? 0)} turn."
                        : monster.Tough
                            ? $"A tough {monster.Name} (Lv {monster.Level}) blocks the way! Hit it where it is weak."
                            : $"A wild {monster.Name} (Lv {monster.Level}) appears in the {zone.Name}!");
            Intro();
        }

        async void Intro()
        {
            // The fighters run in while the HUD pieces slide on: the runner first (the ground under the background is bare
            // without it), then the enemy's sheet, then the hero's.
            Sfx.Play("swish");
            UiMotion.SlideIn(bottomHud, new Vector2(0, HudOff.y), 0.34f, 0, Ease.CubicOut);
            UiMotion.SlideIn(enemyHud, new Vector2(HudOff.x, 0), 0.42f, 0.1f);
            UiMotion.SlideIn(heroHud, new Vector2(-HudOff.x, 0), 0.42f, 0.18f);
            await Task.WhenAll(
                hero.Track(Anim.Float(-200, PlayerX, 0.52f, v => hero.X = v, Ease.BackOut)).Task,
                foe.Track(Anim.Float(Design.Width + 200, EnemyX, 0.52f, v => foe.X = v, Ease.BackOut)).Task);
            Fx.Dust(World, PlayerX, GroundY, 6, Design.Depth(GroundY) + 1);
            Fx.Dust(World, EnemyX, GroundY, 6, Design.Depth(GroundY) + 1);
            hero.Squash(1.1f, 0.9f);
            foe.Squash(1.1f, 0.9f);
            hero.Pose(0, 0.8f, 0.14f);
            foe.Pose(0, 0.8f, 0.14f);
            if (monster.Boss)
            {
                Sfx.Play("warning");
                GameRoot.I.Shake(0.3f, 9);
                GameRoot.I.Flash(0.25f, new Color(120 / 255f, 0, 0));
                Fx.Shockwave(World, EnemyX, GroundY, 0xff4d4d, 4, 0.3f);
                await BossBanner();
            }
            Idle(hero);
            Idle(foe);
            busy = false;
            FadeIntent(true);
            ShowMenu("main", true);
        }

        /// <summary>A boss's name on a red felt band that sweeps across the screen, holds, and sweeps off.</summary>
        async Task BossBanner()
        {
            const float w = 1500, h = 110, y = 250;
            var band = Felt.MakePatch(Root, (Design.Width - w) / 2, y, w, h, 61,
                new Felt.Patch { Cloth = Felt.RedFelt, Thread = Felt.RedStitch, Inset = 9, Radius = 14, Dash = 9, Gap = 6, ThreadWidth = 2.6f }, -3, false);
            var kind = Ui.Text(band.Content, w / 2, 26, monster.IsHeroic ? "HEROIC BOSS" : "BOSS", 20, Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(kind);
            kind.style.letterSpacing = 6;
            kind.style.opacity = 0.85f;
            var name = Ui.Text(band.Content, w / 2, 66, monster.Name, 54, Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Title(name);
            var holder = band.Root;
            holder.style.opacity = 0;
            UiMotion.FadeIn(holder, 0.1f);
            Sfx.Play("swish");
            await UiMotion.SlideIn(holder, new Vector2(-Design.Width - 200, 0), 0.26f, 0, Ease.CubicOut).Task;
            GameRoot.I.Shake(0.15f, 5);
            Sfx.Play("hit");
            // The name drifts a little while it holds, so the band doesn't freeze.
            await UiMotion.Move(holder, new Vector2(24, 0), 0.75f, Ease.Linear).Task;
            Sfx.Play("swish");
            await UiMotion.SlideOut(holder, new Vector2(Design.Width + 300, 0), 0.22f).Task;
            holder.RemoveFromHierarchy();
        }

        /// <summary>Breathing: the bent picture breathes by itself (Bend), a stiff one stretches a little.</summary>
        static void Idle(Fighter f)
        {
            if (f.Fig != null && f.Fig.Bend != null) return;
            f.Track(Anim.Float(1, 1.03f, 0.9f, v => f.Breath = v, Ease.SineInOut, repeat: -1, yoyo: true));
        }

        public override void Tick(float dt)
        {
            hero?.Apply();
            foe?.Apply();
            if (!InputEnabled || busy) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Keys.Pressed(keys[i])) continue;
                if (i < menuButtons.Count && menuButtons[i].Enabled) menuButtons[i].Run();
                break;
            }
        }

        // --- Layout (the felt kit, Ui/Felt.cs) ---

        /// <summary>A line of text on parchment: Alegreya Sans bold, ink, shrunk to fit `maxWidth`.</summary>
        static Label PaperLine(VisualElement parent, float x, float y, string rich, float size, Color? color = null, float ax = 0, float ay = 0.5f, float maxWidth = 360)
        {
            var l = Ui.Text(parent, x, y, rich, size, color ?? Felt.Text, false, ax, ay);
            Fonts.Bold(l);
            FitWidth(l, maxWidth, ax);
            return l;
        }

        /// <summary>Text that sits in a row (flex), not at a fixed spot.</summary>
        static Label FlowText(string rich, float size, Color color)
        {
            var l = new Label(rich) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            Fonts.Bold(l);
            NoBox(l);
            return l;
        }

        /// <summary>Shrink an element to `max` px wide once it's laid out (long names, many resists).</summary>
        static void FitWidth(VisualElement e, float max, float originX = 0)
        {
            e.style.transformOrigin = new TransformOrigin(Length.Percent(originX * 100), Length.Percent(50));
            e.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var w = e.layout.width;
                var s = w > max ? max / w : 1;
                e.style.scale = new Scale(new Vector3(s, s, 1));
            });
        }

        /// <summary>"Wanderer  Lv 3": the name in Alegreya with the level in small Alegreya Sans after it.</summary>
        VisualElement NameRow(VisualElement parent, float x, float y, string name, string level, Color color, float maxWidth)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, x, y, 0, 0.5f);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            var n = new Label(name) { pickingMode = PickingMode.Ignore };
            n.style.fontSize = 28;
            n.style.color = color;
            Fonts.Title(n);
            NoBox(n);
            var lv = new Label(level) { pickingMode = PickingMode.Ignore };
            lv.style.fontSize = 19;
            lv.style.color = Felt.TextDim;
            Fonts.Bold(lv);
            NoBox(lv);
            lv.style.marginLeft = 10;
            lv.style.marginBottom = 3;
            row.Add(n);
            row.Add(lv);
            parent.Add(row);
            FitWidth(row, maxWidth);
            return row;
        }

        static void NoBox(Label l)
        {
            l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
        }

        const float EnemyPanelX = 1280 - 14 - 430, LineX = EnemyPanelX + 48, LineStep = 30;

        /// <summary>An info line under the enemy's HP bar: an ink icon, then the text.</summary>
        Label EnemyLine(ref float y, string icon, string rich, float size = 18, Color? color = null)
        {
            if (icon != null) Felt.Icon(enemyHud, EnemyPanelX + 20, y - 12, 24, icon, true);
            var l = PaperLine(enemyHud, LineX, y, rich, size, color, maxWidth: 358);
            y += LineStep;
            return l;
        }

        void DrawStatus()
        {
            var p = battle.Player;
            var m = monster;

            // The hero's piece. HP and MP are shown once, as bars with the numbers on them (a bar answers both "do I
            // survive the next hit?" and "how far along is this fight?"); a small felt heart and drop label them.
            Felt.MakePatch(heroHud, 14, 16, 420, 122, 11, blocksClicks: false);
            NameRow(heroHud, 36, 44, p.Name, $"Lv {p.Level}", Felt.Text, 240);
            RefreshWeaponTag();
            playerHp = new FeltBar(heroHud, 52, 82, 360, 26, Felt.Hp, "", 21, 17);
            playerMp = new FeltBar(heroHud, 52, 112, 360, 21, Felt.Mp, "", 22, 15);
            Felt.Token(heroHud, 46, 82, 38, Felt.Heart, Felt.RedFelt, 1, HeartInk);
            Felt.Token(heroHud, 46, 111, 29, Felt.Drop, MpFelt, 1, DropInk);

            // The enemy's sheet. Elites get a line for their affix, shelled bosses one for the shell, monsters with a
            // signature move one for that; most have a loot line. The next move hangs on a tag above the monster.
            var affix = m.Elite != null ? Db.Affix(m.Elite.Affix) : null;
            var heroic = m.IsHeroic && m.Heroic != null ? $"Heroic: {m.Heroic.Text}" : "";
            var hasAffinities = AffinityParts().Count > 0;
            var panelH = EnemyPanelHeight();
            Felt.MakePatch(enemyHud, EnemyPanelX, 16, 430, panelH, 12, blocksClicks: false);
            // Status names (the highest text, at GroundY - 280) start under the panel and its status chips.
            enemyTextShift = Mathf.Clamp(16 + panelH + 50 - (GroundY - 280), 0, 90);
            // One tag: an Elite or boss already reads as dangerous, and the weakness line shows the counter.
            var tag = m.IsHeroic ? "Heroic" : m.Boss ? "Boss" : affix != null ? "Elite" : m.Lucky ? "Lucky" : m.Tough ? "Tough" : "";
            var headColor = m.Boss ? Felt.Bad : affix != null ? Felt.Gold : m.Lucky ? Felt.OnFelt(LuckyText) : m.Tough ? Felt.OnFelt(ToughText) : Felt.Text;
            NameRow(enemyHud, EnemyPanelX + 22, 44, m.Name, string.Join("  ·  ", new[] { $"Lv {m.Level}", tag }.Where(s => s != "")), headColor, 386);
            enemyHp = new FeltBar(enemyHud, EnemyPanelX + 38, 84, 370, 26, Felt.Hp, "", 23, 17);
            Felt.Token(enemyHud, EnemyPanelX + 32, 84, 38, Felt.Heart, Felt.RedFelt, 1, HeartInk);

            var y = 124f;
            if (hasAffinities)
            {
                Felt.Icon(enemyHud, EnemyPanelX + 20, y - 12, 24, "flame", true);
                affinityY = y;
                DrawAffinities();
                y += LineStep;
            }
            if (affix != null) EnemyLine(ref y, "star", $"{affix.Name}: {affix.Description}", 16, Felt.Gold);
            if (m.Shell != null)
            {
                shellLine = EnemyLine(ref y, "shield", "", 16, Felt.Magic);
                RefreshShell();
            }
            if (m.Trait != null) EnemyLine(ref y, "star", m.Trait, 17, Felt.Trait);
            if (heroic != "") EnemyLine(ref y, "star", heroic, 16, Felt.Gold);
            if (m.EscapeAfter.HasValue && m.EscapeAfter > 0)
            {
                escapeLine = EnemyLine(ref y, "boot", "", 16);
                RefreshEscape();
            }
            if (m.Lucky && m.Loot != null) DrawLuckyLine(LineX, y);
            else if (m.Loot != null) DrawLootLine(LineX, y, m.Loot);

            playerStatusRow = StatusRow(heroHud, 18, 16 + 122 + 12);
            enemyStatusRow = StatusRow(enemyHud, EnemyPanelX + 4, 16 + panelH + 12);
            DrawIntentTag(16 + panelH);
        }

        /// <summary>The enemy's sheet: 100 px plus a line for each of affinities, affix, shell, trait, heroic, escape, loot.</summary>
        float EnemyPanelHeight()
        {
            var m = monster;
            var lines = (AffinityParts().Count > 0 ? 1 : 0) + (m.Elite != null ? 1 : 0) + (m.Shell != null ? 1 : 0) + (m.Trait != null ? 1 : 0)
                        + (m.IsHeroic && m.Heroic != null ? 1 : 0) + (m.EscapeAfter.HasValue && m.EscapeAfter > 0 ? 1 : 0) + (m.Loot != null ? 1 : 0);
            return 100 + lines * LineStep;
        }

        /// <summary>
        /// Size the stage for this monster: if its head would reach into its sheet or the status chips under it, pull
        /// the camera back (both fighters smaller, standing closer to the middle, hits lower) just enough, down to
        /// MinStage. The backdrop stays as it is.
        /// </summary>
        void Stage(Figure foeFigure)
        {
            var room = GroundY - (16 + EnemyPanelHeight() + 44);
            var height = foeFigure.DrawnHeight * (float)monster.Look.Scale;
            StageScale = height > room ? Mathf.Max(MinStage, room / height) : 1;
            PlayerX = Design.Width / 2 + (PlayerHome - Design.Width / 2) * StageScale;
            EnemyX = Design.Width / 2 + (EnemyHome - Design.Width / 2) * StageScale;
            Chest = ChestHome * StageScale;
        }

        static VisualElement StatusRow(VisualElement parent, float x, float y)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, x, y);
            row.style.flexDirection = FlexDirection.Row;
            parent.Add(row);
            return row;
        }

        /// <summary>The tag for the monster's next move: just above its head, never over its sheet or the status chips under it.</summary>
        void DrawIntentTag(float panelBottom)
        {
            var (top, width) = BodyOf("enemy");
            intentBeside = top - 38 - 18 < panelBottom + 46;
            intentTop = intentBeside ? Mathf.Max(panelBottom + 46, top + 12) : top - 38 - 18;
            intentRight = EnemyX - width / 2 - 16;
            intentTag = new VisualElement { pickingMode = PickingMode.Ignore, name = "intent" };
            Ui.Place(intentTag, EnemyX - 60, intentTop);
            Ui.Size(intentTag, 120, 38);
            intentTag.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            // Hidden until the intro is over; it drops in with the hero's first turn.
            intentTag.style.opacity = 0;
            Root.Add(intentTag);
            intentTag.Add(new Felt.PatchView(Felt.Patch.Tag(), 91).Fill());
            // A small thread-colored marker under the tag, pointing at the monster.
            intentMarker = intentBeside
                ? new PanelPaint(intentTag, 0, 11, 10, 16).Triangle(0, 0, 0, 16, 9, 8, Felt.Stitch)
                : new PanelPaint(intentTag, 0, 41, 16, 10).Triangle(0, 0, 16, 0, 8, 9, Felt.Stitch);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, 0, 0);
            row.style.width = Length.Percent(100);
            row.style.height = 38;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            intentTag.Add(row);
            var icon = Felt.Icon(null, 0, 0, 24, "claw", true);
            icon.style.position = Position.Relative;
            icon.style.marginRight = 8;
            row.Add(icon);
            intentText = FlowText("", 18, Felt.Intent);
            Fonts.Heavy(intentText);
            row.Add(intentText);
            intentText.RegisterCallback<GeometryChangedEvent>(_ => SizeIntentTag());
            RefreshIntent();
        }

        /// <summary>Fit the tag to its text (in 16 px steps, so few sizes get baked) and keep it on screen.</summary>
        void SizeIntentTag()
        {
            var w = intentText.layout.width;
            if (float.IsNaN(w)) return;
            var need = Mathf.Min(608, Mathf.Ceil((w + 24 + 8 + 40) / 16) * 16);
            if (need == intentW) return;
            intentW = need;
            if (intentBeside)
            {
                // Left of the monster's head, its marker pointing right at it.
                var at = Mathf.Max(12, intentRight - need);
                intentTag.style.left = at;
                intentTag.style.width = need;
                intentMarker.style.left = need + 3;
                return;
            }
            var left = Mathf.Clamp(EnemyX - need / 2, 12, Design.Width - 12 - need);
            intentTag.style.left = left;
            intentTag.style.width = need;
            intentMarker.style.left = EnemyX - left - 8;
        }

        Anim.Handle intentFade;

        /// <summary>
        /// The tag lifts away while a turn plays (its move is about to happen) and drops back in, swinging on its marker,
        /// with the next one.
        /// </summary>
        void FadeIntent(bool show)
        {
            RefreshPreview();
            if (intentTag == null) return;
            intentFade?.Stop();
            var from = intentTag.resolvedStyle.opacity;
            if (!show)
            {
                intentFade = Anim.Float(from, 0, 0.16f, v => intentTag.style.opacity = v, Ease.Linear);
                UiMotion.Move(intentTag, new Vector2(0, -14), 0.16f, Ease.QuadIn);
                return;
            }
            intentTag.style.rotate = new Rotate(0);
            intentFade = Anim.Float(from, 1, 0.12f, v => intentTag.style.opacity = v, Ease.Linear);
            UiMotion.SlideIn(intentTag, new Vector2(0, -30), 0.32f, 0, Ease.BackOut);
            UiMotion.Pop(intentTag, 0.75f, 0.32f);
            // A small swing as it lands.
            Anim.Float(0, 1, 0.5f, t => intentTag.style.rotate = new Rotate(Mathf.Sin(t * Mathf.PI * 3) * 4 * (1 - t)), Ease.Linear, 0.12f);
        }

        /// <summary>
        /// The monster's telegraphed hit as a pale chunk at the end of the hero's HP bar, so "will I survive?" is one
        /// glance (Into the Breach's damage preview). Not while a turn plays, not for a wind-up (it hits a turn later).
        /// </summary>
        void RefreshPreview()
        {
            if (playerHp == null) return;
            var b = battle;
            var a = b.Intent;
            double? dmg = null;
            if (!busy && !b.IsOver && a != null && (a.Windup == null || b.Charging == a)) dmg = b.IntentDamage();
            playerHp.Preview(b.Player.Hp, b.Player.MaxHp, dmg);
        }

        /// <summary>"Next: Root Crush ~31 dmg · Defend!" plus "Enrages in 3" / "Enraged +40%".</summary>
        void RefreshIntent()
        {
            if (intentText == null) return;
            var b = battle;
            var a = b.Intent;
            if (a == null)
            {
                intentText.text = "";
                intentTag.style.display = DisplayStyle.None;
                return;
            }
            intentTag.style.display = DisplayStyle.Flex;
            var charging = b.Charging == a;
            var dmg = b.IntentDamage();
            var statuses = (a.Applies ?? new List<Apply>()).Select(x => $"{Db.Status(x.Status).Name}{(x.Chance.HasValue && x.Chance < 1 ? $" {Math.Round(x.Chance.Value * 100)}%" : "")}").ToList();
            string detail;
            var color = Felt.Intent;
            if (a.Windup != null && !charging)
            {
                detail = "winds up a big attack";
                color = Felt.OnFelt(ToughText);
            }
            else if (a.Heal.HasValue && a.Heal > 0)
            {
                detail = $"heals {Math.Round(a.Heal.Value * 100)}% HP";
                color = Felt.Magic;
            }
            else if (dmg == null)
            {
                var self = (a.Applies ?? new List<Apply>()).All(x => x.Self);
                detail = statuses.Count > 0 ? $"{(self ? "buffs itself: " : "")}{string.Join(", ", statuses)}" : "prepares";
                color = Felt.Magic;
            }
            else
            {
                detail = string.Join(" ", new[] { $"~{JsMath.Str(dmg.Value)} dmg" }.Concat(statuses.Select(x => "+ " + x)));
                if (charging)
                {
                    detail += " · Defend or heal!";
                    color = Felt.Bad;
                }
                else if (a.Power >= 1.5) color = Felt.OnFelt(ToughText);
            }
            // The move in red ink (or its warning color), the detail lighter, like the mock's "Next: Gore ~6 dmg".
            var text = $"Next: {a.Name}  {Ui.Colored(detail, color == Felt.Intent ? Felt.TextDim : color)}";
            var left = b.TurnsToEnrage;
            var enrage = b.Enrage > 0 ? $"Enraged +{Math.Round(b.Enrage * 100)}%" : left <= Db.B.Enrage.Warn ? $"Enrages in {left}" : "";
            if (enrage != "") text += Ui.Colored($"  ·  {enrage}", Felt.Bad);
            intentText.text = text;
            intentText.style.color = color;
            RefreshPreview();
            FitOnce(intentText, 608 - 72);
        }

        /// <summary>Scale a label down to `max` px after its next layout.</summary>
        static void FitOnce(Label l, float max)
        {
            void Fit(GeometryChangedEvent _)
            {
                var w = l.layout.width;
                var s = w > max ? max / w : 1;
                l.style.scale = new Scale(new Vector3(s, s, 1));
            }
            l.UnregisterCallback<GeometryChangedEvent>(Fit);
            l.RegisterCallback<GeometryChangedEvent>(Fit);
            l.style.scale = new Scale(Vector3.one);
        }

        /// <summary>"Grit +20%  [Water · Weak!]": boss grit, then a chip with the weapon's element and how it lands now.</summary>
        void RefreshWeaponTag()
        {
            var p = battle.Player;
            weaponTag?.RemoveFromHierarchy();
            var element = p.WeaponElement;
            var effect = battle.EffectOn(element);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, 412, 44, 1, 0.5f);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var grit = battle.Grit;
            if (grit > 0)
            {
                var g = FlowText($"Grit +{Math.Round(grit * 100)}%", 15, Felt.Gold);
                g.style.marginRight = 8;
                row.Add(g);
            }
            var color = element != null ? Palette.ElementText(element) : Design.Hex(0x9a8f78);
            var text = element != null ? Db.Data.ElementLabels[element] : "Neutral";
            if (effect != null) text += Ui.Colored($" · {EffectLabels[effect]}", effect == "weak" ? Felt.Good : Felt.Bad);
            row.Add(Felt.Chip(text, color, 15, 71));
            heroHud.Add(row);
            FitWidth(row, 150, 1);
            weaponTag = row;
        }

        /// <summary>"Frost Shell ◆◆◇: 80% less damage. Fire hits crack it."</summary>
        void RefreshShell()
        {
            var shell = monster.Shell;
            if (shell == null || shellLine == null) return;
            var left = battle.Shell;
            var pips = new string('◆', Math.Max(0, left)) + new string('◇', Math.Max(0, shell.Hits - left));
            var regrow = shell.ReformAt.HasValue ? $" Grows back once at {Math.Round(shell.ReformAt.Value * 100)}% HP." : "";
            shellLine.text = left > 0
                ? $"{shell.Name} {pips}: {Math.Round(shell.Reduction * 100)}% less damage. {Db.Data.ElementLabels[shell.Element]} hits crack it."
                : $"{shell.Name} {pips}: broken!{(shellRegrew ? "" : regrow)}";
        }

        /// <summary>"Runs away in 2 turns. A stun holds it."</summary>
        void RefreshEscape()
        {
            var left = battle.EscapeIn;
            if (!left.HasValue || escapeLine == null) return;
            escapeLine.text = left <= 1 ? "Runs away on its next turn! A stun holds it." : $"Runs away in {left} turns. A stun holds it.";
            escapeLine.style.color = left <= 1 ? Felt.Bad : Felt.OnFelt(LuckyText);
        }

        static string Pct1(double v) => $"{Math.Round(v * 1000) / 10}%";

        /// <summary>A hover-able info line (bag icon): shows `details` on the scroll while hovered or pressed.</summary>
        void InfoLine(float x, float y, string rich, string details)
        {
            Felt.Icon(enemyHud, EnemyPanelX + 20, y - 12, 24, "bag", true);
            var line = PaperLine(enemyHud, x, y, rich, 16, maxWidth: 358);
            line.pickingMode = PickingMode.Position;
            line.RegisterCallback<PointerEnterEvent>(_ => ShowNote(details, 15));
            line.RegisterCallback<PointerDownEvent>(_ => ShowNote(details, 15));
            line.RegisterCallback<PointerLeaveEvent>(_ => RestoreLog());
        }

        /// <summary>One line on the scroll, wrapping at its width.</summary>
        Label LogLabel(string text, float size)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = Felt.Ink;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.width = 462;
            l.style.flexShrink = 0;
            NoBox(l);
            l.style.marginBottom = 3;
            Fonts.Body(l);
            logBox.Add(l);
            return l;
        }

        /// <summary>A note in the log's place (a button's or a loot line's details) until the pointer leaves.</summary>
        void ShowNote(string text, float size)
        {
            if (logBox == null) return;
            logBox.Clear();
            logBox.style.justifyContent = Justify.FlexStart;
            LogLabel(text, size);
        }

        /// <summary>The last lines, oldest faintest, the newest in heavy ink.</summary>
        void RestoreLog()
        {
            if (logBox == null) return;
            logBox.Clear();
            logBox.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < log.Count; i++)
            {
                var age = log.Count - 1 - i;
                var l = LogLabel(log[i], 19);
                if (age == 0) Fonts.Heavy(l);
                else l.style.opacity = age == 1 ? 0.75f : age == 2 ? 0.6f : 0.5f;
            }
            // Long lines wrap: when the newest would be cut off, show the bottom of the log instead.
            logBox.schedule.Execute(() =>
            {
                float sum = 0;
                foreach (var c in logBox.Children()) sum += c.layout.height + 3;
                if (sum > logBox.layout.height + 1) logBox.style.justifyContent = Justify.FlexEnd;
            });
        }

        /// <summary>"Loot  ✦ Sigil 10%  ·  Gold x5": a Lucky Impling's payout. Hover or tap for the Sigil odds.</summary>
        void DrawLuckyLine(float x, float y)
        {
            var l = Db.B.Lucky;
            var rich = Ui.Colored("Loot ", Felt.TextDim) + Ui.Colored($" ✦ Sigil {Pct1(l.SigilChance)} ", Felt.OnFelt(Design.Css("#ff9550"))) + Ui.Colored($" ·  Gold x{l.Gold} ", Felt.Gold);
            var tiers = string.Join(", ", Items.SigilTiers.Select(t => $"{Items.SigilNames[t]} {Pct1(l.SigilTiers[t])}"));
            var details = string.Join("\n", $"{Pct1(l.SigilChance)}: a Sigil piece of this area's gear (no normal gear drops).", $"Which Sigil: {tiers}.",
                $"Always: about {l.Gold}x the gold of the monsters here.");
            InfoLine(x, y, rich, details);
        }

        /// <summary>"Gear  Rare 1%  Uncommon 5%  Common 6%": this fight's drop odds, luck included. Hover or tap for the items.</summary>
        void DrawLootLine(float x, float y, string tableId)
        {
            var m = monster;
            var source = m.Boss ? "boss" : m.Elite != null ? "elite" : m.ChestLoot ? "chest" : "mob";
            var misses = battle.Player.Data.LootPity.TryGetValue(tableId, out var mv) ? mv : 0;
            var odds = Loot.TierOdds(tableId, source, misses);
            var luck = Loot.PityBonus(misses);
            string Pct(double v) => v >= 0.1 ? $"{Math.Round(v * 100)}%" : $"{(v * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%";
            var label = new Dictionary<string, string> { ["rare"] = "Rare", ["uncommon"] = "Uncommon", ["common"] = "Common" };
            var color = new Dictionary<string, Color> { ["rare"] = Felt.Gold, ["uncommon"] = Felt.Good, ["common"] = Felt.Text };
            var rich = Ui.Colored("Gear ", Felt.TextDim);
            foreach (var t in Loot.Tiers)
                if (odds[t] > 0) rich += Ui.Colored($" {label[t]} {Pct(odds[t])}{(t == "rare" && luck > 0 ? " (luck)" : "")} ", color[t]);
            // Elites and bosses: a chance at a piece of the area's set on top.
            var table = Db.LootTable(tableId);
            var setChance = table.Set != null && (source == "elite" || source == "boss") ? Cheats.Boosted(Db.B.SetDrop[source]) : 0;
            if (setChance > 0) rich += Ui.Colored($" · Set {Pct(setChance)} ", Felt.OnFelt(SetText));
            // Guild gather hunts: this area's hunt item, on top of everything else.
            var gather = Guild.GatherHere(battle.Player, zone.Id);
            foreach (var g in gather) rich += Ui.Colored($" · {Db.Item(g.hunt.ItemId).Name} {Pct(g.chance)} ", Felt.Good);

            string Names(string t) => string.Join(", ", table.Tiers[t].Select(e => Db.Item(e.ItemId).Name));
            var pityText = luck > 0
                ? $"Rolled rarest first. Luck: +{Pct(luck)} Rare after {misses} kills here without one."
                : $"Rolled rarest first. {misses} kills here without a Rare; after {Db.B.Loot.PityAfter}, the Rare chance rises.";
            var lines = Loot.Tiers.Where(t => odds[t] > 0).Select(t => $"{label[t]} {Pct(odds[t])}: {Names(t)}").ToList();
            if (setChance > 0) lines.Add($"Set {Pct(setChance)} (extra): {string.Join(", ", Db.Set(table.Set).Pieces.Select(id => Db.Item(id).Name))}");
            lines.AddRange(gather.Select(g => $"{Db.Item(g.hunt.ItemId).Name} {Pct(g.chance)} (extra): your Guild hunt in {zone.Name}."));
            lines.Add(pityText);
            InfoLine(x, y, rich, string.Join("\n", lines));
        }

        /// <summary>
        /// "[Earth]  Weak [Fire] [Wind] [Blunt] · Resists [Water]": the monster's element, weak spots, resists and absorbs,
        /// each a chip washed in its color (empty if it has none).
        /// </summary>
        List<VisualElement> AffinityParts()
        {
            var m = monster;
            var resists = m.Resists ?? new Dictionary<string, double>();
            double R(string e) => resists.TryGetValue(e, out var v) ? v : 0;
            // A bonus to every element (Warded Elites) is shown once as "All +25" instead of listing all eight.
            var all = R("all");
            var elements = Db.Data.Elements;
            var weak = elements.Where(e => R(e) + all <= -25).ToList();
            var strong = elements.Where(e => R(e) + all >= 25 && R(e) > 0 && R(e) + all <= 100).ToList();
            var absorbs = elements.Where(e => R(e) + all > 100).ToList();
            var parts = new List<VisualElement>();
            var seed = 80;
            void Word(string text)
            {
                var w = FlowText(text, 17, Felt.TextDim);
                w.style.marginLeft = parts.Count > 0 ? 8 : 0;
                w.style.marginRight = 6;
                parts.Add(w);
            }
            void Chip(string text, Color c)
            {
                var chip = Felt.Chip(text, c, 15, seed++);
                chip.style.marginRight = 5;
                parts.Add(chip);
            }
            if (m.Element != null) Chip(Db.Data.ElementLabels[m.Element], Palette.ElementText(m.Element));
            void Group(string label, List<string> list)
            {
                if (list.Count == 0) return;
                Word(label);
                foreach (var e in list) Chip(Db.Data.ElementLabels[e], Palette.ElementText(e));
            }
            Group("Weak", weak);
            // Weak spots besides elements: Blunt skills and Poison.
            var spots = new List<(string label, Color color)>();
            if (m.WeakTo?.Blunt > 0) spots.Add(("Blunt", Design.Hex(0x9a7048)));
            if (m.WeakTo?.Poison > 0) spots.Add(("Poison", Design.Css("#8fe36b")));
            if (spots.Count > 0)
            {
                if (weak.Count == 0) Word("Weak");
                foreach (var s in spots) Chip(s.label, s.color);
            }
            Group("Resists", strong);
            Group("Absorbs", absorbs);
            if (all != 0) Chip($"All {(all > 0 ? "+" : "")}{JsMath.Str(all)}", Design.Hex(0xc9a520));
            return parts;
        }

        /// <summary>The affinity row (again after a boss changes form).</summary>
        void DrawAffinities()
        {
            if (affinityY <= 0) return;
            affinityRow?.RemoveFromHierarchy();
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, LineX, affinityY, 0, 0.5f);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            foreach (var part in AffinityParts()) row.Add(part);
            enemyHud.Add(row);
            FitWidth(row, 358);
            affinityRow = row;
        }

        /// <summary>Catch the chips up with the battle (after a turn has finished playing).</summary>
        void SyncStatuses()
        {
            foreach (var side in new[] { "player", "enemy" })
                shown[side] = battle.Statuses(side).Select(s => new KeyValuePair<string, int>(s.Def.Id, s.Turns)).ToList();
            RefreshStatuses();
        }

        void SetShown(string side, string id, int turns)
        {
            var list = shown[side];
            var i = list.FindIndex(k => k.Key == id);
            if (i >= 0) list[i] = new KeyValuePair<string, int>(id, Math.Max(list[i].Value, turns));
            else list.Add(new KeyValuePair<string, int>(id, turns));
        }

        /// <summary>Chips under each patch: "Burn 2", "War Cry 3", on rose (harmful) or sage (helpful) felt.</summary>
        void RefreshStatuses()
        {
            foreach (var side in new[] { "player", "enemy" })
            {
                var row = side == "player" ? playerStatusRow : enemyStatusRow;
                if (row == null) continue;
                row.Clear();
                var seed = side == "player" ? 100 : 120;
                var before = chipsShown[side];
                foreach (var kv in shown[side])
                {
                    var def = Db.Status(kv.Key);
                    var chip = Felt.Chip($"{def.Name} {kv.Value}", def.Harmful ? Design.Hex(0xd9604a) : Design.Hex(0x6aa84f), 15, seed++);
                    chip.style.marginRight = 6;
                    row.Add(chip);
                    // A new status is sewn on with a pop; the ones already there just change their number.
                    if (before.Contains(kv.Key)) continue;
                    chip.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                    UiMotion.Pop(chip, 0.2f, 0.28f);
                }
                chipsShown[side] = new HashSet<string>(shown[side].Select(k => k.Key));
            }
        }

        /// <summary>The bottom: a felt runner with a stitched hem, the log on a linen patch sewn onto it.</summary>
        void DrawBottomPanel()
        {
            Felt.Runner(bottomHud, 540, 3);
            var note = Felt.MakePatch(bottomHud, 22, 562, 520, 140, 14, Felt.Patch.LinenLog(), 0, false);
            logBox = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(logBox, 26, 17);
            Ui.Size(logBox, 466, 108);
            logBox.style.overflow = Overflow.Hidden;
            logBox.style.flexDirection = FlexDirection.Column;
            note.Content.Add(logBox);
            menu = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(menu, 0, 0);
            bottomHud.Add(menu);
        }

        /// <summary>All bars caught up with the battle (the start, the end of a turn).</summary>
        void RefreshBars(bool animate = true)
        {
            var p = battle.Player;
            var e = battle.Enemy;
            hpShown["player"] = p.Hp;
            hpShown["enemy"] = e.Hp;
            playerHp.SetValue(p.Hp, p.MaxHp, animate);
            playerMp.SetValue(p.Mp, p.MaxMp, animate);
            enemyHp.SetValue(e.Hp, e.MaxHp, animate);
        }

        /// <summary>
        /// HP as shown while a turn plays back. The battle is already at the end of the turn, so each hit, heal and tick
        /// moves its side's bar by its own number (the hero's bar doesn't drop when the hero's blow lands).
        /// </summary>
        readonly Dictionary<string, double> hpShown = new Dictionary<string, double>();

        void ShowHp(string side, double delta)
        {
            var max = side == "player" ? battle.Player.MaxHp : battle.Enemy.MaxHp;
            var hp = Math.Max(0, Math.Min(max, (hpShown.TryGetValue(side, out var v) ? v : max) + delta));
            hpShown[side] = hp;
            (side == "player" ? playerHp : enemyHp).SetValue(hp, max);
            // Badly hurt fighters hunch and breathe hard.
            var bend = FighterOf(side)?.Fig?.Bend;
            if (bend != null) bend.Hurt = hp > 0 && hp < max * 0.3 ? 1 : 0;
        }

        /// <summary>MP changes come from the hero's own moves, so the battle's value is the one to show.</summary>
        void ShowMp() => playerMp.SetValue(battle.Player.Mp, battle.Player.MaxMp);

        void PushLog(string line)
        {
            log.Add(line);
            if (log.Count > LogLines) log.RemoveAt(0);
            RestoreLog();
        }

        // --- Menus ---

        MenuButton SkillButton(SkillDef skill)
        {
            var cd = battle.CooldownLeft(skill.Id);
            var element = skill.Element != null && skill.Element != "weapon" ? Db.Data.ElementLabels[skill.Element] : null;
            var fresh = battle.Player.Data.FreshSkills.Contains(skill.Id) ? "NEW · " : "";
            var poisons = skill.Applies?.Any(a => a.Status == "poison" && !a.Self) == true;
            var kind = element ?? (skill.Blunt ? "Blunt" : poisons ? "Poison" : skill.Kind == "heal" ? "Heal" : skill.Kind == "buff" ? "Buff" : null);
            // Teach weak spots: the button says when this skill lands on one.
            var effect = battle.SkillEffect(skill);
            var sub = cd > 0 ? $"Ready in {cd}" : $"{fresh}{JsMath.Str(skill.MpCost)} MP{(kind != null ? $" · {kind}" : "")}{(effect != null ? $" · {EffectLabels[effect]}" : "")}";
            var cdText = skill.Cooldown > 0 ? $" Cooldown {skill.Cooldown}." : "";
            var icon = skill.Kind == "heal" ? "cross" : skill.Kind == "buff" ? "rise" : skill.DamageType == "magic" ? "spark" : skill.DamageType == "pierce" ? "arrow" : "sword";
            return new MenuButton
            {
                Label = skill.Name, Sub = sub, Enabled = battle.CanUseSkill(skill.Id), Hover = $"{skill.Name}: {skill.Description}{cdText}",
                Run = () => Act(() => battle.UseSkill(skill.Id)), Accent = SkillColor(skill), Icon = icon,
            };
        }

        /// <summary>
        /// A skill's color: green heals, gold buffs, attacks in their element's color (a weapon-element attack in the
        /// weapon's), poison green-yellow, plain attacks steel.
        /// </summary>
        Color SkillColor(SkillDef skill)
        {
            if (skill.Kind == "heal") return Design.Hex(0x5cc85c);
            if (skill.Kind == "buff") return Design.Hex(0xe0b040);
            var element = skill.Element == "weapon" ? battle.Player.WeaponElement : skill.Element;
            if (element != null) return Design.Hex(ColorsOf(element)[1]);
            if (skill.Applies?.Any(a => a.Status == "poison" && !a.Self) == true) return Design.Css("#8fe36b");
            return Design.Hex(0x9fb4c8);
        }

        /// <summary>Swap to the spare weapon: its element and how it would land, or a hint when there's no spare.</summary>
        MenuButton SwapButton()
        {
            var p = battle.Player;
            var spare = p.SpareWeapon;
            var range = p.SpareAttackRange;
            if (p.Data.Equipment["spare"] == null || range == null)
                return new MenuButton
                {
                    Label = "Swap", Sub = "No spare", Enabled = false, Icon = "swap",
                    Hover = "Swap: put a second weapon in the Spare slot (Inventory) to switch to it mid-fight.", Run = () => { },
                };
            var element = spare?.Element;
            var effect = battle.EffectOn(element);
            var elName = element != null ? Db.Data.ElementLabels[element] : "Neutral";
            var name = spare?.Name ?? "bare hands";
            var type = Db.Data.DamageTypeLabels[spare?.DamageType ?? "melee"];
            return new MenuButton
            {
                Label = "Swap", Sub = $"{elName}{(effect != null ? $" · {EffectLabels[effect]}" : "")}", Enabled = battle.CanSwapWeapon, Icon = "swap",
                Hover = $"Swap to your {name} ({JsMath.Str(range[0])}-{JsMath.Str(range[1])} {type}, {elName}{(effect != null ? $", {EffectLabels[effect]}" : "")}). Costs your turn.",
                Run = () => Act(() => battle.SwapWeapon()),
            };
        }

        /// <summary>`turnStart`: the hero's turn begins (Attack pops up with a ring and a sound, the rest slide up after it).</summary>
        void ShowMenu(string which, bool turnStart = false)
        {
            menu.Clear();
            pressedHolder = null;
            var p = battle.Player;
            var buttons = new List<MenuButton>();

            if (which == "main")
            {
                var fresh = p.CarriedSkills.Count(sk => p.Data.FreshSkills.Contains(sk.Id));
                buttons.Add(new MenuButton { Label = "Attack", Icon = "sword", Main = true, Run = () => Act(() => battle.Attack()) });
                buttons.Add(new MenuButton { Label = "Skills", Icon = "book", Sub = fresh > 0 ? $"{fresh} NEW" : null, Run = () => ShowMenu("skills") });
                buttons.Add(new MenuButton { Label = "Items", Icon = "bag", Run = () => ShowMenu("items") });
                buttons.Add(new MenuButton { Label = "Defend", Icon = "shield", Sub = "-60% dmg, +MP", Run = () => Act(() => battle.Defend()) });
                buttons.Add(new MenuButton
                {
                    Label = "Flee", Icon = "boot", Sub = battle.CanFlee ? $"{Math.Round(battle.FleeChance * 100)}%" : "Boss", Enabled = battle.CanFlee,
                    Run = () => Act(() => battle.Flee()),
                });
                if (Features.On(p, "weapon_swap") || p.Data.Equipment["spare"] != null) buttons.Add(SwapButton());
            }
            else if (which == "skills")
            {
                var skills = p.CarriedSkills;
                if (skills.Count <= SkillsPerPage + 1) buttons.AddRange(skills.Select(SkillButton));
                else
                {
                    var pages = (int)Math.Ceiling(skills.Count / (double)SkillsPerPage);
                    skillPage %= pages;
                    buttons.AddRange(skills.Skip(skillPage * SkillsPerPage).Take(SkillsPerPage).Select(SkillButton));
                    buttons.Add(new MenuButton
                    {
                        Label = "More", Icon = "more", Sub = $"{skillPage + 1} / {pages}",
                        Run = () =>
                        {
                            skillPage += 1;
                            ShowMenu("skills");
                        },
                    });
                }
                buttons.Add(new MenuButton { Label = "Back", Icon = "back", Run = () => ShowMenu("main") });
            }
            else
            {
                var usable = p.Data.Inventory.Where(e => Db.Item(e.ItemId).Type == "consumable").Take(5).ToList();
                foreach (var entry in usable)
                {
                    var item = Db.Item(entry.ItemId);
                    buttons.Add(new MenuButton
                    {
                        Label = item.Name, Sub = $"x{entry.Qty}", Hover = $"{item.Name}: {item.Description}", Icon = "potion", ArtIcon = "icon_" + item.Id,
                        Accent = item.IconColor.HasValue ? Design.Hex(item.IconColor.Value) : (Color?)null,
                        Run = () =>
                        {
                            if (!battle.CanUseItem(item.Id)) PushLog($"{item.Name} would have no effect right now.");
                            else Act(() => battle.UseItem(item.Id));
                        },
                    });
                }
                if (usable.Count == 0) PushLog("You have no usable items.");
                buttons.Add(new MenuButton { Label = "Back", Icon = "back", Run = () => ShowMenu("main") });
            }

            // Felt patches sewn on the runner, a sewing button with its number key on each (1-6 work too).
            menuButtons = buttons;
            // The main menu leads with one big Attack, the rest smaller beside it; skill and item lists are an even 3 x 2
            // grid. All straight: the hierarchy comes from size and color (a lone tilted button reads as a mistake).
            var lead = which == "main";
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                float x, y, w, h, size, iconSize, subSize, angle;
                if (lead && i == 0)
                {
                    (w, h, size, iconSize, subSize, angle) = (220f, 132f, 36f, 40f, 15f, 0f);
                    (x, y) = (560 + w / 2, 566 + h / 2);
                }
                else if (lead)
                {
                    var j = i - 1;
                    (w, h, size, iconSize, subSize, angle) = (150f, 60f, 21f, 22f, 13f, 0f);
                    (x, y) = (798 + j % 3 * 158 + w / 2, 568 + j / 3 * 70 + h / 2);
                }
                else
                {
                    (w, h, size, iconSize, subSize, angle) = (228f, 64f, b.Label.Length > 12 ? 20f : 23f, 26f, 15f, 0f);
                    (x, y) = (560 + i % 3 * 239 + w / 2, 566 + i / 3 * 74 + h / 2);
                }
                // Attack is red felt with cream thread; skills and items are sand felt dyed toward their own color.
                Color? cloth = b.Main ? Felt.RedFelt : b.Accent is Color accent ? Color.Lerp(Felt.Sand, accent, 0.38f) : (Color?)null;
                // Each button sits in a holder that does the moving (the button's own translate is its hover lift).
                var holder = new VisualElement { pickingMode = PickingMode.Ignore, name = "slot" };
                Ui.Place(holder, 0, 0);
                holder.style.transformOrigin = new TransformOrigin(new Length(x, LengthUnit.Pixel), new Length(y, LengthUnit.Pixel));
                menu.Add(holder);
                var run = b.Run;
                b.Run = () =>
                {
                    pressedHolder = holder;
                    run();
                };
                var btn = new FeltButton(holder, x, y, b.Label, () => b.Run(), w, h, b.Sub, b.Icon, cloth, b.Main ? Felt.RedStitch : (Color?)null, 31 + i,
                    angle, $"{i + 1}", size, iconSize, subSize);
                if (b.ArtIcon != null && Art.Has(b.ArtIcon))
                {
                    // An item's own picture instead of the generic potion.
                    var ic = btn.Q("icon " + b.Icon);
                    if (ic != null) ic.style.backgroundImage = new StyleBackground(Art.Get(b.ArtIcon));
                }
                if ((b.Sub?.Length ?? 0) > 20)
                {
                    var texts = btn.Query<Label>().ToList();
                    if (texts.Count > 1) texts[1].style.fontSize = 13;
                }
                if (!b.Enabled) btn.SetEnabled(false);
                if (b.Hover != null)
                {
                    btn.RegisterCallback<PointerEnterEvent>(_ => ShowNote(b.Hover, 17));
                    btn.RegisterCallback<PointerLeaveEvent>(_ => RestoreLog());
                }
                if (turnStart && lead && i == 0)
                {
                    // Your turn: Attack springs up out of the runner, a ring of thread bursts from it.
                    UiMotion.FadeIn(holder, 0.08f);
                    UiMotion.Pop(holder, 0.35f, 0.38f, 0, Ease.ElasticOut);
                    UiMotion.Ring(menu, x, y, 120, Felt.RedStitch, 5, 0.5f, 0.05f);
                    Sfx.Play("turn");
                }
                else if (turnStart)
                {
                    UiMotion.FadeIn(holder, 0.14f, 0.1f + i * 0.045f);
                    UiMotion.SlideIn(holder, new Vector2(0, 46), 0.3f, 0.1f + i * 0.045f);
                }
                else
                {
                    // Into a submenu or back: the new buttons slide in from the right, one after another.
                    UiMotion.FadeIn(holder, 0.12f, i * 0.035f);
                    UiMotion.SlideIn(holder, new Vector2(54, 0), 0.26f, i * 0.035f);
                }
            }
        }

        /// <summary>The buttons leave as a turn plays: the one used punches out, the others drop into the runner.</summary>
        void DropMenu()
        {
            var holders = menu.Children().ToList();
            for (int i = 0; i < holders.Count; i++)
            {
                var hd = holders[i];
                // No more clicks on the way out (Skills or Back would rebuild the menu mid-turn).
                foreach (var el in hd.Query<VisualElement>().ToList()) el.pickingMode = PickingMode.Ignore;
                var from = hd.resolvedStyle.opacity;
                if (hd == pressedHolder)
                {
                    Anim.Float(0, 1, 0.22f, t =>
                    {
                        var s = 1 + 0.1f * Mathf.Sin(Mathf.Min(1, t * 1.6f) * Mathf.PI) - 0.15f * Mathf.Max(0, t - 0.5f);
                        hd.style.scale = new Scale(new Vector3(s, s, 1));
                        hd.style.opacity = from * (t < 0.45f ? 1 : 1 - (t - 0.45f) / 0.55f);
                    }, Ease.Linear).OnComplete(hd.RemoveFromHierarchy);
                    continue;
                }
                Anim.Float(from, 0, 0.16f, v => hd.style.opacity = v, Ease.Linear, i * 0.02f);
                UiMotion.SlideOut(hd, new Vector2(0, 60), 0.16f, i * 0.02f, Ease.QuadIn).OnComplete(hd.RemoveFromHierarchy);
            }
        }

        /// <summary>The HUD pieces slide off at the end of the fight (before the results).</summary>
        void HudOut()
        {
            FadeIntent(false);
            Sfx.Play("swish");
            UiMotion.SlideOut(heroHud, new Vector2(-HudOff.x, 0), 0.32f, 0.05f);
            UiMotion.SlideOut(enemyHud, new Vector2(HudOff.x, 0), 0.32f);
            UiMotion.SlideOut(bottomHud, new Vector2(0, HudOff.y), 0.3f, 0.1f);
        }

        // --- Turn playback ---

        async void Act(Func<List<BattleEvent>> action)
        {
            if (busy) return;
            var events = action();
            if (events.Count == 0) return;
            busy = true;
            menuButtons = new List<MenuButton>();
            DropMenu();
            RestoreLog();
            FadeIntent(false);
            finishers.Clear();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != "defeated") continue;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (events[j].Kind != "hit" || Other(events[j].Source) != events[i].Side) continue;
                    finishers.Add(events[j]);
                    break;
                }
            }
            for (int i = 0; i < events.Count; i++) await Play(events[i], i > 0 ? events[i - 1] : null, i + 1 < events.Count ? events[i + 1] : null);
            RefreshBars();
            SyncStatuses();
            RefreshIntent();
            RefreshEscape();
            RefreshWeaponTag();

            if (battle.IsOver)
            {
                await Anim.Wait(0.25f);
                HudOut();
                await Anim.Wait(0.45f);
                Finish();
            }
            else
            {
                busy = false;
                FadeIntent(true);
                ShowMenu("main", true);
            }
        }

        Fighter FighterOf(string side) => side == "player" ? hero : foe;
        static float HomeX(string side) => side == "player" ? PlayerX : EnemyX;
        string NameOf(string side) => side == "player" ? "You" : monster.Name;
        static long[] ColorsOf(string element) => element != null && Db.Data.ElementColors.TryGetValue(element, out var c) ? c : Db.Data.NeutralColors;
        static string Other(string side) => side == "player" ? "enemy" : "player";

        /// <summary>Top of a figure's body and its width, for marks over the head and death effects.</summary>
        (float top, float width) BodyOf(string side)
        {
            var f = FighterOf(side);
            var h = f.Fig != null ? f.Fig.DrawnHeight * f.Base : 120;
            var w = f.Fig != null ? f.Fig.DrawnWidth * f.Base : 80;
            return (Mathf.Clamp(GroundY - h, GroundY - 300, GroundY - 120), Mathf.Min(Mathf.Max(80, w), 260));
        }

        /// <summary>Color of a status's visuals: its own tint, else its damage element's, else red or green.</summary>
        static long StatusColor(StatusDef def, string element = null)
        {
            if (def.Tint.HasValue) return def.Tint.Value;
            var el = element ?? (def.Dot?.Element != null && def.Dot.Element != "weapon" ? def.Dot.Element : null);
            if (el != null) return ColorsOf(el)[1];
            return def.Harmful ? 0xff6b6b : 0x7dff9a;
        }

        Style StyleOf(BattleEvent e)
        {
            var colors = ColorsOf(e.Element);
            if (e.Source == "enemy")
                return new Style
                {
                    Kind = e.DamageType == "magic" ? "enemy-spit" : "enemy-melee",
                    Colors = e.Element != null ? colors : new[] { 0xffffffL, monster.Look.Color, monster.Look.Accent }, Element = e.Element,
                };
            if (e.MoveId == "proc") return new Style { Kind = "proc", Colors = colors, Element = e.Element };
            if (e.MoveId == "attack") return new Style { Kind = e.DamageType == "magic" ? "bolt" : "slash", Colors = colors, Element = e.Element };
            var skill = Db.Skill(e.MoveId);
            if (e.DamageType == "magic") return new Style { Kind = "bolt", Colors = colors, Element = e.Element, Big = skill.Fx == "heavy" };
            return new Style { Kind = skill.Fx == "heavy" ? "heavy" : "slash", Colors = colors, Element = e.Element };
        }

        /// <summary>Player hits of the same skill in a row (multi-hit skills); monster moves and weapon specials stand alone.</summary>
        static bool SameMove(BattleEvent a, BattleEvent b)
        {
            if (a == null || (a.Kind != "hit" && a.Kind != "miss") || (b.Kind != "hit" && b.Kind != "miss")) return false;
            return a.Source == "player" && b.Source == "player" && a.MoveId == b.MoveId && a.MoveId != "proc";
        }

        async Task Play(BattleEvent e, BattleEvent prev, BattleEvent next)
        {
            await PlayEvent(e, new Chain { First = !SameMove(prev, e), Last = next == null || !SameMove(e, next), Finisher = finishers.Contains(e) });
            // An enemy action resolves the player's guard and the boss's wind-up.
            var enemyActed = e.Kind == "windup" || ((e.Kind == "hit" || e.Kind == "miss" || e.Kind == "buff") && e.Source == "enemy");
            if (!enemyActed) return;
            if (e.Kind != "windup" && chargeAura != null)
            {
                UnityEngine.Object.Destroy(chargeAura.gameObject);
                chargeAura = null;
                chargeFlow?.Stop();
                chargeFlow = null;
                foe.Strain(0);
                Dim(0, 0.4f);
            }
            if (shield != null)
            {
                var s = shield;
                shield = null;
                hero.Pose(0, 0);
                Anim.Float(0, 1, 0.25f, t =>
                {
                    if (s == null) return;
                    s.Alpha = 1 - t;
                    s.transform.localScale = Vector3.one * (1 + 0.4f * t);
                }).OnComplete(() => { if (s != null) UnityEngine.Object.Destroy(s.gameObject); });
            }
        }

        void Float(float x, float y, string text, Color color, float size, string tag = null) => Ui.FloatText(x, y, text, color, size, tag);

        static string RestoreText(double hp, double mp)
        {
            var parts = new List<string>();
            if (hp != 0) parts.Add($"+{JsMath.Str(hp)} HP");
            if (mp != 0) parts.Add($"+{JsMath.Str(mp)} MP");
            return parts.Count > 0 ? string.Join(", ", parts) : "No effect";
        }

        async Task PlayEvent(BattleEvent e, Chain chain)
        {
            var enemyName = monster.Name;
            switch (e.Kind)
            {
                case "hit":
                case "miss":
                {
                    comboHits = chain.First ? 0 : comboHits + 1;
                    var style = StyleOf(e);
                    var target = Other(e.Source);
                    // Skills gather power first (spells do it in their own cast); plain attacks stay quick.
                    if (e.Source == "player" && chain.First && e.MoveId != "attack" && e.MoveId != "proc" && (style.Kind == "slash" || style.Kind == "heavy"))
                        await SkillCharge(hero, style, style.Kind == "heavy");
                    await AttackAnim(e.Source, style, () =>
                    {
                        if (e.Kind == "hit") Impact(e, style, chain);
                        else Dodge(target);
                    }, chain);
                    var who = e.Source == "player" ? "You use" : $"{enemyName} uses";
                    if (e.Kind == "hit" && e.ShellLeft.HasValue && monster.Shell != null)
                    {
                        var broke = e.ShellLeft == 0;
                        Sfx.Play(broke ? "explode" : "hit");
                        if (broke)
                        {
                            Fx.Shockwave(World, EnemyX, GroundY, 0xbfe9ff, 3, 0.3f);
                            Fx.ElementBurst(World, EnemyX, GroundY - Chest, "ice", 1);
                        }
                        Float(EnemyX, TextY("enemy", GroundY - 300), broke ? $"{monster.Shell.Name} broken!" : "Cracked!", Design.Css("#bfe9ff"), broke ? 34 : 26);
                        RefreshShell();
                    }
                    var dmg = JsMath.Str(e.Damage);
                    if (e.Kind == "hit" && e.Absorbed)
                        PushLog($"{who} {e.Move}, but {(e.Source == "player" ? enemyName : "you")} absorb{(e.Source == "player" ? "s" : "")} it: +{dmg} HP!");
                    else if (e.Kind == "hit")
                    {
                        var notes = (e.Crit ? " Critical!" : "") + (e.Effect == "weak" ? " Weak spot!" : "") + (e.Effect == "resisted" ? " Resisted." : "") + (e.Effect == "immune" ? " Immune!" : "");
                        PushLog(e.MoveId == "proc" ? $"{e.Move}! {dmg} damage.{notes}" : $"{who} {e.Move}: {dmg} damage.{notes}");
                    }
                    else PushLog(e.Source == "player" ? $"You use {e.Move}, but miss." : $"{enemyName} uses {e.Move}, but misses.");
                    // Combo hits follow each other quickly; the last one gets the usual pause.
                    await Anim.Wait(!chain.Last ? 0.12f : e.MoveId == "proc" ? 0.2f : 0.34f);
                    break;
                }
                case "heal":
                {
                    var x = HomeX(e.Source);
                    var f = FighterOf(e.Source);
                    var isSkill = Db.HasSkill(e.MoveId) || e.MoveId == "proc" || e.Source == "enemy";
                    long color = e.Mp > 0 && e.Hp == 0 ? 0x7ec8ff : isSkill ? 0x7dff9a : 0xff8a8a;
                    // Lifesteal and weapon specials heal right after the blow, without a wind-up.
                    var lifesteal = Db.HasSkill(e.MoveId) && Db.Skill(e.MoveId).Kind == "damage";
                    var afterHit = e.MoveId == "proc" || lifesteal;
                    if (lifesteal)
                    {
                        // Life pulled out of the target into the attacker.
                        Fx.Drain(World, new Vector2(HomeX(Other(e.Source)), GroundY - Chest), new Vector2(x, GroundY - Chest), 0xff6b8a);
                        await Anim.Wait(0.26f);
                    }
                    else if (!afterHit && isSkill)
                    {
                        Sfx.Play("charge");
                        Fx.CastCircle(World, x, GroundY, color, 280);
                        await Fx.ChargeUp(World, x + 30, GroundY - Chest, color, 280);
                    }
                    else if (!afterHit)
                    {
                        // A potion: a little hop.
                        await f.Track(Anim.Float(GroundY, GroundY - 20, 0.12f, v => f.Y = v, Ease.CubicOut, yoyo: true)).Task;
                        f.Y = GroundY;
                    }
                    Sfx.Play("heal");
                    Fx.HealSparkles(World, x, GroundY, color);
                    Fx.HealPluses(World, x, GroundY, color);
                    Fx.WhiteFlash(f.Fig, 140, color);
                    var restore = RestoreText(e.Hp, e.Mp);
                    Float(x, TextY(e.Source, GroundY - 220), restore, Palette.TextGood, 30);
                    ShowHp(e.Source, e.Hp);
                    ShowMp();
                    var user = e.Source == "player" ? "You use" : $"{enemyName} uses";
                    PushLog(afterHit ? $"{e.Move} restores {restore}." : $"{user} {e.Move}: {restore}.");
                    await Anim.Wait(afterHit ? 0.35f : 0.6f);
                    break;
                }
                case "buff":
                {
                    var x = HomeX(e.Source);
                    if (e.MoveId == "proc")
                    {
                        Float(EnemyX, TextY("enemy", GroundY - 270), $"{e.Move}!", Palette.TextGold, 26);
                        PushLog($"{e.Move}!");
                        await Anim.Wait(0.25f);
                        break;
                    }
                    var color = e.Source == "player" ? 0xffe08a : monster.Look.Accent;
                    Sfx.Play("charge");
                    Fx.CastCircle(World, x, GroundY, color, 320);
                    await Fx.ChargeUp(World, x, GroundY - Chest, color, 320);
                    Fx.Shockwave(World, x, GroundY, color, 2.2f, 0.35f);
                    Fx.Aura(World, x, GroundY, color);
                    PushLog($"{(e.Source == "player" ? "You use" : $"{enemyName} uses")} {e.Move}.");
                    await Anim.Wait(0.2f);
                    break;
                }
                case "status":
                {
                    var def = Db.Status(e.Status);
                    var x = HomeX(e.Target);
                    var color = StatusColor(def);
                    var head = BodyOf(e.Target).top;
                    Float(x, TextY(e.Target, GroundY - 280), def.Name, def.Harmful ? Palette.TextBad : Palette.TextGood, 26);
                    if (def.Stun)
                    {
                        Sfx.Play("stun");
                        Fx.StunStars(World, x, head - 10);
                    }
                    else
                    {
                        if (def.Harmful) Sfx.Play("debuff");
                        Fx.StatusMark(World, x, head - 20, color, !def.Harmful);
                        if (def.Harmful) Fx.StatusPuff(World, x, GroundY - Chest, color);
                    }
                    SetShown(e.Target, def.Id, e.Turns);
                    RefreshStatuses();
                    var target = e.Target == "player" ? "You are" : $"{enemyName} is";
                    PushLog($"{target} affected by {def.Name} for {e.Turns} turn{(e.Turns > 1 ? "s" : "")}.");
                    await Anim.Wait(0.3f);
                    break;
                }
                case "immune":
                {
                    Sfx.Play("block");
                    Float(HomeX(e.Target), TextY(e.Target, GroundY - 280), "Immune", Palette.TextDim, 26);
                    PushLog($"{NameOf(e.Target)} shrugs off {Db.Status(e.Status).Name}.");
                    await Anim.Wait(0.3f);
                    break;
                }
                case "tick":
                {
                    var def = Db.Status(e.Status);
                    var x = HomeX(e.Target);
                    var f = FighterOf(e.Target);
                    if (e.Damage > 0 || e.Heal == 0)
                    {
                        var color = e.Element != null ? Palette.ElementText(e.Element) : Palette.TextBad;
                        var tint = StatusColor(def, e.Element);
                        // Burn flickers with flames, Spores puff earth, Poison bubbles, Bleed drips.
                        if (e.Element != null && !def.Tint.HasValue) Fx.ElementBurst(World, x, GroundY - Chest, e.Element, 0.25f);
                        else Fx.StatusPuff(World, x, GroundY - Chest, tint);
                        Fx.HitSparks(World, x, GroundY - Chest, true, false, def.Tint.HasValue ? new[] { 0xffffffL, tint, tint } : ColorsOf(e.Element));
                        Fx.WhiteFlash(f.Fig, 100, 0xffffff, tint);
                        if (e.Target == "enemy" && e.Status == "bleed")
                        {
                            Fx.Bleed(World, x - 20, GroundY - Chest, Random.value < 0.5f ? -1 : 1, MonsterBlood, 0.3f, GroundY, GroundOrder);
                            Sfx.Play("splat");
                        }
                        Sfx.Play("hit");
                        var tilt = e.Target == "player" ? -4 : 4;
                        f.Track(Anim.Float(0, tilt, 0.06f, v => f.Angle = v, Ease.CubicOut, yoyo: true)).OnComplete(() => f.Angle = 0);
                        f.Squash(0.95f, 1.04f, 0.2f);
                        UiMotion.Jolt(e.Target == "player" ? heroHud : enemyHud, 3);
                        Float(x, TextY(e.Target, GroundY - 220), JsMath.Str(e.Damage), color, 28, e.Effect != null ? EffectTags[e.Effect] : def.Name);
                        PushLog($"{def.Name}: {JsMath.Str(e.Damage)} damage to {(e.Target == "player" ? "you" : enemyName)}.{(e.Effect == "weak" ? " Weak spot!" : "")}");
                    }
                    else
                    {
                        Fx.HealSparkles(World, x, GroundY, 0x7dff9a);
                        Fx.HealPluses(World, x, GroundY, 0x7dff9a);
                        Float(x, TextY(e.Target, GroundY - 220), $"+{JsMath.Str(e.Heal)}", Palette.TextGood, 28, def.Name);
                        PushLog($"{def.Name}: {(e.Target == "player" ? "you heal" : $"{enemyName} heals")} {JsMath.Str(e.Heal)} HP.");
                    }
                    ShowHp(e.Target, e.Damage > 0 || e.Heal == 0 ? -e.Damage : e.Heal);
                    await Anim.Wait(0.38f);
                    break;
                }
                case "expire":
                {
                    shown[e.Target].RemoveAll(k => k.Key == e.Status);
                    RefreshStatuses();
                    break;
                }
                case "stunned":
                {
                    var f = FighterOf(e.Side);
                    Sfx.Play("stun");
                    Fx.StunStars(World, HomeX(e.Side), BodyOf(e.Side).top - 10, 700);
                    Float(HomeX(e.Side), TextY(e.Side, GroundY - 280), "Stunned!", Palette.TextGold, 30);
                    PushLog(e.Side == "player" ? "You are stunned and lose your turn!" : $"{enemyName} is stunned!");
                    shown[e.Side].RemoveAll(k => k.Key == "stun");
                    RefreshStatuses();
                    await f.Track(Anim.Float(0, e.Side == "player" ? -6 : 6, 0.09f, v => f.Angle = v, Ease.CubicOut, repeat: 2, yoyo: true)).Task;
                    f.Angle = 0;
                    await Anim.Wait(0.25f);
                    break;
                }
                case "defend":
                {
                    Sfx.Play("open");
                    if (shield != null) UnityEngine.Object.Destroy(shield.gameObject);
                    var s = Shapes.Create(World, PlayerX, GroundY - 110, Design.Depth(GroundY) + 10, "shield");
                    s.Circle(Design.Hex(GuardColor, 0.12f), 0, 0, 120);
                    s.StrokeEllipse(Design.Hex(GuardColor, 0.9f), 6, 0, 0, 240, 240, 48);
                    s.transform.localScale = Vector3.one * 0.2f;
                    shield = s;
                    Anim.Float(0.2f, 1, 0.22f, v => { if (s != null) s.transform.localScale = new Vector3(v, v, 1); }, Ease.BackOut);
                    // A slow shimmer while it holds.
                    Anim.Float(1, 0.75f, 0.5f, a => { if (s != null && shield == s) s.Alpha = a; }, Ease.SineInOut, 0.22f, -1, true);
                    Fx.Aura(World, PlayerX, GroundY, GuardColor);
                    hero.Pose(-3, 0.45f);
                    Float(PlayerX, GroundY - 290, e.Mp > 0 ? $"Defend  +{JsMath.Str(e.Mp)} MP" : "Defend", Palette.TextMagic, 28);
                    PushLog("You brace yourself.");
                    ShowMp();
                    await Anim.Wait(0.35f);
                    break;
                }
                case "swap":
                {
                    // A quick turn: the hero spins away, comes back holding the other weapon.
                    var p = battle.Player;
                    hero.Kill();
                    Sfx.Play("equip");
                    await hero.Track(Anim.Float(1, 0, 0.11f, v => hero.Sx = v, Ease.QuadIn)).Task;
                    UnityEngine.Object.Destroy(hero.Fig.gameObject);
                    hero.Fig = Figure.Create(World, PlayerX, GroundY, PlayerLook, 1, p.Weapon, p.WeaponSigil);
                    hero.Fig.EnableBend();
                    hero.Base = (float)PlayerLook.Scale * StageScale;
                    hero.ReadWeapons();
                    if (p.WeaponSigil != null) Fx.SigilGlow(hero.Fig, p.WeaponSigil);
                    hero.Sx = 0;
                    hero.Apply();
                    await hero.Track(Anim.Float(0, 1, 0.14f, v => hero.Sx = v, Ease.BackOut)).Task;
                    hero.Sx = 1;
                    Idle(hero);
                    var color = e.Element != null ? ColorsOf(e.Element)[1] : 0xffffff;
                    Fx.Aura(World, PlayerX, GroundY, color);
                    if (e.Element != null) Fx.ElementBurst(World, PlayerX + 60, GroundY - Chest, e.Element, 0.4f);
                    var label = e.Effect != null ? $"{e.Weapon}  {EffectLabels[e.Effect]}" : e.Weapon;
                    Float(PlayerX, GroundY - 290, label, e.Element != null ? Palette.ElementText(e.Element) : Palette.Text, 28);
                    var how = new Dictionary<string, string>
                    {
                        ["weak"] = $" {enemyName} is weak to it!", ["resisted"] = $" {enemyName} resists it.", ["immune"] = $" {enemyName} is immune to it.",
                        ["absorb"] = $" {enemyName} absorbs it!",
                    };
                    var said = e.Effect != null ? how[e.Effect] : "";
                    PushLog($"You swap to your {e.Weapon}{(e.Element != null ? $" ({Db.Data.ElementLabels[e.Element]})" : "")}.{said}");
                    RefreshWeaponTag();
                    await Anim.Wait(0.35f);
                    break;
                }
                case "windup":
                {
                    Sfx.Play("warning");
                    PushLog(e.Text);
                    GameRoot.I.Shake(0.25f, 5);
                    Float(EnemyX, TextY("enemy", GroundY - 260), $"{e.Move}!", Palette.TextBad, 34);
                    if (chargeAura == null)
                    {
                        var aura = Props.Glow(World, EnemyX, GroundY - 120, Design.Hex(0xff3b3b, 0.5f), 8, 8, Design.Depth(GroundY) - 1);
                        chargeAura = aura;
                        Anim.Float(0, 1, 0.45f, t =>
                        {
                            if (aura == null) return;
                            var sc = Mathf.Lerp(8, 10, t) / 2;
                            aura.transform.localScale = new Vector3(sc, sc, 1);
                            aura.color = Design.Hex(0xff3b3b, Mathf.Lerp(0.5f, 0.8f, t));
                        }, Ease.Linear, 0, -1, true);
                    }
                    foe.Track(Anim.Float(EnemyX, EnemyX + 25, 0.18f, v => foe.X = v, Ease.QuadOut, yoyo: true));
                    // It sinks back and strains, shaking, while red power rises around it and gathers in its body; it
                    // holds the pose until it lets go next turn.
                    foe.Pose(-10, 0.8f);
                    foe.Strain(2.4f);
                    Dim(0.22f, 0.3f);
                    Sfx.Play("charge");
                    Fx.Dust(World, EnemyX - 60, GroundY, 4, Design.Depth(GroundY) + 1);
                    Fx.Dust(World, EnemyX + 60, GroundY, 4, Design.Depth(GroundY) + 1);
                    var body = BodyOf("enemy");
                    if (chargeFlow == null)
                        chargeFlow = Fx.Stream(World, EnemyX, GroundY, Fx.Tex.Soft, new Fx.Emit
                        {
                            Zone = new Rect(-body.width / 2, -12, body.width, 24),
                            SpeedY = Fx.Val.Rand(-300, -140),
                            SpeedX = Fx.Val.Rand(-20, 20),
                            Scale = Fx.Val.Life(1.6f, 0.2f),
                            Alpha = Fx.Val.Life(0.9f, 0),
                            Lifespan = 800,
                            Tints = new[] { 0xff2a2aL, 0xff6a3a, 0xc81e1e },
                            Frequency = 22,
                        });
                    await Fx.ChargeUp(World, EnemyX - 20, (body.top + GroundY) / 2, 0xff3b3b, 520);
                    Fx.Glint(World, EnemyX - 20, (body.top + GroundY) / 2, 0xffd0c0, 2.2f);
                    Sfx.Play("glint");
                    await Anim.Wait(0.25f);
                    break;
                }
                case "enrage":
                {
                    Sfx.Play("warning");
                    GameRoot.I.Flash(0.2f, new Color(140 / 255f, 0, 0));
                    Float(EnemyX, TextY("enemy", GroundY - 290), $"Enraged! +{Math.Round(e.Bonus * 100)}% damage", Palette.TextBad, 30);
                    PushLog($"The fight drags on: {enemyName} is enraged (+{Math.Round(e.Bonus * 100)}% damage).");
                    RefreshIntent();
                    await Anim.Wait(0.5f);
                    break;
                }
                case "rage":
                {
                    Float(PlayerX + 70, GroundY - 170, $"Rage +{JsMath.Str(e.Mp)} MP", Palette.TextMagic, 22);
                    ShowMp();
                    break;
                }
                case "phase":
                {
                    // The battle already holds the new form; catch the screen's copy up and show it off.
                    monster = battle.Enemy.Def;
                    var colors = ColorsOf(e.Element);
                    Sfx.Play("warning");
                    GameRoot.I.Flash(0.35f, Design.Hex(colors[1]));
                    GameRoot.I.Shake(0.3f, 9);
                    Fx.Shockwave(World, EnemyX, GroundY, colors[1], 4, 0.4f);
                    Fx.Explosion(World, EnemyX, GroundY - Chest, colors);
                    Fx.ElementBurst(World, EnemyX, GroundY - Chest, e.Element, 1);
                    foe.Fig.SetTint(Design.Hex(colors[1]));
                    Float(EnemyX, TextY("enemy", GroundY - 290), $"{Db.Data.ElementLabels[e.Element]} form!", Palette.ElementText(e.Element), 40);
                    PushLog(e.Text);
                    DrawAffinities();
                    RefreshIntent();
                    RefreshWeaponTag();
                    await Anim.Wait(1.1f);
                    break;
                }
                case "shell":
                {
                    shellRegrew = true;
                    Sfx.Play("warning");
                    Fx.Shockwave(World, EnemyX, GroundY, 0xbfe9ff, 4, 0.35f);
                    Float(EnemyX, TextY("enemy", GroundY - 290), $"{monster.Shell?.Name ?? "Shell"} grows back!", Design.Css("#bfe9ff"), 32);
                    PushLog(e.Text);
                    RefreshShell();
                    await Anim.Wait(0.9f);
                    break;
                }
                case "flee":
                {
                    var f = hero;
                    if (e.Success)
                    {
                        PushLog("You got away safely.");
                        Sfx.Play("miss");
                        f.Kill();
                        f.Facing = -1;
                        for (int i = 0; i < 4; i++) Anim.After(i * 0.09f, () => Fx.Dust(World, f.X, GroundY, 3, Design.Depth(GroundY) + 1));
                        await f.Track(Anim.Float(f.X, -200, 0.52f, v => f.X = v, Ease.CubicIn)).Task;
                    }
                    else
                    {
                        PushLog("You couldn't get away!");
                        f.Facing = -1;
                        await f.Track(Anim.Float(PlayerX, PlayerX - 60, 0.18f, v => f.X = v, Ease.QuadOut)).Task;
                        f.Facing = 1;
                        Fx.Dust(World, f.X, GroundY, 5, Design.Depth(GroundY) + 1);
                        await f.Track(Anim.Float(PlayerX - 60, PlayerX, 0.22f, v => f.X = v, Ease.BackOut)).Task;
                    }
                    break;
                }
                case "escape":
                {
                    var f = foe;
                    f.Kill();
                    PushLog($"{enemyName} darts away with its gold!");
                    Sfx.Play("miss");
                    Float(EnemyX, TextY("enemy", GroundY - 260), "Got away!", LuckyText, 34);
                    Fx.HealSparkles(World, f.X, GroundY, 0xffe066);
                    await f.Track(Anim.Float(GroundY, GroundY - 60, 0.16f, v => f.Y = v, Ease.QuadOut, yoyo: true)).Task;
                    f.Y = GroundY;
                    for (int i = 0; i < 4; i++) Anim.After(i * 0.08f, () => Fx.Dust(World, f.X, GroundY, 3, Design.Depth(GroundY) + 1));
                    await f.Track(Anim.Float(f.X, Design.Width + 220, 0.48f, v => f.X = v, Ease.CubicIn)).Task;
                    break;
                }
                case "defeated":
                {
                    var f = FighterOf(e.Side);
                    f.Kill();
                    recoil = null;
                    if (e.Side == "enemy")
                    {
                        var m = monster;
                        var body = BodyOf("enemy");
                        var colors = m.Element != null ? ColorsOf(m.Element) : new[] { 0xffffffL, m.Look.Color, m.Look.Accent };
                        PushLog($"{enemyName} is defeated!");
                        Fx.HitStop(m.Boss ? 260 : 160);
                        Fx.WhiteFlash(f.Fig, 140);
                        var bend = f.Fig.Bend;
                        if (bend != null)
                        {
                            bend.Idle = false;
                            bend.Hurt = 0;
                        }
                        var blood = MonsterBlood;
                        var kind = blood?.Kind ?? "liquid";
                        var gold = battle.Rewards?.Gold ?? 0;
                        // Rocked back off balance by the blow.
                        f.Pose(-22, 0.25f);
                        var fromX = f.X;
                        await f.Track(Anim.Float(0, 1, 0.22f, t =>
                        {
                            f.X = Mathf.Lerp(fromX, EnemyX + 40, t);
                            f.Angle = 8 * t;
                        }, Ease.QuadOut)).Task;
                        if (m.Boss)
                        {
                            // A boss goes down in a chain of blasts, shaking, spilling with each.
                            f.Strain(3);
                            for (int i = 0; i < 3; i++)
                            {
                                var bx = EnemyX + Random.Range(-body.width / 2, body.width / 2);
                                var by = Random.Range(body.top + 20, GroundY - 40);
                                Fx.Explosion(World, bx, by, colors);
                                Fx.Bleed(World, bx, by, Random.value < 0.5f ? -1 : 1, blood, 1.2f, GroundY, GroundOrder);
                                Fx.WhiteFlash(f.Fig, 90);
                                Sfx.Play("explode");
                                GameRoot.I.Shake(0.12f, 7);
                                await Anim.Wait(0.2f);
                            }
                            f.Strain(0);
                            GameRoot.I.Flash(0.26f, Color.white);
                            Fx.Shockwave(World, EnemyX, GroundY, colors[1], 5, 0.3f);
                        }
                        // The camera eases back out so the whole fall shows.
                        ViewOut(0.5f);
                        if (kind == "chips") await Shatter(f, body, blood, colors, gold);
                        else if (kind == "wisps") await FadeAway(f, body, blood, colors, gold);
                        else if (m.Look.Shape == "humanoid") await Topple(f, blood, colors, gold);
                        else await Slump(f, body, blood, colors, gold);
                    }
                    else
                    {
                        PushLog("You collapse...");
                        Sfx.Play("defeat");
                        GameRoot.I.Flash(0.3f, new Color(160 / 255f, 0, 0));
                        GameRoot.I.Shake(0.25f, 8);
                        await f.Track(Anim.Float(0, 1, 0.6f, t =>
                        {
                            f.Angle = -80 * t;
                            f.Fig.SetAlpha(1 - 0.5f * t);
                        }, Ease.BounceOut)).Task;
                        ViewOut();
                    }
                    break;
                }
            }
        }

        /// <summary>Cock the weapons a figure holds back (if its art shows them) `back` degrees, ready to swing.</summary>
        static void Cock(Fighter f, float seconds, float back = 40)
        {
            foreach (var t in f.WeaponTweens) t.Stop();
            f.WeaponTweens.Clear();
            for (int i = 0; i < f.Fig.Weapons.Count; i++)
            {
                var w = f.Fig.Weapons[i];
                var baseAngle = f.WeaponAngles[i];
                var from = w.localEulerAngles.z;
                f.WeaponTweens.Add(Anim.Float(0, 1, seconds, t => { if (w != null) w.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(from, baseAngle + back, t)); }, Ease.CubicOut));
            }
        }

        /// <summary>Swing the weapons a figure holds through the target and settle back.</summary>
        static void Swing(Fighter f, bool big = false)
        {
            foreach (var t in f.WeaponTweens) t.Stop();
            f.WeaponTweens.Clear();
            for (int i = 0; i < f.Fig.Weapons.Count; i++)
            {
                var w = f.Fig.Weapons[i];
                var baseAngle = f.WeaponAngles[i];
                var from = w.localEulerAngles.z;
                var through = baseAngle - (big ? 110 : 80);
                var h = Anim.Float(0, 1, big ? 0.11f : 0.08f, t => { if (w != null) w.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(from, through, t)); }, Ease.QuadIn);
                h.OnComplete(() => f.WeaponTweens.Add(Anim.Float(0, 1, big ? 0.22f : 0.16f, t => { if (w != null) w.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(through, baseAngle, t)); }, Ease.QuadOut)));
                f.WeaponTweens.Add(h);
            }
        }

        /// <summary>Close the camera in on (x, y) at `zoom`, kept inside the picture (no black edges past the backdrop).</summary>
        static Anim.Handle ViewIn(float x, float y, float zoom, float seconds, Ease ease = Ease.QuadOut)
        {
            float halfW = Design.Width / 2f / zoom, halfH = Design.Height / 2f / zoom;
            return GameRoot.I.View(Mathf.Clamp(x, halfW, Design.Width - halfW), Mathf.Clamp(y, halfH, Design.Height - halfH), zoom, seconds, ease);
        }

        static void ViewOut(float seconds = 0.45f) => GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1, seconds, Ease.CubicInOut);

        // --- Monster deaths ---

        /// <summary>A body hitting the ground along `length` px centered at x: a thud, a shake and dust along it.</summary>
        void BodyLands(float x, float length)
        {
            Sfx.Play("thud");
            GameRoot.I.Shake(0.16f, 7);
            for (int i = 0; i < 5; i++) Fx.Dust(World, x - length / 2 + length * i / 4f, GroundY + 4, 4, Design.Depth(GroundY) + 1);
        }

        /// <summary>The remains go up in smoke and motes over `w` x `h` px centered at x, the gold flies to the counter, and the body fades.</summary>
        async Task Vanish(Fighter f, float x, float w, float h, long[] colors, double gold, float fade = 0.4f)
        {
            Fx.DeathPoof(World, x, GroundY - h / 2, monster.Look.Color);
            Fx.Dissolve(World, x, GroundY, w, h, colors);
            Sfx.Play("explode");
            _ = Fx.CoinShower(World, new Vector2(x, GroundY - 100), new Vector2(60, 40), (int)Math.Ceiling(gold / 2), () => Sfx.Play("coin"));
            await f.Track(Anim.Float(0, 1, fade, t => f.Fig.SetAlpha(1 - t), Ease.CubicIn)).Task;
        }

        /// <summary>Upright monsters: the knees give, it pitches forward onto its face, bounces once, and a pool spreads under it.</summary>
        async Task Topple(Fighter f, Blood blood, long[] colors, double gold)
        {
            f.Pose(12, 1);
            await Anim.Wait(0.16f);
            var length = Mathf.Min(f.Fig.DrawnHeight * f.Base * 0.92f, 400);
            var from = f.Angle;
            Sfx.Play("whoosh");
            await f.Track(Anim.Float(0, 1, 0.42f, t => f.Angle = Mathf.LerpUnclamped(from, -84, t), Ease.CubicIn)).Task;
            var mid = f.X - length / 2;
            BodyLands(mid, length);
            f.Track(Anim.Float(-84, -77, 0.09f, v => f.Angle = v, Ease.QuadOut, yoyo: true));
            Fx.Pool(World, f.X - length * 0.45f, GroundY + 4, length * 0.85f, blood, GroundOrder);
            await Anim.Wait(0.6f);
            await Vanish(f, mid, length, 70, colors, gold);
        }

        /// <summary>Beasts, blobs and chests: it sags flat, head down, and a pool spreads under it.</summary>
        async Task Slump(Fighter f, (float top, float width) body, Blood blood, long[] colors, double gold)
        {
            f.Pose(26, 1);
            await f.Track(Anim.Float(0, 1, 0.34f, t =>
            {
                f.Angle = Mathf.LerpUnclamped(8, -5, t);
                f.Sy = 1 - 0.24f * t;
                f.Sx = 1 + 0.08f * t;
            }, Ease.BounceOut)).Task;
            BodyLands(f.X, body.width);
            Fx.Pool(World, f.X - 10, GroundY + 6, body.width, blood, GroundOrder);
            await Anim.Wait(0.6f);
            await Vanish(f, f.X, body.width * 0.8f, (GroundY - body.top) * 0.6f, colors, gold);
        }

        /// <summary>Stone, bark, shell and wood: it shakes, cracks white, and bursts into bits.</summary>
        async Task Shatter(Fighter f, (float top, float width) body, Blood blood, long[] colors, double gold)
        {
            // Cracks of light: a few quick flashes while it shakes.
            f.Strain(4);
            Sfx.Play("quake");
            for (int i = 0; i < 3; i++)
            {
                Fx.WhiteFlash(f.Fig, 60, 0xffffff);
                await Anim.Wait(0.13f);
            }
            f.Strain(0);
            for (int i = 0; i < 4; i++)
                Fx.Bleed(World, f.X + Random.Range(-body.width / 2, body.width / 2), Random.Range(body.top + 20, GroundY - 30), i % 2 == 0 ? -1 : 1, blood, 2,
                    GroundY, GroundOrder);
            Fx.Shockwave(World, f.X, GroundY, blood?.Color ?? 0xffffff, 4, 0.3f);
            GameRoot.I.Shake(0.18f, 8);
            Sfx.Play("thud");
            f.Track(Anim.Float(0, 1, 0.18f, t =>
            {
                f.Sx = 1 + 0.3f * t;
                f.Sy = 1 - 0.5f * t;
            }, Ease.QuadOut));
            await Vanish(f, f.X, body.width * 0.8f, GroundY - body.top, colors, gold, 0.18f);
        }

        /// <summary>Spirits: it shudders and rises, its essence pouring off in bursts, and thins away.</summary>
        async Task FadeAway(Fighter f, (float top, float width) body, Blood blood, long[] colors, double gold)
        {
            f.Strain(2);
            f.Pose(-12, 0);
            Sfx.Play("void");
            for (int i = 0; i < 3; i++)
                Anim.After(i * 0.18f, () => Fx.Bleed(World, f.X + Random.Range(-body.width / 3, body.width / 3), Random.Range(body.top + 20, GroundY - 40),
                    Random.value < 0.5f ? -1 : 1, blood, 1.6f, GroundY, GroundOrder));
            await f.Track(Anim.Float(0, 1, 0.6f, t =>
            {
                f.Y = GroundY - 50 * t;
                f.Sx = 1 - 0.3f * t;
                f.Sy = 1 + 0.2f * t;
            }, Ease.QuadIn)).Task;
            f.Strain(0);
            await Vanish(f, f.X, body.width * 0.8f, GroundY - body.top, colors, gold, 0.3f);
        }

        /// <summary>Where a fighter's weapon hand is (design px), or in front of its chest when it holds nothing drawn.</summary>
        static Vector2 HandPoint(Fighter f, int dir)
        {
            if (f.Fig != null && f.Fig.Weapons.Count > 0 && f.Fig.Weapons[0] != null) return Design.FromWorld(f.Fig.Weapons[0].position);
            return new Vector2(f.X + 50 * dir, GroundY - Chest - 10);
        }

        /// <summary>Where a held weapon's tip is (design px): up its blade from the hand. Null without one.</summary>
        static Vector2? BladeTip(Fighter f)
        {
            if (f.Fig == null || f.Fig.Weapons.Count == 0 || f.Fig.Weapons[0] == null) return null;
            var pivot = f.Fig.Weapons[0];
            var reach = f.Fig.DrawnHeight * f.Base * 0.38f / Design.PixelsPerUnit;
            return Design.FromWorld(pivot.position + pivot.up * reach);
        }

        /// <summary>
        /// A skill gathering power before the blow: the hero sinks into a crouch and strains (shaking), the weapon
        /// cocks, power in the skill's color gathers to the weapon hand over a glow on the ground, and a glint runs to the
        /// tip as it peaks.
        /// </summary>
        async Task SkillCharge(Fighter f, Style style, bool heavy)
        {
            var dir = f == hero ? 1 : -1;
            long color = style.Element != null ? style.Colors[1] : 0xffe08a;
            Sfx.Play("charge");
            Dim(0.25f, 0.15f);
            f.Pose(-7, heavy ? 0.9f : 0.6f);
            f.Strain(heavy ? 2.2f : 1.4f);
            Cock(f, 0.2f);
            Fx.CastCircle(World, f.X, GroundY, color, heavy ? 520 : 380);
            Fx.Dust(World, f.X - 50, GroundY, 3, Design.Depth(GroundY) + 1);
            Fx.Dust(World, f.X + 50, GroundY, 3, Design.Depth(GroundY) + 1);
            var hand = HandPoint(f, dir);
            await Fx.ChargeUp(World, hand.x, hand.y, color, heavy ? 520 : 380);
            f.Strain(0);
            var tip = BladeTip(f) ?? hand;
            Fx.Glint(World, tip.x, tip.y, 0xffffff, heavy ? 2.2f : 1.7f);
            Sfx.Play("glint");
            Dim(0, 0.35f);
        }

        // --- A blow in four beats (Disney's principles): anticipation, action, follow-through, recovery ---

        /// <summary>
        /// Anticipation: ease back into a coiled pose (weight back and down, leaning away, the weapon cocked far back;
        /// slow into it), then hold a beat with the muscles straining so the eye reads what's coming. `power` 0-1 is how
        /// big the coming blow is: deeper, further back, longer.
        /// </summary>
        static async Task Anticipate(Fighter f, int dir, float seconds, float hold, float power)
        {
            Cock(f, seconds, 40 + 30 * power);
            f.Shape(1.06f + 0.07f * power, 0.94f - 0.07f * power, seconds, Ease.CubicOut);
            f.Pose(-(7 + 7 * power), 0.45f + 0.5f * power);
            var fromX = f.X;
            var toX = f.X - (20 + 18 * power) * dir;
            var toA = -(6 + 6 * power) * dir;
            await f.Track(Anim.Float(0, 1, seconds, t =>
            {
                f.X = Mathf.Lerp(fromX, toX, t);
                f.Angle = toA * t;
            }, Ease.CubicOut)).Task;
            if (hold <= 0) return;
            f.Strain(0.8f + power);
            await Anim.Wait(hold);
            f.Strain(0);
        }

        /// <summary>
        /// The action: a push-off, then fast in (accelerating) to `to` on a low arc, stretched long along the motion and
        /// leaning in, with afterimages and speed lines behind.
        /// </summary>
        Task Lunge(Fighter f, string side, int dir, float to, float seconds, float hop, long trail)
        {
            Sfx.Play("whoosh");
            Fx.Dust(World, f.X - 20 * dir, GroundY, 3, Design.Depth(GroundY) + 1);
            Afterimages(side, seconds * 1000, trail);
            Fx.SpeedLines(World, f.X, GroundY, dir, trail);
            f.Shape(1.2f, 0.86f, seconds * 0.6f);
            f.Pose(16, 0.15f);
            var fromX = f.X;
            var fromA = f.Angle;
            return Tw(f, seconds, t =>
            {
                f.X = Mathf.LerpUnclamped(fromX, to, Anim.Apply(Ease.QuadIn, t));
                f.Y = GroundY - Mathf.Sin(t * Mathf.PI) * hop;
                f.Angle = Mathf.Lerp(fromA, 5 * dir, t);
            }, Ease.Linear);
        }

        /// <summary>Follow-through: the body squashes on contact and carries on past the blow, pitched in, then settles.</summary>
        async Task FollowThrough(Fighter f, int dir, float distance, float seconds)
        {
            f.Y = GroundY;
            f.Squash(0.9f, 1.08f, 0.22f);
            f.Pose(24, 0.4f);
            var fromX = f.X;
            await Tw(f, seconds, t => f.X = fromX + distance * dir * t, Ease.QuadOut);
            f.Pose(10, 0.25f);
        }

        /// <summary>Recovery: back home on a little hop (an arc, slow out and slow in), landing with a small squash.</summary>
        async Task Recover(Fighter f, float home, float seconds, float hop = 16)
        {
            f.Pose(-3, 0);
            var fromX = f.X;
            var fromA = f.Angle;
            await Tw(f, seconds, t =>
            {
                var e = Anim.Apply(Ease.SineInOut, t);
                f.X = Mathf.LerpUnclamped(fromX, home, e);
                f.Y = GroundY - Mathf.Sin(t * Mathf.PI) * hop;
                f.Angle = Mathf.Lerp(fromA, 0, e);
            }, Ease.Linear);
            f.Y = GroundY;
            f.Squash(1.06f, 0.94f, 0.18f);
            f.Pose(0, 0);
        }

        /// <summary>
        /// Staging: darken the backdrop (not the fighters or effects) to `alpha` over `seconds`, so a big moment stands
        /// out. 0 brings it back.
        /// </summary>
        void Dim(float alpha, float seconds = 0.2f)
        {
            if (dim == null)
            {
                var go = new GameObject("dim");
                go.transform.SetParent(World, false);
                go.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
                go.transform.localScale = new Vector3(Design.Width * 1.6f, Design.Height * 1.6f, 1);
                dim = go.AddComponent<SpriteRenderer>();
                dim.sprite = Art.White;
                dim.sharedMaterial = Fx.AlphaMaterial;
                dim.sortingOrder = GroundOrder + 5;
                dim.color = new Color(0.04f, 0.03f, 0.08f, 0);
            }
            var d = dim;
            var from = d.color.a;
            dimTween?.Stop();
            dimTween = Anim.Float(from, alpha, seconds, a =>
            {
                if (d != null) d.color = new Color(0.04f, 0.03f, 0.08f, a);
            }, Ease.QuadOut);
        }

        /// <summary>A fading silhouette of a figure where it stands now (behind it).</summary>
        void Ghost(string side, long color, float alpha = 0.4f, float ms = 240)
        {
            var f = FighterOf(side);
            if (f.Fig == null) return;
            var copy = side == "player"
                ? Figure.Create(World, f.X, f.Y, PlayerLook, 1, Session.Player.Weapon)
                : Figure.Create(World, f.X, f.Y, monster.Look, -1);
            copy.transform.localScale = f.Fig.transform.localScale;
            copy.transform.localRotation = f.Fig.transform.localRotation;
            if (f.Fig.Bend != null) copy.EnableBend()?.Freeze(f.Fig.Bend);
            copy.Order = f.Fig.Order - 1;
            Fx.FadeGhost(copy, color, alpha, ms);
        }

        /// <summary>Afterimages trailing a lunge that takes `ms`.</summary>
        void Afterimages(string side, float ms, long color)
        {
            for (int i = 0; i < 3; i++)
            {
                var k = i;
                Anim.After((ms / 3 * k + 20) / 1000f, () => Ghost(side, color, 0.35f - k * 0.07f, 220));
            }
        }

        Task Tw(Fighter f, float seconds, Action<float> step, Ease ease, bool yoyo = false) => f.Track(Anim.Float(0, 1, seconds, step, ease, yoyo: yoyo)).Task;

        Task MoveX(Fighter f, float to, float seconds, Ease ease, float angleTo = float.NaN)
        {
            var fromX = f.X;
            var fromA = f.Angle;
            return Tw(f, seconds, t =>
            {
                f.X = Mathf.LerpUnclamped(fromX, to, t);
                if (!float.IsNaN(angleTo)) f.Angle = Mathf.LerpUnclamped(fromA, angleTo, t);
            }, ease);
        }

        /// <summary>Attacker's animation. `onContact` fires at the moment the blow lands.</summary>
        async Task AttackAnim(string source, Style style, Action onContact, Chain chain)
        {
            var f = FighterOf(source);
            var dir = source == "player" ? 1 : -1;
            var home = HomeX(source);
            var target = new Vector2(HomeX(Other(source)), GroundY - Chest);
            long trail = style.Element != null ? style.Colors[1] : source == "player" ? 0x9fd3ff : MonsterHitColor;
            var reach = target.x - 170 * StageScale * dir;

            // Combo hits after the first: already in reach, a quick step back and in, then the next blow.
            if (!chain.First && style.Kind != "bolt" && style.Kind != "proc")
            {
                // A small step back (a quick anticipation), then in again.
                var fromX = f.X;
                var back = reach - 32 * dir;
                f.Pose(-5, 0.35f);
                Cock(f, 0.05f);
                await Tw(f, 0.05f, t => f.X = Mathf.LerpUnclamped(fromX, back, t), Ease.CubicOut);
                f.Pose(18, 0.3f);
                await Tw(f, 0.05f, t => f.X = Mathf.LerpUnclamped(back, reach, t), Ease.QuadIn);
                Sfx.Play("slash");
                Swing(f);
                onContact();
                await FollowThrough(f, dir, 12, 0.06f);
                if (chain.Last) await Recover(f, home, 0.28f);
                return;
            }

            switch (style.Kind)
            {
                case "slash":
                case "enemy-melee":
                {
                    // Monsters take longer to wind up, so you can see them coming; big ones longest (timing sells weight).
                    var monsterBlow = style.Kind == "enemy-melee";
                    var heavyMonster = monsterBlow && (monster.Boss || monster.Look.Scale >= 1.25);
                    if (chain.Finisher)
                    {
                        // The last blow: the scene darkens and the camera leans in as it winds up, a glint, then it strikes.
                        Dim(0.35f, 0.18f);
                        ViewIn((home + target.x) / 2, GroundY - 150, 1.08f, 0.2f);
                        await Anticipate(f, dir, 0.2f, 0.1f, 1);
                        var tip = BladeTip(f);
                        if (tip.HasValue) Fx.Glint(World, tip.Value.x, tip.Value.y, 0xffffff, 2);
                        Sfx.Play("glint");
                    }
                    else if (monsterBlow) await Anticipate(f, dir, heavyMonster ? 0.3f : 0.22f, heavyMonster ? 0.1f : 0.07f, heavyMonster ? 1 : 0.6f);
                    else await Anticipate(f, dir, 0.14f, 0.04f, 0.4f);
                    await Lunge(f, source, dir, reach, monsterBlow ? 0.13f : 0.11f, monsterBlow ? 10 : 14, trail);
                    Fx.Dust(World, reach, GroundY, 3, Design.Depth(GroundY) + 1);
                    Sfx.Play("slash");
                    Swing(f);
                    onContact();
                    await FollowThrough(f, dir, heavyMonster ? 28 : 22, 0.09f);
                    if (chain.Last) await Recover(f, home, 0.3f);
                    break;
                }
                case "heavy":
                {
                    // Crouch, leap, crash down.
                    if (chain.Finisher)
                    {
                        Dim(0.35f, 0.18f);
                        ViewIn((home + target.x) / 2, GroundY - 170, 1.08f, 0.2f);
                    }
                    await Anticipate(f, dir, 0.18f, 0.08f, 1);
                    Fx.Dust(World, f.X, GroundY, 6, Design.Depth(GroundY) + 1);
                    Sfx.Play("whoosh");
                    Afterimages(source, 340, trail);
                    var startX = f.X;
                    var endX = target.x - 180 * StageScale * dir;
                    // Tall and thin on take-off; a true parabola, so it hangs at the top before it comes down.
                    f.Shape(0.86f, 1.16f, 0.12f);
                    f.Pose(-6, 0);
                    await Tw(f, 0.34f, t =>
                    {
                        f.X = Mathf.Lerp(startX, endX, t);
                        f.Y = GroundY - 4 * 150 * t * (1 - t);
                        f.Angle = Mathf.Lerp(-10, 14, t) * dir;
                        if (t > 0.55f) f.Pose(22, 0);
                    }, Ease.Linear);
                    f.Y = GroundY;
                    Sfx.Play("slash");
                    Swing(f, true);
                    // Flattened on landing and held low a moment: the weight of it.
                    f.Squash(1.22f, 0.8f, 0.3f);
                    f.Pose(26, 1, 0.3f);
                    onContact();
                    Fx.Dust(World, f.X, GroundY, 12, Design.Depth(GroundY) + 1);
                    GameRoot.I.Shake(0.12f, 6);
                    GameRoot.I.Kick(0, 6);
                    await Anim.Wait(0.16f);
                    if (chain.Last) await Recover(f, home, 0.34f, 22);
                    break;
                }
                case "bolt":
                case "enemy-spit":
                {
                    var spit = style.Kind == "enemy-spit";
                    if (chain.First)
                    {
                        // Gathering the spell: drawn up tall.
                        f.Shape(0.95f, 1.06f, 0.12f);
                        f.Pose(-8, 0.25f);
                        await MoveX(f, home - 20 * dir, 0.12f, Ease.CubicOut);
                        var charge = spit ? 180 : style.Big ? 520 : 300;
                        Sfx.Play("charge");
                        Fx.CastCircle(World, home, GroundY, style.Colors[1], charge);
                        f.Strain(spit ? 1.4f : style.Big ? 2.2f : 1.2f);
                        await Fx.ChargeUp(World, f.X + 50 * dir, GroundY - Chest - 10, style.Colors[1], charge);
                        f.Strain(0);
                    }
                    var hand = new Vector2(f.X + 50 * dir, GroundY - Chest - 10);
                    Sfx.Play(style.Element == "energy" ? "zap" : "fireball");
                    // Combo bolts fly faster, without a new wind-up.
                    var flight = !chain.First ? 260 : spit ? 360 : style.Big ? 520 : 420;
                    var flying = Fx.Projectile(World, hand, target, style.Colors, flight, style.Element);
                    f.Squash(1.1f, 0.92f, 0.2f);
                    f.Pose(16, 0.2f, 0.3f);
                    GameRoot.I.Kick(-3 * dir, 0);
                    if (chain.First) await Task.WhenAll(MoveX(f, home + 20 * dir, 0.12f, Ease.BackOut), flying);
                    else await flying;
                    Sfx.Play("explode");
                    if (style.Big)
                    {
                        Fx.Explosion(World, target.x, target.y, style.Colors);
                        GameRoot.I.Shake(0.18f, 8);
                    }
                    onContact();
                    if (chain.Last) await Recover(f, home, 0.24f, 8);
                    break;
                }
                case "proc":
                {
                    // Weapon special: a burst at the target right after the blow.
                    Fx.Explosion(World, target.x, target.y, style.Colors);
                    onContact();
                    break;
                }
            }
        }

        void Impact(BattleEvent e, Style style, Chain chain)
        {
            var targetSide = Other(e.Source);
            var f = FighterOf(targetSide);
            var dir = e.Source == "player" ? 1 : -1;
            var x = HomeX(targetSide);
            var y = GroundY - Chest;
            var magic = e.DamageType == "magic";
            var maxHp = targetSide == "enemy" ? battle.Enemy.MaxHp : battle.Player.MaxHp;
            var weight = Mathf.Clamp((float)(e.Damage / maxHp), 0.05f, 0.6f);
            var palette = e.Element != null ? style.Colors : null;
            if (e.Absorbed)
            {
                // The element soaks in and heals it.
                Fx.AbsorbInto(World, x, y, style.Colors);
                Fx.HealSparkles(World, x, GroundY, 0x7dff9a);
                Fx.WhiteFlash(f.Fig, 160, 0x7dff9a);
                Sfx.Play("heal");
                Float(x, TextY(targetSide, GroundY - 230), $"+{JsMath.Str(e.Damage)}", Palette.TextGood, 40, EffectTags["absorb"]);
                ShowHp(targetSide, e.Damage);
                ShowMp();
                return;
            }
            var dull = e.Effect == "resisted" || e.Effect == "immune";
            var guarded = targetSide == "player" && shield != null;
            var big = e.Crit || e.Effect == "weak" || style.Kind == "heavy" || style.Big;
            // Combo hits in the middle of a chain land lighter, so long skills stay snappy.
            var quick = !chain.Last;

            // The blow itself: a crescent from the hero's weapon, claw rakes from a monster.
            if (style.Kind == "slash")
            {
                long color = e.Element != null ? style.Colors[1] : 0xffffff;
                twinFlip = !twinFlip;
                Fx.Slash(World, x, y, dir, color, twinFlip ? 0.35f : -0.35f);
            }
            else if (style.Kind == "enemy-melee") Fx.ClawMarks(World, x, y, dir, e.Element != null ? style.Colors[1] : MonsterHitColor);
            else if (style.Kind == "heavy")
            {
                long color = e.Element != null ? style.Colors[1] : 0xffe08a;
                Fx.Slash(World, x, y, dir, color, 0.6f, true);
                Fx.Shockwave(World, x, GroundY, color, 3, 0.3f);
            }

            // Sparks, then the element on top: flames, shards, splash, gust, rocks, crackle, light or shadow.
            Fx.HitSparks(World, x, y, magic, e.Crit, dull ? new[] { 0xddddddL, 0x999999L, 0x666666L } : palette);
            if (e.Element != null && !dull) Fx.ElementBurst(World, x, y, e.Element, big ? 1 : 0.35f + weight);
            if (e.Effect == "weak") Fx.Shockwave(World, x, y, palette != null ? palette[1] : 0xffe08a, 3.2f, 1);
            if (e.Crit)
            {
                Fx.CritFlare(World, x, y, palette != null ? palette[1] : 0xffe08a);
                GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1.03f, 0.06f, Ease.QuadOut)
                    .OnComplete(() => GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1, 0.18f, Ease.QuadOut));
            }
            if (guarded)
            {
                // The Defend bubble takes the blow.
                Fx.Deflect(World, x - 90 * dir, y, dir, GuardColor);
                var s = shield;
                Anim.Float(1.08f, 1, 0.09f, v => { if (s != null) s.transform.localScale = new Vector3(v, v, 1); });
                Sfx.Play("block");
            }

            Sfx.Play(big ? "crit" : "hit");
            if (e.Element != null && !dull && Sfx.ElementSfx.TryGetValue(e.Element, out var elementSound)) Sfx.Play(elementSound);
            Fx.WhiteFlash(f.Fig, big ? 140 : 100, 0xffffff, palette != null && !dull ? palette[1] : (long?)null);
            // The freeze grows with the blow; the camera is knocked the way the blow travels and springs back.
            Fx.HitStop(quick ? 40 : e.Crit ? 150 : big ? 130 : Mathf.Min(150, 70 + weight * 120));
            GameRoot.I.Shake(e.Crit ? 0.14f : 0.09f, ((guarded || dull ? 0.001f : 0.002f) + weight * (quick ? 0.005f : 0.011f)) * Design.Width);
            var kickPx = (guarded || dull ? 2 : 4 + weight * 14 + (e.Crit ? 4 : 0)) * (quick ? 0.5f : 1);
            GameRoot.I.Kick(kickPx * dir, big ? 2 : 1);
            if (big && !quick) Sfx.Play("thud");

            // Knocked back: snapped away fast (lifted off its feet by a big one), then a slow recovery home, feet
            // dragging dust. A new blow cuts the last one's short.
            recoil?.Stop();
            f.X = x;
            f.Y = GroundY;
            f.Angle = 0;
            var push = guarded ? 8 : 18 + weight * 70 + (e.Crit ? 20 : 0);
            var tilt = (guarded ? 2 : 6 + weight * 12) * dir;
            var lift = guarded || !big ? 0 : 10 + weight * 30;
            var snap = f.Track(Anim.Float(0, 1, 0.06f, t =>
            {
                f.X = x + push * dir * t;
                f.Angle = tilt * t;
                f.Y = GroundY - lift * Mathf.Sin(t * Mathf.PI / 2);
            }, Ease.CubicOut));
            recoil = snap;
            snap.OnComplete(() =>
            {
                if (push > 26) Fx.Dust(World, x + push * dir, GroundY, 3, Design.Depth(GroundY) + 1);
                recoil = f.Track(Anim.Float(1, 0, 0.3f, t =>
                {
                    f.X = x + push * dir * t;
                    f.Angle = tilt * t;
                    f.Y = GroundY - lift * t;
                }, Ease.SineInOut, 0.05f));
            });

            // The body gives under the blow (squashed, then springing back), and the target's HUD piece shakes with it.
            f.Squash(guarded ? 0.97f : 0.86f - weight * 0.12f, guarded ? 1.02f : 1.08f + weight * 0.08f, 0.26f);
            // Rocked back by the blow, the head snapping after.
            f.Pose(guarded ? -3 : -(9 + weight * 34 + (e.Crit ? 8 : 0)), guarded ? 0.5f : 0.2f + weight, 0.16f);
            UiMotion.Jolt(targetSide == "player" ? heroHud : enemyHud, (guarded || dull ? 3 : 5) + weight * 22 + (e.Crit ? 5 : 0));

            // Monsters spill blood (or chips, or wisps): more for heavier blows, a gush for the last one.
            if (targetSide == "enemy" && e.Effect != "immune")
            {
                var amount = (0.2f + weight * 1.6f + (e.Crit ? 0.35f : 0) + (e.Effect == "weak" ? 0.2f : 0)) * (e.Effect == "resisted" ? 0.5f : 1);
                if (chain.Finisher) amount = 2;
                Fx.Bleed(World, x - 40 * dir, y + Random.Range(-20f, 20f), dir, MonsterBlood, amount, GroundY, GroundOrder);
                if ((MonsterBlood?.Kind ?? "liquid") == "liquid") Anim.After(0.06f, () => Sfx.Play("splat"));
            }
            if (chain.Finisher) FinishingBlow(targetSide, x, y, dir, style, e);

            var color2 = e.Element != null ? Palette.ElementText(e.Element) : magic ? Palette.TextMagic : e.Source == "enemy" ? Palette.TextBad : Color.white;
            var tags = string.Join(" ", new[] { e.Crit ? "CRIT!" : "", e.Effect != null ? EffectTags[e.Effect] : "", guarded ? "Guarded" : "" }.Where(s => s != ""));
            var spot = NumberSpot();
            Float(x + spot[0], TextY(targetSide, GroundY - 230) + spot[1], JsMath.Str(e.Damage), color2, e.Crit || e.Effect == "weak" ? 50 : 36, tags != "" ? tags : null);
            ShowHp(targetSide, -e.Damage);
            ShowMp();
        }

        /// <summary>
        /// The blow that ends the fight: a longer freeze, then slow motion while the camera closes in on the one going
        /// down, a white flash, a second slash and a ring across the ground. ("defeated" plays the fall and resets the camera.)
        /// </summary>
        void FinishingBlow(string targetSide, float x, float y, int dir, Style style, BattleEvent e)
        {
            long color = e.Element != null ? style.Colors[1] : 0xffe08a;
            Fx.HitStop(170);
            Fx.SlowMo(0.3f, 950);
            Dim(0, 0.6f);
            ViewIn(x - 70 * dir, GroundY - 150, 1.2f, 0.14f);
            GameRoot.I.Flash(0.1f, new Color(1, 1, 1, 0.55f));
            if (style.Kind == "slash" || style.Kind == "heavy") Fx.Slash(World, x, y, dir, color, -0.6f, true);
            Fx.CritFlare(World, x, y, color);
            Fx.Shockwave(World, x, GroundY, color, 5, 0.3f);
            Sfx.Play("crit");
            Sfx.Play("explode");
            if (targetSide == "enemy") Float(x, TextY("enemy", GroundY - 300), "FINISH!", Design.Css("#ffd36b"), 50);
        }

        void Dodge(string side)
        {
            Sfx.Play("miss");
            var f = FighterOf(side);
            var away = side == "enemy" ? 1 : -1;
            // An afterimage stays where it stood as it sidesteps.
            Ghost(side, 0x9fd3ff, 0.45f, 300);
            Fx.Slash(World, HomeX(side) - 60 * away, GroundY - Chest, -away, 0xffffff, 0);
            Fx.Dust(World, HomeX(side), GroundY, 4, Design.Depth(GroundY) + 1);
            var home = HomeX(side);
            f.Pose(-14, 0.5f, 0.22f);
            f.Track(Anim.Float(0, 1, 0.12f, t => f.X = home + 60 * away * t, Ease.QuadOut))
                .OnComplete(() => f.Track(Anim.Float(1, 0, 0.12f, t => f.X = home + 60 * away * t, Ease.QuadOut, 0.12f)));
            var spot = NumberSpot();
            Float(home + spot[0], TextY(side, GroundY - 220) + spot[1], "Miss", Palette.TextDim, 30);
        }

        /// <summary>Where floating text over a side starts: the enemy's starts lower under a tall info panel.</summary>
        float TextY(string side, float y) => side == "enemy" ? y + enemyTextShift : y;

        /// <summary>Offset for this hit's number: the usual spot, or the next free one in a combo.</summary>
        int[] NumberSpot() => ComboSpots[comboHits % ComboSpots.Length];

        // --- Outcome ---

        void Finish()
        {
            var player = battle.Player;
            var monsterName = monster.Name;
            var backTo = RoomArrival.Battle(back.RoomId, "victory", back.X, back.Y);

            switch (battle.State)
            {
                case "victory":
                {
                    var rewards = battle.Rewards ?? new Rewards();
                    var levelBefore = player.Level;
                    var (levelsGained, lost) = player.GrantRewards(rewards);
                    Travel.RecordGuard(player, back.RoomId, monster.Id);
                    var quests = Quests.RecordKill(player, monster)
                        .Select(q => new QuestTick { Name = q.Quest.Name, Goal = q.Quest.Goal, Count = q.Count, Target = q.Target }).ToList();
                    // Guild hunts: kills counted, and gather items picked up this fight.
                    string HuntName(int rank) => $"Hunt ({Guild.RankId(rank)})";
                    foreach (var u in Guild.RecordHuntKill(player, monster))
                        quests.Add(new QuestTick { Name = HuntName(u.Hunt.Rank), Goal = Guild.Goal(u.Hunt), Count = u.Count, Target = u.Target, TurnIn = "a Guild Clerk" });
                    foreach (var s in player.Data.Guild.Hunts)
                    {
                        var h = Guild.HuntDef(s.Key);
                        if (h?.Kind != "gather" || !rewards.Items.Any(i => i.ItemId == h.ItemId)) continue;
                        quests.Add(new QuestTick
                        {
                            Name = HuntName(h.Rank), Goal = Guild.Goal(h), Count = Math.Min(h.Count, player.CountItem(h.ItemId)), Target = h.Count, TurnIn = "a Guild Clerk",
                        });
                    }
                    if (Db.B.RestoreAfterBattle) player.RestoreFull();
                    Saves.Save(player);
                    var newSkills = player.SkillsLearnedSince(levelBefore).Select(s => s.Name).ToList();
                    GameRoot.I.Go(new ResultScreen(new BattleResult
                    {
                        Outcome = "victory", MonsterName = monsterName, Rewards = rewards, Lost = lost, LevelsGained = levelsGained, NewSkills = newSkills,
                        Quests = quests, Back = backTo,
                    }));
                    break;
                }
                case "defeat":
                {
                    // No penalty: wake up at the nearest inn.
                    player.RestoreFull();
                    Saves.Save(player);
                    GameRoot.I.Go(new ResultScreen(new BattleResult
                    {
                        Outcome = "defeat", MonsterName = monsterName, Wake = Db.Room(back.RoomId).WakeAt ?? Db.Data.InnSpawn,
                        Grit = monster.Boss ? battle.Grit : (double?)null,
                    }));
                    break;
                }
                case "fled":
                    Saves.Save(player);
                    GameRoot.I.Go(new RoomScreen(RoomArrival.Battle(back.RoomId, "fled", back.X, back.Y)));
                    break;
                case "escaped":
                    Saves.Save(player);
                    GameRoot.I.Go(new RoomScreen(RoomArrival.Battle(back.RoomId, "escaped", back.X, back.Y)));
                    break;
            }
        }

        /// <summary>1st, 2nd, 3rd, 4th...</summary>
        static string Ordinal(int n)
        {
            var suffix = n % 100 >= 11 && n % 100 <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return $"{n}{suffix}";
        }
    }
}
