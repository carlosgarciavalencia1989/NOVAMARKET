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

        /// <summary>Short Spanish status for the selection panel.</summary>
        public abstract string Status { get; }

        /// <summary>True once the vehicle has left the scenario and can be removed.</summary>
        public abstract bool IsFinished { get; }

        protected RouteMover Mover { get; }

        internal abstract void Tick(CediOperationSimulation simulation, float deltaTime);

        public abstract SelectionDetails Describe();
    }
}
