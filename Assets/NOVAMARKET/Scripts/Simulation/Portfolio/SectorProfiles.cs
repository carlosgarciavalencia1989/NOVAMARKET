using System.Collections.Generic;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// Industry profiles the portfolio generator works from (DISENO_JUEGO_V2.md, section 8.1). Only
    /// cosmetics exists for now: 14 real Atlas SKUs + 186 synthetic ones in 7 families. Perfumes come
    /// from Europe and sets and brushes from China by sea; lipsticks, creams, eyeliners and personal
    /// care are national, by road. Pack sizes and costs follow the season level's SKUs.
    /// </summary>
    public static class SectorProfiles
    {
        public const string CosmeticsId = "cosmetica";

        /// <summary>Patterns weights order: Stable, Seasonal, Erratic, Intermittent, Launch, Discontinued.</summary>
        public static IReadOnlyList<FamilyProfile> For(string sectorId) =>
            sectorId == CosmeticsId ? Cosmetics : null;

        private static readonly string[] Brands = { "Lunaria", "Veridia", "Kaoba", "Brisa Andina", "Selvia" };

        public static IReadOnlyList<FamilyProfile> Cosmetics { get; } = new[]
        {
            new FamilyProfile
            {
                Name = "Labiales", TemplateCode = "CV10009", SyntheticCount = 50, Origin = SupplyOrigin.Nacional,
                Suppliers = new[] { "Color Andino S.A.S. (Bogotá)", "Maquila Itagüí" }, ProductionDays = (7, 21),
                UnitsPerBox = 48, MoqBoxes = (2, 6), UnitCost = (7_000, 14_000), MedianUnits = 900,
                PatternWeights = new[] { 0.45, 0.15, 0.13, 0.07, 0.12, 0.08 },
                NameFormat = "Labial {0} {1} {2} {3}",
                NameParts = new[]
                {
                    new[] { "Lunaria", "Veridia", "Kaoba" },
                    new[] { "Maae", "Solei", "Kuyay", "Tierra", "Mate" },
                    new[] { "rojo cereza", "coral", "nude arena", "malva", "vino", "rosa palo", "terracota", "ciruela",
                            "fucsia", "durazno", "café canela", "dorado", "marrón", "salmón" },
                    new[] { "3,5 g", "4 g" }
                }
            },
            new FamilyProfile
            {
                Name = "Perfumes", TemplateCode = "CV10001", SyntheticCount = 30, Origin = SupplyOrigin.Europa,
                Modes = new[] { TransportMode.MaritimoFcl, TransportMode.MaritimoLcl },
                Suppliers = new[] { "Don Hernán · Perfumería Hernán (España)", "Parfums Lumière (Francia)" },
                ProductionDays = (10, 30), UnitsPerBox = 12, MoqBoxes = (20, 60), UnitCost = (55_000, 120_000),
                MedianUnits = 600, PatternWeights = new[] { 0.15, 0.45, 0.15, 0.05, 0.12, 0.08 },
                NameFormat = "Perfume {0} {1} {2} {3}",
                NameParts = new[]
                {
                    new[] { "mujer", "hombre" },
                    new[] { "Kaoba", "Veridia", "Lunaria" },
                    new[] { "Aurea", "Noctis", "Ámbar", "Lirio", "Ceiba", "Orquídea", "Selva", "Brisa", "Café Real", "Nevado" },
                    new[] { "50 ml", "75 ml", "100 ml" }
                }
            },
            new FamilyProfile
            {
                Name = "Cremas", TemplateCode = "CV10021", SyntheticCount = 35, Origin = SupplyOrigin.Nacional,
                Suppliers = new[] { "Planta propia (Rionegro)", "Maquila Itagüí" }, ProductionDays = (7, 18),
                UnitsPerBox = 24, MoqBoxes = (2, 8), UnitCost = (15_000, 35_000), MedianUnits = 800,
                PatternWeights = new[] { 0.5, 0.15, 0.1, 0.05, 0.1, 0.1 },
                NameFormat = "Crema {0} {1} {2} {3}",
                NameParts = new[]
                {
                    new[] { "facial", "corporal", "de manos", "contorno de ojos", "de noche" },
                    new[] { "Brisa Andina", "Lunaria", "Selvia" },
                    new[] { "Aloe", "Cacao", "Quinua", "Uchuva", "Café", "Coco", "Rosa mosqueta", "Caléndula" },
                    new[] { "50 g", "75 ml", "200 ml", "400 ml" }
                }
            },
            new FamilyProfile
            {
                Name = "Sets de regalo", TemplateCode = "CV10007", SyntheticCount = 20, Origin = SupplyOrigin.China,
                Modes = new[] { TransportMode.MaritimoFcl, TransportMode.MaritimoLcl },
                Suppliers = new[] { "Ningbo Beauty Pack (China)", "Shenzhen Gift Co. (China)" }, ProductionDays = (15, 30),
                UnitsPerBox = 6, MoqBoxes = (20, 50), UnitCost = (35_000, 70_000), MedianUnits = 90,
                PatternWeights = new[] { 0.05, 0.6, 0.15, 0.1, 0.1, 0 },
                NameFormat = "Set regalo {0} {1} {2}",
                NameParts = new[]
                {
                    Brands,
                    new[] { "Aurora", "Navidad", "Madres", "Amor y amistad", "Clásico", "Viaje", "Esencial", "Noche" },
                    new[] { "estuche", "mini", "Ed. especial", "caja" }
                }
            },
            new FamilyProfile
            {
                Name = "Brochas", TemplateCode = "CV10018", SyntheticCount = 15, Origin = SupplyOrigin.China,
                Modes = new[] { TransportMode.MaritimoLcl, TransportMode.MaritimoFcl },
                Suppliers = new[] { "Shenzhen Brush Co. (China)" }, ProductionDays = (10, 25),
                UnitsPerBox = 12, MoqBoxes = (15, 40), UnitCost = (20_000, 50_000), MedianUnits = 120,
                PatternWeights = new[] { 0.3, 0.2, 0.2, 0.2, 0.1, 0 },
                NameFormat = "{0} {1} {2}",
                NameParts = new[]
                {
                    new[] { "Set brochas", "Brocha base", "Brocha rubor", "Brocha polvo", "Brocha sombras", "Brocha kabuki" },
                    new[] { "Selvia", "Lunaria", "Veridia" },
                    new[] { "Natural", "Pro", "Viaje", "Suave", "Bambú" }
                }
            },
            new FamilyProfile
            {
                Name = "Delineadores", TemplateCode = "CV10025", SyntheticCount = 25, Origin = SupplyOrigin.Nacional,
                Suppliers = new[] { "Color Andino S.A.S. (Bogotá)" }, ProductionDays = (7, 21),
                UnitsPerBox = 48, MoqBoxes = (1, 4), UnitCost = (6_000, 12_000), MedianUnits = 150,
                PatternWeights = new[] { 0.35, 0.15, 0.15, 0.2, 0.1, 0.05 },
                NameFormat = "Delineador {0} {1} {2} {3}",
                NameParts = new[]
                {
                    new[] { "Veridia", "Lunaria" },
                    new[] { "Losolsa", "Trazo", "Mirada", "Fino" },
                    new[] { "negro", "café oscuro", "azul noche", "verde oliva", "vinotinto", "gris" },
                    new[] { "2,5 ml", "1,2 g" }
                }
            },
            new FamilyProfile
            {
                Name = "Cuidado personal", TemplateCode = "CV10000", SyntheticCount = 11, Origin = SupplyOrigin.Nacional,
                Suppliers = new[] { "Laboratorios Kaoba (Medellín)" }, ProductionDays = (7, 14),
                UnitsPerBox = 24, MoqBoxes = (2, 6), UnitCost = (8_000, 16_000), MedianUnits = 450,
                PatternWeights = new[] { 0.4, 0.3, 0.1, 0.1, 0.05, 0.05 },
                NameFormat = "{0} Kaoba {1} {2}",
                NameParts = new[]
                {
                    new[] { "Espuma afeitar", "Gel afeitar", "Bálsamo after shave", "Desodorante", "Jabón facial" },
                    new[] { "Luven", "Bosque", "Marino", "Cedro", "Sport" },
                    new[] { "150 ml", "200 ml", "100 ml" }
                }
            }
        };
    }
}
