#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Drakepg.Bones;
using Drakepg.Rules;
using UnityEngine;

namespace Drakepg.Test
{
    /// <summary>
    /// TEST, editor only: a fight stage to judge moves of the hero on bones (unity/Bones/HOWTO.md). Its own screen in
    /// the running game (GameRoot, camera, Anim): a real battle background, a monster from the game where battle
    /// monsters stand, the hero on bones where the battle hero stands at the drawn hero's size. No HUD, no labels.
    /// On "contact" the monster takes the hit like the battle's Impact (BattleScreen.cs ~2694-2822): the game's own
    /// Fx.WhiteFlash, Fx.HitStop, GameRoot.Shake/Kick, Fx.Slash/HitSparks/Bleed/Dust, Sfx; the battle's private
    /// knock-back, tilt, squash and lean numbers are mirrored here (the Fighter class is private).
    /// <code>
    /// Drakepg.Test.BoneStage.Open("iron_sword", "bog_lurker");   // wait ~0.4 s (screen change)
    /// Drakepg.Test.BoneStage.Play("sword_swing_old");             // reads moves/sword_swing_old.json fresh
    /// Drakepg.Test.BoneStage.Hold("sword_swing_old", 440);        // the pose at 440 ms, still
    /// Drakepg.Test.BoneStage.Speed(2); Drakepg.Test.BoneStage.Close();
    /// </code>
    /// </summary>
    public static class BoneStage
    {
        // The battle's layout (BattleScreen: GroundY, PlayerHome, EnemyHome, ChestHome, BgRaise; mirrored, private there).
        public const float GroundY = 500, PlayerX = 330, EnemyX = 950, Chest = 120, BgRaise = 90;
        /// <summary>The hero on bones' scale against the warrior look's scale: sized so its ready stance is as tall as the
        /// drawn hero's in a real fight (240 px at 1280 wide; measured, see the stage sheet).</summary>
        public static float SizeFactor = 0.89f;
        /// <summary>The weight of the stage's hits (damage / max HP; the baseline's 28 of 195 is 0.14).</summary>
        public static float HitWeight = 0.15f;

        public static BoneDiskSource Source;
        public static BoneHero Hero => screen?.Hero;
        public static Figure Monster => screen?.Monster;
        public static bool Ready => screen != null && screen.Hero != null && GameRoot.I.Screen == screen;
        public static BoneMove LastMove;
        static StageScreen screen;
        static float speedWas = 1;

        /// <summary>The base part set the stage's hero wears (swaps.json "sets"); null = the first bare set.</summary>
        public const string DefaultBaseSet = "bare_v2";

        public static string Open(string weapon = "iron_sword", string monster = "bog_lurker", string zone = "fen", string baseSet = DefaultBaseSet)
        {
            Source = new BoneDiskSource();
            BoneRig.Forget();
            speedWas = Anim.Speed;
            screen = new StageScreen(weapon, monster, zone, baseSet);
            GameRoot.I.Go(screen, 0);
            return $"opening the stage: {weapon} against {monster} in {zone}, parts {baseSet ?? "bare"}";
        }

        public static BoneMove Load(string name)
        {
            if (Source == null) Source = new BoneDiskSource();
            var m = Source.Move(name, Hero != null ? Hero.Rig : BoneRig.Of(Source));
            LastMove = m;
            return m;
        }

        /// <summary>Play a move toward the monster (read fresh from disk). Returns its length and any warnings.</summary>
        public static string Play(string name)
        {
            if (!Ready) return "stage not open";
            var m = Load(name);
            if (m == null) return "no move " + name;
            screen.Home();
            _ = Hero.Play(m, new Vector2(EnemyX, GroundY), screen.Events);
            return $"{m.Name}: {m.Length:0} ms at x1, {m.Length / Mathf.Max(0.01f, Anim.Speed):0} ms at x{Anim.Speed:0.##} (plus any hit pause)"
                + (m.Warnings.Count > 0 ? "; warnings: " + string.Join(" | ", m.Warnings) : "");
        }

