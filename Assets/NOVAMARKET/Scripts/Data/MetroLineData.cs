using NovaMarket.Simulation.Ambient;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// The valley metro: its geometry (exported by the environment builder, do not edit) and how the trains
    /// run (free to tune in the Inspector; read when Play starts).
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Metro Line", fileName = "MetroLine_")]
    public sealed class MetroLineData : NovaDataAsset
    {
        [Header("Geometry (exported by the environment builder)")]
        [SerializeField] private float westEnd = -1450f;
        [SerializeField] private float eastEnd = 1450f;
        [SerializeField] private float eastboundZ = -85.9f;
        [SerializeField] private float westboundZ = -82.1f;
        [SerializeField] private float railHeight = 11.64f;
        [SerializeField] private float[] stationX = { -330f, 420f };
        [SerializeField] private float platformHalfLength = 60f;

        [Header("Service")]
        [SerializeField, Range(1, 8)] private int trains = 4;
        [SerializeField, Range(1, 6)] private int cars = 3;
        [SerializeField, Tooltip("m/s (20 = 72 km/h)")] private float cruiseSpeed = 20f;
        [SerializeField, Tooltip("Seconds with the doors open at each station.")] private float dwellSeconds = 25f;
        [SerializeField, Tooltip("Seconds at each end of the line before returning.")] private float turnbackSeconds = 30f;

        public MetroLineDefinition ToDefinition() =>
            new MetroLineDefinition(westEnd, eastEnd, eastboundZ, westboundZ, railHeight, stationX, platformHalfLength);

        public MetroSettings ToSettings() => new MetroSettings
        {
            TrainCount = trains,
            Cars = cars,
            CruiseSpeed = cruiseSpeed,
            DwellSeconds = dwellSeconds,
            TurnbackSeconds = turnbackSeconds
        };

        /// <summary>Replaces the geometry (used by the Editor exporter); the service values are kept.</summary>
        public void SetGeometry(MetroLineDefinition definition)
        {
            westEnd = definition.WestEnd;
            eastEnd = definition.EastEnd;
            eastboundZ = definition.EastboundZ;
            westboundZ = definition.WestboundZ;
            railHeight = definition.RailHeight;
            stationX = new float[definition.StationX.Count];
            for (int i = 0; i < stationX.Length; i++) stationX[i] = definition.StationX[i];
            platformHalfLength = definition.PlatformHalfLength;
        }
    }
}
