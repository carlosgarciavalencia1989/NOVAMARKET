using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>Road hierarchy: at a crossing without lights the higher class has priority.</summary>
    public enum RoadClass
    {
        Street = 1,
        Avenue = 2,
        Highway = 3
    }

    public enum RoadNodeKind
    {
        /// <summary>Crossing, corner or bend.</summary>
        Junction,

        /// <summary>Where a road leaves the map (the highway ends): vehicles vanish there.</summary>
        Boundary
    }

    /// <summary>A point where roads meet. Positions are world X/Z in meters.</summary>
    public readonly struct RoadNodeDefinition
    {
        public RoadNodeDefinition(Vector2 position, RoadNodeKind kind, bool signalized)
        {
            Position = position;
            Kind = kind;
            Signalized = signalized;
        }

        public Vector2 Position { get; }
        public RoadNodeKind Kind { get; }

        /// <summary>Traffic lights instead of priority and "PARE" signs.</summary>
        public bool Signalized { get; }
    }

    /// <summary>
    /// A straight road between two nodes. Two-way roads have one lane per direction, each
    /// <see cref="LaneOffset"/> to the right of the centre line (right-hand traffic); one-way roads
    /// have a single lane on the centre line, driven from <see cref="From"/> to <see cref="To"/>.
    /// </summary>
    public readonly struct RoadDefinition
    {
        public RoadDefinition(int from, int to, RoadClass roadClass, bool twoWay, float halfWidth, float speedLimit,
            float centreShift = 0f)
        {
            From = from;
            To = to;
            Class = roadClass;
            TwoWay = twoWay;
            HalfWidth = halfWidth;
            SpeedLimit = speedLimit;
            CentreShift = centreShift;
        }

        public int From { get; }
        public int To { get; }
        public RoadClass Class { get; }
        public bool TwoWay { get; }

        /// <summary>Half the paved width (centre line to kerb).</summary>
        public float HalfWidth { get; }

        /// <summary>Meters per second.</summary>
        public float SpeedLimit { get; }

        /// <summary>
        /// Lateral shift of the centre line to the right of From → To: a half street (6 m of asphalt
        /// on one side of a grid line) keeps its node on the grid line and its lane on its asphalt.
        /// </summary>
        public float CentreShift { get; }

        /// <summary>Lane centre from the road centre line: half of the half width on two-way roads.</summary>
        public float LaneOffset => TwoWay ? HalfWidth / 2f : 0f;
    }

    /// <summary>
    /// A place on a lane, given as a world point and the direction of travel: the lane that passes
    /// closest to it in that direction gets it (bus stops, "do not block" boxes).
    /// </summary>
    public readonly struct LaneMarkerDefinition
    {
        public LaneMarkerDefinition(Vector2 point, Vector2 direction, float halfLength = 0f)
        {
            Point = point;
            Direction = Vector2.Normalize(direction);
            HalfLength = halfLength;
        }

        public Vector2 Point { get; }
        public Vector2 Direction { get; }

        /// <summary>Extent along the lane on each side of the point (0 for a single point).</summary>
        public float HalfLength { get; }
    }

    /// <summary>Plain description of a road network, built by the environment exporter or by tests.</summary>
    public sealed class RoadNetworkDefinition
    {
        public List<RoadNodeDefinition> Nodes { get; } = new List<RoadNodeDefinition>();
        public List<RoadDefinition> Roads { get; } = new List<RoadDefinition>();
        public List<LaneMarkerDefinition> BusStops { get; } = new List<LaneMarkerDefinition>();
        public List<LaneMarkerDefinition> KeepClear { get; } = new List<LaneMarkerDefinition>();

        public int AddNode(Vector2 position, RoadNodeKind kind = RoadNodeKind.Junction, bool signalized = false)
        {
            Nodes.Add(new RoadNodeDefinition(position, kind, signalized));
            return Nodes.Count - 1;
        }

        public void AddRoad(int from, int to, RoadClass roadClass, bool twoWay, float halfWidth, float speedLimit,
            float centreShift = 0f) =>
            Roads.Add(new RoadDefinition(from, to, roadClass, twoWay, halfWidth, speedLimit, centreShift));
    }
}
