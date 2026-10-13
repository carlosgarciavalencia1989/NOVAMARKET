using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaMarket.Simulation.Portfolio;

namespace NovaMarket.Tests.Simulation.Portfolio
{
    /// <summary>The real Atlas history file, read once for the tests.</summary>
    public static class PortfolioTestData
    {
        public const string HistoryPath = "Assets/NOVAMARKET/Data/Sectors/Cosmetica/Historia_Atlas_Cosmetica.csv";

        private static IReadOnlyList<HistoryRecord> real;

        public static IReadOnlyList<HistoryRecord> Real => real ??= SalesHistoryCsv.Parse(File.ReadAllText(HistoryPath));

        public static HistoryRecord Record(string code) => Real.First(r => r.Code == code);

        public static NovaMarket.Simulation.Portfolio.Portfolio Generate(int seed) =>
            PortfolioGenerator.Generate(SectorProfiles.CosmeticsId, Real, 200, seed);
    }
}
