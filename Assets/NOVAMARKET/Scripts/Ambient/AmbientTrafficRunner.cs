using System.Collections.Generic;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Operation;
using NovaMarket.Simulation.Ambient;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Runs the city traffic (<see cref="AmbientTraffic"/>) in Play mode around the strategy camera and
    /// draws it: vehicles from a pool, traffic lights and bus stops. It shares the highway with the CEDI
    /// operation. All rules live in the simulation; this component only mirrors it in the scene.
    /// </summary>
    public class AmbientTrafficRunner : MonoBehaviour
    {
        private const float HighwaySurface = 0.06f; // the highway slab (MainRoad) is 8 cm thick
        private const int MaxStepsPerFrame = 3;

        [SerializeField] private RoadNetworkData network;
        [SerializeField] private AmbientSettingsData settings;
        [SerializeField] private AmbientVisualProfile visuals;
        [SerializeField] private StrategyCameraController cameraController;
        [Tooltip("CEDI operation that shares the highway (optional).")]
        [SerializeField] private CediOperationRunner operation;

        [Header("Simulation")]
        [SerializeField] private AmbientQuality quality = AmbientQuality.Medium;
        [SerializeField] private int seed = 21;
        [Tooltip("Vehicles live within this distance of the camera focus: base + camera distance × factor.")]
        [SerializeField] private Vector2 radiusBaseAndFactor = new Vector2(100f, 1.4f);
        [SerializeField] private Vector2 radiusLimits = new Vector2(220f, 800f);
        [Tooltip("Simulation steps per second; the vehicles glide smoothly between steps.")]
        [SerializeField, Range(5f, 60f)] private float stepsPerSecond = 20f;

        [Header("Lights (ready for dusk)")]
        [SerializeField] private bool headlightsOn;

        private readonly Dictionary<AmbientVehicle, AmbientVehicleView> views = new Dictionary<AmbientVehicle, AmbientVehicleView>();
        private readonly List<(TrafficLightView View, TrafficLane Lane)> lights = new List<(TrafficLightView, TrafficLane)>();
        private ViewPool<AmbientVehicleView> pool;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private float accumulator;

        public AmbientTraffic Traffic { get; private set; }
        public AmbientQuality Quality => quality;
        public int ActiveViews => views.Count;
        public int PooledViews => pool?.Created ?? 0;

        /// <summary>Milliseconds of the last simulation step (the traffic logic).</summary>
        public float SimulationMs { get; private set; }

        /// <summary>Milliseconds this frame spent moving the vehicle models and lights.</summary>
        public float ViewsMs { get; private set; }

        private void Awake()
        {
            if (network == null || settings == null)
            {
                Debug.LogWarning("AmbientTrafficRunner: assign the road network and the settings (NOVAMARKET → Ambient → Setup).");
                enabled = false;
                return;
            }
            Traffic = new AmbientTraffic(RoadNetwork.Build(network.ToDefinition(), seed), settings.ToTrafficSettings(quality), seed);
        }

        private void Start()
        {
            if (Traffic == null) return;
            if (operation != null && operation.Simulation != null)
            {
                Traffic.SharedVehicles = operation.Simulation;
                operation.Simulation.SharedRoad = Traffic;
            }

            var vehicleRoot = new GameObject("Vehicles").transform;
            vehicleRoot.SetParent(transform, false);
            pool = new ViewPool<AmbientVehicleView>(vehicleRoot);
            Traffic.VehicleAdded += Show;
            Traffic.VehicleRemoved += Hide;
            PlaceStreetFurniture();
        }

        private void OnDestroy()
        {
            if (Traffic == null) return;
            Traffic.VehicleAdded -= Show;
            Traffic.VehicleRemoved -= Hide;
            if (operation != null && operation.Simulation != null && operation.Simulation.SharedRoad == Traffic)
                operation.Simulation.SharedRoad = null;
        }

        private void Update()
        {
            Num.Vector2 focus = Num.Vector2.Zero;
            float distance = 150f;
            if (cameraController != null && cameraController.Rig != null)
            {
                Vector3 f = cameraController.Rig.Focus;
                focus = new Num.Vector2(f.x, f.z);
                distance = cameraController.Rig.Distance;
            }
            float radius = Mathf.Clamp(radiusBaseAndFactor.x + distance * radiusBaseAndFactor.y, radiusLimits.x, radiusLimits.y);

            // The traffic thinks a few times per second (enough to drive); the bodies glide in between.
            float step = 1f / Mathf.Max(5f, stepsPerSecond);
            accumulator += Time.deltaTime;
            stopwatch.Restart();
            int steps = 0;
            while (accumulator >= step && steps < MaxStepsPerFrame)
            {
                Traffic.Tick(step, focus, radius);
                foreach (AmbientVehicleView view in views.Values) view.Capture();
                accumulator -= step;
                steps++;
            }
            if (steps == MaxStepsPerFrame) accumulator = 0f; // a long hitch: do not try to catch up
            if (steps > 0) SimulationMs = (float)stopwatch.Elapsed.TotalMilliseconds / steps;

            stopwatch.Restart();
            float alpha = accumulator / step, dt = Time.deltaTime;
            foreach (AmbientVehicleView view in views.Values) view.Sync(alpha, dt, headlightsOn);
            foreach ((TrafficLightView view, TrafficLane lane) in lights) view.Show(Traffic.AspectFor(lane));
            ViewsMs = (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>Changes how many vehicles move around (the traffic restarts).</summary>
        public void SetQuality(AmbientQuality level)
        {
            if (Traffic == null || level == quality) return;
            quality = level;
            AmbientTrafficSettings fresh = settings.ToTrafficSettings(level);
            Traffic.Settings.VehicleCount = fresh.VehicleCount;
        }

        private void Show(AmbientVehicle vehicle)
        {
            GameObject prefab = visuals != null ? visuals.PrefabFor(vehicle.Kind, vehicle.Variant) : null;
            if (prefab == null) return;
            AmbientVehicleView view = pool.Get(prefab);
            view.Bind(vehicle, visuals.BodyColourFor(vehicle.Kind, vehicle.Variant));
            views[vehicle] = view;
        }

        private void Hide(AmbientVehicle vehicle)
        {
            if (!views.Remove(vehicle, out AmbientVehicleView view)) return;
            view.Unbind();
            pool.Release(view);
        }

        /// <summary>One traffic light per approach of each crossing with lights, and a shelter at each bus stop.</summary>
        private void PlaceStreetFurniture()
        {
            if (visuals == null) return;
            var root = new GameObject("StreetFurniture").transform;
            root.SetParent(transform, false);

            foreach (TrafficJunction junction in Traffic.Network.Junctions)
            {
                if (junction.Signal == null || visuals.TrafficLightPrefab == null) continue;
                foreach (TrafficLane lane in junction.Incoming)
                {
                    Vector3 at = Ground(lane.End) + Vector3.up * SurfaceHeight(lane);
                    GameObject light = Instantiate(visuals.TrafficLightPrefab, at, Facing(lane.Direction), root);
                    light.name = $"TrafficLight_{junction.Id}_{lane.Id}";
                    var view = light.GetComponent<TrafficLightView>();
                    if (view != null) lights.Add((view, lane));
                }
            }

            if (visuals.BusStopPrefab == null) return;
            foreach (TrafficLane lane in Traffic.Network.Lanes)
                foreach (float stop in lane.BusStops)
                {
                    float kerb = (lane.TwoWay ? lane.HalfWidth / 2f : lane.HalfWidth) + 1.2f;
                    Num.Vector2 at = lane.PointAt(stop) + lane.Right * kerb;
                    GameObject shelter = Instantiate(visuals.BusStopPrefab, Ground(at), Facing(lane.Direction), root);
                    shelter.name = $"BusStop_{lane.Id}";
                }
        }

        /// <summary>The highway slab (MainRoad) stands 8 cm above the valley floor; its markings go on top of it.</summary>
        private static float SurfaceHeight(TrafficLane lane) => lane.Class == RoadClass.Highway ? HighwaySurface : 0f;

        private static Vector3 Ground(Num.Vector2 point) => new Vector3(point.X, 0f, point.Y);

        private static Quaternion Facing(Num.Vector2 direction) =>
            Quaternion.LookRotation(new Vector3(direction.X, 0f, direction.Y), Vector3.up);
    }
}
