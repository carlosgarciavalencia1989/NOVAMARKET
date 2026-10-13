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
        public float ForkliftSpeed { get; set; } = 4.5f;

        /// <summary>Space between queued trucks.</summary>
        public float QueueGap { get; set; } = 4f;

        public float GateCheckSeconds { get; set; } = 4f;
        public float ExitCheckSeconds { get; set; } = 1.5f;
        public float UnloadSecondsPerPallet { get; set; } = 1.2f;
        public float LoadSecondsPerPallet { get; set; } = 1.4f;
        public float ForkHandlingSeconds { get; set; } = 1f;

        /// <summary>Pallets that fit in one staging lane next to a dock.</summary>
        public int StagingCapacity { get; set; } = 8;

        public int ForkliftCount { get; set; } = 4;
        public int MaxTrucksAtGate { get; set; } = 3;

        public float FirstReceivingAt { get; set; } = 2f;
        public float FirstDispatchAt { get; set; } = 14f;
        public float ReceivingInterval { get; set; } = 50f;
        public float DispatchInterval { get; set; } = 38f;

        public int MinPalletsPerTruck { get; set; } = 8;
        public int MaxPalletsPerTruck { get; set; } = 12;
        public int MinPalletsPerDispatch { get; set; } = 4;
        public int MaxPalletsPerDispatch { get; set; } = 7;

        /// <summary>Points for each completed dispatch.</summary>
        public int PointsPerDispatch { get; set; } = 150;

        /// <summary>
        /// True: trucks arrive by themselves every few seconds (free operation). False: only the
        /// trucks scheduled with ScheduleReceiving / ScheduleDispatch come (planning level).
        /// </summary>
        public bool SpawnRandomTrucks { get; set; } = true;

        /// <summary>
        /// True: put-away and picking change the occupied positions of the HUD. False: another
        /// system (the planning level) owns the positions and the forklifts only animate.
        /// </summary>
        public bool TrackPositions { get; set; } = true;

        /// <summary>True: each turbo that leaves counts as a dispatch of the mission counter.</summary>
        public bool CountDispatches { get; set; } = true;

        /// <summary>Seconds between two scheduled trucks entering the highway.</summary>
        public float ScheduledTruckSpacing { get; set; } = 7f;

        /// <summary>
        /// Most scheduled trucks waiting to set off. When the player closes weeks faster than the
        /// yard can work, the oldest waiting truck is dropped (the plan already counted its boxes).
        /// </summary>
        public int MaxScheduledTrucks { get; set; } = 12;

        /// <summary>Settings for the planning level: scheduled trucks, positions owned by the plan.</summary>
        /// <summary>Planner campaign: trucks keep the CEDI alive in the background, but no level counts dispatches.</summary>
        public static CediOperationSettings ForCampaign() => new CediOperationSettings { CountDispatches = false };

        public static CediOperationSettings ForPlanning() => new CediOperationSettings
        {
            SpawnRandomTrucks = false,
            TrackPositions = false,
            CountDispatches = false
        };
    }
}
