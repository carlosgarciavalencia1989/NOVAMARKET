using System;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    public class OperationStateTests
    {
        private OperationState state;

        [SetUp]
        public void SetUp() => state = SampleOperationData.Create();

        [Test]
        public void SampleData_MatchesTheLevelThreeHud()
        {
            Assert.AreEqual("Nivel III: Planeación S&OP", state.Mission.LevelHeader);
            Assert.AreEqual("0/8", HudFormat.Ratio(state.Mission.CounterValue, state.Mission.CounterTarget));
            Assert.AreEqual(109, state.Resources.OccupiedPositions);
            Assert.AreEqual(200, state.Resources.PositionCapacity);
            Assert.AreEqual(4, state.Facilities.Count);
        }

        [Test]
        public void EveryFacilityId_HasSampleData()
        {
            foreach (string id in new[] { CediFacilityIds.Warehouse, CediFacilityIds.Gate,
                         CediFacilityIds.Office, CediFacilityIds.ContainerYard })
            {
                Assert.IsTrue(state.Facilities.TryGet(id, out FacilityInfo info), id);
                Assert.IsNotEmpty(info.Stats, id);
                Assert.IsNotEmpty(info.Orders, id);
            }
        }

        [Test]
        public void IssueOrder_ReachesTheSimulationAndConfirmsWithANotice()
        {
            string issued = null;
            state.OrderIssued += (facility, order) => issued = facility + "/" + order;

            state.IssueOrder(CediFacilityIds.Gate, "admit-truck", now: 0);

            Assert.AreEqual("gate/admit-truck", issued);
            StringAssert.Contains("Autorizar ingreso", state.Notifications.Active[0].Message);
        }

        [Test]
        public void IssueOrder_RejectsUnknownOrders() =>
            Assert.Throws<ArgumentException>(() => state.IssueOrder(CediFacilityIds.Gate, "fly", 0));

        [Test]
        public void RegisterDispatch_AdvancesCounterObjectiveAndScore()
        {
            int startScore = state.Mission.Score;

            state.RegisterDispatch(SampleOperationData.DispatchObjectiveId, 150, now: 0);

            Assert.AreEqual(1, state.Mission.CounterValue);
            Assert.AreEqual(1, state.Mission.Find(SampleOperationData.DispatchObjectiveId).Progress);
            Assert.AreEqual(startScore + 150, state.Mission.Score);
            Assert.AreEqual(NotificationSeverity.Success, state.Notifications.Active[0].Severity);
        }

        [Test]
        public void RegisterDispatch_StopsAtTheTarget()
        {
            for (int i = 0; i < 10; i++) state.RegisterDispatch(SampleOperationData.DispatchObjectiveId, 100, i);
            Assert.AreEqual(8, state.Mission.CounterValue);
            Assert.IsTrue(state.Mission.Find(SampleOperationData.DispatchObjectiveId).IsCompleted);
        }

        [Test]
        public void Resources_ReportOccupancyAndServiceTarget()
        {
            ResourceState resources = state.Resources;
            Assert.AreEqual(0.545, resources.Occupancy, 1e-9);
            Assert.IsFalse(resources.IsStorageCritical);
            Assert.IsFalse(resources.IsServiceBelowTarget);

            resources.SetOccupiedPositions(250);
            resources.SetServiceLevel(0.93);
            Assert.AreEqual(200, resources.OccupiedPositions);
            Assert.IsTrue(resources.IsStorageCritical);
            Assert.IsTrue(resources.IsServiceBelowTarget);
        }
    }
}
