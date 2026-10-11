using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// Ground plan of the CEDI Medellín lot used by the operation: gate lanes, dock doors and
    /// the truck routes between them. Routes are lists of truck centre points (world X/Z,
    /// meters) and depend on the vehicle length so the front stops at stop lines and the rear
    /// at the dock bumpers. Traffic drives on the right (Colombia).
    /// </summary>
    public static class CediLayout
    {
        /// <summary>Warehouse floor height (dock height); forklifts and pallets live on it.</summary>
        public const float IndoorFloor = 1.2f;

        /// <summary>Dock door centres (z), shared by both faces of the warehouse.</summary>
        public static readonly float[] DockZ = { -4.5f, 0f, 4.5f };

        /// <summary>|x| of the dock bumpers on the west (receiving) and east (shipping) faces.</summary>
        public const float DockWallX = 22.6f;

        public const float InboundLaneX = 2.4f;
        public const float OutboundLaneX = -2.4f;
        public const float InboundStopLineZ = -43.2f;
        public const float OutboundStopLineZ = -32.8f;

        public const float EastboundLaneZ = -63f;
        public const float WestboundLaneZ = -57f;
        public const float SpawnDistanceX = 260f;

        /// <summary>Yard road used by trucks driving in (north of the outbound queue).</summary>
        private const float InboundYardZ = -16f;

        /// <summary>Yard road used by trucks driving out.</summary>
        private const float OutboundYardZ = -21f;

        /// <summary>Where trucks wait before reversing: |x| of the truck centre.</summary>
        private const float StagingX = 52f;

        // ------------------------------------------------------------------ gate

        /// <summary>Centre of a truck whose front touches the inbound stop line.</summary>
        public static Vector2 InboundStop(float length) => new Vector2(InboundLaneX, InboundStopLineZ - length / 2f);

        /// <summary>Centre of a truck whose front touches the outbound stop line (driving south).</summary>
        public static Vector2 OutboundStop(float length) => new Vector2(OutboundLaneX, OutboundStopLineZ + length / 2f);

        /// <summary>From the highway (east or west end) to the inbound stop line.</summary>
        public static Polyline Approach(bool fromEast, float length)
        {
            Vector2 stop = InboundStop(length);
            if (fromEast)
                return new Polyline(
                    new Vector2(SpawnDistanceX, WestboundLaneZ),
                    new Vector2(12f, WestboundLaneZ),
                    new Vector2(5f, WestboundLaneZ),
                    new Vector2(InboundLaneX, -54.5f),
                    stop);
            return new Polyline(
                new Vector2(-SpawnDistanceX, EastboundLaneZ),
                new Vector2(-14f, EastboundLaneZ),
                new Vector2(-4f, -62f),
                new Vector2(1.2f, -59f),
                new Vector2(InboundLaneX, -56f),
                stop);
        }

        /// <summary>From the outbound stop line to the highway, turning east or west.</summary>
        public static Polyline Exit(bool toEast, float length)
        {
            Vector2 start = OutboundStop(length);
            if (toEast)
                return new Polyline(
                    start,
                    new Vector2(OutboundLaneX, -50f),
                    new Vector2(-1f, -56f),
                    new Vector2(6f, EastboundLaneZ),
                    new Vector2(SpawnDistanceX, EastboundLaneZ));
            return new Polyline(
                start,
                new Vector2(OutboundLaneX, -52f),
                new Vector2(-4f, -55f),
                new Vector2(-10f, WestboundLaneZ),
                new Vector2(-SpawnDistanceX, WestboundLaneZ));
        }

        // ------------------------------------------------------------------ docks

        /// <summary>+1 for the shipping (east) face, -1 for the receiving (west) face.</summary>
        private static float Side(DockKind kind) => kind == DockKind.Shipping ? 1f : -1f;

        /// <summary>Centre of a truck parked at the dock, rear against the bumpers.</summary>
        public static Vector2 DockedCentre(Dock dock, float length) =>
            new Vector2(dock.WallX + Side(dock.Kind) * length / 2f, dock.Z);

        /// <summary>Centre of the truck when it stops in front of the dock, ready to reverse.</summary>
        public static Vector2 StagingCentre(Dock dock) => new Vector2(Side(dock.Kind) * StagingX, dock.Z);

        /// <summary>From the inbound stop line through the yard to the point in front of the dock.</summary>
        public static Polyline ToStaging(Dock dock, float length)
        {
            float s = Side(dock.Kind);
            return new Polyline(
                InboundStop(length),
                new Vector2(InboundLaneX, -19f),
                new Vector2(InboundLaneX + s * 2.6f, InboundYardZ),
                new Vector2(s * 40f, InboundYardZ),
                new Vector2(s * 45f, dock.Z - 4f),
                new Vector2(s * 48f, dock.Z - 0.5f),
                StagingCentre(dock));
        }

        /// <summary>Straight reverse from the staging point to the dock bumpers.</summary>
        public static Polyline ReverseIntoDock(Dock dock, float length) =>
            new Polyline(StagingCentre(dock), DockedCentre(dock, length));

        /// <summary>From the dock, a U-turn in the yard and down the outbound lane to the stop line.</summary>
        public static Polyline FromDock(Dock dock, float length)
        {
            float s = Side(dock.Kind);
            return new Polyline(
                DockedCentre(dock, length),
                new Vector2(s * 46f, dock.Z),
                new Vector2(s * 53f, dock.Z - 5f),
                new Vector2(s * 50f, -11f),
                new Vector2(s * 32f, OutboundYardZ),
                new Vector2(dock.Kind == DockKind.Shipping ? 1f : -8f, OutboundYardZ),
                new Vector2(OutboundLaneX, -23f),
                OutboundStop(length));
        }

        // ------------------------------------------------------------------ obstacles (for tests and tools)

        /// <summary>Areas trucks must never cross: warehouse, office and parked containers (min/max X-Z).</summary>
        public static IReadOnlyList<(Vector2 min, Vector2 max)> TruckObstacles { get; } = new[]
        {
            (new Vector2(-22f, -13f), new Vector2(22f, 13f)),     // warehouse
            (new Vector2(-55f, -27.5f), new Vector2(-41f, -18.5f)), // office
            (new Vector2(70f, -18f), new Vector2(80f, 4f))         // container stacks
        };
    }
}
