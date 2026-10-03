namespace NovaMarket.Core
{
    /// <summary>
    /// Project-wide scale convention: 1 Unity unit = 1 meter.
    /// Reference these constants instead of hard-coding conversion factors.
    /// </summary>
    public static class WorldScale
    {
        public const float MetersPerUnit = 1f;
        public const float UnitsPerMeter = 1f / MetersPerUnit;

        public static float MetersToUnits(float meters) => meters * UnitsPerMeter;
        public static float UnitsToMeters(float units) => units * MetersPerUnit;
    }
}
