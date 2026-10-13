using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>A lesson shown at the end of the level, linked to the Atlas step it practises.</summary>
    public sealed class PlanningLesson
    {
        public PlanningLesson(string title, string text, string atlasStep, bool isPositive = false)
        {
            Title = title;
            Text = text;
            AtlasStep = atlasStep;
            IsPositive = isPositive;
        }

        public string Title { get; }
        public string Text { get; }
        public string AtlasStep { get; }

        /// <summary>Something the player did well (shown in green).</summary>
        public bool IsPositive { get; }
    }

    /// <summary>
    /// Final result of the level (section 7 of the spec): service level, costs, stars against the
    /// Atlas reference played with the same seed, the gem and the automatic lessons.
    /// </summary>
    public sealed class SeasonResult
    {
        /// <summary>Reward of this level.</summary>
        public const string GemName = "Topacio de la temporada";
        public const int PointsPerStar = 500;

        private SeasonResult(PlanningSimulation player, PlanningSimulation reference)
        {
            Seed = player.Season.Seed;
            Outcome = player.Outcome;
            ServiceLevel = player.ServiceLevel;
            Costs = player.Costs;
            ReferenceServiceLevel = reference.ServiceLevel;
            ReferenceCosts = reference.Costs;
            GameOver = player.GameOver;
            Stars = Outcome == PlanningOutcome.Completed ? StarsFor(ServiceLevel, Costs.Total, ReferenceCosts.Total) : 0;
            Lessons = PlanningLessons.Build(player, reference);
        }

        public int Seed { get; }
        public PlanningOutcome Outcome { get; }
        public double ServiceLevel { get; }
        public CostBreakdown Costs { get; }
        public double ReferenceServiceLevel { get; }
        public CostBreakdown ReferenceCosts { get; }

        /// <summary>Set when the level ended early.</summary>
        public GameOverInfo GameOver { get; }

        public int Stars { get; }

        /// <summary>The level is passed with at least one star; otherwise it is repeated with a new seed.</summary>
        public bool Passed => Stars > 0;

        public bool EarnedGem => Passed;
        public int Points => Stars * PointsPerStar;

        /// <summary>Reference cost minus the player's: positive when the player spent less.</summary>
        public double SavingsVersusReference => ReferenceCosts.Total - Costs.Total;

        public IReadOnlyList<PlanningLesson> Lessons { get; }

        /// <summary>Compares a finished player season with the Atlas reference of the same seed.</summary>
        public static SeasonResult Evaluate(PlanningSimulation player, PlanningSimulation reference)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (!player.IsFinished) throw new InvalidOperationException("The player's season is not over.");
            if (reference.Outcome != PlanningOutcome.Completed)
                throw new InvalidOperationException("The reference must play the whole season.");
            if (reference.Season.Seed != player.Season.Seed)
                throw new ArgumentException("The reference must use the player's seed.", nameof(reference));
            return new SeasonResult(player, reference);
        }

        /// <summary>
        /// 0 stars below 90 % service, 1 below 95 %; from 95 %: 3 if the cost is not above the
        /// reference, 2 if it is at most 10 % above, otherwise 1.
        /// </summary>
        public static int StarsFor(double serviceLevel, double cost, double referenceCost)
        {
            if (serviceLevel < PlanningRules.OneStarService) return 0;
            if (serviceLevel < PlanningRules.TwoStarService) return 1;
            if (cost <= referenceCost) return 3;
            return cost <= referenceCost * PlanningRules.TwoStarCostRatio ? 2 : 1;
        }
    }
}
