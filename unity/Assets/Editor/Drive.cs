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

        /// <summary>Give the hero a fresh piece of gear and put it on (a weapon goes in the hand).</summary>
        public static string Wield(string itemId)
        {
            var p = Session.Player;
            var gear = p.MakeGear(itemId);
            p.Data.Gear.Add(gear);
            return p.Equip(gear.Uid) ? $"wielding {itemId}" : $"could not equip {itemId}";
        }

        static GameObject poseSheet;

        /// <summary>
        /// Line up the hero (`classId`'s look) holding `weaponId` in each of `poses` ("sword_wind,sword_step", "" for
        /// standing) across the current screen, to check a pose's hands. `hands` overrides a pose's first hand for the
        /// test: "pose=x,y,angle;pose2=..." (1x px). Call again to redo; Close it with an empty pose list.
        /// </summary>
        public static string PoseSheet(string classId, string weaponId, string poses, string hands = "", string outfit = null)
        {
            if (poseSheet != null) Object.Destroy(poseSheet);
            if (string.IsNullOrEmpty(poses)) return "cleared";
            var look = Db.Class(classId).Look;
            if (outfit != null)
            {
                // In an armor's outfit, as Player.Look dresses the hero (same hands and poses, the outfit's art).
                look = look.Clone();
                var b = look.Bare[0];
                look.Bare = new List<BareLook> { new BareLook { Art = "char_hero_" + outfit, Hands = b.Hands, Poses = b.Poses } };
            }
            var bare = look.Bare[0];
            foreach (var part in hands.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || !bare.Poses.TryGetValue(kv[0], out var list)) continue;
                var n = kv[1].Split(',');
                var c = System.Globalization.CultureInfo.InvariantCulture;
                int i = n.Length > 3 ? int.Parse(n[3]) : 0;
                list[i].X = double.Parse(n[0], c);
                list[i].Y = double.Parse(n[1], c);
                list[i].Angle = double.Parse(n[2], c);
            }
            poseSheet = new GameObject("pose sheet");
            poseSheet.transform.SetParent(Screen.World, false);
            // A plain backdrop so the weapons read.
            var plate = new GameObject("plate").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(poseSheet.transform, false);
            plate.sprite = Art.White;
            plate.color = new Color(0.9f, 0.88f, 0.83f);
            plate.sortingOrder = 29000;
            plate.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
            plate.transform.localScale = new Vector3(Design.Width * 2, Design.Height * 2, 1);
            var names = poses.Split(',');
            var weapon = Db.Item(weaponId);
            for (int k = 0; k < names.Length; k++)
            {
                var x = (k + 0.5f) * Design.Width / names.Length;
                var fig = Figure.Create(poseSheet.transform, x, 560, look, 1, weapon);
                fig.Order = 30000;
                if (names[k] != "") fig.SetPose(names[k]);
            }
            return $"{names.Length} figures";
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

        /// <summary>
        /// Battle: press buttons at game times from now, e.g. "1@0.5 0@0.9 0@4" (Skills, its first skill, then the
        /// first button again 4 s in). Pairs with Record for clips of whole turns.
        /// </summary>
        public static void Presses(string steps) => GameRoot.I.StartCoroutine(PressLoop(steps));

        static IEnumerator PressLoop(string steps)
        {
            float now = 0;
            foreach (var step in steps.Split(' '))
            {
                var parts = step.Split('@');
                if (parts.Length != 2) continue;
                var at = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                if (at > now) yield return new WaitForSeconds(at - now);
                now = Mathf.Max(now, at);
                Press(int.Parse(parts[0]));
            }
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

        /// <summary>
        /// Visit rooms in turn (space-separated ids) and save a picture of each as dir/&lt;room&gt;.png, `wait` s after
        /// arriving (open menus are closed first). Mark the rooms visited beforehand to skip their first-visit scenes.
        /// </summary>
        public static void Tour(string rooms, string dir, float wait = 1.6f) =>
            GameRoot.I.StartCoroutine(TourLoop(rooms.Split(' '), dir, wait));

        static IEnumerator TourLoop(string[] rooms, string dir, float wait)
        {
            System.IO.Directory.CreateDirectory(dir);
            foreach (var id in rooms)
            {
                if (string.IsNullOrEmpty(id)) continue;
                while (UiModal.OpenCount > 0) Close();
                Room(id);
                yield return new WaitForSeconds(wait);
                while (UiModal.OpenCount > 0)
                {
                    Close();
                    yield return new WaitForSeconds(0.4f);
                }
                GameRoot.I.Snapshot(System.IO.Path.Combine(dir, id + ".png"));
                yield return new WaitForSeconds(0.4f);
            }
            Debug.Log($"drakepg: tour done, {rooms.Length} rooms in {dir}");
        }

        /// <summary>
        /// Fight each "monster:zone:room" in turn at full HP, attacking until it ends; a picture 2.5 s in goes to
        /// dir/&lt;monster&gt;.png. Logs each outcome.
        /// </summary>
        public static void Battles(string spec, string dir) => GameRoot.I.StartCoroutine(BattleLoop(spec.Split(' '), dir));

        static IEnumerator BattleLoop(string[] fights, string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var results = new List<string>();
            foreach (var f in fights)
            {
                var parts = f.Split(':');
                if (parts.Length != 3) continue;
                while (UiModal.OpenCount > 0) Close();
                Session.Player.RestoreFull();
                Fight(parts[0], parts[1], parts[2]);
                yield return new WaitForSeconds(2.5f);
                GameRoot.I.Snapshot(System.IO.Path.Combine(dir, parts[0] + ".png"));
                yield return new WaitForSeconds(0.4f);
                string outcome = null;
                for (int i = 0; i < 600 && Screen is BattleScreen; i++)
                {
                    if (Field("battle") is Battle b) outcome = b.State;
                    Press(0);
                    yield return new WaitForSeconds(0.25f);
                }
                results.Add($"{parts[0]}={outcome}");
                yield return new WaitForSeconds(1.5f);
            }
            Debug.Log("drakepg: battles " + string.Join(", ", results));
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
