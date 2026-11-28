using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Drakepg.Tests
{
    /// <summary>Recorded sound files in place of synth sounds (Sfx.ClipFor, SfxSynth.cs), with a fake file loader.</summary>
    public class SfxFileTests
    {
        readonly List<AudioClip> made = new List<AudioClip>();

        [SetUp]
        public void SetUp() => Sfx.ResetCache();

        [TearDown]
        public void TearDown()
        {
            Sfx.LoadFile = Sfx.DefaultLoadFile;
            Sfx.ResetCache();
            foreach (var c in made) if (c != null) UnityEngine.Object.DestroyImmediate(c);
            made.Clear();
        }

        /// <summary>A 0.3 s 440 Hz sine at the given peak, 48 kHz mono like the takes.</summary>
        AudioClip Tone(string name, float peak)
        {
            const int rate = 48000;
            var data = new float[rate * 3 / 10];
            for (int i = 0; i < data.Length; i++) data[i] = peak * Mathf.Sin(2 * Mathf.PI * 440 * i / rate);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            made.Add(clip);
            return clip;
        }

        static void Only(string sound, AudioClip clip) => Sfx.LoadFile = n => n == sound ? clip : null;

        [Test]
        public void FileClipPlaysTurnedDownToTheSynth()
        {
            var file = Tone("test_slash", 0.9f);
            Only("slash", file);
            var clip = Sfx.ClipFor("slash", out float gain, out _);
            Assert.AreSame(file, clip);
            Assert.That(gain, Is.GreaterThan(0f).And.LessThan(1f), "a loud file is turned down");
        }

        [Test]
        public void HalfAsLoudFileGetsTwiceTheFactor()
        {
            Only("hit", Tone("test_hit_loud", 0.9f));
            Sfx.ClipFor("hit", out float loud, out _);
            Sfx.ResetCache();
            Only("hit", Tone("test_hit_half", 0.45f));
            Sfx.ClipFor("hit", out float half, out _);
            Assert.That(loud, Is.LessThan(0.5f), "the half file must stay under the clamp for this check");
            Assert.That(half, Is.EqualTo(loud * 2).Within(0.001f));
        }

        [Test]
        public void FactorIsClampedAtOne()
        {
            var file = Tone("test_heal_quiet", 0.0005f);
            Only("heal", file);
            var clip = Sfx.ClipFor("heal", out float gain, out _);
            Assert.AreSame(file, clip);
            Assert.AreEqual(1f, gain);
        }

        [Test]
        public void NoFilePlaysTheSynthAtFactorOne()
        {
            Sfx.LoadFile = _ => null;
            var clip = Sfx.ClipFor("block", out float gain, out float pitch);
            Assert.IsNotNull(clip);
            StringAssert.StartsWith("sfx_block", clip.name);
            Assert.AreEqual(1f, gain);
            Assert.AreEqual(1f, pitch);
        }

        [Test]
        public void SilentFileFallsBackToTheSynth()
        {
            Only("coin", Tone("test_coin_silent", 0f));
            LogAssert.Expect(LogType.Warning, "Sfx: could not read the samples of Resources/Sfx/coin, playing the synth sound");
            var clip = Sfx.ClipFor("coin", out float gain, out _);
            Assert.IsNotNull(clip);
            StringAssert.StartsWith("sfx_coin", clip.name);
            Assert.AreEqual(1f, gain);
        }

        [Test]
        public void FileRemovedWhileRunningFallsBackToTheSynth()
        {
            // The file's asset goes away mid-session (uninstalled while the game runs): the synth takes over.
            var file = Tone("test_miss_gone", 0.9f);
            Only("miss", file);
            Assert.AreSame(file, Sfx.ClipFor("miss", out _, out _));
            UnityEngine.Object.DestroyImmediate(file);
            var clip = Sfx.ClipFor("miss", out float gain, out float pitch);
            Assert.IsNotNull(clip);
            StringAssert.StartsWith("sfx_miss", clip.name);
            Assert.AreEqual(1f, gain);
            Assert.AreEqual(1f, pitch);
        }

        [Test]
        public void FileDriftsOnlyWhereTheSynthDrifts()
        {
            // slash uses the pitch factor in its recipe, heal doesn't (SfxSynth.Compose).
            var slash = Tone("test_slash_drift", 0.9f);
            var heal = Tone("test_heal_drift", 0.9f);
            Sfx.LoadFile = n => n == "slash" ? slash : n == "heal" ? heal : null;
            bool moved = false;
            for (int i = 0; i < 40; i++)
            {
                Sfx.ClipFor("slash", out _, out float p);
                Assert.That(p, Is.InRange(0.96f, 1.04f));
                if (Math.Abs(p - 1) > 1e-4f) moved = true;
                Sfx.ClipFor("heal", out _, out float still);
                Assert.AreEqual(1f, still);
            }
            Assert.IsTrue(moved, "slash's pitch never drifted in 40 plays");
        }

        /// <summary>A fake loader that serves these files by name and writes down every name it is asked for.</summary>
        static List<string> Serve(Dictionary<string, AudioClip> files)
        {
            var asked = new List<string>();
            Sfx.LoadFile = n =>
            {
                asked.Add(n);
                return files.TryGetValue(n, out var c) ? c : null;
            };
            return asked;
        }

        [Test]
        public void TwoTakesBothPlayEachWithItsOwnFactor()
        {
            var loud = Tone("test_hit_take1", 0.9f);
            var half = Tone("test_hit_take2", 0.45f);
            Serve(new Dictionary<string, AudioClip> { { "hit", loud }, { "hit_2", half } });
            var gains = new Dictionary<AudioClip, float>();
            for (int i = 0; i < 40; i++)
            {
                var clip = Sfx.ClipFor("hit", out float gain, out _);
                Assert.That(clip == loud || clip == half, "only the two takes play");
                gains[clip] = gain;
            }
            Assert.AreEqual(2, gains.Count, "both takes played in 40 plays");
            Assert.That(gains[loud], Is.LessThan(0.5f), "the half take must stay under the clamp for this check");
            Assert.That(gains[half], Is.EqualTo(gains[loud] * 2).Within(0.001f));
        }

        [Test]
        public void NeverTheSameTakeTwiceInARow()
        {
            var a = Tone("test_slash_a", 0.9f);
            var b = Tone("test_slash_b", 0.8f);
            var c = Tone("test_slash_c", 0.7f);
            Serve(new Dictionary<string, AudioClip> { { "slash", a }, { "slash_2", b }, { "slash_3", c } });
            var seen = new HashSet<AudioClip>();
            AudioClip last = null;
            for (int i = 0; i < 200; i++)
            {
                var clip = Sfx.ClipFor("slash", out _, out _);
                Assert.AreNotSame(last, clip, "play " + i + " repeated the take before it");
                seen.Add(clip);
                last = clip;
            }
            Assert.AreEqual(3, seen.Count, "all three takes played in 200 plays");
        }

        [Test]
        public void AMissingTakeStopsTheLookup()
        {
            var first = Tone("test_block_1", 0.9f);
            var third = Tone("test_block_3", 0.9f);
            var asked = Serve(new Dictionary<string, AudioClip> { { "block", first }, { "block_3", third } });
            for (int i = 0; i < 20; i++) Assert.AreSame(first, Sfx.ClipFor("block", out _, out _));
            CollectionAssert.AreEqual(new[] { "block", "block_2" }, asked, "block_3 is never asked for");
        }

        [Test]
        public void ADestroyedTakeIsSkipped()
        {
            var a = Tone("test_coin_a", 0.9f);
            var b = Tone("test_coin_b", 0.9f);
            Serve(new Dictionary<string, AudioClip> { { "coin", a }, { "coin_2", b } });
            var played = Sfx.ClipFor("coin", out _, out _);
            // The take that just played survives: it plays again and again, never the synth.
            UnityEngine.Object.DestroyImmediate(played == a ? b : a);
            for (int i = 0; i < 10; i++) Assert.AreSame(played, Sfx.ClipFor("coin", out _, out _));
        }

        [Test]
        public void TheTakePlayedLastGoingAwayLeavesTheOthers()
        {
            var a = Tone("test_miss_a", 0.9f);
            var b = Tone("test_miss_b", 0.9f);
            var c = Tone("test_miss_c", 0.9f);
            Serve(new Dictionary<string, AudioClip> { { "miss", a }, { "miss_2", b }, { "miss_3", c } });
            var played = Sfx.ClipFor("miss", out _, out _);
            UnityEngine.Object.DestroyImmediate(played);
            var left = new HashSet<AudioClip>();
            AudioClip last = null;
            for (int i = 0; i < 60; i++)
            {
                var clip = Sfx.ClipFor("miss", out _, out _);
                Assert.IsNotNull(clip);
                Assert.That(clip == a || clip == b || clip == c, "a take plays, not the synth");
                Assert.AreNotSame(last, clip);
                left.Add(clip);
                last = clip;
            }
            Assert.AreEqual(2, left.Count, "both other takes play");
        }

        [Test]
        public void AllTakesDestroyedFallsBackToTheSynth()
        {
            var a = Tone("test_heal_a", 0.9f);
            var b = Tone("test_heal_b", 0.9f);
            Serve(new Dictionary<string, AudioClip> { { "heal", a }, { "heal_2", b } });
            Sfx.ClipFor("heal", out _, out _);
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
            var clip = Sfx.ClipFor("heal", out float gain, out float pitch);
            Assert.IsNotNull(clip);
            StringAssert.StartsWith("sfx_heal", clip.name);
            Assert.AreEqual(1f, gain);
            Assert.AreEqual(1f, pitch);
        }
    }
}
