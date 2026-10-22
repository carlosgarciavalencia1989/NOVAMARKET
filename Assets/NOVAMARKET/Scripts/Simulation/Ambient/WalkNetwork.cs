using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>A point of the walk network where paths meet.</summary>
    public sealed class WalkNode
    {
        internal readonly List<WalkEdge> edges = new List<WalkEdge>();

        internal WalkNode(int id, Vector2 position, float height)
        {
            Id = id;
            Position = position;
            Height = height;
        }

        public int Id { get; }
        public Vector2 Position { get; }
        public float Height { get; }
        public IReadOnlyList<WalkEdge> Edges => edges;
    }

    /// <summary>
    /// A straight walkable stretch. A zebra crossing also knows the lanes it crosses: those arriving at it
    /// (their vehicles stop before it) and those leaving from it, and the lights that rule them, if any.
    /// </summary>
    public sealed class WalkEdge
    {
        internal readonly List<Pedestrian> Occupants = new List<Pedestrian>();
        internal readonly List<TrafficLane> arriving = new List<TrafficLane>();
        internal readonly List<TrafficLane> leaving = new List<TrafficLane>();

        internal WalkEdge(int id, WalkNode a, WalkNode b, WalkEdgeKind kind, float halfWidth, float busyness)
        {
            Id = id;
            A = a;
            B = b;
            Kind = kind;
            HalfWidth = halfWidth;
            Busyness = busyness;
            Length = Vector2.Distance(a.Position, b.Position);
            Direction = Length > 1e-4f ? (b.Position - a.Position) / Length : new Vector2(1f, 0f);
        }

        public int Id { get; }
        public WalkNode A { get; }
        public WalkNode B { get; }
        public WalkEdgeKind Kind { get; }
        public float HalfWidth { get; }
        public float Busyness { get; }
        public float Length { get; }

        /// <summary>Unit direction from A to B.</summary>
        public Vector2 Direction { get; }

        public bool IsCrosswalk => Kind == WalkEdgeKind.Crosswalk;

        /// <summary>Lanes whose vehicles stop before this crossing.</summary>
        public IReadOnlyList<TrafficLane> ArrivingLanes => arriving;

        /// <summary>Lanes that start just before this crossing (vehicles leaving a crossroads drive over it).</summary>
        public IReadOnlyList<TrafficLane> LeavingLanes => leaving;

        /// <summary>The lights of the crossed street (null when it has none).</summary>
        public TrafficSignal Signal { get; internal set; }

        /// <summary>Light group of the crossed street's vehicles.</summary>
        public int CrossedGroup { get; internal set; }

        public WalkNode Other(WalkNode node) => node == A ? B : A;

        public Vector2 PointAt(float distanceFromA) => A.Position + Direction * distanceFromA;

        /// <summary>Ground height at a distance from A: a zebra crossing goes down from the kerbs to the street.</summary>
        public float HeightAt(float distanceFromA)
        {
            if (!IsCrosswalk) return A.Height + (B.Height - A.Height) * (Length > 0f ? distanceFromA / Length : 0f);
            if (distanceFromA < CityWalkLayout.SidewalkInset) return A.Height;
            if (distanceFromA > Length - CityWalkLayout.SidewalkInset) return B.Height;
            return CityWalkLayout.StreetTop;
        }
    }

    /// <summary>The graph people walk on, built from a <see cref="WalkNetworkDefinition"/>. Plain C#.</summary>
    public sealed class WalkNetwork
    {
        private const float GridCell = 50f;

        private readonly List<WalkNode> nodes = new List<WalkNode>();
        private readonly List<WalkEdge> edges = new List<WalkEdge>();
        private readonly Dictionary<(int, int), List<WalkEdge>> grid = new Dictionary<(int, int), List<WalkEdge>>();

        private WalkNetwork() { }

        public IReadOnlyList<WalkNode> Nodes => nodes;
        public IReadOnlyList<WalkEdge> Edges => edges;

        /// <summary>Builds the network; with <paramref name="roads"/> the zebra crossings learn which lanes they cross.</summary>
        public static WalkNetwork Build(WalkNetworkDefinition definition, RoadNetwork roads = null)
        {
            var network = new WalkNetwork();
            foreach (WalkNodeDefinition node in definition.Nodes)
                network.nodes.Add(new WalkNode(network.nodes.Count, node.Position, node.Height));
            foreach (WalkEdgeDefinition e in definition.Edges)
            {
                if (e.A == e.B) continue;
                var edge = new WalkEdge(network.edges.Count, network.nodes[e.A], network.nodes[e.B], e.Kind, e.HalfWidth, e.Busyness);
                network.edges.Add(edge);
                edge.A.edges.Add(edge);
                edge.B.edges.Add(edge);
                network.AddToGrid(edge);
                if (edge.IsCrosswalk && roads != null) LinkCrossing(edge, roads);
            }
            return network;
        }

        private static void LinkCrossing(WalkEdge edge, RoadNetwork roads)
        {
            Vector2 centre = (edge.A.Position + edge.B.Position) / 2f;
            var street = new Vector2(edge.Direction.Y, -edge.Direction.X);
            foreach (Vector2 direction in new[] { street, -street })
            {
                TrafficLane lane = roads.FindLane(centre, direction, 4f, out float s);
                if (lane == null) continue;
                if (s >= lane.Length / 2f)
                {
                    edge.arriving.Add(lane);
                    if (lane.EndJunction?.Signal != null)
                    {
                        edge.Signal = lane.EndJunction.Signal;
                        edge.CrossedGroup = lane.SignalGroup;
                    }
                }
                else edge.leaving.Add(lane);
            }
        }

        /// <summary>Edges that may pass within <paramref name="radius"/> of a point.</summary>
        public void EdgesNear(Vector2 point, float radius, List<WalkEdge> into)
        {
            into.Clear();
            int x0 = Cell(point.X - radius), x1 = Cell(point.X + radius);
            int z0 = Cell(point.Y - radius), z1 = Cell(point.Y + radius);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                if (grid.TryGetValue((x, z), out List<WalkEdge> cell))
                    foreach (WalkEdge edge in cell)
                        if (!into.Contains(edge)) into.Add(edge);
        }

        private void AddToGrid(WalkEdge edge)
        {
            Vector2 a = edge.A.Position, b = edge.B.Position;
            int x0 = Cell(Math.Min(a.X, b.X)), x1 = Cell(Math.Max(a.X, b.X));
            int z0 = Cell(Math.Min(a.Y, b.Y)), z1 = Cell(Math.Max(a.Y, b.Y));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!grid.TryGetValue((x, z), out List<WalkEdge> cell)) grid[(x, z)] = cell = new List<WalkEdge>();
                cell.Add(edge);
            }
        }

        private static int Cell(float coordinate) => (int)Math.Floor(coordinate / GridCell);
    }
}
