using System.Collections.Generic;
using System.Numerics;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    /// <summary>Checks that every truck and forklift route stays clear of buildings, racks and stations.</summary>
    public class CediLayoutTests
    {
        private const float TruckLength = 16f;
        private const float TurboLength = 9f;

        private static IEnumerable<Polyline> AllTruckRoutes()
        {
            var docks = new DockManager();
            foreach (float length in new[] { TruckLength, TurboLength })
            {
                yield return CediLayout.Approach(true, length);
                yield return CediLayout.Approach(false, length);
                yield return CediLayout.Exit(true, length);
                yield return CediLayout.Exit(false, length);
                foreach (Dock dock in docks.Docks)
                {
                    yield return CediLayout.ToStaging(dock, length);
                    yield return CediLayout.FromDock(dock, length);
                }
            }
        }

        [Test]
        public void TruckRoutes_NeverCrossTheWarehouseOrTheOffice()
        {
            foreach (Polyline route in AllTruckRoutes())
                AssertClear(route, CediLayout.TruckObstacles, margin: 1.2f);
        }

        [Test]
        public void DockedTruck_TouchesTheBumpersWithItsRear()
        {
            var docks = new DockManager();
            foreach (Dock dock in docks.Docks)
            {
                Polyline reverse = CediLayout.ReverseIntoDock(dock, TruckLength);
                float rearX = reverse.End.X - System.Math.Sign(reverse.End.X) * TruckLength / 2f;
                Assert.AreEqual(dock.WallX, rearX, 0.01f, $"dock {dock.Number}");
                Assert.AreEqual(dock.Z, reverse.End.Y, 0.01f);
                Assert.AreEqual(reverse.Start, CediLayout.ToStaging(dock, TruckLength).End,
                    "reversing starts where the yard route ends");
            }
        }

        [Test]
        public void GateStops_PutTheFrontOnTheStopLine()
        {
            Assert.AreEqual(CediLayout.InboundStopLineZ, CediLayout.InboundStop(TruckLength).Y + TruckLength / 2f, 0.01f);
            Assert.AreEqual(CediLayout.OutboundStopLineZ, CediLayout.OutboundStop(TurboLength).Y - TurboLength / 2f, 0.01f);
            Assert.AreEqual(CediLayout.InboundStop(TruckLength), CediLayout.Approach(true, TruckLength).End);
            Assert.AreEqual(CediLayout.OutboundStop(TruckLength), CediLayout.FromDock(new DockManager().Docks[0], TruckLength).End);
        }

        [Test]
        public void ForkliftRoutes_ConnectEverySpotWithoutCrossingRacks()
        {
            var spots = new List<ForkliftSpot>();
            for (int lane = 0; lane < 3; lane++)
            {
                spots.Add(ForkliftNetwork.ReceivingStaging(lane));
                spots.Add(ForkliftNetwork.ShippingStaging(lane));
                spots.Add(ForkliftNetwork.Park(lane));
            }
            for (int aisle = 0; aisle < ForkliftNetwork.AisleZ.Length; aisle++)
            {
                spots.Add(ForkliftNetwork.RackSlot(aisle, ForkliftNetwork.RackMinX, true));
                spots.Add(ForkliftNetwork.RackSlot(aisle, ForkliftNetwork.RackMaxX, false));
            }

            foreach (ForkliftSpot from in spots)
            foreach (ForkliftSpot to in spots)
            {
                foreach (RouteLeg leg in ForkliftNetwork.Route(from, to, 3f))
                {
                    AssertClear(leg.Path, ForkliftNetwork.Obstacles, margin: 0.4f);
                    foreach (Vector2 point in leg.Path.Points)
                        Assert.That(point.X, Is.InRange(-21.5f, 21.5f), "forklifts stay inside the warehouse");
                }
            }
        }

        [Test]
        public void RackSlots_FaceTheirRack()
        {
            ForkliftSpot north = ForkliftNetwork.RackSlot(2, 0f, northSide: true);
            Assert.AreEqual(0f, north.Access.Y, 0.01f);
            Assert.Greater(north.Point.Y, north.Access.Y);
            StringAssert.Contains("R3", north.Label);
            StringAssert.Contains("R1", ForkliftNetwork.RackSlot(0, 0f, northSide: false).Label,
                "the southern aisle only has rack 1");
        }

        /// <summary>Samples the route every 0.5 m and fails if a point enters an obstacle grown by the margin.</summary>
        private static void AssertClear(Polyline route, IReadOnlyList<(Vector2 min, Vector2 max)> obstacles, float margin)
        {
            for (float d = 0f; d <= route.Length; d += 0.5f)
            {
                Vector2 p = route.PointAt(d);
                foreach ((Vector2 min, Vector2 max) in obstacles)
                {
                    bool inside = p.X > min.X - margin && p.X < max.X + margin && p.Y > min.Y - margin && p.Y < max.Y + margin;
                    Assert.IsFalse(inside, $"route point {p} enters obstacle {min}..{max}");
                }
            }
        }
    }
}
