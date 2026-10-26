using System;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    public class DayLightTests
    {
        [Test]
        public void Afternoon_ReproducesTheSceneLighting()
        {
            DayLightState s = DayLight.Evaluate(DayLight.AfternoonHour);
            Assert.AreEqual(25f, s.Pitch, 1e-3f);
            Assert.AreEqual(62f, s.Yaw, 1e-3f);
            Assert.AreEqual(2.2f, s.Intensity, 1e-3f);
            Assert.AreEqual(1f, s.Colour.X, 1e-3f);
            Assert.AreEqual(0.85f, s.Colour.Y, 1e-3f);
            Assert.AreEqual(0.70f, s.Colour.Z, 1e-3f);
            Assert.AreEqual(1f, s.AmbientScale, 1e-3f);
            Assert.IsFalse(s.LampsOn);
            Assert.IsFalse(s.IsNight);
        }

        [Test]
        public void Sun_RisesAt6_PeaksAtNoon_SetsAt18()
        {
            Assert.AreEqual(0f, DayLight.SunElevation(6f), 1e-3f);
            Assert.AreEqual(0f, DayLight.SunElevation(18f), 1e-3f);
            Assert.AreEqual(DayLight.NoonElevation, DayLight.SunElevation(12f), 1e-3f);
            Assert.That(DayLight.NoonElevation, Is.InRange(60f, 70f), "Medellín, near the equator: high noon sun");
            Assert.Less(DayLight.SunElevation(22f), 0f);
            Assert.AreEqual(DayLight.SunElevation(16.5f), DayLight.SunElevation(16.5f + 24f), 1e-3f, "hours wrap around");
        }

        [Test]
        public void Dusk_GetsWarmerAndDarker_LampsOnBeforeSunset()
        {
            DayLightState afternoon = DayLight.Evaluate(16.5f);
            DayLightState dusk = DayLight.Evaluate(17.7f);
            Assert.Less(dusk.Intensity, afternoon.Intensity);
            Assert.Less(dusk.Colour.Z, afternoon.Colour.Z, "more orange (less blue)");
            Assert.Less(dusk.AmbientScale, afternoon.AmbientScale);
            Assert.IsTrue(dusk.LampsOn);
            Assert.IsFalse(dusk.IsNight);

            // The light fades smoothly into the night, without jumps.
            float previous = afternoon.Intensity;
            for (float h = 16.5f; h <= 19f; h += 0.05f)
            {
                float now = DayLight.Evaluate(h).Intensity;
                Assert.LessOrEqual(now, previous + 1e-4f);
                Assert.Less(Math.Abs(now - previous), 0.25f, $"jump at {h:F2} h");
                previous = now;
            }
        }

        [Test]
        public void Night_HasADimMoon_AndLampsOn()
        {
            DayLightState night = DayLight.Evaluate(22f);
            Assert.IsTrue(night.IsNight);
            Assert.IsTrue(night.LampsOn);
            Assert.That(night.Intensity, Is.InRange(0.05f, 0.3f));
            Assert.Greater(night.Colour.Z, night.Colour.X, "bluish");
            Assert.That(night.AmbientScale, Is.InRange(0.1f, 0.3f));
            Assert.IsFalse(DayLight.Evaluate(10f).LampsOn, "morning");
        }

        [Test]
        public void Lamps_LineTheAvenues_OnBothKerbs_AwayFromCrossings()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(true, RoadClass.Avenue, RoadClass.Street));
            var lamps = StreetLampLayout.Place(network.Lanes, 30f);

            Assert.Greater(lamps.Count, 20);
            Assert.IsTrue(lamps.All(l => Math.Abs(l.Position.X) >= StreetLampLayout.EndClearance - 0.01f), "no lamp in the crossing");
            Assert.IsTrue(lamps.All(l => Math.Abs(Math.Abs(l.Position.Y) - (6f + StreetLampLayout.KerbMargin)) < 0.01f),
                "on the kerbs of the east-west avenue only (the street gets none)");
            Assert.IsTrue(lamps.Any(l => l.Position.Y > 0f) && lamps.Any(l => l.Position.Y < 0f), "both sides");
            Assert.IsTrue(lamps.All(l => Vector2.Dot(l.Facing, -Vector2.Normalize(new Vector2(0f, l.Position.Y))) > 0.99f),
                "arms reach over the road");
        }

        [Test]
        public void Lamps_LeaveRoomForBusStops()
        {
            var definition = TestNetworks.StreetWithBusStop();
            RoadNetwork street = RoadNetwork.Build(definition);
            Assert.AreEqual(0, StreetLampLayout.Place(street.Lanes, 10f).Count, "plain streets have no lamps");

            var avenue = new RoadNetworkDefinition();
            int west = avenue.AddNode(new Vector2(-400f, 0f), RoadNodeKind.Boundary);
            int east = avenue.AddNode(new Vector2(400f, 0f), RoadNodeKind.Boundary);
            avenue.AddRoad(west, east, RoadClass.Avenue, true, 6f, 14f);
            avenue.BusStops.Add(new LaneMarkerDefinition(Vector2.Zero, new Vector2(1f, 0f)));
            RoadNetwork network = RoadNetwork.Build(avenue);
            TrafficLane eastbound = TestNetworks.LaneHeading(network, new Vector2(1f, 0f), Vector2.Zero);
            Assert.AreEqual(1, eastbound.BusStops.Count);
            float stopX = eastbound.PointAt(eastbound.BusStops[0]).X;

            var lamps = StreetLampLayout.Place(new[] { eastbound }, 5f);
            Assert.Greater(lamps.Count, 100);
            Assert.IsTrue(lamps.All(l => Math.Abs(l.Position.X - stopX) >= StreetLampLayout.BusStopClearance - 0.01f),
                "the shelter's spot stays free");
        }
    }
}
