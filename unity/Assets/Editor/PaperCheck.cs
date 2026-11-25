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
    /// (no creases, no tears), escape (a Lucky Impling, recorded as it runs off). Busy while running.
    /// </summary>
    public static class PaperCheck
    {
        public static bool Busy;

        /// <summary>Whole-frame times (unscaled, ms): the worst and the mean over the ordinary attacks (the baseline) and
        /// over the killing blow to 3 s after it. Logged per fight; record no frames (`frames` 0) for a fair number.</summary>
        static float probeWorst, probeSum;
        static int probeCount;
        static bool probing;

        static IEnumerator Probe()
        {
            probing = true;
            probeWorst = probeSum = 0;
            probeCount = 0;
            yield return null;
            while (probing)
            {
                var ms = Time.unscaledDeltaTime * 1000;
                probeWorst = Mathf.Max(probeWorst, ms);
                probeSum += ms;
                probeCount++;
                yield return null;
            }
        }

        static string ProbeEnd()
        {
            probing = false;
            return $"worst frame {probeWorst:0.0} ms, mean {probeSum / Mathf.Max(1, probeCount):0.0} ms over {probeCount} frames";
        }

        public static void Run(string monsters, string dir, int hits = 4, int speed = 1, bool paper = true, string elite = null, bool heroic = false,
            int floor = 0, bool oneHit = false, int frames = 80, bool escape = false, bool solid = false, int seed = 0)
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
                    Drive.Record($"{dir}/{name}", frames, 20, 1920);
                    yield return null;
                    Drive.Press(0);
                    yield return new WaitUntil(() => Time.captureFramerate == 0);
                    Debug.Log($"drakepg paper check: {name} recorded, {(GameRoot.I.Screen is BattleScreen ? "still in the battle" : "the battle is over")}, "
                        + $"paper left in the scene: {CountFolds()}");
                }
                else
                {
                    // Any blow can miss (an evasive monster, the hit cap): again until it lands, the recording redone.
                    for (int tries = 0; tries < 6; tries++)
                    {
                        if (tries > 0) Paper.NextSeed = fightSeed;
                        SetHp(battle, shown, 1);
                        if (tries == 0) Drive.Shot($"{dir}/{name}_before.png");
                        yield return new WaitForSeconds(0.3f);
                        if (frames > 0) Drive.Record($"{dir}/{name}", frames, 20, 1920);
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
                    Debug.Log($"drakepg paper check: {name} recorded ({frames} frames){(solid ? ", " + Paper.LastSolid : "")}");
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

        /// <summary>Paper fold quads and canvases still alive (none once a battle is gone).</summary>
        public static string CountFolds()
        {
            int quads = 0, canvases = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "paper fold") quads++;
            foreach (var r in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (r.name == "paper canvas") canvases++;
            return $"{quads} fold quads, {canvases} paper canvases";
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
    }
}
