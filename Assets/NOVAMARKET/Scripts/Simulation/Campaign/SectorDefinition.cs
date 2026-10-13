using System;

namespace NovaMarket.Simulation.Campaign
{
    /// <summary>
    /// An industrial sector: a campaign with its own company, portfolio size and the traits it
    /// teaches. Every role is played inside a sector; only the data changes between sectors.
    /// </summary>
    public sealed class SectorDefinition
    {
        public SectorDefinition(string id, string name, string company, int portfolioSize, string traits, bool isPlayable)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A sector needs an id.", nameof(id));
            if (portfolioSize <= 0) throw new ArgumentOutOfRangeException(nameof(portfolioSize));
            Id = id;
            Name = name;
            Company = company ?? string.Empty;
            PortfolioSize = portfolioSize;
            Traits = traits ?? string.Empty;
            IsPlayable = isPlayable;
        }

        public string Id { get; }
        public string Name { get; }
        public string Company { get; }

        /// <summary>Number of SKUs of the sector's portfolio in the game.</summary>
        public int PortfolioSize { get; }

        public string Traits { get; }
        public bool IsPlayable { get; }
    }
}
