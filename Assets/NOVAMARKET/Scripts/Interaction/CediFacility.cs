using UnityEngine;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// A selectable CEDI facility (warehouse, gate, office, container yard). It only stores the
    /// facility id; the HUD looks up the data for that id in the operation state
    /// (see <c>NovaMarket.Simulation.CediFacilityIds</c>).
    /// </summary>
    public class CediFacility : InteractableObject
    {
        [Tooltip("Id of the facility data, e.g. \"warehouse\" (see CediFacilityIds).")]
        [SerializeField] private string facilityId;

        public string FacilityId => facilityId;
    }
}
