using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>Runs the city traffic and checks its rules: no touching, no red lights, nobody stuck, many motos.</summary>
    public class AmbientTrafficTests
    {
        private const float Step = 0.1f;

        /// <summary>Fails when two bodies touch.</summary>
        internal static void AssertApart(IReadOnlyList<Footprint> bodies, string what)
        {
            for (int i = 0; i < bodies.Count; i++)
            for (int j = i + 1; j < bodies.Count; j++)
            {
                if (Vector2.DistanceSquared(bodies[i].Centre, bodies[j].Centre) > 400f) continue;
                Assert.Greater(bodies[i].Separation(bodies[j]), 0f, $"{what}: bodies {i} and {j} touch");
            }
        }

        [TestCase(11)]
        [TestCase(29)]
        public void City_FiveMinutes_NoTouching_NoRedLights_NobodyStuck(int seed)
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.City(), seed);
            var traffic = new AmbientTraffic(network, new AmbientTrafficSettings { VehicleCount = 110 }, seed);
            var focus = new Vector2(0f, -60f);
            int added = 0, motos = 0, junctionEntries = 0;
            traffic.VehicleAdded += v =>
            {
                added++;
                if (v.IsMoto) motos++;
            };
            traffic.EnteredJunction += (v, movement) =>
            {
                junctionEntries++;
                Assert.AreSame(movement, v.Booked, $"{v} drove onto a crossing it had not booked");
                TrafficSignal signal = movement.Junction.Signal;
                // Entering just after it turned red is fine (booked on amber); red for a while is not.
                if (signal != null)
                    Assert.IsFalse(signal.AspectFor(movement.From.SignalGroup, traffic.Time) == SignalAspect.Red
                                   && signal.AspectFor(movement.From.SignalGroup, traffic.Time - 2.5) == SignalAspect.Red,
                        $"{v} ran a red light");
            };

            float worstStop = 0f;
            for (int i = 0; i < 300f / Step; i++)
            {
                traffic.Tick(Step, focus, 650f);
                if (i % 3 == 0) AssertApart(traffic.Vehicles.Select(v => v.Footprint).ToList(), $"t = {traffic.Time:F1}");
                foreach (AmbientVehicle v in traffic.Vehicles) worstStop = System.Math.Max(worstStop, v.StoppedSeconds);
            }

            Assert.AreEqual(0, traffic.StuckRemovals, "nobody should need the safety valve");
            Assert.Less(worstStop, 120f, "longest wait: at most two light cycles at a busy crossing");
            Assert.Greater(traffic.Vehicles.Count, 100, "the city stays full");
            Assert.Greater(junctionEntries, 500, "vehicles keep crossing");
            Assert.That(motos / (float)added, Is.InRange(0.28f, 0.42f), $"{motos} motos of {added}");
        }

        [Test]
        public void Motos_FilterToTheFrontOfARedLight_AndLeaveFirst()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.Crossing(true));
            var traffic = new AmbientTraffic(network, new AmbientTrafficSettings { VehicleCount = 0 });
            TrafficLane lane = TestNetworks.LaneHeading(network, new Vector2(1f, 0f), new Vector2(-150f, 0f));

            // Wait for the light to turn red, then queue cars at the line and motos behind them.
            SignalAspect before = traffic.AspectFor(lane);
            while (!(before != SignalAspect.Red && traffic.AspectFor(lane) == SignalAspect.Red))
            {
                before = traffic.AspectFor(lane);
                traffic.Tick(Step, Vector2.Zero, 1000f);
            }
            float end = lane.Length;
            var cars = new[] { 12f, 19f, 26f }.Select(d => traffic.Insert(AmbientVehicleKind.Car, lane, end - d)).ToList();
            var motos = new[] { 45f, 49f, 53f, 57f }.Select(d => traffic.Insert(AmbientVehicleKind.Moto, lane, end - d)).ToList();
            var order = new List<AmbientVehicle>();
            traffic.EnteredJunction += (v, _) => order.Add(v);

            for (int i = 0; i < 20f / Step; i++)
            {
                traffic.Tick(Step, Vector2.Zero, 1000f);
                AssertApart(traffic.Vehicles.Select(v => v.Footprint).ToList(), $"t = {traffic.Time:F1}");
            }
            Assert.AreEqual(SignalAspect.Red, traffic.AspectFor(lane), "still red");
            float firstCar = cars.Max(c => c.Front);
            foreach (AmbientVehicle moto in motos)
                Assert.Greater(moto.Front, firstCar, $"{moto} waits in front of the cars (offset {moto.Offset:F1})");
            Assert.LessOrEqual(firstCar, end - AmbientTraffic.CarStopBack + 0.01f, "cars stop behind the moto box");

            for (int i = 0; i < 40f / Step && order.Count < 7; i++) traffic.Tick(Step, Vector2.Zero, 1000f);
            Assert.AreEqual(7, order.Count, "everyone crossed on green");
            Assert.IsTrue(order.Take(2).All(v => v.IsMoto), "motos leave first");
        }

        [Test]
        public void Bus_StopsAtItsStop_AndTheCarBehindOvertakesWhileOncomingTrafficWaits()
        {
            RoadNetwork network = RoadNetwork.Build(TestNetworks.StreetWithBusStop());
            var traffic = new AmbientTraffic(network, new AmbientTrafficSettings { VehicleCount = 0, BusDwellSeconds = 20f });
            TrafficLane east = TestNetworks.LaneHeading(network, new Vector2(1f, 0f), Vector2.Zero);
            Assert.AreEqual(1, east.BusStops.Count);
            float stop = east.BusStops[0];

            AmbientVehicle bus = traffic.Insert(AmbientVehicleKind.Bus, east, stop - 30f);
            AmbientVehicle car = traffic.Insert(AmbientVehicleKind.Car, east, stop - 50f);
            AmbientVehicle oncoming = traffic.Insert(AmbientVehicleKind.Car, east.Opposite, 150f, 10f);

            bool busParked = false, carPulledOut = false, carPassed = false;
            for (int i = 0; i < 60f / Step; i++)
            {
                traffic.Tick(Step, Vector2.Zero, 1000f);
                AssertApart(traffic.Vehicles.Select(v => v.Footprint).ToList(), $"t = {traffic.Time:F1}");
                busParked |= bus.IsParked;
                carPulledOut |= car.Offset < -4f;
                if (bus.IsParked && car.Link == bus.Link && car.Front - car.Length > bus.Front) carPassed = true;
            }
            Assert.IsTrue(busParked, "the bus stood at its stop");
            Assert.IsTrue(carPulledOut, "the car moved to the opposite lane");
            Assert.IsTrue(carPassed, "the car passed the bus while it stood at the stop");
            Assert.AreEqual(0f, car.Offset, 0.05f, "and came back to its lane");
            Assert.IsTrue(traffic.Vehicles.Contains(oncoming) || oncoming.Front > 0f);
        }
    }
}
