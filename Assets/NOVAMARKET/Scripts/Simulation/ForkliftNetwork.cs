using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// A place a forklift works at: <see cref="Point"/> is where it stops (facing the rack or the
    /// staging lane) and <see cref="Access"/> the aisle point it reaches it from.
    /// </summary>
    public sealed class ForkliftSpot
    {
        public ForkliftSpot(Vector2 point, Vector2 access, string label, float aisleZ = float.NaN)
        {
            Point = point;
            Access = access;
            Label = label;
            AisleZ = aisleZ;
        }

        public Vector2 Point { get; }
        public Vector2 Access { get; }
        public string Label { get; }

        /// <summary>Set for rack slots: the z of the aisle the slot opens onto.</summary>
        public float AisleZ { get; }

        public bool IsRackSlot => !float.IsNaN(AisleZ);
    }

    /// <summary>
    /// Aisle network inside the warehouse. Forklifts drive only along these aisles, so they
    /// never cross racks or picking stations. Routes are found with Dijkstra on a small graph.
    /// </summary>
    public static class ForkliftNetwork
    {
        /// <summary>Cross aisle between the receiving area and the racks.</summary>
        public const float WestCrossX = -13.5f;

        /// <summary>Cross aisle between the racks and the picking stations.</summary>
        public const float EastCrossX = 5.5f;

        /// <summary>Lane between the picking stations and the shipping area.</summary>
        public const float ShippingLaneX = 14f;

        /// <summary>Rack aisles (z), named A–E from south to north.</summary>
        public static readonly float[] AisleZ = { -11.2f, -5.6f, 0f, 5.6f, 11.2f };

        /// <summary>Rows that go around the picking stations (z).</summary>
        private static readonly float[] BypassZ = { -9.5f, 9.5f };

        public const float RackMinX = -11f;
        public const float RackMaxX = 3f;
        private const float RackFaceOffset = 0.7f;
        private const float ReceivingPickX = -15.5f;
        private const float ShippingDropX = 15.5f;

        /// <summary>Rack rows and picking stations (min/max X-Z) forklifts must never cross.</summary>
        public static IReadOnlyList<(Vector2 min, Vector2 max)> Obstacles { get; } = new[]
        {
            (new Vector2(-12f, -9.5f), new Vector2(4f, -7.3f)),
            (new Vector2(-12f, -3.9f), new Vector2(4f, -1.7f)),
            (new Vector2(-12f, 1.7f), new Vector2(4f, 3.9f)),
            (new Vector2(-12f, 7.3f), new Vector2(4f, 9.5f)),
            (new Vector2(6.75f, -6f), new Vector2(12.25f, 6f))
        };

        public static ForkliftSpot ReceivingStaging(int lane) =>
            new ForkliftSpot(new Vector2(ReceivingPickX, CediLayout.DockZ[lane]),
                new Vector2(WestCrossX, CediLayout.DockZ[lane]), $"muelle {lane + 1}");

        public static ForkliftSpot ShippingStaging(int lane) =>
            new ForkliftSpot(new Vector2(ShippingDropX, CediLayout.DockZ[lane]),
                new Vector2(ShippingLaneX, CediLayout.DockZ[lane]), $"muelle {lane + 4}");

        /// <summary>Idle spots, one per forklift.</summary>
        public static ForkliftSpot Park(int index)
        {
            Vector2[] parks = { new Vector2(ShippingLaneX, -9.5f), new Vector2(WestCrossX, 11.2f), new Vector2(EastCrossX, 11.2f) };
            Vector2 point = parks[index % parks.Length];
            return new ForkliftSpot(point, point, "zona de espera");
        }

        /// <summary>A rack position facing an aisle; <paramref name="northSide"/> picks the rack row across.</summary>
        public static ForkliftSpot RackSlot(int aisle, float x, bool northSide)
        {
            float z = AisleZ[aisle];
            // The outer aisles only have racks on one side.
            if (aisle == 0) northSide = true;
            if (aisle == AisleZ.Length - 1) northSide = false;
            int row = northSide ? aisle + 1 : aisle; // rack rows 1..4 from south to north
            float faceZ = z + (northSide ? RackFaceOffset : -RackFaceOffset);
            return new ForkliftSpot(new Vector2(x, faceZ), new Vector2(x, z), $"rack R{row} (pasillo {(char)('A' + aisle)})", z);
        }

        /// <summary>
        /// Legs from one spot to another: back out to the aisle if needed, then drive forwards
        /// along the aisles to the target spot.
        /// </summary>
        public static List<RouteLeg> Route(ForkliftSpot from, ForkliftSpot to, float speed)
        {
            var legs = new List<RouteLeg>();
            if (Vector2.DistanceSquared(from.Point, from.Access) > 1e-4f)
                legs.Add(new RouteLeg(new Polyline(from.Point, from.Access), speed * 0.5f, reverse: true));

            var points = new List<Vector2>(FindPath(from, to)) { to.Point };
            if (points.Count >= 2 && Vector2.DistanceSquared(points[0], points[points.Count - 1]) > 1e-4f)
                legs.Add(new RouteLeg(new Polyline(points), speed));
            return legs;
        }

        /// <summary>Aisle points from <paramref name="from"/>'s access to <paramref name="to"/>'s access.</summary>
        public static List<Vector2> FindPath(ForkliftSpot from, ForkliftSpot to)
        {
            var nodes = new List<Vector2>();
            var edges = new List<(int a, int b)>();
            BuildGraph(nodes, edges);

            int start = AddSpotNode(from, nodes, edges);
            int end = AddSpotNode(to, nodes, edges);
            if (from.IsRackSlot && to.IsRackSlot && Math.Abs(from.AisleZ - to.AisleZ) < 0.01f)
                edges.Add((start, end));

            return Dijkstra(nodes, edges, start, end);
        }

        private static void BuildGraph(List<Vector2> nodes, List<(int a, int b)> edges)
        {
            var westZ = new List<float>(AisleZ);
            westZ.AddRange(CediLayout.DockZ);
            westZ.Sort();
            var eastZ = new List<float>(AisleZ);
            eastZ.AddRange(BypassZ);
            eastZ.Sort();
            var laneZ = new List<float>(BypassZ);
            laneZ.AddRange(CediLayout.DockZ);
            laneZ.Sort();

            AddChain(nodes, edges, WestCrossX, westZ);
            AddChain(nodes, edges, EastCrossX, eastZ);
            AddChain(nodes, edges, ShippingLaneX, laneZ);

            foreach (float z in AisleZ)
                edges.Add((Index(nodes, new Vector2(WestCrossX, z)), Index(nodes, new Vector2(EastCrossX, z))));
            foreach (float z in BypassZ)
                edges.Add((Index(nodes, new Vector2(EastCrossX, z)), Index(nodes, new Vector2(ShippingLaneX, z))));
        }

        private static void AddChain(List<Vector2> nodes, List<(int a, int b)> edges, float x, List<float> zs)
        {
            for (int i = 0; i < zs.Count; i++)
            {
                int current = Index(nodes, new Vector2(x, zs[i]));
                if (i > 0) edges.Add((Index(nodes, new Vector2(x, zs[i - 1])), current));
            }
        }

        /// <summary>Adds the spot's access point; rack slots connect to both ends of their aisle.</summary>
        private static int AddSpotNode(ForkliftSpot spot, List<Vector2> nodes, List<(int a, int b)> edges)
        {
            int existing = nodes.FindIndex(n => Vector2.DistanceSquared(n, spot.Access) < 1e-4f);
            if (existing >= 0) return existing;

            nodes.Add(spot.Access);
            int index = nodes.Count - 1;
            if (spot.IsRackSlot)
            {
                edges.Add((index, Index(nodes, new Vector2(WestCrossX, spot.AisleZ))));
                edges.Add((index, Index(nodes, new Vector2(EastCrossX, spot.AisleZ))));
            }
            else
            {
                throw new ArgumentException($"Spot access {spot.Access} is not on the aisle network.", nameof(spot));
            }
            return index;
        }

        private static int Index(List<Vector2> nodes, Vector2 point)
        {
            int index = nodes.FindIndex(n => Vector2.DistanceSquared(n, point) < 1e-4f);
            if (index >= 0) return index;
            nodes.Add(point);
            return nodes.Count - 1;
        }

        private static List<Vector2> Dijkstra(List<Vector2> nodes, List<(int a, int b)> edges, int start, int end)
        {
            var distance = new float[nodes.Count];
            var previous = new int[nodes.Count];
            var done = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                distance[i] = float.MaxValue;
                previous[i] = -1;
            }
            distance[start] = 0f;

            for (int step = 0; step < nodes.Count; step++)
            {
                int current = -1;
                for (int i = 0; i < nodes.Count; i++)
                    if (!done[i] && distance[i] < float.MaxValue && (current < 0 || distance[i] < distance[current]))
                        current = i;
                if (current < 0 || current == end) break;
                done[current] = true;

                foreach ((int a, int b) in edges)
                {
                    int next = a == current ? b : b == current ? a : -1;
                    if (next < 0 || done[next]) continue;
                    float candidate = distance[current] + Vector2.Distance(nodes[current], nodes[next]);
                    if (candidate < distance[next])
                    {
                        distance[next] = candidate;
                        previous[next] = current;
                    }
                }
            }

            if (start != end && previous[end] < 0)
                throw new InvalidOperationException("No forklift route between these spots.");

            var path = new List<Vector2>();
            for (int node = end; node >= 0; node = node == start ? -1 : previous[node])
                path.Insert(0, nodes[node]);
            return path;
        }
    }
}
