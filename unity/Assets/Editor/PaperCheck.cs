using System.Collections;
using System.Collections.Generic;
using Drakepg.Rules;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Paper damage on every monster (docs/PLANS.md): scripted fights for judging the paper, one per monster, in play mode.
    /// The monster's HP is made 20 times bigger so real hits only crease it; before the last hits its HP is set just above
    /// its change of form, a third and 15%, so a real hit crosses each; a still is saved before the killing blow; then its
    /// HP is set to 1 and the killing blow and the death are recorded (Drive.Record, f000.jpg...). The hero can't die.
    /// <c>PaperCheck.Run("mossling bog_lurker", dir)</c>; options: hits (creases before the tears), speed (x1/x2),
    /// paper off (today's deaths, for timing), an Elite affix, Heroic, a Spire floor (the monster id is ignored), oneHit
    /// (no creases, no tears), escape (a Lucky Impling, recorded as it runs off), solid (the crumple, the game's death since
    /// 2026-11-26; false: the first fold). Busy while running.
    /// </summary>
    public static class PaperCheck
    {
        public static bool Busy;
        /// <summary>Frames a second the recordings are captured at (game time steps by one frame each).</summary>
        public static int Fps = 20;
        /// <summary>The killing blow as soon as the attack can be pressed (a one-hit kill right as the fight opens), timed, not recorded.</summary>
        public static bool FastKill;

        /// <summary>Whole-frame times (unscaled, ms): the worst and the mean over the ordinary attacks (the baseline) and
        /// over the killing blow to 3 s after it. Logged per fight; record no frames (`frames` 0) for a fair number.</summary>
        static float probeWorst, probeSum, probeStart;
        static int probeCount, probeSlow;
        static bool probing;
        /// <summary>The frames over 16.7 ms: seconds after the probe began and their ms (the first dozen).</summary>
        static string probeSlowList = "";

        static IEnumerator Probe()
        {
            probing = true;
            probeWorst = probeSum = 0;
            probeCount = probeSlow = 0;
            probeSlowList = "";
            probeStart = Time.realtimeSinceStartup;
            var gc = System.GC.CollectionCount(0);
            yield return null;
            while (probing)
            {
                var ms = Time.unscaledDeltaTime * 1000;
                probeWorst = Mathf.Max(probeWorst, ms);
                probeSum += ms;
                probeCount++;
                // "g": the garbage collector ran in that frame.
                var gcNow = System.GC.CollectionCount(0);
                if (ms > 16.7f)
                {
                    probeSlow++;
                    if (probeSlow <= 12) probeSlowList += $" {Time.realtimeSinceStartup - probeStart:0.00}s:{ms:0}{(gcNow != gc ? "g" : "")}"
                        + (ms > 100 ? $"[{GameRoot.I.Screen?.GetType().Name}]" : "");
                }
                gc = gcNow;
                yield return null;
            }
        }

        static string ProbeEnd()
        {
            probing = false;
            return $"worst frame {probeWorst:0.0} ms, mean {probeSum / Mathf.Max(1, probeCount):0.0} ms over {probeCount} frames, {probeSlow} over 16.7 ms{(probeSlow > 0 ? " (s:ms" + probeSlowList + ")" : "")}";
        }

        public static void Run(string monsters, string dir, int hits = 4, int speed = 1, bool paper = true, string elite = null, bool heroic = false,
            int floor = 0, bool oneHit = false, int frames = 80, bool escape = false, bool solid = true, int seed = 0)
        {
            Busy = true;
            System.IO.Directory.CreateDirectory(dir);
            GameRoot.I.StartCoroutine(Loop(monsters.Split(' '), dir, hits, speed, paper, elite, heroic, floor, oneHit, frames, escape, solid, seed));
        }

        static string ZoneOf(string monsterId)
        {
            foreach (var z in Db.Data.Zones)
                foreach (var e in z.Encounters)
                    if (e.MonsterId == monsterId) return z.Id;
            return "thornwood";
        }

        static IEnumerator Loop(string[] ids, string dir, int hits, int speed, bool paper, string elite, bool heroic, int floor, bool oneHit, int frames, bool escape,
            bool solid, int seed)
        {
            var keepSpeed = Saves.BattleSpeed;
            var keepPaper = Paper.On;
            var keepSolid = Paper.Solid;
            Paper.Solid = solid;
            Cheats.God = true;
            // Uncapped for the frame times (the game asks for 60 fps, so every frame would read about 16.7 ms).
            var keepRate = Application.targetFrameRate;
            var keepSync = QualitySettings.vSyncCount;
            if (frames <= 0) { Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }
            int nth = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                // A negative seed: that seed and the next ones, one per fight (the same monster several times over).
                var fightSeed = seed < 0 ? -seed + nth : seed;
                nth++;
                yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) && Time.captureFramerate == 0);
                Saves.BattleSpeed = speed;
                Paper.On = paper;
                // Set before the battle: the crumple is built from it as the fight starts.
                Paper.NextSeed = fightSeed;
                var zone = ZoneOf(id);
                GameRoot.I.Go(new BattleScreen(new BattleSetup
                {
                    MonsterId = floor > 0 ? null : id, ZoneId = zone, RoomId = "thornwood_path", Elite = elite, Heroic = heroic, TowerFloor = floor,
                    X = 640, Y = 600,
                }), 0);
                if (FastKill)
                {
                    var began = Time.realtimeSinceStartup;
                    Battle fb = null;
                    yield return new WaitUntil(() => (fb = Drive.Field("battle") as Battle) != null);
                    SetHp(fb, Drive.Field("hpShown") as Dictionary<string, double>, 1);
                    GameRoot.I.StartCoroutine(Probe());
                    yield return new WaitUntil(() => Drive.Press(0) || !(GameRoot.I.Screen is BattleScreen));
                    var pressedAt = Time.realtimeSinceStartup - began;
                    yield return new WaitForSecondsRealtime(4f);
                    var blow = ProbeEnd();
                    Debug.Log($"drakepg paper check: {id}{(speed > 1 ? "_x2" : "")}_fast_s{fightSeed}: attack pressed {pressedAt:0.00} s after the battle began, "
                        + $"{(fb.Enemy.Hp <= 0 ? "killed" : "NOT killed")}; the press to 4 s after it {blow}; {(solid ? Paper.LastSolid : "")}");
                    yield return new WaitForSeconds(1f);
                    if (GameRoot.I.Screen is BattleScreen) yield return new WaitForSeconds(3f);
                    Drive.Room("brindlemoor");
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
                yield return new WaitForSeconds(0.5f);
                var battle = Drive.Field("battle") as Battle;
                if (battle == null)
                {
                    Debug.LogWarning("drakepg paper check: no battle for " + id);
                    continue;
                }
                var name = floor > 0 ? "floor" + floor : id + (elite != null ? "_" + elite : "") + (heroic ? "_heroic" : "") + (paper ? "" : "_off") + (speed > 1 ? "_x2" : "") + (escape ? "_escape" : "") + (seed < 0 ? "_s" + fightSeed : "");
                var shown = Drive.Field("hpShown") as Dictionary<string, double>;
                // Big HP: real hits crease it without killing it.
                battle.Enemy.MaxHp *= 20;
                battle.Enemy.Hp = battle.Enemy.MaxHp;
                yield return new WaitForSeconds(2.5f);
                var steps = new List<double>();
                if (!oneHit && !escape)
                {
                    for (int i = 0; i < hits; i++) steps.Add(-1);
                    var at = battle.Enemy.Def.Phase?.At;
                    if (at.HasValue) steps.Add(at.Value + 0.005);
                    steps.Add(1 / 3.0 + 0.005);
                    steps.Add(0.15 + 0.005);
                }
                GameRoot.I.StartCoroutine(Probe());
                foreach (var s in steps)
                {
                    if (s > 0) SetHp(battle, shown, battle.Enemy.MaxHp * s);
                    yield return Attack();
                }
                var baseline = ProbeEnd();
                if (escape)
                {
                    // A Lucky Impling: hit until its next turn is the one it runs on, then recorded as it gets away.
                    for (int i = 0; i < 8 && battle.EscapeIn > 1 && GameRoot.I.Screen is BattleScreen; i++) yield return Attack();
                    Drive.Shot($"{dir}/{name}_before.png");
                    Drive.Record($"{dir}/{name}", frames, Fps, 1920);
                    yield return null;
                    Drive.Press(0);
                    yield return new WaitUntil(() => Time.captureFramerate == 0);
                    Debug.Log($"drakepg paper check: {name} recorded, {(GameRoot.I.Screen is BattleScreen ? "still in the battle" : "the battle is over")}, "
                        + $"paper left in the scene: {CountFolds()}");
                }
                else
                {
                    // Any blow can miss (an evasive monster, the hit cap): again until it lands, the recording redone.
                    var noteBefore = Paper.LastSolid;
                    for (int tries = 0; tries < 6; tries++)
                    {
                        if (tries > 0) Paper.NextSeed = fightSeed;
                        SetHp(battle, shown, 1);
                        if (tries == 0) Drive.Shot($"{dir}/{name}_before.png");
                        yield return new WaitForSeconds(0.3f);
                        if (frames > 0) Drive.Record($"{dir}/{name}", frames, Fps, 1920);
                        yield return null;
                        GameRoot.I.StartCoroutine(Probe());
                        Drive.Press(0);
                        yield return new WaitUntil(() => Time.captureFramerate == 0);
                        if (frames <= 0) yield return new WaitForSecondsRealtime(4f);
                        var death = ProbeEnd();
                        Debug.Log($"drakepg paper check: {name} frame times: baseline (attacks) {baseline}; the killing blow to 4 s after it {death}");
                        if (battle.Enemy.Hp <= 0) break;
                        Debug.LogWarning($"drakepg paper check: {name} missed, again");
                        yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) || MenuReady());
                    }
                    // This death's own note: a long one may still be moving when the recording ends.
                    var waitFrom = Time.realtimeSinceStartup;
                    if (solid) yield return new WaitUntil(() => !ReferenceEquals(Paper.LastSolid, noteBefore) || Time.realtimeSinceStartup - waitFrom > 8);
                    Debug.Log($"drakepg paper check: {name} recorded ({frames} frames){(solid ? ", " + (ReferenceEquals(Paper.LastSolid, noteBefore) ? "NO NOTE of its own after 8 s" : Paper.LastSolid) : "")}");
                }
                // On to the result screen and away from the battle.
                yield return new WaitForSeconds(1f);
                if (GameRoot.I.Screen is BattleScreen) yield return new WaitForSeconds(3f);
                // Town: no monster wanders there to start a fight between checks.
                Drive.Room("brindlemoor");
                yield return new WaitForSeconds(0.5f);
            }
            Saves.BattleSpeed = keepSpeed;
            Paper.On = keepPaper;
            Paper.Solid = keepSolid;
            Application.targetFrameRate = keepRate;
            QualitySettings.vSyncCount = keepSync;
            Busy = false;
            Debug.Log("drakepg paper check: done");
        }

        /// <summary>
        /// Step 1d in a real fight: the hero (given the skills for it, put back after) uses each step in turn, waiting
        /// for the menu after each: a skill id (tidal_wave, frost_lance, spark_chain...), "attack", or "kill" (HP to 1,
        /// then an attack). Recorded the whole way as dir/f000.jpg... at `fps` frames a game second (`close`: the camera
        /// zoomed 2x on the monster, no HUD), until 5 s after the killing blow. `frames` 0: not recorded, frame times
        /// probed instead. The monster has 20x HP so only "kill" kills it. `monster` "floor3": that Spire floor's monster.
        /// </summary>
        public static void Sequence(string monster, string dir, int fps = 20, bool close = false, int width = 1920, bool record = true,
            string steps = "tidal_wave frost_lance attack spark_chain kill", int speed = 1) =>
            GameRoot.I.StartCoroutine(SequenceLoop(monster, dir, fps, close, width, record, steps.Split(' '), speed));

        static bool recording, holding;
        /// <summary>The frame the recorder is on (logged with each step, so a clip can be cut from the log).</summary>
        static int recFrame;

        static IEnumerator HoldView(float x, float y, float zoom)
        {
            while (holding)
            {
                GameRoot.I.View(x, y, zoom, 0);
                yield return null;
            }
        }

        static IEnumerator RecordWhile(string dir, int fps, int width)
        {
            System.IO.Directory.CreateDirectory(dir);
            Time.captureFramerate = fps;
            recFrame = 0;
            var rt = new RenderTexture(width, width * 9 / 16, 0);
            var small = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var i = 0;
            while (recording && i < 4000)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                small.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"f{i:0000}.jpg"), small.EncodeToJPG(92));
                Object.Destroy(tex);
                i++;
                recFrame = i;
            }
            Time.captureFramerate = 0;
            Object.Destroy(rt);
            Object.Destroy(small);
            Debug.Log($"drakepg paper sequence: recorded {i} frames to {dir}");
        }

        static IEnumerator SequenceLoop(string monster, string dir, int fps, bool close, int width, bool record, string[] steps, int speed)
        {
            Busy = true;
            Cheats.God = true;
            var keepSpeed = Saves.BattleSpeed;
            Saves.BattleSpeed = speed;
            // Not recorded: uncapped, so the frame times are the work's (the game asks for 60 fps).
            var keepRate = Application.targetFrameRate;
            var keepSync = QualitySettings.vSyncCount;
            if (!record)
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }
            yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) && Time.captureFramerate == 0);
            // "floor3": that Spire floor's monster (built by the tower's own rules) instead of a monster id.
            var floor = monster.StartsWith("floor") ? int.Parse(monster.Substring(5)) : 0;
            GameRoot.I.Go(new BattleScreen(new BattleSetup
            {
                MonsterId = floor > 0 ? null : monster, ZoneId = ZoneOf(monster), RoomId = "thornwood_path", TowerFloor = floor, X = 640, Y = 600,
            }), 0);
            yield return new WaitForSeconds(0.5f);
            var battle = Drive.Field("battle") as Battle;
            var shown = Drive.Field("hpShown") as Dictionary<string, double>;
            if (battle == null)
            {
                Debug.LogWarning("drakepg paper sequence: no battle");
                Busy = false;
                yield break;
            }
            var p = battle.Player;
            var keepLoadout = new List<string>(p.Data.Loadout);
            var keepLearned = new List<string>(p.Data.Learned);
            var keepMp = p.Data.Mp;
            foreach (var id in new[] { "tidal_wave", "frost_lance", "spark_chain", "fire_bolt" })
            {
                if (!p.Data.Learned.Contains(id)) p.Data.Learned.Add(id);
                if (!p.Data.Loadout.Contains(id)) p.Data.Loadout.Add(id);
            }
            battle.Enemy.MaxHp *= 20;
            battle.Enemy.Hp = battle.Enemy.MaxHp;
            // The HUD's HP too, or the first hit reads as a fall to a twentieth (the old tears at a third and 15%).
            if (shown != null) shown["enemy"] = battle.Enemy.Hp;
            yield return new WaitForSeconds(2.5f);
            var paper = Drive.Field("paper") as Paper;
            UnityEngine.UIElements.UIDocument[] docs = null;
            if (close && paper != null)
            {
                var c = Design.FromWorld(paper.GetComponent<Figure>().Body.bounds.center);
                GameRoot.I.View(c.x, c.y - 20, 2, 0);
                // Held there the whole fight: the battle's own zooms (a hit's punch, the finish) would take it back out.
                holding = true;
                GameRoot.I.StartCoroutine(HoldView(c.x, c.y - 20, 2));
                docs = Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
                foreach (var d in docs) if (d.rootVisualElement != null) d.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
            }
            if (record)
            {
                recording = true;
                GameRoot.I.StartCoroutine(RecordWhile(dir, fps, width));
            }
            yield return new WaitForSeconds(0.6f);
            var cooldowns = typeof(Battle).GetField("cooldowns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cooldownOrder = typeof(Battle).GetField("cooldownOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var step in steps)
            {
                if (string.IsNullOrEmpty(step) || !(GameRoot.I.Screen is BattleScreen)) continue;
                p.Data.Mp = 999;
                (cooldowns?.GetValue(battle) as Dictionary<string, int>)?.Clear();
                (cooldownOrder?.GetValue(battle) as List<string>)?.Clear();
                // "kill" or "kill:<skill>": HP to 1, then that blow. "phase": HP to just above the form change, then an attack.
                var kill = step == "kill" || step.StartsWith("kill:");
                var move = kill ? (step.Length > 5 ? step.Substring(5) : "attack") : step == "phase" ? "attack" : step;
                if (kill)
                {
                    SetHp(battle, shown, 1);
                    GameRoot.I.StartCoroutine(Probe());
                }
                if (step == "phase" && battle.Enemy.Def.Phase != null)
                    SetHp(battle, shown, System.Math.Floor(battle.Enemy.MaxHp * battle.Enemy.Def.Phase.At) + 1);
                List<BattleEvent> events = null;
                System.Func<List<BattleEvent>> act = () => events = move == "attack" ? battle.Attack() : battle.UseSkill(move);
                Drive.Call("Act", act);
                Debug.Log($"drakepg paper sequence: {step} at frame {recFrame}");
                if (kill)
                {
                    // A miss: again (up to three more), until it dies.
                    for (int again = 0; again < 3; again++)
                    {
                        yield return new WaitForSeconds(0.3f);
                        yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) || MenuReady() || battle.IsOver);
                        if (battle.Enemy.Hp <= 0 || battle.IsOver || !(GameRoot.I.Screen is BattleScreen)) break;
                        Debug.LogWarning("drakepg paper sequence: the killing blow missed, again");
                        yield return new WaitForSeconds(0.5f);
                        SetHp(battle, shown, 1);
                        Drive.Call("Act", act);
                    }
                    yield return new WaitForSeconds(5f);
                    Debug.Log($"drakepg paper sequence: the killing blow to 5 s after it {ProbeEnd()}; {(battle.Enemy.Hp <= 0 ? "killed" : "NOT killed")}");
                    break;
                }
                yield return new WaitForSeconds(0.3f);
                yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) || MenuReady());
                yield return new WaitForSeconds(0.7f);
                // It missed (no hit of the hero's landed): the same move again, up to three more times.
                for (int again = 0; again < 3 && GameRoot.I.Screen is BattleScreen
                    && (events == null || !events.Exists(e => e.Kind == "hit" && e.Source == "player")); again++)
                {
                    Debug.LogWarning($"drakepg paper sequence: {step} missed, again");
                    p.Data.Mp = 999;
                    (cooldowns?.GetValue(battle) as Dictionary<string, int>)?.Clear();
                    (cooldownOrder?.GetValue(battle) as List<string>)?.Clear();
                    Drive.Call("Act", act);
                    yield return new WaitForSeconds(0.3f);
                    yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) || MenuReady());
                    yield return new WaitForSeconds(0.7f);
                }
            }
            recording = false;
            holding = false;
            yield return new WaitUntil(() => Time.captureFramerate == 0);
            if (docs != null)
                foreach (var d in docs) if (d != null && d.rootVisualElement != null) d.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1, 0);
            p.Data.Loadout = keepLoadout;
            p.Data.Learned = keepLearned;
            p.Data.Mp = keepMp;
            Saves.BattleSpeed = keepSpeed;
            Application.targetFrameRate = keepRate;
            QualitySettings.vSyncCount = keepSync;
            yield return new WaitForSeconds(1f);
            if (GameRoot.I.Screen is BattleScreen) yield return new WaitForSeconds(3f);
            Drive.Room("brindlemoor");
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"drakepg paper sequence: done; paper left in the scene: {CountFolds()}");
            Busy = false;
        }

        /// <summary>Paper fold quads and canvases still alive (none once a battle is gone).</summary>
        public static string CountFolds()
        {
            int quads = 0, canvases = 0, pieces = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "paper fold") quads++;
                if (t.name == "paper piece") pieces++;
            }
            foreach (var r in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (r.name == "paper canvas") canvases++;
            return $"{quads} fold quads, {canvases} paper canvases, {pieces} snapped pieces";
        }

        static void SetHp(Battle battle, Dictionary<string, double> shown, double hp)
        {
            if (battle.Enemy.Hp <= hp) return;
            battle.Enemy.Hp = hp;
            if (shown != null) shown["enemy"] = hp;
        }

        /// <summary>One attack, then wait until the menu is back (the monster's turn played too).</summary>
        static IEnumerator Attack()
        {
            yield return new WaitUntil(() => Drive.Press(0) || !(GameRoot.I.Screen is BattleScreen));
            yield return new WaitForSeconds(0.3f);
            yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen) || MenuReady());
        }

        static bool MenuReady() => !(Drive.Field("busy") is bool b) || !b;

        /// <summary>
        /// Step 1d stills: each monster in each water, ice and energy state, set through the paper's own hit code
        /// (Paper.TestHit, no damage) and settled, saved as dir/&lt;monster&gt;_&lt;state&gt;.png (the whole screen) and
        /// _&lt;state&gt;_3x.png (the camera zoomed 2x on it: 3x game size at 1920 wide). States: clean, wet, drying, dried,
        /// frost, snap, rip, chain (three energy hits of one move), melt (ice, then fire).
        /// </summary>
        public static void States(string monsters, string dir, string states = "clean wet drying dried frost snap rip chain melt") =>
            GameRoot.I.StartCoroutine(StatesLoop(monsters.Split(' '), dir, states.Split(' ')));

        static IEnumerator StatesLoop(string[] ids, string dir, string[] states)
        {
            Busy = true;
            System.IO.Directory.CreateDirectory(dir);
            Cheats.God = true;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                yield return new WaitUntil(() => !(GameRoot.I.Screen is BattleScreen));
                GameRoot.I.Go(new BattleScreen(new BattleSetup { MonsterId = id, ZoneId = ZoneOf(id), RoomId = "thornwood_path", X = 640, Y = 600 }), 0);
                yield return new WaitForSeconds(3f);
                var paper = Drive.Field("paper") as Paper;
                if (paper == null)
                {
                    Debug.LogWarning("drakepg paper states: no paper for " + id);
                    continue;
                }
                foreach (var state in states)
                {
                    paper.TestClear();
                    switch (state)
                    {
                        case "wet": paper.TestHit("water"); break;
                        case "drying": paper.TestHit("water"); paper.Settle(); paper.Turn(); break;
                        case "dried": paper.TestHit("water"); paper.Settle(); paper.Turn(); paper.Turn(); paper.Turn(); break;
                        case "frost": paper.TestHit("ice"); break;
                        case "snap":
                            paper.TestHit("ice");
                            paper.Settle();
                            yield return new WaitForSeconds(0.6f);
                            paper.TestHit(null, 0.4f);
                            break;
                        case "rip": paper.TestHit("energy", 0.4f); break;
                        case "chain": paper.TestHit("energy", 0.25f); paper.TestHit("energy", 0.25f); paper.TestHit("energy", 0.25f); break;
                        case "melt": paper.TestHit("ice"); paper.Settle(); paper.TestHit("fire"); break;
                    }
                    paper.Settle();
                    yield return new WaitForSeconds(state == "wet" || state == "drying" ? 1.2f : 0.25f);
                    // The snapped piece, its flakes and the steam gone before the picture.
                    if (state == "snap" || state == "melt") yield return new WaitForSeconds(2f);
                    GameRoot.I.Snapshot($"{dir}/{id}_{state}.png");
                    yield return null;
                    var c = Design.FromWorld(paper.GetComponent<Figure>().Body.bounds.center);
                    GameRoot.I.View(c.x, c.y, 2, 0);
                    // The close-up without the HUD and menus over it.
                    var docs = Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
                    foreach (var d in docs) if (d.rootVisualElement != null) d.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
                    yield return new WaitForSeconds(0.1f);
                    GameRoot.I.Snapshot($"{dir}/{id}_{state}_3x.png");
                    yield return null;
                    yield return null;
                    foreach (var d in docs) if (d != null && d.rootVisualElement != null) d.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                    GameRoot.I.View(Design.Width / 2, Design.Height / 2, 1, 0);
                    yield return new WaitForSeconds(0.1f);
                }
                paper.TestClear();
                Drive.Room("brindlemoor");
                yield return new WaitForSeconds(0.8f);
            }
            Busy = false;
            Debug.Log("drakepg paper states: done");
        }
    }
}
