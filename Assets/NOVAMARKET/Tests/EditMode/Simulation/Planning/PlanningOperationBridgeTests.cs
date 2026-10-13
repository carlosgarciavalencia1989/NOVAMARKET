using System.Linq;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Planning
{
    public class PlanningOperationBridgeTests
    {
        private const int Seed = 5;

        private OperationState state;
        private CediOperationSimulation operation;
        private PlanningOperationBridge bridge;
        private PlanningSimulation planning;

        [SetUp]
        public void SetUp()
        {
            state = PlanningLevelData.CreateState();
            operation = new CediOperationSimulation(state, CediOperationSettings.ForPlanning(), seed: 3);
            bridge = new PlanningOperationBridge(state, operation, seed: 3);
            planning = new PlanningSimulation(Seed);
            bridge.Begin(planning, now: 0);
        }

        private WeekReport Close(int[] orders)
        {
            if (planning.PendingEvent != null)
            {
                planning.ResolveEvent(0);
                bridge.OnEventAnswered(planning.History.Last().NewEvent, 0);
            }
            WeekReport report = planning.CloseWeek(orders);
            bridge.OnWeekClosed(report, 0);
            return report;
        }

        [Test]
        public void LevelState_ShowsTheSeasonMissionAndTheRackPositions()
        {
            Assert.AreEqual("Nivel I: Temporada de fin de año", state.Mission.LevelHeader);
            Assert.AreEqual(16, state.Mission.CounterTarget);
            // Rack capacity in pallets: labial 210/12 → 18, perfume 240/10 → 24, set 42/4 → 11, cream 78/8 → 10.
            Assert.AreEqual(63, state.Resources.PositionCapacity);
            Assert.IsTrue(state.Facilities.TryGet(CediFacilityIds.Office, out FacilityInfo office));
            Assert.IsNotNull(office.FindOrder(PlanningLevelData.OpenPlanOrderId));
        }

        [Test]
        public void Begin_ShowsTheStartingStockAsPositions()
        {
            int expected = 0;
            for (int sku = 0; sku < 4; sku++) expected += planning.Skus[sku].PalletsFor(planning.OnHand(sku));

            Assert.AreEqual(expected, state.Resources.OccupiedPositions);
            CollectionAssert.AreEqual(Enumerable.Range(0, 4).Select(s => planning.Skus[s].PalletsFor(planning.OnHand(s))),
                bridge.PalletsBySku);
            Assert.AreEqual(1.0, state.Resources.ServiceLevel);
        }

        [Test]
        public void ClosedWeek_SchedulesArrivalTrucksAndSalesTurbos()
        {
            WeekReport report = Close(new[] { 30, 40, 0, 0 });

            int arrivals = report.Arrivals.Count; // each lot fits in one truck
            int sales = report.Skus.Count(r => r.Sold > 0);
            Assert.AreEqual(arrivals + sales, operation.ScheduledTrucks);
            Assert.AreEqual(1, state.Mission.CounterValue);
            Assert.AreEqual(planning.ServiceLevel, state.Resources.ServiceLevel, 1e-9);
            Assert.AreEqual(PlanningLevelData.SeasonBudget - (long)System.Math.Round(planning.Costs.Total),
                state.Resources.Budget);
        }

        [Test]
        public void BigLots_AreSplitIntoSeveralVehicles()
        {
            // 400 boxes of labial = 34 pallets → 3 trucks of at most 12 pallets.
            WeekReport report = Close(new[] { 400, 0, 0, 0 });

            int expected = 0;
            foreach (TransitLot lot in report.Arrivals)
                expected += Vehicles(planning.Skus[lot.Sku].PalletsFor(lot.Boxes), operation.Settings.MaxPalletsPerTruck);
            foreach (SkuWeekResult result in report.Skus)
                expected += Vehicles(planning.Skus[result.Sku].PalletsFor(result.Sold), operation.Settings.MaxPalletsPerDispatch);

            Assert.AreEqual(3, Vehicles(planning.Skus[PlanningCatalog.Labial].PalletsFor(400), 12));
            Assert.AreEqual(expected, operation.ScheduledTrucks);
        }

        private static int Vehicles(int pallets, int perVehicle) => (pallets + perVehicle - 1) / perVehicle;

        [Test]
        public void ScheduledTrucks_DriveIntoTheCedi_WithTheirOrder()
        {
            Close(new[] { 30, 40, 0, 0 });
            int dispatchesBefore = state.Mission.CounterValue;

            for (int i = 0; i < 2400; i++) operation.Tick(0.05f, operation.Time); // two minutes

            Assert.Greater(operation.TrucksEntered, 0);
            TruckAgent truck = operation.Vehicles.OfType<TruckAgent>().FirstOrDefault();
            if (truck != null) StringAssert.Contains("cajas", truck.Cargo);
            Assert.AreEqual(dispatchesBefore, state.Mission.CounterValue, "Turbos do not move the week counter.");
        }

        [Test]
        public void PlanningOperation_NeitherSpawnsRandomTrucksNorChangesThePositions()
        {
            int positions = state.Resources.OccupiedPositions;

            for (int i = 0; i < 2400; i++) operation.Tick(0.05f, operation.Time);

            Assert.AreEqual(0, operation.TrucksEntered);
            Assert.AreEqual(positions, state.Resources.OccupiedPositions);
        }

        [Test]
        public void AnsweredEvents_AdvanceTheirObjective()
        {
            while (planning.Week < 4) Close(PlanningPolicies.AtlasOrders(planning));
            if (planning.PendingEvent != null)
            {
                planning.ResolveEvent(0);
                bridge.OnEventAnswered(planning.History.Last().NewEvent, 0);
            }

            Assert.AreEqual(planning.EventsAnswered,
                state.Mission.Find(PlanningLevelData.EventsObjectiveId).Progress);
            Assert.Greater(planning.EventsAnswered, 0);
        }

        [Test]
        public void SeasonEnd_ChecksTheObjectivesAndAddsTheStarPoints()
        {
            var season = new PlanningSeason(Seed);
            PlanningSimulation informed = InformedPlayer.Play(season);
            bridge.Begin(informed, 0);
            SeasonResult result = SeasonResult.Evaluate(informed, PlanningPolicies.PlayReference(season));

            bridge.OnSeasonEnded(result, 0);

            Assert.AreEqual(result.ServiceLevel >= 0.95,
                state.Mission.Find(PlanningLevelData.ServiceObjectiveId).IsCompleted);
            Assert.IsTrue(state.Mission.Find(PlanningLevelData.FloorObjectiveId).IsCompleted);
            Assert.GreaterOrEqual(state.Mission.Score, result.Points);
        }

        [Test]
        public void Begin_AgainResetsTheMission()
        {
            Close(PlanningPolicies.AtlasOrders(planning));
            state.Mission.AddScore(500);

            bridge.Begin(new PlanningSimulation(Seed + 1), 0);

            Assert.AreEqual(0, state.Mission.CounterValue);
            Assert.AreEqual(0, state.Mission.Score);
            Assert.IsTrue(state.Mission.Objectives.All(o => o.Progress == 0));
        }
    }
}
