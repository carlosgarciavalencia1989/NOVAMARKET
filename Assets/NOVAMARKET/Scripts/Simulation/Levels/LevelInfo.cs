using System.Collections.Generic;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>How much of each formula the game fills in (DISENO_JUEGO_V2.md, section 5).</summary>
    public enum ScaffoldMode
    {
        /// <summary>Most boxes filled; Andrés explains every term.</summary>
        Guided,

        /// <summary>The whole formula with empty boxes; a wrong box gets a hint.</summary>
        EmptyBoxes,

        /// <summary>Only the data and the final result.</summary>
        Free
    }

    /// <summary>The gem a level gives: each level has its own shape, colour and value.</summary>
    public sealed class GemReward
    {
        public GemReward(string name, string shape, string colorHex, int points)
        {
            Name = name;
            Shape = shape;
            ColorHex = colorHex;
            Points = points;
        }

        public string Name { get; }
        public string Shape { get; }
        public string ColorHex { get; }
        public int Points { get; }
    }

    /// <summary>A level of a world: its Atlas step, scaffold mode and gem.</summary>
    public sealed class LevelInfo
    {
        public LevelInfo(string id, string title, string atlasStep, string summary, ScaffoldMode mode, GemReward gem, bool isBuilt)
        {
            Id = id;
            Title = title;
            AtlasStep = atlasStep;
            Summary = summary;
            Mode = mode;
            Gem = gem;
            IsBuilt = isBuilt;
        }

        /// <summary>"1.1", "1.2"…</summary>
        public string Id { get; }

        public string Title { get; }

        /// <summary>Step of the Atlas flowchart ("p1", "p5–p6").</summary>
        public string AtlasStep { get; }

        public string Summary { get; }
        public ScaffoldMode Mode { get; }
        public GemReward Gem { get; }

        /// <summary>False for levels shown as locked until they are built.</summary>
        public bool IsBuilt { get; }
    }

    /// <summary>World 1 of the planner, Forecasting: one level per step of the Atlas flowchart.</summary>
    public static class ForecastWorld
    {
        public const int Number = 1;
        public const string Name = "Pronóstico";

        public static LevelInfo GatherHistory { get; } = new LevelInfo("1.1", "Reunir la historia", "p1",
            "Descarga del ERP la facturación por SKU y por mes y arma la tabla de historia.", ScaffoldMode.Guided,
            new GemReward("Cuarzo de la historia", "hexágono", "#E8C872", 300), true);

        public static LevelInfo CleanData { get; } = new LevelInfo("1.2", "Limpiar los datos", "p2",
            "Detecta ceros que no son reales, pedidos atípicos, meses antes del lanzamiento y unidades mezcladas, y corrígelos.",
            ScaffoldMode.EmptyBoxes, new GemReward("Ágata de los datos", "rombo", "#3FA7A0", 400), true);

        public static IReadOnlyList<LevelInfo> Levels { get; } = new[]
        {
            GatherHistory,
            CleanData,
            Locked("1.3", "Diagnosticar el patrón", "p3", "CV, pendiente con R² e índices estacionales."),
            Locked("1.4", "Elegir métodos candidatos", "p4", "El patrón decide el método."),
            Locked("1.5", "Calcular y optimizar", "p5–p6", "Pronóstico a un paso y búsqueda de α y β."),
            Locked("1.6", "Medir el error y seleccionar", "p7–p8", "WAPE, sesgo y tracking signal."),
            Locked("1.7", "Proyectar el horizonte", "p9", "Entrega el pronóstico estadístico a Gestión de la demanda.")
        };

        private static LevelInfo Locked(string id, string title, string step, string summary) =>
            new LevelInfo(id, title, step, summary, ScaffoldMode.EmptyBoxes, null, false);
    }
}
