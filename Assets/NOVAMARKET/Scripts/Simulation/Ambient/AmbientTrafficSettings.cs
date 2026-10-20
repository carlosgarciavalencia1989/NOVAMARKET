namespace NovaMarket.Simulation.Ambient
{
    /// <summary>How much city traffic there is and of which kinds. Plain C#; the data asset fills it per quality level.</summary>
    public sealed class AmbientTrafficSettings
    {
        /// <summary>Vehicles kept alive around the camera.</summary>
        public int VehicleCount { get; set; } = 120;

        /// <summary>Share of motos among the vehicles (Medellín: 30–40 %).</summary>
        public float MotoShare { get; set; } = 0.35f;

        public float BusShare { get; set; } = 0.06f;
        public float TruckShare { get; set; } = 0.09f;

        /// <summary>Share of yellow taxis among the cars.</summary>
        public float TaxiShareOfCars { get; set; } = 0.3f;

        /// <summary>Most vehicles created in one tick (spreads the work when the camera jumps).</summary>
        public int MaxSpawnsPerTick { get; set; } = 6;

        /// <summary>Seconds a bus stands at a stop.</summary>
        public float BusDwellSeconds { get; set; } = 12f;

        /// <summary>
        /// A vehicle stuck this long out of the camera's close view is removed and replaced (safety
        /// valve; the tests check it is never needed).
        /// </summary>
        public float StuckRemovalSeconds { get; set; } = 120f;

        /// <summary>The highway counts this many times its length when choosing where a vehicle appears.</summary>
        public float HighwaySpawnWeight { get; set; } = 2f;

        public static int VariantCount => 8;
    }
}
