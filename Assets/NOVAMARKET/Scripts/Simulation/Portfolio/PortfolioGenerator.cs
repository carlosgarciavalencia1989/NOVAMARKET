using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// Builds a sector portfolio from its real history: the real SKUs keep their history untouched and
    /// get master data from their family; the rest are synthetic SKUs with the family's seasonal shape,
    /// a log-normal volume (few A SKUs with most of the units, a long C tail) and one of six demand
    /// patterns. Same seed, same portfolio.
    /// </summary>
    public static class PortfolioGenerator
    {
        /// <summary>Spread (σ of ln units) of the volumes inside a family; draws are clipped to ±2σ.</summary>
        public const double VolumeSpread = 1.4;

        /// <summary>Smallest monthly level of a non-intermittent synthetic SKU.</summary>
        public const double MinimumLevel = 8;

        public static Portfolio Generate(string sectorId, IReadOnlyList<HistoryRecord> real, int size, int seed)
        {
            IReadOnlyList<FamilyProfile> families = SectorProfiles.For(sectorId)
                ?? throw new ArgumentException($"No portfolio profile for sector '{sectorId}'.", nameof(sectorId));
            if (real == null) throw new ArgumentNullException(nameof(real));
            if (size < real.Count) throw new ArgumentOutOfRangeException(nameof(size), "Smaller than the real SKUs.");

            var random = new Random(seed);
            var skus = new List<PortfolioSku>();
            var names = new HashSet<string>(real.Select(r => r.Description));
            var codes = new HashSet<string>(real.Select(r => r.Code));

            foreach (HistoryRecord record in real)
            {
                FamilyProfile family = families.FirstOrDefault(f => f.Name == record.Family)
                    ?? throw new ArgumentException($"SKU {record.Code} has unknown family '{record.Family}'.");
                skus.Add(Build(random, record.Code, record.Description, family, SeriesStatistics.Classify(record.Units),
                    true, (int[])record.Units.Clone(), family.Suppliers[0]));
            }

            int[] counts = Allocate(families, size - real.Count);
            int nextNumber = real.Select(r => CodeNumber(r.Code)).DefaultIfEmpty(10000).Max() + 1;
            for (int f = 0; f < families.Count; f++)
            {
                FamilyProfile family = families[f];
                double[] shape = Shape(real, family);
                for (int i = 0; i < counts[f]; i++)
                {
                    string code;
                    do code = $"CV{nextNumber++}"; while (!codes.Add(code));
                    DemandPattern pattern = Pick(random, family.PatternWeights);
                    double level = Math.Max(MinimumLevel, family.MedianUnits * Math.Exp(VolumeSpread * Math.Max(-2, Math.Min(2, Normal(random)))));
                    int[] history = Series(random, pattern, level, shape);
                    string supplier = family.Suppliers[random.Next(family.Suppliers.Length)];
                    skus.Add(Build(random, code, UniqueName(random, family, names), family, pattern, false, history, supplier));
                }
            }
            return new Portfolio(sectorId, seed, skus);
        }

        private static PortfolioSku Build(Random random, string code, string description, FamilyProfile family,
            DemandPattern pattern, bool isReal, int[] history, string supplier)
        {
            TransportMode mode = family.Modes[random.Next(family.Modes.Length)];
            EntryPort port = family.Origin == SupplyOrigin.Nacional ? EntryPort.Ninguno
                : LeadTimeTable.IsSea(mode) ? (random.NextDouble() < 0.8 ? EntryPort.Buenaventura : EntryPort.Cartagena)
                : EntryPort.Rionegro;
            int leadTime = LeadTimeTable.DrawDays(random, family.Origin, mode, port, family.ProductionDays);
            int moq = random.Next(family.MoqBoxes.Min, family.MoqBoxes.Max + 1) * family.UnitsPerBox;
            long cost = (long)Math.Round((family.UnitCost.Min + random.NextDouble() * (family.UnitCost.Max - family.UnitCost.Min)) / 100) * 100;
            return new PortfolioSku(code, description, family.Name, pattern, isReal, supplier, family.Origin, mode, port,
                leadTime, family.UnitsPerBox, moq, cost, history);
        }

        /// <summary>Monthly units of a synthetic SKU.</summary>
        private static int[] Series(Random random, DemandPattern pattern, double level, double[] shape)
        {
            int n = SalesCalendar.Months;
            var series = new int[n];
            (double strength, double cv) = pattern switch
            {
                DemandPattern.Stable => (0.3, 0.08 + 0.1 * random.NextDouble()),
                DemandPattern.Seasonal => (1.0 + 0.3 * random.NextDouble(), 0.1 + 0.1 * random.NextDouble()),
                DemandPattern.Erratic => (0.5, 0.55 + 0.3 * random.NextDouble()),
                DemandPattern.Intermittent => (0.0, 0.4),
                _ => (0.6, 0.15)
            };
            double growth = -0.008 + 0.02 * random.NextDouble();
            int launch = pattern == DemandPattern.Launch ? random.Next(6, 39) : 0;
            int end = pattern == DemandPattern.Discontinued ? random.Next(20, 39) : n;
            double chance = pattern == DemandPattern.Intermittent ? 0.25 + 0.35 * random.NextDouble() : 1;

            for (int t = 0; t < n; t++)
            {
                if (t < launch || t >= end) continue;
                double value = level * Math.Pow(shape[SalesCalendar.CalendarMonth(t)], strength) * Math.Pow(1 + growth, t - n / 2.0);
                if (pattern == DemandPattern.Launch && t - launch < 3) value *= new[] { 0.4, 0.7, 0.9 }[t - launch];
                if (pattern == DemandPattern.Discontinued && end - t <= 6) value *= 0.3 + 0.7 * (end - t - 1) / 6.0;
                if (pattern == DemandPattern.Intermittent)
                {
                    if (random.NextDouble() > chance) continue;
                    value /= chance;
                }
                value *= Math.Max(0.05, 1 + cv * Normal(random));
                series[t] = Math.Max(1, (int)Math.Round(value)); // an active month never shows a false zero
            }
            return series;
        }

        /// <summary>Seasonal indices of the family's template SKU (flat if it is not in the history).</summary>
        private static double[] Shape(IReadOnlyList<HistoryRecord> real, FamilyProfile family)
        {
            HistoryRecord template = real.FirstOrDefault(r => r.Code == family.TemplateCode);
            return template != null ? SeriesStatistics.SeasonalIndices(template.Units) : Enumerable.Repeat(1.0, 12).ToArray();
        }

        /// <summary>Splits <paramref name="total"/> among the families in proportion to their counts (largest remainder).</summary>
        private static int[] Allocate(IReadOnlyList<FamilyProfile> families, int total)
        {
            double sum = families.Sum(f => f.SyntheticCount);
            var counts = new int[families.Count];
            var remainders = new double[families.Count];
            for (int i = 0; i < families.Count; i++)
            {
                double exact = total * families[i].SyntheticCount / sum;
                counts[i] = (int)Math.Floor(exact);
                remainders[i] = exact - counts[i];
            }
            foreach (int i in Enumerable.Range(0, families.Count).OrderByDescending(i => remainders[i]).Take(total - counts.Sum()))
                counts[i]++;
            return counts;
        }

        private static string UniqueName(Random random, FamilyProfile family, HashSet<string> names)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                object[] parts = family.NameParts.Select(p => (object)p[random.Next(p.Length)]).ToArray();
                string name = string.Format(CultureInfo.InvariantCulture, family.NameFormat, parts);
                if (names.Add(name)) return name;
            }
            for (int edition = 2; ; edition++)
            {
                string name = $"{family.Name} Ed. {edition}";
                if (names.Add(name)) return name;
            }
        }

        private static DemandPattern Pick(Random random, double[] weights)
        {
            double roll = random.NextDouble() * weights.Sum();
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return (DemandPattern)i;
            }
            return DemandPattern.Stable;
        }

        private static int CodeNumber(string code) =>
            int.TryParse(new string(code.Where(char.IsDigit).ToArray()), out int number) ? number : 0;

        private static double Normal(Random random)
        {
            double u1 = 1.0 - random.NextDouble(), u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
