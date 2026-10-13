using NovaMarket.Simulation.Campaign;
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

        public bool IsPlayable => isPlayable;

        public SectorDefinition ToDefinition() =>
            new SectorDefinition(Id, DisplayName, company, portfolioSize, traits, isPlayable);
    }
}
