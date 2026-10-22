using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>People on the metro platforms and the staff of the CEDI.</summary>
    public class StationAndCediPeopleTests
    {
        private const float Step = 0.1f;

        private static MetroLine CreateLine() =>
            new MetroLine(new MetroLineDefinition(-1450f, 1450f, -85.9f, -82.1f, 11.64f, new[] { -330f, 420f }, 60f));

        [Test]
        public void Passengers_WaitOnThePlatforms_GetOnAndOffAtTheDoors()
        {
            MetroLine line = CreateLine();
            var passengers = new MetroPassengers(line, 8);
            int on = 0, off = 0;
            passengers.Boarded += (p, train, boarding) =>
            {
                Assert.IsTrue(train.DoorsOpen, "only through open doors");
                Assert.AreEqual(train.Position.X, p.Position.X, train.Length / 2f, "at the train");
                if (boarding) on++;
                else off++;
            };

            for (int i = 0; i < 1800f / Step; i++)
            {
                line.Tick(Step);
                passengers.Tick(Step);
                foreach (Pedestrian p in passengers.People)
                {
                    Assert.AreEqual(passengers.PlatformHeight, p.Height, 1e-3f);
                    float z = p.Position.Y;
                    bool onPlatform = z <= -85.9f - 1.9f + 0.01f && z >= -85.9f - 6.4f || z >= -82.1f + 1.9f - 0.01f && z <= -82.1f + 6.4f;
                    Assert.IsTrue(onPlatform, $"{p} at z = {z:F2} stays on a platform, off the tracks");
                }
            }
            Assert.Greater(on, 20, "people got on");
            Assert.Greater(off, 20, "people got off");
            Assert.That(passengers.People.Count, Is.InRange(10, 4 * 8 + 40));
        }

        [Test]
        public void CediStaff_KeepToTheirPlaces_AndStayAwayFromTheTrucks()
        {
            OperationState state = SampleOperationData.Create();
            var cedi = new CediOperationSimulation(state, seed: 7);
            var workers = new CediWorkers(cedi);
            var seen = new HashSet<Pedestrian>();

            for (int i = 0; i < 900f / Step; i++)
            {
                cedi.Tick(Step, cedi.Time);
                workers.Tick(Step);
                foreach (Pedestrian p in workers.People)
                {
                    seen.Add(p);
                    if (p.Height > 1f) // a picker on the warehouse floor
                        Assert.IsTrue(CediWorkers.PickerLanesX.Any(x => System.Math.Abs(p.Position.X - x) < 0.01f)
                                      && System.Math.Abs(p.Position.Y) <= CediWorkers.PickerHalfReach + 0.01f, $"{p} between the stations");
                    foreach (VehicleAgent vehicle in cedi.Vehicles.Where(v => v.Kind != VehicleKind.Forklift))
                        Assert.Greater(vehicle.Footprint.Separation(p.Footprint), 0.3f, $"t = {cedi.Time:F0}: {p} by {vehicle.Plate}");
                }
            }
            Assert.AreEqual(10, seen.Count, "guard, 4 pickers, 3 staff and 2 chatting");
            Assert.Greater(workers.OfficeVisits, 3, "staff come and go");
        }
    }
}
