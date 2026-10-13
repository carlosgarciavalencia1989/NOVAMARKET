using System.Collections.Generic;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>
    /// HUD state of the planner campaign: the CEDI keeps its illustrative resources in the background,
    /// and the objectives panel shows the checklist of the level being played.
    /// </summary>
    public static class CampaignHudData
    {
        public const string StepsLabel = "Pasos completados";

        public static OperationState CreateState() => new OperationState(
            new MissionState(ForecastWorld.Number, ForecastWorld.Name, StepsLabel, 0),
            SampleOperationData.CreateResources(), new NotificationFeed(), SampleOperationData.CreateFacilities());

        public static string ObjectiveId(int index) => $"paso-{index + 1}";

        /// <summary>Shows the level's checklist in the objectives panel.</summary>
        public static void StartLevel(MissionState mission, LevelSession session)
        {
            IReadOnlyList<LevelObjective> steps = session.Objectives();
            mission.Reset($"Nivel {session.Info.Id} · {session.Info.Title}", StepsLabel, steps.Count);
            for (int i = 0; i < steps.Count; i++)
                mission.AddObjective(new Objective(ObjectiveId(i), steps[i].Text, isPrimary: true));
            Refresh(mission, session);
        }

        /// <summary>Ticks the steps the player has done (they can be undone: a step is re-checked from the sheet).</summary>
        public static void Refresh(MissionState mission, LevelSession session)
        {
            IReadOnlyList<LevelObjective> steps = session.Objectives();
            int done = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (mission.Find(ObjectiveId(i)) == null) continue;
                mission.SetObjectiveProgress(ObjectiveId(i), steps[i].Done ? 1 : 0);
                if (steps[i].Done) done++;
            }
            mission.SetCounter(done);
        }
    }
}
