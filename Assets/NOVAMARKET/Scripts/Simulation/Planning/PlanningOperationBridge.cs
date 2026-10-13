using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// Connects the planning engine with the CEDI operation and the HUD state. After each closed
    /// week it schedules one receiving truck per arrived lot (split when it does not fit in one
    /// truck) and dispatch turbos for the sales, and updates positions, budget, service level,
    /// mission and notices. Plain C#: the scene calls it, the tests drive it directly.
    /// </summary>
    public sealed class PlanningOperationBridge
    {
        private const string AirOrigin = "Aeropuerto José María Córdova (Rionegro)";

        private readonly OperationState state;
        private readonly CediOperationSimulation operation;
        private readonly Random random;
        private int[] pallets = new int[0];

        /// <param name="operation">CEDI operation that receives the trucks; null to only update the HUD.</param>
        public PlanningOperationBridge(OperationState state, CediOperationSimulation operation, int seed = 1)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.operation = operation;
            random = new Random(seed);
        }

        /// <summary>Raised when the pallets in the racks change (to redraw them).</summary>
        public event Action PalletsChanged;

        public PlanningSimulation Simulation { get; private set; }

        /// <summary>Rack positions in use per SKU (catalogue order).</summary>
        public IReadOnlyList<int> PalletsBySku => pallets;

        /// <summary>Starts (or restarts) the level with a new season: resets mission and resources.</summary>
        public void Begin(PlanningSimulation simulation, double now)
        {
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            MissionState mission = state.Mission;
            mission.SetCounter(0);
            foreach (Objective objective in mission.Objectives) mission.SetObjectiveProgress(objective.Id, 0);
            mission.AddScore(-mission.Score);
            operation?.ClearScheduledTrucks();

            PublishWarehouse();
            PublishOffice();
            state.Notifications.Push(
                $"Temporada de fin de año: planea 16 semanas de pedidos para 4 SKU. Abre el plan y cierra la semana 1.",
                NotificationSeverity.Info, now);
        }

        /// <summary>Turns a closed week into trucks, turbos and HUD updates.</summary>
        public void OnWeekClosed(WeekReport report, double now)
        {
            RequireSimulation();
            int trucks = 0, turbos = 0;
            foreach (TransitLot lot in report.Arrivals) trucks += ScheduleArrival(lot);
            foreach (SkuWeekResult result in report.Skus) turbos += ScheduleSales(result, report.Week);

            state.Mission.SetCounter(report.Week);
            PublishWarehouse();
            PublishOffice();

            double lostShare = report.Demand == 0 ? 0 : (double)report.Lost / report.Demand;
            state.Notifications.Push(
                $"Semana {report.Week} cerrada: vendiste {HudFormat.Number(report.Sold)} de " +
                $"{HudFormat.Number(report.Demand)} cajas. {trucks} camiones y {turbos} turbos en camino.",
                lostShare > 0.05 ? NotificationSeverity.Warning : NotificationSeverity.Success, now);
            if (report.Comment != null)
                state.Notifications.Push($"{PlanningCharacters.Name(report.Comment.Character)}: {report.Comment.Text}",
                    SeverityOf(report.Comment.Mood), now);

            if (report.GameOver != null)
                state.Notifications.Push($"Game over: {report.GameOver.Indicator} ({report.GameOver.Value}).",
                    NotificationSeverity.Critical, now);
            else if (report.NewEvent != null)
                state.Notifications.Push(
                    $"{PlanningCharacters.Name(report.NewEvent.Character)} trae un aviso: {report.NewEvent.Title}.",
                    NotificationSeverity.Info, now);
        }

        /// <summary>Counts an answered event and reflects its effects (damaged stock, air freight).</summary>
        public void OnEventAnswered(EventDialog dialog, double now)
        {
            RequireSimulation();
            state.Mission.SetObjectiveProgress(PlanningLevelData.EventsObjectiveId, Simulation.EventsAnswered);
            PublishWarehouse();
            PublishOffice();
            if (dialog.Kind == SeasonEventKind.ShipDelay && dialog.ChosenOption == 1)
                state.Notifications.Push("Don Hernán despacha la mitad del perfume por avión.", NotificationSeverity.Info, now);
        }

        /// <summary>Checks the season objectives and adds the stars' points.</summary>
        public void OnSeasonEnded(SeasonResult result, double now)
        {
            RequireSimulation();
            bool completed = result.Outcome == PlanningOutcome.Completed;
            MissionState mission = state.Mission;
            if (completed && result.ServiceLevel >= PlanningRules.TwoStarService)
                mission.SetObjectiveProgress(PlanningLevelData.ServiceObjectiveId, 1);
            if (completed)
                mission.SetObjectiveProgress(PlanningLevelData.FloorObjectiveId, 1);
            if (completed && result.SavingsVersusReference >= 0)
                mission.SetObjectiveProgress(PlanningLevelData.BeatAtlasObjectiveId, 1);
            if (completed && result.Costs.Liquidation <= 0)
                mission.SetObjectiveProgress(PlanningLevelData.NoLiquidationObjectiveId, 1);
            mission.AddScore(result.Points);

            string message = result.Passed
                ? $"Temporada superada con {result.Stars} {(result.Stars == 1 ? "estrella" : "estrellas")}. ¡Ganaste el {SeasonResult.GemName}!"
                : "Nivel no superado: revisa las lecciones y repite con una semilla nueva.";
            state.Notifications.Push(message, result.Passed ? NotificationSeverity.Success : NotificationSeverity.Critical, now);
        }

        // ------------------------------------------------------------------ trucks

        /// <summary>One truck per lot; lots bigger than a truck are split. Returns the trucks scheduled.</summary>
        private int ScheduleArrival(TransitLot lot)
        {
            PlanningSku sku = Simulation.Skus[lot.Sku];
            int perTruck = operation?.Settings.MaxPalletsPerTruck ?? int.MaxValue;
            string supplier = lot.IsAirFreight ? $"{sku.Supplier} · carga aérea" : sku.Supplier;
            string origin = lot.IsAirFreight ? AirOrigin : sku.Origin;
            string suffix = lot.IsAirFreight ? " · avión" : lot.DelayWeeks > 0 ? $" · {lot.DelayWeeks} semanas tarde" : string.Empty;
            return Split(sku, lot.Boxes, perTruck, (boxes, palletCount) =>
                operation?.ScheduleReceiving(supplier, origin, sku.Name, palletCount,
                    $"{sku.Code} · {HudFormat.Number(boxes)} cajas · semana {lot.OrderedWeek}{suffix}"));
        }

        /// <summary>The week's sales leave in turbos to the valley's stores. Returns the turbos scheduled.</summary>
        private int ScheduleSales(SkuWeekResult result, int week)
        {
            if (result.Sold <= 0) return 0;
            PlanningSku sku = Simulation.Skus[result.Sku];
            int perTurbo = operation?.Settings.MaxPalletsPerDispatch ?? int.MaxValue;
            return Split(sku, result.Sold, perTurbo, (boxes, palletCount) =>
            {
                string destination = CediCatalog.Destinations[random.Next(CediCatalog.Destinations.Length)];
                operation?.ScheduleDispatch(destination, sku.Name, palletCount,
                    $"{sku.Code} · {HudFormat.Number(boxes)} cajas · semana {week}");
            });
        }

        /// <summary>Splits boxes into vehicles of at most <paramref name="maxPallets"/> pallets.</summary>
        private static int Split(PlanningSku sku, int boxes, int maxPallets, Action<int, int> schedule)
        {
            int totalPallets = sku.PalletsFor(boxes);
            if (totalPallets == 0) return 0;
            int vehicles = (totalPallets + maxPallets - 1) / maxPallets;
            int remaining = boxes;
            for (int i = 0; i < vehicles; i++)
            {
                int share = i == vehicles - 1 ? remaining : boxes / vehicles;
                remaining -= share;
                schedule(share, Math.Max(1, sku.PalletsFor(share)));
            }
            return vehicles;
        }

        // ------------------------------------------------------------------ HUD

        private void PublishWarehouse()
        {
            int skus = Simulation.Skus.Count;
            if (pallets.Length != skus) pallets = new int[skus];
            int total = 0;
            for (int sku = 0; sku < skus; sku++)
            {
                pallets[sku] = Simulation.Skus[sku].PalletsFor(Simulation.OnHand(sku));
                total += pallets[sku];
            }

            ResourceState resources = state.Resources;
            resources.SetOccupiedPositions(total);
            resources.SetServiceLevel(Simulation.ServiceLevel);
            resources.SetBudget(PlanningLevelData.SeasonBudget - (long)Math.Round(Simulation.Costs.Total));
            PalletsChanged?.Invoke();
        }

        private void PublishOffice()
        {
            int week = Math.Min(Simulation.Week, PlanningRules.SeasonWeeks);
            string weekText = Simulation.IsFinished
                ? "Temporada terminada"
                : $"{week} de {PlanningRules.SeasonWeeks} · {PlanningRules.MonthName(week)}";
            state.Facilities.Set(PlanningLevelData.OfficeInfo(new[]
            {
                new FacilityStat("Semana", weekText),
                new FacilityStat("Nivel de servicio", HudFormat.Percent(Simulation.ServiceLevel)),
                new FacilityStat("Costo de la temporada", HudFormat.Pesos((long)Math.Round(Simulation.Costs.Total))),
                new FacilityStat("Ventas perdidas", $"{HudFormat.Number(Simulation.TotalLost)} cajas"),
                new FacilityStat("Avisos atendidos", HudFormat.Ratio(Simulation.EventsAnswered, Simulation.Season.Events.Count))
            }));
        }

        private static NotificationSeverity SeverityOf(CharacterMood mood) => mood switch
        {
            CharacterMood.Happy => NotificationSeverity.Success,
            CharacterMood.Angry => NotificationSeverity.Warning,
            CharacterMood.Worried => NotificationSeverity.Warning,
            _ => NotificationSeverity.Info
        };

        private void RequireSimulation()
        {
            if (Simulation == null) throw new InvalidOperationException("Call Begin first.");
        }
    }
}
