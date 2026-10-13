using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// A SKU of a sector portfolio with its material master data (supplier, origin, mode, lead time,
    /// MOQ, pack size, cost) and its monthly sales history in units. <see cref="IsReal"/> marks the
    /// SKUs that come from the Atlas history; their history is never changed.
    /// </summary>
    public sealed class PortfolioSku
    {
        public PortfolioSku(string code, string description, string family, DemandPattern pattern, bool isReal,
            string supplier, SupplyOrigin origin, TransportMode mode, EntryPort port, int leadTimeDays,
            int unitsPerBox, int minimumOrderUnits, long unitCost, int[] history)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A SKU needs a code.", nameof(code));
            if (unitsPerBox <= 0) throw new ArgumentOutOfRangeException(nameof(unitsPerBox));
            Code = code;
            Description = description;
            Family = family;
            Pattern = pattern;
            IsReal = isReal;
            Supplier = supplier;
            Origin = origin;
            Mode = mode;
            Port = port;
            LeadTimeDays = leadTimeDays;
            UnitsPerBox = unitsPerBox;
            MinimumOrderUnits = minimumOrderUnits;
            UnitCost = unitCost;
            this.history = history ?? throw new ArgumentNullException(nameof(history));
        }

        private readonly int[] history;

        public string Code { get; }
        public string Description { get; }
        public string Family { get; }
        public DemandPattern Pattern { get; }
        public bool IsReal { get; }
        public string Supplier { get; }
        public SupplyOrigin Origin { get; }
        public TransportMode Mode { get; }
        public EntryPort Port { get; }

        /// <summary>Nominal lead time in days, from the purchase order to available stock.</summary>
        public int LeadTimeDays { get; }

        public int UnitsPerBox { get; }

        /// <summary>Minimum order quantity in units (a whole number of boxes).</summary>
        public int MinimumOrderUnits { get; }

        /// <summary>Cost per unit in COP.</summary>
        public long UnitCost { get; }

        /// <summary>Units sold per month (index 0 = January 2023).</summary>
        public IReadOnlyList<int> History => history;

        public bool IsImported => Origin != SupplyOrigin.Nacional;

        /// <summary>Total of the last 12 months.</summary>
        public int LastYearUnits => SeriesStatistics.LastMonthsTotal(history);
    }
}
