using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// Day-to-day operation of the CEDI: receiving trucks and dispatch turbos come and go through
    /// the gate and the docks, and forklifts move pallets between the staging lanes and the racks.
    /// Every event updates the <see cref="OperationState"/> (positions, trucks in the lot,
    /// dispatches, notices and facility data), so the HUD reflects what happens in the scene.
    /// Plain C#: call <see cref="Tick"/> with the elapsed time; the scene only draws the agents.
    /// </summary>
    public sealed class CediOperationSimulation
    {
        private const string ImportObjectiveId = "import-container";
        private const float PublishInterval = 1f;

        private readonly OperationState state;
        private readonly Random random;
        private readonly List<VehicleAgent> vehicles = new List<VehicleAgent>();
        private readonly int[] receivingStaged = new int[CediLayout.DockZ.Length];
        private readonly int[] receivingClaimed = new int[CediLayout.DockZ.Length];
        private readonly int[] shippingStaged = new int[CediLayout.DockZ.Length];
        private readonly int[] shippingIncoming = new int[CediLayout.DockZ.Length];
        private readonly FacilityInfo baseWarehouse;
        private readonly FacilityInfo baseGate;

        private int nextId = 1;
        private int receivingCount;
        private float nextReceivingAt;
        private float nextDispatchAt;
        private int pendingPutAways;
        private int pendingPicks;
        private double totalGateWait;
        private double now;
        private float lastPublish = float.MinValue;
        private bool facilitiesDirty = true;

        public CediOperationSimulation(OperationState state, CediOperationSettings settings = null, int seed = 1616)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            Settings = settings ?? new CediOperationSettings();
            random = new Random(seed);
            InboundGate = new GateLane(Settings.QueueGap);
            OutboundGate = new GateLane(Settings.QueueGap);
            state.Facilities.TryGet(CediFacilityIds.Warehouse, out baseWarehouse);
            state.Facilities.TryGet(CediFacilityIds.Gate, out baseGate);

            nextReceivingAt = Settings.FirstReceivingAt;
            nextDispatchAt = Settings.FirstDispatchAt;
            for (int i = 0; i < Settings.ForkliftCount; i++)
                vehicles.Add(new ForkliftAgent(nextId++, $"MC-{i + 1:00}", Settings, ForkliftNetwork.Park(i)));
        }

        public event Action<VehicleAgent> VehicleAdded;
        public event Action<VehicleAgent> VehicleRemoved;

        public CediOperationSettings Settings { get; }
        public DockManager Docks { get; } = new DockManager();
        public GateLane InboundGate { get; }
        public GateLane OutboundGate { get; }

        /// <summary>Simulation time in seconds since the start.</summary>
        public float Time { get; private set; }

        public IReadOnlyList<VehicleAgent> Vehicles => vehicles;

        /// <summary>Pallets waiting in each receiving staging lane (index = dock lane).</summary>
        public IReadOnlyList<int> ReceivingStaged => receivingStaged;

        /// <summary>Pallets ready in each shipping staging lane (index = dock lane).</summary>
        public IReadOnlyList<int> ShippingStaged => shippingStaged;

        public int TrucksEntered { get; private set; }
        public int TrucksExited { get; private set; }
        public double AverageGateWait => TrucksEntered == 0 ? 0 : totalGateWait / TrucksEntered;

        /// <summary>Advances the operation.</summary>
        /// <param name="deltaTime">Elapsed simulation seconds.</param>
        /// <param name="clock">Clock for notices (the HUD's time), in seconds.</param>
        public void Tick(float deltaTime, double clock)
        {
            now = clock;
            Time += deltaTime;
            SpawnTrucks();

            for (int i = 0; i < vehicles.Count; i++) vehicles[i].Tick(this, deltaTime);
            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                if (!vehicles[i].IsFinished) continue;
                VehicleAgent gone = vehicles[i];
                vehicles.RemoveAt(i);
                VehicleRemoved?.Invoke(gone);
            }

            int inYard = 0;
            foreach (VehicleAgent vehicle in vehicles)
                if (vehicle is TruckAgent truck && truck.IsInYard) inYard++;
            if (inYard != state.Resources.TrucksInYard) state.Resources.SetTrucksInYard(inYard);

            if (facilitiesDirty && Time - lastPublish >= PublishInterval) PublishFacilities();
        }

        // ------------------------------------------------------------------ spawning

        private void SpawnTrucks()
        {
            if (Time >= nextReceivingAt)
            {
                nextReceivingAt = Time + Jitter(Settings.ReceivingInterval);
                if (InboundGate.QueueLength < Settings.MaxTrucksAtGate) Add(CreateReceivingTruck());
            }
            if (Time >= nextDispatchAt)
            {
                nextDispatchAt = Time + Jitter(Settings.DispatchInterval);
                if (InboundGate.QueueLength < Settings.MaxTrucksAtGate) Add(CreateDispatchTruck());
            }
        }

        private float Jitter(float interval) => interval * (0.8f + 0.4f * (float)random.NextDouble());

        private TruckAgent CreateReceivingTruck()
        {
            // The first truck of the day is Don Hernán's import container.
            Supplier supplier = receivingCount++ == 0
                ? CediCatalog.Suppliers[0]
                : CediCatalog.Suppliers[random.Next(CediCatalog.Suppliers.Length)];
            int pallets = random.Next(Settings.MinPalletsPerTruck, Settings.MaxPalletsPerTruck + 1);
            return new TruckAgent(nextId++, VehicleKind.ReceivingTruck, CediCatalog.Plate(random), Settings,
                supplier.Name, supplier.Origin, supplier.Product, pallets, random.Next(2) == 0, random.Next(2) == 0);
        }

        private TruckAgent CreateDispatchTruck()
        {
            string destination = CediCatalog.Destinations[random.Next(CediCatalog.Destinations.Length)];
            string product = CediCatalog.DispatchProducts[random.Next(CediCatalog.DispatchProducts.Length)];
            int pallets = random.Next(Settings.MinPalletsPerDispatch, Settings.MaxPalletsPerDispatch + 1);
            return new TruckAgent(nextId++, VehicleKind.DispatchTruck, CediCatalog.Plate(random), Settings,
                $"Tiendas de {destination}", destination, product, pallets, random.Next(2) == 0, random.Next(2) == 0);
        }

        private void Add(TruckAgent truck)
        {
            vehicles.Add(truck);
            InboundGate.Join(truck);
            VehicleAdded?.Invoke(truck);
        }

        // ------------------------------------------------------------------ truck events

        internal void OnTruckAtGate(TruckAgent truck)
        {
            if (truck.IsReceiving) Notify($"Camión de {truck.Partner} en portería.", NotificationSeverity.Info);
            facilitiesDirty = true;
        }

        internal void OnNoDockFree(TruckAgent truck)
        {
            string docks = truck.IsReceiving ? "recibo" : "despacho";
            Notify($"Muelles de {docks} ocupados: {truck.Plate} espera en portería.", NotificationSeverity.Warning);
        }

        internal void OnEnteredYard(TruckAgent truck, float gateWait)
        {
            TrucksEntered++;
            totalGateWait += gateWait;
            facilitiesDirty = true;
        }

        internal void OnUnloaded(TruckAgent truck)
        {
            if (truck.Partner == CediCatalog.DonHernan)
            {
                Notify("Contenedor de Don Hernán descargado.", NotificationSeverity.Success);
                if (state.Mission.Find(ImportObjectiveId) is Objective objective && !objective.IsCompleted)
                    state.Mission.SetObjectiveProgress(ImportObjectiveId, objective.Target);
            }
            facilitiesDirty = true;
        }

        internal void OnLoaded(TruckAgent truck)
        {
            Notify($"Despacho a {truck.Place} en camino.", NotificationSeverity.Info);
            facilitiesDirty = true;
        }

        internal void OnLeftYard(TruckAgent truck)
        {
            TrucksExited++;
            if (!truck.IsReceiving)
                state.RegisterDispatch(SampleOperationData.DispatchObjectiveId, Settings.PointsPerDispatch, now);
            facilitiesDirty = true;
        }

        internal bool TryStagePallet(Dock dock)
        {
            if (receivingStaged[dock.LaneIndex] >= Settings.StagingCapacity) return false;
            receivingStaged[dock.LaneIndex]++;
            facilitiesDirty = true;
            return true;
        }

        internal bool TryTakeStagedPallet(Dock dock)
        {
            if (shippingStaged[dock.LaneIndex] == 0) return false;
            shippingStaged[dock.LaneIndex]--;
            facilitiesDirty = true;
            return true;
        }

        // ------------------------------------------------------------------ forklift tasks

        /// <summary>
        /// Next pallet move for an idle forklift: feeding docked turbos first, unless a receiving
        /// lane is full (then trucks could not unload), or null when there is nothing to do.
        /// </summary>
        internal ForkliftTask AssignTask(ForkliftAgent forklift)
        {
            bool receivingJammed = Array.Exists(receivingStaged, staged => staged >= Settings.StagingCapacity);
            ForkliftTask task = receivingJammed
                ? CreatePutAway() ?? CreatePick()
                : CreatePick() ?? CreatePutAway();
            if (task != null) facilitiesDirty = true;
            return task;
        }

        private ForkliftTask CreatePick()
        {
            if (state.Resources.OccupiedPositions - pendingPicks <= 0) return null;
            foreach (Dock dock in Docks.Docks)
            {
                if (dock.Kind != DockKind.Shipping || dock.Occupant == null) continue;
                int lane = dock.LaneIndex;
                int coming = shippingStaged[lane] + shippingIncoming[lane];
                if (coming >= dock.Occupant.PalletsRemaining || coming >= Settings.StagingCapacity) continue;

                shippingIncoming[lane]++;
                pendingPicks++;
                return new ForkliftTask(ForkliftTaskKind.Pick, RandomRackSlot(), ForkliftNetwork.ShippingStaging(lane), dock);
            }
            return null;
        }

        private ForkliftTask CreatePutAway()
        {
            ResourceState resources = state.Resources;
            if (resources.OccupiedPositions + pendingPutAways >= resources.PositionCapacity) return null;

            int best = -1;
            for (int lane = 0; lane < receivingStaged.Length; lane++)
            {
                int waiting = receivingStaged[lane] - receivingClaimed[lane];
                if (waiting > 0 && (best < 0 || waiting > receivingStaged[best] - receivingClaimed[best])) best = lane;
            }
            if (best < 0) return null;

            receivingClaimed[best]++;
            pendingPutAways++;
            return new ForkliftTask(ForkliftTaskKind.PutAway, ForkliftNetwork.ReceivingStaging(best), RandomRackSlot(),
                Docks.Get(DockKind.Receiving, best));
        }

        internal void OnPalletLifted(ForkliftTask task)
        {
            if (task.Kind == ForkliftTaskKind.PutAway)
            {
                receivingStaged[task.Dock.LaneIndex]--;
                receivingClaimed[task.Dock.LaneIndex]--;
            }
            else
            {
                pendingPicks--;
                state.Resources.SetOccupiedPositions(state.Resources.OccupiedPositions - 1);
            }
            facilitiesDirty = true;
        }

        internal void OnPalletDropped(ForkliftTask task)
        {
            if (task.Kind == ForkliftTaskKind.PutAway)
            {
                pendingPutAways--;
                bool wasCritical = state.Resources.IsStorageCritical;
                state.Resources.SetOccupiedPositions(state.Resources.OccupiedPositions + 1);
                if (!wasCritical && state.Resources.IsStorageCritical)
                    Notify("Bodega por encima del 90 % de ocupación.", NotificationSeverity.Warning);
            }
            else
            {
                shippingIncoming[task.Dock.LaneIndex]--;
                shippingStaged[task.Dock.LaneIndex]++;
            }
            facilitiesDirty = true;
        }

        private ForkliftSpot RandomRackSlot()
        {
            int aisle = random.Next(ForkliftNetwork.AisleZ.Length);
            float x = ForkliftNetwork.RackMinX + (float)random.NextDouble() * (ForkliftNetwork.RackMaxX - ForkliftNetwork.RackMinX);
            return ForkliftNetwork.RackSlot(aisle, x, random.Next(2) == 0);
        }

        // ------------------------------------------------------------------ HUD data

        private void Notify(string message, NotificationSeverity severity) =>
            state.Notifications.Push(message, severity, now);

        /// <summary>Refreshes the warehouse and gate data shown in the selection panel.</summary>
        private void PublishFacilities()
        {
            lastPublish = Time;
            facilitiesDirty = false;
            ResourceState resources = state.Resources;
            int busyForklifts = 0;
            foreach (VehicleAgent vehicle in vehicles)
                if (vehicle is ForkliftAgent forklift && forklift.IsBusy) busyForklifts++;

            if (baseWarehouse != null)
                state.Facilities.Set(new FacilityInfo(baseWarehouse.Id, baseWarehouse.Name, baseWarehouse.Subtitle,
                    baseWarehouse.Kind,
                    new[]
                    {
                        new FacilityStat("Posiciones ocupadas", HudFormat.Ratio(resources.OccupiedPositions, resources.PositionCapacity)),
                        new FacilityStat("Muelles de recibo libres", $"{Docks.FreeCount(DockKind.Receiving)} de 3"),
                        new FacilityStat("Muelles de despacho libres", $"{Docks.FreeCount(DockKind.Shipping)} de 3"),
                        new FacilityStat("Montacargas trabajando", $"{busyForklifts}/{Settings.ForkliftCount}"),
                        new FacilityStat("Estibas por ubicar", HudFormat.Number(Sum(receivingStaged))),
                        new FacilityStat("Estibas listas para despacho", HudFormat.Number(Sum(shippingStaged)))
                    },
                    baseWarehouse.Orders));

            if (baseGate != null)
                state.Facilities.Set(new FacilityInfo(baseGate.Id, baseGate.Name, baseGate.Subtitle, baseGate.Kind,
                    new[]
                    {
                        new FacilityStat("Camiones en cola", HudFormat.Number(InboundGate.QueueLength)),
                        new FacilityStat("Camiones en el patio", HudFormat.Number(resources.TrucksInYard)),
                        new FacilityStat("Ingresos hoy", HudFormat.Number(TrucksEntered)),
                        new FacilityStat("Salidas hoy", HudFormat.Number(TrucksExited)),
                        new FacilityStat("Espera promedio en portería", $"{AverageGateWait:0} s")
                    },
                    baseGate.Orders));
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values) total += value;
            return total;
        }
    }
}
