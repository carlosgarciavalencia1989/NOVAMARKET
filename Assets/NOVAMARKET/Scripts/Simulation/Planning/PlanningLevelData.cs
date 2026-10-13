using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// HUD state of the first playable level, "Temporada de fin de año": mission with the season
    /// objectives, resources sized to the rack space of the four SKUs, and the CEDI facilities
    /// (the office opens the order plan).
    /// </summary>
    public static class PlanningLevelData
    {
        public const int LevelNumber = 1;
        public const string LevelTitle = "Temporada de fin de año";

        /// <summary>Operating budget of the season; the HUD shows it minus the costs so far.</summary>
        public const long SeasonBudget = 120_000_000;

        public const int Operators = 12;

        public const string ServiceObjectiveId = "season-service";
        public const string FloorObjectiveId = "season-no-floor";
        public const string EventsObjectiveId = "season-events";
        public const string BeatAtlasObjectiveId = "season-beat-atlas";
        public const string NoLiquidationObjectiveId = "season-no-liquidation";

        /// <summary>Office order that opens the order plan panel.</summary>
        public const string OpenPlanOrderId = "open-plan";

        public static OperationState CreateState() =>
            new OperationState(CreateMission(), CreateResources(), new NotificationFeed(), CreateFacilities());

        public static MissionState CreateMission()
        {
            int events = new PlanningSeason(1).Events.Count;
            var mission = new MissionState(LevelNumber, LevelTitle, "Semanas cerradas", PlanningRules.SeasonWeeks);
            mission.AddObjective(new Objective(ServiceObjectiveId, "Termina la temporada con servicio de 95 % o más", true, 1, 300));
            mission.AddObjective(new Objective(FloorObjectiveId, "No dejes cajas en el piso 3 semanas seguidas", true, 1, 200));
            mission.AddObjective(new Objective(EventsObjectiveId, "Atiende los avisos de los personajes", false, events, 200));
            mission.AddObjective(new Objective(BeatAtlasObjectiveId, "Gasta menos que la política del Atlas", false, 1, 300));
            mission.AddObjective(new Objective(NoLiquidationObjectiveId, "Cierra la temporada sin liquidar inventario", false, 1, 150));
            return mission;
        }

        public static ResourceState CreateResources() =>
            new ResourceState(occupiedPositions: 0, positionCapacity: PositionCapacity(PlanningCatalog.Skus),
                budget: SeasonBudget, trucksInYard: 0, operators: Operators, serviceLevel: 1.0,
                serviceLevelTarget: PlanningRules.TwoStarService);

        /// <summary>Rack positions of the level: the rack capacity of every SKU, in pallets.</summary>
        public static int PositionCapacity(IReadOnlyList<PlanningSku> skus)
        {
            int positions = 0;
            foreach (PlanningSku sku in skus) positions += sku.PalletsFor(new DemandPlan(sku).RackCapacity);
            return positions;
        }

        public static FacilityDirectory CreateFacilities()
        {
            FacilityDirectory directory = SampleOperationData.CreateFacilities();
            directory.Set(OfficeInfo(new[] { new FacilityStat("Semana", "1 de 16 · Septiembre") }));
            return directory;
        }

        /// <summary>The office with the planning data rows and the order that opens the plan.</summary>
        public static FacilityInfo OfficeInfo(IReadOnlyList<FacilityStat> stats) =>
            new FacilityInfo(CediFacilityIds.Office, "Oficina de planeación", "Plan de temporada · Andrés (S&OP)",
                FacilityKind.Office, stats,
                new[] { new FacilityOrder(OpenPlanOrderId, "Abrir plan de pedidos") });
    }
}
