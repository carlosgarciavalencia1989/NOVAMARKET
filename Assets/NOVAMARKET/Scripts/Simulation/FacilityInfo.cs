using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>Type of CEDI facility; the HUD chooses the selection icon from it.</summary>
    public enum FacilityKind
    {
        Warehouse,
        Gate,
        Office,
        ContainerYard
    }

    /// <summary>One line of data in the selection panel ("Posiciones ocupadas: 109/200").</summary>
    public sealed class FacilityStat
    {
        public FacilityStat(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }

    /// <summary>An order button of the selection panel. <see cref="Id"/> is what the simulation receives.</summary>
    public sealed class FacilityOrder
    {
        public FacilityOrder(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    /// <summary>
    /// What the selection panel shows for a facility. Scene objects only store the
    /// <see cref="Id"/>; the data lives here so the simulation can update it.
    /// </summary>
    public sealed class FacilityInfo
    {
        private readonly List<FacilityStat> stats;
        private readonly List<FacilityOrder> orders;

        public FacilityInfo(string id, string name, string subtitle, FacilityKind kind,
            IEnumerable<FacilityStat> stats, IEnumerable<FacilityOrder> orders)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
            Kind = kind;
            this.stats = new List<FacilityStat>(stats);
            this.orders = new List<FacilityOrder>(orders);
        }

        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public FacilityKind Kind { get; }
        public IReadOnlyList<FacilityStat> Stats => stats;
        public IReadOnlyList<FacilityOrder> Orders => orders;

        public FacilityOrder FindOrder(string orderId) => orders.Find(o => o.Id == orderId);

        public SelectionDetails ToDetails() =>
            new SelectionDetails(Name, Subtitle, IconFor(Kind), stats, orders, Id);

        private static SelectionIcon IconFor(FacilityKind kind) => kind switch
        {
            FacilityKind.Warehouse => SelectionIcon.Warehouse,
            FacilityKind.Gate => SelectionIcon.Gate,
            FacilityKind.Office => SelectionIcon.Office,
            FacilityKind.ContainerYard => SelectionIcon.ContainerYard,
            _ => SelectionIcon.Info
        };
    }

    /// <summary>All facilities of the scenario, by id. Raises <see cref="Changed"/> when one is replaced.</summary>
    public sealed class FacilityDirectory
    {
        private readonly Dictionary<string, FacilityInfo> facilities = new Dictionary<string, FacilityInfo>();

        /// <summary>Raised with the id of the facility whose data changed.</summary>
        public event Action<string> Changed;

        public int Count => facilities.Count;

        /// <summary>Adds or replaces a facility's data.</summary>
        public void Set(FacilityInfo info)
        {
            facilities[info.Id] = info;
            Changed?.Invoke(info.Id);
        }

        public bool TryGet(string id, out FacilityInfo info)
        {
            if (id != null) return facilities.TryGetValue(id, out info);
            info = null;
            return false;
        }
    }
}
