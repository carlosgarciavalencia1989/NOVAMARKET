using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>People on sidewalks, park paths and zebra crossings, with and without traffic.</summary>
    public class PedestrianTests
    {
        private const float Step = 0.1f;

        [Test]
        public void WalkLayout_HasSidewalksParkPathsAndCrossings_AllConnected()
        {
            WalkNetwork walks = WalkNetwork.Build(CityWalkLayout.Build(TestNetworks.CityBlocks()));

            Assert.Greater(walks.Edges.Count(e => e.Kind == WalkEdgeKind.Crosswalk), 20);
            Assert.Greater(walks.Edges.Count(e => e.Kind == WalkEdgeKind.ParkPath), 0);
            foreach (WalkEdge crossing in walks.Edges.Where(e => e.IsCrosswalk))
            {
                Assert.AreEqual(14.5f, crossing.Length, 0.01f, "street (12 m) plus a sidewalk path inset on each side");
                Assert.Greater(crossing.A.Edges.Count, 1, "a crossing starts on a sidewalk");
                Assert.Greater(crossing.B.Edges.Count, 1);
            }

            // The highway and the river split the valley: each side is one network of sidewalks and crossings.
            foreach (bool north in new[] { true, false })
            {
                List<WalkNode> side = walks.Nodes.Where(n => n.Position.Y > -100f == north).ToList();
                var seen = new HashSet<WalkNode> { side[0] };
                var queue = new Queue<WalkNode>(seen);
                while (queue.Count > 0)
                {
                    WalkNode node = queue.Dequeue();
                    foreach (WalkEdge edge in node.Edges)
                        if (seen.Add(edge.Other(node))) queue.Enqueue(edge.Other(node));
                }
                Assert.AreEqual(side.Count, seen.Count, north ? "north side" : "south side");
            }
        }

        [Test]
        public void Crossings_KnowTheLanesTheyCross_AndThoseLanesStopBeforeThem()
        {
            RoadNetwork roads = RoadNetwork.Build(TestNetworks.City());
            WalkNetwork walks = WalkNetwork.Build(CityWalkLayout.Build(TestNetworks.CityBlocks()), roads);

            List<WalkEdge> crossings = walks.Edges.Where(e => e.IsCrosswalk).ToList();
            Assert.IsTrue(crossings.All(c => c.ArrivingLanes.Count + c.LeavingLanes.Count >= 1), "every crossing spans a street");
            foreach (WalkEdge crossing in crossings)
            foreach (TrafficLane lane in crossing.ArrivingLanes)
            {
                Vector2 centre = (crossing.A.Position + crossing.B.Position) / 2f;
                float s = lane.Project(centre, out _);
                Assert.LessOrEqual(lane.Length - lane.StopLineBack, s - CityWalkLayout.CrosswalkWidth / 2f + 0.01f,
                    "the stop line is before the zebra");
            }
            Assert.IsTrue(crossings.Any(c => c.Signal != null), "some crossings have lights");
        }

        [Test]
        public void People_StayOnTheirPaths_WithoutTraffic()
        {
            WalkNetwork walks = WalkNetwork.Build(CityWalkLayout.Build(TestNetworks.CityBlocks()));
            var people = new AmbientPedestrians(walks, null, new PedestrianSettings { Count = 120 }, 3);
            for (int i = 0; i < 300f / Step; i++)
            {
                people.Tick(Step, new Vector2(0f, -100f), 400f);
                foreach (Pedestrian p in people.People)
                {
                    Assert.That(p.Height, Is.InRange(0f, 0.25f));
                    Assert.LessOrEqual(p.Speed, 2f);
                }
            }
            Assert.Greater(people.People.Count, 100);
            Assert.Greater(people.People.Count(p => p.Speed > 0.5f), 60, "most are walking");
        }

        [TestCase(5)]
        [TestCase(17)]
        public void People_CrossOnRedForTheCars_AndVehiclesNeverTouchThem(int seed)
        {
            RoadNetwork roads = RoadNetwork.Build(TestNetworks.City(), seed);
            var traffic = new AmbientTraffic(roads, new AmbientTrafficSettings { VehicleCount = 100 }, seed);
            WalkNetwork walks = WalkNetwork.Build(CityWalkLayout.Build(TestNetworks.CityBlocks()), roads);
            var people = new AmbientPedestrians(walks, traffic, new PedestrianSettings { Count = 160 }, seed);
            traffic.SharedPedestrians = people;

            int crossings = 0;
            people.StartedCrossing += (p, crossing) =>
            {
                crossings++;
                if (crossing.Signal != null)
                    Assert.AreEqual(SignalAspect.Red, crossing.Signal.AspectFor(crossing.CrossedGroup, traffic.Time),
                        "people cross while the crossed street has red");
            };

            var focus = new Vector2(0f, -120f);
            for (int i = 0; i < 300f / Step; i++)
            {
                traffic.Tick(Step, focus, 600f);
                people.Tick(Step, focus, 350f);
                foreach (Pedestrian p in people.People.Where(p => p.State == PedestrianState.Crossing))
                foreach (AmbientVehicle v in traffic.Vehicles)
                {
                    if (Vector2.DistanceSquared(v.Position, p.Position) > 225f) continue;
                    Assert.Greater(v.Footprint.Separation(p.Footprint), 0f, $"t = {traffic.Time:F1}: {v} touches {p}");
                }
            }
            Assert.Greater(crossings, 40, "people do cross the streets");
            Assert.AreEqual(0, traffic.StuckRemovals, "waiting for people never jams the traffic");
        }
    }
}
