using System.Numerics;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class WindModelTests
    {
        [Test]
        public void Wind_HasGustsAndLulls_ChangesSmoothly_AndKeepsItsDirection()
        {
            var wind = new WindModel(250f, 0.35f, 3);
            float lowest = 1f, highest = 0f, previous = wind.Gust;
            for (int i = 0; i < 6000; i++)
            {
                wind.Tick(0.05f);
                Assert.That(wind.Gust, Is.InRange(0f, 1f));
                Assert.Less(System.Math.Abs(wind.Gust - previous), 0.05f, "no sudden jumps");
                previous = wind.Gust;
                lowest = System.Math.Min(lowest, wind.Gust);
                highest = System.Math.Max(highest, wind.Gust);
                Assert.AreEqual(1f, wind.Direction.Length(), 1e-4f);
                // 250° = towards the west-southwest: mostly -X, a bit -Z; it wanders at most 12°.
                Assert.Greater(Vector2.Dot(wind.Direction, new Vector2(-0.94f, -0.34f)), 0.97f);
            }
            Assert.Less(lowest, 0.15f, "lulls");
            Assert.Greater(highest, 0.6f, "gusts");
        }
    }
}
