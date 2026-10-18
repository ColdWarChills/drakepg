using UnityEngine;

namespace Drakepg
{
    public static partial class Sfx
    {
        /// <summary>Every sound in the recipes, for warming up.</summary>
        static readonly string[] AllNames =
        {
            "click", "open", "slash", "hit", "crit", "miss", "charge", "fireball", "explode", "heal", "coin", "levelup", "step", "alert", "encounter",
            "victory", "defeat", "quest", "warning", "denied", "equip", "rare", "sigil", "eclipse", "recall", "omen", "page", "whoosh", "block",
            "debuff", "stun", "burn", "freeze", "splash", "gust", "quake", "zap", "holy", "void", "turn", "swish", "glint", "splat", "thud",
        };

        static int warmName, warmClip;

        /// <summary>
        /// Renders one clip that hasn't been made yet and returns true, or false once all are ready. GameRoot calls it
        /// once a frame after boot, so a sound's first play (mid-fight) never stalls a frame.
        /// </summary>
        public static bool WarmUpStep()
        {
            if (!Application.isPlaying) return false;
            while (warmName < AllNames.Length)
            {
                var name = AllNames[warmName];
                if (!sounds.TryGetValue(name, out var sound))
                {
                    sound = Describe(name);
                    sounds[name] = sound;
                }
                if (sound == null || warmClip >= sound.Clips.Length)
                {
                    warmName++;
                    warmClip = 0;
                    continue;
                }
                var k = warmClip++;
                if (sound.Clips[k] != null) continue;
                double v = sound.Drifts ? 1 + Drift * ((k + 0.5) / DriftSteps - 0.5) : 1;
                sound.Clips[k] = Render(sound.Drifts ? name + "_" + k : name, name, v);
                return true;
            }
            return false;
        }
    }
}
