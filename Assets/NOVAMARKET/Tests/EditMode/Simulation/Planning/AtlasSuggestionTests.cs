using System;
using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    public class AtlasSuggestionTests
    {
        private static DemandPlan Plan(int sku) => new DemandPlan(PlanningCatalog.Skus[sku]);

        [Test]
        public void Labial_WeekOne_MatchesTheHandCalculation()
        {
            // L = 1: F = 45.3 + 45.3 = 90.6; σ = 0.18 × 45.3 = 8.154; SS = 1.65 × 8.154 × √2 = 19.027;
            // S = 109.627; position = 91 + 0 → order = ceil(18.627) = 19.
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(Plan(PlanningCatalog.Labial), 1, 91, 0);

            Assert.AreEqual(2, suggestion.ProtectionWeeks);
            Assert.AreEqual(90.6, suggestion.Forecast, 1e-9);
            Assert.AreEqual(8.154, suggestion.Sigma, 1e-9);
            Assert.AreEqual(19.027, suggestion.SafetyStock, 0.001);
            Assert.AreEqual(109.627, suggestion.TargetLevel, 0.001);
            Assert.AreEqual(91, suggestion.Position);
            Assert.AreEqual(19, suggestion.Order);
            Assert.IsFalse(suggestion.RaisedToMinimum);
        }

        [Test]
        public void Perfume_WeekOne_CountsTheTransitInThePosition()
        {
            // L = 3: F = 4 × 43 = 172; σ = 0.26 × 43 = 11.18; SS = 1.65 × 11.18 × √4 = 36.894;
            // S = 208.894; position = 86 + 86 → order = ceil(36.894) = 37 (above the MOQ of 24).
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(Plan(PlanningCatalog.Perfume), 1, 86, 86);

            Assert.AreEqual(172.0, suggestion.Forecast, 1e-9);
            Assert.AreEqual(36.894, suggestion.SafetyStock, 1e-6);
            Assert.AreEqual(172, suggestion.Position);
            Assert.AreEqual(37, suggestion.Order);
        }

        [Test]
        public void SmallPositiveOrder_IsRaisedToTheMinimumOrder()
        {
            // S = 208.894 and position 200 → 9 boxes, below the perfume MOQ of 24.
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(Plan(PlanningCatalog.Perfume), 1, 120, 80);

            Assert.AreEqual(9, suggestion.RawOrder);
            Assert.IsTrue(suggestion.RaisedToMinimum);
            Assert.AreEqual(24, suggestion.Order);
        }

        [Test]
        public void PositionAboveTheTarget_OrdersNothing()
        {
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(Plan(PlanningCatalog.Perfume), 1, 300, 0);

            Assert.AreEqual(0, suggestion.Order);
            Assert.IsFalse(suggestion.RaisedToMinimum, "A zero order is never raised to the MOQ.");
        }

        [Test]
        public void PlanBeyondTheSeason_UsesJanuary()
        {
            DemandPlan plan = Plan(PlanningCatalog.Perfume);
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(plan, 16, 0, 0);

            Assert.AreEqual(plan.Mean(16) + 3 * plan.Mean(17), suggestion.Forecast, 1e-9);
        }

        [Test]
        public void AdjustedPlan_ChangesForecastAndSigma()
        {
            PlanningSku labial = PlanningCatalog.Skus[PlanningCatalog.Labial];
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(labial, week => 10.0 * week, 2, 0, 0);

            Assert.AreEqual(50.0, suggestion.Forecast, 1e-9); // weeks 2 and 3
            Assert.AreEqual(0.18 * 20.0, suggestion.Sigma, 1e-9);
        }

        [Test]
        public void Explain_ShowsEveryStepWithTheNumbers()
        {
            AtlasSuggestion suggestion = AtlasSuggestion.Compute(Plan(PlanningCatalog.Labial), 1, 91, 0);
            var steps = suggestion.Explain();

            Assert.AreEqual(6, steps.Count);
            StringAssert.Contains("45,3 + 45,3 = 90,6", steps[0]);
            StringAssert.Contains("0,18 × 45,3 = 8,2", steps[1]);
            StringAssert.Contains("√2 = 19,0", steps[2]);
            StringAssert.Contains("= 109,6", steps[3]);
            StringAssert.Contains("91 + 0 = 91", steps[4]);
            StringAssert.Contains("= 19 cajas", steps[5]);
        }

        [Test]
        public void Explain_MentionsTheMinimumOrderWhenItApplies()
        {
            var steps = AtlasSuggestion.Compute(Plan(PlanningCatalog.Perfume), 1, 120, 80).Explain();

            Assert.AreEqual(7, steps.Count);
            StringAssert.Contains("MOQ", steps[6]);
        }

        [Test]
        public void Compute_RejectsMissingInputs()
        {
            Assert.Throws<ArgumentNullException>(() => AtlasSuggestion.Compute(null, w => 1, 1, 0, 0));
            Assert.Throws<ArgumentNullException>(() =>
                AtlasSuggestion.Compute(PlanningCatalog.Skus[0], null, 1, 0, 0));
        }
    }
}
