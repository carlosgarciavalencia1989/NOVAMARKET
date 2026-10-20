using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NovaMarket.Simulation.Ambient;

namespace NovaMarket.Tests.Simulation.Ambient
{
    /// <summary>Small road networks for the traffic tests.</summary>
    internal static class TestNetworks
    {
        /// <summary>Two two-way roads crossing at the origin, each end leaving the map.</summary>
        public static RoadNetworkDefinition Crossing(bool signalized, RoadClass eastWest = RoadClass.Street,
            RoadClass northSouth = RoadClass.Street)
        {
            var definition = new RoadNetworkDefinition();
            int centre = definition.AddNode(Vector2.Zero, RoadNodeKind.Junction, signalized);
            int west = definition.AddNode(new Vector2(-300f, 0f), RoadNodeKind.Boundary);
            int east = definition.AddNode(new Vector2(300f, 0f), RoadNodeKind.Boundary);
            int south = definition.AddNode(new Vector2(0f, -200f), RoadNodeKind.Boundary);
            int north = definition.AddNode(new Vector2(0f, 200f), RoadNodeKind.Boundary);
            definition.AddRoad(west, centre, eastWest, true, 6f, 11f);
            definition.AddRoad(centre, east, eastWest, true, 6f, 11f);
            definition.AddRoad(south, centre, northSouth, true, 6f, 11f);
            definition.AddRoad(centre, north, northSouth, true, 6f, 11f);
            return definition;
        }

        /// <summary>A straight two-way street with a bus stop in the middle of the eastbound lane.</summary>
        public static RoadNetworkDefinition StreetWithBusStop()
        {
            var definition = new RoadNetworkDefinition();
            int west = definition.AddNode(new Vector2(-400f, 0f), RoadNodeKind.Boundary);
            int east = definition.AddNode(new Vector2(400f, 0f), RoadNodeKind.Boundary);
            definition.AddRoad(west, east, RoadClass.Street, true, 6f, 11f);
            definition.BusStops.Add(new LaneMarkerDefinition(Vector2.Zero, new Vector2(1f, 0f)));
            return definition;
        }

        public static readonly float[] BridgeX = { -450f, -150f, 150f, 450f };
        public static readonly float[] AvenueX = { -450f, 450f };
        public static readonly float[] AvenueZ = { 106f, -297f };
        public const float NorthEdge = -54f;
        public const float SouthEdge = -137f;

        /// <summary>
        /// The valley around the CEDI as the environment builder lays it out: 100 × 80 m cells north of the
        /// highway and south of the river, with the CEDI lot (no cells) north of the highway near x = 0.
        /// </summary>
        public static RoadNetworkDefinition City()
        {
            var cells = new List<StreetCell>();
            for (int i = -5; i < 5; i++)
            {
                float x0 = i * 100f + 50f, x1 = x0 + 100f;
                for (int row = 0; row < 3; row++)
                {
                    float z0 = NorthEdge + row * 80f;
                    bool cediLot = row < 2 && x1 > -120f && x0 < 140f;
                    if (!cediLot) cells.Add(new StreetCell(x0, z0, x1, z0 + 80f));
                    float s1 = SouthEdge - row * 80f;
                    cells.Add(new StreetCell(x0, s1 - 80f, x1, s1));
                }
            }
            return CityRoadLayout.Build(cells, NorthEdge, SouthEdge, BridgeX, AvenueX, AvenueZ);
        }

        public static TrafficLane LaneHeading(RoadNetwork network, Vector2 direction, Vector2 near) =>
            network.Lanes.Where(l => Vector2.Dot(l.Direction, direction) > 0.99f)
                .OrderBy(l => Vector2.Distance((l.Start + l.End) / 2f, near)).First();
    }
}
