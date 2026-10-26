using System.Numerics;

namespace NovaMarket.Simulation
{
    public enum VehicleKind
    {
        /// <summary>Tractor-trailer that brings goods to a receiving dock.</summary>
        ReceivingTruck,

        /// <summary>"Turbo": medium rigid truck that takes orders from a shipping dock.</summary>
        DispatchTruck,

        Forklift
    }

    /// <summary>
    /// Anything that moves in the CEDI operation. Holds its pose for the scene and describes
    /// itself for the selection panel; the subclasses implement the behaviour.
    /// </summary>
    public abstract class VehicleAgent
    {
        protected VehicleAgent(int id, VehicleKind kind, string plate, float length, RouteMover mover, float floorHeight)
        {
            Id = id;
            Kind = kind;
            Plate = plate;
            Length = length;
            Mover = mover;
            FloorHeight = floorHeight;
        }

        public int Id { get; }
        public VehicleKind Kind { get; }
        public string Plate { get; }

        /// <summary>Bumper-to-bumper length in meters.</summary>
        public float Length { get; }

        /// <summary>Height of the surface it drives on (0 outside, the dock floor inside).</summary>
        public float FloorHeight { get; }

        public Vector2 Position => Mover.Position;
        public Vector2 Heading => Mover.Heading;
        public float Speed => Mover.Speed;

        /// <summary>Moving backwards (into or out of a dock): the reverse beeper sounds.</summary>
        public bool IsReversing => Mover.IsReversing && Mover.Speed > 0.05f;

        /// <summary>Body width in meters.</summary>
        public float Width => Kind switch
        {
            VehicleKind.ReceivingTruck => 2.6f,
            VehicleKind.DispatchTruck => 2.5f,
            _ => 1.5f
        };

        /// <summary>Ground rectangle the vehicle covers now (forklifts include the forks in front).</summary>
        public Footprint Footprint => FootprintAt(Position, Heading);

        internal Footprint FootprintAt(Vector2 position, Vector2 heading) => Kind == VehicleKind.Forklift
            ? new Footprint(position + heading * ForkOverhang / 2f, heading, Length + ForkOverhang, Width)
            : new Footprint(position, heading, Length, Width);

        internal bool IsBackingOut => Mover.IsReversing;

        /// <summary>
        /// Where a forklift that is backing out of a spot will end up (on the aisle): the others keep that
        /// place free, so it can always get out. Null when it is not backing out.
        /// </summary>
        internal Footprint? BackingOutTarget
        {
            get
            {
                // Only while it is really backing out: one waiting for its turn reserves nothing yet.
                if (Kind != VehicleKind.Forklift || !Mover.IsReversing || PreviousBlocker != null) return null;
                (Vector2 centre, Vector2 heading) = Mover.PoseAhead(Mover.CurrentLeg.Path.Length);
                return FootprintAt(centre, heading);
            }
        }

        /// <summary>How far the forks reach beyond the forklift body.</summary>
        private const float ForkOverhang = 0.5f;

        // Traffic bookkeeping (see CediOperationSimulation.TrafficLimit).
        internal VehicleAgent BlockedBy { get; set; }
        internal VehicleAgent PreviousBlocker { get; set; }
        internal float BlockedSeconds { get; set; }
        internal VehicleAgent GoesFirstPast { get; set; }

        /// <summary>True while the vehicle is stopped because another one is in its way.</summary>
        public bool IsWaitingInTraffic => BlockedBy != null;

        /// <summary>Short Spanish status for the selection panel.</summary>
        public abstract string Status { get; }

        /// <summary>True once the vehicle has left the scenario and can be removed.</summary>
        public abstract bool IsFinished { get; }

        protected RouteMover Mover { get; }

        internal abstract void Tick(CediOperationSimulation simulation, float deltaTime);

        public abstract SelectionDetails Describe();
    }
}
