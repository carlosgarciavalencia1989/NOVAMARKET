using System;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// Day ranges of each lead-time phase (DISENO_JUEGO_V2.md, section 3.1). The nominal lead time
    /// of a SKU is the sum of one draw per phase: production, pickup to the origin port, international
    /// transit, arrival, customs release, national transport and receiving. National suppliers only
    /// have production, road and receiving.
    /// </summary>
    public static class LeadTimeTable
    {
        /// <summary>Port-to-port transit days for an origin and mode.</summary>
        public static (int Min, int Max) Transit(SupplyOrigin origin, TransportMode mode) => (origin, mode) switch
        {
            (SupplyOrigin.China, TransportMode.MaritimoFcl) => (25, 35),
            (SupplyOrigin.China, TransportMode.MaritimoLcl) => (30, 40),
            (SupplyOrigin.Japon, TransportMode.MaritimoFcl) => (28, 38),
            (SupplyOrigin.Japon, TransportMode.MaritimoLcl) => (33, 43),
            (SupplyOrigin.EstadosUnidos, TransportMode.MaritimoFcl) => (4, 8),
            (SupplyOrigin.EstadosUnidos, TransportMode.MaritimoLcl) => (6, 10),
            (SupplyOrigin.EstadosUnidos, TransportMode.Aereo) => (1, 2),
            (SupplyOrigin.EstadosUnidos, TransportMode.Courier) => (2, 3),
            (SupplyOrigin.Europa, TransportMode.MaritimoFcl) => (18, 30),
            (SupplyOrigin.Europa, TransportMode.MaritimoLcl) => (25, 36),
            (SupplyOrigin.Europa, TransportMode.Aereo) => (2, 4),
            (SupplyOrigin.Europa, TransportMode.Courier) => (3, 5),
            (_, TransportMode.Aereo) => (3, 6),
            (_, TransportMode.Courier) => (3, 6),
            (SupplyOrigin.Nacional, _) => (1, 3),
            _ => throw new ArgumentException($"No transit for {origin} by {mode}.")
        };

        public static bool IsSea(TransportMode mode) => mode == TransportMode.MaritimoFcl || mode == TransportMode.MaritimoLcl;

        /// <summary>Draws a nominal lead time in days.</summary>
        public static int DrawDays(Random random, SupplyOrigin origin, TransportMode mode, EntryPort port,
            (int Min, int Max) production)
        {
            int days = Draw(random, production);
            if (origin == SupplyOrigin.Nacional)
                return days + Draw(random, (1, 3)) + Draw(random, (1, 3));

            bool sea = IsSea(mode), courier = mode == TransportMode.Courier;
            days += Draw(random, sea ? (3, 7) : courier ? (1, 2) : (2, 4));    // pickup to the origin port
            days += Draw(random, Transit(origin, mode));
            days += courier ? 0 : Draw(random, sea ? (2, 4) : (1, 2));          // unloading at destination
            days += Draw(random, sea ? (2, 4) : courier ? (1, 2) : (1, 3));     // customs release
            days += courier ? 0 : port switch                                   // national transport
            {
                EntryPort.Buenaventura => Draw(random, (1, 2)),
                EntryPort.Cartagena => Draw(random, (2, 3)),
                _ => 1
            };
            days += Draw(random, courier ? (1, 2) : (1, 3));                    // receiving and quality check
            return days;
        }

        private static int Draw(Random random, (int Min, int Max) range) => random.Next(range.Min, range.Max + 1);
    }
}
