using System.Linq;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Levels;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    public class CampaignHudDataTests
    {
        [Test]
        public void MissionReset_StartsAnotherLevelAndKeepsTheScore()
        {
            MissionState mission = SampleOperationData.CreateMission();
            mission.AddScore(250);
            int changes = 0;
            mission.Changed += () => changes++;

            mission.Reset("Nivel 1.1 · Reunir la historia", "Pasos completados", 4);

            Assert.AreEqual("Nivel 1.1 · Reunir la historia", mission.LevelHeader);
            Assert.AreEqual("Pasos completados", mission.CounterLabel);
            Assert.AreEqual(4, mission.CounterTarget);
            Assert.AreEqual(0, mission.CounterValue);
            Assert.IsEmpty(mission.Objectives);
            Assert.AreEqual(250, mission.Score);
            Assert.AreEqual(1, changes);

            mission.SetScore(700);
            Assert.AreEqual(700, mission.Score, "The campaign shows the total of its best results.");
            mission.SetScore(-5);
            Assert.AreEqual(0, mission.Score);
        }

        [Test]
        public void LevelChecklist_FollowsTheSheet_AndCanBeUndone()
        {
            OperationState state = CampaignHudData.CreateState();
            var level = new GatherHistoryLevel(LevelPlayers.Portfolio, seed: 4);

            CampaignHudData.StartLevel(state.Mission, level);
            Assert.AreEqual("Nivel 1.1 · Reunir la historia", state.Mission.LevelHeader);
            Assert.AreEqual(4, state.Mission.Objectives.Count);
            Assert.AreEqual(0, state.Mission.CounterValue);

            LevelPlayers.SolveGather(level);
            CampaignHudData.Refresh(state.Mission, level);
            Assert.AreEqual(4, state.Mission.CounterValue);
            Assert.IsTrue(state.Mission.Objectives.All(o => o.IsCompleted));

            level.Book.Sheet(level.HistorySheet).SetInput("C1", string.Empty);
            CampaignHudData.Refresh(state.Mission, level);
            Assert.AreEqual(3, state.Mission.CounterValue, "Deleting a period un-ticks that step.");
            Assert.IsFalse(state.Mission.Find(CampaignHudData.ObjectiveId(2)).IsCompleted);
        }
    }
}
