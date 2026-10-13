using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>The SKUs of a sector campaign (real Atlas SKUs plus generated ones), in code order.</summary>
    public sealed class Portfolio
    {
        private readonly Dictionary<string, PortfolioSku> byCode;

        public Portfolio(string sectorId, int seed, IEnumerable<PortfolioSku> skus)
        {
            SectorId = sectorId;
            Seed = seed;
            Skus = (skus ?? throw new ArgumentNullException(nameof(skus))).OrderBy(s => s.Code, StringComparer.Ordinal).ToList();
            byCode = Skus.ToDictionary(s => s.Code);
        }

        public string SectorId { get; }
        public int Seed { get; }
        public IReadOnlyList<PortfolioSku> Skus { get; }

        public PortfolioSku Find(string code) => code != null && byCode.TryGetValue(code, out PortfolioSku sku) ? sku : null;

        public IEnumerable<string> Families => Skus.Select(s => s.Family).Distinct();

        /// <summary>
        /// Share of the last 12 months' units sold by the top <paramref name="fraction"/> of SKUs
        /// (0.2 → the Pareto "20 % of SKUs sell ~80 %").
        /// </summary>
        public double TopShare(double fraction)
        {
            List<int> volumes = Skus.Select(s => s.LastYearUnits).OrderByDescending(v => v).ToList();
            double total = volumes.Sum(v => (double)v);
            int top = (int)Math.Round(volumes.Count * fraction);
            return total == 0 ? 0 : volumes.Take(top).Sum(v => (double)v) / total;
        }
    }
}
