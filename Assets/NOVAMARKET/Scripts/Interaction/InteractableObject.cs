using NovaMarket.Data;
using UnityEngine;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Minimal default implementation of <see cref="IInteractable"/>.
    /// Name and description fall back to the assigned data asset, then to the GameObject name.
    /// Override the virtual methods to add behaviour.
    /// </summary>
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 6)] private string description;
        [SerializeField] private NovaDataAsset data;
        [SerializeField] private bool canInteract = true;

        public string DisplayName =>
            !string.IsNullOrEmpty(displayName) ? displayName :
            data != null ? data.DisplayName : gameObject.name;

        public string Description =>
            !string.IsNullOrEmpty(description) ? description :
            data != null ? data.Description : string.Empty;

        public NovaDataAsset Data => data;
        public bool CanInteract => canInteract;

        public virtual void OnSelected() { }
        public virtual void OnDeselected() { }
        public virtual void Interact() { }
    }
}
