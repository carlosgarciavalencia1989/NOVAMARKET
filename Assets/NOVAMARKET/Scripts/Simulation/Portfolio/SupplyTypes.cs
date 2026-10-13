namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>Where a SKU is bought (DISENO_JUEGO_V2.md, sections 3.1 and 8.1).</summary>
    public enum SupplyOrigin
    {
        Nacional,
        Europa,
        China,
        EstadosUnidos,
        Japon
    }

    /// <summary>Default transport mode of a SKU.</summary>
    public enum TransportMode
    {
        Carretera,
        MaritimoFcl,
        MaritimoLcl,
        Aereo,
        Courier
    }

    /// <summary>Port or airport where an import enters Colombia.</summary>
    public enum EntryPort
    {
        Ninguno,
        Buenaventura,
        Cartagena,
        Rionegro
    }

    /// <summary>Demand pattern a synthetic SKU is generated with (real SKUs are classified from their data).</summary>
    public enum DemandPattern
    {
        Stable,
        Seasonal,
        Erratic,
        Intermittent,
        Launch,
        Discontinued
    }

    /// <summary>Spanish names of the supply enums.</summary>
    public static class SupplyNames
    {
        public static string Name(SupplyOrigin origin) => origin switch
        {
            SupplyOrigin.Nacional => "Nacional",
            SupplyOrigin.Europa => "Europa",
            SupplyOrigin.China => "China",
            SupplyOrigin.EstadosUnidos => "Estados Unidos",
            _ => "Japón"
        };

        public static string Name(TransportMode mode) => mode switch
        {
            TransportMode.Carretera => "Carretera",
            TransportMode.MaritimoFcl => "Marítimo FCL",
            TransportMode.MaritimoLcl => "Marítimo LCL",
            TransportMode.Aereo => "Aéreo",
            _ => "Courier"
        };

        public static string Name(EntryPort port) => port switch
        {
            EntryPort.Buenaventura => "Buenaventura",
            EntryPort.Cartagena => "Cartagena",
            EntryPort.Rionegro => "Rionegro",
            _ => "—"
        };

        public static string Name(DemandPattern pattern) => pattern switch
        {
            DemandPattern.Stable => "Estable",
            DemandPattern.Seasonal => "Estacional",
            DemandPattern.Erratic => "Errático",
            DemandPattern.Intermittent => "Intermitente",
            DemandPattern.Launch => "Lanzamiento",
            _ => "Descontinuado"
        };
    }
}
