using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// How the SKUs of a product family are generated: count, supply data, volume, pattern mix and
    /// the words their names are built from. The real SKU named in <see cref="TemplateCode"/> gives
    /// the family's seasonal shape.
    /// </summary>
    public sealed class FamilyProfile
    {
        public string Name { get; set; }

        /// <summary>Real Atlas SKU whose seasonal indices the family follows.</summary>
        public string TemplateCode { get; set; }

        /// <summary>Synthetic SKUs to add to the real ones of the family.</summary>
        public int SyntheticCount { get; set; }

        public SupplyOrigin Origin { get; set; }

        /// <summary>Default modes; one is chosen per SKU.</summary>
        public TransportMode[] Modes { get; set; } = { TransportMode.Carretera };

        /// <summary>Suppliers; real SKUs take the first one.</summary>
        public string[] Suppliers { get; set; } = Array.Empty<string>();

        /// <summary>Production days until the cargo is ready (minimum, maximum).</summary>
        public (int Min, int Max) ProductionDays { get; set; } = (7, 21);

        public int UnitsPerBox { get; set; } = 12;

        /// <summary>MOQ in boxes (minimum, maximum).</summary>
        public (int Min, int Max) MoqBoxes { get; set; } = (1, 4);

        /// <summary>Cost per unit in COP (minimum, maximum).</summary>
        public (long Min, long Max) UnitCost { get; set; } = (5_000, 10_000);

        /// <summary>Median monthly units of a SKU of the family; the volume spread is log-normal around it.</summary>
        public double MedianUnits { get; set; } = 500;

        /// <summary>Relative weights of each <see cref="DemandPattern"/> (same order as the enum).</summary>
        public double[] PatternWeights { get; set; } = { 1, 1, 1, 1, 1, 1 };

        /// <summary>Name pattern with {0}…{n} filled from <see cref="NameParts"/>, one list per slot.</summary>
        public string NameFormat { get; set; } = "{0}";

        public IReadOnlyList<string[]> NameParts { get; set; } = Array.Empty<string[]>();
    }
}
