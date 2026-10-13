using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    /// <summary>
    /// 200 seeds for each level: every seed can be solved with 3 stars, and the typical mistakes the
    /// level teaches (keeping the projection, doing nothing, imputing a seasonal zero with the
    /// neighbours, "fixing" the December peak) never get the full result.
    /// </summary>
    public class LevelBalanceTests
    {
        private static readonly int[] Seeds = Enumerable.Range(1, 200).ToArray();

        [Test]
        public void GatherHistory_IsSolvableOnEverySeed_AndTheProjectionAlwaysFails()
        {
            int threeStars = 0, projectionPassed = 0;
            foreach (int seed in Seeds)
            {
                var solved = new GatherHistoryLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.SolveGather(solved);
                if (solved.Deliver().Stars == 3) threeStars++;

                var trapped = new GatherHistoryLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.SolveGather(trapped, projection: true);
                if (trapped.Deliver().Passed) projectionPassed++;
            }
            TestContext.WriteLine($"1.1: 3 stars {threeStars}/200; passed with the projection {projectionPassed}/200");
            Assert.AreEqual(200, threeStars);
            Assert.AreEqual(0, projectionPassed);
        }

        [Test]
        public void CleanData_IsSolvableOnEverySeed_AndTheMistakesCost()
        {
            int threeStars = 0, nothingPassed = 0, neighboursFull = 0, decemberFull = 0, damaged = 0;
            double levelError = 0, indexError = 0;
            int codesSeen = 0;
            var placements = new System.Collections.Generic.HashSet<string>();
            foreach (int seed in Seeds)
            {
                var solved = new CleanDataLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.SolveClean(solved);
                if (solved.Deliver().Stars == 3) threeStars++;
                placements.Add(string.Join(",", solved.Data.Issues.Select(i => i.Code + "@" + i.Months[0])));

                var lazy = new CleanDataLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.Download(lazy);
                LevelResult nothing = lazy.Deliver();
                if (nothing.Passed) nothingPassed++;
                damaged += nothing.DamagedSkus.Count();
                foreach (SkuImpact impact in nothing.Impact)
                {
                    levelError += impact.LevelError;
                    indexError += impact.IndexError;
                    codesSeen++;
                }

                var neighbours = new CleanDataLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.SolveClean(neighbours);
                int row = HistoryTable.FindRow(neighbours.CleanSheet, "CV10004");
                neighbours.CleanSheet.SetNumber(new CellAddress(HistoryTable.MonthColumn(17), row), 3301.5);
                if (neighbours.Deliver().Stars == 3) neighboursFull++;

                var december = new CleanDataLevel(LevelPlayers.Portfolio, seed);
                LevelPlayers.SolveClean(december);
                int dec24 = NovaMarket.Simulation.Portfolio.SalesCalendar.IndexOfKey("2024-12");
                int decRow = HistoryTable.FindRow(december.CleanSheet, "CV10004");
                december.Mark(new IssueMark("CV10004", dec24, DataIssueKind.OneOffOrder, CorrectionAction.Impute));
                december.CleanSheet.SetNumber(new CellAddress(HistoryTable.MonthColumn(dec24), decRow), 4500);
                if (december.Deliver().Stars == 3) decemberFull++;
            }

            TestContext.WriteLine($"1.2: 3 stars {threeStars}/200; doing nothing passed {nothingPassed}/200; " +
                                  $"neighbours on jun-24 with 3 stars {neighboursFull}/200; December fixed with 3 stars {decemberFull}/200");
            TestContext.WriteLine($"1.2: damaged SKUs per seed without cleaning {damaged / 200.0:F1}; average distortion " +
                                  $"level {levelError / codesSeen:P1}, worst index {indexError / codesSeen:F2}; distinct placements {placements.Count}");
            Assert.AreEqual(200, threeStars);
            Assert.AreEqual(0, nothingPassed);
            Assert.AreEqual(0, neighboursFull);
            Assert.AreEqual(0, decemberFull);
            Assert.AreEqual(5 * 200, damaged, "Without cleaning, all five SKUs teach the model something wrong.");
            Assert.Greater(placements.Count, 150, "Seeds change where the problems are.");
        }

        [Test]
        public void OneOffOrders_AreAlwaysFlaggedByTheAtlasThreeDeviationRule()
        {
            foreach (int seed in Seeds)
            {
                var level = new CleanDataLevel(LevelPlayers.Portfolio, seed);
                DataIssue order = level.Data.Issues.Single(i => i.Kind == DataIssueKind.OneOffOrder);
                var billed = level.Data.Billed(order.Code);
                double mean = NovaMarket.Simulation.Portfolio.SeriesStatistics.Mean(billed);
                double deviation = NovaMarket.Simulation.Portfolio.SeriesStatistics.StdDev(billed);
                Assert.Greater(System.Math.Abs(billed[order.Months[0]] - mean), 3 * deviation, $"seed {seed}");
            }
        }
    }
}
