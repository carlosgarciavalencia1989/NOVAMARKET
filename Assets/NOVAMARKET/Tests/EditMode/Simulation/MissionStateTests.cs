using System;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    public class MissionStateTests
    {
        private MissionState mission;

        [SetUp]
        public void SetUp()
        {
            mission = new MissionState(3, "Planeación S&OP", "Despachos completados", 8);
            mission.AddObjective(new Objective("dispatch", "Completa 8 despachos", true, 8, 400));
            mission.AddObjective(new Objective("meeting", "Reunión S&OP", false, 1, 150));
        }

        [Test]
        public void LevelHeader_UsesRomanNumerals() =>
            Assert.AreEqual("Nivel III: Planeación S&OP", mission.LevelHeader);

        [Test]
        public void Counter_IsClampedToTheTarget()
        {
            mission.SetCounter(12);
            Assert.AreEqual(8, mission.CounterValue);
            mission.SetCounter(-1);
            Assert.AreEqual(0, mission.CounterValue);
        }

        [Test]
        public void Objective_AwardsItsPointsOnceWhenCompleted()
        {
            Objective completed = null;
            mission.ObjectiveCompleted += o => completed = o;

            mission.AdvanceObjective("meeting");
            mission.AdvanceObjective("meeting");

            Assert.AreEqual("meeting", completed.Id);
            Assert.IsTrue(mission.Find("meeting").IsCompleted);
            Assert.AreEqual(150, mission.Score);
        }

        [Test]
        public void ProgressObjective_CompletesOnlyAtItsTarget()
        {
            Objective dispatch = mission.Find("dispatch");
            mission.SetObjectiveProgress("dispatch", 7);
            Assert.IsFalse(dispatch.IsCompleted);
            Assert.IsTrue(dispatch.ShowsProgress);

            mission.AdvanceObjective("dispatch");
            Assert.IsTrue(dispatch.IsCompleted);
            Assert.AreEqual(400, mission.Score);
        }

        [Test]
        public void AllPrimaryCompleted_IgnoresSecondaryObjectives()
        {
            Assert.IsFalse(mission.AllPrimaryCompleted);
            mission.SetObjectiveProgress("dispatch", 8);
            Assert.IsTrue(mission.AllPrimaryCompleted);
        }

        [Test]
        public void Changed_IsRaisedOnUpdates()
        {
            int changes = 0;
            mission.Changed += () => changes++;
            mission.SetCounter(1);
            mission.AddScore(10);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void DuplicateObjectiveIds_AreRejected() =>
            Assert.Throws<ArgumentException>(() => mission.AddObjective(new Objective("dispatch", "x", true)));
    }
}
