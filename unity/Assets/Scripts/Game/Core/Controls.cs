using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Drakepg
{
    /// <summary>
    /// The game's keys, each changeable in Options (Ui/OptionsPanel.cs) and remembered on this device (Saves). Screens ask
    /// for an action ("map"), never a key. Some keys always work and can't be taken: Enter (confirm), Esc (back), Space
    /// (talk, go on), the arrows (walk), ` (cheats) and F12 (a picture).
    /// </summary>
    public static class Controls
    {
        /// <summary>Where an action is used. Two actions may share a key only if they are never used in the same place.</summary>
        [Flags]
        public enum Where { Room = 1, Battle = 2 }

        public class Action
        {
            public readonly string Id, Name;
            public readonly Where Where;
            public readonly Key Default;

            public Action(string id, string name, Where where, Key key)
            {
                Id = id;
                Name = name;
                Where = where;
                Default = key;
            }
        }

        public static readonly Action[] All =
        {
            new Action("up", "Walk up", Where.Room, Key.W),
            new Action("down", "Walk down", Where.Room, Key.S),
            new Action("left", "Walk left", Where.Room, Key.A),
            new Action("right", "Walk right", Where.Room, Key.D),
            new Action("talk", "Talk, go on", Where.Room | Where.Battle, Key.E),
            new Action("character", "Character", Where.Room, Key.C),
            new Action("bag", "Inventory", Where.Room, Key.I),
            new Action("map", "Map", Where.Room, Key.M),
            new Action("spellbook", "Spellbook", Where.Room, Key.K),
            new Action("act1", "Button 1", Where.Battle, Key.Digit1),
            new Action("act2", "Button 2", Where.Battle, Key.Digit2),
            new Action("act3", "Button 3", Where.Battle, Key.Digit3),
            new Action("act4", "Button 4", Where.Battle, Key.Digit4),
            new Action("act5", "Button 5", Where.Battle, Key.Digit5),
            new Action("act6", "Button 6", Where.Battle, Key.Digit6),
            new Action("speed", "Battle speed", Where.Battle, Key.S),
        };

        static readonly Dictionary<string, Key> keys = new Dictionary<string, Key>();

        static Action Find(string id) => All.FirstOrDefault(a => a.Id == id) ?? throw new ArgumentException("No control " + id);

        public static Key KeyOf(string id) => keys.TryGetValue(id, out var k) ? k : Find(id).Default;

        public static bool Pressed(string id) => Keys.Pressed(KeyOf(id));
        public static bool Held(string id) => Keys.Held(KeyOf(id));

        /// <summary>The key's name as printed on the keyboard (the layout's letter), for the Options page and hints.</summary>
        public static string Name(Key key)
        {
            if (key == Key.Space) return "Space";
            if (key == Key.Tab) return "Tab";
            if (key == Key.LeftShift) return "Left Shift";
            if (key == Key.RightShift) return "Right Shift";
            // The number row: the digit printed on the cap, not what the layout types (Lithuanian gives Ą Č Ę...).
            if (key >= Key.Digit1 && key <= Key.Digit9) return (key - Key.Digit1 + 1).ToString();
            if (key == Key.Digit0) return "0";
            if (key >= Key.Numpad0 && key <= Key.Numpad9) return "Num " + (key - Key.Numpad0);
            if (key >= Key.F1 && key <= Key.F12) return "F" + (key - Key.F1 + 1);
            string n = null;
            try { n = Keyboard.current?[key]?.displayName; }
            catch (Exception) { }
            if (string.IsNullOrEmpty(n)) n = key.ToString();
            return n.Length == 1 ? n.ToUpperInvariant() : n;
        }

        /// <summary>The action's key, as printed ("E").</summary>
        public static string Label(string id) => Name(KeyOf(id));

        /// <summary>At most three letters, for the small key dot on a battle button.</summary>
        public static string Short(string id)
        {
            var key = KeyOf(id);
            if (key == Key.LeftShift || key == Key.RightShift) return "Shf";
            if (key >= Key.Numpad0 && key <= Key.Numpad9) return "N" + (key - Key.Numpad0);
            var n = Name(key);
            return n.Length <= 3 ? n : n.Substring(0, 3);
        }

        /// <summary>
        /// Keys an action can take: letters, digits (also on the numpad), F1-F11, punctuation, Tab and Shift. Not the
        /// keys that always work, nor Ctrl, Alt or the system keys (a browser takes Ctrl+W and Alt for itself).
        /// </summary>
        public static bool Allowed(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return true;
            if (key >= Key.Digit1 && key <= Key.Digit0) return true;
            if (key >= Key.Numpad0 && key <= Key.Numpad9) return true;
            if (key >= Key.F1 && key <= Key.F11) return true;
            switch (key)
            {
                case Key.Tab:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.Comma:
                case Key.Period:
                case Key.Slash:
                case Key.Semicolon:
                case Key.Quote:
                case Key.LeftBracket:
                case Key.RightBracket:
                case Key.Minus:
                case Key.Equals:
                case Key.Backslash:
                    return true;
            }
            return false;
        }

        /// <summary>Why a key can't be taken, in a few words, or null when it can.</summary>
        public static string WhyNot(Key key)
        {
            if (Allowed(key)) return null;
            switch (key)
            {
                case Key.Space: return "Space always talks and goes on.";
                case Key.Enter:
                case Key.NumpadEnter: return "Enter always confirms.";
                case Key.UpArrow:
                case Key.DownArrow:
                case Key.LeftArrow:
                case Key.RightArrow: return "The arrows always walk.";
                case Key.Backquote: return "` opens the cheats.";
            }
            return Name(key) + " can't be used.";
        }

        static bool Clash(Action a, Action b) => a != b && (a.Where & b.Where) != 0;

        /// <summary>
        /// Give `id` the key. An action in the same place that had it takes this one's old key (a swap). Returns the
        /// names of the actions that moved, or null if it couldn't be done without two actions sharing a key.
        /// </summary>
        public static List<string> Bind(string id, Key key)
        {
            var me = Find(id);
            var old = KeyOf(id);
            if (old == key) return new List<string>();
            var moved = All.Where(a => Clash(a, me) && KeyOf(a.Id) == key).ToList();
            var before = new Dictionary<string, Key>(keys);
            keys[id] = key;
            foreach (var a in moved) keys[a.Id] = old;
            if (!Valid())
            {
                keys.Clear();
                foreach (var kv in before) keys[kv.Key] = kv.Value;
                return null;
            }
            Save();
            return moved.Select(a => a.Name).ToList();
        }

        public static void Reset()
        {
            keys.Clear();
            Save();
        }

        public static bool IsDefault => All.All(a => KeyOf(a.Id) == a.Default);

        /// <summary>No two actions used in the same place share a key, and every key can be taken.</summary>
        static bool Valid()
        {
            foreach (var a in All)
            {
                if (!Allowed(KeyOf(a.Id))) return false;
                foreach (var b in All)
                    if (Clash(a, b) && KeyOf(a.Id) == KeyOf(b.Id)) return false;
            }
            return true;
        }

        const string Pref = "drakepg.keys";

        /// <summary>Read the saved keys ("map=Tab;talk=F"); anything unreadable, or a set that clashes, is the defaults.</summary>
        public static void Load()
        {
            keys.Clear();
            var text = PlayerPrefs.GetString(Pref, "");
            foreach (var part in text.Split(';'))
            {
                var bits = part.Split('=');
                if (bits.Length != 2) continue;
                if (All.Any(a => a.Id == bits[0]) && Enum.TryParse(bits[1], out Key k) && Enum.IsDefined(typeof(Key), k)) keys[bits[0]] = k;
            }
            if (!Valid())
            {
                Debug.LogWarning("drakepg: saved keys clash or can't be used; back to the defaults.");
                keys.Clear();
            }
        }

        static void Save()
        {
            var changed = All.Where(a => KeyOf(a.Id) != a.Default).Select(a => a.Id + "=" + KeyOf(a.Id));
            PlayerPrefs.SetString(Pref, string.Join(";", changed));
            PlayerPrefs.Save();
        }
    }
}
