using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Selects <see cref="IInteractable"/> objects with a left click (raycast from the camera).
    /// Clicking empty ground or pressing Escape clears the selection. Clicks on the HUD are
    /// ignored through <see cref="IsPointerBlocked"/>, which the HUD sets.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private SelectionRing ring;
        [SerializeField] private LayerMask selectableLayers = ~0;
        [SerializeField] private float maxRayDistance = 5000f;

        private InputActionMap actions;
        private InputAction select;
        private InputAction cancel;
        private InputAction pointer;

        public event Action<IInteractable> SelectionChanged;

        public IInteractable Selected { get; private set; }

        /// <summary>Returns true when the screen point is over UI and must not select the world.</summary>
        public Func<Vector2, bool> IsPointerBlocked { get; set; }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            actions = new InputActionMap("Selection");
            select = actions.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            cancel = actions.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            pointer = actions.AddAction("Pointer", InputActionType.Value, "<Pointer>/position");
            pointer.expectedControlType = "Vector2";
        }

        private void OnEnable() => actions.Enable();
        private void OnDisable() => actions.Disable();
        private void OnDestroy() => actions.Dispose();

        private void Update()
        {
            if (cancel.WasPressedThisFrame()) Select(null);
            if (!select.WasPressedThisFrame()) return;

            Vector2 screenPoint = pointer.ReadValue<Vector2>();
            if (IsPointerBlocked != null && IsPointerBlocked(screenPoint)) return;
            Select(Pick(screenPoint));
        }

        /// <summary>Selects an object (or clears the selection with null).</summary>
        public void Select(IInteractable target)
        {
            if (ReferenceEquals(target, Selected)) return;

            Selected?.OnDeselected();
            Selected = target;
            Selected?.OnSelected();

            if (ring != null)
            {
                if (Selected is Component component) ring.Show(component.transform);
                else ring.Hide();
            }
            SelectionChanged?.Invoke(Selected);
        }

        private IInteractable Pick(Vector2 screenPoint)
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, selectableLayers, QueryTriggerInteraction.Ignore))
                return null;
            return FindInteractable(hit.collider);
        }

        private static IInteractable FindInteractable(Collider collider)
        {
            var link = collider.GetComponentInParent<InteractableLink>();
            IInteractable target = link != null && link.Target != null
                ? link.Target
                : collider.GetComponentInParent<IInteractable>();
            return target != null && target.CanInteract ? target : null;
        }
    }
}
