using System.Numerics;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class SmokeRulesTests
    {
        [Test]
        public void Chimney_WorksInSlowCycles_Between35And100Percent()
        {
            var source = new SmokeSource(new Vector3(0f, 20f, 0f), 1f, 0f);
            Assert.AreEqual(0.675f, SmokeRules.ChimneyOutput(source, 0f), 1e-4f);
            Assert.AreEqual(1f, SmokeRules.ChimneyOutput(source, SmokeRules.CycleSeconds / 4f), 1e-4f);
            Assert.AreEqual(0.35f, SmokeRules.ChimneyOutput(source, SmokeRules.CycleSeconds * 3f / 4f), 1e-4f);

            var strong = new SmokeSource(Vector3.Zero, 1.2f, 0f);
            Assert.AreEqual(1.2f, SmokeRules.ChimneyOutput(strong, SmokeRules.CycleSeconds / 4f), 1e-4f);
        }

        [Test]
        public void Drift_FollowsTheWind_AndGrowsWithItsStrengthAndGusts()
        {
            var west = new Vector2(-1f, 0f);
            Vector2 breeze = SmokeRules.Drift(west, 0.35f, 0.5f);
            Assert.AreEqual(-2f, breeze.X, 1e-4f, "default breeze: 2 m/s");
            Assert.AreEqual(0f, breeze.Y, 1e-4f);
            Assert.Greater(SmokeRules.Drift(west, 0.35f, 1f).Length(), breeze.Length(), "gusts carry it faster");
            Assert.Greater(SmokeRules.Drift(west, 1f, 0.5f).Length(), breeze.Length(), "stronger wind");
            Assert.AreEqual(Vector2.Zero, SmokeRules.Drift(west, 0f, 1f), "calm: the smoke rises straight");
            Assert.AreEqual(Vector2.Zero, SmokeRules.Drift(Vector2.Zero, 0.35f, 0.5f));
        }

        [Test]
        public void Exhaust_OnlyDiesel_BlackWhenPullingAway_FaintWhenIdle()
        {
            Assert.AreEqual(0f, SmokeRules.Exhaust(AmbientVehicleKind.Car, 2f, 0f, 0.05f).Rate);
            Assert.AreEqual(0f, SmokeRules.Exhaust(AmbientVehicleKind.Moto, 2f, 0f, 0.05f).Rate);

            ExhaustPuff pullAway = SmokeRules.Exhaust(AmbientVehicleKind.Bus, 2f, 1.93f, 0.05f);   // 1.4 m/s²
            ExhaustPuff idle = SmokeRules.Exhaust(AmbientVehicleKind.Bus, 0f, 0f, 0.05f);
            ExhaustPuff cruise = SmokeRules.Exhaust(AmbientVehicleKind.Truck, 12f, 12f, 0.05f);
            Assert.Greater(pullAway.Rate, idle.Rate);
            Assert.Greater(idle.Rate, cruise.Rate);
            Assert.Greater(pullAway.Darkness, idle.Darkness);
            Assert.Greater(cruise.Rate, 0f);

            ExhaustPuff fastAcceleration = SmokeRules.Exhaust(AmbientVehicleKind.Truck, 10f, 9.9f, 0.05f);
            Assert.Less(fastAcceleration.Darkness, pullAway.Darkness, "only the start from low speed is black");
        }

        [Test]
        public void Emit_KeepsTheFraction_SoLowRatesStillPuff()
        {
            float carry = 0f;
            int total = 0;
            for (int i = 0; i < 80; i++) total += SmokeRules.Emit(ref carry, 0.5f, 0.125f); // 10 s at 0.5 puffs/s
            Assert.AreEqual(5, total);
            Assert.AreEqual(0, SmokeRules.Emit(ref carry, 0f, 0.125f));
            Assert.AreEqual(2, SmokeRules.Emit(ref carry, 8f, 0.25f));
        }
    }
}
