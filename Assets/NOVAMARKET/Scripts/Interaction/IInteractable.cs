using NovaMarket.Data;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Contract for any scene object the player can select and interact with.
    /// </summary>
    public interface IInteractable
    {
        string DisplayName { get; }
        string Description { get; }

        /// <summary>Data asset backing this object; may be null.</summary>
        NovaDataAsset Data { get; }

        bool CanInteract { get; }

        void OnSelected();
        void OnDeselected();
        void Interact();
    }
}
