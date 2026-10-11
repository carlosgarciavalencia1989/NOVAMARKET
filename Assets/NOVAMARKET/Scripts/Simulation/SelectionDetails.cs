using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>Icon the selection panel shows for a selected object.</summary>
    public enum SelectionIcon
    {
        Info,
        Warehouse,
        Gate,
        Office,
        ContainerYard,
        Truck,
        Forklift
    }

    /// <summary>
    /// Everything the selection panel shows: title, data rows, optional progress bar and
    /// order buttons. Facilities and vehicles both describe themselves with this.
    /// </summary>
    public sealed class SelectionDetails
    {
        private static readonly FacilityOrder[] NoOrders = new FacilityOrder[0];

        public SelectionDetails(string title, string subtitle, SelectionIcon icon, IReadOnlyList<FacilityStat> stats,
            IReadOnlyList<FacilityOrder> orders = null, string orderTargetId = null,
            double? progress = null, string progressLabel = null)
        {
            Title = title;
            Subtitle = subtitle;
            Icon = icon;
            Stats = stats;
            Orders = orders ?? NoOrders;
            OrderTargetId = orderTargetId;
            Progress = progress;
            ProgressLabel = progressLabel;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public SelectionIcon Icon { get; }
        public IReadOnlyList<FacilityStat> Stats { get; }
        public IReadOnlyList<FacilityOrder> Orders { get; }

        /// <summary>Facility id the order buttons are sent to; null when there are no orders.</summary>
        public string OrderTargetId { get; }

        /// <summary>0..1 for the progress bar (e.g. unloading); null hides the bar.</summary>
        public double? Progress { get; }
        public string ProgressLabel { get; }
    }
}