        /// <summary>The move's pose at `ms` (x1), held still.</summary>
        public static string Hold(string name, float ms)
        {
            if (!Ready) return "stage not open";
            var m = Load(name);
            if (m == null) return "no move " + name;
            screen.Home();
            Hero.Target = new Vector2(EnemyX, GroundY);
            Hero.Sample(m, ms);
            return $"{m.Name} at {ms} ms";
        }

        /// <summary>
        /// Still poses as pictures, several moves in one go: `spec` = "move@ms[=label] move@ms ..." (dir/f000.jpg...,
        /// track.json with each still's label as its event and the hero box), for `bones_sheets.py strip DIR --every 1`
        /// or panels. For trying key poses before recording a take.
        /// </summary>
        public static string Stills(string spec, string dir)
        {
            if (!Ready) return "stage not open";
            if (Busy) return "busy";
            var list = new List<string[]>();
            foreach (var s in spec.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int at = s.IndexOf('@');
                if (at < 0) continue;
                string rest = s.Substring(at + 1);
                int eq = rest.IndexOf('=');
                list.Add(new[] { s.Substring(0, at), eq < 0 ? rest : rest.Substring(0, eq), eq < 0 ? s : rest.Substring(eq + 1) });
            }
            Busy = true;
            GameRoot.I.StartCoroutine(StillsLoop(list, dir));
            return list.Count + " stills to " + dir;
        }

