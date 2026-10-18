using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Drakepg.Rules;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Play-mode helpers for a chat testing the game through the Unity MCP (execute_code), e.g.
    /// <c>Drakepg.EditorTools.Drive.NewGame("rogue", 2); Drakepg.EditorTools.Drive.Shot("C:/tmp/a.png", 0.5f);</c>
    /// They reach into screens by reflection, so nothing in the game exists only for tests.
    /// </summary>
    public static class Drive
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static GameScreen Screen => GameRoot.I != null ? GameRoot.I.Screen : null;

        /// <summary>A new hero at a chapter start (1: the prologue road, 2+: in town with fitting level and gear).</summary>
        public static string NewGame(string classId = "warrior", int chapter = 1, string name = "Tester")
        {
            var p = Player.CreateNew(name, classId);
            var start = Db.Data.ChapterStarts.Find(s => s.Chapter == chapter);
            if (start != null && chapter > 1) DevTools.ApplyChapterStart(p, start);
            Features.MarkSkillsSeen(p, chapter == 1);
            var at = (chapter == 1 ? Db.Data.Start : Db.Data.TownStart).Clone();
            p.Data.Location = at.Clone();
            Session.Start(p, true);
            GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(at.RoomId, (float)at.X, (float)at.Y, chapter == 1)), 0);
            return $"{classId} Lv {p.Level} in {at.RoomId}";
        }

        /// <summary>Go to a room (its middle, or x/y).</summary>
        public static void Room(string roomId, float x = 640, float y = -1)
        {
            var r = Db.Room(roomId);
            GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(roomId, x, y >= 0 ? y : (float)((r.Walk[0] + r.Walk[1]) / 2))), 0);
        }

        /// <summary>Start a fight with a monster in a zone (back to `roomId` after).</summary>
        public static void Fight(string monsterId, string zoneId, string roomId, string elite = null, bool heroic = false)
        {
            GameRoot.I.Go(new BattleScreen(new BattleSetup { MonsterId = monsterId, ZoneId = zoneId, RoomId = roomId, Elite = elite, Heroic = heroic, X = 640, Y = 600 }), 0);
        }

        /// <summary>Call a method of the current screen by name (private ones too), e.g. "ToggleInventory".</summary>
        public static object Call(string method, params object[] args) => Screen?.GetType().GetMethod(method, Any)?.Invoke(Screen, args);

        /// <summary>A field of the current screen.</summary>
        public static object Field(string name) => Screen?.GetType().GetField(name, Any)?.GetValue(Screen);

        /// <summary>Battle: press menu button `index` (0-5, like keys 1-6). False while a turn is playing.</summary>
        public static bool Press(int index)
        {
            if (!(Field("menuButtons") is IList buttons) || buttons.Count <= index) return false;
            var b = buttons[index];
            if (!(bool)b.GetType().GetField("Enabled").GetValue(b)) return false;
            ((System.Action)b.GetType().GetField("Run").GetValue(b))();
            return true;
        }

        /// <summary>Battle: keep attacking until the fight is over (then the result screen shows).</summary>
        public static void AutoFight() => GameRoot.I.StartCoroutine(AutoFightLoop());

        static IEnumerator AutoFightLoop()
        {
            for (int i = 0; i < 400 && Screen is BattleScreen; i++)
            {
                Press(0);
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>Close the top menu.</summary>
        public static void Close() => UiModal.Top?.Close();

        /// <summary>Save a picture of the game (world and UI) after `delay` seconds.</summary>
        public static void Shot(string path, float delay = 0) => GameRoot.I.StartCoroutine(ShotLater(path, delay));

        static IEnumerator ShotLater(string path, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            GameRoot.I.Snapshot(path);
        }

        /// <summary>
        /// Save `frames` pictures `1/fps` s of game time apart (Time.captureFramerate, so slow captures don't skip motion),
        /// `width` px wide, as dir/f000.jpg...; then press battle button `press` (-1: none) `pressAt` s in.
        /// </summary>
        public static void Record(string dir, int frames, int fps = 20, int width = 1280, int press = -1, float pressAt = 0) =>
            GameRoot.I.StartCoroutine(RecordLoop(dir, frames, fps, width, press, pressAt));

        static IEnumerator RecordLoop(string dir, int frames, int fps, int width, int press, float pressAt)
        {
            System.IO.Directory.CreateDirectory(dir);
            Time.captureFramerate = fps;
            var rt = new RenderTexture(width, width * 9 / 16, 0);
            var small = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            for (int i = 0; i < frames; i++)
            {
                if (press >= 0 && i == Mathf.RoundToInt(pressAt * fps)) Press(press);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                small.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"f{i:000}.jpg"), small.EncodeToJPG(92));
                Object.Destroy(tex);
            }
            Time.captureFramerate = 0;
            Object.Destroy(rt);
            Object.Destroy(small);
            Debug.Log($"drakepg: recorded {frames} frames to {dir}");
        }

        /// <summary>What's on screen: the screen, open menus, the battle state.</summary>
        public static string State()
        {
            var s = Screen;
            var parts = new List<string> { s?.GetType().Name ?? "none", $"menus={UiModal.OpenCount}" };
            if (Field("battle") is Battle b) parts.Add($"battle {b.State} you {b.Player.Hp}/{b.Player.MaxHp} foe {b.Enemy.Hp}/{b.Enemy.MaxHp}");
            if (Session.HasPlayer) parts.Add($"{Session.Player.Name} Lv {Session.Player.Level} {Session.Player.Gold} g at {Session.Player.Data.Location?.RoomId}");
            return string.Join("  ", parts);
        }
    }
}
