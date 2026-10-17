using Drakepg.Rules;
using NUnit.Framework;

namespace Drakepg.Tests
{
    /// <summary>Values printed by the TypeScript `seeded()` in src/game/rng.ts (node, 2026-10-17).</summary>
    public class RngTests
    {
        [Test]
        public void Mulberry32MatchesTypeScript()
        {
            var expected = new[] { 0.6011037519201636, 0.44829055899754167, 0.8524657934904099, 0.6697340414393693, 0.17481389874592423 };
            var rng = new Mulberry32(42);
            foreach (var value in expected) Assert.AreEqual(value, rng.Next(), 0.0);
        }

        [Test]
        public void RandIntMatchesTypeScript()
        {
            var expected = new[] { 1, 1, 6, 5, 4, 3, 3, 2 };
            var rng = new Mulberry32(7);
            foreach (var value in expected) Assert.AreEqual(value, rng.RandInt(1, 6));
        }
    }
}
