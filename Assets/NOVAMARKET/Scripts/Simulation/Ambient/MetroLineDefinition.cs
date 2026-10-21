using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// An elevated metro line along the X axis with two tracks: eastbound trains run on the south track,
    /// westbound on the north one (right-hand running, as the road traffic). Meters, world X/Z.
    /// </summary>
    public sealed class MetroLineDefinition
    {
        public MetroLineDefinition(float westEnd, float eastEnd, float eastboundZ, float westboundZ, float railHeight,
            IEnumerable<float> stationX, float platformHalfLength)
        {
            if (eastEnd <= westEnd) throw new ArgumentException("The east end must be east of the west end.");
            WestEnd = westEnd;
            EastEnd = eastEnd;
            EastboundZ = eastboundZ;
            WestboundZ = westboundZ;
            RailHeight = railHeight;
            var stations = new List<float>(stationX);
            stations.Sort();
            StationX = stations;
            PlatformHalfLength = platformHalfLength;
        }

        public float WestEnd { get; }
        public float EastEnd { get; }
        public float EastboundZ { get; }
        public float WestboundZ { get; }

        /// <summary>Height of the top of the rails (where the wheels run).</summary>
        public float RailHeight { get; }

        /// <summary>Centres of the stations, west to east.</summary>
        public IReadOnlyList<float> StationX { get; }

        public float PlatformHalfLength { get; }

        public float TrackLength => EastEnd - WestEnd;
    }

    /// <summary>How the trains run (plain C#; the data asset fills it).</summary>
    public sealed class MetroSettings
    {
        public int TrainCount { get; set; } = 4;
        public int Cars { get; set; } = 3;
        public float CarLength { get; set; } = 22f;
        public float CarGap { get; set; } = 1f;

        /// <summary>Meters per second (20 m/s = 72 km/h).</summary>
        public float CruiseSpeed { get; set; } = 20f;

        public float Acceleration { get; set; } = 1f;
        public float Deceleration { get; set; } = 1f;

        /// <summary>Seconds a train stands at a station with its doors open.</summary>
        public float DwellSeconds { get; set; } = 25f;

        /// <summary>Seconds a train waits at the end of the line before returning on the other track.</summary>
        public float TurnbackSeconds { get; set; } = 30f;

        /// <summary>Distance a train keeps from the rear of the one ahead on the same track.</summary>
        public float MinSeparation { get; set; } = 150f;

        public float TrainLength => Cars * CarLength + (Cars - 1) * CarGap;
    }
}
