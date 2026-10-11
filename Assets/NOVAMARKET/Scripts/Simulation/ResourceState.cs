using System;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// Operational resources shown in the HUD resource bar: storage positions, budget, trucks in
    /// the yard, operators on shift and service level. Raises <see cref="Changed"/> on every update.
    /// </summary>
    public sealed class ResourceState
    {
        public ResourceState(int occupiedPositions, int positionCapacity, long budget, int trucksInYard,
            int operators, double serviceLevel, double serviceLevelTarget)
        {
            if (positionCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(positionCapacity), positionCapacity, "Capacity must be at least 1.");
            PositionCapacity = positionCapacity;
            ServiceLevelTarget = serviceLevelTarget;
            OccupiedPositions = Clamp(occupiedPositions, 0, positionCapacity);
            Budget = budget;
            TrucksInYard = Math.Max(0, trucksInYard);
            Operators = Math.Max(0, operators);
            ServiceLevel = Clamp01(serviceLevel);
        }

        public event Action Changed;

        /// <summary>Rack positions in use (pallet positions).</summary>
        public int OccupiedPositions { get; private set; }
        public int PositionCapacity { get; }

        /// <summary>Available budget in Colombian pesos.</summary>
        public long Budget { get; private set; }

        public int TrucksInYard { get; private set; }
        public int Operators { get; private set; }

        /// <summary>Fraction of orders delivered complete and on time (0..1).</summary>
        public double ServiceLevel { get; private set; }
        public double ServiceLevelTarget { get; }

        public double Occupancy => (double)OccupiedPositions / PositionCapacity;

        /// <summary>Above 90 % occupancy the warehouse starts losing flexibility.</summary>
        public bool IsStorageCritical => Occupancy >= 0.9;

        public bool IsServiceBelowTarget => ServiceLevel < ServiceLevelTarget;

        public void SetOccupiedPositions(int value) => Set(() => OccupiedPositions = Clamp(value, 0, PositionCapacity));
        public void SetBudget(long value) => Set(() => Budget = value);
        public void SetTrucksInYard(int value) => Set(() => TrucksInYard = Math.Max(0, value));
        public void SetOperators(int value) => Set(() => Operators = Math.Max(0, value));
        public void SetServiceLevel(double value) => Set(() => ServiceLevel = Clamp01(value));

        private void Set(Action apply)
        {
            apply();
            Changed?.Invoke();
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
    }
}
