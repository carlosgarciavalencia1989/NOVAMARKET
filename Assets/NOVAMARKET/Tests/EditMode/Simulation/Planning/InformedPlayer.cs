using NovaMarket.Simulation.Planning;

namespace NovaMarket.Tests.Simulation.Planning
{
    /// <summary>
    /// Test player that does what the level teaches: orders the Atlas suggestion computed on the
    /// statistical plan multiplied by the events already announced (promo, new customer,
    /// competitor), and scales the orders to the purchase cap. Waits for the ship by default.
    /// </summary>
    public static class InformedPlayer
    {
        public static int[] Orders(PlanningSimulation simulation)
        {
            var orders = new int[simulation.Skus.Count];
            for (int sku = 0; sku < orders.Length; sku++)
            {
                int index = sku;
                orders[sku] = AtlasSuggestion.Compute(simulation.Skus[sku],
                    week => simulation.PlanMean(index, week) * simulation.AnnouncedFactor(index, week),
                    simulation.Week, simulation.OnHand(sku), simulation.InTransitBoxes(sku)).Order;
            }
            return PlanningPolicies.ScaleToCap(simulation, orders);
        }

        public static PlanningSimulation Play(PlanningSeason season, int shipDelayOption = 0) =>
            PlanningPolicies.Play(new PlanningSimulation(season), Orders,
                dialog => dialog.Kind == SeasonEventKind.ShipDelay && dialog.Options.Count > 1 ? shipDelayOption : 0);
    }
}
