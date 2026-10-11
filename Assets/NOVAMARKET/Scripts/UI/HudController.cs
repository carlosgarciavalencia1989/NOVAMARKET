using NovaMarket.CameraControl;
using NovaMarket.Interaction;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Age of Empires III–style HUD (UI Toolkit, layout in Hud.uxml / Hud.uss). Connects the
    /// panels to the <see cref="HudDataSource"/> state, the selection system and the camera;
    /// it holds no game data itself.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudController : MonoBehaviour
    {
        [SerializeField] private HudDataSource dataSource;
        [SerializeField] private SelectionController selection;
        [SerializeField] private StrategyCameraController cameraController;
        [SerializeField] private MinimapRenderer minimap;
        [Tooltip("World position of the CEDI marker on the minimap.")]
        [SerializeField] private Vector3 cediPosition = Vector3.zero;

        private VisualElement root;
        private ObjectivesView objectives;
        private ResourceBarView resources;
        private NotificationsView notifications;
        private SelectionPanelView selectionPanel;
        private MinimapView minimapView;

        // Built in Start, after every OnEnable, so the UIDocument has already created its visual tree.
        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            objectives = new ObjectivesView(root, dataSource.State.Mission);
            resources = new ResourceBarView(root, dataSource.State.Resources);
            notifications = new NotificationsView(root, dataSource.State.Notifications);
            selectionPanel = new SelectionPanelView(root, dataSource.State, () => dataSource.Now);
            minimapView = new MinimapView(root, minimap, cameraController, cediPosition);

            selection.IsPointerBlocked = IsPointerOverHud;
            selection.SelectionChanged += selectionPanel.Show;
        }

        private void LateUpdate() => minimapView?.Update();

        private void OnDestroy()
        {
            if (selectionPanel == null) return;
            selection.SelectionChanged -= selectionPanel.Show;
            selection.IsPointerBlocked = null;
            objectives.Dispose();
            resources.Dispose();
            notifications.Dispose();
            selectionPanel.Dispose();
        }

        /// <summary>True when a screen point (Input System coordinates, origin bottom-left) is over a HUD panel.</summary>
        public bool IsPointerOverHud(Vector2 screenPoint)
        {
            IPanel panel = root?.panel;
            if (panel == null) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            return panel.Pick(panelPoint) != null;
        }
    }
}
