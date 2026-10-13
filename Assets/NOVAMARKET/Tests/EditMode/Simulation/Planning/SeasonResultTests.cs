using System;
using System.Linq;
using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    public class SeasonResultTests
    {
        private const int Seed = 11;

        [TestCase(0.89, 100, 100, 0)]
        [TestCase(0.90, 100, 100, 1)]
        [TestCase(0.949, 50, 100, 1)]
        [TestCase(0.95, 100, 100, 3)]
        [TestCase(0.97, 105, 100, 2)]
        [TestCase(0.97, 110, 100, 2)]
        [TestCase(0.97, 111, 100, 1)]
        public void StarsFor_FollowsServiceAndCostThresholds(double service, double cost, double reference, int stars) =>
            Assert.AreEqual(stars, SeasonResult.StarsFor(service, cost, reference));

        [Test]
        public void Evaluate_ComparesWithTheReferenceOfTheSameSeed()
        {
            var season = new PlanningSeason(Seed);
            PlanningSimulation reference = PlanningPolicies.PlayReference(season);
            PlanningSimulation player = InformedPlayer.Play(season);

            SeasonResult result = SeasonResult.Evaluate(player, reference);

            Assert.AreEqual(PlanningOutcome.Completed, result.Outcome);
            Assert.AreEqual(player.ServiceLevel, result.ServiceLevel);
            Assert.AreEqual(reference.Costs.Total - player.Costs.Total, result.SavingsVersusReference, 1e-6);
            Assert.AreEqual(SeasonResult.StarsFor(player.ServiceLevel, player.Costs.Total, reference.Costs.Total), result.Stars);
            Assert.AreEqual(result.Stars * SeasonResult.PointsPerStar, result.Points);
            Assert.IsTrue(result.Lessons.Any(l => l.Title.Contains("Atlas")), "Always compares with the Atlas.");
        }

        [Test]
        public void PlayingTheReference_TiesWithItForThreeStarsWhenServiceIsHigh()
        {
            PlanningSimulation reference = PlanningPolicies.PlayReference(Seed);
            PlanningSimulation copy = PlanningPolicies.PlayReference(Seed);

            SeasonResult result = SeasonResult.Evaluate(copy, reference);

            Assert.AreEqual(0, result.SavingsVersusReference, 1e-6);
            Assert.IsTrue(result.Lessons.Last().IsPositive, "Equal cost counts as beating the Atlas.");
        }

        [Test]
        public void GameOver_GivesNoStarsAndExplainsWhatFailed()
        {
            var player = new PlanningSimulation(Seed);
            PlanningPolicies.Play(player, s => new int[s.Skus.Count], _ => 0);
            SeasonResult result = SeasonResult.Evaluate(player, PlanningPolicies.PlayReference(Seed));

            Assert.AreEqual(PlanningOutcome.GameOver, result.Outcome);
            Assert.AreEqual(0, result.Stars);
            Assert.IsFalse(result.Passed);
            Assert.IsFalse(result.EarnedGem);
            Assert.IsNotNull(result.GameOver);
            Assert.IsTrue(result.Lessons.Any(l => l.Title == "Lo que logró la política del Atlas"));
        }

        [Test]
        public void Lessons_PointAtThePromoWhenLabialWasLostThere()
        {
            var season = new PlanningSeason(Seed);
            int promo = season.PromotionWeek;
            // Atlas orders, but nothing of labial in the weeks that feed the promo.
            var player = new PlanningSimulation(season, allowGameOver: false);
            PlanningPolicies.Play(player, s =>
            {
                int[] orders = PlanningPolicies.AtlasOrders(s);
                if (s.Week == promo + 1 || s.Week == promo + 2) orders[PlanningCatalog.Labial] = 0;
                return orders;
            }, _ => 0);

            SeasonResult result = SeasonResult.Evaluate(player, PlanningPolicies.PlayReference(season));

            Assert.IsTrue(result.Lessons.Any(l => l.Title == "Ventas perdidas de labial en la promo"));
        }

        [Test]
        public void Lessons_ExplainOverflowAndLiquidation()
        {
            var season = new PlanningSeason(Seed);
            var player = new PlanningSimulation(season, allowGameOver: false);
            PlanningPolicies.Play(player, s =>
            {
                int[] orders = PlanningPolicies.AtlasOrders(s);
                if (s.Week == 15) orders[PlanningCatalog.Labial] += 300;
                return orders;
            }, _ => 0);

            SeasonResult result = SeasonResult.Evaluate(player, PlanningPolicies.PlayReference(season));

            Assert.IsTrue(result.Lessons.Any(l => l.Title == "Costo de desborde"));
            Assert.IsTrue(result.Lessons.Any(l => l.Title == "Costo de liquidación"));
        }

        [Test]
        public void Evaluate_RejectsUnfinishedOrMismatchedSeasons()
        {
            PlanningSimulation reference = PlanningPolicies.PlayReference(Seed);
            Assert.Throws<InvalidOperationException>(() => SeasonResult.Evaluate(new PlanningSimulation(Seed), reference));
            Assert.Throws<ArgumentException>(() =>
                SeasonResult.Evaluate(PlanningPolicies.PlayReference(Seed + 1), reference));
        }
    }
}
