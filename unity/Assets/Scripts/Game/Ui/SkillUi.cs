using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// How skills are drawn in menus (spellbook, Character panel, battle buttons). Skills without their own drawing
    /// (element families, Wards, generic books) borrow a related one, tinted with their element's color.
    /// </summary>
    public static class SkillUi
    {
        /// <summary>The element a skill sits under in the spellbook: its own, a Ward's, else null (weapon or neutral).</summary>
        public static string ElementOf(SkillDef s)
        {
            if (s.Element != null && s.Element != "weapon") return s.Element;
            foreach (var e in Db.Data.Elements) if (s.Id == e + "_ward") return e;
            return null;
        }

        /// <summary>The skill's drawing: its own (scripts/ui_icons.py), else its family's or a related one, else one by kind.</summary>
        public static string Icon(SkillDef s)
        {
            if (Felt.HasIcon("skill_" + s.Id)) return "skill_" + s.Id;
            var borrowed = s.Family switch
            {
                "strike" => "skill_heavy_strike",
                "knives" => "skill_fan_of_knives",
                "bolt" => "skill_ember_bolt",
                _ => s.Id.EndsWith("_ward") ? "shield"
                    : s.Id switch { "field_dressing" => "skill_bandage", "play_dead" => "skill_smoke_bomb", "meditate" => "spark", _ => null },
            };
            if (borrowed != null && Felt.HasIcon(borrowed)) return borrowed;
            return s.Kind == "heal" ? "cross" : s.Kind == "buff" ? "rise" : s.DamageType == "magic" ? "spark" : s.DamageType == "pierce" ? "arrow" : "sword";
        }

        /// <summary>Tint for a borrowed drawing (an element's color), or null when the skill has its own.</summary>
        public static Color? IconTint(SkillDef s)
        {
            if (Felt.HasIcon("skill_" + s.Id)) return null;
            var e = ElementOf(s);
            return e != null ? ElementColor(e) : (Color?)null;
        }

        public static Color ElementColor(string element) =>
            Design.Hex(Db.Data.ElementColors.TryGetValue(element, out var c) ? c[1] : Db.Data.NeutralColors[1]);

        /// <summary>A status chip's drawing: its own, else a related one (Freeze and Thawed: Chill, Wards: the shield).</summary>
        public static string StatusIcon(StatusDef d)
        {
            if (Felt.HasIcon("status_" + d.Id)) return "status_" + d.Id;
            if (d.Freeze || d.Id == "thawed") return "status_chill";
            if (d.Id.EndsWith("_ward")) return "shield";
            return null;
        }

        /// <summary>One short word for what a skill is: its element, Blunt, Poison, Heal, Buff, MP or Flee.</summary>
        public static string KindWord(SkillDef s)
        {
            var element = s.Element != null && s.Element != "weapon" ? Db.Data.ElementLabels[s.Element] : null;
            var poisons = s.Applies?.Exists(a => a.Status == "poison" && !a.Self) == true;
            return element ?? (s.Blunt ? "Blunt" : poisons ? "Poison" : s.Kind switch
            {
                "heal" => "Heal",
                "buff" => "Buff",
                "mana" => "Recover MP",
                "flee" => "Flee",
                _ => null,
            });
        }
    }
}
