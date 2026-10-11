using System;

namespace NovaMarket.Simulation
{
    /// <summary>A supplier that sends receiving trucks.</summary>
    public sealed class Supplier
    {
        public Supplier(string name, string origin, string product, bool isImport)
        {
            Name = name;
            Origin = origin;
            Product = product;
            IsImport = isImport;
        }

        public string Name { get; }
        public string Origin { get; }
        public string Product { get; }
        public bool IsImport { get; }
    }

    /// <summary>Example suppliers, dispatch destinations and Colombian truck plates.</summary>
    public static class CediCatalog
    {
        public const string DonHernan = "Don Hernán";

        public static readonly Supplier[] Suppliers =
        {
            new Supplier(DonHernan, "Importado · Puerto de Cartagena", "electrodomésticos", true),
            new Supplier("Lácteos del Oriente", "Rionegro", "lácteos", false),
            new Supplier("Confecciones Itagüí", "Itagüí", "ropa", false),
            new Supplier("Café La Montaña", "Fredonia", "café", false),
            new Supplier("Aseo Aburrá", "Girardota", "productos de aseo", false)
        };

        /// <summary>Municipalities and neighbourhoods of the Aburrá valley the CEDI serves.</summary>
        public static readonly string[] Destinations =
        {
            "Envigado", "Itagüí", "Bello", "Sabaneta", "La Estrella", "Copacabana", "Laureles", "El Poblado", "Caldas"
        };

        public static readonly string[] DispatchProducts =
        {
            "abarrotes", "bebidas", "productos de aseo", "lácteos", "electrodomésticos"
        };

        /// <summary>Cargo-style plate: S/T/W, two letters and three digits ("SXR 482").</summary>
        public static string Plate(Random random)
        {
            const string first = "STW";
            const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
            return $"{first[random.Next(first.Length)]}{letters[random.Next(letters.Length)]}" +
                   $"{letters[random.Next(letters.Length)]} {random.Next(100, 1000)}";
        }
    }
}
