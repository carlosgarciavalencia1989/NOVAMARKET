using System.Collections.Generic;

namespace NovaMarket.Simulation.Planning
{
    /// <summary>
    /// The four real SKUs of the "Temporada de fin de año" level with their sales history
    /// (January 2023 to August 2026, see Documentation/SIMULACION_PLANEACION.md, section 2).
    /// </summary>
    public static class PlanningCatalog
    {
        public const int Labial = 0;
        public const int Perfume = 1;
        public const int GiftSet = 2;
        public const int Cream = 3;

        /// <summary>Calendar year of history index 0 (January).</summary>
        public const int HistoryStartYear = 2023;

        public static IReadOnlyList<PlanningSku> Skus { get; } = new[]
        {
            new PlanningSku("lab", "CV10009", "Labial Maae coral 3,5 g", 48, 432_000, 1, 0.18, 0, 250_000,
                "Proveedor nacional", "Bogotá", 12, new[]
                {
                    7818, 6795, 6254, 10709, 10487, 6038, 7666, 6843, 9190, 5148, 12908, 9931,
                    7520, 9867, 7712, 10713, 7836, 5739, 6997, 9458, 11123, 8365, 8559, 9757,
                    6557, 6931, 5821, 9533, 10340, 7903, 5865, 8886, 7940, 8329, 5565, 8026,
                    8163, 7887, 9445, 11898, 9225, 8650, 12425, 8952
                }),
            new PlanningSku("per", "CV10001", "Perfume Malizmor 75 ml", 12, 960_000, 3, 0.26, 24, 900_000,
                CediCatalog.DonHernan, "Importado · Puerto de Cartagena", 10, new[]
                {
                    2227, 1536, 2080, 2151, 2041, 3792, 1983, 1698, 2386, 2411, 2632, 4383,
                    1823, 2353, 2237, 1929, 2241, 3389, 1932, 1705, 2045, 2293, 2591, 3477,
                    1524, 1423, 1617, 1368, 1395, 2964, 1561, 1408, 2272, 1928, 1725, 1798,
                    1391, 1341, 1748, 1705, 1581, 2136, 1386, 1554
                }),
            new PlanningSku("set", "CV10007", "Set regalo Lunaria Tribel", 6, 270_000, 2, 0.40, 0, 250_000,
                "Maquila local", "Itagüí", 4, new[]
                {
                    39, 61, 61, 67, 197, 103, 48, 52, 142, 83, 179, 249,
                    46, 93, 54, 84, 213, 129, 82, 82, 237, 97, 195, 400,
                    75, 83, 65, 116, 260, 160, 85, 108, 271, 103, 259, 396,
                    53, 126, 95, 125, 364, 236, 136, 174
                }),
            new PlanningSku("cre", "CV10021", "Crema facial Rimiri 50 g", 24, 528_000, 2, 0.20, 0, 250_000,
                "Planta propia", "Rionegro", 8, new[]
                {
                    2004, 1857, 1806, 1923, 2215, 1434, 1684, 1778, 1479, 1656, 2160, 2033,
                    1330, 1400, 1530, 1517, 1998, 1386, 1952, 1642, 1565, 1912, 1648, 1462,
                    2054, 1273, 1324, 1309, 1494, 1789, 1254, 1525, 1393, 1033, 1575, 1604,
                    1116, 1546, 1452, 1251, 1441, 1227, 1708, 1115
                })
        };
    }
}
