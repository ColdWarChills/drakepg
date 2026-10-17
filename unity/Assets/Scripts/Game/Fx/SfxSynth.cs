using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// The sound synth (src/ui/sfx.ts): every sound is oscillator tones plus filtered noise, rendered the first time it
    /// plays into an AudioClip (no audio files). The web version drifts the pitch of some sounds by up to 4% on every
    /// play; here those sounds keep a few drifted renders and pick one at random. Playback is a small pool of
    /// AudioSources so sounds overlap.
    /// </summary>
    public static partial class Sfx
    {
        /// <summary>The extra layer an element's hit adds on top of the normal hit sound.</summary>
        public static readonly Dictionary<string, string> ElementSfx = new Dictionary<string, string>
        {
            { "fire", "burn" },
            { "ice", "freeze" },
            { "water", "splash" },
            { "wind", "gust" },
            { "earth", "quake" },
            { "energy", "zap" },
            { "light", "holy" },
            { "dark", "void" },
        };

        /// <summary>Remembered on this device (the M key / speaker button).</summary>
        public static bool Muted
        {
            get => Saves.Muted;
            set
            {
                Saves.Muted = value;
                Saves.SaveSettings();
            }
        }

        const int Rate = 44100;
        /// <summary>Master gain of every sound (VOLUME in sfx.ts).</summary>
        const double Volume = 0.45;
        /// <summary>Pitch drift range: v = 1 + (random - 0.5) * Drift.</summary>
        const double Drift = 0.08;
        /// <summary>Drifted renders kept per drifting sound (the web rolls v freely; these are the bucket centers).</summary>
        const int DriftSteps = 5;
        const int Voices = 8;

        sealed class Sound
        {
            public bool Drifts;
            public AudioClip[] Clips;
        }

        static readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
        static readonly System.Random noise = new System.Random(20261017);
        static GameObject host;
        static AudioSource[] pool;
        static float[] started;
        static int next;

        static partial void PlayImpl(string name)
        {
            if (Muted || !Application.isPlaying || string.IsNullOrEmpty(name)) return;
            var clip = ClipFor(name);
            if (clip == null) return;
            var source = FreeSource();
            source.clip = clip;
            source.Play();
        }

        static AudioClip ClipFor(string name)
        {
            if (!sounds.TryGetValue(name, out var sound))
            {
                sound = Describe(name);
                sounds[name] = sound; // null for an unknown name, so it warns once
            }
            if (sound == null) return null;
            int k = sound.Drifts ? UnityEngine.Random.Range(0, DriftSteps) : 0;
            if (sound.Clips[k] == null)
            {
                double v = sound.Drifts ? 1 + Drift * ((k + 0.5) / DriftSteps - 0.5) : 1;
                sound.Clips[k] = Render(sound.Drifts ? name + "_" + k : name, name, v);
            }
            return sound.Clips[k];
        }

        static Sound Describe(string name)
        {
            var plain = new Mix();
            if (!Compose(plain, name, 1))
            {
                Debug.LogWarning("Sfx: no sound named '" + name + "'");
                return null;
            }
            // A sound drifts when its recipe uses the pitch factor.
            var drifted = new Mix();
            Compose(drifted, name, 1 + Drift / 2);
            bool drifts = !plain.SameAs(drifted);
            return new Sound { Drifts = drifts, Clips = new AudioClip[drifts ? DriftSteps : 1] };
        }

        static AudioClip Render(string clipName, string name, double v)
        {
            var mix = new Mix();
            Compose(mix, name, v);
            float[] data = mix.Render(noise);
            var clip = AudioClip.Create("sfx_" + clipName, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>An idle source (round robin), or the one that started longest ago.</summary>
        static AudioSource FreeSource()
        {
            if (host == null) MakeHost();
            int pick = -1;
            for (int i = 0; i < Voices && pick < 0; i++)
            {
                int j = (next + i) % Voices;
                if (!pool[j].isPlaying) pick = j;
            }
            if (pick < 0)
            {
                pick = 0;
                for (int j = 1; j < Voices; j++)
                    if (started[j] < started[pick]) pick = j;
                pool[pick].Stop();
            }
            next = (pick + 1) % Voices;
            started[pick] = Time.unscaledTime;
            return pool[pick];
        }

        static void MakeHost()
        {
            host = new GameObject("Sfx") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(host);
            pool = new AudioSource[Voices];
            started = new float[Voices];
            next = 0;
            for (int i = 0; i < Voices; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0;
                s.volume = 1;
                pool[i] = s;
            }
            // The Boot scene's camera has the listener; a camera made in code doesn't, and then nothing would be heard.
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
        }

        /// <summary>The recipes, call for call as in play() in sfx.ts (times are seconds after the sound starts).</summary>
        static bool Compose(Mix m, string name, double v)
        {
            switch (name)
            {
                case "click":
                    m.Tone(0, Wave.Square, 900 * v, 700 * v, 0.05, 0.12);
                    return true;
                case "open":
                    m.Tone(0, Wave.Triangle, 500, 900, 0.09, 0.15);
                    return true;
                case "slash":
                    m.Hiss(0, 0.16, 0.5, Pass.Bandpass, 5000 * v, 900, 2);
                    return true;
                case "hit":
                    m.Tone(0, Wave.Sine, 160 * v, 50, 0.16, 0.7);
                    m.Hiss(0, 0.09, 0.45, Pass.Lowpass, 3000, 400);
                    return true;
                case "crit":
                    m.Tone(0, Wave.Sine, 200 * v, 40, 0.28, 0.9);
                    m.Tone(0, Wave.Square, 1300, 600, 0.12, 0.12);
                    m.Hiss(0, 0.18, 0.6, Pass.Lowpass, 6000, 300);
                    return true;
                case "miss":
                    m.Hiss(0, 0.2, 0.25, Pass.Bandpass, 2500 * v, 600, 3);
                    return true;
                case "charge":
                    m.Tone(0, Wave.Sawtooth, 120, 600, 0.3, 0.09);
                    m.Hiss(0, 0.3, 0.12, Pass.Bandpass, 400, 3000, 4);
                    return true;
                case "fireball":
                    m.Hiss(0, 0.4, 0.35, Pass.Lowpass, 800, 2500, 1);
                    return true;
                case "explode":
                    m.Hiss(0, 0.5, 0.8, Pass.Lowpass, 2500, 80);
                    m.Tone(0, Wave.Sine, 120, 35, 0.4, 0.8);
                    return true;
                case "heal":
                    m.Arpeggio(0, new[] { 523.0, 659, 784, 1047 }, 0.07, Wave.Sine, 0.22, 0.3);
                    return true;
                case "coin":
                    m.Tone(0, Wave.Square, 1250 * v, 1250 * v, 0.06, 0.08);
                    m.Tone(0.055, Wave.Square, 1680 * v, 1680 * v, 0.12, 0.08);
                    return true;
                case "levelup":
                    m.Arpeggio(0, new[] { 392.0, 523, 659, 784, 1047, 1319 }, 0.08, Wave.Triangle, 0.3, 0.35);
                    return true;
                case "step":
                    m.Hiss(0, 0.05, 0.08, Pass.Lowpass, 700 * v, 200);
                    return true;
                case "alert":
                    m.Tone(0, Wave.Square, 700, 700, 0.07, 0.12);
                    m.Tone(0.08, Wave.Square, 1050, 1050, 0.1, 0.12);
                    return true;
                case "encounter":
                    m.Tone(0, Wave.Sawtooth, 880, 110, 0.45, 0.18);
                    m.Hiss(0, 0.45, 0.4, Pass.Bandpass, 4000, 200, 2);
                    return true;
                case "victory":
                    m.Arpeggio(0, new[] { 523.0, 659, 784 }, 0.11, Wave.Triangle, 0.3, 0.2);
                    m.Tone(0.36, Wave.Triangle, 1047, 1047, 0.6, 0.32);
                    return true;
                case "defeat":
                    m.Arpeggio(0, new[] { 392.0, 349, 311, 262 }, 0.18, Wave.Triangle, 0.25, 0.4);
                    return true;
                case "quest":
                    m.Arpeggio(0, new[] { 659.0, 880, 1175 }, 0.09, Wave.Sine, 0.25, 0.4);
                    return true;
                case "warning":
                    m.Tone(0, Wave.Sawtooth, 220, 180, 0.35, 0.16);
                    m.Tone(0.18, Wave.Sawtooth, 200, 150, 0.4, 0.16);
                    return true;
                case "denied":
                    m.Tone(0, Wave.Square, 220, 180, 0.12, 0.1);
                    return true;
                case "equip":
                    // Buckle click plus a short metal ring.
                    m.Tone(0, Wave.Square, 900, 500, 0.04, 0.08);
                    m.Tone(0.04, Wave.Triangle, 1400, 1350, 0.25, 0.12);
                    return true;
                case "rare":
                    // Shimmer up for a high-grade drop.
                    m.Arpeggio(0, new[] { 784.0, 988, 1175, 1568, 1976 }, 0.06, Wave.Sine, 0.22, 0.5);
                    m.Tone(0.3, Wave.Triangle, 2637, 2637, 0.6, 0.06);
                    return true;
                case "sigil":
                    // Bell chord, then a rising shimmer: a Sigil of Dawn or Dusk dropped.
                    foreach (double n in new[] { 523.0, 659, 784, 1047 }) m.Tone(0, Wave.Sine, n, n, 1.2, 0.09);
                    m.Arpeggio(0.25, new[] { 1047.0, 1319, 1568, 2093, 2637 }, 0.07, Wave.Triangle, 0.1, 0.5);
                    m.Hiss(0.2, 0.9, 0.05, Pass.Highpass, 4000, 9000);
                    return true;
                case "eclipse":
                {
                    // Deep boom, dark chord swelling up, then the shimmer.
                    m.Tone(0, Wave.Sine, 110, 40, 1.1, 0.4);
                    m.Hiss(0, 0.6, 0.18, Pass.Lowpass, 900, 80);
                    var chord = new[] { 220.0, 262, 330, 415 };
                    for (int i = 0; i < chord.Length; i++)
                        m.Tone(0.15 + i * 0.05, Wave.Sawtooth, chord[i], chord[i] * 2, 1.4, 0.05);
                    m.Arpeggio(0.7, new[] { 880.0, 1047, 1319, 1661, 2093, 2637 }, 0.08, Wave.Sine, 0.14, 0.7);
                    m.Hiss(0.7, 1.4, 0.06, Pass.Highpass, 5000, 11000);
                    return true;
                }
                case "recall":
                    // A Recall Scroll: paper whoosh, then a soft rising bell.
                    m.Hiss(0, 0.35, 0.25, Pass.Bandpass, 800, 3000, 1.5);
                    m.Arpeggio(0.15, new[] { 392.0, 523, 659, 784, 1047 }, 0.09, Wave.Sine, 0.18, 0.6);
                    m.Hiss(0.4, 0.8, 0.04, Pass.Highpass, 4000, 9000);
                    return true;
                case "omen":
                    // A quiet room's first visit: a low swell and a far-off creak.
                    m.Tone(0, Wave.Sine, 98, 92, 1.8, 0.22);
                    m.Tone(0.05, Wave.Triangle, 147, 139, 1.6, 0.06);
                    m.Tone(0.9, Wave.Sawtooth, 70, 55, 0.5, 0.05);
                    return true;
                case "page":
                    m.Hiss(0, 0.18, 0.12, Pass.Bandpass, 2500, 1200, 1.2);
                    return true;
                case "whoosh":
                    // A quick lunge through the air.
                    m.Hiss(0, 0.14, 0.22, Pass.Bandpass, 900 * v, 2600, 1.5);
                    return true;
                case "block":
                    // Blow glancing off a guard: a dull knock and a bright ring.
                    m.Tone(0, Wave.Sine, 140 * v, 70, 0.1, 0.45);
                    m.Tone(0, Wave.Triangle, 1900 * v, 1750, 0.3, 0.12);
                    m.Tone(0, Wave.Triangle, 2850 * v, 2700, 0.2, 0.06);
                    return true;
                case "debuff":
                    m.Tone(0, Wave.Triangle, 520 * v, 260, 0.25, 0.12);
                    m.Tone(0.06, Wave.Sine, 390 * v, 190, 0.25, 0.08);
                    return true;
                case "stun":
                {
                    // Dizzy bird chirps.
                    var chirps = new[] { 1568.0, 1319, 1568 };
                    for (int i = 0; i < chirps.Length; i++)
                        m.Tone(i * 0.07, Wave.Sine, chirps[i] * v, chirps[i] * v * 1.06, 0.07, 0.08);
                    return true;
                }
                case "burn":
                    m.Hiss(0, 0.35, 0.3, Pass.Bandpass, 700 * v, 2400, 0.8);
                    m.Hiss(0.05, 0.25, 0.12, Pass.Highpass, 3000, 6000);
                    return true;
                case "freeze":
                {
                    // Ice cracking: glassy tinkles.
                    var tinkles = new[] { 2637.0, 3136, 2349 };
                    for (int i = 0; i < tinkles.Length; i++)
                        m.Tone(i * 0.035, Wave.Triangle, tinkles[i] * v, tinkles[i] * v * 0.97, 0.18, 0.06);
                    m.Hiss(0, 0.08, 0.2, Pass.Highpass, 5000, 8000);
                    return true;
                }
                case "splash":
                    m.Hiss(0, 0.3, 0.3, Pass.Lowpass, 2400 * v, 500, 1.2);
                    m.Tone(0, Wave.Sine, 600 * v, 1400, 0.08, 0.1);
                    return true;
                case "gust":
                    m.Hiss(0, 0.4, 0.22, Pass.Bandpass, 500 * v, 1800, 2.5);
                    return true;
                case "quake":
                    m.Tone(0, Wave.Sine, 70 * v, 35, 0.4, 0.6);
                    m.Hiss(0, 0.35, 0.3, Pass.Lowpass, 600, 80);
                    return true;
                case "zap":
                    m.Tone(0, Wave.Sawtooth, 1800 * v, 200, 0.12, 0.1);
                    m.Hiss(0, 0.15, 0.25, Pass.Highpass, 3000, 1500);
                    m.Tone(0.06, Wave.Square, 900 * v, 120, 0.08, 0.06);
                    return true;
                case "holy":
                    foreach (double n in new[] { 1047.0, 1319, 1568 }) m.Tone(0, Wave.Sine, n * v, n * v, 0.45, 0.06);
                    m.Hiss(0, 0.3, 0.06, Pass.Highpass, 5000, 9000);
                    return true;
                case "void":
                    m.Tone(0, Wave.Sine, 220 * v, 55, 0.35, 0.35);
                    m.Tone(0, Wave.Sawtooth, 110 * v, 60, 0.3, 0.05);
                    return true;
                default:
                    return false;
            }
        }

        enum Wave { Sine, Square, Sawtooth, Triangle }

        enum Pass { Lowpass, Highpass, Bandpass }

        /// <summary>
        /// One sound's voices, rendered the way WebAudio plays them: tone() is an oscillator whose frequency glides
        /// exponentially from `from` to `to` over `dur`; hiss() is white noise through a biquad filter whose cutoff
        /// glides the same way. Both have an exponential gain envelope 0.0001 -> vol (8 ms / 6 ms) -> 0.0001 at `dur`.
        /// </summary>
        sealed class Mix
        {
            struct Voice
            {
                public bool Noise;
                public Wave Wave;
                public Pass Pass;
                public double T, Dur, Vol, From, To, Q;
            }

            /// <summary>Where the gain envelopes start and end (an exponential ramp can't reach 0).</summary>
            const double Floor = 0.0001;
            const double ToneAttack = 0.008, HissAttack = 0.006;
            /// <summary>Sources stop 20 ms after their ramps end.</summary>
            const double Tail = 0.02;
            /// <summary>Noise runs out early: the web plays a 1 s buffer from a random offset of up to 0.5 s (0.25 on average).</summary>
            const double NoiseSeconds = 0.75;
            /// <summary>WebAudio scales its band-limited square and sawtooth so their ringing peaks at 1.</summary>
            const double EdgeLevel = 0.85;

            readonly List<Voice> voices = new List<Voice>();

            public void Tone(double t, Wave type, double from, double to, double dur, double vol) =>
                voices.Add(new Voice { Wave = type, T = t, Dur = dur, Vol = vol, From = from, To = to });

            public void Hiss(double t, double dur, double vol, Pass filter, double from, double to, double q = 1) =>
                voices.Add(new Voice { Noise = true, Pass = filter, T = t, Dur = dur, Vol = vol, From = from, To = to, Q = q });

            public void Arpeggio(double t, double[] notes, double step, Wave type, double vol, double len = 0.25)
            {
                for (int i = 0; i < notes.Length; i++) Tone(t + i * step, type, notes[i], notes[i], len, vol);
            }

            public bool SameAs(Mix other)
            {
                if (voices.Count != other.voices.Count) return false;
                for (int i = 0; i < voices.Count; i++)
                {
                    Voice a = voices[i], b = other.voices[i];
                    if (a.Noise != b.Noise || a.Wave != b.Wave || a.Pass != b.Pass || a.T != b.T || a.Dur != b.Dur ||
                        a.Vol != b.Vol || a.From != b.From || a.To != b.To || a.Q != b.Q)
                        return false;
                }
                return true;
            }

            public float[] Render(System.Random rng)
            {
                double end = 0;
                foreach (var v in voices) end = Math.Max(end, v.T + v.Dur + Tail);
                var mix = new double[Math.Max(1, (int)Math.Ceiling(end * Rate))];
                foreach (var v in voices)
                {
                    if (v.Noise) RenderHiss(mix, v, rng);
                    else RenderTone(mix, v);
                }
                var data = new float[mix.Length];
                for (int i = 0; i < mix.Length; i++) data[i] = Limit(mix[i] * Volume);
                return data;
            }

            static void RenderTone(double[] mix, in Voice v)
            {
                int start = (int)Math.Round(v.T * Rate);
                int count = Math.Min(mix.Length - start, (int)Math.Round((v.Dur + Tail) * Rate));
                var env = new Envelope(v, ToneAttack);
                double phase = 0;
                for (int i = 0; i < count; i++)
                {
                    double dt = env.Freq / Rate;
                    mix[start + i] += Osc(v.Wave, phase, dt) * env.Gain;
                    phase += dt;
                    if (phase >= 1) phase -= 1;
                    env.Step();
                }
            }

            static void RenderHiss(double[] mix, in Voice v, System.Random rng)
            {
                int start = (int)Math.Round(v.T * Rate);
                int count = Math.Min(mix.Length - start, (int)Math.Round((v.Dur + Tail) * Rate));
                int noiseLength = (int)Math.Round(NoiseSeconds * Rate);
                var env = new Envelope(v, HissAttack);
                // WebAudio reads Q in dB for lowpass/highpass, as is for bandpass.
                double q = v.Pass == Pass.Bandpass ? v.Q : Math.Pow(10, v.Q / 20);
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int i = 0; i < count; i++)
                {
                    double x = i < noiseLength ? rng.NextDouble() * 2 - 1 : 0;
                    // RBJ biquad coefficients (WebAudio spec), recomputed every sample as the cutoff glides.
                    double w = 2 * Math.PI * Math.Min(env.Freq, Rate * 0.5) / Rate;
                    double cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
                    double b0, b1, b2;
                    switch (v.Pass)
                    {
                        case Pass.Lowpass:
                            b0 = (1 - cos) / 2;
                            b1 = 1 - cos;
                            b2 = b0;
                            break;
                        case Pass.Highpass:
                            b0 = (1 + cos) / 2;
                            b1 = -(1 + cos);
                            b2 = b0;
                            break;
                        default:
                            b0 = alpha;
                            b1 = 0;
                            b2 = -alpha;
                            break;
                    }
                    double a0 = 1 + alpha, a1 = -2 * cos, a2 = 1 - alpha;
                    double y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                    x2 = x1;
                    x1 = x;
                    y2 = y1;
                    y1 = y;
                    mix[start + i] += y * env.Gain;
                    env.Step();
                }
            }

            /// <summary>
            /// The two automations per sample. Frequency: `from` -> max(20, `to`) exponentially over `dur`, then holds.
            /// Gain: Floor -> vol over the attack, -> Floor at `dur`, then holds (all exponential).
            /// </summary>
            struct Envelope
            {
                public double Freq, Gain;
                readonly double to, glide, vol, up, down;
                readonly int attack, ramp;
                int i;

                public Envelope(in Voice v, double attackSeconds)
                {
                    ramp = Math.Max(2, (int)Math.Round(v.Dur * Rate));
                    attack = Math.Max(1, Math.Min(ramp - 1, (int)Math.Round(attackSeconds * Rate)));
                    to = Math.Max(20, v.To);
                    vol = v.Vol;
                    Freq = v.From;
                    glide = Math.Pow(to / v.From, 1.0 / ramp);
                    Gain = Floor;
                    up = Math.Pow(vol / Floor, 1.0 / attack);
                    down = Math.Pow(Floor / vol, 1.0 / (ramp - attack));
                    i = 0;
                }

                public void Step()
                {
                    i++;
                    Freq = i < ramp ? Freq * glide : to;
                    if (i < attack) Gain *= up;
                    else if (i == attack) Gain = vol;
                    else if (i < ramp) Gain *= down;
                    else Gain = Floor;
                }
            }

            /// <summary>One oscillator sample at phase p (0..1), with WebAudio's wave shapes and starting phases.</summary>
            static double Osc(Wave type, double p, double dt)
            {
                switch (type)
                {
                    case Wave.Sine:
                        return Math.Sin(2 * Math.PI * p);
                    case Wave.Square:
                    {
                        double s = p < 0.5 ? 1 : -1;
                        s += Blep(p, dt);
                        s -= Blep(Wrap(p + 0.5), dt);
                        return s * EdgeLevel;
                    }
                    case Wave.Sawtooth:
                    {
                        // Rises from 0 at phase 0, drops from +1 to -1 at phase 0.5.
                        double q = Wrap(p + 0.5);
                        return (2 * q - 1 - Blep(q, dt)) * EdgeLevel;
                    }
                    default:
                        return p < 0.25 ? 4 * p : p < 0.75 ? 2 - 4 * p : 4 * p - 4;
                }
            }

            static double Wrap(double x) => x >= 1 ? x - 1 : x;

            /// <summary>PolyBLEP: rounds a hard edge over a sample each side, so square and saw don't alias.</summary>
            static double Blep(double t, double dt)
            {
                if (t < dt)
                {
                    t /= dt;
                    return t + t - t * t - 1;
                }
                if (t > 1 - dt)
                {
                    t = (t - 1) / dt;
                    return t * t + t + t + 1;
                }
                return 0;
            }

            /// <summary>Soft limit above 0.8 so stacked voices never clip (WebAudio would hard-clip at 1).</summary>
            static float Limit(double x)
            {
                const double knee = 0.8;
                double a = Math.Abs(x);
                if (a <= knee) return (float)x;
                a = knee + (1 - knee) * Math.Tanh((a - knee) / (1 - knee));
                return (float)(x < 0 ? -a : a);
            }
        }
    }
}
