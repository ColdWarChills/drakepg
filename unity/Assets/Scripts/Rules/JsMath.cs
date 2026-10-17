using System;

namespace Drakepg.Rules
{
    /// <summary>
    /// JavaScript's number rules where C# differs. `Math.round` in JS rounds halves up (2.5 -> 3, -2.5 -> -2); C#'s
    /// Math.Round rounds them to even. Every rounding in the port goes through `Round` so results match the TypeScript.
    /// </summary>
    public static class JsMath
    {
        public static double Round(double x) => Math.Floor(x + 0.5);
        public static int RoundInt(double x) => (int)Math.Floor(x + 0.5);
        public static double Clamp(double v, double min, double max) => Math.Min(max, Math.Max(min, v));

        /// <summary>A number printed like JavaScript's String(n): the shortest digits that read back the same.</summary>
        public static string Str(double v)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(inv);
            for (int digits = 15; digits <= 17; digits++)
            {
                var s = v.ToString("G" + digits, inv);
                if (double.Parse(s, inv) == v) return s;
            }
            return v.ToString("R", inv);
        }
    }
}
