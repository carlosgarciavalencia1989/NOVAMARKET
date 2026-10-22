using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    public enum PedestrianState
    {
        Walking,

        /// <summary>Stopped for a while (chatting, looking at a phone…).</summary>
        Standing,

        /// <summary>At the kerb, waiting for the lights or a gap in the traffic.</summary>
        WaitingToCross,

        /// <summary>On a zebra crossing.</summary>
        Crossing
    }

    /// <summary>
    /// A person walking in the valley. Holds where it is on the walk network and what it does; the scene
    /// draws it (no Unity objects here).
    /// </summary>
    public sealed class Pedestrian
    {
        /// <summary>Body size used to keep vehicles away while crossing (meters).</summary>
        public const float BodySize = 0.6f;

        internal Pedestrian(int id, int variant, float desiredSpeed, float side)
        {
            Id = id;
            Variant = variant;
            DesiredSpeed = desiredSpeed;
            Side = side;
        }

        public int Id { get; }

        /// <summary>Random number the scene uses to pick a model or colours.</summary>
        public int Variant { get; }

        public PedestrianState State { get; internal set; }

        /// <summary>Ground position (world X/Z) and height of the ground under the feet.</summary>
        public Vector2 Position { get; internal set; }
        public float Height { get; internal set; }

        /// <summary>Unit direction the person faces.</summary>
        public Vector2 Heading { get; internal set; } = new Vector2(1f, 0f);

        /// <summary>Walking speed now (m/s); 0 while standing or waiting.</summary>
        public float Speed { get; internal set; }

        public Footprint Footprint => new Footprint(Position, Heading, BodySize, BodySize);

        internal float DesiredSpeed { get; }

        /// <summary>Distance kept to the right of the path centre.</summary>
        internal float Side { get; }

        internal WalkEdge Edge { get; set; }

        /// <summary>Distance walked along <see cref="Edge"/> in the walking direction.</summary>
        internal float Along { get; set; }

        /// <summary>Walking from A to B (else from B to A).</summary>
        internal bool Forward { get; set; }

        internal WalkEdge CameFrom { get; set; }
        internal float Timer { get; set; }
        internal bool Done { get; set; }

        internal WalkNode Target => Forward ? Edge.B : Edge.A;
        internal WalkNode Start => Forward ? Edge.A : Edge.B;
        internal Vector2 WalkDirection => Forward ? Edge.Direction : -Edge.Direction;

        public override string ToString() => $"Peatón #{Id}";
    }
}
