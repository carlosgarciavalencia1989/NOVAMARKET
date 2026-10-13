using NovaMarket.Simulation.Campaign;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>A role of the start menu: one link of the Atlas supply chain map.</summary>
    [CreateAssetMenu(menuName = MenuRoot + "Role", fileName = "Role_")]
    public sealed class RoleData : NovaDataAsset
    {
        [SerializeField] private ChainLane lane;
        [SerializeField, TextArea(1, 3)] private string covers;
        [SerializeField] private bool isPlayable;
        [SerializeField, Tooltip("World names in play order.")] private string[] worlds = new string[0];

        public ChainLane Lane => lane;
        public bool IsPlayable => isPlayable;

        public RoleDefinition ToDefinition() => new RoleDefinition(Id, DisplayName, lane, covers, isPlayable, worlds);
    }
}
