using System;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// Fixed rules of the season (Documentation/SIMULACION_PLANEACION.md): calendar, cost rates,
    /// Atlas service factor, game-over limits and star thresholds.
    /// </summary>
    public static class PlanningRules
    {
        /// <summary>Weeks the player plans (September to December).</summary>
        public const int SeasonWeeks = 16;

        /// <summary>Week after the season (January), only used for the closing.</summary>
        public const int ClosingWeek = SeasonWeeks + 1;

        /// <summary>Average number of weeks in a month, to turn monthly sales into weekly ones.</summary>
        public const double WeeksPerMonth = 4.33;

        /// <summary>Rack capacity of each SKU, in weeks of its average season demand.</summary>
        public const int RackCapacityWeeks = 5;

        /// <summary>Weekly noise is a standard normal clipped to ± this value.</summary>
        public const double NoiseClip = 2.3;

        public const double HoldingRate = 0.004;
        public const double OverflowRate = 0.012;
        public const double LostSaleRate = 0.5;
        public const double LiquidationRate = 0.10;

        /// <summary>At the closing, stock above this many weeks of January demand is liquidated.</summary>
        public const int ClosingCoverWeeks = 3;

        /// <summary>Service factor of the Atlas suggestion (95 % cycle service level).</summary>
        public const double ServiceZ = 1.65;

        public const long AirFreightCost = 4_500_000;

        /// <summary>Game over if the cumulative service level is below this from week 4 on.</summary>
        public const double GameOverServiceLevel = 0.85;
        public const int GameOverServiceFromWeek = 4;

        /// <summary>Game over after this many weeks in a row with boxes on the floor.</summary>
        public const int GameOverOverflowWeeks = 3;

        public const double OneStarService = 0.90;
        public const double TwoStarService = 0.95;

        /// <summary>Two stars allow spending up to 10 % more than the Atlas reference.</summary>
        public const double TwoStarCostRatio = 1.10;

        /// <summary>Calendar month (0 = January) of a season week: 1–4 September … 13–16 December, 17 January.</summary>
        public static int MonthOfWeek(int week)
        {
            if (week < 1 || week > ClosingWeek) throw new ArgumentOutOfRangeException(nameof(week), week, "Week must be 1..17.");
            return week == ClosingWeek ? 0 : 8 + (week - 1) / 4;
        }

        public static string MonthName(int week) => MonthOfWeek(week) switch
        {
            8 => "Septiembre",
            9 => "Octubre",
            10 => "Noviembre",
            11 => "Diciembre",
            _ => "Enero"
        };

        /// <summary>Half-up rounding, like the prototype (Math.Round would round 2.5 to 2).</summary>
        public static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        public static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }
}
