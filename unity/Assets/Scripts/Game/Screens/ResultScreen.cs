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
            // A boss's first kill: a bigger sheet, its name for the title.
            var first = won && data.Tower == null ? data.FirstKill : null;

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

            // The panel is a paper sheet drawn in the world so effects (confetti, sparkles) can fly over it; the text
            // sits on top.
            var go = new GameObject("panel");
            go.transform.SetParent(World, false);
            go.transform.position = Design.World(cx, Design.Height / 2, 4);
            var panel = go.AddComponent<SpriteRenderer>();
            panel.sprite = Felt.PatchSprite(first != null ? 860 : 720, first != null ? 600 : 520, 77, Felt.Patch.Menu());
            panel.sortingOrder = -30000;
            panel.color = new Color(1, 1, 1, 0);
            Anim.Float(0, 1, 0.3f, t =>
            {
                if (panel == null) return;
                var s = Mathf.LerpUnclamped(0.9f, 1, t);
                panel.transform.localScale = new Vector3(s, s, 1);
                panel.color = new Color(1, 1, 1, Mathf.Clamp01(t));
            }, Ease.BackOut);
            Root.AddToClassList(FeltPass.Class);

            var heading = first != null ? $"{Capitalize(The(first.Monster.Name).Substring(4))} defeated!" : won ? "Victory!" : "Defeated";
            var title = Ui.Text(Root, cx, first != null ? 116 : 170, heading, first != null ? 54 : 64, won ? Palette.TextGold : Palette.TextBad, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(title);
            title.style.opacity = 0;
            title.style.scale = new Scale(Vector3.one * 2);
            Anim.Float(0, 1, 0.38f, t =>
            {
                var s = Mathf.LerpUnclamped(2, 1, t);
                title.style.scale = new Scale(new Vector3(s, s, 1));
                title.style.opacity = Mathf.Clamp01(t);
            }, Ease.BackOut, 0.1f);

            if (data.Tower != null)
            {
                TowerResult(cx, won);
                return;
            }
            if (won) WonCard(cx);
            else
            {
                GameRoot.I.Shake(0.3f, 8);
                DefeatCard(cx);
            }
        }

        /// <summary>
        /// Victory: XP and gold counting up, the drops, quest progress, the XP bar (a level-up gets its page next).
        /// A boss's first kill gets its own card: the boss on the left under a FIRST KILL stamp, the turns it took,
        /// and what the win opened (who to see and what waits there, or the road a guard kept shut).
        /// </summary>
        void WonCard(float cx)
        {
            var player = Session.Player;
            var rewards = data.Rewards;
            var first = data.FirstKill;
            Anim.After(0.25f, () => Fx.Confetti(World));
            Sfx.Play("victory");

            // The column the rows sit in: the card's middle, or right of the boss on a first kill.
            var x = first != null ? 800f : cx;
            var dropsY = first != null ? 292f : 360f;
            if (first != null)
            {
                BossPortrait(first.Monster, 405, 360, 548);
                var turns = first.Turns == 1 ? "1 turn" : $"{first.Turns} turns";
                Ui.Text(Root, x, 180, $"Beaten in {turns}.", 24, ax: 0.5f, ay: 0.5f);
            }
            else Ui.Text(Root, cx, 240, $"You defeated {The(data.MonsterName)}.", 24, ax: 0.5f, ay: 0.5f);
            var rowY = first != null ? 228f : 288f;
            var xpText = Ui.Text(Root, x - 120, rowY, "+0 XP", 30, Palette.TextGood, true, 0.5f, 0.5f);
            var goldText = Ui.Text(Root, x + 120, rowY, "+0 gold", 30, Palette.TextGold, true, 0.5f, 0.5f);
            // With no drops the rows below move up into their place.
            var low = rewards.Items.Count > 0 ? 0 : first != null ? -70 : -64;
            DropReveal(x, dropsY, rewards.Items, data.Lost, first != null ? 460 : 680);
            var y = (first != null ? 392f : 456f) + low;
            var lines = new List<VisualElement>();
            if (first != null) y = Opened(first, x, y, lines);
            foreach (var q in data.Quests)
            {
                if (first?.Quest != null && q.Quest == first.Quest) continue;
                var done = q.Count >= q.Target;
                lines.Add(Ui.Text(Root, x, y, $"{q.Name}: {q.Goal} {q.Count}/{q.Target}{(done ? $"  ✔ Return to {q.TurnIn ?? "the quest giver"}" : "")}",
                    19, done ? Palette.TextGood : Palette.TextGold, done, 0.5f, 0.5f));
                if (done) Anim.After(1.1f, () => Sfx.Play("quest"));
                y += 28;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                line.style.opacity = 0;
                Anim.Float(0, 1, 0.3f, a => line.style.opacity = a, Ease.Linear, 1.1f + i * 0.15f);
            }
            CountUp(xpText, rewards.Xp, " XP", 0.4f);
            CountUp(goldText, rewards.Gold, " gold", 0.65f);

            // The XP bar fills from where it stood before the fight; a level-up fills it to the brim, then bursts.
            var before = data.Before ?? HeroBefore.Of(player);
            var leveled = data.LevelsGained > 0;
            var barY = y + (first != null ? 30 : 28);
            var bar = new UiBar(Root, x - 190, barY, 380, 24, Palette.Xp, "XP ");
            bar.SetValue(before.Xp, before.XpNeeded, false);
            Anim.After(0.5f, () => bar.SetValue(leveled ? before.XpNeeded : player.Xp, leveled ? before.XpNeeded : player.XpNeeded));
            if (leveled)
            {
                Anim.After(1.2f, () =>
                {
                    if (page != 1) return;
                    Fx.LevelUpBurst(World, x, barY);
                    Sfx.Play("levelup");
                    GameRoot.I.Shake(0.2f, 8);
                    bar.SetValue(player.Xp, player.XpNeeded, false);
                    var banner = Ui.Text(Root, x, barY + 36, $"LEVEL UP!  Now level {player.Level}", 30, Palette.TextGood, true, 0.5f, 0.5f, stroke: true);
                    banner.style.scale = new Scale(Vector3.zero);
                    void Scale(float s) => banner.style.scale = new Scale(new Vector3(s, s, 1));
                    Anim.Float(0, 1.2f, 0.2f, Scale, Ease.BackOut)
                        .OnComplete(() => Anim.Float(1.2f, 1, 0.15f, Scale)
                            .OnComplete(() => Anim.Float(1, 1.06f, 0.5f, Scale, Ease.Linear, repeat: -1, yoyo: true)));
                });
            }

            // A level-up gets its own page (what grew, the stat points) before the room.
            var buttonY = first != null ? 616 : Mathf.Min(576, barY + (leveled ? 92 : 62));
            if (leveled) ContinueButton("Level up!", () => LevelPage(true), buttonY);
            else ContinueButton("Continue", ToRoom(data.Back), buttonY);
        }

        /// <summary>What a first kill opened, as lines from `y` (returns the y under them): the quest done and who to see with what waits there, or the open road.</summary>
        float Opened(FirstKill first, float x, float y, List<VisualElement> lines)
        {
            if (first.Quest == null)
            {
                lines.Add(Ui.Text(Root, x, y, first.Road != null ? $"✔  The road to {first.Road} is open." : "✔  The way on is open.", 21, Palette.TextGood, true, 0.5f, 0.5f));
                Anim.After(1.1f, () => Sfx.Play("quest"));
                return y + 30;
            }
            lines.Add(Ui.Text(Root, x, y, $"✔  {first.Quest.Name}", 21, Palette.TextGood, true, 0.5f, 0.5f));
            Anim.After(1.1f, () => Sfx.Play("quest"));
            var where = first.GiverRoom != null ? $" in {first.GiverRoom}" : "";
            lines.Add(Ui.Text(Root, x, y + 30, $"Return to {first.Giver}{where}.", 19, Palette.Text, false, 0.5f, 0.5f));
            y += 46;
            if (first.Opens.Count > 0)
            {
                var opens = $"Waiting there: {string.Join(", ", first.Opens)}.";
                lines.Add(Ui.Text(Root, x, y, opens, 17, Palette.TextDim, false, 0.5f, 0, wrap: 460, align: TextAnchor.UpperCenter));
                y += 22 * Mathf.CeilToInt(opens.Length * 7.4f / 460);   // ~7 px a letter at 17 px
            }
            return y + 10;
        }

        /// <summary>The beaten boss on the card's left, centred on `midY` (feet no lower than `floorY`), a FIRST KILL stamp slamming down on its feet.</summary>
        void BossPortrait(MonsterDef monster, float x, float midY, float floorY)
        {
            var y = midY;
            var key = Art.Pick(monster.Look?.Art);
            if (key != null && Art.Has(key))
            {
                var size = Art.Size(Art.Get(key));
                float h = 360, w = size.y > 0 ? h * size.x / size.y : h;
                if (w > 330) { h *= 330 / w; w = 330; }
                y = Mathf.Min(floorY, midY + h / 2);
                var pic = Ui.Image(Root, x, y, w, h, key, 0.5f, 1);
                pic.style.opacity = 0;
                Anim.Float(0, 1, 0.35f, a => pic.style.opacity = a, Ease.Linear, 0.15f);
            }

            var stamp = new VisualElement { pickingMode = PickingMode.Ignore };
            const float dx = 30, dy = -20;
            Ui.Place(stamp, x + dx, y + dy);
            Root.Add(stamp);
            Ui.Rect(stamp, -128, -34, 256, 68, new Color(1, 1, 1, 0.55f), 6, 5, Palette.TextBad);
            var word = Ui.Text(stamp, 0, 0, "FIRST KILL", 38, Palette.TextBad, true, 0.5f, 0.5f);
            Fonts.Title(word);
            stamp.style.rotate = new Rotate(-10);
            stamp.style.opacity = 0;
            const float at = 0.95f;
            Anim.Float(0, 1, 0.16f, t =>
            {
                var s = Mathf.Lerp(2.6f, 1, t);
                stamp.style.scale = new Scale(new Vector3(s, s, 1));
                stamp.style.opacity = Mathf.Clamp01(t * 2);
            }, Ease.QuadIn, at).OnComplete(() =>
            {
                if (page != 1) return;
                Sfx.Play("thud");
                GameRoot.I.Shake(0.25f, 10);
                Fx.Burst(World, x + dx, y + dy, "fx_star", 14, 60, 220, 0.6f, 0.6f, Palette.TextBad, true);
            });
        }

        /// <summary>
        /// Defeated, and why: its biggest hit on you, the hits of yours it resisted, its weak spots (icons, the words on
        /// hover, as on the battle's monster sheet), grit, then where you go. A room's boss offers Try again at its door.
        /// </summary>
        void DefeatCard(float cx)
        {
            Ui.Text(Root, cx, 236, $"{Capitalize(The(data.MonsterName))} got the better of you.", 24, ax: 0.5f, ay: 0.5f);
            const float labelX = 548, valueX = 566;
            float y = 292;
            var shown = 0;
            void Row(string label, List<VisualElement> parts, string note = null)
            {
                var name = Ui.Text(Root, labelX, y, label, 20, Palette.TextDim, false, 1, 0.5f);
                var row = new VisualElement();
                Ui.Place(row, valueX, y, 0, 0.5f);
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                foreach (var p in parts) row.Add(p);
                Root.Add(row);
                var fade = new List<VisualElement> { name, row };
                if (note != null)
                {
                    fade.Add(Ui.Text(Root, valueX, y + 18, note, 16, Palette.TextDim, false, 0, 0, wrap: 410));
                    y += 4 + 19 * Mathf.CeilToInt(note.Length * 6.8f / 410);   // ~6.6 px a letter at 16 px
                }
                foreach (var e in fade)
                {
                    e.style.opacity = 0;
                    var at = 0.35f + shown * 0.15f;
                    Anim.Float(0, 1, 0.25f, a => e.style.opacity = a, Ease.Linear, at);
                }
                shown++;
                y += 44;
            }
            var recap = data.Recap;
            if (recap != null && recap.BiggestHit > 0)
            {
                var parts = new List<VisualElement> { Word(JsMath.Str(recap.BiggestHit), 28, Palette.TextBad, true), Word(recap.BiggestMove ?? "", 20, Palette.Text, false, 10) };
                if (recap.BiggestCrit) parts.Add(Word("critical", 16, Palette.TextDim, false, 8));
                // Its signature move: the trait says how to meet it ("Defend on the beat!").
                var trait = data.Monster?.Trait;
                var named = recap.BiggestMove != null && trait != null && trait.IndexOf(recap.BiggestMove, StringComparison.OrdinalIgnoreCase) >= 0;
                Row("Biggest hit", parts, named ? trait : null);
            }
            if (recap != null && recap.Resisted.Count > 0)
            {
                var parts = new List<VisualElement>();
                foreach (var kv in recap.Resisted.OrderByDescending(kv => kv.Value))
                {
                    var label = Db.Data.ElementLabels[kv.Key];
                    var r = ResistOf(data.Monster, kv.Key);
                    var icon = Token("el_" + kv.Key, r > 100 ? $"Absorbs {label}" : r >= 100 ? $"Immune to {label}" : $"Resists {label}",
                        $"{kv.Value} of your {recap.Hits} hits were {label}" + (r > 100 ? ": they healed it." : r >= 100 ? ": they did nothing." : $", {JsMath.Str(r)}% weaker."));
                    parts.Add(icon);
                    parts.Add(Word($"×{kv.Value}", 18, Palette.Text, true, 2, 10));
                }
                Row("Resisted", parts);
            }
            if (data.Monster != null)
            {
                var spots = WeakSpots(data.Monster);
                Row("Weak to", spots.Count > 0 ? spots : new List<VisualElement> { Word("nothing", 20, Palette.TextDim, false) });
            }
            if (data.Grit is double grit && grit > 0)
            {
                var max = grit >= Db.B.Grit.Max ? " (max)" : "";
                Row("Grit", new List<VisualElement> { Word($"+{Math.Round(grit * 100)}% damage against it next time{max}", 20, Palette.TextGood, false) });
            }
            var town = Db.Room(data.Wake.RoomId).Name;
            var wake = ToRoom(RoomArrival.Spawn(data.Wake.RoomId, (float)data.Wake.X, (float)data.Wake.Y));
            var lineY = Mathf.Max(486, y + 4);
            var buttonY = Mathf.Min(570, lineY + 56);
            if (data.Door == null)
            {
                Ui.Text(Root, cx, lineY, $"You wake up in {town}, fully rested.", 21, Palette.TextDim, false, 0.5f, 0.5f);
                ContinueButton("Wake up", wake, buttonY);
                return;
            }
            Ui.Text(Root, cx, lineY, $"Fully rested: try again at its door, or wake up in {town}.", 21, Palette.TextDim, false, 0.5f, 0.5f);
            var tryAgain = ToRoom(data.Door);
            go = tryAgain;
            var b1 = new UiButton(Root, cx - 150, buttonY, "Try again", tryAgain, 260, 56);
            var b2 = new UiButton(Root, cx + 150, buttonY, "Wake up", wake, 260, 56);
            foreach (var b in new[] { b1, b2 })
            {
                b.style.opacity = 0;
                Anim.Float(0, 1, 0.3f, a => b.style.opacity = a, Ease.Linear, 0.7f);
            }
            keysFrom = Time.time + 0.7f;
        }

        static Label Word(string text, float size, Color color, bool bold, float marginLeft = 0, float marginRight = 0)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.marginLeft = marginLeft;
            l.style.marginRight = marginRight;
            l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        static VisualElement Token(string icon, string title, string body)
        {
            var t = Felt.Icon(null, 0, 0, 30, icon);
            t.style.position = Position.Relative;
            t.style.marginRight = 4;
            t.pickingMode = PickingMode.Position;
            Tooltip.Attach(t, title, body);
            return t;
        }

        static double ResistOf(MonsterDef m, string element)
        {
            var resists = m?.Resists;
            double R(string e) => resists != null && resists.TryGetValue(e, out var v) ? v : 0;
            return R(element) + R("all");
        }

        /// <summary>The monster's weak spots as icons (the battle sheet's "Weak" icons): elements, then Blunt and Poison.</summary>
        static List<VisualElement> WeakSpots(MonsterDef m)
        {
            var parts = new List<VisualElement>();
            foreach (var e in Db.Data.Elements.Where(e => ResistOf(m, e) <= -25))
            {
                var label = Db.Data.ElementLabels[e];
                parts.Add(Token("el_" + e, $"Weak to {label}", $"Takes {JsMath.Str(-ResistOf(m, e))}% more from {label}."));
            }
            if (m.WeakTo?.Blunt > 0) parts.Add(Token("spot_blunt", "Weak to Blunt", $"Takes {JsMath.Str(m.WeakTo.Blunt.Value)}% more from Blunt skills."));
            if (m.WeakTo?.Poison > 0) parts.Add(Token("spot_poison", "Weak to Poison", $"Poison hurts it {JsMath.Str(m.WeakTo.Poison.Value)}% more."));
            return parts;
        }

        static readonly Dictionary<string, string> StatHelp = new Dictionary<string, string>
        {
            [Stat.Str] = "Melee damage", [Stat.Int] = "Magic, max MP", [Stat.Dex] = "Pierce, hit, crit", [Stat.End] = "Max HP",
        };

        /// <summary>1: the fight's rewards. 2: the level-up page.</summary>
        int page = 1;

        /// <summary>
        /// The level-up page: the level, what grew (before -> after), new skills, and the stat points to place right
        /// here (buttons, or keys 1-4); what's left over waits in the Character panel. `intro`: with the fanfare (the
        /// first time; placing a point redraws it without).
        /// </summary>
        void LevelPage(bool intro)
        {
            const float cx = Design.Width / 2;
            var player = Session.Player;
            var before = data.Before;
            page = 2;
            go = null;
            Root.Clear();

            var title = Ui.Text(Root, cx, 150, "Level Up!", 56, Palette.TextGold, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(title);
            Ui.Text(Root, cx, 204, $"Level {before.Level}  →  {player.Level}", 26, bold: true, ax: 0.5f, ay: 0.5f);
            HeroPortrait(player, cx - 235, 428, 190);

            // What grew. Stat points placed here show up at once.
            var rows = new List<(string label, double from, double to)> { ("Max HP", before.MaxHp, player.MaxHp), ("Max MP", before.MaxMp, player.MaxMp) };
            var stats = player.Stats;
            foreach (var k in Stat.Keys)
                if (stats[k] != before.Stats[k]) rows.Add((Player.StatLabels[k], before.Stats[k], stats[k]));
            const float x0 = cx - 100;
            var y = 244f;
            foreach (var (label, from, to) in rows)
            {
                Ui.Text(Root, x0, y, label, 20, ay: 0.5f);
                Ui.Text(Root, x0 + 170, y, JsMath.Str(from), 20, Palette.TextDim, false, 1, 0.5f);
                Ui.Text(Root, x0 + 196, y, "→", 20, Palette.TextDim, false, 0.5f, 0.5f);
                Ui.Text(Root, x0 + 222, y, JsMath.Str(to), 20, Palette.TextGood, true, 0, 0.5f);
                y += 28;
            }
            Ui.Text(Root, x0, y, "HP and MP restored", 18, Palette.TextGood, false, 0, 0.5f);
            y += 28;
            foreach (var skill in data.NewSkills)
            {
                Ui.Text(Root, x0, y, $"New skill: {skill}", 20, Palette.TextMagic, true, 0, 0.5f);
                y += 28;
            }
            if (Hud.NextUnlock(player) is string next) Ui.Text(Root, x0, y, $"Coming up  ·  {next}", 16, Palette.TextDim, false, 0, 0.5f);

            // The stat points, placed right here.
            var points = player.Data.StatPoints;
            Ui.Text(Root, cx, 448, points > 0 ? $"{points} stat point{(points > 1 ? "s" : "")} to place" : "All stat points placed", 19,
                points > 0 ? Palette.TextGold : Palette.TextDim, points > 0, 0.5f, 0.5f);
            for (int i = 0; i < Stat.Keys.Length; i++)
            {
                var k = Stat.Keys[i];
                var x = cx + (i - 1.5f) * 158;
                var text = $"{Player.StatLabels[k]}  {JsMath.Str(stats[k])}";
                if (points > 0) new UiButton(Root, x, 488, $"{text}   +", () => PlacePoint(k), 146, 44, 19);
                else Ui.Text(Root, x, 488, text, 20, bold: true, ax: 0.5f, ay: 0.5f);
                Ui.Text(Root, x, 529, StatHelp[k], 13, Palette.TextDim, false, 0.5f, 0.5f);
            }

            if (intro)
            {
                title.style.scale = new Scale(Vector3.one * 2);
                title.style.opacity = 0;
                Anim.Float(0, 1, 0.38f, t =>
                {
                    var s = Mathf.LerpUnclamped(2, 1, t);
                    title.style.scale = new Scale(new Vector3(s, s, 1));
                    title.style.opacity = Mathf.Clamp01(t);
                }, Ease.BackOut);
                Fx.LevelUpBurst(World, cx - 235, 428);
                Fx.Confetti(World);
                Sfx.Play("levelup");
            }
            ContinueButton("Continue", ToRoom(data.Back), 578, intro ? 0.5f : 0);
        }

        void PlacePoint(string stat)
        {
            var player = Session.Player;
            if (!player.SpendStatPoint(stat)) return;
            Saves.Save(player);
            Sfx.Play("equip");
            LevelPage(false);
        }

        /// <summary>Leave for a room (once).</summary>
        Action ToRoom(RoomArrival back)
        {
            var gone = false;
            return () =>
            {
                if (gone) return;
                gone = true;
                GameRoot.I.Go(new RoomScreen(back));
            };
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
        void DropReveal(float cx, float y, List<ItemGrant> items, List<ItemGrant> lost, float width = 680)
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
            if (shown.Count == 0) return;
            foreach (var c in shown)
            {
                c.Info = ItemUi.GradeInfo(c.Def, c.G.Traits());
                c.Tier = c.IsLost ? "plain" : c.Info.Sigil ?? (c.Def.IsGear && c.Info.Tier != "standard" && c.Info.Tier != "fair" ? c.Info.Tier : "plain");
            }
            var cards = shown.OrderBy(c => Array.IndexOf(RevealOrder, c.Tier)).ToList();

            const float step = 128;
            var scale = Mathf.Min(1, width / (cards.Count * step));
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
                if (windup > 0) Anim.After(at, () => { if (page == 1) WindUp(screenX, screenY, c.Tier, windup); });
                Anim.After(pop, () => { if (page == 1) PopCard(card, screenX, screenY, c.Tier, c.Info.Frame, label); });
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
                Fx.Burst(World, x, y, "fx_star", master ? 36 : tier == "superior" ? 20 : 10, 60, master ? 320 : 200, master ? 1 : 0.7f, master ? 1 : 0.7f, color, true);
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
            Fx.Burst(World, x, y, "fx_star", eclipse ? 60 : big ? 36 : 24, 80, eclipse ? 420 : 280, big ? 1.1f : 0.8f, eclipse ? 1.4f : 1, colors.Color, true);
            Anim.Float(1, 0.35f, 0.14f, a => text.style.opacity = a, Ease.Linear, repeat: 3, yoyo: true);
            GameRoot.I.Flash(eclipse ? 0.6f : 0.3f, colors.Color);
            if (eclipse) GameRoot.I.Shake(0.4f, 10);
            var banner = Ui.Text(Root, Design.Width / 2, 62, $"{Items.SigilNames[tier].ToUpperInvariant()}!", eclipse ? 46 : 36, colors.TextColor, true, 0.5f, 0.5f, stroke: true);
            banner.AddToClassList(FeltPass.Keep);
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

        /// <summary>A tower floor's result: marks and items on a first clear, the twists again on a loss; Next floor / Retry and Back.</summary>
        void TowerResult(float cx, bool won)
        {
            var t = data.Tower;
            var player = Session.Player;
            if (won)
            {
                Anim.After(0.25f, () => Fx.Confetti(World));
                Sfx.Play("victory");
                Ui.Text(Root, cx, 240, $"Floor {t.Floor} cleared!", 28, Palette.Text, true, 0.5f, 0.5f);
                if (t.Clear != null)
                {
                    var marks = Ui.Text(Root, cx, 290, "+0 marks", 30, Palette.TextGold, true, 0.5f, 0.5f);
                    CountUp(marks, t.Clear.Marks, " marks", 0.4f);
                    // Gear (the Sigil piece) revealed like a drop.
                    var gear = t.Clear.Items.Where(g => Db.Item(g.ItemId).IsGear).ToList();
                    if (gear.Count > 0) DropReveal(cx, 360, gear, data.Lost);
                }
                else Ui.Text(Root, cx, 290, "Practice run: this floor paid out on its first clear.", 20, Palette.TextDim, false, 0.5f, 0.5f);
                var hasGear = t.Clear?.Items.Any(g => Db.Item(g.ItemId).IsGear) == true;
                Ui.Text(Root, cx, hasGear ? 470 : 400, $"Marks: {player.Data.Tower.Marks}   ·   Best floor: {player.Data.Tower.Best} / {Tower.LastFloor}", 19, Palette.TextDim, false, 0.5f, 0.5f);
                if (t.Next is int next) TowerButtons("Next floor", () => TowerPanel.StartFloor(player, next), 560);
                else ContinueButton("Back", ToRoom(data.Back), 560);
            }
            else
            {
                GameRoot.I.Shake(0.3f, 8);
                var lines = new List<string> { $"{Capitalize(The(data.MonsterName))} got the better of you on floor {t.Floor}.", "" };
                lines.AddRange(t.Twists.Select(Db.Twist).Select(tw => $"{tw.Name}: {tw.Text}"));
                lines.Add("");
                lines.Add("Your HP is back where it was. Change skills or gear, or go again.");
                Ui.Text(Root, cx, 340, string.Join("\n", lines), 22, ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter, wrap: 640);
                TowerButtons("Retry", () => TowerPanel.StartFloor(player, t.Floor), 540);
            }
        }

        /// <summary>Two buttons: `main` (also Space or Enter) and Back to the room.</summary>
        void TowerButtons(string label, Action main, float y)
        {
            var gone = false;
            Action Once(Action a) => () =>
            {
                if (gone) return;
                gone = true;
                a();
            };
            go = Once(main);
            var a1 = new UiButton(Root, Design.Width / 2 - 160, y, label, go, 280, 64);
            var a2 = new UiButton(Root, Design.Width / 2 + 160, y, "Back", Once(() => GameRoot.I.Go(new RoomScreen(data.Back))), 280, 64);
            foreach (var b in new[] { a1, a2 })
            {
                b.style.opacity = 0;
                Anim.Float(0, 1, 0.3f, a => b.style.opacity = a, Ease.Linear, 0.7f);
            }
            keysFrom = Time.time + 0.7f;
        }

        /// <summary>The button at the bottom (also Space, Enter or E), usable `delay` s in.</summary>
        void ContinueButton(string label, Action run, float y, float delay = 0.7f)
        {
            go = run;
            var btn = new UiButton(Root, Design.Width / 2, y, label, run, 280, 56);
            keysFrom = Time.time + delay;
            if (delay <= 0) return;
            btn.style.opacity = 0;
            Anim.Float(0, 1, 0.3f, a => btn.style.opacity = a, Ease.Linear, delay);
        }

        public override void Tick(float dt)
        {
            if (!InputEnabled) return;
            if (page == 2)
            {
                // Keys 1-4 place a stat point.
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
                for (int i = 0; i < keys.Length; i++)
                    if (Keys.Pressed(keys[i])) { PlacePoint(Stat.Keys[i]); return; }
            }
            if (go == null || Time.time < keysFrom) return;
            if (Keys.Pressed(Key.Space) || Keys.Pressed(Key.Enter) || Keys.Pressed(Key.E)) go();
        }
    }
}
