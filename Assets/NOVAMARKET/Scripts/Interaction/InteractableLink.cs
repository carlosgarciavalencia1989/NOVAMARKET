using UnityEngine;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Forwards clicks on this object (and its children) to another interactable. Used where the
    /// hierarchy splits one facility into groups, e.g. the warehouse zones select the warehouse.
    /// </summary>
    public class InteractableLink : MonoBehaviour
    {
        [SerializeField] private InteractableObject target;

        public InteractableObject Target => target;
    }
}
