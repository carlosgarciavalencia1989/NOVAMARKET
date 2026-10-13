using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaMarket.CameraControl
{
    /// <summary>
    /// Age of Empires–style strategy camera for Play mode. Reads input through the Input System
    /// and drives a <see cref="StrategyCameraRig"/>, which holds all the camera math.
    /// Controls: WASD / arrows and screen edges to move, middle mouse drag to grab the ground,
    /// mouse wheel to zoom, Q / E to rotate in steps.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StrategyCameraController : MonoBehaviour
    {
        [SerializeField] private StrategyCameraSettings settings = new StrategyCameraSettings();
        [Tooltip("Ground point the camera looks at when Play starts (the CEDI).")]
        [SerializeField] private Vector3 startFocus = new Vector3(15f, 0f, 0f);
        [SerializeField] private float startYaw = 30f;
        [SerializeField] private bool edgeScrolling = true;
        [Tooltip("Objects whose name contains this are buildings the camera stays above (generated city chunks).")]
        [SerializeField] private string buildingNameTag = "_Buildings_";

        private Camera targetCamera;
        private StrategyCameraRig rig;
        private InputActionMap actions;
        private InputAction move;
        private InputAction zoom;
        private InputAction rotateLeft;
        private InputAction rotateRight;
        private InputAction dragButton;
        private InputAction pointer;
        private bool dragging;
        private bool dragHeld;
        private Vector3 dragAnchor;
        private float pendingZoomSteps;
        private int pendingRotationSteps;
        private readonly List<MeshCollider> buildings = new List<MeshCollider>();
        private float buildingLift;

        public StrategyCameraRig Rig => rig;
        public Camera Camera => targetCamera;

        /// <summary>
        /// True while a full-screen menu or a window takes the keyboard and mouse wheel (typing
        /// "WASD" in the planner sheet must not move the camera). Pending inputs are dropped.
        /// </summary>
        public bool InputSuspended { get; set; }

        /// <summary>Makes the camera travel to a ground point, e.g. after a minimap click.</summary>
        public void FocusOn(Vector3 worldPoint) => rig.FocusOn(worldPoint);

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            targetCamera.fieldOfView = settings.FieldOfView;
            rig = CreateStartRig();
            CreateActions();
            CollectBuildings();
            ApplyPose();
        }

        private void OnEnable() => actions?.Enable();
        private void OnDisable() => actions?.Disable();
        private void OnDestroy() => actions?.Dispose();

        private void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            Vector2 pointerPosition = pointer.ReadValue<Vector2>();
            if (InputSuspended)
            {
                pendingZoomSteps = 0f;
                pendingRotationSteps = 0;
                dragging = false;
                rig.Tick(deltaTime);
                ApplyPose();
                return;
            }

            Vector2 pan = move.ReadValue<Vector2>();
            if (edgeScrolling && !dragging && IsPointerOverGame())
                pan += StrategyCameraRig.EdgeScroll(pointerPosition, new Vector2(Screen.width, Screen.height),
                    settings.EdgeScrollPixels);
            rig.Pan(pan, deltaTime);

            if (pendingZoomSteps != 0f) rig.Zoom(pendingZoomSteps);
            if (pendingRotationSteps != 0) rig.Rotate(pendingRotationSteps);
            pendingZoomSteps = 0f;
            pendingRotationSteps = 0;

            UpdateDrag(pointerPosition);
            rig.Tick(deltaTime);
            ApplyPose();
        }

        /// <summary>Moves the camera to the start view; also usable in Edit mode to preview it.</summary>
        [ContextMenu("Apply Start View")]
        private void ApplyStartView()
        {
            targetCamera = GetComponent<Camera>();
            targetCamera.fieldOfView = settings.FieldOfView;
            rig = CreateStartRig();
            ApplyPose();
        }

        private StrategyCameraRig CreateStartRig() =>
            new StrategyCameraRig(settings, startFocus, startYaw, settings.StartDistance);

        private void CreateActions()
        {
            actions = new InputActionMap("StrategyCamera");

            move = actions.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            zoom = actions.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            zoom.expectedControlType = "Axis";
            rotateLeft = actions.AddAction("RotateLeft", InputActionType.Button, "<Keyboard>/q");
            rotateRight = actions.AddAction("RotateRight", InputActionType.Button, "<Keyboard>/e");
            dragButton = actions.AddAction("Drag", InputActionType.Button, "<Mouse>/middleButton");
            pointer = actions.AddAction("Pointer", InputActionType.Value, "<Pointer>/position");
            pointer.expectedControlType = "Vector2";

            // Discrete inputs are counted in callbacks so no press or wheel notch is lost,
            // even when several arrive between two frames.
            zoom.performed += context => pendingZoomSteps += Mathf.Sign(context.ReadValue<float>());
            rotateLeft.performed += _ => pendingRotationSteps--;
            rotateRight.performed += _ => pendingRotationSteps++;

            actions.Enable();
        }

        /// <summary>Grab-the-ground drag: the point under the cursor stays under the cursor.</summary>
        private void UpdateDrag(Vector2 pointerPosition)
        {
            bool held = dragButton.IsPressed();
            if (held && !dragHeld)
                dragging = TryGetGroundPoint(pointerPosition, out dragAnchor);
            else if (!held)
                dragging = false;
            dragHeld = held;

            if (dragging && TryGetGroundPoint(pointerPosition, out Vector3 current))
                rig.Drag(dragAnchor - current);
        }

        /// <summary>
        /// Edge scrolling only makes sense while the cursor is over the game. In the Editor the
        /// Input System keeps reporting a stale position (often a corner) when the mouse is over
        /// another window, which would make the camera drift on its own.
        /// </summary>
        private static bool IsPointerOverGame()
        {
            if (!Application.isFocused) return false;
#if UNITY_EDITOR
            UnityEditor.EditorWindow hovered = UnityEditor.EditorWindow.mouseOverWindow;
            return hovered != null && hovered.GetType().Name == "GameView";
#else
            return true;
#endif
        }

        private bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        private void ApplyPose()
        {
            Vector3 position = rig.Position;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
                position.y = Mathf.Max(position.y, ground + settings.GroundClearance);
            }

            // Over a tower the camera rises above its roof instead of going through it: up at once
            // (the roofs are measured a bit around the camera, so it starts rising before), down smoothly.
            float needed = Mathf.Max(0f, RoofHeightAround(position) + settings.BuildingClearance - position.y);
            buildingLift = Application.isPlaying
                ? Mathf.Max(needed, Mathf.MoveTowards(buildingLift, needed, 15f * Time.unscaledDeltaTime))
                : needed;
            position.y += buildingLift;
            transform.SetPositionAndRotation(position, rig.Rotation);
        }

        /// <summary>
        /// Gives the generated city buildings a collider to measure roof heights. They go on the
        /// Ignore Raycast layer so they never block clicks on the scene.
        /// </summary>
        private void CollectBuildings()
        {
            buildings.Clear();
            if (string.IsNullOrEmpty(buildingNameTag)) return;
            foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (!filter.name.Contains(buildingNameTag) || filter.sharedMesh == null) continue;
                if (!filter.TryGetComponent(out MeshCollider collider))
                {
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                filter.gameObject.layer = 2; // Ignore Raycast
                buildings.Add(collider);
            }
        }

        /// <summary>Highest roof under the camera or within the building clearance around it (-inf when none).</summary>
        private float RoofHeightAround(Vector3 position)
        {
            float reach = settings.BuildingClearance, roof = float.NegativeInfinity;
            foreach (MeshCollider building in buildings)
            {
                if (building == null) continue;
                Bounds bounds = building.bounds;
                if (position.x < bounds.min.x - reach || position.x > bounds.max.x + reach ||
                    position.z < bounds.min.z - reach || position.z > bounds.max.z + reach) continue;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 offset = i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(reach, 0f, 0f);
                    var ray = new Ray(new Vector3(position.x + offset.x, bounds.max.y + 1f, position.z + offset.z), Vector3.down);
                    if (building.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f)) roof = Mathf.Max(roof, hit.point.y);
                }
            }
            return roof;
        }
    }
}
