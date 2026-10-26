using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>A street lamp: where its post stands and which way its arm reaches (towards the road).</summary>
    public readonly struct StreetLamp
    {
        public StreetLamp(Vector2 position, Vector2 facing)
        {
            Position = position;
            Facing = facing;
        }

        public Vector2 Position { get; }
        public Vector2 Facing { get; }
    }

    /// <summary>
    /// Lamp posts along the highway and the avenues: on the right kerb of each lane every few meters, away from
    /// the crossings and the bus stops. Plain C#; the scene places one lamp model at each.
    /// </summary>
    public static class StreetLampLayout
    {
        /// <summary>Meters between the kerb and the post.</summary>
        public const float KerbMargin = 1f;

        /// <summary>Meters kept free at each end of a lane (the crossing) and around a bus stop.</summary>
        public const float EndClearance = 12f;
        public const float BusStopClearance = 8f;

        public static List<StreetLamp> Place(IEnumerable<TrafficLane> lanes, float spacing)
        {
            var lamps = new List<StreetLamp>();
            if (spacing <= 0f) return lamps;
            foreach (TrafficLane lane in lanes)
            {
                if (lane.Class != RoadClass.Highway && lane.Class != RoadClass.Avenue) continue;
                float kerb = (lane.TwoWay ? lane.HalfWidth / 2f : lane.HalfWidth) + KerbMargin;
                float usable = lane.Length - 2f * EndClearance;
                if (usable < 0f) continue;
                int count = (int)(usable / spacing) + 1;
                float start = EndClearance + (usable - (count - 1) * spacing) / 2f; // centred on the lane
                for (int i = 0; i < count; i++)
                {
                    float along = start + i * spacing;
                    if (NearBusStop(lane, along)) continue;
                    lamps.Add(new StreetLamp(lane.PointAt(along) + lane.Right * kerb, -lane.Right));
                }
            }
            return lamps;
        }

        private static bool NearBusStop(TrafficLane lane, float along)
        {
            foreach (float stop in lane.BusStops)
                if (Math.Abs(stop - along) < BusStopClearance) return true;
            return false;
        }
    }
}
