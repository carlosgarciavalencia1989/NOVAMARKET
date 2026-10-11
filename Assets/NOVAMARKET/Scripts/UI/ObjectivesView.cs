using NovaMarket.Simulation;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>Top-right block: level bar, dispatch counter, objective checklists and score.</summary>
    public sealed class ObjectivesView
    {
        private readonly MissionState mission;
        private readonly Label levelLabel;
        private readonly Label counterLabel;
        private readonly Label counterValue;
        private readonly VisualElement primaryList;
        private readonly VisualElement secondaryList;
        private readonly Label scoreValue;

        public ObjectivesView(VisualElement root, MissionState mission)
        {
            this.mission = mission;
            levelLabel = root.Q<Label>("levelLabel");
            counterLabel = root.Q<Label>("counterLabel");
            counterValue = root.Q<Label>("counterValue");
            primaryList = root.Q("primaryObjectives");
            secondaryList = root.Q("secondaryObjectives");
            scoreValue = root.Q<Label>("scoreValue");

            mission.Changed += Refresh;
            Refresh();
        }

        public void Dispose() => mission.Changed -= Refresh;

        private void Refresh()
        {
            levelLabel.text = mission.LevelHeader;
            counterLabel.text = mission.CounterLabel;
            counterValue.text = HudFormat.Ratio(mission.CounterValue, mission.CounterTarget);
            scoreValue.text = HudFormat.Number(mission.Score);

            primaryList.Clear();
            secondaryList.Clear();
            foreach (Objective objective in mission.Objectives)
                (objective.IsPrimary ? primaryList : secondaryList).Add(CreateRow(objective));
        }

        private static VisualElement CreateRow(Objective objective)
        {
            var row = new VisualElement();
            row.AddToClassList("objective-row");
            if (objective.IsCompleted) row.AddToClassList("objective-row--done");
            if (!objective.IsPrimary) row.AddToClassList("objective-row--secondary");

            var box = new VisualElement();
            box.AddToClassList("objective-box");
            if (objective.IsCompleted) box.Add(new HudIcon { Kind = HudIconKind.Check });
            row.Add(box);

            var text = new Label(objective.Text);
            text.AddToClassList("objective-text");
            row.Add(text);

            if (objective.ShowsProgress)
            {
                var progress = new Label(HudFormat.Ratio(objective.Progress, objective.Target));
                progress.AddToClassList("objective-progress");
                row.Add(progress);
            }
            return row;
        }
    }
}
