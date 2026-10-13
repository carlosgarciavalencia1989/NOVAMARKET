using System;
using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    public class DemandPlanTests
    {
        private static DemandPlan Plan(int sku) => new DemandPlan(PlanningCatalog.Skus[sku]);

        [Test]
        public void MonthOfWeek_FollowsTheSeasonCalendar()
        {
            Assert.AreEqual(8, PlanningRules.MonthOfWeek(1));
            Assert.AreEqual(8, PlanningRules.MonthOfWeek(4));
            Assert.AreEqual(9, PlanningRules.MonthOfWeek(5));
            Assert.AreEqual(11, PlanningRules.MonthOfWeek(16));
            Assert.AreEqual(0, PlanningRules.MonthOfWeek(17));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlanningRules.MonthOfWeek(18));
        }

        [Test]
        public void MonthlyAverage_UsesOnlyTheYearsWithSales()
        {
            // September exists in 2023, 2024 and 2025: (9190 + 11123 + 7940) / 3.
            Assert.AreEqual(9417.667, DemandPlan.MonthlyAverage(PlanningCatalog.Skus[PlanningCatalog.Labial], 8), 0.001);
            // January exists in four years: (7818 + 7520 + 6557 + 8163) / 4.
            Assert.AreEqual(7514.5, DemandPlan.MonthlyAverage(PlanningCatalog.Skus[PlanningCatalog.Labial], 0), 0.001);
        }

        [Test]
        public void WeeklyMean_IsMonthlyAverageOverWeeksAndUnitsPerBox()
        {
            // 9417.67 / 4.33 / 48 = 45.31 → 45.3 boxes.
            Assert.AreEqual(45.3, Plan(PlanningCatalog.Labial).Mean(1), 1e-9);
            // 7514.5 / 4.33 / 48 = 36.16 → 36.2.
            Assert.AreEqual(36.2, Plan(PlanningCatalog.Labial).Mean(17), 1e-9);
            // December gift set: (249 + 400 + 396) / 3 / 4.33 / 6 = 13.41 → 13.4.
            Assert.AreEqual(13.4, Plan(PlanningCatalog.GiftSet).Mean(13), 1e-9);
            // December perfume: (4383 + 3477 + 1798) / 3 / 4.33 / 12 = 61.96 → 62.
            Assert.AreEqual(62.0, Plan(PlanningCatalog.Perfume).Mean(16), 1e-9);
        }

        [Test]
        public void WeeksAfterJanuary_UseJanuarysMean()
        {
            DemandPlan plan = Plan(PlanningCatalog.Perfume);
            Assert.AreEqual(plan.Mean(17), plan.Mean(19));
        }

        [Test]
        public void RackCapacity_IsFiveWeeksOfTheSeasonAverage()
        {
            // Labial: (45.3 + 35 + 43.4 + 44.4) × 4 weeks / 16 = 42.025 × 5 = 210.1 → 210.
            Assert.AreEqual(210, Plan(PlanningCatalog.Labial).RackCapacity);
            // Perfume: (43 + 42.5 + 44.6 + 62) / 4 = 48.025 × 5 = 240.1 → 240.
            Assert.AreEqual(240, Plan(PlanningCatalog.Perfume).RackCapacity);
        }

        [Test]
        public void PalletsFor_RoundsUpToWholePallets()
        {
            PlanningSku labial = PlanningCatalog.Skus[PlanningCatalog.Labial];
            Assert.AreEqual(0, labial.PalletsFor(0));
            Assert.AreEqual(1, labial.PalletsFor(1));
            Assert.AreEqual(1, labial.PalletsFor(labial.BoxesPerPallet));
            Assert.AreEqual(2, labial.PalletsFor(labial.BoxesPerPallet + 1));
        }
    }
}
