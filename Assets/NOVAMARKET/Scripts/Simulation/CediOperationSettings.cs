namespace NovaMarket.Simulation
{
    /// <summary>
    /// Tuning of the CEDI operation: vehicle sizes and speeds (m, m/s), handling times (s) and
    /// how often trucks arrive. Times are compressed so something happens every few seconds.
    /// </summary>
    public sealed class CediOperationSettings
    {
        public float TruckLength { get; set; } = 16f;
        public float TurboLength { get; set; } = 9f;
        public float ForkliftLength { get; set; } = 2.8f;

        public float HighwaySpeed { get; set; } = 13f;
        public float YardSpeed { get; set; } = 5f;
        public float ReverseSpeed { get; set; } = 2.2f;
        public float ForkliftSpeed { get; set; } = 3.5f;

        /// <summary>Space between queued trucks.</summary>
        public float QueueGap { get; set; } = 4f;

        public float GateCheckSeconds { get; set; } = 4f;
        public float ExitCheckSeconds { get; set; } = 1.5f;
        public float UnloadSecondsPerPallet { get; set; } = 1.2f;
        public float LoadSecondsPerPallet { get; set; } = 1.4f;
        public float ForkHandlingSeconds { get; set; } = 1.5f;

        /// <summary>Pallets that fit in one staging lane next to a dock.</summary>
        public int StagingCapacity { get; set; } = 8;

        public int ForkliftCount { get; set; } = 3;
        public int MaxTrucksAtGate { get; set; } = 3;

        public float FirstReceivingAt { get; set; } = 2f;
        public float FirstDispatchAt { get; set; } = 14f;
        public float ReceivingInterval { get; set; } = 40f;
        public float DispatchInterval { get; set; } = 32f;

        public int MinPalletsPerTruck { get; set; } = 8;
        public int MaxPalletsPerTruck { get; set; } = 12;
        public int MinPalletsPerDispatch { get; set; } = 4;
        public int MaxPalletsPerDispatch { get; set; } = 7;

        /// <summary>Points for each completed dispatch.</summary>
        public int PointsPerDispatch { get; set; } = 150;
    }
}
