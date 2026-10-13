using System.Collections.Generic;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>
    /// A screen of NOVA ERP with its SAP equivalent (shown small, as educational data) and the world
    /// of the planner where it unlocks (DISENO_JUEGO_V2.md, section 4).
    /// </summary>
    public sealed class ErpScreen
    {
        public ErpScreen(string id, string name, string purpose, string sapEquivalent, int unlockWorld)
        {
            Id = id;
            Name = name;
            Purpose = purpose;
            SapEquivalent = sapEquivalent;
            UnlockWorld = unlockWorld;
        }

        public string Id { get; }
        public string Name { get; }
        public string Purpose { get; }
        public string SapEquivalent { get; }

        /// <summary>First planner world where the screen can be used.</summary>
        public int UnlockWorld { get; }

        public bool IsAvailable(int world) => world >= UnlockWorld;
    }

    /// <summary>The screens of NOVA ERP, in menu order.</summary>
    public static class ErpScreens
    {
        public const string Billing = "facturacion";
        public const string MaterialMaster = "maestro-materiales";

        public static IReadOnlyList<ErpScreen> All { get; } = new[]
        {
            new ErpScreen(Billing, "Facturación", "La demanda real se mira en la facturación: unidades facturadas por material y mes.",
                "VF05 / análisis de ventas", 1),
            new ErpScreen(MaterialMaster, "Maestro de materiales", "LT, MOQ, unidad de empaque, costo y proveedor de cada material.",
                "MM03", 1),
            new ErpScreen("historial-ventas", "Historial de ventas", "Ventas por semana y mes, con las promociones marcadas.",
                "Informes de ventas / consumo", 2),
            new ErpScreen("stock-almacen", "Stock por almacén", "Inventario disponible, bloqueado y en calidad.", "MMBE / MB52", 3),
            new ErpScreen("necesidades", "Situación de necesidades", "Stock, entradas y salidas futuras por fecha.", "MD04", 3),
            new ErpScreen("solped", "Crear solicitud de pedido", "Crear la SolPed: SKU, cantidad y fecha requerida.", "ME51N", 3),
            new ErpScreen("ordenes-compra", "Órdenes de compra", "Paula convierte la SolPed en OC; se ven las fechas confirmadas.",
                "ME21N / ME23N", 3),
            new ErpScreen("entregas-pendientes", "Entregas pendientes", "Seguimiento de las OC abiertas y su etapa.", "ME2M / ME2N", 3),
            new ErpScreen("seguimiento-importaciones", "Seguimiento de importaciones",
                "Todas las fases de la OC, de la creación al inventario disponible.", "Transacción Z de seguimiento", 3),
            new ErpScreen("entradas-mercancia", "Entradas de mercancía", "Registro del recibo en el CEDI.", "MIGO", 3),
            new ErpScreen("historial-lead-times", "Historial de lead times", "Fecha prometida contra fecha real, por proveedor y etapa.",
                "Informes de compras", 3)
        };

        public static ErpScreen Find(string id)
        {
            foreach (ErpScreen screen in All) if (screen.Id == id) return screen;
            return null;
        }
    }
}
