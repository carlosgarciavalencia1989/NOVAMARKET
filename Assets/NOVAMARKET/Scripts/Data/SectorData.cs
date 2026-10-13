using NovaMarket.Simulation.Campaign;
using NovaMarket.Simulation.Portfolio;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>An industrial sector of the start menu: a campaign with its company and portfolio.</summary>
    [CreateAssetMenu(menuName = MenuRoot + "Sector", fileName = "Sector_")]
    public sealed class SectorData : NovaDataAsset
    {
        [SerializeField] private string company;
        [SerializeField, Min(1)] private int portfolioSize = 200;
        [SerializeField, TextArea(1, 3)] private string traits;
        [SerializeField] private bool isPlayable;
        [SerializeField, Tooltip("Real monthly history (CSV with ';'), the base of the generated portfolio.")]
        private TextAsset realHistory;
        [SerializeField, Tooltip("Seed of the generated SKUs: the same seed gives the same portfolio.")]
        private int portfolioSeed = 2026;

        public bool IsPlayable => isPlayable;
        public TextAsset RealHistory => realHistory;

        public SectorDefinition ToDefinition() =>
            new SectorDefinition(Id, DisplayName, company, portfolioSize, traits, isPlayable);

        /// <summary>The sector's portfolio: the real SKUs of <see cref="RealHistory"/> plus generated ones.</summary>
        public Portfolio CreatePortfolio() => PortfolioGenerator.Generate(Id,
            realHistory != null ? SalesHistoryCsv.Parse(realHistory.text) : new HistoryRecord[0], portfolioSize, portfolioSeed);
    }
}
