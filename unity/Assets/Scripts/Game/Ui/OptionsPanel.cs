using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Drakepg
{
    /// <summary>
    /// Options (from the room's Menu and the title): every game key can be changed (Controls), and the screen and sound
    /// settings (Saves): fullscreen, screen shake, flashes, sound volume, battle speed, story text. All kept on this
    /// device, not in the save.
    /// </summary>
    public static class OptionsPanel
    {
        const float W = 1160, H = 640;
        static readonly Color Picked = Design.Hex(0x6b5a2e);
        static readonly Color Waiting = Design.Hex(0x8a3a2a);

        public static UiModal Open(Action onClose = null)
        {
            string waiting = null;
            bool? fullscreen = null;
            var modal = new UiModal(W, H, "Options", () =>
            {
                Keys.StopCatching();
                onClose?.Invoke();
            });
            var c = modal.Content;

            void Caption(float x, float y, string text, float size = 18) => Ui.Text(c, x, y, text, size, Palette.TextDim, true, 0, 0.5f);

            void Choices<T>(float x, float y, string title, IList<(string label, T value)> options, T current, Action<T> pick, float w)
            {
                Caption(x, y, title);
                for (int i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    var picked = EqualityComparer<T>.Default.Equals(o.value, current);
                    new UiButton(c, x + w / 2 + i * (w + 8), y + 34, o.label, () =>
                    {
                        pick(o.value);
                        Saves.SaveSettings();
                        Render();
                    }, w, 38, 17, color: picked ? Picked : (Color?)null);
                }
            }

            void KeyRow(float x, float y, Controls.Action a)
            {
                Ui.Text(c, x, y, a.Name, 19, Palette.Text, false, 0, 0.5f);
                var catching = waiting == a.Id;
                new UiButton(c, x + 200, y, catching ? "Press a key" : Controls.Label(a.Id), () =>
                {
                    waiting = a.Id;
                    Keys.Catch(key =>
                    {
                        waiting = null;
                        if (key != Key.Escape)
                        {
                            var why = Controls.WhyNot(key);
                            if (why != null) Ui.Toast(why, Palette.TextBad);
                            else
                            {
                                // Whatever had the key here takes this one's old key.
                                var old = Controls.KeyOf(a.Id);
                                var moved = Controls.Bind(a.Id, key);
                                if (moved == null) Ui.Toast($"{Controls.Name(key)} is taken here.", Palette.TextBad);
                                else if (moved.Count > 0) Ui.Toast($"{string.Join(", ", moved)}: now {Controls.Name(old)}", Palette.TextGood);
                            }
                        }
                        if (!modal.Closed) Render();
                    });
                    Render();
                }, 110, 38, catching ? 15 : 18, color: catching ? Waiting : (Color?)null);
            }

            void Render()
            {
                c.Clear();
                // Keys, in two lists: walking around and in battle.
                Ui.Text(c, 40, 82, "Keys", 24, Palette.Text, true, 0, 0.5f);
                Caption(40, 118, "Around the world", 16);
                Caption(380, 118, "In battle", 16);
                var room = new List<Controls.Action>();
                var battle = new List<Controls.Action>();
                foreach (var a in Controls.All)
                {
                    if ((a.Where & Controls.Where.Room) != 0) room.Add(a);
                    else battle.Add(a);
                }
                for (int i = 0; i < room.Count; i++) KeyRow(40, 156 + i * 46, room[i]);
                for (int i = 0; i < battle.Count; i++) KeyRow(380, 156 + i * 46, battle[i]);
                var reset = new UiButton(c, 120, 586, "Reset keys", () =>
                {
                    Keys.StopCatching();
                    waiting = null;
                    Controls.Reset();
                    Render();
                }, 160, 40, 17);
                reset.SetEnabled(!Controls.IsDefault);
                Ui.Text(c, 220, 586, "Esc, Enter, Space and the arrows always work.", 15, Palette.TextDim, false, 0, 0.5f);

                // Screen and sound.
                const float x = 760;
                Ui.Text(c, x, 82, "Screen and sound", 24, Palette.Text, true, 0, 0.5f);
                // Unity switches fullscreen at the end of the frame, so the page shows the pick, not what Screen says yet.
                Choices(x, 124, "Fullscreen", new List<(string, bool)> { ("On", true), ("Off", false) }, fullscreen ?? UnityEngine.Screen.fullScreen,
                    v => UnityEngine.Screen.fullScreen = (fullscreen = v).Value, 80);
                Choices(x, 204, "Screen shake", new List<(string, int)> { ("Full", 0), ("Low", 1), ("Off", 2) }, Saves.Shake, v => Saves.Shake = v, 80);
                Choices(x, 284, "Flashes", new List<(string, int)> { ("Full", 0), ("Soft", 1), ("Off", 2) }, Saves.Flashes, v => Saves.Flashes = v, 80);
                Choices(x, 364, "Sound", new List<(string, int)> { ("Off", 0), ("25%", 25), ("50%", 50), ("75%", 75), ("100%", 100) },
                    Saves.Muted ? 0 : Saves.Volume, v =>
                    {
                        Saves.Muted = v == 0;
                        if (v > 0) Saves.Volume = v;
                        if (v > 0) Sfx.Play("click");
                    }, 64);
                Choices(x, 444, "Battle speed", new List<(string, int)> { ("x1", 1), ("x2", 2) }, Saves.BattleSpeed, v => Saves.BattleSpeed = v, 80);
                Choices(x, 524, "Story text", new List<(string, bool)> { ("On", true), ("Off", false) }, Saves.StoryText, v => Saves.StoryText = v, 80);
            }

            Render();
            return modal;
        }
    }
}
