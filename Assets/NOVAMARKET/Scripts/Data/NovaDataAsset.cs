using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// Base class for every Supply Chain Run data asset (CompanyData, ScenarioData, WarehouseData,
    /// VehicleData, ProductData, LocationData, MissionData, EventData, RoleData...).
    /// Concrete types derive from this and add a [CreateAssetMenu] using <see cref="MenuRoot"/>.
    /// </summary>
    public abstract class NovaDataAsset : ScriptableObject
    {
        public const string MenuRoot = "NOVAMARKET/Data/";

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 6)] private string description;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
    }
}
