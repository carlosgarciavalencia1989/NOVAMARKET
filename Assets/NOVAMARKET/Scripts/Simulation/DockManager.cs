using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    public enum DockKind
    {
        Receiving,
        Shipping
    }

    /// <summary>A loading dock door of the warehouse. At most one truck uses it at a time.</summary>
    public sealed class Dock
    {
        internal Dock(int number, DockKind kind, int laneIndex, float z, float wallX)
        {
            Number = number;
            Kind = kind;
            LaneIndex = laneIndex;
            Z = z;
            WallX = wallX;
        }

        /// <summary>Dock number as painted in the scene (1–3 receiving, 4–6 shipping).</summary>
        public int Number { get; }
        public DockKind Kind { get; }

        /// <summary>0..2 within its kind; also the index of its staging lane.</summary>
        public int LaneIndex { get; }

        /// <summary>World z of the door centre.</summary>
        public float Z { get; }

        /// <summary>World x where a docked truck's rear touches the bumpers.</summary>
        public float WallX { get; }

        public TruckAgent Occupant { get; internal set; }
        public bool IsFree => Occupant == null;

        public Vector2 Door => new Vector2(WallX, Z);
    }

    /// <summary>Assigns free docks to trucks. Each dock holds one truck from reservation to release.</summary>
    public sealed class DockManager
    {
        private readonly List<Dock> docks = new List<Dock>();

        public DockManager()
        {
            for (int i = 0; i < CediLayout.DockZ.Length; i++)
                docks.Add(new Dock(i + 1, DockKind.Receiving, i, CediLayout.DockZ[i], -CediLayout.DockWallX));
            for (int i = 0; i < CediLayout.DockZ.Length; i++)
                docks.Add(new Dock(i + 4, DockKind.Shipping, i, CediLayout.DockZ[i], CediLayout.DockWallX));
        }

        public IReadOnlyList<Dock> Docks => docks;

        /// <summary>Reserves the first free dock of a kind for a truck, or returns null if all are busy.</summary>
        public Dock Reserve(DockKind kind, TruckAgent truck)
        {
            Dock dock = docks.Find(d => d.Kind == kind && d.IsFree);
            if (dock != null) dock.Occupant = truck;
            return dock;
        }

        public void Release(Dock dock) => dock.Occupant = null;

        public int FreeCount(DockKind kind) => docks.FindAll(d => d.Kind == kind && d.IsFree).Count;

        public Dock Get(DockKind kind, int laneIndex) => docks.Find(d => d.Kind == kind && d.LaneIndex == laneIndex);
    }
}
