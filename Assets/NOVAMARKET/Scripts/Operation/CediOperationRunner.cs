using System.Collections.Generic;
using NovaMarket.Simulation;
using NovaMarket.UI;
using UnityEngine;

namespace NovaMarket.Operation
{
    /// <summary>
    /// Runs <see cref="CediOperationSimulation"/> in Play mode on the HUD's
    /// <see cref="NovaMarket.Simulation.OperationState"/> and draws it: one prefab per vehicle,
    /// the gate barriers and the pallets waiting in the staging lanes. All rules live in the
    /// simulation; this component only mirrors it in the scene.
    /// </summary>
    public class CediOperationRunner : MonoBehaviour
    {
        private const int PalletsPerRow = 4;
        private const float PalletSpacing = 1.3f;

        [SerializeField] private HudDataSource dataSource;

        [Header("Prefabs (simple shapes for now; swap them for models later)")]
        [SerializeField] private VehicleVisual receivingTruckPrefab;
        [SerializeField] private VehicleVisual dispatchTruckPrefab;
        [SerializeField] private VehicleVisual forkliftPrefab;
        [SerializeField] private GameObject palletPrefab;
        [Tooltip("Random colours for the receiving trucks' containers (renderer named \"Cargo\").")]
        [SerializeField] private Material[] containerMaterials;

        [Header("Scene")]
        [SerializeField] private GateBarrier inboundBarrier;
        [SerializeField] private GateBarrier outboundBarrier;

        [Header("Simulation")]
        [Tooltip("1 = real time; higher values speed the whole operation up.")]
        [SerializeField, Range(0.25f, 4f)] private float simulationSpeed = 1f;
        [SerializeField] private int seed = 1616;

        private readonly Dictionary<VehicleAgent, VehicleVisual> visuals = new Dictionary<VehicleAgent, VehicleVisual>();
        private readonly System.Random colourRandom = new System.Random(5);
        private Transform vehicleRoot;
        private List<GameObject>[] receivingPallets;
        private List<GameObject>[] shippingPallets;

        public CediOperationSimulation Simulation { get; private set; }

        // Created in Awake so the planning level can schedule trucks from its own Start.
        private void Awake()
        {
            CediOperationSettings settings = dataSource.Scenario switch
            {
                HudScenario.SeasonPlanning => CediOperationSettings.ForPlanning(),
                HudScenario.PlannerCampaign => CediOperationSettings.ForCampaign(),
                _ => null
            };
            Simulation = new CediOperationSimulation(dataSource.State, settings, seed);
        }

        private void Start()
        {
            Simulation.VehicleAdded += Spawn;
            Simulation.VehicleRemoved += Despawn;

            vehicleRoot = new GameObject("Vehicles").transform;
            vehicleRoot.SetParent(transform, false);
            foreach (VehicleAgent vehicle in Simulation.Vehicles) Spawn(vehicle);

            receivingPallets = CreatePalletLanes(DockKind.Receiving);
            shippingPallets = CreatePalletLanes(DockKind.Shipping);
        }

        private void OnDestroy()
        {
            if (Simulation == null) return;
            Simulation.VehicleAdded -= Spawn;
            Simulation.VehicleRemoved -= Despawn;
        }

        private void Update()
        {
            Simulation.Tick(Time.deltaTime * simulationSpeed, dataSource.Now);

            if (inboundBarrier != null) inboundBarrier.IsOpen = Simulation.InboundGate.IsBarrierOpen;
            if (outboundBarrier != null) outboundBarrier.IsOpen = Simulation.OutboundGate.IsBarrierOpen;
            ShowPallets(receivingPallets, Simulation.ReceivingStaged);
            ShowPallets(shippingPallets, Simulation.ShippingStaged);
        }

        private void Spawn(VehicleAgent vehicle)
        {
            VehicleVisual prefab = vehicle.Kind switch
            {
                VehicleKind.ReceivingTruck => receivingTruckPrefab,
                VehicleKind.DispatchTruck => dispatchTruckPrefab,
                _ => forkliftPrefab
            };
            VehicleVisual visual = Instantiate(prefab, vehicleRoot);
            visual.Bind(vehicle);
            visuals[vehicle] = visual;

            if (vehicle.Kind == VehicleKind.ReceivingTruck && containerMaterials != null && containerMaterials.Length > 0)
                foreach (Renderer part in visual.GetComponentsInChildren<Renderer>())
                    if (part.name == "Cargo")
                        part.sharedMaterial = containerMaterials[colourRandom.Next(containerMaterials.Length)];
        }

        private void Despawn(VehicleAgent vehicle)
        {
            if (!visuals.Remove(vehicle, out VehicleVisual visual)) return;
            Destroy(visual.gameObject);
        }

        /// <summary>Two rows of pallets per staging lane, from the dock door inwards.</summary>
        private List<GameObject>[] CreatePalletLanes(DockKind kind)
        {
            int capacity = Simulation.Settings.StagingCapacity;
            var lanes = new List<GameObject>[CediLayout.DockZ.Length];
            float side = kind == DockKind.Shipping ? 1f : -1f;
            var parent = new GameObject($"StagedPallets_{kind}").transform;
            parent.SetParent(transform, false);

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                lanes[lane] = new List<GameObject>();
                for (int i = 0; i < capacity; i++)
                {
                    int column = i % PalletsPerRow;
                    float row = i / PalletsPerRow == 0 ? -0.65f : 0.65f;
                    var position = new Vector3(side * (20.6f - column * PalletSpacing), CediLayout.IndoorFloor,
                        CediLayout.DockZ[lane] + row);
                    GameObject pallet = Instantiate(palletPrefab, position, Quaternion.identity, parent);
                    pallet.SetActive(false);
                    lanes[lane].Add(pallet);
                }
            }
            return lanes;
        }

        private static void ShowPallets(List<GameObject>[] lanes, IReadOnlyList<int> counts)
        {
            for (int lane = 0; lane < lanes.Length; lane++)
            for (int i = 0; i < lanes[lane].Count; i++)
            {
                bool visible = i < counts[lane];
                if (lanes[lane][i].activeSelf != visible) lanes[lane][i].SetActive(visible);
            }
        }
    }
}
