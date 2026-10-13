using System;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// Statistical weekly plan of one SKU, built from its sales history (section 3 of the spec):
    /// the mean of a week is the average of that calendar month over the years with sales,
    /// divided by 4.33 weeks and by the units per box, rounded to one decimal.
    /// </summary>
    public sealed class DemandPlan
    {
        private readonly double[] means = new double[PlanningRules.ClosingWeek + 1];

        public DemandPlan(PlanningSku sku)
        {
            Sku = sku ?? throw new ArgumentNullException(nameof(sku));
            for (int week = 1; week <= PlanningRules.ClosingWeek; week++)
            {
                double monthly = MonthlyAverage(sku, PlanningRules.MonthOfWeek(week));
                means[week] = PlanningRules.Round1(monthly / PlanningRules.WeeksPerMonth / sku.UnitsPerBox);
            }

            double seasonTotal = 0;
            for (int week = 1; week <= PlanningRules.SeasonWeeks; week++) seasonTotal += means[week];
            RackCapacity = PlanningRules.Round(seasonTotal / PlanningRules.SeasonWeeks * PlanningRules.RackCapacityWeeks);
        }

        public PlanningSku Sku { get; }

        /// <summary>Boxes that fit in the SKU's rack space; more than this goes to the floor (overflow).</summary>
        public int RackCapacity { get; }

        /// <summary>Statistical mean demand of a week in boxes; weeks after 17 use January's.</summary>
        public double Mean(int week)
        {
            if (week < 1) throw new ArgumentOutOfRangeException(nameof(week), week, "Weeks start at 1.");
            return means[Math.Min(week, PlanningRules.ClosingWeek)];
        }

        /// <summary>Average monthly units of a calendar month (0 = January) over the years with sales.</summary>
        public static double MonthlyAverage(PlanningSku sku, int month)
        {
            double total = 0;
            int years = 0;
            for (int i = month; i < sku.MonthlyHistory.Count; i += 12)
            {
                if (sku.MonthlyHistory[i] <= 0) continue;
                total += sku.MonthlyHistory[i];
                years++;
            }
            return years == 0 ? 0 : total / years;
        }
    }
}
