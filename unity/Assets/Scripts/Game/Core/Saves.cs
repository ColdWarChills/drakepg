using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// Where saves and settings live (SaveSystem.ts, settings.ts): PlayerPrefs, which a web build keeps in the browser's
    /// IndexedDB. The text is the web format (Rules.SaveFile), so a save exported from the Phaser game can be imported.
    /// </summary>
    public static class Saves
    {
        const string Key = "drakepg.save";
        const string StoryKey = "drakepg.storyText";
        const string MuteKey = "drakepg.muted";

        /// <summary>Show the full story dialogue for quests (off: one short line).</summary>
        public static bool StoryText;
        public static bool Muted;

        public static void Init()
        {
            StoryText = PlayerPrefs.GetInt(StoryKey, 0) == 1;
            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        }

        public static void SaveSettings()
        {
            PlayerPrefs.SetInt(StoryKey, StoryText ? 1 : 0);
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool HasSave => PlayerPrefs.HasKey(Key);

        public static bool Save(Player player)
        {
            try
            {
                PlayerPrefs.SetString(Key, SaveFile.Write(player));
                PlayerPrefs.Save();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                return false;
            }
        }

        public static Player Load() => HasSave ? SaveFile.Read(PlayerPrefs.GetString(Key)) : null;

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>The save as text (to copy out), or null.</summary>
        public static string Export() => HasSave ? PlayerPrefs.GetString(Key) : null;

        /// <summary>Replace the save with pasted text (e.g. from the web version). False if it can't be read.</summary>
        public static bool Import(string text)
        {
            var p = SaveFile.Read(text);
            if (p == null) return false;
            return Save(p);
        }
    }
}