        static IEnumerator StillsLoop(List<string[]> list, string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f*.jpg")) File.Delete(f);
            BonesTrack.Begin(Path.Combine(dir, "track.json"), 1, 1280, 720);
            var rt = new RenderTexture(1280, 720, 0);
            var small = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            for (int i = 0; i < list.Count; i++)
            {
                float ms = float.Parse(list[i][1], System.Globalization.CultureInfo.InvariantCulture);
                Hold(list[i][0], ms);
                BonesTrack.Event(list[i][2]);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                small.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"f{i:000}.jpg"), small.EncodeToJPG(92));
                Object.Destroy(tex);
                if (Hero != null) BonesTrack.Box("hero", BoundsOf(Hero.gameObject));
                BonesTrack.Next(i);
            }
            BonesTrack.End();
            Object.Destroy(rt);
            Object.Destroy(small);
            Busy = false;
        }

        /// <summary>The sword hand: old (false) or the grip hands (BoneHero.NewHands), with the elbow's fold cap (deg).</summary>
        public static string Hands(bool grip, float foldCap = 30)
        {
            if (!Ready) return "stage not open";
            Hero.NewHands = grip;
            Hero.FoldCap = foldCap;
            Hero.Reload();
            Hero.Rest();
            return grip ? $"grip hands, elbow folds at most {foldCap} deg more" : "old hand";
        }

        /// <summary>
        /// Still poses as close-ups, like Stills, the camera zoomed `zoom` x on the near weapon's grip (the same point
        /// for the old and the grip hands): dir/f000.jpg... square, `size` px; labels.txt one label per line. The camera
        /// is put back after.
        /// </summary>
        public static string Close(string spec, string dir, float zoom = 5, int size = 720)
        {
            if (!Ready) return "stage not open";
            if (Busy) return "busy";
            var list = new List<string[]>();
            foreach (var s in spec.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int at = s.IndexOf('@');
                if (at < 0) continue;
                string rest = s.Substring(at + 1);
                int eq = rest.IndexOf('=');
                list.Add(new[] { s.Substring(0, at), eq < 0 ? rest : rest.Substring(0, eq), eq < 0 ? s : rest.Substring(eq + 1) });
            }
            Busy = true;
            GameRoot.I.StartCoroutine(CloseLoop(list, dir, zoom, size));
            return list.Count + " close-ups to " + dir;
        }

        static IEnumerator CloseLoop(List<string[]> list, string dir, float zoom, int size)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f*.jpg")) File.Delete(f);
            var labels = new StringBuilder();
            var out1 = new Texture2D(size, size, TextureFormat.RGB24, false);
            var rt = new RenderTexture(size, size, 0);
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    float ms = float.Parse(list[i][1], System.Globalization.CultureInfo.InvariantCulture);
                    Hold(list[i][0], ms);
                    var w = BonesFind(Hero.transform, "near weapon");
                    var at = Design.FromWorld(w != null && w.GetComponent<SpriteRenderer>().enabled ? w.position : BonesFind(Hero.transform, "n_hand").position);
                    GameRoot.I.View(at.x, at.y, zoom, 0.0001f);
                    yield return null;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    int side = Mathf.Min(tex.width, tex.height);
                    // the middle square of the screen, scaled to `size`
                    var sq = new Texture2D(side, side, TextureFormat.RGB24, false);
                    sq.SetPixels(tex.GetPixels((tex.width - side) / 2, (tex.height - side) / 2, side, side));
                    sq.Apply();
                    Graphics.Blit(sq, rt);
                    RenderTexture.active = rt;
                    out1.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    out1.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"f{i:000}.jpg"), out1.EncodeToJPG(92));
                    Object.Destroy(tex);
                    Object.Destroy(sq);
                    var fr = Hero.Frame;
                    labels.AppendLine($"{list[i][2]}\t{list[i][0]}@{list[i][1]}\thand={(fr.HandPic[0] ?? "rig")}\trel={fr.Relation[0]:0}\tgap={fr.WristGap[0]:0.0}");
                }
            }
            finally
            {
                GameRoot.I.ResetView();
                File.WriteAllText(Path.Combine(dir, "labels.txt"), labels.ToString());
                Object.Destroy(rt);
                Object.Destroy(out1);
                Busy = false;
            }
        }

        /// <summary>
        /// Per sample every `step` ms of a move: the near grip, blade tip and wrist (canvas px), the hand shown, the
        /// blade-to-forearm angle and the forearm's gap to the hand's wrist; tab separated, for comparing hand styles.
        /// </summary>
        public static string Path2(string move, float step = 5)
        {
            if (!Ready) return "stage not open";
            var m = Load(move);
            var sb = new StringBuilder();
            for (float t = 0; t <= m.Length + 0.01f; t += step)
            {
                Hero.Sample(m, t);
                var f = Hero.Frame;
                Hero.Blade(f, 0, out var tip, out _);
                sb.AppendLine($"{t:0}\t{f.Grip[0].x:0.00}\t{f.Grip[0].y:0.00}\t{tip.x:0.00}\t{tip.y:0.00}\t{f.At["n_hand"].x:0.0}\t{f.At["n_hand"].y:0.0}\t{f.At["n_fore"].x:0.0}\t{f.At["n_fore"].y:0.0}\t{f.HandPic[0] ?? "rig"}\t{f.Relation[0]:0}\t{f.WristGap[0]:0.0}");
            }
            Hero.Rest();
            return sb.ToString();
        }

        /// <summary>The battle speed option for the stage only (Anim.Speed; the saved setting is not touched).</summary>
        public static string Speed(float x)
        {
            Anim.Speed = x;
            return "speed x" + x;
        }

        public static string Close()
        {
            screen = null;
            GameRoot.I.Go(new TitleScreen(), 0);
            Anim.Speed = speedWas;
            return "closed (back to the title screen)";
        }

        /// <summary>The stage's screen: no HUD; ticks the monster's place, tilt and squash like the battle's Fighter.</summary>
        class StageScreen : GameScreen
        {
            readonly string weapon, monsterId, zoneId, baseSet;
            public BoneHero Hero;
            public Figure Monster;
            public BoneGameEvents Events;
            MonsterDef def;
            SpriteRenderer dim;
            float x = EnemyX, y = GroundY, angle, sx = 1, sy = 1, scale = 1;
            Anim.Handle recoil, shaping, dimming;
            bool twin;

            public StageScreen(string weapon, string monster, string zone, string baseSet)
            {
                this.weapon = weapon;
                monsterId = monster;
                zoneId = zone;
                this.baseSet = baseSet;
            }

            public override void Enter()
            {
                var zone = Db.Zone(zoneId);
                Backdrop.Draw(World, zone.Background, zone.Backdrop, BgRaise);
                dim = Backdrop.Rect(World, Design.Width / 2, Design.Height / 2, Design.Width * 2, Design.Height * 2, new Color(0, 0, 0, 0), -31990);
                def = Db.Monster(monsterId);
                Monster = Figure.Create(World, EnemyX, GroundY, def.Look, -1);
                Monster.EnableBend();
                scale = (float)def.Look.Scale; // StageScale 1: the battle shrinks the stage only for monsters too tall for it
                Tick(0);
                var look = Db.Class("warrior").Look;
                Hero = BoneHero.Create(World, Source, PlayerX, GroundY, (float)look.Scale * SizeFactor);
                Hero.Order = Design.Depth(GroundY);
                if (!string.IsNullOrEmpty(baseSet) && baseSet != "bare")
                {
                    Hero.BaseSets.Add(baseSet);
                    Hero.Reload();
                }
                Hero.Hold(weapon);
                var shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(Hero.transform, false);
                shadow.sprite = Art.Soft;
                shadow.color = new Color(0, 0, 0, 0.45f);
                shadow.sortingOrder = -100;
                float s = Hero.transform.localScale.x;
                shadow.transform.localScale = new Vector3(150 / 64f / s, 26 / 64f / s, 1);
                Events = new BoneGameEvents { Contact = (h, e) => Hit(), Dim = Dim, Heard = Log };
                Hero.Rest();
                var ready = Load("sword_swing_old");
                if (ready != null) Hero.Sample(ready, 0);
            }

            /// <summary>Back home, the monster too (a new take starts from the same place).</summary>
            public void Home()
            {
                recoil?.Stop();
                shaping?.Stop();
                x = EnemyX;
                y = GroundY;
                angle = 0;
                sx = sy = 1;
                Tick(0);
            }

            static void Log(BoneHero h, BoneEvent e)
            {
                if (BonesTrack.Active) BonesTrack.Event(e.Raw);
            }

            void Dim(float alpha, float ms)
            {
                dimming?.Stop();
                float from = dim.color.a;
                dimming = Anim.Float(from, alpha, ms / 1000f, a => { if (dim != null) dim.color = new Color(0, 0, 0, a); }, Ease.QuadOut);
            }

            public override void Tick(float dt)
            {
                if (Monster == null) return;
                Monster.transform.position = Design.World(x, y);
                Monster.transform.localRotation = Quaternion.Euler(0, 0, -angle);
                Monster.transform.localScale = new Vector3(scale * sx * -1, scale * sy, 1);
                Monster.Order = Design.Depth(GroundY);
            }

            /// <summary>The battle's Impact on the monster for a plain everyday hit of HitWeight (not a crit, not a boss).</summary>
            void Hit()
            {
                float w = HitWeight;
                const int dir = 1;
                float hx = EnemyX, hy = GroundY - Chest;
                twin = !twin;
                Fx.Slash(World, hx, hy, dir, 0xffffff, twin ? 0.35f : -0.35f);
                Fx.HitSparks(World, hx, hy, false, false);
                Sfx.Play("hit");
                Fx.WhiteFlash(Monster, 100, 0xffffff);
                // min(150, 70 + weight x 120), capped at 70 ms for a monster that is not a boss
                Fx.HitStop(Mathf.Min(Mathf.Min(150, 70 + w * 120), 70));
                GameRoot.I.Shake(0.09f, (0.002f + w * 0.011f) * Design.Width);
                GameRoot.I.Kick((4 + w * 14) * dir, 1);
                // knocked back and tilted fast, then home slowly (BattleScreen.Impact: 60 ms CubicOut, 300 ms SineInOut after 50)
                recoil?.Stop();
                float push = 18 + w * 70, tilt = (6 + w * 12) * dir;
                x = hx;
                angle = 0;
                recoil = Anim.Float(0, 1, 0.06f, t => { x = EnemyX + push * dir * t; angle = tilt * t; }, Ease.CubicOut);
                recoil.OnComplete(() =>
                {
                    if (push > 26) Fx.Dust(World, EnemyX + push * dir, GroundY, 3, Design.Depth(GroundY) + 1);
                    recoil = Anim.Float(1, 0, 0.3f, t => { x = EnemyX + push * dir * t; angle = tilt * t; }, Ease.SineInOut, 0.05f);
                });
                // squashed, springing back (Fighter.Squash: snap, then 260 ms BackOut)
                shaping?.Stop();
                sx = 0.86f - w * 0.12f;
                sy = 1.08f + w * 0.08f;
                float fx = sx, fy = sy;
                shaping = Anim.Float(0, 1, 0.26f, t => { sx = Mathf.LerpUnclamped(fx, 1, t); sy = Mathf.LerpUnclamped(fy, 1, t); }, Ease.BackOut);
                // rocked back, the head snapping after (Fighter.Pose -> Bend.Pose)
                Monster.Bend?.Pose(-(9 + w * 34), 0.2f + w, 0.16f);
                Fx.Bleed(World, hx - 40 * dir, hy + Random.Range(-20f, 20f), dir, def.Look.Blood, 0.2f + w * 1.6f, GroundY, Design.Depth(GroundY) - 60);
                if ((def.Look.Blood?.Kind ?? "liquid") == "liquid") Anim.After(0.06f, () => Sfx.Play("splat"));
            }
        }

        // --- the port check: the old test swing and the new core, key by key -------------------------------------------

        static GameObject plate;
        public static bool Busy { get; private set; }
        public static string Report { get; private set; }

        static readonly string[] Moments = { "wind", "coil", "strike", "contact", "follow", "back", "ready" };
        static readonly float[] MomentMs = { 300, 360, 400, 440, 520, 800, 1160 };
        /// <summary>The old key shown at each moment (SkeletonSwingDemo.Hold); "strike" lies between keys (the old track).</summary>
        static readonly string[] OldKeys = { "wind", "coil", null, "strike", "follow", "follow", "ready" };

        static SkeletonSwing.Key OldMoment(int i) => OldKeys[i] != null ? SkeletonSwing.Keys[OldKeys[i]] : OldAt(MomentMs[i]);

        /// <summary>
        /// The old SkeletonSwing hero (left) and the new core (right) on a plain plate over the stage, same size and
        /// feet. Their pose at `ms` of the swing: the old one through its own key track (its Mix, by reflection), the
        /// new one through Sample. Returns the largest distance between their joints, grip and tip (rig px).
        /// </summary>
        public static string Port(float ms, string move = "sword_swing_old")
        {
            if (!Ready) return "stage not open";
            var hero = PortHeroes(move, out var old);
            hero.Sample(LastMove, ms);
            old.Pose(OldAt(ms));
            return Diff(old, hero);
        }

        static BoneHero portNew;
        static SkeletonSwing portOld;
        public static BoneHero PortNew => portNew;

        static BoneHero PortHeroes(string move, out SkeletonSwing old)
        {
            Load(move);
            if (plate == null)
            {
                plate = new GameObject("port plate");
                plate.transform.SetParent(GameRoot.I.Screen.World, false);
                var sr = plate.AddComponent<SpriteRenderer>();
                sr.sprite = Art.White;
                sr.color = new Color(0.9f, 0.88f, 0.83f);
                sr.sortingOrder = 29000;
                plate.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
                plate.transform.localScale = new Vector3(Design.Width * 2, Design.Height * 2, 1);
                LayeredHero.Folder = BoneDiskSource.HeroFolder;
                LayeredHero.Reload();
                SkeletonSwing.Reload();
                float scale = (float)Db.Class("warrior").Look.Scale * 1.1f;
                portOld = SkeletonSwing.Create(GameRoot.I.Screen.World, 400, 640, scale);
                portOld.GetComponent<LayeredHero>().Order = 30000;
                portOld.Breathes = false;
                portOld.enabled = false;
                portNew = BoneHero.Create(GameRoot.I.Screen.World, Source, 880, 640, scale);
                portNew.Order = 30000;
            }
            old = portOld;
            return portNew;
        }

        public static string PortClose()
        {
            if (plate != null) Object.Destroy(plate);
            if (portOld != null) Object.Destroy(portOld.gameObject);
            if (portNew != null) Object.Destroy(portNew.gameObject);
            plate = null;
            return "port plate gone";
        }

        static readonly MethodInfo OldMix = typeof(SkeletonSwing).GetMethod("Mix", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>The old swing's pose at `ms` after Blow(), without breathing (SkeletonSwing.Blow + Update, read).</summary>
        public static SkeletonSwing.Key OldAt(float ms)
        {
            var track = new List<(float t, SkeletonSwing.Key k, System.Func<float, float> ease)>
            {
                (0, SkeletonSwing.Ready, SkeletonSwing.InOut), (300, SkeletonSwing.Wind, SkeletonSwing.InOut), (360, SkeletonSwing.Coil, SkeletonSwing.Out),
                (440, SkeletonSwing.Strike, SkeletonSwing.In), (520, SkeletonSwing.Follow, SkeletonSwing.Out), (800, SkeletonSwing.Follow, SkeletonSwing.InOut),
                (1160, SkeletonSwing.Ready, SkeletonSwing.InOut),
            };
            if (ms >= track[track.Count - 1].t) return track[track.Count - 1].k;
            for (int i = 0; i + 1 < track.Count; i++)
                if (ms < track[i + 1].t)
                {
                    float k = Mathf.Clamp01((ms - track[i].t) / (track[i + 1].t - track[i].t));
                    return (SkeletonSwing.Key)OldMix.Invoke(null, new object[] { track[i].k, track[i + 1].k, track[i + 1].ease(k) });
                }
            return track[0].k;
        }

        /// <summary>Largest distance (rig px) between the two heroes' joints, weapon grip and weapon tip, each from its own feet.</summary>
        static string Diff(SkeletonSwing old, BoneHero hero)
        {
            var o = old.transform;
            var n = hero.transform;
            float unit = hero.Unit * n.lossyScale.x;
            float worst = 0;
            string at = "";
            var sb = new StringBuilder();
            void One(string name, Vector3 a, Vector3 b)
            {
                float d = ((a - o.position) - (b - n.position)).magnitude / unit;
                if (d > worst) { worst = d; at = name; }
                if (d > 1) sb.Append($" {name} {d:0.0}");
            }
            foreach (var p in hero.Rig.Parts)
            {
                var a = BonesFind(o, p.Name);
                var b = BonesFind(n, p.Name);
                if (a != null && b != null) One("j_" + p.Joint, a.position, b.position);
            }
            var wa = BonesFind(o, "near weapon");
            var wb = BonesFind(n, "near weapon");
            if (wa != null && wb != null)
            {
                One("grip", wa.position, wb.position);
                One("tip", wa.position + wa.up * 2.2f * o.lossyScale.y, wb.position + wb.up * 2.2f * n.lossyScale.y);
            }
            return $"worst {worst:0.00} rig px ({at})" + (sb.Length > 0 ? ";" + sb : "");
        }

        static Transform BonesFind(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = BonesFind(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// The seven key moments as pictures for the kit (dir/f000.jpg..., track.json with the moment's name as its
        /// event; `bones_sheets.py strip DIR --every 1`), each old (left) beside new (right), plus a diff line per moment
        /// in dir/diff.txt. Also a diff every 10 ms over the whole swing (the in-betweens).
        /// </summary>
        public static string PortKeys(string dir, string move = "sword_swing_old")
        {
            if (!Ready) return "stage not open";
            if (Busy) return "busy";
            Busy = true;
            GameRoot.I.StartCoroutine(PortLoop(dir, move));
            return "port keys to " + dir;
        }

        static IEnumerator PortLoop(string dir, string move)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f*.jpg")) File.Delete(f);
            var hero = PortHeroes(move, out var old);
            var lines = new StringBuilder();
            BonesTrack.Begin(Path.Combine(dir, "track.json"), 1, 1280, 720);
            var rt = new RenderTexture(1280, 720, 0);
            var small = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            for (int i = 0; i < Moments.Length; i++)
            {
                hero.Sample(LastMove, MomentMs[i]);
                old.Pose(OldMoment(i));
                lines.AppendLine($"{Moments[i]} @{MomentMs[i]} ms: {Diff(old, hero)}");
                BonesTrack.Event(Moments[i]);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                small.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"f{i:000}.jpg"), small.EncodeToJPG(92));
                Object.Destroy(tex);
                BonesTrack.Box("hero", BoundsOf(old.gameObject, hero.gameObject));
                BonesTrack.Next(MomentMs[i] / 1000f);
            }
            BonesTrack.End();
            Object.Destroy(rt);
            Object.Destroy(small);
            float worst = 0;
            string worstAt = "";
            // the biggest jump of any joint, grip or tip between two samples 2 ms apart: a pop shows as a jump far
            // bigger than its neighbours'
            var jumpOld = new Jumps();
            var jumpNew = new Jumps();
            for (float t = 0; t <= 1160; t += 2)
            {
                hero.Sample(LastMove, t);
                old.Pose(OldAt(t));
                jumpOld.Add(old.transform, t);
                jumpNew.Add(hero.transform, t);
                if (Mathf.RoundToInt(t) % 10 != 0) continue;
                var d = Diff(old, hero);
                float v = float.Parse(d.Substring(6, d.IndexOf(' ', 6) - 6), System.Globalization.CultureInfo.InvariantCulture);
                if (v > worst) { worst = v; worstAt = $"{t} ms: {d}"; }
            }
            lines.AppendLine($"in-betweens (every 10 ms): worst {worstAt}");
            lines.AppendLine("largest jump in 2 ms (rig px), old test: " + jumpOld.Worst(hero.Unit * hero.transform.lossyScale.x));
            lines.AppendLine("largest jump in 2 ms (rig px), new core: " + jumpNew.Worst(hero.Unit * hero.transform.lossyScale.x));
            File.WriteAllText(Path.Combine(dir, "diff.txt"), lines.ToString());
            Report = lines.ToString();
            Busy = false;
        }

        /// <summary>Per named transform, the largest move between two samples (from its hero's feet).</summary>
        class Jumps
        {
            readonly Dictionary<string, Vector3> last = new Dictionary<string, Vector3>();
            float worst;
            string at = "";

            public void Add(Transform root, float t)
            {
                foreach (var name in new[] { "n_upper", "n_fore", "n_hand", "f_fore", "f_hand", "head", "torso", "n_shin", "f_shin", "near weapon" })
                {
                    var tr = BonesFind(root, name);
                    if (tr == null) continue;
                    var p = tr.position - root.position;
                    if (name == "near weapon") p += tr.up * 2.2f * root.lossyScale.y;
                    if (last.TryGetValue(name, out var was) && (p - was).magnitude > worst)
                    {
                        worst = (p - was).magnitude;
                        at = $"{(name == "near weapon" ? "tip" : name)} at {t} ms";
                    }
                    last[name] = p;
                }
            }

            public string Worst(float unit) => $"{worst / unit:0.0} ({at})";
        }

        static Bounds BoundsOf(params GameObject[] gos)
        {
            Bounds? all = null;
            foreach (var go in gos)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.name == "shadow" || r.name.Contains("trail")) continue;
                    if (all == null) all = r.bounds;
                    else { var b = all.Value; b.Encapsulate(r.bounds); all = b; }
                }
            return all ?? new Bounds();
        }
    }
}
#endif
