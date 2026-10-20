using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>The CEDI trucks and the city traffic share the highway.</summary>
    public class SharedRoadTests
    {
        private const float Step = 0.1f;

        [TestCase(7)]
        [TestCase(31)]
        public void CediTrucksAndTraffic_NeverTouch_TrucksStopAtRedLights_AndTheCediKeepsWorking(int seed)
        {
            OperationState state = SampleOperationData.Create();
            var cedi = new CediOperationSimulation(state, seed: seed);
            RoadNetwork network = RoadNetwork.Build(TestNetworks.City(), seed);
            var traffic = new AmbientTraffic(network, new AmbientTrafficSettings { VehicleCount = 90 }, seed);
            traffic.SharedVehicles = cedi;
            cedi.SharedRoad = traffic;

            // The westbound stop lines of the lights at x = 150 and x = -150.
            var lights = new[] { 150f, -150f }
                .Select(x => network.FindLane(new Vector2(x + 20f, CityRoadLayout.HighwayZ + 3f), new Vector2(-1f, 0f)))
                .ToList();
            var previousFront = new Dictionary<VehicleAgent, float>();
            int redCrossings = 0, overtakes = 0;

            for (int i = 0; i < 900f / Step; i++)
            {
                traffic.Tick(Step, new Vector2(0f, -40f), 520f);
                cedi.Tick(Step, cedi.Time);

                var bodies = traffic.Vehicles.Select(v => v.Footprint)
                    .Concat(cedi.Vehicles.Where(v => v.FloorHeight < 0.01f).Select(v => v.Footprint)).ToList();
                if (i % 2 == 0) AmbientTrafficTests.AssertApart(bodies, $"t = {cedi.Time:F1}");
                overtakes += traffic.Vehicles.Count(v => v.Offset < -4f && System.Math.Abs(v.Position.X) < 40f);

                foreach (VehicleAgent truck in cedi.Vehicles.Where(v => v.Kind != VehicleKind.Forklift))
                {
                    float front = truck.Position.X + truck.Heading.X * truck.Length / 2f;
                    if (truck.Heading.X < -0.9f && System.Math.Abs(truck.Position.Y - (CityRoadLayout.HighwayZ + 3f)) < 1.5f
                        && previousFront.TryGetValue(truck, out float before))
                        foreach (TrafficLane lane in lights)
                        {
                            float line = lane.End.X;
                            if (before > line && front <= line && traffic.AspectFor(lane) == SignalAspect.Red) redCrossings++;
                        }
                    previousFront[truck] = front;
                }
            }

            Assert.AreEqual(0, redCrossings, "CEDI trucks stop at red lights");
            Assert.GreaterOrEqual(cedi.TrucksExited, 6, "trucks still come and go");
            Assert.AreEqual(0, traffic.StuckRemovals);
            Assert.Greater(traffic.Vehicles.Count, 80);
            TestContext.WriteLine($"overtaking ticks near the gate: {overtakes}");
        }
    }
}
