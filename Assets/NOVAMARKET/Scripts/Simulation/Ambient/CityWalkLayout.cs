using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    public enum CityBlockKind
    {
        City,
        Downtown,
        Park,
        Industrial
    }

    /// <summary>A block of the street grid (without its half streets), meters X/Z, and what it holds.</summary>
    public readonly struct CityBlock
    {
        public CityBlock(float xMin, float zMin, float xMax, float zMax, CityBlockKind kind)
        {
            XMin = xMin;
            ZMin = zMin;
            XMax = xMax;
            ZMax = zMax;
            Kind = kind;
        }

        public float XMin { get; }
        public float ZMin { get; }
        public float XMax { get; }
        public float ZMax { get; }
        public CityBlockKind Kind { get; }
    }

    public enum WalkEdgeKind
    {
        /// <summary>The raised sidewalk around a city block.</summary>
        Sidewalk,

        /// <summary>Paths of a park (the plazas of the valley).</summary>
        ParkPath,

        /// <summary>The edge of an industrial block (few people).</summary>
        Yard,

        /// <summary>A zebra crossing over a street.</summary>
        Crosswalk,

        /// <summary>A metro platform.</summary>
        Platform,

        /// <summary>A footpath inside the CEDI lot.</summary>
        CediPath
    }

    /// <summary>A point people walk through: position (X/Z) and the height of the ground there.</summary>
    public readonly struct WalkNodeDefinition
    {
        public WalkNodeDefinition(Vector2 position, float height)
        {
            Position = position;
            Height = height;
        }

        public Vector2 Position { get; }
        public float Height { get; }
    }

    /// <summary>A straight walkable stretch between two nodes.</summary>
    public readonly struct WalkEdgeDefinition
    {
        public WalkEdgeDefinition(int a, int b, WalkEdgeKind kind, float halfWidth, float busyness)
        {
            A = a;
            B = b;
            Kind = kind;
            HalfWidth = halfWidth;
            Busyness = busyness;
        }

        public int A { get; }
        public int B { get; }
        public WalkEdgeKind Kind { get; }

        /// <summary>People keep within this distance of the centre line.</summary>
        public float HalfWidth { get; }

        /// <summary>How many people appear here per meter, relative to a city sidewalk (1).</summary>
        public float Busyness { get; }
    }

    /// <summary>Plain description of where people walk (built by <see cref="CityWalkLayout"/> or tests).</summary>
    public sealed class WalkNetworkDefinition
    {
        public List<WalkNodeDefinition> Nodes { get; } = new List<WalkNodeDefinition>();
        public List<WalkEdgeDefinition> Edges { get; } = new List<WalkEdgeDefinition>();

        public int AddNode(Vector2 position, float height)
        {
            Nodes.Add(new WalkNodeDefinition(position, height));
            return Nodes.Count - 1;
        }

        public void AddEdge(int a, int b, WalkEdgeKind kind, float halfWidth, float busyness) =>
            Edges.Add(new WalkEdgeDefinition(a, b, kind, halfWidth, busyness));
    }

    /// <summary>A zebra crossing over the street between two neighbouring blocks.</summary>
    public readonly struct Crosswalk
    {
        public Crosswalk(Vector2 from, Vector2 to)
        {
            From = from;
            To = to;
        }

        /// <summary>The ends, on the sidewalk path of each block.</summary>
        public Vector2 From { get; }
        public Vector2 To { get; }

        public Vector2 Centre => (From + To) / 2f;

        /// <summary>Direction of the street it crosses.</summary>
        public Vector2 StreetDirection
        {
            get
            {
                Vector2 across = Vector2.Normalize(To - From);
                return new Vector2(across.Y, -across.X);
            }
        }
    }

    /// <summary>
    /// Where people walk in the valley, from the blocks of the street grid: the 2.5 m sidewalk around city
    /// and downtown blocks (path 1.25 m from the kerb, 18 cm high), the edge of industrial blocks, the
    /// perimeter and the two crossing paths of each park, and zebra crossings between neighbouring blocks,
    /// 3 m back from each corner (the vehicles' stop line is moved behind them).
    /// </summary>
    public static class CityWalkLayout
    {
        public const float SidewalkInset = 1.25f;
        public const float SidewalkTop = 0.18f;
        public const float ParkPathTop = 0.22f;
        public const float StreetTop = 0.03f;
        public const float StreetGap = 12f;

        /// <summary>Distance from the block corner (along the street) to the centre of a zebra crossing.</summary>
        public const float CrosswalkFromCorner = 3f;

        /// <summary>Width of the zebra band.</summary>
        public const float CrosswalkWidth = 3f;

        public static WalkNetworkDefinition Build(IEnumerable<CityBlock> blocks)
        {
            var definition = new WalkNetworkDefinition();
            var nodes = new Dictionary<(int, int), int>();
            var list = new List<CityBlock>(blocks);

            int Node(Vector2 p, float height)
            {
                var key = ((int)Math.Round(p.X * 10f), (int)Math.Round(p.Y * 10f));
                if (!nodes.TryGetValue(key, out int index)) nodes[key] = index = definition.AddNode(p, height);
                return index;
            }

            foreach (CityBlock block in list) AddBlock(definition, block, Node);

            foreach (Crosswalk crosswalk in Crosswalks(Cells(list)))
            {
                int a = Node(crosswalk.From, Height(Kind(list, crosswalk.From)));
                int b = Node(crosswalk.To, Height(Kind(list, crosswalk.To)));
                definition.AddEdge(a, b, WalkEdgeKind.Crosswalk, 1.2f, 0f);
            }
            return definition;
        }

        /// <summary>
        /// The zebra crossings of a street grid: one near each end of every street between two neighbouring
        /// cells (a cell is a block plus half of each street around it).
        /// </summary>
        public static List<Crosswalk> Crosswalks(IEnumerable<StreetCell> cells)
        {
            var result = new List<Crosswalk>();
            var all = new List<StreetCell>(cells);
            float half = StreetGap / 2f, inset = half + SidewalkInset, along = half + CrosswalkFromCorner;
            foreach (StreetCell a in all)
            foreach (StreetCell b in all)
            {
                // b east of a: cross the north-south street between them.
                if (Near(b.XMin, a.XMax) && Near(b.ZMin, a.ZMin) && Near(b.ZMax, a.ZMax))
                    foreach (float z in new[] { a.ZMin + along, a.ZMax - along })
                        result.Add(new Crosswalk(new Vector2(a.XMax - inset, z), new Vector2(b.XMin + inset, z)));
                // b north of a: cross the east-west street between them.
                if (Near(b.ZMin, a.ZMax) && Near(b.XMin, a.XMin) && Near(b.XMax, a.XMax))
                    foreach (float x in new[] { a.XMin + along, a.XMax - along })
                        result.Add(new Crosswalk(new Vector2(x, a.ZMax - inset), new Vector2(x, b.ZMin + inset)));
            }
            return result;
        }

        private static void AddBlock(WalkNetworkDefinition definition, CityBlock block, Func<Vector2, float, int> node)
        {
            const float inset = SidewalkInset;
            float height = Height(block.Kind);
            WalkEdgeKind kind = block.Kind == CityBlockKind.Industrial ? WalkEdgeKind.Yard : WalkEdgeKind.Sidewalk;
            float busyness = block.Kind switch
            {
                CityBlockKind.Downtown => 2f,
                CityBlockKind.Park => 1.5f,
                CityBlockKind.Industrial => 0.15f,
                _ => 1f
            };
            float x0 = block.XMin + inset, x1 = block.XMax - inset, z0 = block.ZMin + inset, z1 = block.ZMax - inset;
            const float c = CrosswalkFromCorner - inset; // where the crossings attach (from the path corner)
            float mx = (x0 + x1) / 2f, mz = (z0 + z1) / 2f;

            // Perimeter, split where the crossings attach and (parks) where the inner paths start.
            var ring = new List<Vector2>
            {
                new Vector2(x0, z0), new Vector2(x0 + c, z0), new Vector2(mx, z0), new Vector2(x1 - c, z0),
                new Vector2(x1, z0), new Vector2(x1, z0 + c), new Vector2(x1, mz), new Vector2(x1, z1 - c),
                new Vector2(x1, z1), new Vector2(x1 - c, z1), new Vector2(mx, z1), new Vector2(x0 + c, z1),
                new Vector2(x0, z1), new Vector2(x0, z1 - c), new Vector2(x0, mz), new Vector2(x0, z0 + c)
            };
            for (int i = 0; i < ring.Count; i++)
                definition.AddEdge(node(ring[i], height), node(ring[(i + 1) % ring.Count], height), kind, 0.9f, busyness);

            if (block.Kind != CityBlockKind.Park) return;
            int centre = node(new Vector2(mx, mz), ParkPathTop);
            foreach (Vector2 end in new[] { new Vector2(mx, z0), new Vector2(x1, mz), new Vector2(mx, z1), new Vector2(x0, mz) })
                definition.AddEdge(node(end, height), centre, WalkEdgeKind.ParkPath, 1.3f, 2.5f);
        }

        private static float Height(CityBlockKind kind) => kind == CityBlockKind.Industrial ? StreetTop : SidewalkTop;

        private static CityBlockKind Kind(List<CityBlock> blocks, Vector2 point)
        {
            foreach (CityBlock block in blocks)
                if (point.X >= block.XMin - 0.1f && point.X <= block.XMax + 0.1f && point.Y >= block.ZMin - 0.1f && point.Y <= block.ZMax + 0.1f)
                    return block.Kind;
            return CityBlockKind.City;
        }

        private static IEnumerable<StreetCell> Cells(List<CityBlock> blocks)
        {
            float half = StreetGap / 2f;
            foreach (CityBlock block in blocks)
                yield return new StreetCell(block.XMin - half, block.ZMin - half, block.XMax + half, block.ZMax + half);
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.5f;
    }
}
