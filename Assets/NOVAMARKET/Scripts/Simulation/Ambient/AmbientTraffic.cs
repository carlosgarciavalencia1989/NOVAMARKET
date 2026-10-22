using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// City traffic around the CEDI: cars, taxis, motos, buses and trucks drive on a
    /// <see cref="RoadNetwork"/>, follow each other, stop at red lights and "PARE" signs, give way,
    /// and motos filter between cars to the front of the queue. Only the vehicles inside a circle
    /// around the camera exist: those that drive out of it are removed and new ones appear at its edge.
    /// It shares the highway with the CEDI trucks (<see cref="SharedVehicles"/> in, <see cref="ISharedRoad"/> out).
    /// Plain C#: call <see cref="Tick"/> with the elapsed time; the scene only draws the vehicles.
    /// </summary>
    public sealed partial class AmbientTraffic : ISharedRoad
    {
        /// <summary>Depth of the moto box at traffic lights: cars stop this far before the line.</summary>
        public const float CarStopBack = 3.5f;

        private const float GridCell = 20f;

        private readonly List<AmbientVehicle> vehicles = new List<AmbientVehicle>();
        private readonly Random random;

        private readonly List<SharedRoadVehicle> externals = new List<SharedRoadVehicle>();
        private readonly List<ExternalOnLane>[] laneExternals;
        private readonly List<int> touchedLanes = new List<int>();
        private readonly List<OvertakeSpan>[] laneSpans;

        private readonly List<Body> bodies = new List<Body>();
        private readonly Dictionary<(int, int), List<int>> bodyGrid = new Dictionary<(int, int), List<int>>();
        private readonly List<List<int>> usedCells = new List<List<int>>();
        private readonly List<int> candidates = new List<int>();
        private readonly List<TrafficLane> nearLanes = new List<TrafficLane>();

        private int nextId = 1;
        private bool filled;

        public AmbientTraffic(RoadNetwork network, AmbientTrafficSettings settings = null, int seed = 21)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Settings = settings ?? new AmbientTrafficSettings();
            random = new Random(seed);
            laneExternals = new List<ExternalOnLane>[network.Lanes.Count];
            laneSpans = new List<OvertakeSpan>[network.Lanes.Count];
            for (int i = 0; i < network.Lanes.Count; i++)
            {
                laneExternals[i] = new List<ExternalOnLane>();
                laneSpans[i] = new List<OvertakeSpan>();
            }
        }

        public event Action<AmbientVehicle> VehicleAdded;
        public event Action<AmbientVehicle> VehicleRemoved;

        /// <summary>A vehicle drove onto a crossing movement (for tests and debugging).</summary>
        public event Action<AmbientVehicle, TrafficConnector> EnteredJunction;

        public RoadNetwork Network { get; }
        public AmbientTrafficSettings Settings { get; }

        /// <summary>Vehicles of another simulation on the same roads (the CEDI trucks); null when alone.</summary>
        public ISharedRoadVehicles SharedVehicles { get; set; }

        /// <summary>People on zebra crossings (vehicles wait for them); null without pedestrians.</summary>
        public ISharedRoadVehicles SharedPedestrians { get; set; }

        /// <summary>Simulation time in seconds (drives the traffic lights).</summary>
        public double Time { get; private set; }

        public IReadOnlyList<AmbientVehicle> Vehicles => vehicles;

        /// <summary>Centre and radius of the circle where vehicles live (set by <see cref="Tick"/>).</summary>
        public Vector2 Focus { get; private set; }
        public float Radius { get; private set; } = 300f;

        /// <summary>Vehicles removed because they were stuck (should stay 0).</summary>
        public int StuckRemovals { get; private set; }

        /// <summary>Vehicles whose front is on a lane.</summary>
        public IReadOnlyList<AmbientVehicle> VehiclesOn(TrafficLink link) => link.Occupants;

        /// <summary>True while a vehicle crossing the crossroads at the lane's start is driving into it.</summary>
        public bool IsEnteringLane(TrafficLane lane)
        {
            if (lane.StartJunction == null) return false;
            foreach (AmbientVehicle holder in lane.StartJunction.Holders)
                if (holder.Held.To == lane) return true;
            return false;
        }

        /// <summary>Light colour a lane sees at its crossing (green where there are no lights).</summary>
        public SignalAspect AspectFor(TrafficLane lane) =>
            lane.EndJunction?.Signal?.AspectFor(lane.SignalGroup, Time) ?? SignalAspect.Green;

        /// <summary>Advances the traffic.</summary>
        /// <param name="focus">Where the camera looks (X/Z).</param>
        /// <param name="radius">Vehicles exist within this distance of the focus.</param>
        public void Tick(float deltaTime, Vector2 focus, float radius)
        {
            if (deltaTime <= 0f) return;
            deltaTime = Math.Min(deltaTime, 0.1f);
            Time += deltaTime;
            Focus = focus;
            Radius = radius;

            CollectExternals();
            RebuildBodies();
            for (int i = 0; i < vehicles.Count; i++) Drive(vehicles[i], deltaTime);
            RemoveFinished();
            Spawn(!filled);
            filled = true;
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Places a vehicle on a lane with its front at <paramref name="front"/> (tests and scripted scenes; no room check).</summary>
        public AmbientVehicle Insert(AmbientVehicleKind kind, TrafficLane lane, float front, float speed = 0f)
        {
            var vehicle = new AmbientVehicle(nextId++, kind, 0, AmbientVehicleSpec.For(kind), 1f)
            {
                Link = lane,
                Front = front,
                Speed = speed
            };
            while (vehicle.BusStopIndex < lane.BusStops.Count && lane.BusStops[vehicle.BusStopIndex] < front) vehicle.BusStopIndex++;
            lane.Occupants.Add(vehicle);
            EnsureAhead(vehicle, 60f);
            vehicle.UpdatePose();
            vehicles.Add(vehicle);
            VehicleAdded?.Invoke(vehicle);
            return vehicle;
        }

        private void Spawn(bool anywhere)
        {
            int missing = Settings.VehicleCount - vehicles.Count;
            if (missing <= 0) return;
            int budget = anywhere ? missing : Math.Min(missing, Settings.MaxSpawnsPerTick);

            Network.LanesNear(Focus, Radius, nearLanes);
            float total = 0f;
            foreach (TrafficLane lane in nearLanes) total += SpawnWeight(lane);
            if (total <= 0f) return;

            int spawned = 0;
            for (int attempt = 0; attempt < budget * 8 && spawned < budget; attempt++)
            {
                TrafficLane lane = PickLane(total);
                if (lane == null || lane.Length < 40f) continue;
                float s = 14f + (float)random.NextDouble() * (lane.Length - 28f);
                float distance = Vector2.Distance(lane.PointAt(s), Focus);
                if (distance > Radius || !anywhere && distance < Radius * 0.72f) continue;
                // At the edge, most new vehicles drive inwards (those driving out would leave at once).
                if (!anywhere && Vector2.Dot(lane.Direction, Focus - lane.PointAt(s)) < 0f && random.NextDouble() < 0.85) continue;
                if (!IsSpawnPlaceFree(lane, s)) continue;

                AmbientVehicleKind kind = PickKind();
                AmbientVehicleSpec spec = AmbientVehicleSpec.For(kind);
                if (!IsSpawnClear(lane, s, spec)) continue;

                var vehicle = new AmbientVehicle(nextId++, kind, random.Next(AmbientTrafficSettings.VariantCount), spec,
                    0.9f + 0.18f * (float)random.NextDouble())
                {
                    Link = lane,
                    Front = s,
                    Speed = Math.Min(lane.SpeedLimit, spec.MaxSpeed) * 0.6f
                };
                while (vehicle.BusStopIndex < lane.BusStops.Count && lane.BusStops[vehicle.BusStopIndex] < s) vehicle.BusStopIndex++;
                lane.Occupants.Add(vehicle);
                EnsureAhead(vehicle, 60f);
                vehicle.UpdatePose();
                vehicles.Add(vehicle);
                spawned++;
                VehicleAdded?.Invoke(vehicle);
            }
        }

        private float SpawnWeight(TrafficLane lane) =>
            lane.Length * (lane.Class == RoadClass.Highway ? Settings.HighwaySpawnWeight : 1f);

        private TrafficLane PickLane(float total)
        {
            float pick = (float)random.NextDouble() * total;
            foreach (TrafficLane lane in nearLanes)
            {
                pick -= SpawnWeight(lane);
                if (pick <= 0f) return lane;
            }
            return nearLanes.Count > 0 ? nearLanes[nearLanes.Count - 1] : null;
        }

        private AmbientVehicleKind PickKind()
        {
            double r = random.NextDouble();
            if (r < Settings.MotoShare) return AmbientVehicleKind.Moto;
            r -= Settings.MotoShare;
            if (r < Settings.BusShare) return AmbientVehicleKind.Bus;
            r -= Settings.BusShare;
            if (r < Settings.TruckShare) return AmbientVehicleKind.Truck;
            return random.NextDouble() < Settings.TaxiShareOfCars ? AmbientVehicleKind.Taxi : AmbientVehicleKind.Car;
        }

        /// <summary>Not at a bus stop, in a "do not block" box or in a stretch kept for overtaking.</summary>
        private bool IsSpawnPlaceFree(TrafficLane lane, float s)
        {
            foreach (float stop in lane.BusStops)
                if (Math.Abs(stop - s) < 18f) return false;
            foreach ((float start, float end) in lane.KeepClear)
                if (s > start - 15f && s < end + 15f) return false;
            foreach (OvertakeSpan span in laneSpans[lane.Id])
                if (s > span.Start - 25f && s < span.End + 15f) return false;
            return true;
        }

        /// <summary>The new vehicle needs its body plus room to brake in front and a gap behind.</summary>
        private bool IsSpawnClear(TrafficLane lane, float s, AmbientVehicleSpec spec)
        {
            const float behind = 8f, ahead = 18f;
            float from = s - spec.Length - behind, to = s + ahead;
            var area = new Footprint(lane.PointAt((from + to) / 2f), lane.Direction, to - from, spec.Width + 1f);
            foreach (AmbientVehicle other in vehicles)
                if (Vector2.DistanceSquared(other.Position, area.Centre) < 60f * 60f && Overlaps(area, other.Footprint, 0f))
                    return false;
            foreach (SharedRoadVehicle external in externals)
                if (Overlaps(area, external.Footprint, 1f)) return false;
            return true;
        }

        // ------------------------------------------------------------------ removing

        private void RemoveFinished()
        {
            float keep = Radius * 1.15f + 40f;
            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                AmbientVehicle vehicle = vehicles[i];
                float distance = Vector2.Distance(vehicle.Position, Focus);
                bool stuck = vehicle.StoppedSeconds > Settings.StuckRemovalSeconds && distance > Radius * 0.5f;
                if (!vehicle.Done && distance <= keep && !stuck) continue;
                if (stuck && !vehicle.Done && distance <= keep) StuckRemovals++;
                Remove(i);
            }
        }

        /// <summary>Removes every vehicle (e.g. when the quality level changes).</summary>
        public void Clear()
        {
            for (int i = vehicles.Count - 1; i >= 0; i--) Remove(i);
            filled = false;
        }

        private void Remove(int index)
        {
            AmbientVehicle vehicle = vehicles[index];
            Release(vehicle);
            EndOvertake(vehicle);
            vehicle.Link.Occupants.Remove(vehicle);
            vehicles.RemoveAt(index);
            foreach (AmbientVehicle other in vehicles)
            {
                if (other.BlockerVehicle == vehicle) other.BlockerVehicle = null;
                if (other.PreviousBlocker == vehicle) other.PreviousBlocker = null;
                if (other.PassThrough == vehicle) other.PassThrough = null;
            }
            VehicleRemoved?.Invoke(vehicle);
        }

        // ------------------------------------------------------------------ bodies (ambient + shared)

        private struct Body
        {
            public AmbientVehicle Vehicle; // null for a shared (CEDI) vehicle
            public int External;
        }

        private struct ExternalOnLane
        {
            public float Rear;
            public float Front;
            public float Speed;
        }

        private Footprint BodyFootprint(int body) =>
            bodies[body].Vehicle?.Footprint ?? externals[bodies[body].External].Footprint;

        private float BodySpeed(int body) =>
            bodies[body].Vehicle?.Speed ?? externals[bodies[body].External].Speed;

        private void CollectExternals()
        {
            externals.Clear();
            foreach (int lane in touchedLanes) laneExternals[lane].Clear();
            touchedLanes.Clear();
            SharedVehicles?.CollectSharedRoadVehicles(externals);
            SharedPedestrians?.CollectSharedRoadVehicles(externals);

            foreach (SharedRoadVehicle external in externals)
            {
                Footprint body = external.Footprint;
                TrafficLane lane = Network.FindLane(body.Centre, body.Forward, 2.8f, out _);
                if (lane == null) continue;
                (float rear, float front) = ProjectedRange(lane, body);
                if (laneExternals[lane.Id].Count == 0) touchedLanes.Add(lane.Id);
                laneExternals[lane.Id].Add(new ExternalOnLane { Rear = rear, Front = front, Speed = external.Speed });
            }
        }

        /// <summary>Stretch of a lane covered by a body (distances from the lane start).</summary>
        private static (float Rear, float Front) ProjectedRange(TrafficLane lane, Footprint body)
        {
            float centre = lane.Project(body.Centre, out _);
            float reach = body.HalfLength * Math.Abs(Vector2.Dot(body.Forward, lane.Direction))
                          + body.HalfWidth * Math.Abs(Vector2.Dot(body.Right, lane.Direction));
            return (centre - reach, centre + reach);
        }

        private void RebuildBodies()
        {
            bodies.Clear();
            foreach (List<int> cell in usedCells) cell.Clear();
            usedCells.Clear();
            for (int i = 0; i < vehicles.Count; i++) AddBody(new Body { Vehicle = vehicles[i], External = -1 }, vehicles[i].Position);
            for (int i = 0; i < externals.Count; i++) AddBody(new Body { External = i }, externals[i].Footprint.Centre);
        }

        private void AddBody(Body body, Vector2 position)
        {
            bodies.Add(body);
            var key = (Cell(position.X), Cell(position.Y));
            if (!bodyGrid.TryGetValue(key, out List<int> cell)) bodyGrid[key] = cell = new List<int>();
            if (cell.Count == 0) usedCells.Add(cell);
            cell.Add(bodies.Count - 1);
        }

        /// <summary>Bodies whose centre lies within <paramref name="radius"/> (plus a cell) of a point.</summary>
        private void QueryBodies(Vector2 centre, float radius, List<int> into)
        {
            into.Clear();
            int x0 = Cell(centre.X - radius), x1 = Cell(centre.X + radius);
            int z0 = Cell(centre.Y - radius), z1 = Cell(centre.Y + radius);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                if (bodyGrid.TryGetValue((x, z), out List<int> cell))
                    into.AddRange(cell);
        }

        private static int Cell(float coordinate) => (int)Math.Floor(coordinate / GridCell);

        // ------------------------------------------------------------------ shared road (for the CEDI)

        public void CollectFootprints(Vector2 centre, float radius, List<Footprint> into)
        {
            foreach (AmbientVehicle vehicle in vehicles)
            {
                float reach = radius + vehicle.Length;
                if (Vector2.DistanceSquared(vehicle.Position, centre) <= reach * reach) into.Add(vehicle.Footprint);
            }
        }

        public float DistanceToRedLight(Vector2 front, Vector2 heading, float speed, float lookahead)
        {
            TrafficLane lane = Network.FindLane(front, heading, 2.8f, out float s);
            if (lane?.EndJunction?.Signal == null || s > lane.Length - 0.3f) return float.MaxValue;
            // Between the car line and the moto line (stopped there behind someone) it stays where it is.
            float distance = Math.Max(0f, lane.Length - lane.StopLineBack - CarStopBack - s);
            if (distance > lookahead) return float.MaxValue;
            SignalAspect aspect = AspectFor(lane);
            if (aspect == SignalAspect.Red) return Math.Max(0f, distance);
            if (aspect == SignalAspect.Amber && speed * speed / (2f * AmberDecel) < distance) return distance;
            return float.MaxValue;
        }

        /// <summary>
        /// Nobody close to the point, and nobody driving towards it from within <paramref name="upstream"/>
        /// meters (vehicles standing in a queue behind it are no danger: they wait).
        /// </summary>
        public bool IsMergeClear(Vector2 point, Vector2 heading, float upstream, float downstream)
        {
            const float near = 12f, moving = 1.5f;
            heading = Vector2.Normalize(heading);
            var right = new Vector2(heading.Y, -heading.X);
            foreach (AmbientVehicle vehicle in vehicles)
            {
                if (Vector2.Dot(vehicle.Heading, heading) < 0.3f) continue;
                Vector2 relative = vehicle.Position - point;
                if (Math.Abs(Vector2.Dot(relative, right)) > 8f) continue;
                float along = Vector2.Dot(relative, heading); // negative = behind the point (coming)
                float reach = vehicle.Length / 2f;
                if (along > downstream + reach) continue;
                if (along > -near - reach) return false;
                if (along > -upstream - reach && vehicle.Speed > moving) return false;
            }
            return true;
        }

        public bool IsAreaClear(Footprint area, float gap)
        {
            foreach (AmbientVehicle vehicle in vehicles)
                if (Vector2.DistanceSquared(vehicle.Position, area.Centre) < 80f * 80f && Overlaps(area, vehicle.Footprint, gap))
                    return false;
            return true;
        }

        // ------------------------------------------------------------------ geometry (no allocations)

        /// <summary>Same test as <see cref="Footprint.Overlaps(Footprint, float)"/>, without allocating.</summary>
        internal static bool Overlaps(in Footprint a, in Footprint b, float gap)
        {
            float grow = gap / 2f;
            return !Apart(a, b, a.Forward, grow) && !Apart(a, b, a.Right, grow)
                   && !Apart(a, b, b.Forward, grow) && !Apart(a, b, b.Right, grow);
        }

        private static bool Apart(in Footprint a, in Footprint b, Vector2 axis, float grow) =>
            Math.Abs(Vector2.Dot(b.Centre - a.Centre, axis)) >= Extent(a, axis, grow) + Extent(b, axis, grow);

        /// <summary>Gap between two rectangles (negative when they overlap).</summary>
        internal static float Separation(in Footprint a, in Footprint b)
        {
            float best = Gap(a, b, a.Forward);
            best = Math.Max(best, Gap(a, b, a.Right));
            best = Math.Max(best, Gap(a, b, b.Forward));
            return Math.Max(best, Gap(a, b, b.Right));
        }

        private static float Gap(in Footprint a, in Footprint b, Vector2 axis) =>
            Math.Abs(Vector2.Dot(b.Centre - a.Centre, axis)) - Extent(a, axis, 0f) - Extent(b, axis, 0f);

        private static float Extent(in Footprint f, Vector2 axis, float grow) =>
            (f.HalfLength + grow) * Math.Abs(Vector2.Dot(f.Forward, axis)) + (f.HalfWidth + grow) * Math.Abs(Vector2.Dot(f.Right, axis));

        private static float Radius2(in Footprint f) => f.HalfLength + f.HalfWidth;
    }
}
