using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>
    /// Level 1.1 "Reunir la historia" (Atlas step p1, guided). Sofía asks for the billing of four real
    /// SKUs from ene-23 to ago-26. The player queries NOVA ERP, downloads to the sheet, leaves out the
    /// projection column (the Atlas trap), numbers the periods 1…44 by dragging and drags the 12-month
    /// total (=SUMA(AI3:AT3), given in AW3) to every SKU.
    /// Stars: history ready (right query and totals) · periods row · no Atlas help.
    /// </summary>
    public sealed class GatherHistoryLevel : LevelSession
    {
        public const int SkuCount = 4;
        public const string TotalHeader = "Total últimos 12 meses";

        /// <summary>Column AW, where the 12-month totals are delivered.</summary>
        public static readonly int TotalColumn = CellAddress.ColumnIndex("AW");

        public const string TopicQuery = "consulta";
        public const string TopicPeriods = "periodos";
        public const string TopicTotals = "totales";

        private static readonly string[] Pool = { "CV10009", "CV10001", "CV10021", "CV10017", "CV10012", "CV10000", "CV10015" };

        private readonly List<string> codes;

        public GatherHistoryLevel(Portfolio.Portfolio portfolio, int seed)
            : base(ForecastWorld.GatherHistory, new ErpDataSet(portfolio, seed), seed)
        {
            var random = new Random(seed);
            codes = Pool.OrderBy(_ => random.Next()).Take(SkuCount).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        public override IReadOnlyList<string> RequestedCodes => codes;
        public override IReadOnlyList<DialogueLine> Intro => LevelDialogue.GatherIntro(codes);
        public override IReadOnlyList<string> HelpTopics { get; } = new[] { TopicQuery, TopicPeriods, TopicTotals };

        /// <summary>The tab of the last billing download (the history table).</summary>
        public string HistorySheet { get; private set; }

        private Worksheet Sheet => Book.Sheet(HistorySheet);

        protected override void OnDownloaded(ErpTable table, Worksheet sheet)
        {
            if (!IsBillingByMonth(table)) return;
            HistorySheet = sheet.Name;
            Book.Batch(() =>
            {
                sheet.SetText(new CellAddress(TotalColumn, ErpTable.HeaderRow), TotalHeader);
                // Guided mode: the first period and the first total are given.
                sheet.SetNumber(new CellAddress(HistoryTable.FirstMonthColumn, ErpTable.TitleRow), 1);
                if (table.Rows.Count > 0) sheet.SetInput(new CellAddress(TotalColumn, ErpTable.FirstDataRow), "=SUMA(AI3:AT3)");
                sheet.SetFormat(new CellRange(new CellAddress(TotalColumn, ErpTable.FirstDataRow),
                    new CellAddress(TotalColumn, ErpTable.FirstDataRow + Math.Max(0, table.Rows.Count - 1))), CellFormat.Number(0));
            });
            if (table.Headers.Any(h => h.EndsWith(NovaErp.ProjectionSuffix))) Say(LevelDialogue.ProjectionWarning);
            Say(LevelDialogue.PeriodsHint);
            Say(LevelDialogue.TotalsHint);
        }

        // ------------------------------------------------------------------ checks

        /// <summary>The tab lists exactly the requested SKUs with the 44 months ene-23…ago-26.</summary>
        public bool QueryIsRight()
        {
            Worksheet sheet = Sheet;
            if (sheet == null) return false;
            List<string> listed = HistoryTable.Codes(sheet);
            if (listed.Count != codes.Count || !listed.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(codes)) return false;
            for (int m = 0; m < SalesCalendar.Months; m++)
                if (sheet.Value(new CellAddress(HistoryTable.MonthColumn(m), ErpTable.HeaderRow)).Text != SalesCalendar.Label(m))
                    return false;
            return true;
        }

        /// <summary>True while a projection column still has numbers in it.</summary>
        public bool HasProjection()
        {
            Worksheet sheet = Sheet;
            if (sheet == null) return false;
            for (int column = 0; column < sheet.UsedColumns; column++)
            {
                CellValue header = sheet.Value(new CellAddress(column, ErpTable.HeaderRow));
                if (!header.IsText || !header.Text.EndsWith(NovaErp.ProjectionSuffix)) continue;
                for (int row = ErpTable.FirstDataRow; row < sheet.UsedRows; row++)
                    if (sheet.Value(new CellAddress(column, row)).IsNumber) return true;
            }
            return false;
        }

        public bool PeriodsAreRight()
        {
            Worksheet sheet = Sheet;
            if (sheet == null) return false;
            for (int m = 0; m < SalesCalendar.Months; m++)
            {
                CellValue value = sheet.Value(new CellAddress(HistoryTable.MonthColumn(m), ErpTable.TitleRow));
                if (!value.IsNumber || !Tolerance.Matches(value.Number, m + 1)) return false;
            }
            return true;
        }

        public bool TotalsAreRight(out bool allFormulas)
        {
            allFormulas = false;
            Worksheet sheet = Sheet;
            if (sheet == null) return false;
            allFormulas = true;
            foreach (string code in codes)
            {
                int row = HistoryTable.FindRow(sheet, code);
                if (row < 0) return false;
                var cell = new CellAddress(TotalColumn, row);
                CellValue value = sheet.Value(cell);
                int expected = SeriesStatistics.LastMonthsTotal(Data.Billed(code));
                if (!value.IsNumber || !Tolerance.Matches(value.Number, expected)) return false;
                allFormulas &= sheet.IsFormula(cell);
            }
            return true;
        }

        public override IReadOnlyList<LevelObjective> Objectives() => new[]
        {
            new LevelObjective($"Consulta en el ERP la facturación de {string.Join(", ", codes)} de ene-23 a ago-26 y descárgala",
                QueryIsRight()),
            new LevelObjective("Descarga solo la demanda real", HistorySheet != null && !HasProjection()),
            new LevelObjective("Numera los periodos 1 a 44 en la fila 1 (C1:AT1)", PeriodsAreRight()),
            new LevelObjective("Total de los últimos 12 meses de cada SKU en la columna AW", TotalsAreRight(out _))
        };

        protected override LevelResult Evaluate()
        {
            bool query = QueryIsRight(), projection = HasProjection(), periods = PeriodsAreRight();
            bool totals = TotalsAreRight(out bool formulas);
            bool ready = query && !projection && totals;

            var criteria = new[]
            {
                new StarCriterion("Historia lista", ready, ready
                    ? "Los 4 SKU, 44 meses de venta facturada y el total de 12 meses de cada uno."
                    : !query ? "La consulta no tiene los SKU o los meses pedidos."
                    : projection ? "Quedó la columna de proyección: no es venta."
                    : "Los totales de 12 meses no cuadran."),
                new StarCriterion("Fila de periodos 1…44", periods, periods
                    ? "Periodos numerados con el arrastre." : "La fila 1 debe ir de 1 en C1 a 44 en AT1."),
                new StarCriterion("Sin ayuda del Atlas", !Help.Used, Help.Used
                    ? $"Pediste ayuda al Atlas {Help.Topics.Count} {(Help.Topics.Count == 1 ? "vez" : "veces")}." : "Lo resolviste solo.")
            };

            var debrief = new List<DialogueLine>();
            if (ready) debrief.Add(LevelDialogue.GatherSuccess);
            else if (!query || projection) debrief.Add(LevelDialogue.GatherWrongQuery);
            else debrief.Add(LevelDialogue.GatherWrongTotals);
            if (projection) debrief.Add(LevelDialogue.ProjectionWarning);

            var lessons = new List<string>
            {
                "Arma la historia con un SKU por fila y un mes por columna. Lo ideal son 24 meses o más: con menos de 12 no se " +
                "puede medir la estacionalidad.",
                "No mezcles venta real con pronósticos en la misma tabla: una columna proyectada parece venta, pero es un " +
                "promedio móvil.",
                "La venta es la demanda que se pudo atender: si hubo agotados, subestima lo que el mercado quería."
            };
            return new LevelResult(Info, criteria, ready && formulas, debrief, lessons);
        }

        protected override AtlasAnswer Explain(string topic) => topic switch
        {
            TopicQuery => new AtlasAnswer("Reunir la historia (p1)", new[]
            {
                "Abre NOVA ERP → Facturación.",
                $"Materiales: {string.Join(", ", codes)}. Desde ene-23 hasta ago-26.",
                "Quita la marca «Incluir proyección del mes en curso»: esa columna es un promedio móvil, no venta.",
                "Pulsa «Descargar a hoja»: los meses quedan en las columnas C a AT."
            }),
            TopicPeriods => new AtlasAnswer("Fila de periodos (p1)", new[]
            {
                "Escribe 1 en C1 y 2 en D1.",
                "Selecciona C1:D1 y arrastra la esquina inferior derecha hasta AT1.",
                "La hoja sigue la serie: 3, 4… hasta 44 en AT1."
            }),
            TopicTotals => new AtlasAnswer("Total de los últimos 12 meses (p1)", new[]
            {
                "Los últimos 12 meses van de sep-25 (columna AI) a ago-26 (columna AT).",
                "En AW3: =SUMA(AI3:AT3).",
                "Arrastra AW3 hacia abajo hasta el último SKU: la fila de cada referencia cambia sola."
            }),
            _ => null
        };
    }
}
