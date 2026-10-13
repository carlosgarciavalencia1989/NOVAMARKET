using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>State of the level.</summary>
    public enum PlanningOutcome
    {
        Playing,

        /// <summary>The 16 weeks were played and the closing applied.</summary>
        Completed,

        /// <summary>The level ended early (service collapse or repeated overflow).</summary>
        GameOver
    }

    /// <summary>
    /// Planning engine of the "Temporada de fin de año" level (Documentation/SIMULACION_PLANEACION.md).
    /// Each week the player orders boxes of every SKU and closes the week; this class applies the
    /// weekly step in the exact order of the spec (orders → demand and sales → costs → arrivals →
    /// closing → events), keeps the inventory, transit, costs and service level, and detects game over.
    /// Plain C#: the scene and the HUD only read it and call <see cref="CloseWeek"/>.
    /// </summary>
    public sealed partial class PlanningSimulation
    {
        private readonly int[] onHand;
        private readonly List<TransitLot> transit = new List<TransitLot>();
        private readonly List<WeekReport> history = new List<WeekReport>();
        private readonly List<ScheduledEvent> announced = new List<ScheduledEvent>();
        private readonly int[] liquidatedBoxes;
        private int overflowStreak;

        public PlanningSimulation(int seed, bool allowGameOver = true) : this(new PlanningSeason(seed), allowGameOver) { }

        /// <param name="allowGameOver">False for the Atlas reference, which always plays the whole season.</param>
        public PlanningSimulation(PlanningSeason season, bool allowGameOver = true)
        {
            Season = season ?? throw new ArgumentNullException(nameof(season));
            AllowGameOver = allowGameOver;
            onHand = new int[Skus.Count];
            liquidatedBoxes = new int[Skus.Count];

            for (int sku = 0; sku < Skus.Count; sku++)
            {
                DemandPlan plan = season.Plans[sku];
                onHand[sku] = PlanningRules.Round(plan.Mean(1) * 2);
                for (int week = 2; week <= Skus[sku].LeadTimeWeeks; week++)
                    transit.Add(new TransitLot(sku, PlanningRules.Round(plan.Mean(week)), week, 0));
            }
            ApplyEventsOfWeek();
        }

        public PlanningSeason Season { get; }
        public IReadOnlyList<PlanningSku> Skus => Season.Skus;
        public bool AllowGameOver { get; }

        /// <summary>Week being planned (1..16); 17 once the season is over.</summary>
        public int Week { get; private set; } = 1;

        public PlanningOutcome Outcome { get; private set; } = PlanningOutcome.Playing;
        public bool IsFinished => Outcome != PlanningOutcome.Playing;

        /// <summary>Lots ordered and not yet received, in arrival order.</summary>
        public IReadOnlyList<TransitLot> InTransit => transit;

        /// <summary>Closed weeks, oldest first.</summary>
        public IReadOnlyList<WeekReport> History => history;

        /// <summary>Events already announced to the player, in order.</summary>
        public IReadOnlyList<ScheduledEvent> AnnouncedEvents => announced;

        /// <summary>Event dialog the player must answer before closing the week; null when none.</summary>
        public EventDialog PendingEvent { get; private set; }

        /// <summary>Most the orders of this week may cost, in pesos; null when there is no cap.</summary>
        public long? PurchaseCap { get; private set; }

        public CostBreakdown Costs { get; } = new CostBreakdown();
        public int TotalDemand { get; private set; }
        public int TotalSold { get; private set; }
        public int TotalLost => TotalDemand - TotalSold;
        public int EventsAnswered { get; private set; }

        /// <summary>Sold / demanded since the start (1 before any demand).</summary>
        public double ServiceLevel => TotalDemand == 0 ? 1.0 : (double)TotalSold / TotalDemand;

        /// <summary>Weeks in a row that ended with boxes on the floor.</summary>
        public int OverflowStreak => overflowStreak;

        public GameOverInfo GameOver { get; private set; }

        public int OnHand(int sku) => onHand[sku];

        public int InTransitBoxes(int sku)
        {
            int total = 0;
            foreach (TransitLot lot in transit)
                if (lot.Sku == sku) total += lot.Boxes;
            return total;
        }

        /// <summary>Boxes of a SKU sold off at the closing (0 before it).</summary>
        public int LiquidatedBoxes(int sku) => liquidatedBoxes[sku];

        public int RackCapacity(int sku) => Season.Plans[sku].RackCapacity;

        /// <summary>Statistical plan of a SKU's week, in boxes (what the forecast knows).</summary>
        public double PlanMean(int sku, int week) => Season.Plans[sku].Mean(week);

        /// <summary>
        /// Demand factor of a SKU's week from events already announced (1 when none). The Atlas
        /// suggestion ignores it; it is the information the player has to add by hand.
        /// </summary>
        public double AnnouncedFactor(int sku, int week)
        {
            double factor = 1.0;
            foreach (ScheduledEvent scheduled in announced)
                if (scheduled.Affects(sku, week)) factor *= scheduled.DemandFactor;
            return factor;
        }

        /// <summary>The Atlas suggestion of a SKU for the current week (statistical plan, no events).</summary>
        public AtlasSuggestion Suggest(int sku) =>
            AtlasSuggestion.Compute(Season.Plans[sku], Week, onHand[sku], InTransitBoxes(sku));

        /// <summary>Purchase value of a set of orders (boxes × cost per box), in pesos.</summary>
        public long OrderValue(IReadOnlyList<int> orders)
        {
            long value = 0;
            for (int sku = 0; sku < Skus.Count; sku++) value += Math.Max(0, orders[sku]) * Skus[sku].CostPerBox;
            return value;
        }

        public bool ExceedsPurchaseCap(IReadOnlyList<int> orders) =>
            PurchaseCap.HasValue && OrderValue(orders) > PurchaseCap.Value;

        /// <summary>Answers the pending event with one of its options (0 = the first, default one).</summary>
        public void ResolveEvent(int option)
        {
            EventDialog dialog = PendingEvent ?? throw new InvalidOperationException("There is no event to answer.");
            if (option < 0 || option >= dialog.Options.Count)
                throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown option.");

            if (dialog.Kind == SeasonEventKind.ShipDelay && option == 1) SendHalfByAir();
            dialog.ChosenOption = option;
            PendingEvent = null;
            EventsAnswered++;
        }

        /// <summary>Places the orders (boxes per SKU, catalogue order) and plays the week.</summary>
        public WeekReport CloseWeek(IReadOnlyList<int> orders)
        {
            if (IsFinished) throw new InvalidOperationException("The season is over.");
            if (PendingEvent != null) throw new InvalidOperationException("Answer the pending event first.");
            if (orders == null || orders.Count != Skus.Count)
                throw new ArgumentException($"Expected {Skus.Count} orders.", nameof(orders));
            for (int sku = 0; sku < Skus.Count; sku++)
                if (orders[sku] < 0) throw new ArgumentOutOfRangeException(nameof(orders), "Orders cannot be negative.");
            if (ExceedsPurchaseCap(orders)) throw new InvalidOperationException("The orders exceed this week's purchase cap.");

            int week = Week;
            var costs = new CostBreakdown();

            // 1. Orders go into transit.
            for (int sku = 0; sku < Skus.Count; sku++)
            {
                if (orders[sku] <= 0) continue;
                transit.Add(new TransitLot(sku, orders[sku], week + Skus[sku].LeadTimeWeeks, week));
                costs.Ordering += Skus[sku].OrderCost;
            }

            // 2–3. Demand, sales (lost sales are not backordered) and costs on the closing stock.
            var results = new SkuWeekResult[Skus.Count];
            for (int sku = 0; sku < Skus.Count; sku++)
            {
                double cost = Skus[sku].CostPerBox;
                int demand = Season.Demand(sku, week);
                int sold = Math.Min(onHand[sku], demand);
                onHand[sku] -= sold;
                int ending = onHand[sku];
                int overflow = Math.Max(0, ending - RackCapacity(sku));

                costs.Holding += ending * cost * PlanningRules.HoldingRate;
                costs.Overflow += overflow * cost * PlanningRules.OverflowRate;
                costs.LostSales += (demand - sold) * cost * PlanningRules.LostSaleRate;
                results[sku] = new SkuWeekResult(sku, orders[sku], demand, sold, ending, overflow);
                TotalDemand += demand;
                TotalSold += sold;
            }

            // 4. The purchase cap only lasts one week.
            PurchaseCap = null;

            // 5. Next week: everything due arrives.
            Week = week + 1;
            var arrivals = new List<TransitLot>();
            for (int i = 0; i < transit.Count; i++)
            {
                if (transit[i].ArrivalWeek > Week) continue;
                onHand[transit[i].Sku] += transit[i].Boxes;
                arrivals.Add(transit[i]);
                transit.RemoveAt(i--);
            }

            var report = new WeekReport(week, results, costs, arrivals);
            overflowStreak = report.HasOverflow ? overflowStreak + 1 : 0;

            GameOverInfo gameOver = AllowGameOver ? CheckGameOver(week) : null;
            if (gameOver != null)
            {
                GameOver = gameOver;
                Outcome = PlanningOutcome.GameOver;
                report.GameOver = gameOver;
            }
            else if (Week > PlanningRules.SeasonWeeks)
            {
                // 6. Closing: stock above 3 weeks of January demand is liquidated.
                ApplyClosing(costs);
                Outcome = PlanningOutcome.Completed;
                report.EndedSeason = true;
            }
            else
            {
                // 7. Events of the new week.
                report.NewEvent = ApplyEventsOfWeek();
            }

            Costs.Add(costs);
            report.Comment = CommentOn(report);
            history.Add(report);
            return report;
        }

        private GameOverInfo CheckGameOver(int week)
        {
            if (week >= PlanningRules.GameOverServiceFromWeek && ServiceLevel < PlanningRules.GameOverServiceLevel)
                return new GameOverInfo(week, "Nivel de servicio acumulado", HudFormat.Percent(ServiceLevel),
                    $"Después de la semana {week} solo atendiste el {HudFormat.Percent(ServiceLevel)} de la demanda " +
                    $"(el mínimo es {HudFormat.Percent(PlanningRules.GameOverServiceLevel)}). Las tiendas se quedaron " +
                    "sin producto porque los pedidos no cubrieron la demanda del lead time.",
                    "Repasa el stock de seguridad (SS = Z × σ × √(L+1)) y el nivel objetivo S = F + SS: " +
                    "pide lo que falta para llegar a S cada semana.");

            if (overflowStreak >= PlanningRules.GameOverOverflowWeeks)
                return new GameOverInfo(week, "Desborde de bodega",
                    $"{overflowStreak} semanas seguidas",
                    $"Durante {overflowStreak} semanas seguidas hubo cajas en el piso porque el inventario superó " +
                    "la capacidad del rack. Julián no tiene dónde guardar más mercancía.",
                    "Repasa el tamaño del lote (EOQ = √(2·D·S/H)) y la posición de inventario: no pidas por encima " +
                    "del nivel objetivo, el lote también depende del espacio.");
            return null;
        }

        private void ApplyClosing(CostBreakdown costs)
        {
            for (int sku = 0; sku < Skus.Count; sku++)
            {
                int inventory = onHand[sku] + InTransitBoxes(sku);
                int allowed = ClosingAllowance(sku);
                int excess = Math.Max(0, inventory - allowed);
                liquidatedBoxes[sku] = excess;
                costs.Liquidation += excess * Skus[sku].CostPerBox * PlanningRules.LiquidationRate;
            }
        }

        /// <summary>Boxes of a SKU that may remain at the closing: round(3 × January mean).</summary>
        public int ClosingAllowance(int sku) =>
            PlanningRules.Round(PlanningRules.ClosingCoverWeeks * PlanMean(sku, PlanningRules.ClosingWeek));

        private void SendHalfByAir()
        {
            int sku = PlanningCatalog.Perfume;
            int remaining = PlanningRules.Round(InTransitBoxes(sku) / 2.0);
            int airBoxes = remaining;
            foreach (TransitLot lot in transit)
            {
                if (lot.Sku != sku || remaining == 0) continue;
                int taken = Math.Min(lot.Boxes, remaining);
                lot.Boxes -= taken;
                remaining -= taken;
            }
            transit.RemoveAll(lot => lot.Boxes == 0);
            transit.Add(new TransitLot(sku, airBoxes, Week + 1, Week, isAirFreight: true));
            Costs.AirFreight += PlanningRules.AirFreightCost;
        }
    }
}
