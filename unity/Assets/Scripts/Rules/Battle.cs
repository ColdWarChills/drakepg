using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>
    /// What happened during a turn, in order; the battle screen plays these back. One class for the TS union: `Kind`
    /// is hit, miss, heal, buff, status, immune, tick, expire, stunned, flee, defend, swap, windup, rage, phase, shell,
    /// enrage, escape or defeated.
    /// </summary>
    public class BattleEvent
    {
        public string Kind;
        /// <summary>player or enemy: who acted (hit, miss, heal, buff).</summary>
        public string Source;
        /// <summary>Who it happened to (status, immune, tick, expire), or who (stunned, defeated: `Side`).</summary>
        public string Target, Side;
        public string Move, MoveId, DamageType, Element, Effect, Status, Text, Weapon;
        public double Damage, Hp, Mp, Heal, Bonus;
        public int Turns;
        public bool Crit, Absorbed, Success;
        public int? ShellLeft;
        public int Left;

        /// <summary>A compact line for logs and the parity tests (the same text as scripts/golden.ts writes).</summary>
        public override string ToString()
        {
            string n(double v) => JsMath.Str(v);
            return Kind switch
            {
                "hit" => $"hit {Source} {MoveId} {n(Damage)}{(Crit ? " crit" : "")}{(Effect != null ? " " + Effect : "")}{(Absorbed ? " absorbed" : "")}{(ShellLeft.HasValue ? " shell" + ShellLeft : "")}",
                "miss" => $"miss {Source} {MoveId}",
                "heal" => $"heal {Source} {MoveId} {n(Hp)} {n(Mp)}",
                "buff" => $"buff {Source} {MoveId}",
                "status" => $"status {Target} {Status} {Turns}",
                "immune" => $"immune {Target} {Status}",
                "tick" => $"tick {Target} {Status} {n(Damage)} {n(Heal)}{(Effect != null ? " " + Effect : "")}",
                "expire" => $"expire {Target} {Status}",
                "stunned" => $"stunned {Side}",
                "flee" => $"flee {(Success ? "ok" : "fail")}",
                "defend" => $"defend {n(Mp)}",
                "swap" => $"swap {Weapon}",
                "windup" => $"windup {Move}",
                "rage" => $"rage {n(Mp)}",
                "phase" => $"phase {Element}",
                "shell" => $"shell {Left}",
                "enrage" => $"enrage {n(Bonus)}",
                "escape" => "escape",
                "defeated" => $"defeated {Side}",
                _ => Kind,
            };
        }
    }

    public class Enemy
    {
        public MonsterDef Def;
        public double Hp, MaxHp;
    }

    public class ActiveStatus
    {
        public StatusDef Def;
        public int Turns;
        public bool Fresh;
        public double Tick;
        public string Element;
        public bool Weak;
    }

    /// <summary>One fight, a port of src/game/Battle.ts. Never touches Unity.</summary>
    public class Battle
    {
        public readonly Enemy Enemy;
        public readonly Player Player;
        /// <summary>player_turn, victory, defeat, fled or escaped.</summary>
        public string State = "player_turn";
        public Rewards Rewards;
        public double RewardScale = 1;
        public string ZoneId;
        public MonsterAttack Charging;
        public MonsterAttack Intent;
        public int Turn;
        int picks;
        readonly HashSet<string> usedOnce = new HashSet<string>();
        double enrageShown;
        public int Shell;
        bool shellReformed;
        int enemyTurns;
        bool defending;
        // Insertion order matters for the countdown, like a JS Map.
        readonly List<string> cooldownOrder = new List<string>();
        readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>();
        readonly Dictionary<string, List<ActiveStatus>> effects = new Dictionary<string, List<ActiveStatus>>
        {
            ["player"] = new List<ActiveStatus>(), ["enemy"] = new List<ActiveStatus>(),
        };
        readonly IRng rng;

        static Balance B => Db.B;

        public Battle(Player player, MonsterDef monster, IRng rng = null)
        {
            Player = player;
            this.rng = rng ?? new SystemRng();
            Enemy = new Enemy { Def = monster, Hp = monster.Hp, MaxHp = monster.Hp };
            Shell = monster.Shell?.Hits ?? 0;
            Intent = PickIntent();
        }

        public bool IsOver => State != "player_turn";

        public IReadOnlyList<ActiveStatus> Statuses(string side) => effects[side];

        public int CooldownLeft(string skillId) => cooldowns.TryGetValue(skillId, out var v) ? v : 0;

        public int? EscapeIn => Enemy.Def.EscapeAfter is int n && n > 0 ? Math.Max(0, n - enemyTurns) : (int?)null;

        public double ResistOf(string side, string element)
        {
            var b = side == "player" ? Player.Resists : (Enemy.Def.Resists ?? new Dictionary<string, double>());
            double r = Get(b, element) + Get(b, "all");
            foreach (var s in effects[side]) r += Get(s.Def.Resists, element) + Get(s.Def.Resists, "all");
            var max = side == "player" ? B.MaxPlayerResist : 200;
            return JsMath.Clamp(r, B.MinResist, max);
        }

        static double Get(Dictionary<string, double> d, string k) => d != null && k != null && d.TryGetValue(k, out var v) ? v : 0;

        // --- Player actions ---

        public List<BattleEvent> Attack()
        {
            if (IsOver) return new List<BattleEvent>();
            var p = Player;
            var events = new List<BattleEvent>();
            var hit = Strike("player", "Attack", "attack", p.WeaponDamageType, p.WeaponElement, 1);
            events.Add(hit);
            var proc = p.Weapon?.Proc;
            if (hit.Kind == "hit" && proc != null && Enemy.Hp > 0 && rng.Chance(p.ProcChance))
            {
                var element = proc.Element ?? p.WeaponElement;
                for (int i = 0; i < (proc.Hits ?? 1) && proc.Power > 0 && Enemy.Hp > 0; i++)
                    events.Add(Strike("player", proc.Name, "proc", p.WeaponDamageType, element, proc.Power));
                if (proc.Power == 0) events.Add(new BattleEvent { Kind = "buff", Source = "player", Move = proc.Name, MoveId = "proc" });
                foreach (var a in proc.Applies ?? new List<Apply>()) PushApply(events, "player", a, element, p.WeaponDamageType);
                if (proc.Heal.HasValue && proc.Heal.Value != 0)
                    events.Add(new BattleEvent { Kind = "heal", Source = "player", Move = proc.Name, MoveId = "proc", Hp = p.Heal(JsMath.Round(p.MaxHp * proc.Heal.Value)) });
            }
            return EndPlayerTurn(events);
        }

        public bool CanUseSkill(string skillId)
        {
            var skill = Db.Skill(skillId);
            if (skill.Kind == "flee" && !CanFlee) return false;
            return !IsOver && Player.IsCarried(skillId) && Player.Mp >= skill.MpCost && !cooldowns.ContainsKey(skillId);
        }

        /// <summary>A family skill with a weapon of its element: +Balance.Books.Match damage. For the skill buttons too.</summary>
        public bool Matched(SkillDef skill) => skill.Family != null && skill.Element != null && skill.Element == Player.WeaponElement;

        public List<BattleEvent> UseSkill(string skillId)
        {
            if (!CanUseSkill(skillId)) return new List<BattleEvent>();
            var skill = Db.Skill(skillId);
            var p = Player;
            p.SpendMp(skill.MpCost);
            p.Data.FreshSkills = p.Data.FreshSkills.Where(id => id != skill.Id).ToList();
            if (skill.Cooldown > 0) SetCooldown(skill.Id, skill.Cooldown + 1);

            var events = new List<BattleEvent>();
            var type = skill.DamageType == null || skill.DamageType == "weapon" ? p.WeaponDamageType : skill.DamageType;
            var element = skill.Element == null || skill.Element == "weapon" ? p.WeaponElement : skill.Element;
            bool landed = false;

            if (skill.Kind == "flee")
            {
                // Play Dead: a sure flee.
                State = "fled";
                return new List<BattleEvent> { new BattleEvent { Kind = "flee", Success = true } };
            }
            if (skill.Kind == "heal")
            {
                var amount = JsMath.Round(p.MaxHp * skill.Power + p.Stats.Int);
                events.Add(new BattleEvent { Kind = "heal", Source = "player", Move = skill.Name, MoveId = skill.Id, Hp = p.Heal(amount) });
            }
            else if (skill.Kind == "mana")
            {
                events.Add(new BattleEvent { Kind = "heal", Source = "player", Move = skill.Name, MoveId = skill.Id, Mp = p.RestoreMp(JsMath.Round(p.MaxMp * skill.Power)) });
            }
            else if (skill.Kind == "buff")
            {
                events.Add(new BattleEvent { Kind = "buff", Source = "player", Move = skill.Name, MoveId = skill.Id });
            }
            else
            {
                double dealt = 0;
                var power = skill.Power * (Matched(skill) ? 1 + B.Books.Match : 1);
                for (int i = 0; i < (skill.Hits ?? 1) && Enemy.Hp > 0; i++)
                {
                    var e = Strike("player", skill.Name, skill.Id, type, element, power, skill.CritBonus ?? 0, skill.Blunt);
                    events.Add(e);
                    if (e.Kind == "hit")
                    {
                        landed = true;
                        dealt += e.Damage;
                    }
                }
                if (skill.Lifesteal.HasValue && skill.Lifesteal.Value != 0 && dealt > 0)
                    events.Add(new BattleEvent { Kind = "heal", Source = "player", Move = skill.Name, MoveId = skill.Id, Hp = p.Heal(JsMath.Round(dealt * skill.Lifesteal.Value)) });
            }

            foreach (var a in skill.Applies ?? new List<Apply>())
            {
                if (!a.Self && (Enemy.Hp <= 0 || (skill.Kind == "damage" && !landed))) continue;
                PushApply(events, "player", a, element, type);
            }
            return EndPlayerTurn(events);
        }

        void SetCooldown(string id, int turns)
        {
            if (!cooldowns.ContainsKey(id)) cooldownOrder.Add(id);
            cooldowns[id] = turns;
        }

        public bool CanUseItem(string itemId)
        {
            var item = Db.Item(itemId);
            if (IsOver || item.Type != "consumable" || Player.CountItem(itemId) == 0) return false;
            var p = Player;
            return (item.Restore.Hp is double h && h != 0 && p.Hp < p.MaxHp) || (item.Restore.Mp is double m && m != 0 && p.Mp < p.MaxMp);
        }

        public List<BattleEvent> UseItem(string itemId)
        {
            if (IsOver) return new List<BattleEvent>();
            var item = Db.Item(itemId);
            var restored = Player.UseConsumable(itemId);
            if (restored == null) return new List<BattleEvent>();
            var events = new List<BattleEvent> { new BattleEvent { Kind = "heal", Source = "player", Move = item.Name, MoveId = item.Id, Hp = restored.Value.hp, Mp = restored.Value.mp } };
            return EndPlayerTurn(events);
        }

        public List<BattleEvent> Defend()
        {
            if (IsOver) return new List<BattleEvent>();
            defending = true;
            var mp = Player.RestoreMp(B.DefendMp);
            return EndPlayerTurn(new List<BattleEvent> { new BattleEvent { Kind = "defend", Mp = mp } });
        }

        public bool CanSwapWeapon => !IsOver && Player.Data.Equipment["spare"] != null;

        public List<BattleEvent> SwapWeapon()
        {
            if (!CanSwapWeapon) return new List<BattleEvent>();
            var p = Player;
            p.SwapWeapons();
            var element = p.WeaponElement;
            return EndPlayerTurn(new List<BattleEvent> { new BattleEvent { Kind = "swap", Weapon = p.Weapon?.Name ?? "bare hands", Element = element, Effect = EffectOn(element) } });
        }

        public string EffectOn(string element) => ElementMultiplier("enemy", element).effect;

        public string SkillEffect(SkillDef skill)
        {
            if (skill.Kind != "damage") return null;
            var element = skill.Element == null || skill.Element == "weapon" ? Player.WeaponElement : skill.Element;
            var effect = EffectOn(element);
            if (effect == "absorb" || effect == "immune") return effect;
            var weak = Enemy.Def.WeakTo ?? new WeakTo();
            if ((skill.Blunt && (weak.Blunt ?? 0) != 0) || ((weak.Poison ?? 0) != 0 && (skill.Applies?.Any(a => a.Status == "poison" && !a.Self) ?? false))) return "weak";
            return effect;
        }

        public bool CanFlee => !Enemy.Def.Boss;

        public double FleeChance
        {
            get
            {
                if (!CanFlee) return 0;
                var diff = Player.Stats.Dex - Enemy.Def.Stats.Dex;
                return JsMath.Clamp(B.BaseFleeChance + diff * 0.03, 0.2, 0.9);
            }
        }

        public List<BattleEvent> Flee()
        {
            if (IsOver || !CanFlee) return new List<BattleEvent>();
            if (rng.Chance(FleeChance))
            {
                State = "fled";
                return new List<BattleEvent> { new BattleEvent { Kind = "flee", Success = true } };
            }
            return EndPlayerTurn(new List<BattleEvent> { new BattleEvent { Kind = "flee", Success = false } });
        }

        // --- Turn flow ---

        List<BattleEvent> EndPlayerTurn(List<BattleEvent> events)
        {
            Turn += 1;
            if (CheckOver(events)) return events;
            EndTurn("player", events);
            for (int round = 0; round < 3; round++)
            {
                EnemyTurn(events);
                defending = false;
                if (CheckOver(events)) return events;
                StartTurn("player", events);
                if (CheckOver(events)) return events;
                if (!ConsumeStun("player", events)) break;
                EndTurn("player", events);
            }
            return events;
        }

        void EnemyTurn(List<BattleEvent> events)
        {
            StartTurn("enemy", events);
            if (Enemy.Hp <= 0) return;
            if (ConsumeStun("enemy", events))
            {
                if (Charging != null)
                {
                    Charging = null;
                    Intent = PickIntent();
                }
            }
            else if (Enemy.Def.EscapeAfter is int esc && esc > 0 && ++enemyTurns >= esc)
            {
                events.Add(new BattleEvent { Kind = "escape" });
                State = "escaped";
                return;
            }
            else
            {
                EnemyAction(events);
            }
            EndTurn("enemy", events);
        }

        void EnemyAction(List<BattleEvent> events)
        {
            var m = Enemy.Def;
            var bonus = Enrage;
            if (bonus > enrageShown)
            {
                enrageShown = bonus;
                events.Add(new BattleEvent { Kind = "enrage", Bonus = bonus });
            }
            MonsterAttack attack;
            if (Charging != null)
            {
                attack = Charging;
                Charging = null;
            }
            else
            {
                attack = Intent;
                if (attack.Windup != null)
                {
                    Charging = attack;
                    events.Add(new BattleEvent { Kind = "windup", Move = attack.Name, Text = attack.Windup });
                    return;
                }
            }
            Intent = PickIntent();
            var element = attack.Element ?? m.Element;
            if (attack.Heal.HasValue && attack.Heal.Value != 0)
            {
                events.Add(new BattleEvent { Kind = "heal", Source = "enemy", Move = attack.Name, MoveId = "enemy", Hp = HealSide("enemy", JsMath.Round(Enemy.MaxHp * attack.Heal.Value)) });
            }
            else if (attack.Power == 0)
            {
                events.Add(new BattleEvent { Kind = "buff", Source = "enemy", Move = attack.Name, MoveId = "enemy" });
            }
            else
            {
                var hit = Strike("enemy", attack.Name, "enemy", attack.DamageType, element, attack.Power);
                events.Add(hit);
                if (hit.Kind == "miss") return;
                DoRage(hit.Damage, events);
            }
            foreach (var a in attack.Applies ?? new List<Apply>()) PushApply(events, "enemy", a, element, attack.DamageType);
        }

        MonsterAttack PickIntent(bool count = true)
        {
            if (Charging != null) return Charging;
            var attacks = Enemy.Def.Attacks;
            int n = picks + 1;
            if (count) picks = n;
            var opener = n == 1 ? attacks.FirstOrDefault(a => a.Opener) : null;
            if (opener != null) return opener;
            var timed = attacks.FirstOrDefault(a => a.Every is int e && e != 0 && n % e == 0);
            if (timed != null) return timed;
            var hurt = attacks.FirstOrDefault(a => a.HpBelow is double hb && hb != 0 && !usedOnce.Contains(a.Name) && Enemy.Hp <= Enemy.MaxHp * hb);
            if (hurt != null)
            {
                usedOnce.Add(hurt.Name);
                return hurt;
            }
            var pool = attacks.Where(a => !a.Opener && !(a.Every is int e && e != 0) && !(a.HpBelow is double hb && hb != 0)).ToList();
            return rng.WeightedPick(pool.Count > 0 ? pool : attacks, a => a.Weight);
        }

        public double Enrage => EnrageAt(Turn);

        double EnrageAt(int turn)
        {
            var e = B.Enrage;
            var at = Enemy.Def.Boss ? e.BossTurns : e.Turns;
            return Math.Max(0, turn - at + 1) * e.Step;
        }

        public int TurnsToEnrage
        {
            get
            {
                var e = B.Enrage;
                return Math.Max(0, (Enemy.Def.Boss ? e.BossTurns : e.Turns) - Turn);
            }
        }

        /// <summary>Rough damage of the enemy's next move (average roll, no crit, before Defend). Null if it doesn't hit.</summary>
        public double? IntentDamage()
        {
            var a = Intent;
            if (a.Power <= 0 || (a.Heal.HasValue && a.Heal.Value != 0)) return null;
            var element = a.Element ?? Enemy.Def.Element;
            var raw = BaseHit("enemy", a.DamageType) * a.Power;
            var mult = ElementMultiplier("player", element).mult;
            var enraged = (1 + EnrageAt(Turn + 1)) / (1 + Enrage);
            return Math.Max(1, JsMath.Round(ScaleHit("enemy", "player", Mitigate(raw, DefenseOf("player", a.DamageType)), mult) * enraged));
        }

        void StartTurn(string side, List<BattleEvent> events)
        {
            foreach (var s in effects[side].ToList())
            {
                if (s.Tick > 0)
                {
                    var (mult, effect) = ElementMultiplier(side, s.Element);
                    if (mult < 0)
                    {
                        var heal = HealSide(side, Math.Max(1, JsMath.Round(s.Tick * -mult)));
                        events.Add(new BattleEvent { Kind = "tick", Target = side, Status = s.Def.Id, Damage = 0, Heal = heal, Element = s.Element, Effect = effect });
                    }
                    else
                    {
                        var damage = mult > 0 && !(Cheats.God && side == "player") ? Math.Max(1, JsMath.Round(s.Tick * mult)) : 0;
                        DamageSide(side, damage);
                        events.Add(new BattleEvent { Kind = "tick", Target = side, Status = s.Def.Id, Damage = damage, Heal = 0, Element = s.Element, Effect = s.Weak ? "weak" : effect });
                    }
                }
                if (s.Def.Regen.HasValue && s.Def.Regen.Value != 0)
                {
                    var heal = HealSide(side, JsMath.Round(MaxHpOf(side) * s.Def.Regen.Value));
                    events.Add(new BattleEvent { Kind = "tick", Target = side, Status = s.Def.Id, Damage = 0, Heal = heal });
                }
                if (HpOf(side) <= 0) return;
            }
        }

        void EndTurn(string side, List<BattleEvent> events)
        {
            var list = effects[side];
            foreach (var s in list.ToList())
            {
                if (s.Fresh)
                {
                    s.Fresh = false;
                    continue;
                }
                s.Turns -= 1;
                if (s.Turns <= 0)
                {
                    list.Remove(s);
                    events.Add(new BattleEvent { Kind = "expire", Target = side, Status = s.Def.Id });
                }
            }
            if (side == "player")
            {
                foreach (var id in cooldownOrder.ToList())
                {
                    var left = cooldowns[id];
                    if (left <= 1)
                    {
                        cooldowns.Remove(id);
                        cooldownOrder.Remove(id);
                    }
                    else cooldowns[id] = left - 1;
                }
            }
        }

        bool ConsumeStun(string side, List<BattleEvent> events)
        {
            var list = effects[side];
            int i = list.FindIndex(s => s.Def.Stun);
            if (i < 0) return false;
            var stun = list[i];
            list.RemoveAt(i);
            events.Add(new BattleEvent { Kind = "stunned", Side = side });
            if (stun.Def.Freeze)
            {
                // Thawed: it can't be frozen again until it has acted once (lasts through its next turn).
                list.Add(new ActiveStatus { Def = Db.Status("thawed"), Turns = 1, Fresh = true, Tick = 0 });
                events.Add(new BattleEvent { Kind = "status", Target = side, Status = "thawed", Turns = 1 });
            }
            return true;
        }

        /// <summary>Freeze odds on the enemy right now, for a skill of this damage type and effect chance (0 while it's thawed).</summary>
        public double FreezeChance(string type, double baseChance)
        {
            if (effects["enemy"].Any(s => s.Def.Id == "thawed")) return 0;
            var f = B.Freeze;
            var raw = (baseChance + Player.StatFor(Player.Stats, type) * f.PerStat) * (1 - ResistOf("enemy", "ice") / 100);
            return JsMath.Clamp(raw, 0, f.Max);
        }

        bool CheckOver(List<BattleEvent> events)
        {
            if (State == "escaped") return true;
            CheckPhase(events);
            var grit = Player.Data.BossGrit;
            var id = Enemy.Def.Id;
            if (Enemy.Hp <= 0)
            {
                events.Add(new BattleEvent { Kind = "defeated", Side = "enemy" });
                State = "victory";
                Rewards = RollRewards();
                if (Enemy.Def.Boss) grit.Remove(id);
                return true;
            }
            if (Player.IsDead)
            {
                events.Add(new BattleEvent { Kind = "defeated", Side = "player" });
                State = "defeat";
                if (Enemy.Def.Boss) grit[id] = (grit.TryGetValue(id, out var g) ? g : 0) + 1;
                return true;
            }
            return false;
        }

        public double Grit
        {
            get
            {
                if (!Enemy.Def.Boss) return 0;
                var g = B.Grit;
                return Math.Min(g.Max, (Player.Data.BossGrit.TryGetValue(Enemy.Def.Id, out var n) ? n : 0) * g.Step);
            }
        }

        void DoRage(double damage, List<BattleEvent> events)
        {
            var gain = Player.ClassDef.MpOnHit ?? 0;
            if (gain <= 0 || damage <= 0 || Player.IsDead) return;
            var mp = Player.RestoreMp(gain);
            if (mp > 0) events.Add(new BattleEvent { Kind = "rage", Mp = mp });
        }

        void CheckPhase(List<BattleEvent> events)
        {
            var def = Enemy.Def;
            var hp = Enemy.Hp;
            var maxHp = Enemy.MaxHp;
            var shell = def.Shell;
            if (shell?.ReformAt is double ra && ra != 0 && !shellReformed && hp > 0 && hp <= maxHp * ra)
            {
                shellReformed = true;
                Shell = shell.Hits;
                var name = def.Name.StartsWith("The ") ? "the " + def.Name.Substring(4) : def.Name;
                events.Add(new BattleEvent { Kind = "shell", Left = shell.Hits, Text = $"The {shell.Name} grows back over {name}!" });
            }
            var phase = def.Phase;
            if (phase == null || hp <= 0 || hp > maxHp * phase.At) return;
            if (Charging != null)
            {
                var c = Charging.Clone();
                c.Element = Charging.Element ?? def.Element;
                Charging = c;
            }
            var next = def.Clone();
            next.Element = phase.Element;
            next.Resists = phase.Resists;
            next.Attacks = phase.Attacks ?? def.Attacks;
            next.Phase = null;
            Enemy.Def = next;
            Intent = PickIntent(false);
            events.Add(new BattleEvent { Kind = "phase", Text = phase.Text, Element = phase.Element });
        }

        // --- Statuses ---

        void PushApply(List<BattleEvent> events, string source, Apply a, string element, string type)
        {
            var target = a.Self ? source : Other(source);
            var def = Db.Status(a.Status);
            // Freeze: the odds grow with the stat and drop with Ice resist; a thawed target can't be frozen right away.
            if (def.Freeze && effects[target].Any(s => s.Def.Id == "thawed")) return;
            var odds = def.Freeze && source == "player" ? FreezeChance(type, a.Chance ?? 1) : a.Chance ?? 1;
            if (!rng.Chance(odds)) return;
            // Bosses that shrug off stuns shrug off Freeze too.
            var immune = Enemy.Def.Immune ?? new List<string>();
            if (target == "enemy" && (immune.Contains(def.Id) || (def.Stun && immune.Contains("stun"))))
            {
                events.Add(new BattleEvent { Kind = "immune", Target = target, Status = def.Id });
                return;
            }
            var weakPct = target == "enemy" && def.Id == "poison" ? (Enemy.Def.WeakTo?.Poison ?? 0) : 0;
            var tick = def.Dot != null ? Math.Max(1, JsMath.Round(BaseHit(source, type) * def.Dot.Power * (1 + weakPct / 100))) : 0;
            var dotElement = def.Dot?.Element == "weapon" ? element : def.Dot?.Element;
            var fresh = target == source;
            var weak = weakPct > 0;
            var existing = effects[target].FirstOrDefault(s => s.Def.Id == def.Id);
            if (existing != null)
            {
                existing.Turns = Math.Max(existing.Turns, a.Turns);
                existing.Tick = Math.Max(existing.Tick, tick);
                existing.Fresh = existing.Fresh || fresh;
                existing.Weak = existing.Weak || weak;
            }
            else
            {
                effects[target].Add(new ActiveStatus { Def = def, Turns = a.Turns, Fresh = fresh, Tick = tick, Element = dotElement, Weak = weak });
            }
            events.Add(new BattleEvent { Kind = "status", Target = target, Status = def.Id, Turns = a.Turns });
        }

        double Mod(string side, string key)
        {
            double total = 0;
            foreach (var s in effects[side]) total += s.Def.Mod(key);
            return total;
        }

        // --- Damage math ---

        static double Mitigate(double damage, double armor) => damage * (1 - armor / (armor + 30));

        Stats StatsOf(string side) => side == "player" ? Player.Stats : Enemy.Def.Stats;

        double[] RollOf(string side)
        {
            if (side == "player") return Player.WeaponDamage;
            var m = Enemy.Def.Boss ? B.BossDamage : B.MobDamage;
            return new[] { JsMath.Round(Enemy.Def.Damage[0] * m), JsMath.Round(Enemy.Def.Damage[1] * m) };
        }

        double DefenseOf(string side, string type) => side == "player" ? Player.DefenseFor(type) : Enemy.Def.DefenseOf(type);

        double HpOf(string side) => side == "player" ? Player.Hp : Enemy.Hp;

        double MaxHpOf(string side) => side == "player" ? Player.MaxHp : Enemy.MaxHp;

        void DamageSide(string side, double amount)
        {
            if (side == "player") Player.TakeDamage(amount);
            else Enemy.Hp = Math.Max(0, Enemy.Hp - amount);
        }

        double HealSide(string side, double amount)
        {
            if (side == "player") return Player.Heal(amount);
            var before = Enemy.Hp;
            Enemy.Hp = Math.Min(Enemy.MaxHp, Enemy.Hp + amount);
            return Enemy.Hp - before;
        }

        double BaseHit(string side, string type)
        {
            var r = RollOf(side);
            return (r[0] + r[1]) / 2 + Math.Floor(Player.StatFor(StatsOf(side), type) * B.StatDamageScale);
        }

        (double mult, string effect) ElementMultiplier(string target, string element)
        {
            if (element == null) return (1, null);
            var r = ResistOf(target, element);
            string effect = r > 100 ? "absorb" : r == 100 ? "immune" : r >= 25 ? "resisted" : r <= -25 ? "weak" : null;
            return (1 - r / 100, effect);
        }

        double ScaleHit(string source, string target, double damage, double mult)
        {
            damage *= mult * Math.Max(0, 1 + Mod(source, "damageDealt")) * Math.Max(0, 1 + Mod(target, "damageTaken"));
            if (source == "player") damage *= (1 + Grit) * (1 + Player.DamageBonus);
            else damage *= 1 + Enrage;
            return damage;
        }

        BattleEvent Strike(string source, string move, string moveId, string damageType, string element, double power, double critBonus = 0, bool blunt = false)
        {
            var target = Other(source);
            var atk = StatsOf(source);
            var def = StatsOf(target);
            BattleEvent Base(string kind) => new BattleEvent { Kind = kind, Source = source, Move = move, MoveId = moveId, DamageType = damageType, Element = element };

            var rolled = JsMath.Clamp(B.BaseHitChance + (atk.Dex - def.Dex) * 0.01, B.MinHitChance, B.MaxHitChance);
            var hitChance = JsMath.Clamp(rolled + Mod(source, "hitChance") - Mod(target, "evasion"), 0.05, B.MaxHitChance);
            if (!rng.Chance(hitChance)) return Base("miss");

            var range = RollOf(source);
            double damage = rng.RandInt((int)range[0], (int)range[1]) + Math.Floor(Player.StatFor(atk, damageType) * B.StatDamageScale);
            damage *= power;

            var gearCrit = source == "player" ? Player.CritBonus : 0;
            var critChance = B.BaseCritChance + atk.Dex * B.CritPerDex + Mod(source, "critChance") + critBonus + gearCrit;
            var crit = rng.Chance(critChance);
            if (crit) damage *= B.CritMultiplier;

            damage = Mitigate(damage, DefenseOf(target, damageType));
            var affinity = ElementMultiplier(target, element);
            var crush = blunt && target == "enemy" && affinity.mult > 0 ? (Enemy.Def.WeakTo?.Blunt ?? 0) / 100 : 0;
            var mult = affinity.mult * (1 + crush);
            var effect = crush > 0 && mult > 1 ? "weak" : affinity.effect;
            if (mult < 0)
            {
                var healed = HealSide(target, Math.Max(1, JsMath.Round(ScaleHit(source, target, damage, -mult))));
                var ev = Base("hit");
                ev.Damage = healed;
                ev.Crit = crit;
                ev.Effect = effect;
                ev.Absorbed = true;
                return ev;
            }
            damage = ScaleHit(source, target, damage, mult);
            if (target == "player" && defending) damage *= B.DefendDamage;
            var shell = Enemy.Def.Shell;
            var shielded = target == "enemy" && shell != null && Shell > 0;
            if (shielded) damage *= 1 - shell.Reduction;

            double final = mult > 0 ? Math.Max(1, JsMath.Round(damage)) : 0;
            if (Cheats.God && target == "player") final = 0;
            if (Cheats.OneHit && target == "enemy") final = Math.Max(final, Enemy.Hp);
            DamageSide(target, final);
            var result = Base("hit");
            result.Damage = final;
            result.Crit = crit;
            result.Effect = effect;
            if (shielded && element == shell.Element)
            {
                Shell--;
                result.ShellLeft = Shell;
            }
            return result;
        }

        Rewards RollRewards()
        {
            var m = Enemy.Def;
            var source = m.Boss ? "boss" : m.Elite != null ? "elite" : m.ChestLoot ? "chest" : "mob";
            var materials = Features.On(Player, "materials");
            var extras = Features.On(Player, "sigils");
            var xp = JsMath.Round(m.Xp * RewardScale * Cheats.Rates);
            double Gold() => JsMath.Round(rng.RandInt((int)m.Gold[0], (int)m.Gold[1]) * RewardScale * Cheats.Rates);
            if (m.Lucky)
            {
                // `{ xp, gold: gold(), items: ... }`: the gold roll comes before the Sigil roll.
                var luckyGold = Gold();
                var luckyItems = new List<ItemGrant>();
                if (m.Loot != null && rng.Chance(Cheats.Boosted(B.Lucky.SigilChance))) luckyItems.Add(Lucky.LuckySigil(m.Loot, rng));
                return new Rewards { Xp = xp, Gold = luckyGold, Items = luckyItems };
            }
            // Like the TS filter chain: every drop's chance is rolled first, then the items are granted.
            var allowed = m.Drops.Where(d => materials || Db.Item(d.ItemId).Type != "material" || Quests.QuestWantsItem(Player, d.ItemId)).ToList();
            var dropped = allowed.Where(d => rng.Chance(Cheats.Boosted(d.Chance))).ToList();
            var items = new List<ItemGrant>();
            foreach (var d in dropped)
                for (int i = 0; i < (d.Qty ?? 1); i++) items.Add(Items.GrantFor(d.ItemId, rng, source, extras));
            if (ZoneId != null) items.AddRange(Guild.HuntDrops(Player, ZoneId, rng));
            for (int i = 0; m.Loot != null && i < (m.IsHeroic ? B.Heroic.LootRolls : 1); i++)
            {
                var pity = Player.Data.LootPity;
                var gear = Loot.RollLoot(m.Loot, source, rng, pity.TryGetValue(m.Loot, out var pm) ? pm : 0, extras);
                if (gear?.Tier == "rare") pity.Remove(m.Loot);
                else pity[m.Loot] = (pity.TryGetValue(m.Loot, out var pv) ? pv : 0) + 1;
                if (gear != null) items.Add(gear.Grant);
                var set = Db.LootTable(m.Loot).Set;
                if (set != null && (source == "elite" || source == "boss") && rng.Chance(Cheats.Boosted(B.SetDrop[source])))
                {
                    var pieces = Db.Set(set).Pieces;
                    items.Add(Items.GrantFor(pieces[rng.RandInt(0, pieces.Count - 1)], rng, source, extras));
                }
            }
            // Skill books: Elites may drop their area's element books; the first Heroic win teaches a class skill.
            if (source == "elite" && m.Loot != null)
            {
                var book = Books.EliteBook(Player, m.Loot, rng);
                if (book != null) items.Add(new ItemGrant { ItemId = book });
            }
            var heroic = m.IsHeroic ? Books.HeroicBook(Player) : null;
            if (heroic != null) items.Add(new ItemGrant { ItemId = heroic });
            return new Rewards { Xp = xp, Gold = Gold(), Items = items };
        }

        static string Other(string side) => side == "player" ? "enemy" : "player";
    }
}
