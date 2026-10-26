using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>
    /// A walkable screen (RoomScene.ts): move with WASD/arrows or by clicking, talk to NPCs, bump into monsters to
    /// fight. Characters and props are world sprites; names, prompts and menus are UI on top.
    /// </summary>
    public class RoomScreen : GameScreen
    {
        /// <summary>Characters are drawn a bit smaller on maps than in battle.</summary>
        const float MapScale = 0.85f;
        const float Edge = 36;
        /// <summary>Place names over the scene: cream Alegreya with an ink outline, like a map's lettering.</summary>
        static readonly Color MapLabel = Design.Hex(0xf6e7c1);
        static readonly Vector2 TalkRange = new Vector2(130, 80);
        static readonly Vector2 TouchRange = new Vector2(62, 34);
        /// <summary>Roaming monsters keep at least this far apart (x), unless they are on clearly different rows (y).</summary>
        static readonly Vector2 MobGap = new Vector2(150, 70);
        /// <summary>World order for exit arrows and other things drawn over everyone.</summary>
        const int Overlay = 30000;
        const float LabelHeight = 21;
        static readonly Color EliteGlow = Design.Hex(0xffc23d);
        static readonly Color LuckyGlow = Design.Hex(0x7dffb0);
        static readonly Color LuckyText = Design.Css("#8dffbe");

        static readonly Dictionary<string, string> BlockerLabels = new Dictionary<string, string>
        {
            ["thorns"] = "Blocked by\nthorns", ["barricade"] = "Barricaded", ["reeds"] = "Flooded", ["snowdrift"] = "Snowed in",
        };

        static readonly Dictionary<string, string> BlockerBump = new Dictionary<string, string>
        {
            ["thorns"] = "A wall of thorns blocks the way.",
            ["barricade"] = "The road is barricaded. The Elder will know why.",
            ["reeds"] = "Deep water and thick reeds block the way.",
            ["snowdrift"] = "Snow is piled higher than your head. Someone in town might know a way through.",
        };

        static bool showedControls;

        class Actor
        {
            public Figure Fig;
            public float X, Y, Scale, Height, WalkT, Hop;
            public int Facing = 1;
            public bool Moving;
            public Label Label;
        }

        class Npc : Actor
        {
            public NpcDef Def;
            public Label Marker;
        }

        class Mob : Actor
        {
            public int Uid;
            public MonsterDef Def;
            public string Elite;
            public SpriteRenderer Aura;
            public Label Tag;
            public Transform Sparkle;
            /// <summary>idle, wander, alert, chase or run (a Lucky Impling running from you).</summary>
            public string State = "idle";
            public float Timer;
            public Vector2? Target;
        }

        class BossActor : Actor
        {
            public MonsterDef Def;
        }

        /// <summary>Things in a room you walk up to and use with E: a campfire, or something to look at.</summary>
        class Thing
        {
            public string Kind;
            public float X, Y;
            public InspectDef Def;
            public SpriteRenderer Star;
        }

        class ChestView
        {
            public Chest Data;
            public Transform Fig;
            public Transform Lid;
            public Hit Hit;
        }

        /// <summary>A clickable spot in the world (design px).</summary>
        class Hit
        {
            public Rect Area;
            public Action Click;
            public Action<bool> Hover;
            public int Order;
            public bool Hovered, Removed;
        }

        class Cutscene
        {
            public Action Next, Skip;
        }

        readonly RoomArrival data;
        RoomDef room;
        Actor player;
        string playerGear = "";
        readonly List<Npc> npcs = new List<Npc>();
        readonly List<Mob> mobs = new List<Mob>();
        readonly List<Hit> hits = new List<Hit>();
        readonly List<Thing> things = new List<Thing>();
        BossActor boss;
        Hud hud;
        UiButton charBtn, invBtn;
        Anim.Handle charPulse;
        UiModal inventory, mapModal;
        VisualElement labels, ui, tracker, prompt;
        Label promptText;
        float wallBumpAt;
        bool bossAsked;
        Vector2? moveTarget;
        Npc pendingNpc;
        RoomExit pendingExit;
        Thing pendingThing;
        Cutscene cutscene;
        float safeUntil;
        bool leaving;
        float dustTimer;
        int nextUid = 1;
        float newsAt;
        ChestView chest;
        Hit pressed;
        readonly List<float> titleTaps = new List<float>();

        static IRng Rng => Dice.Rng;
        static Player P => Session.Player;
        float Now => Time.time;
        float Top => (float)room.Walk[0];
        float Bottom => (float)room.Walk[1];

        public RoomScreen(RoomArrival data) => this.data = data;

        public override void Enter()
        {
            room = Db.Room(data.RoomId);
            // Labels over heads sit under the menus: everything added to Root later draws above them.
            labels = Layer("labels");
            ui = Layer("ui");

            Backdrop.Draw(World, room.Background, room.Backdrop);
            if (room.Ambient != null) Fx.Ambient(World, room.Ambient);
            DrawExits();
            // A fight in the room doesn't end the visit; walking in again does (one campfire rest per visit).
            if (data.Via != "battle") Session.RestedAt = null;
            DrawThings();

            // Some people only show up once their chapter unlocks them (Hulda and her forge).
            foreach (var def in room.Npcs) if (def.Feature == null || Features.On(P, def.Feature)) AddNpc(def);
            SpawnPlayer();
            // A moment of safety on arrival so nothing jumps you mid-fade.
            if (data.Via != "spawn") safeUntil = Now + 1.2f;
            SpawnMonsters();
            SpawnBoss();
            BuildUi(data.Via != "battle");
            RefreshQuests();

            var firstVisit = Travel.VisitRoom(P, room.Id);
            SaveLocation();
            if (data.Via == "spawn" && data.Recalled)
            {
                Sfx.Play("recall");
                Fx.HealSparkles(World, player.X, player.Y, 0xffe9a8);
                Ui.Toast($"You arrive in {room.Name}.", Palette.TextGold);
            }
            if (firstVisit && room.FirstVisit != null && data.Via != "battle")
            {
                // Wait out any popup, then play the room's opening lines.
                void Play()
                {
                    if (ModalOpen) Anim.After(0.3f, Play);
                    else PlayFirstVisit();
                }
                Anim.After(0.6f, Play);
            }
            if (data.Via == "spawn" && data.Intro)
            {
                var elder = npcs.Find(n => n.Def.Role.Kind == "talk");
                // Wait out the "New!" popup (a new hero's first skill) so the greeting isn't skipped.
                void Greet()
                {
                    if (ModalOpen) Anim.After(0.3f, Greet);
                    else Interact(elder, true);
                }
                if (elder != null) Anim.After(0.5f, Greet);
            }
        }

        VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Ui.Place(e, 0, 0);
            Ui.Size(e, Design.Width, Design.Height);
            Root.Add(e);
            return e;
        }

        // --- Setup ---

        void SpawnPlayer()
        {
            float x, y;
            if (data.Via == "exit" && data.Side == "back")
            {
                // Step out of the doorway, just below it.
                x = data.HasX ? data.X : Design.Width / 2;
                y = Top + 24;
            }
            else if (data.Via == "exit" && data.Side == "down")
            {
                // Climb back up from the path at the bottom edge.
                x = data.HasX ? data.X : Design.Width / 2;
                y = Bottom - 24;
            }
            else if (data.Via == "exit")
            {
                x = data.Side == "left" ? Edge + 50 : Design.Width - Edge - 50;
                y = data.Y;
            }
            else
            {
                x = data.X;
                y = data.Y;
            }
            var fig = Figure.Create(World, x, y, P.Look, 1, P.Weapon, P.WeaponSigil);
            if (P.WeaponSigil != null) Fx.SigilGlow(fig, P.WeaponSigil);
            player = MakeActor<Actor>(fig, x, y, (float)P.Look.Scale);
            playerGear = GearKey();
            if (data.Via == "exit") player.Facing = data.Side == "right" ? -1 : 1;
            ClampToWalk(player);
            Place(player);
        }

        void SpawnMonsters()
        {
            if (room.ZoneId == null) return;
            var memory = Session.RoomMemory(room.Id);
            List<RoamingMonster> roaming = null;
            Chest chestData = null;
            if (data.Via == "battle")
            {
                var snap = Session.TakeRoom(room.Id);
                if (snap != null)
                {
                    // Beaten, or ran away (a Lucky Impling): either way it's gone.
                    var won = data.Outcome == "victory";
                    var gone = won || data.Outcome == "escaped";
                    roaming = snap.Monsters.Where(m => !(gone && m.Uid == snap.FightingUid)).ToList();
                    nextUid = snap.Monsters.Select(m => m.Uid).DefaultIfEmpty(0).Max() + 1;
                    // A beaten mimic is gone; a chest you didn't touch is still there.
                    chestData = won && snap.FightingUid == Monsters.MimicUid ? null : memory?.Chest;
                    if (data.Outcome == "fled")
                    {
                        safeUntil = Now + (float)Db.B.FleeGraceSeconds;
                        BlinkPlayer();
                    }
                    if (data.Outcome == "escaped") safeUntil = Now + (float)Db.B.FleeGraceSeconds;
                }
            }

            if (roaming == null && memory != null)
            {
                // Back in a room you cleared: the dead stay dead, and one monster wanders back per `respawnSeconds` away.
                roaming = memory.Monsters.Select(m => new RoamingMonster { Uid = m.Uid, MonsterId = m.MonsterId, Elite = m.Elite, X = m.X, Y = m.Y }).ToList();
                chestData = memory.Chest;
                nextUid = roaming.Select(m => m.Uid).DefaultIfEmpty(0).Max() + 1;
                var back = (int)Math.Floor((Time.realtimeSinceStartupAsDouble - memory.LeftAt) / Db.B.RespawnSeconds);
                var missing = Math.Max(0, (room.MonsterCount ?? 0) - roaming.Count);
                for (int i = 0; i < Math.Min(back, missing); i++) roaming.Add(NewRoamer(roaming));
                // Fully refilled: another chance at a chest.
                if (chestData == null && missing > 0 && back >= missing) chestData = NewChest(roaming);
            }

            if (roaming == null)
            {
                roaming = new List<RoamingMonster>();
                for (int i = 0; i < (room.MonsterCount ?? 0); i++) roaming.Add(NewRoamer(roaming));
                chestData = NewChest(roaming, true);
                // Testing: a new character's first monster room has a Lucky Impling.
                if (Session.LuckyNext && roaming.Count > 0 && !roaming.Any(m => Monsters.IsLucky(m.MonsterId)))
                {
                    Session.LuckyNext = false;
                    var last = roaming[roaming.Count - 1];
                    roaming[roaming.Count - 1] = new RoamingMonster { Uid = last.Uid, MonsterId = Monsters.LuckyId, X = last.X, Y = last.Y };
                }
            }
            // Give some room: nobody stays right next to you when you arrive or come back from a fight.
            foreach (var m in roaming)
            {
                if (Math.Abs(m.X - player.X) < 260)
                {
                    var away = m.X < player.X ? -1 : 1;
                    var x = player.X + away * 260;
                    m.X = x < 80 || x > Design.Width - 80 ? player.X - away * 260 : x;
                }
            }
            foreach (var r in roaming) AddMob(r);
            if (chestData != null) AddChest(chestData);
        }

        /// <summary>A freshly spawned roaming monster (maybe an Elite, at most `BALANCE.elite.maxPerRoom` per room).</summary>
        RoamingMonster NewRoamer(List<RoamingMonster> taken)
        {
            var zone = Db.Zone(room.ZoneId);
            var pos = FindSpawnPoint(taken.Select(m => new Vector2((float)m.X, (float)m.Y)));
            // Monsters are visible, so tougher ones can appear; their level label warns you.
            // Now and then a Lucky Impling instead (at most one per room).
            if (!taken.Any(m => Monsters.IsLucky(m.MonsterId)) && Monsters.RollLucky(Rng))
                return new RoamingMonster { Uid = nextUid++, MonsterId = Monsters.LuckyId, X = pos.x, Y = pos.y };
            var monsterId = Rng.WeightedPick(zone.Encounters, e => e.Weight).MonsterId;
            var elites = taken.Count(m => m.Elite != null);
            var elitesOn = Features.On(P, "elites") || Cheats.Elites;
            var elite = elitesOn && (elites < Db.B.Elite.MaxPerRoom || Cheats.Elites)
                ? Monsters.RollElite(Rng, Quests.WantsElite(P) || Guild.HuntWantsElite(P))
                : null;
            return new RoamingMonster { Uid = nextUid++, MonsterId = monsterId, Elite = elite, X = pos.x, Y = pos.y };
        }

        /// <summary>Maybe a treasure chest, placed away from the monsters. A side room's own chest is always there on its first fill.</summary>
        Chest NewChest(List<RoamingMonster> taken, bool first = false)
        {
            if (!Features.On(P, "chests")) return null;
            if (first && room.Chest != null) return new Chest { X = room.Chest.X, Y = room.Chest.Y, Mimic = false, Book = room.Chest.Book };
            var roll = Monsters.RollChest(Rng);
            if (roll == null) return null;
            var pos = FindSpawnPoint(taken.Select(m => new Vector2((float)m.X, (float)m.Y)));
            return new Chest { X = pos.x, Y = pos.y, Mimic = roll.Value && Features.On(P, "mimics") };
        }

        List<RoamingMonster> Roamers() =>
            mobs.Select(x => new RoamingMonster { Uid = x.Uid, MonsterId = x.Def.Id, Elite = x.Elite, X = x.X, Y = x.Y }).ToList();

        /// <summary>Keep this room's survivors and chest for when you come back.</summary>
        void RememberRoom()
        {
            if (room.ZoneId == null) return;
            Session.RememberRoom(room.Id, new RoomMemory { Monsters = Roamers(), Chest = chest?.Data, LeftAt = Time.realtimeSinceStartupAsDouble });
        }

        // --- Treasure chests ---

        /// <summary>A drawn chest. Mimics give themselves away a little: they breathe, and now and then their lid twitches.</summary>
        void AddChest(Chest c)
        {
            float x = (float)c.X, y = (float)c.Y;
            var fig = new GameObject("chest").transform;
            fig.SetParent(World, false);
            fig.position = Design.World(x, y);
            var order = Design.Depth(y);
            var body = Shapes.Create(fig, x, y, order);
            body.Ellipse(new Color(0, 0, 0, 0.35f), 0, 0, 90, 18);
            body.Rect(Design.Hex(0x8d5a2b), -38, -49, 76, 46).StrokeRect(Design.Hex(0x3e2723), 3, -38, -49, 76, 46);
            body.Rect(Design.Hex(0xd4a017), -38, -30, 76, 8);
            Shapes teeth = null;
            if (c.Mimic)
            {
                teeth = Shapes.Create(fig, x, y, order + 1, "teeth");
                for (int i = -3; i <= 3; i++) teeth.Tri(Design.Hex(0xf5f0e1), i * 10 - 4, -41, i * 10, -34, i * 10 + 4, -41);
                teeth.Alpha = 0;
            }
            var lid = Shapes.Create(fig, x, y - 52, order + 2, "lid");
            lid.Rect(Design.Hex(0x9c6631), -40, -11, 80, 22).StrokeRect(Design.Hex(0x3e2723), 3, -40, -11, 80, 22);
            lid.Rect(Design.Hex(0xd4a017), -40, -3, 80, 6);
            var lockShape = Shapes.Create(fig, x, y - 40, order + 3, "lock");
            lockShape.Rect(Design.Hex(0xf1c40f), -6, -7, 12, 14).StrokeRect(Design.Hex(0x3e2723), 2, -6, -7, 12, 14);
            var view = new ChestView { Data = c, Fig = fig, Lid = lid.transform };
            if (c.Mimic)
            {
                Anim.Float(1, 1.04f, 1.1f, s => { if (fig != null) fig.localScale = new Vector3(1, s, 1); }, Ease.SineInOut, repeat: -1, yoyo: true);
                void Twitch()
                {
                    if (fig == null || chest != view) return;
                    if (Math.Abs(player.X - x) <= 360)
                    {
                        teeth.Alpha = 1;
                        Anim.Float(0, 1, 0.12f, t =>
                        {
                            if (lid == null) return;
                            lid.transform.localPosition = new Vector3(0, (52 + 8 * t) / Design.PixelsPerUnit, 0);
                            lid.transform.localRotation = Quaternion.Euler(0, 0, 8 * t);
                        }, Ease.Linear, yoyo: true).OnComplete(() => { if (teeth != null) teeth.Alpha = 0; });
                    }
                    Anim.After(3.2f, Twitch);
                }
                Anim.After(3.2f, Twitch);
            }
            else
            {
                void Glint()
                {
                    if (fig == null || chest != view) return;
                    Fx.HitSparks(World, x + 20, y - 50, false, false);
                    Anim.After(2.6f, Glint);
                }
                Anim.After(2.6f, Glint);
            }
            view.Hit = AddHit(x, y - 35, 100, 80, () =>
            {
                if (ModalOpen || leaving) return;
                pendingNpc = null;
                pendingExit = null;
                moveTarget = new Vector2(x + (player.X < x ? -40 : 40), y);
            }, order + 4);
            chest = view;
        }

        /// <summary>"New!": systems this chapter just opened, and skills just learned. Shown once each.</summary>
        void ShowNews()
        {
            var news = Features.Unseen(P);
            if (!news.Any) return;
            Features.MarkSeen(P, news);
            Save(false);
            var rows = new List<(string title, string text, Color color, string icon)>();
            foreach (var f in news.Features) rows.Add((f.Name, f.Text, Palette.TextGold, null));
            foreach (var s in news.Skills)
            {
                var element = s.Element != null && s.Element != "weapon" ? $" · {Db.Data.ElementLabels[s.Element]}" : "";
                var cd = s.Cooldown > 0 ? $" · cooldown {s.Cooldown}" : "";
                var slot = P.IsCarried(s.Id) ? "" : $" Your {Db.B.SkillSlots} skill slots are full: carry it from the Character panel (C).";
                rows.Add(($"New skill: {s.Name}  ({s.MpCost} MP{element}{cd})", s.Description + slot, Palette.TextMagic, "skill_" + s.Id));
            }
            Sfx.Play("quest");
            var height = Math.Min(640, 150 + rows.Count * 84);
            var modal = new UiModal(760, height, "New!", Refresh);
            var list = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(list, 40, 80);
            list.style.width = 680;
            list.style.flexDirection = FlexDirection.Column;
            modal.Content.Add(list);
            foreach (var r in rows)
            {
                var title = FlowText(r.title, 20, r.color, true, 4);
                if (Felt.HasIcon(r.icon))
                {
                    // A new skill shows its drawing next to its name.
                    var head = new VisualElement { pickingMode = PickingMode.Ignore };
                    head.style.flexDirection = FlexDirection.Row;
                    head.style.alignItems = Align.Center;
                    head.style.marginBottom = 4;
                    var ic = Felt.Icon(null, 0, 0, 30, r.icon, true);
                    ic.style.position = Position.Relative;
                    ic.style.marginRight = 8;
                    head.Add(ic);
                    title.style.marginBottom = 0;
                    head.Add(title);
                    list.Add(head);
                }
                else list.Add(title);
                list.Add(FlowText(r.text, 16, Palette.Text, false, 14));
            }
            new UiButton(modal.Content, 380, height - 44, "Got it", () => modal.Close(Refresh), 180, 48);
        }

        static Label FlowText(string text, float size, Color color, bool bold, float gapBelow)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.marginBottom = gapBelow;
            return l;
        }

        /// <summary>Walking into a chest opens it.</summary>
        void CheckChest()
        {
            var c = chest;
            if (c == null || ModalOpen) return;
            if (Math.Abs(player.X - c.Data.X) > 56 || Math.Abs(player.Y - c.Data.Y) > 34) return;
            chest = null;
            RemoveHit(c.Hit);
            if (c.Data.Mimic)
            {
                SpringMimic(c);
                return;
            }
            var rewards = Monsters.RollChestLoot(Db.Zone(room.ZoneId), Rng, Features.On(P, "sigils"), Features.On(P, "materials"));
            var book = c.Data.Book != null ? Books.ChestBook(P, room) : null;
            if (book != null) rewards.Items.Add(new ItemGrant { ItemId = book });
            var (_, lost) = P.GrantRewards(rewards, book != null);
            Sfx.Play("quest");
            var lid = c.Lid;
            var lidFrom = lid.localPosition;
            Anim.Float(0, 1, 0.22f, t =>
            {
                if (lid == null) return;
                lid.localPosition = lidFrom + new Vector3(0, 18 * t / Design.PixelsPerUnit, 0);
                lid.localRotation = Quaternion.Euler(0, 0, 25 * t);
            });
            Fx.HealSparkles(World, (float)c.Data.X, (float)c.Data.Y, 0xffd34d);
            var fig = c.Fig;
            Anim.Float(1, 0, 0.4f, a =>
            {
                if (fig == null) return;
                foreach (var s in fig.GetComponentsInChildren<Shapes>()) s.Alpha = a;
            }, Ease.Linear, 0.9f).OnComplete(() => { if (fig != null) UnityEngine.Object.Destroy(fig.gameObject); });
            var items = rewards.Items.Where(g => !lost.Contains(g)).ToList();
            // Stacks read as "Iron Scrap x2".
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            var names = new List<string>();
            foreach (var g in items)
            {
                var def = Db.Item(g.ItemId);
                if (g.Rolls != null) names.Add(ItemUi.LootName(def, g.Traits()));
                else
                {
                    if (!counts.ContainsKey(def.Name))
                    {
                        counts[def.Name] = 0;
                        order.Add(def.Name);
                    }
                    counts[def.Name]++;
                }
            }
            foreach (var n in order) names.Add(counts[n] > 1 ? $"{n} x{counts[n]}" : n);
            Ui.Toast("Treasure!", Palette.TextGold);
            Ui.Toast(string.Join("   ", new[] { $"+{rewards.Gold} gold" }.Concat(names)), Palette.TextGood);
            if (lost.Count > 0) Ui.Toast($"Bag full! {lost.Count} left behind", Palette.TextBad);
            _ = Fx.CoinShower(World, new Vector2((float)c.Data.X, (float)c.Data.Y - 60), new Vector2(320, 40), (int)Math.Ceiling(rewards.Gold / 8), () => Sfx.Play("coin"));
            Refresh();
        }

        /// <summary>It was a mimic: it jumps up, and the fight starts.</summary>
        void SpringMimic(ChestView c)
        {
            if (leaving) return;
            leaving = true;
            ShowPrompt(false);
            Sfx.Play("warning");
            Ui.Toast("The chest has teeth!", Palette.TextBad);
            GameRoot.I.Shake(0.3f, 15);
            var fig = c.Fig;
            var home = fig.position;
            Anim.Float(0, -30, 0.16f, v => { if (fig != null) fig.position = home + new Vector3(0, -v / Design.PixelsPerUnit, 0); }, Ease.CubicOut, repeat: 1, yoyo: true);
            // Remember the room with the mimic still in it, in case you run away.
            Session.SaveRoom(new RoomSnapshot { RoomId = room.Id, FightingUid = Monsters.MimicUid, Monsters = Roamers() });
            chest = new ChestView { Data = c.Data, Fig = fig, Lid = c.Lid };
            RememberRoom();
            SaveLocation();
            var setup = new BattleSetup { ZoneId = room.ZoneId, MonsterId = "mimic", Mimic = true, RoomId = room.Id, X = player.X, Y = player.Y };
            Anim.After(0.65f, () => GameRoot.I.Go(new BattleScreen(setup), 0.2f));
        }

        /// <summary>A spot away from the player and from `taken` (monsters placed so far). Falls back to the roomiest try.</summary>
        Vector2 FindSpawnPoint(IEnumerable<Vector2> takenSeq)
        {
            var taken = takenSeq.ToList();
            var best = new Vector2(640, (Top + Bottom) / 2);
            var bestScore = float.NegativeInfinity;
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector2(Random.Range(180, (int)Design.Width - 140 + 1), Random.Range((int)Top + 10, (int)Bottom - 10 + 1));
                var fromPlayer = Math.Abs(p.x - player.X);
                var fromOthers = taken.Count == 0 ? float.PositiveInfinity : taken.Min(m => Math.Abs(m.x - p.x));
                if (fromPlayer > 320 && fromOthers > MobGap.x) return p;
                var score = Math.Min(fromPlayer, 320) + Math.Min(fromOthers, MobGap.x * 2);
                if (score > bestScore)
                {
                    best = p;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>Push roaming monsters that crowd each other apart, a little each frame.</summary>
        void SeparateMobs(float dt)
        {
            var push = 90 * dt;
            for (int i = 0; i < mobs.Count; i++)
            {
                for (int j = i + 1; j < mobs.Count; j++)
                {
                    var a = mobs[i];
                    var b = mobs[j];
                    var dx = b.X - a.X;
                    if (Math.Abs(dx) >= MobGap.x || Math.Abs(b.Y - a.Y) >= MobGap.y) continue;
                    var dir = dx == 0 ? (a.Uid < b.Uid ? 1 : -1) : Math.Sign(dx);
                    // Chasers keep their pace; only the calmer one yields.
                    if (a.State != "chase") a.X -= dir * push;
                    if (b.State != "chase") b.X += dir * push;
                }
            }
        }

        string GearKey()
        {
            var eq = P.Data.Equipment;
            return $"{eq["weapon"]?.Uid}|{eq["body"]?.Uid}";
        }

        /// <summary>The hero holds the equipped weapon and wears the armor's outfit, so redraw them after a gear swap.</summary>
        void RedrawPlayerIfRearmed()
        {
            var key = GearKey();
            if (key == playerGear) return;
            playerGear = key;
            var old = player.Fig;
            player.Fig = Figure.Create(World, player.X, player.Y, P.Look, 1, P.Weapon, P.WeaponSigil);
            if (P.WeaponSigil != null) Fx.SigilGlow(player.Fig, P.WeaponSigil);
            player.Height = player.Fig.DrawnHeight;
            player.Scale = (float)P.Look.Scale * MapScale;
            UnityEngine.Object.Destroy(old.gameObject);
            Place(player);
        }

        T MakeActor<T>(Figure fig, float x, float y, float scale) where T : Actor, new() =>
            new T { Fig = fig, X = x, Y = y, Scale = scale * MapScale, Height = fig.DrawnHeight, Facing = 1 };

        Label WorldLabel(string text, float size, Color color) => Ui.Text(labels, 0, 0, text, size, color, true, 0.5f, 1, stroke: true);

        void AddNpc(NpcDef def)
        {
            float x = (float)def.X, y = (float)def.Y;
            var fig = Figure.Create(World, x, y, def.Look, 1);
            var npc = MakeActor<Npc>(fig, x, y, (float)def.Look.Scale);
            npc.Def = def;
            npc.Label = WorldLabel(def.Name, 17, Palette.Text);
            npcs.Add(npc);
            Place(npc);
            var h = npc.Height * npc.Scale;
            AddHit(x, y - h * 0.5f, 120, h, () =>
            {
                if (ModalOpen || leaving) return;
                if (InTalkRange(npc)) Interact(npc);
                else
                {
                    pendingNpc = npc;
                    var side = player.X < npc.X ? -1 : 1;
                    moveTarget = new Vector2(npc.X + side * 90, npc.Y + 10);
                }
            }, Design.Depth(y) + 1, on => npc.Label.style.color = on ? Palette.TextGold : Palette.Text);
        }

        void AddMob(RoamingMonster r)
        {
            var zone = Db.Zone(room.ZoneId);
            var baseDef = Monsters.IsLucky(r.MonsterId) ? Monsters.MakeImpling(zone) : Db.Monster(r.MonsterId);
            var def = r.Elite != null ? Monsters.MakeElite(baseDef, r.Elite) : baseDef;
            float x = (float)r.X, y = (float)r.Y;
            var fig = Figure.Create(World, x, y, def.Look, 1);
            var mob = MakeActor<Mob>(fig, x, y, (float)def.Look.Scale * 0.85f);
            var diff = def.Level - P.Level;
            var color = diff >= 2 ? Palette.TextBad : diff == 1 ? Palette.TextGold : diff <= -2 ? Palette.TextDim : Palette.Text;
            // A shape as well as the color, so level vs yours reads without color: ▲▲ much higher, ▲ higher, ▼ much lower.
            var mark = diff >= 2 ? "▲▲ " : diff == 1 ? "▲ " : diff <= -2 ? "▼ " : "";
            mob.Label = WorldLabel($"{def.Name}  {mark}Lv {def.Level}{(def.Tough ? "  ·  Tough" : "")}", 15, color);
            mob.Uid = r.Uid;
            mob.Def = def;
            mob.Timer = Random.Range(0.3f, 1.5f);
            mob.Facing = r.X > player.X ? -1 : 1;
            if (r.Elite != null)
            {
                mob.Elite = r.Elite;
                mob.Aura = Props.Glow(World, x, y, EliteGlow, 5, 5, Design.Depth(y) - 1);
                Pulse(mob.Aura, EliteGlow, 0.3f, 0.55f, 5, 5.8f, 0.9f);
                mob.Tag = WorldLabel("ELITE", 14, Palette.TextGold);
            }
            if (def.Lucky)
            {
                mob.Aura = Props.Glow(World, x, y, LuckyGlow, 3.5f, 3.5f, Design.Depth(y) - 1);
                Pulse(mob.Aura, LuckyGlow, 0.3f, 0.6f, 3.5f, 4.2f, 0.6f);
                mob.Tag = WorldLabel("LUCKY", 14, LuckyText);
                mob.Sparkle = Twinkles(Design.Depth(y) + 2);
            }
            mobs.Add(mob);
            Place(mob);
            PlaceEliteMarks(mob);
            // Pop in.
            fig.SetAlpha(0);
            Anim.Float(0, 1, 0.3f, a => { if (fig != null) fig.SetAlpha(a); }, Ease.Linear, 0.15f);
        }

        /// <summary>A glow breathing between two alphas and sizes (sizes like the web's fx_soft scale).</summary>
        static void Pulse(SpriteRenderer glow, Color color, float a0, float a1, float s0, float s1, float seconds)
        {
            Anim.Float(0, 1, seconds, t =>
            {
                if (glow == null) return;
                glow.color = new Color(color.r, color.g, color.b, Mathf.Lerp(a0, a1, t));
                var s = Mathf.Lerp(s0, s1, t) / 2;
                glow.transform.localScale = new Vector3(s, s, 1);
            }, Ease.SineInOut, repeat: -1, yoyo: true);
        }

        /// <summary>Gold twinkles rising around a Lucky Impling (moved with it).</summary>
        Transform Twinkles(int order)
        {
            var root = new GameObject("twinkles").transform;
            root.SetParent(World, false);
            Color[] tints = { Design.Hex(0xffe066), Design.Hex(0xfff6c4), Design.Hex(0x8dffbe) };
            for (int i = 0; i < 6; i++)
            {
                var star = Props.Glow(root, 0, 0, tints[i % 3], 0.55f, 0.55f, order);
                star.transform.localPosition = Vector3.zero;
                var tint = tints[i % 3];
                float ox = 0, oy = 0, rise = 0;
                Anim.Float(0, 1, 0.7f, t =>
                {
                    if (star == null) return;
                    if (t < 0.02f)
                    {
                        ox = Random.Range(-40f, 40f);
                        oy = Random.Range(-90f, 0f);
                        rise = Random.Range(10f, 30f);
                    }
                    star.transform.localPosition = new Vector3(ox, -(oy - rise * t), 0) / Design.PixelsPerUnit;
                    star.color = new Color(tint.r, tint.g, tint.b, 1 - t);
                    var s = 0.55f * (1 - t) / 2;
                    star.transform.localScale = new Vector3(s, s, 1);
                }, Ease.Linear, i * 0.11f, -1);
            }
            return root;
        }

        /// <summary>Keep an Elite's (or Lucky Impling's) aura, tag and twinkles with it.</summary>
        void PlaceEliteMarks(Mob m)
        {
            if (m.Aura != null)
            {
                m.Aura.transform.position = Design.World(m.X, m.Y - m.Height * m.Scale * 0.45f, 1);
                m.Aura.sortingOrder = Design.Depth(m.Y) - 1;
            }
            if (m.Tag != null)
            {
                m.Tag.style.left = m.X;
                m.Tag.style.top = m.Label.style.top.value.value - LabelHeight;
            }
            if (m.Sparkle != null) m.Sparkle.position = Design.World(m.X, m.Y);
        }

        // --- Exits ---

        Shapes Arrow(float x, float y, Vector2[] pts)
        {
            var g = Shapes.Create(World, x, y, Overlay);
            g.Poly(Palette.Gold, pts);
            g.Stroke(Design.Hex(0x3e2723), 4, true, pts);
            return g;
        }

        void DrawExits()
        {
            var y = (Top + Bottom) / 2 - 70;
            foreach (var exit in room.Exits)
            {
                if (exit.Side == "back")
                {
                    DrawBackExit(exit);
                    continue;
                }
                if (exit.Side == "down")
                {
                    DrawDownExit(exit);
                    continue;
                }
                var dir = exit.Side == "right" ? 1 : -1;
                var x = exit.Side == "right" ? Design.Width - 46 : 46;
                if (!ExitOpen(exit))
                {
                    DrawBlocker(exit.Side, exit.Blocker ?? "thorns");
                    continue;
                }
                var arrow = Arrow(x, y, new[] { new Vector2(-16 * dir, -26), new Vector2(22 * dir, 0), new Vector2(-16 * dir, 26) });
                Ui.Text(labels, x - 10 * dir, y - 48, exit.Label, 16, Palette.Text, true, exit.Side == "right" ? 1 : 0, 0.5f, stroke: true,
                    align: exit.Side == "right" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                Anim.Float(0, 8 * dir, 0.5f, v => { if (arrow != null) arrow.SetPosition(x + v, y); }, Ease.SineInOut, repeat: -1, yoyo: true);
                var e = exit;
                AddHit(x, y, 90, 90, () =>
                {
                    if (ModalOpen || leaving) return;
                    pendingNpc = null;
                    moveTarget = new Vector2(e.Side == "right" ? Design.Width + 40 : -40, player.Y);
                }, Overlay);
            }
        }

        /// <summary>A bobbing up-arrow and label over a doorway in the art; tap it to walk in. A closed one gets its blocker drawn instead.</summary>
        void DrawBackExit(RoomExit exit)
        {
            var x = (float)exit.X;
            // A sealed way you can't open yet is just clutter: show it once its quest can be picked up.
            if (exit.RequiresQuest != null && Quests.Status(P, Db.Quest(exit.RequiresQuest)) == "locked") return;
            if (exit.Frame != null) DrawDoorway(x, exit.Frame);
            if (!ExitOpen(exit))
            {
                DrawBackBlocker(x, exit.Blocker ?? "barricade");
                return;
            }
            var y = Top - 70;
            var arrow = Arrow(x, y, new[] { new Vector2(-26, 14), new Vector2(0, -20), new Vector2(26, 14) });
            Ui.Text(labels, x, y - 46, exit.Label, 16, Palette.Text, true, 0.5f, 0.5f, stroke: true, align: TextAnchor.MiddleCenter);
            Anim.Float(0, -8, 0.5f, v => { if (arrow != null) arrow.SetPosition(x, y + v); }, Ease.SineInOut, repeat: -1, yoyo: true);
            AddHit(x, y - 10, 120, 100, () =>
            {
                if (ModalOpen || leaving) return;
                pendingNpc = null;
                pendingExit = exit;
                moveTarget = new Vector2(x, Top);
            }, Overlay);
        }

        /// <summary>A bobbing down-arrow at the bottom edge where a path leads off (side rooms); tap it to walk there.</summary>
        void DrawDownExit(RoomExit exit)
        {
            if (!ExitOpen(exit)) return;
            var x = (float)exit.X;
            var y = Bottom + 2;
            // A worn path running off the bottom of the screen.
            var path = Shapes.Create(World, 0, 0, 1);
            path.Ellipse(Design.Hex(0x3b2a1a, 0.45f), x, Bottom + 14, 170, 40);
            path.Ellipse(Design.Hex(0x3b2a1a, 0.3f), x, Bottom - 14, 120, 26);
            var arrow = Arrow(x, y, new[] { new Vector2(-22, -12), new Vector2(0, 14), new Vector2(22, -12) });
            Ui.Text(labels, x + 34, y - 2, exit.Label, 16, Palette.Text, true, 0, 0.5f, stroke: true);
            Anim.Float(0, 6, 0.5f, v => { if (arrow != null) arrow.SetPosition(x, y + v); }, Ease.SineInOut, repeat: -1, yoyo: true);
            AddHit(x, y - 10, 140, 70, () =>
            {
                if (ModalOpen || leaving) return;
                pendingNpc = null;
                pendingThing = null;
                pendingExit = exit;
                moveTarget = new Vector2(x, Bottom);
            }, Overlay);
        }

        /// <summary>The down exit whose path the player is standing on (at the bottom of the walkable strip).</summary>
        RoomExit DownExitAt(Actor p) =>
            room.Exits.FirstOrDefault(e => e.Side == "down" && ExitOpen(e) && Math.Abs(p.X - (float)e.X) < 70 && p.Y >= Bottom - 8);

        /// <summary>A gate or cave mouth drawn at the top of the walkable strip, behind everyone.</summary>
        void DrawDoorway(float x, string frame)
        {
            var top = Top;
            var g = Shapes.Create(World, 0, 0, Design.Depth(top) - 8);
            if (frame == "cave")
            {
                g.Ellipse(Design.Hex(0x4a4146), x, top - 70, 220, 190);
                g.Ellipse(Design.Hex(0x151012), x, top - 55, 170, 150);
                g.Ellipse(Design.Hex(0xff8a3d, 0.25f), x, top - 20, 120, 50);
                g.Rect(Design.Hex(0x4a4146), x - 120, top + 6, 240, 14);
                return;
            }
            // Stone posts, a lintel and open wooden doors on a dark passage.
            g.Rect(Design.Hex(0x1d1a18), x - 60, top - 170, 120, 176);
            g.Rect(Design.Hex(0x8d8478), x - 86, top - 190, 28, 196).Rect(Design.Hex(0x8d8478), x + 58, top - 190, 28, 196);
            g.Rect(Design.Hex(0x6f675c), x - 96, top - 212, 192, 30);
            g.Rect(Design.Hex(0x6d4c33), x - 58, top - 166, 22, 170).Rect(Design.Hex(0x6d4c33), x + 36, top - 166, 22, 170);
            g.StrokeRect(Design.Hex(0x3e2723), 3, x - 96, top - 212, 192, 30);
        }

        /// <summary>The back exit whose doorway the player is standing in (it sits at the top of the walkable strip).</summary>
        RoomExit BackExitAt(Actor p) =>
            room.Exits.FirstOrDefault(e => e.Side == "back" && Math.Abs(p.X - (float)e.X) < 50 && p.Y <= Top + 8 &&
                !(e.RequiresQuest != null && Quests.Status(P, Db.Quest(e.RequiresQuest)) == "locked"));

        bool ExitOpen(RoomExit exit) => Travel.ExitOpen(P, room, exit);

        /// <summary>Thorns, a barricade or flooded reeds across a locked exit.</summary>
        void DrawBlocker(string side, string kind)
        {
            float top = Top, bottom = Bottom;
            var x0 = side == "right" ? Design.Width - 90 : 0;
            var g = Shapes.Create(World, 0, 0, Design.Depth(bottom) + 4);
            if (kind == "thorns")
            {
                for (int i = 0; i < 70; i++)
                {
                    var x = x0 + i * 37 % 90;
                    var y = top - 260 + i * 53 % (bottom - top + 280);
                    g.Line(Design.Hex(i % 3 != 0 ? 0x2b1d14 : 0x1f3a1c), 6, x, y, x + i * 29 % 50 - 25, y + 40);
                    g.Tri(Design.Hex(0x4a3426), x - 4, y + 10, x + 4, y + 10, x + (i % 2 == 1 ? 12 : -12), y + 2);
                }
            }
            else if (kind == "barricade")
            {
                // Posts, then planks nailed across them.
                for (int i = 0; i < 3; i++)
                {
                    g.Rect(Design.Hex(0x4e342e), x0 + 10 + i * 30, top - 230, 18, bottom - top + 250);
                    g.Tri(Design.Hex(0x3e2723), x0 + 10 + i * 30, top - 230, x0 + 19 + i * 30, top - 250, x0 + 28 + i * 30, top - 230);
                }
                for (int i = 0; i < 7; i++)
                {
                    var y = top - 200 + i * ((bottom - top + 200) / 7);
                    var tilt = i % 2 == 1 ? 14 : -14;
                    g.Poly(Design.Hex(i % 2 == 1 ? 0x8d6e63 : 0x795548),
                        new Vector2(x0 - 4, y + tilt), new Vector2(x0 + 94, y - tilt), new Vector2(x0 + 94, y - tilt + 22), new Vector2(x0 - 4, y + tilt + 22));
                    g.StrokeRect(Design.Hex(0x3e2723), 2, x0 + 2, y - 6, 86, 2);
                }
            }
            else if (kind == "snowdrift") DrawSnowdrift(g, x0 + 45, bottom + 10, 110, bottom - top + 200);
            else
            {
                // Deep water with tall reeds and cattails.
                g.Rect(Design.Hex(0x24485a, 0.85f), x0, top - 20, 90, bottom - top + 40);
                for (int i = 0; i < 40; i++)
                {
                    var x = x0 + i * 41 % 90;
                    var b = bottom + 10 - i * 23 % 60;
                    var h = 160 + i * 67 % 220;
                    g.Line(Design.Hex(i % 3 != 0 ? 0x5d7a3a : 0x47602b), 4, x, b, x + i * 13 % 20 - 10, b - h);
                    if (i % 3 == 0) g.Ellipse(Design.Hex(0x5d3a1e), x + i * 13 % 20 - 10, b - h + 16, 10, 30);
                }
            }
            Ui.Text(labels, side == "right" ? Design.Width - 50 : 50, top - 290, BlockerLabels[kind], 15, Palette.TextDim, true, 0.5f, 0.5f,
                stroke: true, align: TextAnchor.MiddleCenter);
        }

        /// <summary>Snow piled into a mound: `cx` center, `b` bottom y, `w` x `h`.</summary>
        static void DrawSnowdrift(Shapes g, float cx, float b, float w, float h)
        {
            // Overlapping humps on a flat base, shaded darker at the bottom.
            float[][] humps = { new[] { -0.32f, 0.55f, 0.5f }, new[] { 0.28f, 0.6f, 0.55f }, new[] { 0f, 1f, 0.6f } };
            foreach (var p in humps) g.Ellipse(Design.Hex(0xc9d7e4), cx + p[0] * w, b - h * p[1] / 2, w * p[2], h * p[1]);
            foreach (var p in humps) g.Ellipse(Design.Hex(0xf4f8fc), cx + p[0] * w - w * 0.04f, b - h * p[1] / 2 - h * 0.06f, w * p[2] * 0.85f, h * p[1] * 0.8f);
            g.Ellipse(Color.white, cx - w * 0.06f, b - h * 0.78f, w * 0.22f, h * 0.14f);
            g.Rect(Design.Hex(0xb3c4d6), cx - w * 0.6f, b - 8, w * 1.2f, 10);
        }

        /// <summary>What blocks a closed back exit, drawn over its doorway.</summary>
        void DrawBackBlocker(float x, string kind)
        {
            var top = Top;
            var g = Shapes.Create(World, 0, 0, Design.Depth(top) - 4);
            if (kind == "snowdrift") DrawSnowdrift(g, x, top + 14, 200, 150);
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    g.Rect(Design.Hex(i % 2 == 1 ? 0x8d6e63 : 0x795548), x - 70, top - 130 + i * 34, 140, 22);
                    g.StrokeRect(Design.Hex(0x3e2723), 2, x - 70, top - 130 + i * 34, 140, 22);
                }
            }
            Ui.Text(labels, x, top - 190, BlockerLabels[kind].Replace("\n", " "), 15, Palette.TextDim, true, 0.5f, 0.5f, stroke: true);
        }

        void BumpWall(RoomExit exit)
        {
            if (Now < wallBumpAt) return;
            wallBumpAt = Now + 2;
            Sfx.Play("denied");
            GameRoot.I.Shake(0.12f, 5);
            var guard = exit.Guarded && boss != null ? $"The {StripThe(boss.Def.Name)} blocks the way. Beat it to get through." : null;
            Ui.Toast(guard ?? BlockerBump[exit.Blocker ?? (exit.Side == "back" ? "barricade" : "thorns")], Palette.TextDim);
        }

        static string StripThe(string name) => name.StartsWith("The ") ? name.Substring(4) : name;

        void SpawnBoss()
        {
            var b = room.Boss;
            var monsterId = Travel.RoomBoss(P, room);
            if (b == null || monsterId == null) return;
            var def = Db.Monster(monsterId);
            float x = (float)b.X, y = (float)b.Y;
            // Bosses face the middle of the room, where the player comes from.
            var faceLeft = x > Design.Width / 2;
            var fig = Figure.Create(World, x, y, def.Look, faceLeft ? -1 : 1);
            boss = MakeActor<BossActor>(fig, x, y, (float)def.Look.Scale);
            boss.Facing = faceLeft ? -1 : 1;
            boss.Def = def;
            boss.Label = WorldLabel($"{def.Name}  ·  Boss", 18, Palette.TextBad);
            Place(boss);
            var aura = Props.Glow(World, x, y - 120, Design.Hex(0x7dff9a, 0.25f), 9, 9, Design.Depth(y) - 1);
            Pulse(aura, Design.Hex(0x7dff9a), 0.25f, 0.45f, 9, 10, 1.4f);
            var h = boss.Height * boss.Scale;
            AddHit(x, y - h * 0.5f, 260, h, () =>
            {
                if (ModalOpen || leaving) return;
                if (NearestTarget() is BossActor) ConfirmBoss();
                else moveTarget = new Vector2(x + (faceLeft ? -170 : 170), y);
            }, Design.Depth(y) + 1);
        }

        void BuildUi(bool titleCard)
        {
            hud = new Hud(ui, 16, 16);
            hud.Refresh(P, false);

            var right = Design.Width - 16;
            UiButton Btn(float y, string label, Action run) => new UiButton(ui, right - 70, y, label, run, 140, 48, 19);
            charBtn = Btn(40, "Character", OpenCharacter);
            invBtn = Btn(98, "Inventory", ToggleInventory);
            Btn(156, "Map", ToggleMap);
            Btn(214, "Menu", OpenMenu);

            var title = Ui.Text(ui, Design.Width / 2, 36, room.Name, 32, MapLabel, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(title);
            title.style.unityTextOutlineColor = Felt.Ink;
            title.pickingMode = PickingMode.Position;
            // Hidden testing menu on touch screens: tap the room name 5 times.
            title.RegisterCallback<PointerUpEvent>(_ =>
            {
                var now = Now;
                titleTaps.RemoveAll(t => now - t >= 2.5f);
                titleTaps.Add(now);
                if (titleTaps.Count >= 5)
                {
                    titleTaps.Clear();
                    OpenCheats();
                }
            });

            if (titleCard) TitleCard();

            if (!showedControls)
            {
                showedControls = true;
                var hint = Ui.Text(ui, Design.Width / 2, Design.Height - 18, "WASD / arrows: move    E / Space: talk    C: character    M: map    Click: walk there",
                    17, Palette.Text, ax: 0.5f, ay: 1, stroke: true);
                Anim.Float(1, 0, 1, a => hint.style.opacity = a, Ease.Linear, 9).OnComplete(hint.RemoveFromHierarchy);
            }

            tracker = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(tracker, 16, 176);
            ui.Add(tracker);

            // "E Talk" bubble shown over the nearest NPC.
            prompt = Ui.Rect(ui, 0, 0, 110, 34, Color.clear);
            Felt.CardBehind(prompt, 41);
            prompt.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            promptText = Ui.Text(prompt, 55, 17, "", 17, Palette.TextGold, true, 0.5f, 0.5f);
            prompt.style.display = DisplayStyle.None;
            Anim.Float(1, 1.06f, 0.45f, s => prompt.style.scale = new Scale(new Vector3(s, s, 1)), Ease.SineInOut, repeat: -1, yoyo: true);
        }

        /// <summary>Big room name that fades in and out on arrival.</summary>
        void TitleCard()
        {
            var card = Ui.Text(ui, Design.Width / 2, 250, room.Name, 64, MapLabel, false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(card);
            card.style.unityTextOutlineColor = Felt.Ink;
            card.style.opacity = 0;
            card.style.scale = new Scale(Vector3.one * 0.8f);
            Anim.Float(0, 1, 0.35f, t =>
            {
                card.style.opacity = Mathf.Clamp01(t);
                var s = Mathf.LerpUnclamped(0.8f, 1, t);
                card.style.scale = new Scale(new Vector3(s, s, 1));
            }, Ease.BackOut).OnComplete(() =>
                Anim.Float(0, 1, 0.6f, t =>
                {
                    card.style.opacity = 1 - t;
                    card.style.top = 250 - 30 * t;
                }, Ease.Linear, 0.9f).OnComplete(card.RemoveFromHierarchy));
        }

        // --- Frame loop ---

        public override void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            HandleKeys();
            HandlePointer();
            if (leaving) return;
            var blocked = ModalOpen;
            MovePlayer(dt, blocked);
            if (leaving) return;
            foreach (var npc in npcs) UpdateNpc(npc);
            if (!blocked) SeparateMobs(dt);
            foreach (var mob in mobs.ToList())
            {
                UpdateMob(mob, dt, blocked);
                if (leaving) return;
            }
            if (!blocked && !leaving) CheckChest();
            if (!blocked && !leaving && Now > newsAt)
            {
                newsAt = Now + 0.5f;
                ShowNews();
            }
            UpdatePrompt(blocked);
        }

        void HandleKeys()
        {
            if (!InputEnabled) return;
            if (Keys.Pressed(Key.Escape)) CloseTopModal();
            if (Keys.Pressed(Key.C)) OpenCharacter();
            if (Keys.Pressed(Key.I)) ToggleInventory();
            if (Keys.Pressed(Key.M)) ToggleMap();
            if (Keys.Pressed(Key.Backquote)) OpenCheats();
            if (Keys.Pressed(Key.E) || Keys.Pressed(Key.Space)) Talk();
        }

        void Talk()
        {
            if (cutscene != null)
            {
                cutscene.Next();
                return;
            }
            if (ModalOpen || leaving) return;
            var target = NearestTarget();
            if (target is BossActor) ConfirmBoss();
            else if (target is Npc npc) Interact(npc);
            else
            {
                var thing = NearestThing();
                if (thing != null) UseThing(thing);
            }
        }

        Hit AddHit(float cx, float cy, float w, float h, Action click, int order, Action<bool> hover = null)
        {
            var hit = new Hit { Area = new Rect(cx - w / 2, cy - h / 2, w, h), Click = click, Order = order, Hover = hover };
            hits.Add(hit);
            return hit;
        }

        void RemoveHit(Hit hit)
        {
            if (hit == null) return;
            hit.Removed = true;
            hits.Remove(hit);
        }

        Hit HitAt(Vector2 p)
        {
            Hit best = null;
            foreach (var h in hits)
                if (h.Area.Contains(p) && (best == null || h.Order > best.Order)) best = h;
            return best;
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null || !InputEnabled) return;
            var at = GameRoot.I.PointerDesign();
            var overUi = GameRoot.I.PointerOverUi();
            // Hover: names light up gold.
            var hover = at.HasValue && !overUi && !ModalOpen ? HitAt(at.Value) : null;
            foreach (var h in hits)
            {
                var on = h == hover;
                if (on == h.Hovered) continue;
                h.Hovered = on;
                h.Hover?.Invoke(on);
            }
            if (pointer.press.wasPressedThisFrame)
            {
                if (cutscene != null)
                {
                    cutscene.Next();
                    return;
                }
                if (!at.HasValue || overUi || ModalOpen || leaving) return;
                pressed = HitAt(at.Value);
                if (pressed == null) GroundClick(at.Value);
            }
            if (pointer.press.wasReleasedThisFrame && pressed != null)
            {
                var h = pressed;
                pressed = null;
                if (!h.Removed && at.HasValue && h.Area.Contains(at.Value)) h.Click();
            }
        }

        void GroundClick(Vector2 at)
        {
            pendingNpc = null;
            pendingThing = null;
            var target = new Vector2(at.x, Mathf.Clamp(at.y, Top, Bottom));
            moveTarget = target;
            // Little marker where you clicked.
            var ring = Shapes.Create(World, target.x, target.y, 2, "ring");
            ring.StrokeEllipse(Palette.Gold, 3, 0, 0, 40, 14);
            Anim.Float(0, 1, 0.45f, t =>
            {
                if (ring == null) return;
                var s = 1 + 0.6f * t;
                ring.transform.localScale = new Vector3(s, s, 1);
                ring.Alpha = 1 - t;
            }, Ease.CubicOut).OnComplete(() => { if (ring != null) UnityEngine.Object.Destroy(ring.gameObject); });
        }

        void MovePlayer(float dt, bool blocked)
        {
            var p = player;
            float vx = 0, vy = 0;
            if (!blocked)
            {
                vx = (Keys.Held(Key.D) || Keys.Held(Key.RightArrow) ? 1 : 0) - (Keys.Held(Key.A) || Keys.Held(Key.LeftArrow) ? 1 : 0);
                vy = (Keys.Held(Key.S) || Keys.Held(Key.DownArrow) ? 1 : 0) - (Keys.Held(Key.W) || Keys.Held(Key.UpArrow) ? 1 : 0);
                if (vx != 0 || vy != 0)
                {
                    moveTarget = null;
                    pendingNpc = null;
                    pendingThing = null;
                }
                else if (moveTarget.HasValue)
                {
                    var dx = moveTarget.Value.x - p.X;
                    var dy = moveTarget.Value.y - p.Y;
                    if (Math.Abs(dx) < 6 && Math.Abs(dy) < 6)
                    {
                        moveTarget = null;
                        if (pendingNpc != null && InTalkRange(pendingNpc)) Interact(pendingNpc);
                        if (pendingThing != null && ThingInRange(pendingThing)) UseThing(pendingThing);
                        pendingNpc = null;
                        pendingThing = null;
                    }
                    else
                    {
                        var l = Mathf.Sqrt(dx * dx + dy * dy);
                        vx = dx / l;
                        vy = dy / l;
                    }
                }
            }

            var len = Mathf.Sqrt(vx * vx + vy * vy);
            p.Moving = len > 0;
            if (p.Moving)
            {
                var speed = (float)Db.B.WalkSpeed;
                p.X += vx / len * speed * dt;
                p.Y += vy / len * speed * 0.7f * dt;
                if (Math.Abs(vx) > 0.1f) p.Facing = vx > 0 ? 1 : -1;
                p.WalkT += dt * 13;
                dustTimer -= dt;
                if (dustTimer <= 0)
                {
                    dustTimer = 0.22f;
                    Fx.Dust(World, p.X - p.Facing * 20, p.Y, 2);
                    Sfx.Play("step");
                }
            }

            // Exits.
            var exitL = room.Exits.FirstOrDefault(e => e.Side == "left" && ExitOpen(e));
            var exitR = room.Exits.FirstOrDefault(e => e.Side == "right" && ExitOpen(e));
            if (exitR != null && p.X > Design.Width - Edge / 2)
            {
                Leave(exitR);
                return;
            }
            if (exitL != null && p.X < Edge / 2)
            {
                Leave(exitL);
                return;
            }
            var shutR = exitR != null ? null : room.Exits.FirstOrDefault(e => e.Side == "right");
            var shutL = exitL != null ? null : room.Exits.FirstOrDefault(e => e.Side == "left");
            var maxX = exitR != null ? Design.Width : shutR != null ? Design.Width - 110 : Design.Width - Edge;
            var minX = exitL != null ? 0 : shutL != null ? 110 : Edge;
            if (shutR != null && p.X >= maxX && vx > 0) BumpWall(shutR);
            if (shutL != null && p.X <= minX && vx < 0) BumpWall(shutL);
            // Back exits: walk up into the doorway (or arrive there after tapping it).
            var back = BackExitAt(p);
            if (back != null && (vy < 0 || pendingExit == back))
            {
                pendingExit = null;
                if (ExitOpen(back))
                {
                    Leave(back);
                    return;
                }
                BumpWall(back);
            }
            // Down exits: walk off the bottom edge along the path (or arrive there after tapping it).
            var down = DownExitAt(p);
            if (down != null && (vy > 0 || pendingExit == down))
            {
                pendingExit = null;
                Leave(down);
                return;
            }
            ClampToWalk(p, minX, maxX);
            Place(p);
            if (boss != null)
            {
                Place(boss);
                var dx = Math.Abs(boss.X - p.X);
                if (dx > 280) bossAsked = false;
                else if (!blocked && !bossAsked && dx < 130 && Math.Abs(boss.Y - p.Y) < 70)
                {
                    bossAsked = true;
                    moveTarget = null;
                    ConfirmBoss();
                }
            }
        }

        void UpdateNpc(Npc npc)
        {
            // Turn toward the player when they're close.
            if (Math.Abs(npc.X - player.X) < 260) npc.Facing = player.X < npc.X ? -1 : 1;
            Place(npc);
            if (npc.Marker != null)
            {
                npc.Marker.style.left = npc.X;
                npc.Marker.style.top = npc.Label.style.top.value.value - LabelHeight - 8 + Mathf.Sin(Now * 5) * 5;
            }
        }

        void UpdateMob(Mob m, float dt, bool blocked)
        {
            var p = player;
            var dx = p.X - m.X;
            var dy = p.Y - m.Y;
            var dist = Mathf.Sqrt(dx * dx + dy * 1.6f * dy * 1.6f);
            var safe = Now < safeUntil;
            float speed = 0;
            Vector2? target = null;
            var aggro = (float)Db.B.MonsterAggroRange;

            if (!blocked && m.Def.Lucky)
            {
                // A Lucky Impling runs from you instead, and slips along the walls when cornered.
                if (dist < aggro * 1.15f)
                {
                    if (m.State != "run") AlertPop(m);
                    m.State = "run";
                    target = RunTarget(m);
                    speed = (float)Db.B.Lucky.RunSpeed;
                }
                else if (m.State == "run")
                {
                    m.State = "idle";
                    m.Timer = 0.6f;
                }
            }
            if (!blocked && m.State != "run")
            {
                if (!safe && m.State != "chase" && m.State != "alert" && dist < aggro)
                {
                    m.State = "alert";
                    m.Timer = 0.45f;
                    AlertPop(m);
                }
                switch (m.State)
                {
                    case "alert":
                        m.Facing = dx < 0 ? -1 : 1;
                        m.Timer -= dt;
                        if (m.Timer <= 0) m.State = "chase";
                        break;
                    case "chase":
                        if (safe || dist > aggro * 1.6f)
                        {
                            m.State = "idle";
                            m.Timer = 1;
                        }
                        else
                        {
                            target = new Vector2(p.X, p.Y);
                            speed = (float)Db.B.MonsterChaseSpeed;
                        }
                        break;
                    case "idle":
                        m.Timer -= dt;
                        if (m.Timer <= 0)
                        {
                            // Wander somewhere no other monster is standing.
                            for (int i = 0; i < 6; i++)
                            {
                                var t = new Vector2(Mathf.Clamp(m.X + Random.Range(-220, 221), 120, Design.Width - 120), Random.Range((int)Top + 5, (int)Bottom - 5 + 1));
                                m.Target = t;
                                if (mobs.All(o => o == m || Math.Abs(o.X - t.x) >= MobGap.x || Math.Abs(o.Y - t.y) >= MobGap.y)) break;
                            }
                            m.State = "wander";
                        }
                        break;
                    case "wander":
                        target = m.Target;
                        speed = (float)Db.B.MonsterWanderSpeed;
                        if (!target.HasValue || (Math.Abs(target.Value.x - m.X) < 5 && Math.Abs(target.Value.y - m.Y) < 5))
                        {
                            m.State = "idle";
                            m.Timer = Random.Range(1f, 2.5f);
                            target = null;
                        }
                        break;
                }
            }

            m.Moving = false;
            if (target.HasValue && speed > 0)
            {
                var tx = target.Value.x - m.X;
                var ty = target.Value.y - m.Y;
                var len = Mathf.Sqrt(tx * tx + ty * ty);
                if (len > 1)
                {
                    var step = Math.Min(len, speed * dt);
                    m.X += tx / len * step;
                    m.Y += ty / len * step * 0.8f;
                    m.Facing = tx < 0 ? -1 : 1;
                    m.Moving = true;
                    m.WalkT += dt * (m.State == "chase" || m.State == "run" ? 14 : 8);
                }
            }
            ClampToWalk(m, 60, Design.Width - 60);
            Place(m);
            PlaceEliteMarks(m);

            if (!blocked && !safe && Math.Abs(dx) < TouchRange.x && Math.Abs(dy) < TouchRange.y) StartBattle(m);
        }

        /// <summary>Where a running Lucky Impling heads: away from you; along the wall once it's backed into one.</summary>
        Vector2 RunTarget(Mob m)
        {
            var p = player;
            var away = m.X == p.X ? (m.Uid % 2 != 0 ? 1 : -1) : Math.Sign(m.X - p.X);
            var x = m.X + away * 200;
            var y = m.Y + (m.Y - p.Y >= 0 ? 60 : -60);
            const float margin = 90;
            if (x < margin || x > Design.Width - margin)
            {
                // Cornered: dash past you on the roomier side.
                x = Mathf.Clamp(x, margin, Design.Width - margin);
                y = m.Y - Top > Bottom - m.Y ? Top : Bottom;
                if (Math.Abs(m.X - p.X) < 140) x = p.X - away * 220;
            }
            return new Vector2(x, Mathf.Clamp(y, Top, Bottom));
        }

        void ShowPrompt(bool on) => prompt.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        void PromptAt(string verb, float x, float y)
        {
            promptText.text = "E  " + verb;
            prompt.style.left = x;
            prompt.style.top = y;
            ShowPrompt(true);
        }

        void UpdatePrompt(bool blocked)
        {
            var near = blocked ? null : NearestTarget();
            if (near == null)
            {
                var thing = blocked ? null : NearestThing();
                if (thing == null)
                {
                    ShowPrompt(false);
                    return;
                }
                PromptAt(thing.Kind == "campfire" ? "Rest" : "Look", thing.X, thing.Y - (thing.Kind == "campfire" ? 130 : 80));
                return;
            }
            if (near is BossActor)
            {
                PromptAt("Fight", boss.X, boss.Label.style.top.value.value - LabelHeight - 22);
                return;
            }
            var npc = (Npc)near;
            var q = Quests.ForNpc(P, npc.Def.Id);
            var kind = npc.Def.Role.Kind;
            var verb = q?.status == "ready" || (kind == "guild" && Guild.AnyHuntReady(P)) ? "Turn in"
                : q?.status == "available" ? "Quest"
                : kind == "shop" ? "Shop"
                : kind == "forge" ? "Forge"
                : kind == "inn" ? "Rest"
                : kind == "guild" ? "Hunts"
                : "Talk";
            var marker = npc.Marker;
            var above = marker != null && marker.style.display != DisplayStyle.None
                ? marker.style.top.value.value - 40 - 46
                : npc.Label.style.top.value.value - LabelHeight - 22;
            PromptAt(verb, npc.X, above);
        }

        /// <summary>Closest thing the player can interact with: an NPC, or the boss.</summary>
        Actor NearestTarget()
        {
            if (boss != null && Math.Abs(boss.X - player.X) < 280 && Math.Abs(boss.Y - player.Y) < 110) return boss;
            return NearestNpc();
        }

        // --- Helpers ---

        /// <summary>Position, depth, scale and walk bob of an actor. Lower on screen = closer = bigger and in front.</summary>
        void Place(Actor a)
        {
            var t = Mathf.Clamp01((a.Y - Top) / (Bottom - Top));
            var s = a.Scale * (0.9f + 0.1f * t);
            var bob = a.Moving ? -Mathf.Abs(Mathf.Sin(a.WalkT)) * 8 : 0;
            var breathe = a.Moving ? 1 : 1 + Mathf.Sin(Now * 3 + a.X * 0.01f) * 0.015f;
            if (a.Fig == null) return;
            a.Fig.transform.position = Design.World(a.X, a.Y + bob + a.Hop);
            a.Fig.Order = Design.Depth(a.Y);
            a.Fig.Facing = a.Facing;
            a.Fig.transform.localScale = new Vector3(s * a.Facing, s * breathe, 1);
            a.Fig.transform.localRotation = Quaternion.Euler(0, 0, a.Moving ? -Mathf.Sin(a.WalkT) * 0.05f * Mathf.Rad2Deg : 0);
            if (a.Label != null)
            {
                a.Label.style.left = a.X;
                a.Label.style.top = a.Y - a.Height * s - 8;
            }
        }

        void ClampToWalk(Actor a, float minX = Edge, float maxX = Design.Width - Edge)
        {
            a.Y = Mathf.Clamp(a.Y, Top, Bottom);
            a.X = Mathf.Clamp(a.X, minX, maxX);
        }

        bool InTalkRange(Npc npc) => Math.Abs(npc.X - player.X) < TalkRange.x && Math.Abs(npc.Y - player.Y) < TalkRange.y;

        Npc NearestNpc()
        {
            Npc best = null;
            var bestD = float.PositiveInfinity;
            foreach (var npc in npcs)
            {
                if (!InTalkRange(npc)) continue;
                var d = Math.Abs(npc.X - player.X);
                if (d < bestD)
                {
                    best = npc;
                    bestD = d;
                }
            }
            return best;
        }

        bool ModalOpen => cutscene != null || UiModal.OpenCount > 0;

        void CloseTopModal()
        {
            if (cutscene != null)
            {
                cutscene.Skip();
                return;
            }
            UiModal.Top?.Escape();
        }

        void AlertPop(Mob m)
        {
            Sfx.Play(m.Def.Lucky ? "coin" : "alert");
            var y0 = m.Y - m.Height * m.Scale - 30;
            var mark = Ui.Text(labels, m.X, y0, "!", 44, m.Def.Lucky ? LuckyText : Palette.TextBad, true, 0.5f, 0.5f, stroke: true);
            mark.style.scale = new Scale(Vector3.zero);
            Anim.Float(0, 1.3f, 0.14f, s => mark.style.scale = new Scale(new Vector3(s, s, 1)), Ease.BackOut)
                .OnComplete(() => Anim.Float(1.3f, 1, 0.1f, s => mark.style.scale = new Scale(new Vector3(s, s, 1)))
                    .OnComplete(() => Anim.Float(0, 1, 0.3f, t =>
                    {
                        mark.style.opacity = 1 - t;
                        mark.style.top = y0 - 20 * t;
                    }, Ease.Linear, 0.3f).OnComplete(mark.RemoveFromHierarchy)));
            Anim.Float(0, -18, 0.12f, v => m.Hop = v, Ease.CubicOut, yoyo: true).OnComplete(() => m.Hop = 0);
        }

        void BlinkPlayer()
        {
            var fig = player.Fig;
            Anim.Float(1, 0.35f, 0.12f, a => { if (player.Fig != null) player.Fig.SetAlpha(a); }, Ease.Linear,
                repeat: Mathf.RoundToInt((float)Db.B.FleeGraceSeconds * 1000 / 240), yoyo: true).OnComplete(() => { if (player.Fig != null) player.Fig.SetAlpha(1); });
        }

        // --- Transitions ---

        void Leave(RoomExit exit)
        {
            if (leaving) return;
            leaving = true;
            ShowPrompt(false);
            // Arrive at the way back: the destination's exit to this room, else the opposite edge.
            var back = Db.Room(exit.To).Exits.FirstOrDefault(e => e.To == room.Id);
            var side = back?.Side ?? (exit.Side == "right" ? "left" : "right");
            var next = RoomArrival.Exit(exit.To, side, player.Y, back?.X is double bx ? (float)bx : (float?)null);
            RememberRoom();
            GameRoot.I.Go(new RoomScreen(next));
        }

        void StartBattle(Mob m)
        {
            if (leaving) return;
            leaving = true;
            ShowPrompt(false);
            Session.SaveRoom(new RoomSnapshot { RoomId = room.Id, FightingUid = m.Uid, Monsters = Roamers() });
            RememberRoom();
            SaveLocation();

            Sfx.Play(m.Elite != null ? "warning" : m.Def.Lucky ? "coin" : "encounter");
            Fx.WhiteFlash(m.Fig, 140);
            Fx.HitSparks(World, (m.X + player.X) / 2, m.Y - 80, false, true);
            labels.style.display = DisplayStyle.None;
            GameRoot.I.Shake(0.25f, 15);
            GameRoot.I.Flash(0.18f, Color.white);
            GameRoot.I.View((m.X + player.X) / 2, m.Y - 120, 1.6f, 0.45f, Ease.CubicIn, 0.08f);
            var setup = new BattleSetup { ZoneId = room.ZoneId, MonsterId = m.Def.Id, Elite = m.Elite, RoomId = room.Id, X = player.X, Y = player.Y };
            Anim.After(0.38f, () => GameRoot.I.Go(new BattleScreen(setup), 0.2f));
        }

        // --- NPCs and menus ---

        void Refresh()
        {
            hud.Refresh(P);
            RedrawPlayerIfRearmed();
            if (tracker != null) RefreshQuests();
            Save(false);
        }

        void SaveLocation()
        {
            P.Data.Location = new Location { RoomId = room.Id, X = Math.Round(player.X), Y = Math.Round(player.Y) };
            Saves.Save(P);
        }

        void Save(bool announce)
        {
            P.Data.Location = new Location { RoomId = room.Id, X = Math.Round(player.X), Y = Math.Round(player.Y) };
            var ok = Saves.Save(P);
            if (announce) Ui.Toast(ok ? "Game saved" : "Save failed (storage blocked?)", ok ? Palette.TextGood : Palette.TextBad);
        }

        void SaveAndQuit()
        {
            Save(false);
            leaving = true;
            GameRoot.I.Go(new TitleScreen());
        }

        /// <summary>Save, sound, story text, fullscreen and back to the title, in one place so the screen stays clear.</summary>
        void OpenMenu()
        {
            if (ModalOpen || leaving) return;
            moveTarget = null;
            var modal = new UiModal(460, 470, "Menu", () => { });
            void Render()
            {
                modal.Clear();
                var c = modal.Content;
                var rows = new List<(string label, Action run)>
                {
                    ("Save", () => Save(true)),
                    (Saves.Muted ? "Sound: off" : "Sound: on", () =>
                    {
                        Saves.Muted = !Saves.Muted;
                        Saves.SaveSettings();
                        Render();
                    }),
                    (Saves.StoryText ? "Story text: on" : "Story text: off", () =>
                    {
                        Saves.StoryText = !Saves.StoryText;
                        Saves.SaveSettings();
                        Render();
                    }),
                    ("Fullscreen", () => UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen),
                    ("Save and quit to title", () =>
                    {
                        modal.Close();
                        SaveAndQuit();
                    }),
                };
                for (int i = 0; i < rows.Count; i++) new UiButton(c, 230, 100 + i * 66, rows[i].label, rows[i].run, 300, 52, 19);
                Ui.Text(c, 230, 440, "Story text off: quests say in one line what to do.", 14, Palette.TextDim, ax: 0.5f, ay: 0.5f, wrap: 400,
                    align: TextAnchor.MiddleCenter);
            }
            Render();
        }

        void OpenCharacter()
        {
            if (ModalOpen || leaving) return;
            CharacterPanel.OpenCharacterPanel(P, Refresh, ToggleInventory);
        }

        void OpenCheats()
        {
            if (ModalOpen || leaving) return;
            moveTarget = null;
            CheatMenu.OpenCheatMenu(P, new CheatHooks
            {
                OnChange = Refresh,
                Travel = (roomId, x, y) =>
                {
                    var r = Db.Room(roomId);
                    leaving = true;
                    Save(false);
                    GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(roomId, x ?? 640, y ?? (float)((r.Walk[0] + r.Walk[1]) / 2))));
                },
                ResetCharacter = () =>
                {
                    var old = P;
                    var fresh = Player.CreateNew(old.Name, old.Data.ClassId);
                    Session.Start(fresh, true);
                    Saves.Save(fresh);
                    leaving = true;
                    var start = Db.Data.Start;
                    GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(start.RoomId, (float)start.X, (float)start.Y)));
                },
            });
        }

        /// <summary>I opens the inventory, and closes it again.</summary>
        void ToggleInventory()
        {
            if (inventory != null && !inventory.Closed)
            {
                inventory.Escape();
                return;
            }
            if (ModalOpen || leaving) return;
            moveTarget = null;
            inventory = InventoryPanel.Open(P, Refresh, ReadRecall);
        }

        /// <summary>M or the Map button opens the map, and closes it again.</summary>
        void ToggleMap()
        {
            if (mapModal != null && !mapModal.Closed)
            {
                mapModal.Escape();
                return;
            }
            if (ModalOpen || leaving) return;
            moveTarget = null;
            mapModal = MapPanel.Open(P, room.Id, ReadRecall);
        }

        // --- Recall Scroll ---

        /// <summary>Asks first (it's one way), then reads a scroll. Called from the inventory and the map.</summary>
        void ReadRecall()
        {
            if (leaving) return;
            var (town, reason) = Travel.RecallCheck(P, room.Id);
            if (reason != null)
            {
                Sfx.Play("denied");
                Ui.Toast(reason, Palette.TextDim);
                return;
            }
            if (inventory != null && !inventory.Closed) inventory.Close();
            if (mapModal != null && !mapModal.Closed) mapModal.Close();
            var left = P.CountItem(Travel.RecallScroll);
            var modal = new UiModal(640, 280, "Recall Scroll");
            var text = $"Read a scroll and return to {town.Name}?\nOne way: you walk back out from there.\n({left} scroll{(left == 1 ? "" : "s")} left)";
            Ui.Text(modal.Content, 320, 120, text, 20, ax: 0.5f, ay: 0.5f, wrap: 580, align: TextAnchor.MiddleCenter);
            new UiButton(modal.Content, 200, 225, "Read it", () =>
            {
                modal.Close();
                RecallTo();
            }, 220);
            new UiButton(modal.Content, 440, 225, "Not now", () => modal.Close(), 220);
        }

        void RecallTo()
        {
            var dest = Travel.UseRecall(P, room.Id);
            if (dest == null || leaving) return;
            leaving = true;
            ShowPrompt(false);
            RememberRoom();
            Sfx.Play("recall");
            Fx.HealSparkles(World, player.X, player.Y, 0xffe9a8);
            var fig = player.Fig;
            var sy = fig.transform.localScale.y;
            Anim.Float(0, 1, 0.5f, t =>
            {
                if (fig == null) return;
                fig.SetAlpha(1 - t);
                var s = fig.transform.localScale;
                fig.transform.localScale = new Vector3(s.x, sy * (1 + 0.3f * t), 1);
            }, Ease.CubicIn);
            GameRoot.I.Flash(0.4f, new Color(1, 0.94f, 0.78f));
            // Save where you'll land, so a reload mid-fade doesn't undo the trip (the scroll is already spent).
            P.Data.Location = dest.Clone();
            Saves.Save(P);
            Anim.After(0.45f, () => GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(dest.RoomId, (float)dest.X, (float)dest.Y, recalled: true)), 0.3f));
        }

        // --- Campfires and things to look at ---

        void DrawThings()
        {
            var cf = room.Campfire;
            if (cf != null)
            {
                Props.Campfire(World, (float)cf.X, (float)cf.Y);
                var thing = new Thing { Kind = "campfire", X = (float)cf.X, Y = (float)cf.Y };
                things.Add(thing);
                ThingHit(thing, 150, 120);
            }
            foreach (var def in room.Inspect ?? new List<InspectDef>())
            {
                Props.Prop(World, def, !Session.Inspected.Contains(def.Id), out var star);
                var thing = new Thing { Kind = "inspect", Def = def, X = (float)def.X, Y = (float)def.Y, Star = star };
                things.Add(thing);
                Ui.Text(labels, thing.X, thing.Y - (def.Look == "sign" ? 110 : 44), def.Label, 15, Palette.TextDim, true, 0.5f, 1, stroke: true);
                ThingHit(thing, 130, 90);
            }
        }

        /// <summary>Tap a campfire or note: walk next to it, then use it.</summary>
        void ThingHit(Thing thing, float w, float h)
        {
            AddHit(thing.X, thing.Y - h / 2, w, h, () =>
            {
                if (ModalOpen || leaving) return;
                if (ThingInRange(thing))
                {
                    UseThing(thing);
                    return;
                }
                pendingNpc = null;
                pendingExit = null;
                pendingThing = thing;
                var side = player.X < thing.X ? -1 : 1;
                moveTarget = new Vector2(thing.X + side * 90, Mathf.Clamp(thing.Y, Top, Bottom));
            }, Design.Depth(thing.Y) + 1);
        }

        bool ThingInRange(Thing thing) => Math.Abs(thing.X - player.X) < TalkRange.x && Math.Abs(thing.Y - player.Y) < TalkRange.y;

        Thing NearestThing()
        {
            Thing best = null;
            foreach (var t in things)
                if (ThingInRange(t) && (best == null || Math.Abs(t.X - player.X) < Math.Abs(best.X - player.X))) best = t;
            return best;
        }

        void UseThing(Thing thing)
        {
            if (ModalOpen || leaving) return;
            moveTarget = null;
            player.Facing = thing.X < player.X ? -1 : 1;
            if (thing.Kind == "campfire")
            {
                RestAtFire();
                return;
            }
            Sfx.Play("page");
            if (!Session.Inspected.Contains(thing.Def.Id))
            {
                Session.Inspected.Add(thing.Def.Id);
                // The glint goes once it's been read.
                if (thing.Star != null) UnityEngine.Object.Destroy(thing.Star.gameObject);
                thing.Star = null;
            }
            Dialogue.Narration(thing.Def.Label, thing.Def.Lines);
        }

        /// <summary>One rest per visit: full HP and MP, free.</summary>
        void RestAtFire()
        {
            if (Session.RestedAt == room.Id)
            {
                Ui.Toast("The fire has burned low. Come back another time.", Palette.TextDim);
                return;
            }
            if (P.Hp >= P.MaxHp && P.Mp >= P.MaxMp)
            {
                Ui.Toast("You are already rested.", Palette.TextDim);
                return;
            }
            Session.RestedAt = room.Id;
            P.RestoreFull();
            GameRoot.I.Blink(0.3f, 0.6f, () =>
            {
                Fx.HealSparkles(World, player.X, player.Y, 0xffb347);
                Sfx.Play("heal");
                Ui.Toast("You rest by the fire. HP and MP restored.", Palette.TextGood);
                Refresh();
            });
        }

        // --- First visit ---

        /// <summary>A quiet room's opening: dark bars, a low sound, the camera drifts toward what's ahead, and a few lines.</summary>
        void PlayFirstVisit()
        {
            var fv = room.FirstVisit;
            if (fv == null || leaving || cutscene != null) return;
            moveTarget = null;
            Sfx.Play("omen");

            var layer = Layer("first visit");
            var topBar = Ui.Rect(layer, 0, -70, Design.Width, 70, Color.black);
            var bottomBar = Ui.Rect(layer, 0, Design.Height, Design.Width, 150, new Color(0, 0, 0, 0.88f));
            var text = Ui.Text(layer, Design.Width / 2, Design.Height - 88, "", 24, ax: 0.5f, ay: 0.5f, wrap: 1040, align: TextAnchor.MiddleCenter);
            text.style.opacity = 0;
            var hint = Ui.Text(layer, Design.Width - 24, Design.Height - 16, "E / Space / click: continue", 14, Palette.TextDim, ax: 1, ay: 1);
            hint.style.opacity = 0;
            // The room's own buttons and labels would zoom too: hide them for the scene.
            labels.style.display = DisplayStyle.None;
            ui.style.display = DisplayStyle.None;

            Anim.Float(-70, 0, 0.4f, v => topBar.style.top = v);
            Anim.Float(Design.Height, Design.Height - 150, 0.4f, v => bottomBar.style.top = v);
            const float zoom = 1.15f;
            var half = Design.Width / (2 * zoom);
            var panX = Mathf.Clamp(fv.PanTo.HasValue ? (float)fv.PanTo.Value : Design.Width / 2, half, Design.Width - half);
            GameRoot.I.View(panX, Design.Height / 2 + 20, zoom, 2.4f, Ease.SineInOut);

            var lines = fv.Lines;
            void Show(int i)
            {
                text.text = lines[i];
                text.style.opacity = 0;
                Anim.Float(0, 1, 0.45f, a => text.style.opacity = a, Ease.Linear);
            }
            void Finish()
            {
                if (cutscene == null) return;
                cutscene = null;
                GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1, 0.6f, Ease.SineInOut);
                Anim.Float(0, -70, 0.4f, v => topBar.style.top = v);
                Anim.Float(1, 0, 0.4f, a =>
                {
                    bottomBar.style.opacity = a;
                    text.style.opacity = Math.Min(a, text.resolvedStyle.opacity);
                    hint.style.opacity = Math.Min(a, hint.resolvedStyle.opacity);
                }, Ease.Linear).OnComplete(() =>
                {
                    layer.RemoveFromHierarchy();
                    labels.style.display = DisplayStyle.Flex;
                    ui.style.display = DisplayStyle.Flex;
                });
            }
            // A short pause before taps count, so the key that walked you in doesn't skip the first line.
            var ready = Now + 0.7f;
            var line = 0;
            cutscene = new Cutscene
            {
                Next = () =>
                {
                    if (Now < ready) return;
                    ready = Now + 0.25f;
                    line += 1;
                    if (line >= lines.Count) Finish();
                    else Show(line);
                },
                Skip = Finish,
            };
            Anim.After(0.4f, () =>
            {
                Show(0);
                Anim.Float(0, 1, 0.4f, a => hint.style.opacity = a, Ease.Linear, 0.6f);
            });
        }

        void NpcHop(Npc npc) => Anim.Float(0, -10, 0.1f, v => npc.Hop = v, Ease.CubicOut, yoyo: true).OnComplete(() => npc.Hop = 0);

        void Interact(Npc npc, bool intro = false)
        {
            if (ModalOpen) return;
            var role = npc.Def.Role;
            var actions = new List<DialogueAction>();
            // Story text off: one line of small talk, and quests say what to do instead of telling the story.
            var story = Saves.StoryText;
            var lines = story ? npc.Def.Dialogue : npc.Def.Dialogue.Take(1).ToList();

            if (role.Kind == "guild")
            {
                moveTarget = null;
                player.Facing = npc.X < player.X ? -1 : 1;
                NpcHop(npc);
                GuildPanel.OpenGuild(P, npc.Def, Refresh, r => HuntTurnedIn(r, npc));
                return;
            }

            var q = Quests.ForNpc(P, npc.Def.Id);
            if (q?.status == "ready")
            {
                var quest = q.Value.quest;
                // Story text off still keeps the giver's voice: their first thank-you line.
                lines = story ? quest.Complete : new List<string> { $"{quest.Name}: done!  {quest.Complete[0]}" };
                actions.Add(new DialogueAction("Complete", close =>
                {
                    close();
                    CompleteQuest(quest, npc);
                }));
                moveTarget = null;
                NpcHop(npc);
                Dialogue.Open(npc.Def, actions, lines);
                return;
            }
            if (q?.status == "available")
            {
                var quest = q.Value.quest;
                lines = story ? (intro ? npc.Def.Dialogue.Concat(quest.Offer).ToList() : quest.Offer) : new List<string> { $"New quest: {quest.Name}.  {quest.Reminder}" };
                actions.Add(new DialogueAction("Accept", close =>
                {
                    close();
                    Quests.Accept(P, quest);
                    Sfx.Play("quest");
                    Ui.Toast($"New quest: {quest.Name}", Palette.TextGold);
                    RefreshQuests();
                    Save(false);
                }));
            }
            else if (q?.status == "active")
            {
                var quest = q.Value.quest;
                lines = new List<string> { $"{quest.Reminder}  ({quest.Goal}: {Quests.Progress(P, quest)}/{quest.Objective.Count})" };
            }
            else if (q?.status == "rank")
            {
                lines = new List<string>
                {
                    $"I have work for you, but not for a Rank {Guild.RankId(P.Data.Guild.Rank)}. Come back once the Adventurers' Guild ranks you {q.Value.quest.RequiresRank}: take their hunts (a Guild Clerk in any town).",
                };
            }
            moveTarget = null;
            player.Facing = npc.X < player.X ? -1 : 1;
            NpcHop(npc);

            if (role.Kind == "shop")
            {
                actions.Add(new DialogueAction("Browse wares", close =>
                {
                    close();
                    InventoryPanel.OpenShop(P, $"{npc.Def.Name}'s Shop", Books.ShopStock(P, role), Refresh);
                }));
            }
            else if (role.Kind == "forge")
            {
                actions.Add(new DialogueAction("Use the forge", close =>
                {
                    close();
                    ForgePanel.OpenForge(P, $"{npc.Def.Name}'s Forge", role.Recipes, Refresh);
                }));
            }
            else if (role.Kind == "inn")
            {
                var rested = P.Hp >= P.MaxHp && P.Mp >= P.MaxMp;
                var cost = Db.B.InnCost;
                actions.Add(new DialogueAction("Rest", close =>
                {
                    P.Data.Gold -= cost;
                    P.RestoreFull();
                    close();
                    GameRoot.I.Blink(0.3f, 0.5f, () =>
                    {
                        Fx.HealSparkles(World, player.X, player.Y, 0x7dff9a);
                        Sfx.Play("heal");
                        Ui.Toast("You feel refreshed.", Palette.TextGood);
                        Refresh();
                    });
                }, rested ? "Already rested" : $"{cost} g", !rested && P.Gold >= cost));
            }

            Dialogue.Open(npc.Def, actions, lines);
        }

        // --- Quests ---

        /// <summary>Update NPC markers, the quest tracker, and the stat-point hint on the Character button.</summary>
        void RefreshQuests()
        {
            foreach (var npc in npcs)
            {
                var q = Quests.ForNpc(P, npc.Def.Id);
                var huntDone = npc.Def.Role.Kind == "guild" && Guild.AnyHuntReady(P);
                var symbol = q?.status == "ready" || huntDone ? "?" : q?.status == "available" ? "!" : "";
                if (npc.Marker == null) npc.Marker = Ui.Text(labels, npc.X, 0, "", 40, Palette.TextGold, true, 0.5f, 1, stroke: true);
                npc.Marker.text = symbol;
                npc.Marker.style.display = symbol != "" ? DisplayStyle.Flex : DisplayStyle.None;
            }

            tracker.Clear();
            var quests = Quests.Active(P);
            var hunts = Guild.ActiveHunts(P);
            var waiting = quests.Count == 0 ? Quests.WaitingOnRank(P) : null;
            tracker.style.display = DisplayStyle.None;
            if (quests.Count > 0 || hunts.Count > 0 || waiting != null)
            {
                var rows = new List<string>();
                foreach (var q in quests)
                    rows.Add(q.status == "ready" ? $"{q.quest.Name}: done! Return to {GiverName(q.quest)}" : $"{q.quest.Name}: {q.quest.Goal} {q.count}/{q.quest.Objective.Count}");
                foreach (var h in hunts)
                    rows.Add(h.Ready ? $"Hunt ({Guild.RankId(h.Hunt.Rank)}): done! Return to a Guild Clerk" : $"Hunt ({Guild.RankId(h.Hunt.Rank)}): {Guild.Goal(h.Hunt)} {h.Count}/{h.Hunt.Count}");
                if (waiting != null) rows.Add($"Next: reach Guild Rank {waiting.RequiresRank} (you are {Guild.RankId(P.Data.Guild.Rank)})");
                var text = FlowText(string.Join("\n", rows.Take(5)), 16, Palette.Text, false, 0);
                text.style.unityParagraphSpacing = 6;
                tracker.style.display = DisplayStyle.Flex;
                tracker.style.minWidth = 340;
                tracker.style.paddingLeft = tracker.style.paddingRight = 12;
                tracker.style.paddingTop = tracker.style.paddingBottom = 8;
                if (tracker.Q("card") == null) Felt.CardBehind(tracker, 42);
                tracker.Add(text);
            }

            var points = P.Data.StatPoints;
            charBtn.SetText(points > 0 ? $"Character ({points})" : "Character");
            var fresh = P.Data.Gear.Count(g => g.IsNew == true);
            invBtn.SetText(fresh > 0 ? $"Inventory ({fresh})" : "Inventory");
            charPulse?.Stop();
            charBtn.style.scale = new Scale(Vector3.one);
            if (points > 0) charPulse = Anim.Float(1, 1.07f, 0.5f, s => charBtn.style.scale = new Scale(new Vector3(s, s, 1)), Ease.Linear, repeat: -1, yoyo: true);
        }

        static string GiverName(QuestDef quest) =>
            Db.Data.Rooms.SelectMany(r => r.Npcs).FirstOrDefault(n => n.Id == quest.Giver)?.Name ?? "the quest giver";

        void LevelUpToasts(int levelsGained, IEnumerable<SkillDef> skills, float delay)
        {
            if (levelsGained <= 0) return;
            var list = skills.ToList();
            Anim.After(delay, () =>
            {
                Fx.LevelUpBurst(World, player.X, player.Y);
                Sfx.Play("levelup");
                Ui.Toast($"Level up! Now level {P.Level}. +{levelsGained * Db.B.StatPointsPerLevel} stat points (C)", Palette.TextGood);
                foreach (var sk in list) Ui.Toast($"New skill: {sk.Name}", Palette.TextMagic);
            });
        }

        /// <summary>A hunt handed in at a clerk: coins, then level-ups and promotions over the board.</summary>
        void HuntTurnedIn(Guild.TurnInHunt result, Npc npc)
        {
            var rewards = result.Rewards;
            var items = rewards.Items.Select(g => ItemUi.LootName(Db.Item(g.ItemId), g.Traits()));
            Ui.Toast(string.Join("   ", new[] { $"+{rewards.Xp} XP", $"+{rewards.Gold} gold" }.Concat(items)), Palette.TextGood);
            _ = Fx.CoinShower(World, new Vector2(npc.X, npc.Y - 120), new Vector2(320, 40), (int)Math.Ceiling(rewards.Gold / 6), () => Sfx.Play("coin"));
            foreach (var rank in result.Promotions)
            {
                Anim.After(0.3f, () =>
                {
                    Fx.Confetti(World);
                    Sfx.Play("levelup");
                    Ui.Toast($"Guild promotion: Rank {rank}!", Palette.TextGold);
                });
            }
            LevelUpToasts(result.LevelsGained, P.SkillsLearnedSince(P.Level - result.LevelsGained), 0.7f);
            Save(false);
        }

        void CompleteQuest(QuestDef quest, Npc npc)
        {
            var result = Quests.TurnIn(P, quest, Rng);
            if (result == null) return;
            Sfx.Play("quest");
            var rewards = result.Rewards;
            Ui.Toast($"Quest complete: {quest.Name}", Palette.TextGold);
            var items = rewards.Items.Select(g => ItemUi.LootName(Db.Item(g.ItemId), g.Traits()));
            Ui.Toast(string.Join("   ", new[] { $"+{rewards.Xp} XP", $"+{rewards.Gold} gold" }.Concat(items)), Palette.TextGood);
            _ = Fx.CoinShower(World, new Vector2(npc.X, npc.Y - 120), new Vector2(320, 40), (int)Math.Ceiling(rewards.Gold / 6), () => Sfx.Play("coin"));
            LevelUpToasts(result.LevelsGained, result.NewSkills ?? new List<SkillDef>(), 0.5f);
            Refresh();
            RefreshQuests();
            if (quest.EndsChapter != null) Anim.After(1.4f, () => ChapterComplete(quest.EndsChapter));
        }

        void ChapterComplete(string title)
        {
            var layer = Layer("chapter complete");
            layer.pickingMode = PickingMode.Position;
            layer.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            var head = Ui.Text(layer, Design.Width / 2, 220, "Chapter Complete!", 72, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
            Ui.Text(layer, Design.Width / 2, 300, title, 30, Palette.Text, ax: 0.5f, ay: 0.5f);
            Ui.Text(layer, Design.Width / 2, 370, "Thanks for playing this early build. More chapters are on the way.\nYou can keep exploring, leveling and shopping.",
                20, Palette.TextDim, ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter);
            Anim.Float(0.3f, 1, 0.6f, s => head.style.scale = new Scale(new Vector3(s, s, 1)), Ease.BackOut);
            Sfx.Play("victory");
            Fx.Confetti(World);
            long[] pops = { 0xffd75e, 0x7ec8ff, 0xff7b9c, 0x7dff9a };
            for (int i = 0; i < 6; i++)
            {
                Anim.After(i * 0.35f, () =>
                {
                    var x = Random.Range(200, (int)Design.Width - 200 + 1);
                    var y = Random.Range(120, 421);
                    Fx.Explosion(World, x, y, new long[] { 0xffffff, pops[Random.Range(0, pops.Length)], 0xffffff });
                    Sfx.Play("explode");
                });
            }
            new UiButton(layer, Design.Width / 2, 480, "Continue", layer.RemoveFromHierarchy, 240, 60);
        }

        // --- Boss ---

        void ConfirmBoss()
        {
            if (boss == null || ModalOpen) return;
            var def = boss.Def;
            // A gate guard is only ever fought until it's beaten, then it's gone.
            var rematch = !Travel.IsGuard(room) && !Quests.NeedsKill(P, def.Id);
            var heroic = rematch && def.Heroic != null;
            var h = Db.B.Heroic;
            var modal = new UiModal(760, heroic ? 360 : 300, rematch ? $"{def.Name} (rematch)" : def.Name);
            var intro = room.Boss?.Intro ?? "It turns toward you.";
            var text = rematch
                ? $"It is back. There is no running from this fight.\nRematch: {Math.Round(Db.B.BossRepeatRewards * 100)}% XP and gold, full loot{(Features.On(P, "sigils") ? " (Sigils drop more often from bosses)" : "")}." +
                  (heroic
                      ? $"\nHeroic: +{h.Levels} levels, +{Math.Round((h.Damage - 1) * 100)}% damage and a new move ({def.Heroic.Text}) Full XP and gold, {h.LootRolls} gear rolls."
                      : "")
                : $"{intro}\nThere is no running from this fight.";
            Ui.Text(modal.Content, 380, heroic ? 130 : 110, text, rematch ? 18 : 22, ax: 0.5f, ay: 0.5f, wrap: 700, align: TextAnchor.MiddleCenter);
            var y = heroic ? 290 : 230;
            if (heroic)
            {
                new UiButton(modal.Content, 150, y, "Fight", () => { modal.Close(); StartBossBattle(); }, 200);
                new UiButton(modal.Content, 380, y, "Heroic", () => { modal.Close(); StartBossBattle(true); }, 200);
                new UiButton(modal.Content, 610, y, "Not yet", () => modal.Close(), 200);
            }
            else
            {
                new UiButton(modal.Content, 260, y, "Fight", () => { modal.Close(); StartBossBattle(); }, 220);
                new UiButton(modal.Content, 500, y, "Not yet", () => modal.Close(), 220);
            }
        }

        void StartBossBattle(bool heroic = false)
        {
            if (boss == null || leaving) return;
            leaving = true;
            ShowPrompt(false);
            Session.SaveRoom(new RoomSnapshot { RoomId = room.Id, FightingUid = -1 });
            RememberRoom();
            SaveLocation();
            Sfx.Play("encounter");
            labels.style.display = DisplayStyle.None;
            GameRoot.I.Shake(0.5f, 19);
            GameRoot.I.Flash(0.3f, new Color(1, 0.235f, 0.235f));
            GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1.4f, 0.6f, Ease.CubicIn);
            var setup = new BattleSetup { ZoneId = room.ZoneId, MonsterId = boss.Def.Id, RoomId = room.Id, X = player.X, Y = player.Y, Heroic = heroic };
            Anim.After(0.6f, () => GameRoot.I.Go(new BattleScreen(setup), 0.3f));
        }
    }
}
