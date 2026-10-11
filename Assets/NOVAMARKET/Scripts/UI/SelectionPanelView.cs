using System;
using System.Collections.Generic;
using NovaMarket.Interaction;
using NovaMarket.Simulation;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Bottom-left panel for the selected object: icon, name, data rows, an optional progress
    /// bar and order buttons. Data comes as <see cref="SelectionDetails"/>: facilities read it
    /// from the <see cref="FacilityDirectory"/>, vehicles and other providers describe
    /// themselves, any other <see cref="IInteractable"/> shows its name and description.
    /// <see cref="Refresh"/> runs every frame and only rebuilds rows when their layout changes.
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
        private readonly VisualElement progress;
        private readonly Label progressLabel;
        private readonly VisualElement progressFill;
        private readonly VisualElement orders;
        private readonly List<Label> statValues = new List<Label>();
        private Func<SelectionDetails> source;
        private string ordersKey;

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
            progress = root.Q("selectionProgress");
            progressLabel = root.Q<Label>("selectionProgressLabel");
            progressFill = root.Q("selectionProgressFill");
            orders = root.Q("selectionOrders");
            Show(null);
        }

        /// <summary>Shows the panel for a new selection (or hides it for null).</summary>
        public void Show(IInteractable target)
        {
            source = SourceFor(target);
            ordersKey = null;
            stats.Clear();
            statValues.Clear();
            Refresh();
        }

        /// <summary>Updates the panel with fresh data; call once per frame.</summary>
        public void Refresh()
        {
            SelectionDetails details = source?.Invoke();
            panel.EnableInClassList("hidden", details == null);
            if (details != null) Apply(details);
        }

        private Func<SelectionDetails> SourceFor(IInteractable target)
        {
            switch (target)
            {
                case null:
                    return null;
                case ISelectionDetailsProvider provider:
                    return provider.GetDetails;
                case CediFacility facility:
                    FacilityInfo cachedInfo = null;
                    SelectionDetails cachedDetails = null;
                    return () =>
                    {
                        if (!state.Facilities.TryGet(facility.FacilityId, out FacilityInfo info)) return null;
                        if (info != cachedInfo)
                        {
                            cachedInfo = info;
                            cachedDetails = info.ToDetails();
                        }
                        return cachedDetails;
                    };
                default:
                    var generic = new SelectionDetails(target.DisplayName, target.Description, SelectionIcon.Info,
                        new FacilityStat[0]);
                    return () => generic;
            }
        }

        private void Apply(SelectionDetails details)
        {
            HudIconKind kind = IconFor(details.Icon);
            if (icon.Kind != kind) icon.Kind = kind;
            nameLabel.text = details.Title;
            SetText(subtitleLabel, details.Subtitle);
            SetText(descriptionLabel, null); // generic objects show their description as the subtitle

            ApplyStats(details.Stats);
            ApplyProgress(details);
            ApplyOrders(details);
        }

        private void ApplyStats(IReadOnlyList<FacilityStat> rows)
        {
            stats.EnableInClassList("hidden", rows.Count == 0);
            if (rows.Count != statValues.Count) RebuildStats(rows);
            for (int i = 0; i < rows.Count; i++)
                if (statValues[i].text != rows[i].Value) statValues[i].text = rows[i].Value;
        }

        private void RebuildStats(IReadOnlyList<FacilityStat> rows)
        {
            stats.Clear();
            statValues.Clear();
            foreach (FacilityStat stat in rows)
            {
                var row = new VisualElement();
                row.AddToClassList("stat-row");
                var label = new Label(stat.Label);
                label.AddToClassList("stat-label");
                var value = new Label(stat.Value);
                value.AddToClassList("stat-value");
                row.Add(label);
                row.Add(value);
                stats.Add(row);
                statValues.Add(value);
            }
        }

        private void ApplyProgress(SelectionDetails details)
        {
            bool visible = details.Progress.HasValue;
            progress.EnableInClassList("hidden", !visible);
            if (!visible) return;

            float percent = (float)Math.Max(0.0, Math.Min(1.0, details.Progress.Value)) * 100f;
            progressFill.style.width = Length.Percent(percent); // dynamic value: USS cannot express it
            progressFill.EnableInClassList("progress-fill--done", percent >= 100f);
            progressLabel.text = details.ProgressLabel ?? string.Empty;
        }

        private void ApplyOrders(SelectionDetails details)
        {
            string key = details.OrderTargetId + ":" + string.Join(",", OrderIds(details.Orders));
            orders.EnableInClassList("hidden", details.Orders.Count == 0);
            if (key == ordersKey) return;
            ordersKey = key;

            orders.Clear();
            foreach (FacilityOrder order in details.Orders)
            {
                string targetId = details.OrderTargetId;
                var button = new Button(() => state.IssueOrder(targetId, order.Id, clock())) { text = order.Label };
                button.AddToClassList("order-button");
                orders.Add(button);
            }
        }

        private static IEnumerable<string> OrderIds(IReadOnlyList<FacilityOrder> list)
        {
            foreach (FacilityOrder order in list) yield return order.Id;
        }

        private static void SetText(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        private static HudIconKind IconFor(SelectionIcon selectionIcon) => selectionIcon switch
        {
            SelectionIcon.Warehouse => HudIconKind.Warehouse,
            SelectionIcon.Gate => HudIconKind.Gate,
            SelectionIcon.Office => HudIconKind.Office,
            SelectionIcon.ContainerYard => HudIconKind.Container,
            SelectionIcon.Truck => HudIconKind.Truck,
            SelectionIcon.Forklift => HudIconKind.Forklift,
            _ => HudIconKind.Info
        };
    }
}
