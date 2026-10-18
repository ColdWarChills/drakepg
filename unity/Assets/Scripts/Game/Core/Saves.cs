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

        // --- Picking a save file (Import on the title) ---

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void DrakepgPickerInit();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void DrakepgPickSaveFile(string objectName, string methodName);
#endif

        static System.Action<string> picked;

        /// <summary>The browser starts watching the pointer, so a pick knows whether the button is still held.</summary>
        public static void PreparePicker()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DrakepgPickerInit();
#endif
        }

        /// <summary>
        /// Ask for a save file and get its text. In the browser this is called on the press (Plugins/WebGL/SaveFile.jslib
        /// opens the picker on the release, or at once if the click is already over); the editor shows a file
        /// dialog; a desktop build reads the clipboard.
        /// </summary>
        public static void PickFile(System.Action<string> onText)
        {
            picked = onText;
#if UNITY_WEBGL && !UNITY_EDITOR
            DrakepgPickSaveFile(GameRoot.I.gameObject.name, nameof(GameRoot.OnSaveFile));
#elif UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Import a drakepg save", "", "json,txt");
            if (!string.IsNullOrEmpty(path)) Picked(System.IO.File.ReadAllText(path));
#else
            Picked(GUIUtility.systemCopyBuffer);
#endif
        }

        /// <summary>The picked file's text (GameRoot.OnSaveFile passes it on from the browser).</summary>
        public static void Picked(string text)
        {
            var done = picked;
            picked = null;
            done?.Invoke(text);
        }

        /// <summary>The browser opens its picker on a release, the editor and desktop on a click.</summary>
        public static bool PickOnPress => Application.platform == RuntimePlatform.WebGLPlayer;
    }
}
