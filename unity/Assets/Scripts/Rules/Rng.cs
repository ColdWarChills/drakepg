using System;
using System.Collections.Generic;

namespace Drakepg.Rules
{
    /// <summary>Random source returning a double in [0, 1). Port of src/game/rng.ts.</summary>
    public interface IRng
    {
        double Next();
    }

    public sealed class SystemRng : IRng
    {
        readonly Random random;
        public SystemRng(int? seed = null) => random = seed.HasValue ? new Random(seed.Value) : new Random();
        public double Next() => random.NextDouble();
    }

    /// <summary>Mulberry32, bit for bit the same as `seeded()` in rng.ts, so seeded fights match the TypeScript ones.</summary>
    public sealed class Mulberry32 : IRng
    {
        uint a;
        public Mulberry32(uint seed) => a = seed;

        public double Next()
        {
            unchecked
            {
                a += 0x6d2b79f5;
                uint t = a;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    public static class RngExt
    {
        /// <summary>Inclusive on both ends.</summary>
        public static int RandInt(this IRng rng, int min, int max) => min + (int)Math.Floor(rng.Next() * (max - min + 1));

        public static bool Chance(this IRng rng, double p) => rng.Next() < p;

        public static T WeightedPick<T>(this IRng rng, IReadOnlyList<T> entries, Func<T, double> weight)
        {
            if (entries.Count == 0) throw new ArgumentException("WeightedPick: empty list");
            double total = 0;
            foreach (var e in entries) total += Math.Max(0, weight(e));
            double roll = rng.Next() * total;
            foreach (var e in entries)
            {
                roll -= Math.Max(0, weight(e));
                if (roll < 0) return e;
            }
            return entries[entries.Count - 1];
        }
    }
}
