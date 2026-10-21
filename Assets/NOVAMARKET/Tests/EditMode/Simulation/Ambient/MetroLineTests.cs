using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Ambient;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>Runs the valley metro for an hour and checks stops, doors, spacing and the turn-backs.</summary>
    public class MetroLineTests
    {
        private const float Step = 0.1f;
        private static readonly float[] Stations = { -330f, 420f };

        private static MetroLine CreateLine(MetroSettings settings = null) =>
            new MetroLine(new MetroLineDefinition(-1450f, 1450f, -85.9f, -82.1f, 11.64f, Stations, 60f), settings);

        [Test]
        public void Trains_StopCentredOnEveryPlatformInBothDirections_WithDoorsOpenForTheDwell()
        {
            MetroLine line = CreateLine();
            var arrivals = new Dictionary<(int Train, MetroDirection Direction, int Station), int>();
            var arrivedAt = new Dictionary<MetroTrain, double>();
            double time = 0;
            line.Arrived += (train, station) =>
            {
                Assert.AreEqual(Stations[station], train.Position.X, 0.5f, $"{train} centred on the platform");
                Assert.AreEqual(0f, train.Speed);
                Assert.IsTrue(train.DoorsOpen);
                var key = (train.Id, train.Direction, station);
                arrivals[key] = arrivals.TryGetValue(key, out int n) ? n + 1 : 1;
                arrivedAt[train] = time;
            };
            line.Departed += (train, _) =>
            {
                Assert.AreEqual(line.Settings.DwellSeconds, time - arrivedAt[train], 0.2, "doors open for the dwell");
                Assert.IsFalse(train.DoorsOpen);
            };

            for (int i = 0; i < 3600f / Step; i++)
            {
                line.Tick(Step);
                time += Step;
                foreach (MetroTrain train in line.Trains)
                {
                    Assert.LessOrEqual(train.Speed, line.Settings.CruiseSpeed + 1e-3f);
                    Assert.IsTrue(!train.DoorsOpen || train.Speed == 0f, "doors only open when standing");
                    Assert.That(train.Position.X, Is.InRange(-1450f, 1450f));
                }
            }

            foreach (MetroTrain train in line.Trains)
            foreach (MetroDirection direction in new[] { MetroDirection.Eastbound, MetroDirection.Westbound })
            for (int station = 0; station < Stations.Length; station++)
                Assert.GreaterOrEqual(arrivals.TryGetValue((train.Id, direction, station), out int n) ? n : 0, 2,
                    $"{train} {direction} at station {station}");
        }

        [Test]
        public void Trains_OnTheSameTrack_KeepTheirSeparation()
        {
            MetroLine line = CreateLine(new MetroSettings { TrainCount = 6 });
            for (int i = 0; i < 3600f / Step; i++)
            {
                line.Tick(Step);
                foreach (MetroTrain a in line.Trains)
                foreach (MetroTrain b in line.Trains)
                {
                    if (a == b || a.Direction != b.Direction || b.Rear < a.Front) continue;
                    Assert.GreaterOrEqual(b.Rear - a.Front, line.Settings.MinSeparation - 1f, $"{a} behind {b}");
                }
            }
        }

        [Test]
        public void Stations_SeeATrainInEachDirectionEveryFewMinutes()
        {
            MetroLine line = CreateLine();
            var last = new Dictionary<(MetroDirection, int), double>();
            double time = 0, worst = 0;
            line.Arrived += (train, station) =>
            {
                var key = (train.Direction, station);
                if (last.TryGetValue(key, out double before)) worst = System.Math.Max(worst, time - before);
                last[key] = time;
            };
            for (int i = 0; i < 3600f / Step; i++)
            {
                line.Tick(Step);
                time += Step;
            }
            Assert.AreEqual(4, last.Count, "both directions at both stations");
            Assert.Less(worst, 8 * 60, "longest wait between two trains (seconds)");
            TestContext.WriteLine($"longest headway: {worst:F0} s");
        }

        [Test]
        public void Trains_TurnBackAtTheEndsOfTheLine()
        {
            MetroLine line = CreateLine();
            var directions = line.Trains.ToDictionary(t => t, t => new HashSet<MetroDirection> { t.Direction });
            for (int i = 0; i < 1800f / Step; i++)
            {
                line.Tick(Step);
                foreach (MetroTrain train in line.Trains) directions[train].Add(train.Direction);
            }
            foreach (var pair in directions) Assert.AreEqual(2, pair.Value.Count, $"{pair.Key} ran both ways");
        }
    }
}
