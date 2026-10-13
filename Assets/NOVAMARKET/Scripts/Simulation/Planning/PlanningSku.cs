using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// A product the player plans in the season: costs, supplier terms and monthly sales history.
    /// Quantities in the planning level are boxes; the history is in units.
    /// </summary>
    public sealed class PlanningSku
    {
        public PlanningSku(string id, string code, string name, int unitsPerBox, long costPerBox, int leadTimeWeeks,
            double variationCoefficient, int minimumOrder, long orderCost, string supplier, string origin,
            int boxesPerPallet, IReadOnlyList<int> monthlyHistory)
        {
            if (unitsPerBox < 1) throw new ArgumentOutOfRangeException(nameof(unitsPerBox));
            if (leadTimeWeeks < 1) throw new ArgumentOutOfRangeException(nameof(leadTimeWeeks));
            if (boxesPerPallet < 1) throw new ArgumentOutOfRangeException(nameof(boxesPerPallet));
            Id = id;
            Code = code;
            Name = name;
            UnitsPerBox = unitsPerBox;
            CostPerBox = costPerBox;
            LeadTimeWeeks = leadTimeWeeks;
            VariationCoefficient = variationCoefficient;
            MinimumOrder = minimumOrder;
            OrderCost = orderCost;
            Supplier = supplier;
            Origin = origin;
            BoxesPerPallet = boxesPerPallet;
            MonthlyHistory = monthlyHistory ?? throw new ArgumentNullException(nameof(monthlyHistory));
        }

        /// <summary>Short id used in code and tests ("lab", "per", "set", "cre").</summary>
        public string Id { get; }

        /// <summary>Catalogue code shown to the player ("CV10009").</summary>
        public string Code { get; }
        public string Name { get; }
        public int UnitsPerBox { get; }

        /// <summary>Cost of one box in Colombian pesos.</summary>
        public long CostPerBox { get; }

        /// <summary>Weeks between placing an order and receiving it (L).</summary>
        public int LeadTimeWeeks { get; }

        /// <summary>Weekly coefficient of variation of the demand (σ / mean).</summary>
        public double VariationCoefficient { get; }

        /// <summary>Minimum order quantity in boxes; 0 = no minimum.</summary>
        public int MinimumOrder { get; }

        /// <summary>Fixed cost of placing one order, in pesos.</summary>
        public long OrderCost { get; }

        public string Supplier { get; }

        /// <summary>Where the receiving trucks of this SKU come from.</summary>
        public string Origin { get; }

        /// <summary>Boxes on one pallet (one rack position); only used to draw the operation.</summary>
        public int BoxesPerPallet { get; }

        /// <summary>Units sold per month from January 2023 (index 0) onwards.</summary>
        public IReadOnlyList<int> MonthlyHistory { get; }

        /// <summary>Rack positions needed for a number of boxes.</summary>
        public int PalletsFor(int boxes) => boxes <= 0 ? 0 : (boxes + BoxesPerPallet - 1) / BoxesPerPallet;

        public override string ToString() => $"{Code} {Name}";
    }
}
