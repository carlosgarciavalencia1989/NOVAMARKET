using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    /// <summary>
    /// Replicates the balance validated in the web prototype (section 8 of the spec) over 200
    /// seeds: the Atlas reference reaches ~93 % service, an informed player ~96 % with ~18 %
    /// less cost and mostly 3 stars, and ordering nothing ~16 %.
    /// </summary>
    public class PlanningBalanceTests
    {
        private const int Seasons = 200;

        private static double referenceService;
        private static double referenceCost;
        private static double informedService;
        private static double informedCost;
        private static double zeroService;
        private static int informedGameOvers;
        private static readonly int[] Stars = new int[4];

        [OneTimeSetUp]
        public void PlayAllSeasons()
        {
            referenceService = referenceCost = informedService = informedCost = zeroService = 0;
            informedGameOvers = 0;
            System.Array.Clear(Stars, 0, Stars.Length);

            for (int seed = 1; seed <= Seasons; seed++)
            {
                var season = new PlanningSeason(seed);
                PlanningSimulation reference = PlanningPolicies.PlayReference(season);
                PlanningSimulation informed = InformedPlayer.Play(season);
                PlanningSimulation zero = PlanningPolicies.Play(new PlanningSimulation(season, allowGameOver: false),
                    s => new int[s.Skus.Count], _ => 0);

                referenceService += reference.ServiceLevel;
                referenceCost += reference.Costs.Total;
                informedService += informed.ServiceLevel;
                informedCost += informed.Costs.Total;
                zeroService += zero.ServiceLevel;
                if (informed.Outcome == PlanningOutcome.GameOver) informedGameOvers++;
                Stars[SeasonResult.Evaluate(informed, reference).Stars]++;
            }

            referenceService /= Seasons;
            informedService /= Seasons;
            zeroService /= Seasons;
        }

        [Test]
        public void AtlasReference_ServesAbout93Percent() =>
            Assert.That(referenceService, Is.InRange(0.91, 0.95), $"Reference service {referenceService:P1}");

        [Test]
        public void InformedPlayer_ServesAbout96Percent() =>
            Assert.That(informedService, Is.InRange(0.95, 0.98), $"Informed service {informedService:P1}");

        [Test]
        public void InformedPlayer_SpendsAbout18PercentLessThanTheReference()
        {
            double savings = 1.0 - informedCost / referenceCost;
            Assert.That(savings, Is.InRange(0.12, 0.25), $"Savings {savings:P1}");
        }

        [Test]
        public void InformedPlayer_GetsThreeStarsInMostSeasons()
        {
            Assert.AreEqual(0, informedGameOvers, "Listening to the events never ends in game over.");
            Assert.Greater(Stars[3], Seasons / 2, $"Stars 0..3: {string.Join(", ", Stars)}");
            Assert.AreEqual(0, Stars[0]);
        }

        [Test]
        public void OrderingNothing_ServesAbout16Percent() =>
            Assert.That(zeroService, Is.InRange(0.12, 0.20), $"Zero-order service {zeroService:P1}");
    }
}
