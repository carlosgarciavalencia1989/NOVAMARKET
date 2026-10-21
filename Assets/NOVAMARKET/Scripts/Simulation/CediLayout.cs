using System;
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

        /// <summary>
        /// |x| where a docked truck's rear stops on the west (receiving) and east (shipping) faces: 1 m in
        /// front of the bumpers (22.3), so the truck parks visibly at the edge of the dock.
        /// </summary>
        public const float DockWallX = 23.3f;

        public const float InboundLaneX = 2.4f;
        public const float OutboundLaneX = -2.4f;
        public const float InboundStopLineZ = -43.2f;
        public const float OutboundStopLineZ = -32.8f;

        public const float EastboundLaneZ = -63f;
        public const float WestboundLaneZ = -57f;
        /// <summary>
        /// Where trucks appear and vanish on the highway: halfway between the crossings at x = 250 and 350,
        /// so a truck never appears inside a crossing of the city traffic.
        /// </summary>
        public const float SpawnDistanceX = 300f;

        /// <summary>Where an outbound truck joins the westbound lane (it waits for a gap there).</summary>
        public static readonly Vector2 ExitMergePoint = new Vector2(-6f, WestboundLaneZ);

        /// <summary>Yard road used by trucks driving in (north of the outbound road, clear of the entrance canopy).</summary>
        private const float InboundYardZ = -18f;

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
            var start = new[]
            {
                InboundStop(length),
                new Vector2(InboundLaneX, InboundYardZ - 3f)
            };
            if (dock.Kind == DockKind.Shipping)
                return new Polyline(start[0], start[1],
                    new Vector2(InboundLaneX + 2.6f, InboundYardZ),
                    new Vector2(40f, InboundYardZ),
                    new Vector2(45f, dock.Z - 4f),
                    new Vector2(48f, dock.Z - 0.5f),
                    StagingCentre(dock));

            // West side: between the warehouse and the office, then north past the office and the
            // trucks parked at the receiving docks.
            return new Polyline(start[0], start[1],
                new Vector2(InboundLaneX - 2.6f, InboundYardZ),
                new Vector2(-28f, InboundYardZ),
                new Vector2(-38f, -13f),
                new Vector2(-44f, -8f),
                new Vector2(-47f, dock.Z - 1f),
                StagingCentre(dock));
        }

        /// <summary>True once a leaving truck's rear is on the outbound road, past the inbound road.</summary>
        public static bool LeftManoeuvreArea(Vector2 rear) => rear.Y <= OutboundYardZ + 0.5f && Math.Abs(rear.X) <= 32f;

        /// <summary>Straight reverse from the staging point to the dock bumpers.</summary>
        public static Polyline ReverseIntoDock(Dock dock, float length) =>
            new Polyline(StagingCentre(dock), DockedCentre(dock, length));

        /// <summary>From the dock, a U-turn in the yard and down the outbound lane to the stop line.</summary>
        public static Polyline FromDock(Dock dock, float length)
        {
            if (dock.Kind == DockKind.Shipping)
                return new Polyline(
                    DockedCentre(dock, length),
                    new Vector2(46f, dock.Z),
                    new Vector2(53f, dock.Z - 5f),
                    new Vector2(50f, -11f),
                    new Vector2(32f, OutboundYardZ),
                    new Vector2(1f, OutboundYardZ),
                    new Vector2(OutboundLaneX, -23f),
                    OutboundStop(length));

            // West side: U-turn north of the office, back east under the parked trucks and down the
            // gap between the warehouse and the office.
            return new Polyline(
                DockedCentre(dock, length),
                new Vector2(-46f, dock.Z),
                new Vector2(-53f, dock.Z - 5f),
                new Vector2(-50f, -9.8f),
                new Vector2(-30f, -9.8f),
                new Vector2(-26f, -14.5f),
                new Vector2(-26f, -18.5f),
                new Vector2(-22f, OutboundYardZ),
                new Vector2(-8f, OutboundYardZ),
                new Vector2(OutboundLaneX, -23f),
                OutboundStop(length));
        }

        // ------------------------------------------------------------------ obstacles (for tests and tools)

        /// <summary>Clearance every truck keeps from <see cref="TruckObstacles"/> (whole footprint, meters).</summary>
        public const float BuildingMargin = 1f;

        /// <summary>Areas trucks must never cross: warehouse, office and parked containers (min/max X-Z).</summary>
        public static IReadOnlyList<(Vector2 min, Vector2 max)> TruckObstacles { get; } = new[]
        {
            (new Vector2(-22.2f, -13f), new Vector2(22.2f, 13.2f)),    // warehouse
            (new Vector2(-16.5f, -15f), new Vector2(-11.5f, -13f)),    // main entrance canopy
            (new Vector2(-55.3f, -27.8f), new Vector2(-40.7f, -16.5f)), // office
            (new Vector2(66f, -26f), new Vector2(100f, 10f))           // container yard
        };
    }
}
