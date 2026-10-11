using System;
using NovaMarket.Interaction;
using NovaMarket.Simulation;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Bottom-left panel for the selected object: icon, name, data and order buttons.
    /// CEDI facilities take their data from the <see cref="FacilityDirectory"/>; any other
    /// <see cref="IInteractable"/> shows its own name and description.
    /// </summary>
    public sealed class SelectionPanelView
    {
        private readonly OperationState state;
        private readonly Func<double> clock;
        private readonly VisualElement panel;
        private readonly HudIcon icon;
        private readonly Label nameLabel;
        private readonly Label subtitleLabel;
        private readonly Label descriptionLabel;
        private readonly VisualElement stats;
        private readonly VisualElement orders;
        private IInteractable selected;

        public SelectionPanelView(VisualElement root, OperationState state, Func<double> clock)
        {
            this.state = state;
            this.clock = clock;
            panel = root.Q("selectionPanel");
            icon = root.Q<HudIcon>("selectionIcon");
            nameLabel = root.Q<Label>("selectionName");
            subtitleLabel = root.Q<Label>("selectionSubtitle");
            descriptionLabel = root.Q<Label>("selectionDescription");
            stats = root.Q("selectionStats");
            orders = root.Q("selectionOrders");

            state.Facilities.Changed += OnFacilityChanged;
            Show(null);
        }

        public void Dispose() => state.Facilities.Changed -= OnFacilityChanged;

        public void Show(IInteractable target)
        {
            selected = target;
            panel.EnableInClassList("hidden", target == null);
            if (target == null) return;

            if (target is CediFacility facility && state.Facilities.TryGet(facility.FacilityId, out FacilityInfo info))
                ShowFacility(info);
            else
                ShowGeneric(target);
        }

        private void ShowFacility(FacilityInfo info)
        {
            icon.Kind = IconFor(info.Kind);
            nameLabel.text = info.Name;
            SetText(subtitleLabel, info.Subtitle);
            SetText(descriptionLabel, null);

            stats.Clear();
            foreach (FacilityStat stat in info.Stats) stats.Add(CreateStatRow(stat));
            stats.EnableInClassList("hidden", info.Stats.Count == 0);

            orders.Clear();
            foreach (FacilityOrder order in info.Orders)
            {
                var button = new Button(() => state.IssueOrder(info.Id, order.Id, clock())) { text = order.Label };
                button.AddToClassList("order-button");
                orders.Add(button);
            }
            orders.EnableInClassList("hidden", info.Orders.Count == 0);
        }

        private void ShowGeneric(IInteractable target)
        {
            icon.Kind = HudIconKind.Info;
            nameLabel.text = target.DisplayName;
            SetText(subtitleLabel, null);
            SetText(descriptionLabel, target.Description);
            stats.Clear();
            stats.AddToClassList("hidden");
            orders.Clear();
            orders.AddToClassList("hidden");
        }

        private void OnFacilityChanged(string facilityId)
        {
            if (selected is CediFacility facility && facility.FacilityId == facilityId) Show(selected);
        }

        private static VisualElement CreateStatRow(FacilityStat stat)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");
            var label = new Label(stat.Label);
            label.AddToClassList("stat-label");
            var value = new Label(stat.Value);
            value.AddToClassList("stat-value");
            row.Add(label);
            row.Add(value);
            return row;
        }

        private static void SetText(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        private static HudIconKind IconFor(FacilityKind kind) => kind switch
        {
            FacilityKind.Warehouse => HudIconKind.Warehouse,
            FacilityKind.Gate => HudIconKind.Gate,
            FacilityKind.Office => HudIconKind.Office,
            FacilityKind.ContainerYard => HudIconKind.Container,
            _ => HudIconKind.Info
        };
    }
}
