using System.Linq;
using NovaMarket.Simulation.Campaign;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// Groups every role, sector and character asset of the game (Data/Campaign.asset). The scene
    /// references it and turns it into a plain C# <see cref="CampaignCatalog"/>.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Campaign Database", fileName = "Campaign")]
    public sealed class CampaignDatabase : NovaDataAsset
    {
        [SerializeField] private RoleData[] roles = new RoleData[0];
        [SerializeField] private SectorData[] sectors = new SectorData[0];
        [SerializeField] private CharacterData[] characters = new CharacterData[0];

        public RoleData[] Roles => roles;
        public SectorData[] Sectors => sectors;
        public CharacterData[] Characters => characters;

        public CharacterData FindCharacter(string id) => characters.FirstOrDefault(c => c != null && c.Id == id);

        public CampaignCatalog ToCatalog() => new CampaignCatalog(
            roles.Where(r => r != null).Select(r => r.ToDefinition()),
            sectors.Where(s => s != null).Select(s => s.ToDefinition()),
            characters.Where(c => c != null).Select(c => c.ToProfile()));
    }
}
