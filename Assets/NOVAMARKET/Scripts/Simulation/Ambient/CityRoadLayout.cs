using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>A grid cell with asphalt: a city block plus half of each street around it (meters, X/Z).</summary>
    public readonly struct StreetCell
    {
        public StreetCell(float xMin, float zMin, float xMax, float zMax)
        {
            XMin = xMin;
            ZMin = zMin;
            XMax = xMax;
            ZMax = zMax;
        }

        public float XMin { get; }
        public float ZMin { get; }
        public float XMax { get; }
        public float ZMax { get; }
    }

    /// <summary>
    /// Road network of the Aburrá valley around the CEDI, from the street grid of the environment
    /// builder: the highway along the transport corridor, streets between blocks (two-way where there is a
    /// block on both sides, one-way "half streets" of 6 m where there is only one), avenues, the
    /// bridges over the river, traffic lights, bus stops and the "do not block" box at the CEDI gate.
    /// </summary>
    public static class CityRoadLayout
    {
        public const float HighwayZ = -60f;
        public const float HighwayHalfWidth = 6f;
        public const float HighwayHalfLength = 1450f;
        public const float StreetHalfWidth = 6f;
        public const float HalfStreetHalfWidth = 3f;

        public const float HighwaySpeed = 16.7f; // 60 km/h
        public const float AvenueSpeed = 13.9f;  // 50 km/h
        public const float StreetSpeed = 11.1f;  // 40 km/h

        /// <summary>Westbound lane in front of the CEDI gate: trucks turn in and out there.</summary>
        public static readonly LaneMarkerDefinition GateKeepClear =
            new LaneMarkerDefinition(new Vector2(-2f, HighwayZ + 3f), new Vector2(-1f, 0f), 15f);

        /// <summary>Bus stops on the highway, near the two metro stations.</summary>
        public static readonly float[] HighwayBusStopX = { -330f, 330f };

        /// <param name="cells">Cells of the street grid that have asphalt.</param>
        /// <param name="northEdge">Grid line along the highway's north kerb (its frontage strip is not a street).</param>
        /// <param name="bridgeX">Streets that cross the river and the highway (with traffic lights at the highway).</param>
        /// <param name="avenueX">North-south grid lines that are avenues.</param>
        /// <param name="avenueZ">East-west grid lines that are avenues (lights where two avenues cross).</param>
        public static RoadNetworkDefinition Build(IEnumerable<StreetCell> cells, float northEdge, float southEdge,
            IReadOnlyCollection<float> bridgeX, IReadOnlyCollection<float> avenueX, IReadOnlyCollection<float> avenueZ)
        {
            var cellList = new List<StreetCell>(cells);
            var definition = new RoadNetworkDefinition();
            var nodes = new Dictionary<(int, int), int>();
            var highwayNodes = new SortedDictionary<int, int>();

            int HighwayNode(float x)
            {
                int key = Key(x);
                if (!highwayNodes.TryGetValue(key, out int node))
                    highwayNodes[key] = node = definition.AddNode(new Vector2(x, HighwayZ), RoadNodeKind.Junction,
                        Contains(bridgeX, x));
                return node;
            }

            int GridNode(float x, float z)
            {
                if (Near(z, northEdge)) return HighwayNode(x);
                var key = (Key(x), Key(z));
                if (!nodes.TryGetValue(key, out int node))
                    nodes[key] = node = definition.AddNode(new Vector2(x, z), RoadNodeKind.Junction,
                        Contains(avenueX, x) && Contains(avenueZ, z));
                return node;
            }

            // Every cell side is a street: count the cells on each side of it.
            var sides = new Dictionary<(int, int, int, int), (Vector2 A, Vector2 B, int Left, int Right)>();
            void AddSide(Vector2 a, Vector2 b, bool cellOnLeft)
            {
                var key = (Key(a.X), Key(a.Y), Key(b.X), Key(b.Y));
                sides.TryGetValue(key, out var side);
                side.A = a;
                side.B = b;
                if (cellOnLeft) side.Left++;
                else side.Right++;
                sides[key] = side;
            }
            foreach (StreetCell cell in cellList)
            {
                // Sides always run west→east or south→north; "left" is north or west of them.
                AddSide(new Vector2(cell.XMin, cell.ZMin), new Vector2(cell.XMax, cell.ZMin), true);  // south side, cell north
                AddSide(new Vector2(cell.XMin, cell.ZMax), new Vector2(cell.XMax, cell.ZMax), false); // north side, cell south
                AddSide(new Vector2(cell.XMin, cell.ZMin), new Vector2(cell.XMin, cell.ZMax), false); // west side, cell east
                AddSide(new Vector2(cell.XMax, cell.ZMin), new Vector2(cell.XMax, cell.ZMax), true);  // east side, cell west
            }

            var roads = new List<(int From, int To, bool TwoWay, bool Avenue)>();
            foreach (var side in sides.Values)
            {
                bool horizontal = Math.Abs(side.A.Y - side.B.Y) < 0.5f;
                if (horizontal && Near(side.A.Y, northEdge)) continue; // the highway's frontage strip
                bool avenue = horizontal ? Contains(avenueZ, side.A.Y) : Contains(avenueX, side.A.X);
                float speed = avenue ? AvenueSpeed : StreetSpeed;
                RoadClass roadClass = avenue ? RoadClass.Avenue : RoadClass.Street;
                int a = GridNode(side.A.X, side.A.Y), b = GridNode(side.B.X, side.B.Y);

                if (side.Left > 0 && side.Right > 0)
                    definition.AddRoad(a, b, roadClass, true, StreetHalfWidth, speed);
                else
                {
                    // One-way half street driven with its cell on the left (around each area counter-clockwise),
                    // its lane shifted onto the asphalt (to the left = negative shift).
                    bool forward = side.Left > 0;
                    definition.AddRoad(forward ? a : b, forward ? b : a, roadClass, false, HalfStreetHalfWidth, speed,
                        -HalfStreetHalfWidth);
                }
            }

            // Bridges: from the highway, under the metro and over the river, to the grid on the south bank.
            foreach (float x in bridgeX)
            {
                if (!nodes.TryGetValue((Key(x), Key(southEdge)), out int south)) continue;
                bool avenue = Contains(avenueX, x);
                definition.AddRoad(HighwayNode(x), south, avenue ? RoadClass.Avenue : RoadClass.Street, true,
                    StreetHalfWidth, avenue ? AvenueSpeed : StreetSpeed);
            }

            // The highway: a chain of nodes between its two ends.
            int previous = definition.AddNode(new Vector2(-HighwayHalfLength, HighwayZ), RoadNodeKind.Boundary);
            foreach (KeyValuePair<int, int> pair in highwayNodes)
            {
                definition.AddRoad(previous, pair.Value, RoadClass.Highway, true, HighwayHalfWidth, HighwaySpeed);
                previous = pair.Value;
            }
            int east = definition.AddNode(new Vector2(HighwayHalfLength, HighwayZ), RoadNodeKind.Boundary);
            definition.AddRoad(previous, east, RoadClass.Highway, true, HighwayHalfWidth, HighwaySpeed);

            AddBusStops(definition, southEdge, northEdge);
            foreach (Crosswalk crosswalk in CityWalkLayout.Crosswalks(cellList))
                definition.Crosswalks.Add(new LaneMarkerDefinition(crosswalk.Centre, crosswalk.StreetDirection,
                    CityWalkLayout.CrosswalkWidth / 2f));
            definition.KeepClear.Add(GateKeepClear);
            return definition;
        }

        /// <summary>Stops on the highway near the metro stations and on every other avenue block (not on bridges), both directions.</summary>
        private static void AddBusStops(RoadNetworkDefinition definition, float southEdge, float northEdge)
        {
            foreach (float x in HighwayBusStopX)
            {
                definition.BusStops.Add(new LaneMarkerDefinition(new Vector2(x, HighwayZ), new Vector2(1f, 0f)));
                definition.BusStops.Add(new LaneMarkerDefinition(new Vector2(x, HighwayZ), new Vector2(-1f, 0f)));
            }
            int avenueRoads = 0;
            foreach (RoadDefinition road in definition.Roads)
            {
                if (road.Class != RoadClass.Avenue || !road.TwoWay || avenueRoads++ % 2 != 0) continue;
                Vector2 a = definition.Nodes[road.From].Position, b = definition.Nodes[road.To].Position;
                Vector2 middle = (a + b) / 2f, direction = Vector2.Normalize(b - a);
                if (middle.Y > southEdge && middle.Y < northEdge) continue; // a bridge over the river: no stop
                definition.BusStops.Add(new LaneMarkerDefinition(middle, direction));
                definition.BusStops.Add(new LaneMarkerDefinition(middle, -direction));
            }
        }

        private static int Key(float value) => (int)Math.Round(value * 10f);
        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.5f;

        private static bool Contains(IReadOnlyCollection<float> values, float value)
        {
            foreach (float candidate in values)
                if (Near(candidate, value)) return true;
            return false;
        }
    }
}
