using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    /// <summary>Runs the whole operation with a fixed seed and checks its rules and its effect on the HUD state.</summary>
    public class CediOperationSimulationTests
    {
        private const float Step = 0.05f;

        private OperationState state;
        private CediOperationSimulation simulation;
        private List<string> notices;

        [SetUp]
        public void SetUp()
        {
            state = SampleOperationData.Create();
            notices = new List<string>();
            state.Notifications.Added += n => notices.Add(n.Message);
            simulation = new CediOperationSimulation(state, seed: 7);
        }

        /// <summary>Runs for some seconds, checking invariants after every step.</summary>
        private void Run(float seconds)
        {
            int steps = (int)(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                simulation.Tick(Step, simulation.Time);
                AssertInvariants();
            }
        }

        private void AssertInvariants()
        {
            var trucks = simulation.Vehicles.OfType<TruckAgent>().ToList();

            // One truck per dock, and docks point back to their truck.
            foreach (Dock dock in simulation.Docks.Docks.Where(d => !d.IsFree))
            {
                Assert.AreSame(dock, dock.Occupant.Dock);
                Assert.AreEqual(dock.Kind == DockKind.Receiving, dock.Occupant.IsReceiving, "trucks use docks of their kind");
            }
            Assert.AreEqual(trucks.Count(t => t.IsInYard), state.Resources.TrucksInYard);

            Assert.That(state.Resources.OccupiedPositions, Is.InRange(0, state.Resources.PositionCapacity));
            foreach (int staged in simulation.ReceivingStaged.Concat(simulation.ShippingStaged))
                Assert.That(staged, Is.InRange(0, simulation.Settings.StagingCapacity));
        }

        [Test]
        public void FirstTruck_IsDonHernanAndAnnouncesItselfAtTheGate()
        {
            Run(40f);

            Assert.Contains("Camión de Don Hernán en portería.", notices);
            TruckAgent first = simulation.Vehicles.OfType<TruckAgent>().First(t => t.IsReceiving);
            Assert.AreEqual(CediCatalog.DonHernan, first.Partner);
        }

        [Test]
        public void Barrier_OpensOnlyForATruckCrossingIt()
        {
            bool sawOpen = false;
            for (int i = 0; i < 40f / Step; i++)
            {
                simulation.Tick(Step, simulation.Time);
                if (!simulation.InboundGate.IsBarrierOpen) continue;
                sawOpen = true;
                Assert.IsTrue(simulation.Vehicles.OfType<TruckAgent>().Any(t => t.State == TruckState.Entering),
                    "the barrier is up only while a truck drives through");
            }
            Assert.IsTrue(sawOpen, "the first truck should have entered within 40 s");
        }

        [Test]
        public void ReceivingTruck_ReversesToADockUnloadsAndLeaves()
        {
            TruckAgent truck = null;
            var seen = new HashSet<TruckState>();
            for (int i = 0; i < 240f / Step; i++)
            {
                simulation.Tick(Step, simulation.Time);
                truck ??= simulation.Vehicles.OfType<TruckAgent>().FirstOrDefault(t => t.IsReceiving);
                if (truck == null) continue;
                seen.Add(truck.State);
                if (truck.State == TruckState.Gone) break;
            }

            CollectionAssert.IsSupersetOf(seen, new[]
            {
                TruckState.Approaching, TruckState.AtGate, TruckState.Entering, TruckState.Reversing,
                TruckState.Docked, TruckState.Leaving, TruckState.ExitCheck, TruckState.Exiting, TruckState.Gone
            });
            Assert.AreEqual(truck.Pallets, truck.PalletsHandled);
            Assert.IsFalse(simulation.Vehicles.Contains(truck), "finished trucks are removed");
            Assert.IsTrue(state.Mission.Find("import-container").IsCompleted, "Don Hernán's container counts for the objective");
        }

        [Test]
        public void Forklifts_PutReceivedPalletsAwayIntoTheRacks()
        {
            // Receiving only, so no dispatch takes pallets out of the racks meanwhile.
            var settings = new CediOperationSettings { FirstDispatchAt = 1e6f, ReceivingInterval = 1e6f };
            simulation = new CediOperationSimulation(state, settings, seed: 7);
            int start = state.Resources.OccupiedPositions;

            TruckAgent truck = null;
            for (int i = 0; i < 150f / Step; i++)
            {
                simulation.Tick(Step, simulation.Time);
                truck ??= simulation.Vehicles.OfType<TruckAgent>().FirstOrDefault();
            }

            int unloaded = truck.PalletsHandled;
            int stillStaged = simulation.ReceivingStaged.Sum();
            Assert.Greater(state.Resources.OccupiedPositions, start, "put-away adds occupied positions");
            Assert.AreEqual(start + unloaded - stillStaged - CarriedPallets(), state.Resources.OccupiedPositions,
                "every unloaded pallet is either staged, on a forklift or in a rack");
        }

        private int CarriedPallets() => simulation.Vehicles.OfType<ForkliftAgent>().Count(f => f.IsCarrying);

        [Test]
        public void Dispatches_CompleteAndUpdateTheMission()
        {
            Run(400f);

            Assert.Greater(state.Mission.CounterValue, 0, "turbos left loaded");
            Assert.IsTrue(notices.Any(n => n.StartsWith("Despacho a ") && n.EndsWith(" en camino.")));
            Assert.IsTrue(notices.Any(n => n.StartsWith("Despacho completado")));
            Assert.Greater(simulation.TrucksExited, 0);
        }

        /// <summary>
        /// 30 minutes of operation: no two vehicles on the same floor ever touch (trucks in the yard,
        /// forklifts in the aisles), and nobody waits in traffic for more than a minute (no gridlock).
        /// </summary>
        [TestCase(7)]
        [TestCase(11)]
        [TestCase(23)]
        public void Vehicles_NeverOverlap_AndNeverWaitForever(int seed)
        {
            simulation = new CediOperationSimulation(state, seed: seed);
            var waiting = new Dictionary<VehicleAgent, float>();
            for (int i = 0; i < (int)(1800f / Step); i++)
            {
                simulation.Tick(Step, simulation.Time);
                IReadOnlyList<VehicleAgent> vehicles = simulation.Vehicles;
                for (int a = 0; a < vehicles.Count; a++)
                {
                    VehicleAgent v = vehicles[a];
                    waiting[v] = v.IsWaitingInTraffic ? (waiting.TryGetValue(v, out float w) ? w : 0f) + Step : 0f;
                    Assert.Less(waiting[v], 60f, $"{v.Plate} ({v.Status}) has been waiting a minute at {v.Position}. " +
                                       simulation.TrafficReport());
                    for (int b = a + 1; b < vehicles.Count; b++)
                    {
                        VehicleAgent o = vehicles[b];
                        if (System.Math.Abs(v.FloorHeight - o.FloorHeight) > 0.01f) continue;
                        Assert.IsFalse(v.Footprint.Overlaps(o.Footprint),
                            $"{simulation.Time:F1}s: {v.Plate} ({v.Status}) and {o.Plate} ({o.Status}) overlap at {v.Position} / {o.Position}");
                    }
                }
            }
        }

        [Test]
        public void TrucksOnOneSide_TakeTurnsToManoeuvre()
        {
            int samples = 0;
            for (int i = 0; i < (int)(1200f / Step); i++)
            {
                simulation.Tick(Step, simulation.Time);
                foreach (DockKind side in new[] { DockKind.Receiving, DockKind.Shipping })
                {
                    var moving = simulation.Vehicles.OfType<TruckAgent>().Where(t => t.Dock?.Kind == side &&
                        (t.State == TruckState.Reversing || t.State == TruckState.Leaving && t.Position.Y > -16f && System.Math.Abs(t.Position.X) > 30f)).ToList();
                    Assert.LessOrEqual(moving.Count, 1, $"{simulation.Time:F0}s: two trucks manoeuvre at the {side} docks");
                    if (moving.Count == 1)
                    {
                        Assert.AreSame(moving[0], simulation.ManoeuvreHolder(side));
                        samples++;
                    }
                }
            }
            Assert.Greater(samples, 100, "trucks did manoeuvre");
        }

        [Test]
        public void Operation_KeepsUpWithArrivalsWithoutGridlock()
        {
            Run(1800f);

            // With real traffic (one-way aisles, crossings, safety gaps) four forklifts move about one
            // pallet every 4 s: dispatches keep completing and the gate keeps flowing.
            Assert.GreaterOrEqual(state.Mission.CounterValue, 8, "the 8 dispatches of the mission are done in 30 min");
            Assert.GreaterOrEqual(simulation.TrucksExited, 20, "trucks keep flowing through the gate");
            Assert.LessOrEqual(simulation.Vehicles.OfType<TruckAgent>().Count(), 9, "no growing backlog of trucks");
        }

        [Test]
        public void WarehouseData_FollowsTheOperation()
        {
            int warehouseUpdates = 0, gateUpdates = 0;
            state.Facilities.Changed += id =>
            {
                state.Facilities.TryGet(id, out FacilityInfo info);
                if (id == CediFacilityIds.Warehouse)
                {
                    warehouseUpdates++;
                    Assert.AreEqual(HudFormat.Ratio(state.Resources.OccupiedPositions, 200),
                        info.Stats.First(s => s.Label == "Posiciones ocupadas").Value);
                    Assert.IsNotEmpty(info.Orders, "orders are kept");
                }
                else if (id == CediFacilityIds.Gate)
                {
                    gateUpdates++;
                    Assert.AreEqual(simulation.TrucksEntered.ToString(), info.Stats.First(s => s.Label == "Ingresos hoy").Value);
                }
            };

            Run(60f);

            Assert.Greater(warehouseUpdates, 1);
            Assert.Greater(gateUpdates, 1);
        }

        [Test]
        public void Vehicles_DescribeThemselvesForTheSelectionPanel()
        {
            Run(30f);

            TruckAgent truck = simulation.Vehicles.OfType<TruckAgent>().First();
            SelectionDetails details = truck.Describe();
            Assert.AreEqual(truck.Plate, details.Title);
            Assert.IsTrue(details.Stats.Any(s => s.Label == "Origen" || s.Label == "Destino"));
            Assert.IsTrue(details.Stats.Any(s => s.Label == "Estado" && s.Value == truck.Status));
            Assert.That(details.Progress, Is.InRange(0.0, 1.0));

            ForkliftAgent forklift = simulation.Vehicles.OfType<ForkliftAgent>().First();
            StringAssert.StartsWith("Montacargas MC-", forklift.Describe().Title);
            Assert.IsNull(forklift.Describe().Progress);
        }
    }
}
