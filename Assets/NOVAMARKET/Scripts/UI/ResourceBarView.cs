using NovaMarket.Simulation;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>Bottom-right resource bar: positions, budget, trucks, operators and service level.</summary>
    public sealed class ResourceBarView
    {
        private readonly ResourceState resources;
        private readonly Label positions;
        private readonly Label budget;
        private readonly Label trucks;
        private readonly Label operators;
        private readonly Label service;
        private readonly VisualElement positionsItem;
        private readonly VisualElement serviceItem;

        public ResourceBarView(VisualElement root, ResourceState resources)
        {
            this.resources = resources;
            positions = root.Q<Label>("positionsValue");
            budget = root.Q<Label>("budgetValue");
            trucks = root.Q<Label>("trucksValue");
            operators = root.Q<Label>("operatorsValue");
            service = root.Q<Label>("serviceValue");
            positionsItem = root.Q("positionsResource");
            serviceItem = root.Q("serviceResource");

            resources.Changed += Refresh;
            Refresh();
        }

        public void Dispose() => resources.Changed -= Refresh;

        private void Refresh()
        {
            positions.text = HudFormat.Ratio(resources.OccupiedPositions, resources.PositionCapacity);
            budget.text = HudFormat.Pesos(resources.Budget);
            trucks.text = HudFormat.Number(resources.TrucksInYard);
            operators.text = HudFormat.Number(resources.Operators);
            service.text = HudFormat.Percent(resources.ServiceLevel);

            positionsItem.EnableInClassList("resource--alert", resources.IsStorageCritical);
            serviceItem.EnableInClassList("resource--alert", resources.IsServiceBelowTarget);
            serviceItem.EnableInClassList("resource--good", !resources.IsServiceBelowTarget);
        }
    }
}
