using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// Automatic ways of playing a season. The Atlas reference (section 5 of the spec) always
    /// orders the suggestion, answers every event with its default option and, under a purchase
    /// cap, scales all orders down proportionally. The player's result is compared with it.
    /// </summary>
    public static class PlanningPolicies
    {
        /// <summary>The Atlas suggestion of every SKU for the current week, scaled to the purchase cap.</summary>
        public static int[] AtlasOrders(PlanningSimulation simulation)
        {
            var orders = new int[simulation.Skus.Count];
            for (int sku = 0; sku < orders.Length; sku++) orders[sku] = simulation.Suggest(sku).Order;
            return ScaleToCap(simulation, orders);
        }

        /// <summary>
        /// Scales every order by the same factor (rounding down) so their value fits this week's
        /// purchase cap. Orders are returned unchanged when there is no cap or they already fit.
        /// </summary>
        public static int[] ScaleToCap(PlanningSimulation simulation, IReadOnlyList<int> orders)
        {
            var scaled = new int[orders.Count];
            for (int sku = 0; sku < scaled.Length; sku++) scaled[sku] = orders[sku];
            if (!simulation.ExceedsPurchaseCap(orders)) return scaled;

            double factor = (double)simulation.PurchaseCap.Value / simulation.OrderValue(orders);
            for (int sku = 0; sku < scaled.Length; sku++) scaled[sku] = (int)Math.Floor(orders[sku] * factor);
            return scaled;
        }

        /// <summary>Plays a whole season with the Atlas reference policy (never ends in game over).</summary>
        public static PlanningSimulation PlayReference(int seed) => PlayReference(new PlanningSeason(seed));

        public static PlanningSimulation PlayReference(PlanningSeason season) =>
            Play(new PlanningSimulation(season, allowGameOver: false), AtlasOrders, _ => 0);

        /// <summary>Plays until the season ends: answers events, then orders and closes each week.</summary>
        public static PlanningSimulation Play(PlanningSimulation simulation, Func<PlanningSimulation, int[]> orders,
            Func<EventDialog, int> answer)
        {
            while (!simulation.IsFinished)
            {
                if (simulation.PendingEvent != null) simulation.ResolveEvent(answer(simulation.PendingEvent));
                simulation.CloseWeek(orders(simulation));
            }
            return simulation;
        }
    }
}
