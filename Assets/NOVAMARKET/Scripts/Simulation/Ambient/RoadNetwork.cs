using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    public enum TurnKind
    {
        Straight,
        Right,
        Left,
        UTurn
    }

    /// <summary>A piece of road a vehicle drives along: a lane between crossings or a movement through one.</summary>
    public abstract class TrafficLink
    {
        internal readonly List<TrafficLink> next = new List<TrafficLink>();

        /// <summary>Vehicles whose front is on this link (kept by <see cref="AmbientTraffic"/>).</summary>
        internal readonly List<AmbientVehicle> Occupants = new List<AmbientVehicle>();

        protected TrafficLink(int id, Polyline path, float speedLimit)
        {
            Id = id;
            Path = path;
            SpeedLimit = speedLimit;
        }

        public int Id { get; }
        public Polyline Path { get; }
        public float Length => Path.Length;

        /// <summary>Meters per second.</summary>
        public float SpeedLimit { get; }

        /// <summary>Where a vehicle can continue from the end of this link.</summary>
        public IReadOnlyList<TrafficLink> Next => next;
    }

    /// <summary>
    /// One direction of a road between two crossings: a straight line from just after one crossing to
    /// the stop line of the next. It knows how its crossing treats it (lights, priority or "PARE").
    /// </summary>
    public sealed class TrafficLane : TrafficLink
    {
        internal readonly List<float> busStops = new List<float>();
        internal readonly List<(float Start, float End)> keepClear = new List<(float, float)>();

        internal TrafficLane(int id, Polyline path, float speedLimit, int road, RoadDefinition definition)
            : base(id, path, speedLimit)
        {
            Road = road;
            Class = definition.Class;
            HalfWidth = definition.HalfWidth;
            TwoWay = definition.TwoWay;
            Direction = Vector2.Normalize(path.End - path.Start);
            Right = new Vector2(Direction.Y, -Direction.X);
        }

        public int Road { get; }
        public RoadClass Class { get; }
        public float HalfWidth { get; }
        public bool TwoWay { get; }
        public Vector2 Direction { get; }

        /// <summary>Right-hand side of the direction of travel.</summary>
        public Vector2 Right { get; }

        public Vector2 Start => Path.Start;
        public Vector2 End => Path.End;

        /// <summary>The other direction of a two-way road (null on one-way roads).</summary>
        public TrafficLane Opposite { get; internal set; }

        /// <summary>The crossing at the end of the lane (null where the road leaves the map).</summary>
        public TrafficJunction EndJunction { get; internal set; }

        /// <summary>The crossing at the start of the lane (null where the road enters the map).</summary>
        public TrafficJunction StartJunction { get; internal set; }

        /// <summary>True when the lane has priority at its crossing (lights, or the main road).</summary>
        public bool IsMajor { get; internal set; } = true;

        /// <summary>Vehicles stop completely at the end before entering ("PARE").</summary>
        public bool HasStopSign { get; internal set; }

        /// <summary>Light group at a crossing with lights (0 = main road, 1 = cross road).</summary>
        public int SignalGroup { get; internal set; }

        /// <summary>Distances from the start where buses stop.</summary>
        public IReadOnlyList<float> BusStops => busStops;

        /// <summary>Stretches where nobody may stop (e.g. in front of the CEDI gate).</summary>
        public IReadOnlyList<(float Start, float End)> KeepClear => keepClear;

        /// <summary>
        /// Distance from the end of the lane to its stop line: a zebra crossing just before the crossing pushes
        /// the line back (vehicles wait before it, so people cross in front of them). 0 without one.
        /// </summary>
        public float StopLineBack { get; internal set; }

        public bool HasSignal => EndJunction?.Signal != null;

        /// <summary>Distance along the lane of the projection of <paramref name="point"/> and its offset to the right.</summary>
        public float Project(Vector2 point, out float lateral)
        {
            Vector2 d = point - Start;
            lateral = Vector2.Dot(d, Right);
            return Vector2.Dot(d, Direction);
        }

        public Vector2 PointAt(float distance) => Start + Direction * distance;
    }

    /// <summary>A movement through a crossing: from the end of one lane to the start of another.</summary>
    public sealed class TrafficConnector : TrafficLink
    {
        internal readonly List<TrafficConnector> conflicts = new List<TrafficConnector>();

        internal TrafficConnector(int id, Polyline path, float speedLimit, TrafficLane from, TrafficLane to,
            TurnKind turn, TrafficJunction junction)
            : base(id, path, speedLimit)
        {
            From = from;
            To = to;
            Turn = turn;
            Junction = junction;
        }

        public TrafficLane From { get; }
        public TrafficLane To { get; }
        public TurnKind Turn { get; }
        public TrafficJunction Junction { get; }

        /// <summary>Lower goes first: a movement yields to conflicting ones with a lower number.</summary>
        public int Priority { get; internal set; }

        /// <summary>Movements whose paths cross or merge with this one.</summary>
        public IReadOnlyList<TrafficConnector> Conflicts => conflicts;
    }

    /// <summary>A crossing: its movements, its lights (if any) and who is crossing it now.</summary>
    public sealed class TrafficJunction
    {
        internal readonly List<TrafficConnector> movements = new List<TrafficConnector>();
        internal readonly List<TrafficLane> incoming = new List<TrafficLane>();

        /// <summary>Vehicles that have taken a movement and not yet left it.</summary>
        internal readonly List<AmbientVehicle> Holders = new List<AmbientVehicle>();

        internal TrafficJunction(int id, Vector2 position, TrafficSignal signal)
        {
            Id = id;
            Position = position;
            Signal = signal;
        }

        public int Id { get; }
        public Vector2 Position { get; }

        /// <summary>Lights (null at crossings ruled by priority).</summary>
        public TrafficSignal Signal { get; }

        public IReadOnlyList<TrafficConnector> Movements => movements;
        public IReadOnlyList<TrafficLane> Incoming => incoming;
    }

    /// <summary>
    /// Lanes, movements and crossings built from a <see cref="RoadNetworkDefinition"/>. Lanes stop
    /// short of each crossing (the kerb of the cross road plus a margin); movements are smooth curves
    /// between them. Plain C#, no Unity.
    /// </summary>
    public sealed class RoadNetwork
    {
        private const float GridCell = 50f;
        private const float StopMargin = 1.5f;
        private const float ConflictDistance = 4.5f;

        private readonly List<TrafficLane> lanes = new List<TrafficLane>();
        private readonly List<TrafficConnector> connectors = new List<TrafficConnector>();
        private readonly List<TrafficJunction> junctions = new List<TrafficJunction>();
        private readonly Dictionary<(int, int), List<TrafficLane>> grid = new Dictionary<(int, int), List<TrafficLane>>();
        private readonly List<TrafficLane> searchBuffer = new List<TrafficLane>();

        private RoadNetwork() { }

        public IReadOnlyList<TrafficLane> Lanes => lanes;
        public IReadOnlyList<TrafficConnector> Connectors => connectors;
        public IReadOnlyList<TrafficJunction> Junctions => junctions;

        /// <summary>Builds the network; <paramref name="seed"/> staggers the light cycles.</summary>
        public static RoadNetwork Build(RoadNetworkDefinition definition, int seed = 3)
        {
            var network = new RoadNetwork();
            network.Create(definition, new Random(seed));
            return network;
        }

        /// <summary>Lanes whose area may contain points within <paramref name="radius"/> of <paramref name="point"/>.</summary>
        public void LanesNear(Vector2 point, float radius, List<TrafficLane> into)
        {
            into.Clear();
            int x0 = Cell(point.X - radius), x1 = Cell(point.X + radius);
            int z0 = Cell(point.Y - radius), z1 = Cell(point.Y + radius);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                if (grid.TryGetValue((x, z), out List<TrafficLane> cell))
                    foreach (TrafficLane lane in cell)
                        if (!into.Contains(lane)) into.Add(lane);
        }

        /// <summary>The lane that passes closest to a point in a direction (null when none is within <paramref name="maxLateral"/>).</summary>
        public TrafficLane FindLane(Vector2 point, Vector2 direction, float maxLateral = 2.5f) =>
            FindLane(point, direction, maxLateral, out _);

        public TrafficLane FindLane(Vector2 point, Vector2 direction, float maxLateral, out float distance)
        {
            List<TrafficLane> near = searchBuffer;
            LanesNear(point, maxLateral + 1f, near);
            TrafficLane best = null;
            float bestLateral = maxLateral;
            distance = 0f;
            foreach (TrafficLane lane in near)
            {
                if (Vector2.Dot(lane.Direction, direction) < 0.8f) continue;
                float s = lane.Project(point, out float lateral);
                if (s < -0.5f || s > lane.Length + 0.5f || Math.Abs(lateral) > bestLateral) continue;
                best = lane;
                bestLateral = Math.Abs(lateral);
                distance = Math.Max(0f, Math.Min(lane.Length, s));
            }
            return best;
        }

        // ------------------------------------------------------------------ building

        private struct Incident
        {
            public int Road;
            public Vector2 Out; // unit direction from the node along the road
            public float HalfWidth; // how far its asphalt reaches from the node line
        }

        private void Create(RoadNetworkDefinition definition, Random random)
        {
            int nodeCount = definition.Nodes.Count;
            var incidents = new List<Incident>[nodeCount];
            for (int i = 0; i < nodeCount; i++) incidents[i] = new List<Incident>();
            for (int r = 0; r < definition.Roads.Count; r++)
            {
                RoadDefinition road = definition.Roads[r];
                Vector2 d = Vector2.Normalize(definition.Nodes[road.To].Position - definition.Nodes[road.From].Position);
                float reach = road.HalfWidth + Math.Abs(road.CentreShift); // a shifted half street reaches further from its node
                incidents[road.From].Add(new Incident { Road = r, Out = d, HalfWidth = reach });
                incidents[road.To].Add(new Incident { Road = r, Out = -d, HalfWidth = reach });
            }

            // Lanes, trimmed at both ends so they stop before the crossing.
            var forward = new TrafficLane[definition.Roads.Count];
            var backward = new TrafficLane[definition.Roads.Count];
            for (int r = 0; r < definition.Roads.Count; r++)
            {
                RoadDefinition road = definition.Roads[r];
                Vector2 a = definition.Nodes[road.From].Position, b = definition.Nodes[road.To].Position;
                Vector2 dir = Vector2.Normalize(b - a);
                var right = new Vector2(dir.Y, -dir.X);
                float trimA = Trim(definition, incidents[road.From], r, road.From);
                float trimB = Trim(definition, incidents[road.To], r, road.To);
                if (Vector2.Distance(a, b) - trimA - trimB < 4f)
                    throw new InvalidOperationException($"Road {r} is too short for its crossings.");

                Vector2 shift = right * road.CentreShift;
                Vector2 start = a + dir * trimA + shift, end = b - dir * trimB + shift;
                forward[r] = AddLane(new Polyline(start + right * road.LaneOffset, end + right * road.LaneOffset), road, r);
                if (!road.TwoWay) continue;
                backward[r] = AddLane(new Polyline(end - right * road.LaneOffset, start - right * road.LaneOffset), road, r);
                forward[r].Opposite = backward[r];
                backward[r].Opposite = forward[r];
            }

            // Crossings and their movements.
            for (int n = 0; n < nodeCount; n++)
            {
                RoadNodeDefinition node = definition.Nodes[n];
                if (node.Kind == RoadNodeKind.Boundary || incidents[n].Count == 0) continue;
                TrafficSignal signal = node.Signalized
                    ? new TrafficSignal(30f, 20f, offset: (float)random.NextDouble() * 50f)
                    : null;
                var junction = new TrafficJunction(junctions.Count, node.Position, signal);
                junctions.Add(junction);

                var into = new List<TrafficLane>();
                var outOf = new List<TrafficLane>();
                foreach (Incident incident in incidents[n])
                {
                    RoadDefinition road = definition.Roads[incident.Road];
                    bool startsHere = road.From == n;
                    TrafficLane leaving = startsHere ? forward[incident.Road] : backward[incident.Road];
                    TrafficLane arriving = startsHere ? backward[incident.Road] : forward[incident.Road];
                    if (leaving != null) outOf.Add(leaving);
                    if (arriving != null) into.Add(arriving);
                }

                bool deadEnd = incidents[n].Count == 1;
                foreach (TrafficLane from in into)
                {
                    from.EndJunction = junction;
                    junction.incoming.Add(from);
                    foreach (TrafficLane to in outOf)
                    {
                        if (to.Road == from.Road && !deadEnd) continue;
                        TrafficConnector connector = AddConnector(from, to, junction);
                        to.StartJunction = junction;
                        from.next.Add(connector);
                        connector.next.Add(to);
                    }
                }
                ClassifyApproaches(definition, junction, incidents[n].Count);
                FindConflicts(junction);
            }

            foreach (LaneMarkerDefinition stop in definition.BusStops)
            {
                TrafficLane lane = FindLane(stop.Point, stop.Direction, 4f, out float s);
                if (lane != null && s > 12f && s < lane.Length - 12f) lane.busStops.Add(s);
            }
            foreach (LaneMarkerDefinition zone in definition.KeepClear)
            {
                TrafficLane lane = FindLane(zone.Point, zone.Direction, 4f, out float s);
                if (lane != null) lane.keepClear.Add((Math.Max(0f, s - zone.HalfLength), Math.Min(lane.Length, s + zone.HalfLength)));
            }
            foreach (LaneMarkerDefinition crosswalk in definition.Crosswalks)
            foreach (Vector2 direction in new[] { crosswalk.Direction, -crosswalk.Direction })
            {
                TrafficLane lane = FindLane(crosswalk.Point, direction, 4f, out float s);
                if (lane == null || s < lane.Length / 2f) continue; // only the lane arriving at it stops before it
                lane.StopLineBack = Math.Max(lane.StopLineBack, lane.Length - (s - crosswalk.HalfLength) + 0.3f);
            }
            foreach (TrafficLane lane in lanes) lane.busStops.Sort();
        }

        /// <summary>How far from a node a road's lanes stop: the kerb of the widest other road plus a margin.</summary>
        private static float Trim(RoadNetworkDefinition definition, List<Incident> incidents, int road, int node)
        {
            if (definition.Nodes[node].Kind == RoadNodeKind.Boundary) return 0f;
            if (incidents.Count == 1) return 4f;
            float widest = 0f;
            Vector2 mine = default;
            foreach (Incident incident in incidents)
                if (incident.Road == road) mine = incident.Out;
            bool straightThrough = incidents.Count == 2;
            foreach (Incident incident in incidents)
            {
                if (incident.Road == road) continue;
                widest = Math.Max(widest, incident.HalfWidth);
                if (Vector2.Dot(incident.Out, mine) > -0.95f) straightThrough = false;
            }
            return straightThrough ? 1f : widest + StopMargin;
        }

        private TrafficLane AddLane(Polyline path, RoadDefinition road, int index)
        {
            var lane = new TrafficLane(lanes.Count, path, road.SpeedLimit, index, road);
            lanes.Add(lane);
            float pad = road.HalfWidth + 2f;
            int x0 = Cell(Math.Min(path.Start.X, path.End.X) - pad), x1 = Cell(Math.Max(path.Start.X, path.End.X) + pad);
            int z0 = Cell(Math.Min(path.Start.Y, path.End.Y) - pad), z1 = Cell(Math.Max(path.Start.Y, path.End.Y) + pad);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!grid.TryGetValue((x, z), out List<TrafficLane> cell)) grid[(x, z)] = cell = new List<TrafficLane>();
                cell.Add(lane);
            }
            return lane;
        }

        private TrafficConnector AddConnector(TrafficLane from, TrafficLane to, TrafficJunction junction)
        {
            Vector2 a = from.End, b = to.Start;
            float dot = Vector2.Dot(from.Direction, to.Direction);
            float cross = from.Direction.X * to.Direction.Y - from.Direction.Y * to.Direction.X;
            TurnKind turn = dot > 0.7f ? TurnKind.Straight : dot < -0.7f ? TurnKind.UTurn : cross > 0f ? TurnKind.Left : TurnKind.Right;

            Polyline path;
            if (turn == TurnKind.Straight && Math.Abs(Vector2.Dot(b - a, from.Right)) < 0.3f)
                path = new Polyline(a, b);
            else
            {
                float handle = turn == TurnKind.UTurn ? 5f : Math.Max(1.5f, Vector2.Distance(a, b) * 0.55f);
                Vector2 p1 = a + from.Direction * handle, p2 = b - to.Direction * handle;
                var points = new Vector2[11];
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f, u = 1f - t;
                    points[i] = u * u * u * a + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b;
                }
                path = new Polyline(points);
            }

            float speed = Math.Min(from.SpeedLimit, to.SpeedLimit);
            speed = turn switch
            {
                TurnKind.Right => Math.Min(speed, 5.5f),
                TurnKind.Left => Math.Min(speed, 7f),
                TurnKind.UTurn => Math.Min(speed, 3.5f),
                _ => speed
            };
            var connector = new TrafficConnector(lanes.Count + connectors.Count + 100000, path, speed, from, to, turn, junction);
            connectors.Add(connector);
            junction.movements.Add(connector);
            return connector;
        }

        /// <summary>
        /// Lights: group 0 is the axis of the main road. Without lights the highest road class (east-west on
        /// a tie) has priority and the others have "PARE". Bends and dead ends have no rules.
        /// </summary>
        private static void ClassifyApproaches(RoadNetworkDefinition definition, TrafficJunction junction, int degree)
        {
            RoadClass top = RoadClass.Street;
            foreach (TrafficLane lane in junction.incoming)
                if (lane.Class > top) top = lane.Class;
            foreach (TrafficConnector movement in junction.movements)
                if (movement.To.Class > top) top = movement.To.Class;

            bool HorizontalHasTop() // the east-west axis has a road of the top class
            {
                foreach (TrafficLane lane in junction.incoming)
                    if (lane.Class == top && IsHorizontal(lane.Direction)) return true;
                foreach (TrafficConnector movement in junction.movements)
                    if (movement.To.Class == top && IsHorizontal(movement.To.Direction)) return true;
                return false;
            }
            bool mainIsHorizontal = HorizontalHasTop();

            foreach (TrafficLane lane in junction.incoming)
            {
                bool onMainAxis = IsHorizontal(lane.Direction) == mainIsHorizontal;
                if (junction.Signal != null)
                {
                    lane.SignalGroup = onMainAxis ? 0 : 1;
                    lane.IsMajor = true;
                }
                else if (degree <= 2) lane.IsMajor = true;
                else
                {
                    lane.IsMajor = lane.Class == top && onMainAxis;
                    lane.HasStopSign = !lane.IsMajor;
                }
            }

            foreach (TrafficConnector movement in junction.movements)
            {
                bool turnsAcross = movement.Turn == TurnKind.Left || movement.Turn == TurnKind.UTurn;
                movement.Priority = junction.Signal != null || movement.From.IsMajor
                    ? turnsAcross ? 1 : 0
                    : turnsAcross ? 3 : 2;
            }
        }

        private static bool IsHorizontal(Vector2 direction) => Math.Abs(direction.X) >= Math.Abs(direction.Y);

        /// <summary>Two movements conflict when they come from different lanes and their paths cross, touch or merge.</summary>
        private static void FindConflicts(TrafficJunction junction)
        {
            var samples = new List<Vector2[]>();
            foreach (TrafficConnector movement in junction.movements)
            {
                int count = Math.Max(2, (int)Math.Ceiling(movement.Length / 0.75f) + 1);
                var points = new Vector2[count];
                for (int i = 0; i < count; i++) points[i] = movement.Path.PointAt(movement.Length * i / (count - 1));
                samples.Add(points);
            }

            for (int i = 0; i < junction.movements.Count; i++)
            for (int j = i + 1; j < junction.movements.Count; j++)
            {
                TrafficConnector a = junction.movements[i], b = junction.movements[j];
                if (a.From == b.From) continue;
                bool conflict = a.To == b.To || Closest(samples[i], samples[j]) < ConflictDistance;
                if (!conflict) continue;
                a.conflicts.Add(b);
                b.conflicts.Add(a);
            }
        }

        private static float Closest(Vector2[] a, Vector2[] b)
        {
            float best = float.MaxValue;
            foreach (Vector2 p in a)
            foreach (Vector2 q in b)
                best = Math.Min(best, Vector2.DistanceSquared(p, q));
            return (float)Math.Sqrt(best);
        }

        private static int Cell(float coordinate) => (int)Math.Floor(coordinate / GridCell);
    }
}
