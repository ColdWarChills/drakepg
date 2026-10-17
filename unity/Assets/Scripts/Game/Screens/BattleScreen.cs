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
        const float GroundY = 500, PlayerX = 330, EnemyX = 950;
        const int LogLines = 4;
        /// <summary>Painted backgrounds keep their ground low, mostly under the bottom panel: lift them so the fighters stand on it.</summary>
        const float BgRaise = 90;
        /// <summary>Height above the feet where hits land.</summary>
        const float Chest = 120;
        /// <summary>Skills shown per page of the Skills menu (the other slots are More / Back).</summary>
        const int SkillsPerPage = 4;
        const long GuardColor = 0x8ec5ff;
        /// <summary>Claw rakes and lunge trails of monster attacks without an element.</summary>
        const long MonsterHitColor = 0xff6b5a;
        const long EliteGlow = 0xffc23d, LuckyGlow = 0x7dffb0;
        static readonly Color LuckyText = Design.Css("#8dffbe");
        static readonly Color ToughText = Design.Css("#ff9a4d");
        static readonly Color SetText = Design.Css("#7fe0c0");

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
            public static readonly Chain Single = new Chain { First = true, Last = true };
        }

        class MenuButton
        {
            public string Label, Sub, Hover;
            public bool Enabled = true;
            public Action Run;
            /// <summary>Skills and items: a color of their own (stripe, border, tint) so they read apart at a glance.</summary>
            public Color? Accent;
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

            public void Kill()
            {
                foreach (var t in Tweens) t.Stop();
                Tweens.Clear();
            }

            public void ReadWeapons()
            {
                WeaponAngles = Fig.Weapons.Select(w => w.localEulerAngles.z).ToArray();
            }
        }

        readonly BattleSetup back;
        Battle battle;
        ZoneDef zone;
        MonsterDef monster;
        Fighter hero, foe;
        UiBar playerHp, playerMp, enemyHp;
        Label logText;
        readonly List<string> log = new List<string>();
        VisualElement menu;
        List<MenuButton> menuButtons = new List<MenuButton>();
        int skillPage;
        bool busy;
        bool twinFlip;
        /// <summary>Red glow while the enemy winds up a telegraphed attack.</summary>
        SpriteRenderer chargeAura;
        /// <summary>Blue bubble while the player defends.</summary>
        Shapes shield;
        VisualElement playerStatusRow, enemyStatusRow;
        Label affinityRow, shellLine, intentLine, enrageText, escapeLine, weaponTag;
        bool shellRegrew;
        Anim.Handle recoil;
        int comboHits;
        float enemyTextShift;
        float enemyX;
        /// <summary>Statuses as shown so far during playback (status id -> turns). The battle itself is already a turn ahead.</summary>
        readonly Dictionary<string, List<KeyValuePair<string, int>>> shown = new Dictionary<string, List<KeyValuePair<string, int>>>
        {
            ["player"] = new List<KeyValuePair<string, int>>(), ["enemy"] = new List<KeyValuePair<string, int>>(),
        };

        static IRng Rng => Dice.Rng;
        Look PlayerLook => battle.Player.Look;

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
            hero = new Fighter { Fig = Figure.Create(World, -200, GroundY, PlayerLook, 1, Session.Player.Weapon, Session.Player.WeaponSigil), X = -200, Base = (float)PlayerLook.Scale };
            hero.ReadWeapons();
            if (Session.Player.WeaponSigil != null) Fx.SigilGlow(hero.Fig, Session.Player.WeaponSigil);
            if (monster.Elite != null || monster.Lucky)
            {
                var color = monster.Lucky ? LuckyGlow : EliteGlow;
                var glow = Props.Glow(World, EnemyX, GroundY - 110, Design.Hex(color, 0), 7, 7, Design.Depth(GroundY) - 1);
                float alpha = 0;
                Anim.Float(0, 0.4f, 0.6f, a => alpha = a, Ease.Linear, 0.4f);
                Anim.Float(7, 8, 1.1f, s =>
                {
                    if (glow == null) return;
                    glow.transform.localScale = new Vector3(s / 2, s / 2, 1);
                    glow.color = Design.Hex(color, alpha);
                }, Ease.SineInOut, repeat: -1, yoyo: true);
            }
            foe = new Fighter { Fig = Figure.Create(World, Design.Width + 200, GroundY, monster.Look, -1), X = Design.Width + 200, Facing = -1, Base = (float)monster.Look.Scale };
            foe.ReadWeapons();
            hero.Apply();
            foe.Apply();

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
            await Task.WhenAll(
                hero.Track(Anim.Float(-200, PlayerX, 0.52f, v => hero.X = v, Ease.BackOut)).Task,
                foe.Track(Anim.Float(Design.Width + 200, EnemyX, 0.52f, v => foe.X = v, Ease.BackOut)).Task);
            Fx.Dust(World, PlayerX, GroundY, 6, Design.Depth(GroundY) + 1);
            Fx.Dust(World, EnemyX, GroundY, 6, Design.Depth(GroundY) + 1);
            if (monster.Boss)
            {
                Sfx.Play("warning");
                GameRoot.I.Shake(0.4f, 15);
                GameRoot.I.Flash(0.25f, new Color(120 / 255f, 0, 0));
                Fx.Shockwave(World, EnemyX, GroundY, 0xff4d4d, 4, 0.3f);
                await Anim.Wait(0.5f);
            }
            Idle(hero);
            Idle(foe);
            busy = false;
            ShowMenu("main");
        }

        static void Idle(Fighter f) => f.Track(Anim.Float(1, 1.03f, 0.9f, v => f.Breath = v, Ease.SineInOut, repeat: -1, yoyo: true));

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

        // --- Layout ---

        static Label RichLine(VisualElement parent, float x, float y, string rich, float size, float ax = 0, float ay = 0.5f, float maxWidth = 388)
        {
            var l = Ui.Text(parent, x, y, rich, size, Palette.Text, true, ax, ay);
            FitWidth(l, maxWidth, ax);
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

        void DrawStatus()
        {
            var p = battle.Player;
            var m = monster;
            StatusPanel(16, 16, 420, 120);
            Ui.Text(Root, 32, 26, $"{p.Name}  ·  Lv {p.Level}", 22, bold: true);
            RefreshWeaponTag();
            playerHp = new UiBar(Root, 32, 74, 388, 26, Palette.Hp, "HP ");
            playerMp = new UiBar(Root, 32, 108, 388, 22, Palette.Mp, "MP ");

            enemyX = Design.Width - 436;
            var ex = enemyX;
            // Elites get an extra line explaining their affix, shelled bosses one for the shell, monsters with a
            // signature move one for that; every monster has the "Next:" line.
            var affix = m.Elite != null ? Db.Affix(m.Elite.Affix) : null;
            var heroic = m.IsHeroic && m.Heroic != null ? $"Heroic: {m.Heroic.Text}" : "";
            var infoLines = (affix != null ? 1 : 0) + (m.Shell != null ? 1 : 0) + (m.Trait != null ? 1 : 0) + (heroic != "" ? 1 : 0) + (m.EscapeAfter.HasValue && m.EscapeAfter > 0 ? 1 : 0) + 1;
            var lootY = 136 + infoLines * 28;
            var panelH = 120 + infoLines * 28 + (m.Loot != null ? 26 : 0);
            StatusPanel(ex, 16, 420, panelH);
            // Status names (the highest text, at GroundY - 280) start under the panel and its status chips.
            enemyTextShift = Mathf.Clamp(16 + panelH + 50 - (GroundY - 280), 0, 90);
            // One tag: an Elite or boss already reads as dangerous, and the weakness line shows the counter.
            var tag = m.IsHeroic ? "HEROIC" : m.Boss ? "BOSS" : affix != null ? "ELITE" : m.Lucky ? "LUCKY" : m.Tough ? "TOUGH" : "";
            var headColor = m.Boss ? Palette.TextBad : affix != null ? Palette.TextGold : m.Lucky ? LuckyText : m.Tough ? ToughText : Palette.Text;
            var head = Ui.Text(Root, ex + 16, 26, string.Join("  ·  ", new[] { m.Name, $"Lv {m.Level}", tag }.Where(s => s != "")), 22, headColor, true);
            FitWidth(head, 388);
            enemyHp = new UiBar(Root, ex + 16, 68, 388, 26, Palette.Hp, "HP ");
            DrawAffinities(ex + 16, 108);
            if (affix != null) RichLine(Root, ex + 16, 136, Ui.Colored($"{affix.Name}: {affix.Description}", Palette.TextGold), 15);
            float lineY = 136 + (affix != null ? 28 : 0);
            if (m.Shell != null)
            {
                shellLine = RichLine(Root, ex + 16, lineY, "", 15);
                shellLine.style.color = Design.Css("#bfe9ff");
                RefreshShell();
                lineY += 28;
            }
            if (m.Trait != null)
            {
                RichLine(Root, ex + 16, lineY, m.Trait, 15).style.color = Design.Css("#e8c8ff");
                lineY += 28;
            }
            if (heroic != "")
            {
                RichLine(Root, ex + 16, lineY, heroic, 15).style.color = Palette.TextGold;
                lineY += 28;
            }
            if (m.EscapeAfter.HasValue && m.EscapeAfter > 0)
            {
                escapeLine = RichLine(Root, ex + 16, lineY, "", 15);
                RefreshEscape();
                lineY += 28;
            }
            intentLine = Ui.Text(Root, ex + 16, lineY, "", 16, bold: true, ay: 0.5f);
            intentLine.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
            enrageText = Ui.Text(Root, ex + 404, lineY, "", 15, Palette.TextBad, true, 1, 0.5f);
            RefreshIntent();

            if (m.Lucky && m.Loot != null) DrawLuckyLine(ex + 16, lootY);
            else if (m.Loot != null) DrawLootLine(ex + 16, lootY, m.Loot);

            playerStatusRow = StatusRow(16, 152);
            enemyStatusRow = StatusRow(ex, 152 + infoLines * 28 + (m.Loot != null ? 26 : 0));
        }

        VisualElement StatusRow(float x, float y)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, x, y - 12);
            row.style.flexDirection = FlexDirection.Row;
            Root.Add(row);
            return row;
        }

        /// <summary>"Next: Root Crush ~31 dmg · Defend!" plus "Enrages in 3" / "Enraged +40%" on the right.</summary>
        void RefreshIntent()
        {
            if (intentLine == null || enrageText == null) return;
            var b = battle;
            var a = b.Intent;
            if (a == null)
            {
                intentLine.text = "";
                return;
            }
            var charging = b.Charging == a;
            var dmg = b.IntentDamage();
            var statuses = (a.Applies ?? new List<Apply>()).Select(x => $"{Db.Status(x.Status).Name}{(x.Chance.HasValue && x.Chance < 1 ? $" {Math.Round(x.Chance.Value * 100)}%" : "")}").ToList();
            string detail;
            var color = Palette.Text;
            if (a.Windup != null && !charging)
            {
                detail = "winds up a big attack";
                color = ToughText;
            }
            else if (a.Heal.HasValue && a.Heal > 0)
            {
                detail = $"heals {Math.Round(a.Heal.Value * 100)}% HP";
                color = Palette.TextMagic;
            }
            else if (dmg == null)
            {
                var self = (a.Applies ?? new List<Apply>()).All(x => x.Self);
                detail = statuses.Count > 0 ? $"{(self ? "buffs itself: " : "")}{string.Join(", ", statuses)}" : "prepares";
                color = Palette.TextMagic;
            }
            else
            {
                detail = string.Join(" ", new[] { $"~{JsMath.Str(dmg.Value)} dmg" }.Concat(statuses.Select(x => "+ " + x)));
                if (charging)
                {
                    detail += " · Defend or heal!";
                    color = Palette.TextBad;
                }
                else if (a.Power >= 1.5) color = ToughText;
            }
            intentLine.text = $"Next: {a.Name}  {detail}";
            intentLine.style.color = color;
            var left = b.TurnsToEnrage;
            var warn = Db.B.Enrage.Warn;
            enrageText.text = b.Enrage > 0 ? $"Enraged +{Math.Round(b.Enrage * 100)}%" : left <= warn ? $"Enrages in {left}" : "";
            var room = 388 - (enrageText.text != "" ? enrageText.text.Length * 8.5f + 10 : 0);
            intentLine.style.maxWidth = StyleKeyword.None;
            FitOnce(intentLine, room);
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

        /// <summary>"Grit +20%   Water · Weak!": the weapon in hand, how its element lands now, and boss grit.</summary>
        void RefreshWeaponTag()
        {
            var p = battle.Player;
            weaponTag?.RemoveFromHierarchy();
            var element = p.WeaponElement;
            var effect = battle.EffectOn(element);
            var text = "";
            var grit = battle.Grit;
            if (grit > 0) text += Ui.Colored($"Grit +{Math.Round(grit * 100)}%    ", Palette.TextGold);
            text += Ui.Colored(element != null ? Db.Data.ElementLabels[element] : "Neutral", element != null ? Palette.ElementText(element) : Palette.TextDim);
            if (effect != null) text += Ui.Colored($" · {EffectLabels[effect]}", EffectColor(effect));
            weaponTag = Ui.Text(Root, 420, 30, text, 16, bold: true, ax: 1, ay: 0);
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
            escapeLine.style.color = left <= 1 ? Palette.TextBad : LuckyText;
        }

        static string Pct1(double v) => $"{Math.Round(v * 1000) / 10}%";

        /// <summary>A hover-able info line: shows `details` in the log box while hovered or pressed.</summary>
        void InfoLine(float x, float y, string rich, string details)
        {
            var line = RichLine(Root, x, y, rich, 14);
            line.pickingMode = PickingMode.Position;
            void Show()
            {
                logText.style.fontSize = 15;
                logText.text = details;
            }
            line.RegisterCallback<PointerEnterEvent>(_ => Show());
            line.RegisterCallback<PointerDownEvent>(_ => Show());
            line.RegisterCallback<PointerLeaveEvent>(_ => RestoreLog());
        }

        void RestoreLog()
        {
            logText.style.fontSize = 19;
            logText.text = string.Join("\n", log);
        }

        /// <summary>"Loot  ✦ Sigil 10%  ·  Gold x5": a Lucky Impling's payout. Hover or tap for the Sigil odds.</summary>
        void DrawLuckyLine(float x, float y)
        {
            var l = Db.B.Lucky;
            var rich = Ui.Colored("Loot ", Palette.TextDim) + Ui.Colored($" ✦ Sigil {Pct1(l.SigilChance)} ", Design.Css("#ff9550")) + Ui.Colored($" ·  Gold x{l.Gold} ", Palette.TextGold);
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
            var color = new Dictionary<string, Color> { ["rare"] = Design.Css("#ffd34d"), ["uncommon"] = Design.Css("#7ddc7d"), ["common"] = Palette.Text };
            var rich = Ui.Colored("Gear ", Palette.TextDim);
            foreach (var t in Loot.Tiers)
                if (odds[t] > 0) rich += Ui.Colored($" {label[t]} {Pct(odds[t])}{(t == "rare" && luck > 0 ? " (luck)" : "")} ", color[t]);
            // Elites and bosses: a chance at a piece of the area's set on top.
            var table = Db.LootTable(tableId);
            var setChance = table.Set != null && (source == "elite" || source == "boss") ? Cheats.Boosted(Db.B.SetDrop[source]) : 0;
            if (setChance > 0) rich += Ui.Colored($" · Set {Pct(setChance)} ", SetText);
            // Guild gather hunts: this area's hunt item, on top of everything else.
            var gather = Guild.GatherHere(battle.Player, zone.Id);
            foreach (var g in gather) rich += Ui.Colored($" · {Db.Item(g.hunt.ItemId).Name} {Pct(g.chance)} ", Palette.TextGood);

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

        /// <summary>"Earth · Weak: Fire Wind Blunt · Resists: Earth Water", each element in its color.</summary>
        void DrawAffinities(float x, float y)
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
            var parts = new List<string>();
            if (m.Element != null) parts.Add(Ui.Colored(Db.Data.ElementLabels[m.Element], Palette.ElementText(m.Element)));
            void Group(string label, List<string> list)
            {
                if (list.Count == 0) return;
                parts.Add(Ui.Colored($"{(parts.Count > 0 ? "  ·  " : "")}{label}", Palette.TextDim));
                foreach (var e in list) parts.Add(Ui.Colored($" {Db.Data.ElementLabels[e]}", Palette.ElementText(e)));
            }
            Group("Weak:", weak);
            // Weak spots besides elements: Blunt skills and Poison.
            var spots = new List<(string label, Color color)>();
            if (m.WeakTo?.Blunt > 0) spots.Add(("Blunt", Design.Css("#d9c9a8")));
            if (m.WeakTo?.Poison > 0) spots.Add(("Poison", Design.Css("#8fe36b")));
            if (spots.Count > 0)
            {
                if (weak.Count == 0) parts.Add(Ui.Colored($"{(parts.Count > 0 ? "  ·  " : "")}Weak:", Palette.TextDim));
                foreach (var s in spots) parts.Add(Ui.Colored($" {s.label}", s.color));
            }
            Group("Resists:", strong);
            Group("Absorbs:", absorbs);
            if (all != 0) parts.Add(Ui.Colored($"{(parts.Count > 0 ? "  ·  " : "")}All {(all > 0 ? "+" : "")}{JsMath.Str(all)}", Palette.TextGold));
            affinityRow?.RemoveFromHierarchy();
            affinityRow = RichLine(Root, x, y, string.Join("", parts), 15);
        }

        void StatusPanel(float x, float y, float w, float h) =>
            Ui.Rect(Root, x, y, w, h, new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.9f), 10, 2, Palette.PanelBorder);

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

        /// <summary>Chips under each status panel: "Burn 2", "War Cry 3".</summary>
        void RefreshStatuses()
        {
            foreach (var side in new[] { "player", "enemy" })
            {
                var row = side == "player" ? playerStatusRow : enemyStatusRow;
                if (row == null) continue;
                row.Clear();
                foreach (var kv in shown[side])
                {
                    var def = Db.Status(kv.Key);
                    var chip = new Label($"{def.Name} {kv.Value}") { pickingMode = PickingMode.Ignore };
                    chip.style.fontSize = 14;
                    chip.style.unityFontStyleAndWeight = FontStyle.Bold;
                    chip.style.color = Design.Css(def.Harmful ? "#ffd0cc" : "#d4ffd8");
                    chip.style.backgroundColor = def.Harmful ? Design.Hex(0x6e1f1f, 0.9f) : Design.Hex(0x1f5a2c, 0.9f);
                    chip.style.height = 24;
                    chip.style.paddingLeft = chip.style.paddingRight = 8;
                    chip.style.paddingTop = chip.style.paddingBottom = 0;
                    chip.style.marginLeft = chip.style.marginTop = chip.style.marginBottom = 0;
                    chip.style.marginRight = 6;
                    chip.style.unityTextAlign = TextAnchor.MiddleCenter;
                    Ui.Round(chip, 8);
                    row.Add(chip);
                }
            }
        }

        void DrawBottomPanel()
        {
            const float top = 540;
            Ui.Rect(Root, 8, top, Design.Width - 16, Design.Height - top - 8, new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.95f), 12, 3, Palette.PanelBorder);
            logText = Ui.Text(Root, 30, top + 18, "", 19, wrap: 590);
            logText.style.unityParagraphSpacing = 6;
            menu = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(menu, 0, 0);
            Root.Add(menu);
        }

        void RefreshBars(bool animate = true)
        {
            var p = battle.Player;
            var e = battle.Enemy;
            playerHp.SetValue(p.Hp, p.MaxHp, animate);
            playerMp.SetValue(p.Mp, p.MaxMp, animate);
            enemyHp.SetValue(e.Hp, e.MaxHp, animate);
        }

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
            return new MenuButton
            {
                Label = skill.Name, Sub = sub, Enabled = battle.CanUseSkill(skill.Id), Hover = $"{skill.Name}: {skill.Description}{cdText}",
                Run = () => Act(() => battle.UseSkill(skill.Id)), Accent = SkillColor(skill),
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
                return new MenuButton { Label = "Swap", Sub = "No spare", Enabled = false, Hover = "Swap: put a second weapon in the Spare slot (Inventory) to switch to it mid-fight.", Run = () => { } };
            var element = spare?.Element;
            var effect = battle.EffectOn(element);
            var elName = element != null ? Db.Data.ElementLabels[element] : "Neutral";
            var name = spare?.Name ?? "bare hands";
            var type = Db.Data.DamageTypeLabels[spare?.DamageType ?? "melee"];
            return new MenuButton
            {
                Label = "Swap", Sub = $"{elName}{(effect != null ? $" · {EffectLabels[effect]}" : "")}", Enabled = battle.CanSwapWeapon,
                Hover = $"Swap to your {name} ({JsMath.Str(range[0])}-{JsMath.Str(range[1])} {type}, {elName}{(effect != null ? $", {EffectLabels[effect]}" : "")}). Costs your turn.",
                Run = () => Act(() => battle.SwapWeapon()),
            };
        }

        void ShowMenu(string which)
        {
            menu.Clear();
            var p = battle.Player;
            var buttons = new List<MenuButton>();

            if (which == "main")
            {
                var fresh = p.CarriedSkills.Count(sk => p.Data.FreshSkills.Contains(sk.Id));
                buttons.Add(new MenuButton { Label = "Attack", Run = () => Act(() => battle.Attack()) });
                buttons.Add(new MenuButton { Label = "Skills", Sub = fresh > 0 ? $"{fresh} NEW" : null, Run = () => ShowMenu("skills") });
                buttons.Add(new MenuButton { Label = "Items", Run = () => ShowMenu("items") });
                buttons.Add(new MenuButton { Label = "Defend", Sub = "-60% dmg, +MP", Run = () => Act(() => battle.Defend()) });
                buttons.Add(new MenuButton
                {
                    Label = "Flee", Sub = battle.CanFlee ? $"{Math.Round(battle.FleeChance * 100)}%" : "Boss", Enabled = battle.CanFlee,
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
                        Label = "More", Sub = $"{skillPage + 1} / {pages}",
                        Run = () =>
                        {
                            skillPage += 1;
                            ShowMenu("skills");
                        },
                    });
                }
                buttons.Add(new MenuButton { Label = "Back", Run = () => ShowMenu("main") });
            }
            else
            {
                var usable = p.Data.Inventory.Where(e => Db.Item(e.ItemId).Type == "consumable").Take(5).ToList();
                foreach (var entry in usable)
                {
                    var item = Db.Item(entry.ItemId);
                    buttons.Add(new MenuButton
                    {
                        Label = item.Name, Sub = $"x{entry.Qty}", Hover = $"{item.Name}: {item.Description}",
                        Accent = item.IconColor.HasValue ? Design.Hex(item.IconColor.Value) : (Color?)null,
                        Run = () =>
                        {
                            if (!battle.CanUseItem(item.Id)) PushLog($"{item.Name} would have no effect right now.");
                            else Act(() => battle.UseItem(item.Id));
                        },
                    });
                }
                if (usable.Count == 0) PushLog("You have no usable items.");
                buttons.Add(new MenuButton { Label = "Back", Run = () => ShowMenu("main") });
            }

            // 3 x 2 grid on the right side of the bottom panel. Number keys 1-6 work too.
            menuButtons = buttons;
            const int cols = 3;
            const float bw = 196, bh = 66;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                var x = 650 + i % cols * (bw + 10) + bw / 2;
                var y = 590 + i / cols * (bh + 10);
                var tint = b.Accent.HasValue ? Color.Lerp(Palette.Button, b.Accent.Value, 0.22f) : Palette.Button;
                var btn = new UiButton(menu, x, y, b.Label, () => b.Run(), bw, bh, b.Label.Length > 13 ? 17 : 20, b.Sub, tint);
                if (b.Accent is Color accent)
                {
                    Ui.Border(btn, 2, Color.Lerp(Palette.PanelBorder, accent, 0.7f));
                    // A colored bar down the left edge, like a tab.
                    var bar = Ui.Rect(btn, 8, 10, 6, bh - 24, accent, 3);
                    bar.style.position = Position.Absolute;
                    if (!b.Enabled) bar.style.opacity = 0.4f;
                    // Center the texts in the room right of the bar; long lines ("10 MP · Energy · Weak!") get smaller.
                    btn.style.paddingLeft = 14;
                    var texts = btn.Query<Label>().ToList();
                    if (texts.Count > 1 && (b.Sub?.Length ?? 0) > 17) texts[1].style.fontSize = 13;
                }
                if (!b.Enabled) btn.SetEnabled(false);
                if (b.Hover != null)
                {
                    btn.RegisterCallback<PointerEnterEvent>(_ => logText.text = b.Hover);
                    btn.RegisterCallback<PointerLeaveEvent>(_ => RestoreLog());
                }
                Ui.Text(btn, b.Accent.HasValue ? 20 : 10, 6, $"{i + 1}", 13, Palette.TextDim).style.position = Position.Absolute;
                btn.style.opacity = 0;
                btn.style.scale = new Scale(Vector3.one * 0.8f);
                Anim.Float(0.8f, 1, 0.18f, s =>
                {
                    btn.style.scale = new Scale(new Vector3(s, s, 1));
                    btn.style.opacity = Mathf.InverseLerp(0.8f, 1, Mathf.Min(1, s));
                }, Ease.BackOut, i * 0.03f);
            }
        }

        // --- Turn playback ---

        async void Act(Func<List<BattleEvent>> action)
        {
            if (busy) return;
            var events = action();
            if (events.Count == 0) return;
            busy = true;
            menuButtons = new List<MenuButton>();
            menu.Clear();
            RestoreLog();
            for (int i = 0; i < events.Count; i++) await Play(events[i], i > 0 ? events[i - 1] : null, i + 1 < events.Count ? events[i + 1] : null);
            RefreshBars();
            SyncStatuses();
            RefreshIntent();
            RefreshEscape();
            RefreshWeaponTag();

            if (battle.IsOver)
            {
                await Anim.Wait(0.6f);
                Finish();
            }
            else
            {
                busy = false;
                ShowMenu("main");
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
            await PlayEvent(e, new Chain { First = !SameMove(prev, e), Last = next == null || !SameMove(e, next) });
            // An enemy action resolves the player's guard and the boss's wind-up.
            var enemyActed = e.Kind == "windup" || ((e.Kind == "hit" || e.Kind == "miss" || e.Kind == "buff") && e.Source == "enemy");
            if (!enemyActed) return;
            if (e.Kind != "windup" && chargeAura != null)
            {
                UnityEngine.Object.Destroy(chargeAura.gameObject);
                chargeAura = null;
            }
            if (shield != null)
            {
                var s = shield;
                shield = null;
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
                    RefreshBars();
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
                        Sfx.Play("hit");
                        var tilt = e.Target == "player" ? -4 : 4;
                        f.Track(Anim.Float(0, tilt, 0.06f, v => f.Angle = v, Ease.CubicOut, yoyo: true)).OnComplete(() => f.Angle = 0);
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
                    RefreshBars();
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
                    Float(PlayerX, GroundY - 290, e.Mp > 0 ? $"Defend  +{JsMath.Str(e.Mp)} MP" : "Defend", Palette.TextMagic, 28);
                    PushLog("You brace yourself.");
                    RefreshBars();
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
                    hero.Base = (float)PlayerLook.Scale;
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
                    GameRoot.I.Shake(0.35f, 8);
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
                    await Anim.Wait(0.7f);
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
                    RefreshBars();
                    break;
                }
                case "phase":
                {
                    // The battle already holds the new form; catch the screen's copy up and show it off.
                    monster = battle.Enemy.Def;
                    var colors = ColorsOf(e.Element);
                    Sfx.Play("warning");
                    GameRoot.I.Flash(0.35f, Design.Hex(colors[1]));
                    GameRoot.I.Shake(0.45f, 15);
                    Fx.Shockwave(World, EnemyX, GroundY, colors[1], 4, 0.4f);
                    Fx.Explosion(World, EnemyX, GroundY - Chest, colors);
                    Fx.ElementBurst(World, EnemyX, GroundY - Chest, e.Element, 1);
                    foe.Fig.SetTint(Design.Hex(colors[1]));
                    Float(EnemyX, TextY("enemy", GroundY - 290), $"{Db.Data.ElementLabels[e.Element]} form!", Palette.ElementText(e.Element), 40);
                    PushLog(e.Text);
                    DrawAffinities(Design.Width - 420, 108);
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
                        Fx.WhiteFlash(f.Fig, 260);
                        // Knocked back off its feet.
                        var fromX = f.X;
                        await f.Track(Anim.Float(0, 1, 0.2f, t =>
                        {
                            f.X = Mathf.Lerp(fromX, EnemyX + 40, t);
                            f.Angle = 10 * t;
                        }, Ease.QuadOut)).Task;
                        if (m.Boss)
                        {
                            // A boss goes down in a chain of blasts.
                            for (int i = 0; i < 3; i++)
                            {
                                var bx = EnemyX + Random.Range(-body.width / 2, body.width / 2);
                                var by = Random.Range(body.top + 20, GroundY - 40);
                                Fx.Explosion(World, bx, by, colors);
                                Fx.WhiteFlash(f.Fig, 90);
                                Sfx.Play("explode");
                                GameRoot.I.Shake(0.16f, 13);
                                await Anim.Wait(0.2f);
                            }
                            GameRoot.I.Flash(0.26f, Color.white);
                            Fx.Shockwave(World, EnemyX, GroundY, colors[1], 5, 0.35f);
                        }
                        Fx.DeathPoof(World, f.X, GroundY - 60, m.Look.Color);
                        Fx.Dissolve(World, f.X, GroundY, body.width * 0.8f, GroundY - body.top, colors);
                        Sfx.Play("explode");
                        GameRoot.I.Shake(0.2f, 10);
                        var gold = battle.Rewards?.Gold ?? 0;
                        _ = Fx.CoinShower(World, new Vector2(f.X, GroundY - 100), new Vector2(60, 40), (int)Math.Ceiling(gold / 2), () => Sfx.Play("coin"));
                        await f.Track(Anim.Float(0, 1, 0.38f, t =>
                        {
                            f.Sy = 1 - 0.8f * t;
                            f.Fig.SetAlpha(1 - t);
                        }, Ease.CubicIn)).Task;
                    }
                    else
                    {
                        PushLog("You collapse...");
                        Sfx.Play("defeat");
                        GameRoot.I.Flash(0.3f, new Color(160 / 255f, 0, 0));
                        GameRoot.I.Shake(0.3f, 13);
                        await f.Track(Anim.Float(0, 1, 0.6f, t =>
                        {
                            f.Angle = -80 * t;
                            f.Fig.SetAlpha(1 - 0.5f * t);
                        }, Ease.BounceOut)).Task;
                    }
                    break;
                }
            }
        }

        /// <summary>Cock the weapons a figure holds back (if its art shows them), ready to swing.</summary>
        static void Cock(Fighter f, float seconds)
        {
            foreach (var t in f.WeaponTweens) t.Stop();
            f.WeaponTweens.Clear();
            for (int i = 0; i < f.Fig.Weapons.Count; i++)
            {
                var w = f.Fig.Weapons[i];
                var baseAngle = f.WeaponAngles[i];
                var from = w.localEulerAngles.z;
                f.WeaponTweens.Add(Anim.Float(0, 1, seconds, t => { if (w != null) w.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(from, baseAngle + 40, t)); }, Ease.QuadOut));
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

        /// <summary>Lean back (and cock the weapon) before a lunge: the anticipation that sells the blow.</summary>
        static Task WindUp(Fighter f, int dir, float seconds, bool big = false)
        {
            Cock(f, seconds);
            var fromX = f.X;
            var toX = f.X - (big ? 30 : 20) * dir;
            var toA = -(big ? 9 : 6) * dir;
            return f.Track(Anim.Float(0, 1, seconds, t =>
            {
                f.X = Mathf.Lerp(fromX, toX, t);
                f.Angle = toA * t;
            }, Ease.QuadOut)).Task;
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
            var reach = target.x - 170 * dir;

            // Combo hits after the first: already in reach, a quick step back and in, then the next blow.
            if (!chain.First && style.Kind != "bolt" && style.Kind != "proc")
            {
                var fromX = f.X;
                await Tw(f, 0.06f, t => f.X = Mathf.LerpUnclamped(fromX, reach - 26 * dir, t), Ease.QuadOut, true);
                Sfx.Play("slash");
                Swing(f);
                onContact();
                if (chain.Last) await MoveX(f, home, 0.24f, Ease.QuadOut, 0);
                return;
            }

            switch (style.Kind)
            {
                case "slash":
                case "enemy-melee":
                {
                    await WindUp(f, dir, 0.08f);
                    Sfx.Play("whoosh");
                    Afterimages(source, 150, trail);
                    Fx.SpeedLines(World, f.X, GroundY, dir, trail);
                    await MoveX(f, reach, 0.15f, Ease.QuadIn, 4 * dir);
                    Fx.Dust(World, reach, GroundY, 3, Design.Depth(GroundY) + 1);
                    Sfx.Play("slash");
                    Swing(f);
                    onContact();
                    if (chain.Last) await MoveX(f, home, 0.24f, Ease.QuadOut, 0);
                    break;
                }
                case "heavy":
                {
                    // Crouch, leap, crash down.
                    await WindUp(f, dir, 0.13f, true);
                    Fx.Dust(World, f.X, GroundY, 5, Design.Depth(GroundY) + 1);
                    Sfx.Play("whoosh");
                    Afterimages(source, 300, trail);
                    var startX = f.X;
                    var endX = target.x - 180 * dir;
                    await Tw(f, 0.3f, t =>
                    {
                        f.X = Mathf.Lerp(startX, endX, t);
                        f.Y = GroundY - Mathf.Sin(t * Mathf.PI) * 130;
                        f.Angle = -9 * dir + t * 21 * dir;
                    }, Ease.SineInOut);
                    f.Y = GroundY;
                    Sfx.Play("slash");
                    Swing(f, true);
                    onContact();
                    Fx.Dust(World, f.X, GroundY, 10, Design.Depth(GroundY) + 1);
                    GameRoot.I.Shake(0.18f, 13);
                    if (chain.Last) await MoveX(f, home, 0.3f, Ease.QuadOut, 0);
                    break;
                }
                case "bolt":
                case "enemy-spit":
                {
                    var spit = style.Kind == "enemy-spit";
                    if (chain.First)
                    {
                        await MoveX(f, home - 20 * dir, 0.12f, Ease.CubicOut);
                        var charge = spit ? 180 : style.Big ? 520 : 300;
                        Sfx.Play("charge");
                        Fx.CastCircle(World, home, GroundY, style.Colors[1], charge);
                        await Fx.ChargeUp(World, f.X + 50 * dir, GroundY - Chest - 10, style.Colors[1], charge);
                    }
                    var hand = new Vector2(f.X + 50 * dir, GroundY - Chest - 10);
                    Sfx.Play(style.Element == "energy" ? "zap" : "fireball");
                    // Combo bolts fly faster, without a new wind-up.
                    var flight = !chain.First ? 260 : spit ? 360 : style.Big ? 520 : 420;
                    var flying = Fx.Projectile(World, hand, target, style.Colors, flight, style.Element);
                    if (chain.First) await Task.WhenAll(MoveX(f, home + 20 * dir, 0.12f, Ease.BackOut), flying);
                    else await flying;
                    Sfx.Play("explode");
                    if (style.Big)
                    {
                        Fx.Explosion(World, target.x, target.y, style.Colors);
                        GameRoot.I.Shake(0.26f, 15);
                    }
                    onContact();
                    if (chain.Last) await MoveX(f, home, 0.2f, Ease.CubicOut);
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
            const float y = GroundY - Chest;
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
                RefreshBars();
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
            Fx.HitStop(quick ? 35 : e.Crit ? 130 : big ? 110 : 60);
            GameRoot.I.Shake(e.Crit ? 0.22f : 0.14f, ((guarded || dull ? 0.002f : 0.004f) + weight * (quick ? 0.012f : 0.03f)) * Design.Width);

            // Knockback and a recoil tilt, bigger for heavier blows. A new blow cuts the last one's short.
            recoil?.Stop();
            f.X = x;
            f.Y = GroundY;
            f.Angle = 0;
            var push = guarded ? 8 : 16 + weight * 60 + (e.Crit ? 18 : 0);
            var tilt = (guarded ? 2 : 5 + weight * 10) * dir;
            recoil = f.Track(Anim.Float(0, 1, 0.08f, t =>
            {
                f.X = x + push * dir * t;
                f.Angle = tilt * t;
            }, Ease.QuadOut, yoyo: true)).OnComplete(() => f.Angle = 0);

            var color2 = e.Element != null ? Palette.ElementText(e.Element) : magic ? Palette.TextMagic : e.Source == "enemy" ? Palette.TextBad : Color.white;
            var tags = string.Join(" ", new[] { e.Crit ? "CRIT!" : "", e.Effect != null ? EffectTags[e.Effect] : "", guarded ? "Guarded" : "" }.Where(s => s != ""));
            var spot = NumberSpot();
            Float(x + spot[0], TextY(targetSide, GroundY - 230) + spot[1], JsMath.Str(e.Damage), color2, e.Crit || e.Effect == "weak" ? 50 : 36, tags != "" ? tags : null);
            RefreshBars();
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
