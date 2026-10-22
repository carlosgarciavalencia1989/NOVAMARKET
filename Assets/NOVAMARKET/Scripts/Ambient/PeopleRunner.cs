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
    /// Runs the people of the valley in Play mode and draws them from one pool: walkers on the sidewalks,
    /// park paths and zebra crossings around the camera (the traffic waits for those crossing), passengers
    /// on the metro platforms and the CEDI staff. All rules live in the plain C# simulations.
    /// </summary>
    public class PeopleRunner : MonoBehaviour
    {
        private const float CrosswalkPaintHeight = 0.04f;

        [SerializeField] private CityBlocksData blocks;
        [SerializeField] private AmbientSettingsData settings;
        [SerializeField] private AmbientVisualProfile visuals;
        [SerializeField] private AmbientTrafficRunner traffic;
        [SerializeField] private MetroRunner metro;
        [SerializeField] private CediOperationRunner operation;
        [SerializeField] private StrategyCameraController cameraController;

        [Header("Simulation")]
        [SerializeField] private int seed = 31;
        [Tooltip("People live within this distance of the camera focus: base + camera distance × factor.")]
        [SerializeField] private Vector2 radiusBaseAndFactor = new Vector2(60f, 1f);
        [SerializeField] private Vector2 radiusLimits = new Vector2(150f, 450f);
        [SerializeField, Range(5f, 30f)] private float stepsPerSecond = 10f;
        [SerializeField, Range(0, 20)] private int waitingPerPlatform = 8;

        private readonly Dictionary<Pedestrian, PedestrianView> views = new Dictionary<Pedestrian, PedestrianView>();
        private ViewPool<PedestrianView> pool;
        private float accumulator;

        public AmbientPedestrians Walkers { get; private set; }
        public MetroPassengers Passengers { get; private set; }
        public CediWorkers Staff { get; private set; }
        public int ActiveViews => views.Count;

        private void Start()
        {
            if (visuals == null || settings == null)
            {
                Debug.LogWarning("PeopleRunner: assign the visual profile and the settings (NOVAMARKET → Ambient → Setup).");
                enabled = false;
                return;
            }
            var root = new GameObject("People").transform;
            root.SetParent(transform, false);
            pool = new ViewPool<PedestrianView>(root);
            AmbientQuality quality = traffic != null ? traffic.Quality : AmbientQuality.Medium;

            if (blocks != null && traffic != null && traffic.Traffic != null)
            {
                WalkNetwork walks = WalkNetwork.Build(CityWalkLayout.Build(blocks.ToBlocks()), traffic.Traffic.Network);
                Walkers = new AmbientPedestrians(walks, traffic.Traffic, settings.ToPedestrianSettings(quality), seed);
                traffic.Traffic.SharedPedestrians = Walkers;
                Walkers.Added += Show;
                Walkers.Removed += Hide;
                PaintCrosswalks(walks);
            }
            if (metro != null && metro.Metro != null)
            {
                Passengers = new MetroPassengers(metro.Metro, waitingPerPlatform, seed);
                foreach (Pedestrian p in Passengers.People) Show(p);
                Passengers.Added += Show;
                Passengers.Removed += Hide;
            }
            if (operation != null && operation.Simulation != null)
            {
                Staff = new CediWorkers(operation.Simulation, seed);
                foreach (Pedestrian p in Staff.People) Show(p);
                Staff.Added += Show;
                Staff.Removed += Hide;
            }
        }

        private void OnDestroy()
        {
            if (traffic != null && traffic.Traffic != null && traffic.Traffic.SharedPedestrians == Walkers)
                traffic.Traffic.SharedPedestrians = null;
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

            float step = 1f / Mathf.Max(5f, stepsPerSecond);
            accumulator += Time.deltaTime;
            int steps = 0;
            while (accumulator >= step && steps < 3)
            {
                Walkers?.Tick(step, focus, radius);
                Passengers?.Tick(step);
                Staff?.Tick(step);
                foreach (PedestrianView view in views.Values) view.Capture();
                accumulator -= step;
                steps++;
            }
            if (steps == 3) accumulator = 0f;
            float alpha = accumulator / step, dt = Time.deltaTime;
            foreach (PedestrianView view in views.Values) view.Sync(alpha, dt);
        }

        private void Show(Pedestrian person)
        {
            if (views.ContainsKey(person)) return;
            GameObject prefab = visuals.PedestrianPrefabFor(person.Variant);
            if (prefab == null) return;
            PedestrianView view = pool.Get(prefab);
            view.Bind(person, visuals.ClothesColourFor(person.Variant));
            views[person] = view;
        }

        private void Hide(Pedestrian person)
        {
            if (!views.Remove(person, out PedestrianView view)) return;
            view.Unbind();
            pool.Release(view);
        }

        /// <summary>Zebra stripes on every crossing (static street furniture).</summary>
        private void PaintCrosswalks(WalkNetwork walks)
        {
            if (visuals.CrosswalkPrefab == null) return;
            var root = new GameObject("Crosswalks").transform;
            root.SetParent(transform, false);
            foreach (WalkEdge edge in walks.Edges)
            {
                if (!edge.IsCrosswalk) continue;
                Num.Vector2 centre = (edge.A.Position + edge.B.Position) / 2f;
                var at = new Vector3(centre.X, CrosswalkPaintHeight, centre.Y);
                var facing = Quaternion.LookRotation(new Vector3(edge.Direction.X, 0f, edge.Direction.Y), Vector3.up);
                Instantiate(visuals.CrosswalkPrefab, at, facing, root).name = $"Crosswalk_{edge.Id}";
            }
        }
    }
}
