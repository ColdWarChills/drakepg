using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// The hidden driver behind Fx: steps every particle system each frame (slowed by Anim.TimeScale, so hit-stops freeze
    /// them too) and ends hit-stops on real time. Added to the GameRoot object on first use; systems whose root was
    /// destroyed with its screen are dropped.
    /// </summary>
    [AddComponentMenu("")]
    public class FxRunner : MonoBehaviour
    {
        static FxRunner instance;
        static readonly List<FxSystem> systems = new List<FxSystem>();
        static float stopUntil;
        /// <summary>Slow motion: game time runs at `slowScale` until `slowUntil` (real time), between hit-stops.</summary>
        static float slowUntil, slowScale = 1;

        /// <summary>Clears the statics when play starts (they survive between plays when domain reload is off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            systems.Clear();
            stopUntil = 0;
            slowUntil = 0;
            slowScale = 1;
            Fx.ResetStatics();
        }

        internal static void Register(FxSystem s)
        {
            Ensure();
            systems.Add(s);
        }

        /// <summary>Restore Anim.TimeScale to 1 after `seconds` of real time (the latest of overlapping stops wins).</summary>
        internal static void EndHitStopIn(float seconds)
        {
            Ensure();
            stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
        }

        /// <summary>Run game time at `scale` for `seconds` of real time (Fx.SlowMo).</summary>
        internal static void SlowFor(float scale, float seconds)
        {
            Ensure();
            slowScale = scale;
            slowUntil = Mathf.Max(slowUntil, Time.unscaledTime + seconds);
        }

        static void Ensure()
        {
            if (instance != null) return;
            GameObject host;
            if (GameRoot.I != null) host = GameRoot.I.gameObject;
            else
            {
                host = new GameObject("FxRunner");
                DontDestroyOnLoad(host);
            }
            instance = host.GetComponent<FxRunner>();
            if (instance == null) instance = host.AddComponent<FxRunner>();
            instance.hideFlags = HideFlags.HideInInspector;
        }

        void Update()
        {
            if (stopUntil > 0 && Time.unscaledTime >= stopUntil)
            {
                stopUntil = 0;
                Anim.TimeScale = slowUntil > Time.unscaledTime ? slowScale : 1;
            }
            if (slowUntil > 0 && Time.unscaledTime >= slowUntil)
            {
                slowUntil = 0;
                if (stopUntil <= 0) Anim.TimeScale = 1;
            }
            float dt = Time.deltaTime * (Fx.ParticlesFollowHitStop ? Anim.TimeScale : 1) * Anim.Speed;
            for (int i = systems.Count - 1; i >= 0; i--)
            {
                if (i >= systems.Count) continue;
                var s = systems[i];
                if (s.Root == null)
                {
                    systems.RemoveAt(i);
                    Fx.Forget(s);
                    continue;
                }
                try { s.Step(dt); }
                catch (Exception e) { Debug.LogException(e); }
                if (s.Finished)
                {
                    Destroy(s.Root.gameObject);
                    systems.RemoveAt(i);
                    Fx.Forget(s);
                }
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
