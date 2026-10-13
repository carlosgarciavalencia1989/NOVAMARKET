using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Levels
{
    public enum DialogueMood
    {
        Neutral,
        Happy,
        Worried,
        Serious
    }

    /// <summary>A line said by a character (id of its CharacterData: "andres", "sofia").</summary>
    public sealed class DialogueLine
    {
        public DialogueLine(string characterId, DialogueMood mood, string text)
        {
            CharacterId = characterId;
            Mood = mood;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string CharacterId { get; }
        public DialogueMood Mood { get; }
        public string Text { get; }

        public override string ToString() => $"{CharacterId}: {Text}";
    }

    /// <summary>
    /// What Andrés (mentor, S&amp;OP director) and Sofía (inventory control, she hands over the ERP
    /// data) say in the levels 1.1 and 1.2. The UI draws them; the levels choose which ones to show.
    /// </summary>
    public static class LevelDialogue
    {
        public const string Andres = "andres";
        public const string Sofia = "sofia";

        private static DialogueLine A(DialogueMood mood, string text) => new DialogueLine(Andres, mood, text);
        private static DialogueLine S(DialogueMood mood, string text) => new DialogueLine(Sofia, mood, text);

        // ------------------------------------------------------------------ 1.1 Reunir la historia

        public static IReadOnlyList<DialogueLine> GatherIntro(IReadOnlyList<string> codes) => new[]
        {
            A(DialogueMood.Neutral, "Bienvenido al equipo de planeación. Todo pronóstico empieza por la historia: " +
                                    "el modelo solo aprende lo que está en ella. Hoy la vas a reunir."),
            S(DialogueMood.Neutral, $"Hola. Necesito la facturación real de {string.Join(", ", codes)}, de ene-23 a ago-26. " +
                                    "Sácala del NOVA ERP, pantalla Facturación, y descárgala a tu hoja con «Descargar a hoja»."),
            A(DialogueMood.Neutral, "Arma la tabla como en el Atlas: un SKU por fila y un mes por columna. " +
                                    "Con 24 meses o más ya ves dos ciclos de temporada.")
        };

        public static DialogueLine ProjectionWarning { get; } = A(DialogueMood.Worried,
            "Mira la columna que dice «(proy.)». Es un promedio móvil del mes en curso, no una venta. " +
            "Si la dejas, mezclas pronóstico con historia y todo lo que sigue hereda el error. Consulta sin proyección o borra esa columna.");

        public static DialogueLine PeriodsHint { get; } = A(DialogueMood.Neutral,
            "Encima de los meses numera los periodos: escribe 1 en C1 y 2 en D1, selecciona las dos celdas y " +
            "arrastra la esquina hasta AT1. La hoja sigue la serie hasta 44.");

        public static DialogueLine TotalsHint { get; } = A(DialogueMood.Neutral,
            "En AW3 te dejé =SUMA(AI3:AT3): suma los últimos 12 meses del primer SKU. Arrastra la esquina de la celda " +
            "hacia abajo y la fórmula se ajusta sola a cada fila.");

        public static DialogueLine GatherSuccess { get; } = A(DialogueMood.Happy,
            "Historia completa: 44 meses, solo venta facturada y un SKU por fila. Esta tabla es la base de todo el mundo de Pronóstico.");

        public static DialogueLine GatherWrongQuery { get; } = A(DialogueMood.Serious,
            "La tabla no es la que pidió Sofía. Revisa los SKU, que vaya de ene-23 a ago-26 y que no quede ninguna proyección.");

        public static DialogueLine GatherWrongTotals { get; } = A(DialogueMood.Serious,
            "Los totales de 12 meses no cuadran. Deben sumar de sep-25 a ago-26: columnas AI a AT de cada fila.");

        // ------------------------------------------------------------------ 1.2 Limpiar los datos

        public static IReadOnlyList<DialogueLine> CleanIntro(int skus) => new[]
        {
            S(DialogueMood.Worried, $"Te pasé la facturación de {skus} SKU, pero viene con problemas: ceros que no son reales, " +
                                    "un pedido raro de un cliente, meses antes de un lanzamiento y algo raro con las unidades. " +
                                    "Revísala antes de que Andrés la use."),
            A(DialogueMood.Neutral, "Descárgala y te armo la pestaña «Serie limpia» con una copia. Marca cada problema con su tipo " +
                                    "y decide si imputas, excluyes o conviertes. Corrige solo en la Serie limpia."),
            A(DialogueMood.Serious, "Una advertencia: no todo pico es un error. Si un mes alto se repite cada año, es la temporada.")
        };

        public static DialogueLine Hint(Erp.DataIssueKind kind) => kind switch
        {
            Erp.DataIssueKind.FalseZero => S(DialogueMood.Neutral,
                "Un cero aislado entre meses con venta casi siempre es un agotado o un error de registro. " +
                "Si el mes es fuerte para el SKU, imputa con el mismo mes de los otros años."),
            Erp.DataIssueKind.OneOffOrder => S(DialogueMood.Neutral,
                "Si un mes se dispara, abre el detalle de facturas de ese mes en el ERP: ahí ves qué cliente lo compró y por qué."),
            Erp.DataIssueKind.PreLaunch => S(DialogueMood.Neutral,
                "Cuando un SKU arranca con puros ceros, revisa si ya existía. Los meses antes del lanzamiento no son demanda: se excluyen."),
            _ => S(DialogueMood.Neutral,
                "Mira la columna UM. Si un SKU dice CJ y los demás UN, está en cajas. El maestro de materiales dice cuántas unidades trae cada caja.")
        };

        public static DialogueLine CleanSuccess { get; } = A(DialogueMood.Happy,
            "Serie limpia. Cada corrección quedó documentada y el pronóstico va a aprender de la demanda real, no de los accidentes del ERP.");

        public static DialogueLine CleanMissing(int missing) => A(DialogueMood.Serious,
            $"Te faltan {missing} {(missing == 1 ? "problema" : "problemas")} por encontrar. Mira cómo dañarían el pronóstico:");

        public static DialogueLine CleanBadCorrection { get; } = A(DialogueMood.Worried,
            "Encontraste los problemas, pero algunas correcciones no quedaron bien. Mira la diferencia en el error del pronóstico:");

        private static readonly string[] MonthNames =
        {
            "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
        };

        /// <summary>Andrés shows what the model would learn wrong from a SKU of the player's series.</summary>
        public static DialogueLine Impact(SkuImpact sku)
        {
            if (sku.LevelIsWorse)
                return A(DialogueMood.Serious,
                    $"Con tu serie, el modelo creería que {sku.Code} vende {HudFormat.Number((long)Math.Round(sku.PlayerLevel))} " +
                    $"unidades al mes en promedio, y son {HudFormat.Number((long)Math.Round(sku.CleanLevel))}: todo el pronóstico " +
                    $"quedaría {(sku.PlayerLevel > sku.CleanLevel ? "inflado" : "corto")}.");
            string month = MonthNames[sku.Month];
            return A(DialogueMood.Serious,
                $"Con tu serie, el índice de {month} de {sku.Code} sería {HudFormat.Decimal(sku.PlayerIndex, 2)} en vez de " +
                $"{HudFormat.Decimal(sku.CleanIndex, 2)}: cada {month} el pronóstico quedaría " +
                $"{(sku.PlayerIndex > sku.CleanIndex ? "por encima" : "por debajo")} de la venta.");
        }

        public static DialogueLine CleanChangedGoodData { get; } = A(DialogueMood.Serious,
            "Cambiaste datos que estaban bien. Un pico que se repite cada diciembre es temporada, no un error: si lo borras, " +
            "el modelo deja de esperarlo.");
    }
}
