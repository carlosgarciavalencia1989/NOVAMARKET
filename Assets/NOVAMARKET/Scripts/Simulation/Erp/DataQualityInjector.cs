using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Portfolio;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>A problem the level wants in its data, and the SKUs it may land on.</summary>
    public sealed class IssueRequest
    {
        public IssueRequest(DataIssueKind kind, params string[] candidates)
        {
            Kind = kind;
            Candidates = candidates;
        }

        public DataIssueKind Kind { get; }
        public IReadOnlyList<string> Candidates { get; }

        /// <summary>For a false zero on CV10004: use the real case of the Atlas (jun-24).</summary>
        public bool PreferAtlasCase { get; set; }
    }

    /// <summary>
    /// Plants data quality problems in the ERP billing (DISENO_JUEGO_V2.md §4.2 and §11) and records
    /// the answers the Atlas rules give (step p2): a zero in a seasonal month is imputed with the same
    /// month of the other years; elsewhere the neighbours' average is also accepted; a one-off order is
    /// taken out (or the month imputed); months before the launch are excluded; boxes are converted
    /// with the units per box of the material master. Each SKU gets at most one problem.
    /// </summary>
    public static class DataQualityInjector
    {
        /// <summary>The isolated zero the Atlas documents: CV10004, jun-24.</summary>
        public const string AtlasZeroCode = "CV10004";
        public const int AtlasZeroMonth = 17;

        /// <summary>A month counts as seasonal for imputation when its index is at least this.</summary>
        public const double SeasonalPeak = 1.2;

        private static readonly string[] OneOffCustomers =
        {
            "Mayorista Caribe (apertura de tiendas)",
            "Licitación de una caja de compensación",
            "Exportación puntual a Panamá",
            "Cadena Belleza Total (inventario inicial)"
        };

        public static ErpDataSet Inject(Portfolio.Portfolio portfolio, IEnumerable<IssueRequest> requests, int seed)
        {
            var random = new Random(seed);
            var billed = new Dictionary<string, int[]>();
            var units = new Dictionary<string, string>();
            var issues = new List<DataIssue>();
            var orders = new List<OneOffOrder>();
            var used = new HashSet<string>();

            foreach (IssueRequest request in requests)
            {
                List<PortfolioSku> candidates = request.Candidates.Select(portfolio.Find)
                    .Where(s => s != null && !used.Contains(s.Code) && Qualifies(request.Kind, s)).ToList();
                if (candidates.Count == 0)
                    throw new InvalidOperationException($"No free SKU qualifies for {request.Kind}.");

                PortfolioSku sku = request.PreferAtlasCase && candidates.Any(c => c.Code == AtlasZeroCode)
                    ? candidates.First(c => c.Code == AtlasZeroCode)
                    : candidates[random.Next(candidates.Count)];
                used.Add(sku.Code);
                int[] raw = sku.History.ToArray();
                billed[sku.Code] = raw;

                switch (request.Kind)
                {
                    case DataIssueKind.FalseZero:
                        issues.Add(FalseZero(random, sku, raw, request.PreferAtlasCase && sku.Code == AtlasZeroCode));
                        break;
                    case DataIssueKind.OneOffOrder:
                        issues.Add(OneOff(random, sku, raw, orders));
                        break;
                    case DataIssueKind.PreLaunch:
                        issues.Add(PreLaunch(sku));
                        break;
                    default:
                        units[sku.Code] = ErpDataSet.Boxes;
                        issues.Add(InBoxes(sku, raw));
                        break;
                }
            }
            return new ErpDataSet(portfolio, seed, billed, units, issues, orders);
        }

        public static bool Qualifies(DataIssueKind kind, PortfolioSku sku)
        {
            int first = SeriesStatistics.FirstSale(sku.History), last = SeriesStatistics.LastSale(sku.History);
            bool regular = sku.Pattern != DemandPattern.Intermittent && first == 0 && last == sku.History.Count - 1;
            return kind switch
            {
                DataIssueKind.PreLaunch => first >= 6 && last == sku.History.Count - 1,
                DataIssueKind.UnitsInBoxes => regular && SeriesStatistics.Mean(sku.History) >= sku.UnitsPerBox * 10,
                _ => regular
            };
        }

        // ------------------------------------------------------------------ issues

        private static DataIssue FalseZero(Random random, PortfolioSku sku, int[] raw, bool atlasCase)
        {
            double[] indices = SeriesStatistics.SeasonalIndices(sku.History);
            bool seasonalSku = sku.Pattern == DemandPattern.Seasonal;
            int month;
            if (atlasCase)
            {
                month = AtlasZeroMonth;
            }
            else
            {
                // Months with the same calendar month in another year on both sides when possible.
                List<int> pool = Enumerable.Range(12, 20)
                    .Where(m => seasonalSku == indices[SalesCalendar.CalendarMonth(m)] >= SeasonalPeak).ToList();
                if (pool.Count == 0) pool = Enumerable.Range(12, 20).ToList();
                month = pool[random.Next(pool.Count)];
            }

            double sameMonth = SameMonthAverage(sku.History, month);
            double neighbours = (sku.History[month - 1] + sku.History[month + 1]) / 2.0;
            bool seasonalMonth = seasonalSku && indices[SalesCalendar.CalendarMonth(month)] >= SeasonalPeak;
            var answers = new List<double?[]> { new double?[] { sameMonth } };
            if (!seasonalMonth) answers.Add(new double?[] { neighbours });
            raw[month] = 0;

            string label = SalesCalendar.Label(month);
            string explanation = seasonalMonth
                ? $"{sku.Code} {label}: el ERP muestra 0, pero {SalesCalendar.MonthName(SalesCalendar.CalendarMonth(month))} es un mes fuerte del SKU. " +
                  "Fue un agotado o un error de registro. Se imputa con el promedio del mismo mes en los otros años, " +
                  "porque con los meses vecinos quedaría subestimado."
                : $"{sku.Code} {label}: el ERP muestra 0 en medio de meses con venta. Fue un agotado o un error de registro: " +
                  "un cero así le enseña al modelo que no hubo demanda. Se imputa con el mismo mes de otros años o con los vecinos.";
            return new DataIssue(DataIssueKind.FalseZero, sku.Code, new[] { month }, new[] { 0 }, answers, atlasCase, explanation,
                seasonalMonth ? "p2 · Imputar un cero con estacionalidad: promedio del mismo mes en los otros años"
                    : "p2 · Imputar sin estacionalidad: promedio de los meses vecinos");
        }

        private static DataIssue OneOff(Random random, PortfolioSku sku, int[] raw, List<OneOffOrder> orders)
        {
            double[] indices = SeriesStatistics.SeasonalIndices(sku.History);
            List<int> pool = Enumerable.Range(3, 38).Where(m => indices[SalesCalendar.CalendarMonth(m)] < 1.15).ToList();
            int month = pool[random.Next(pool.Count)];
            int clean = sku.History[month];
            int extra = (int)Math.Round(clean * (2.5 + 1.5 * random.NextDouble()) / 10) * 10;
            string customer = OneOffCustomers[random.Next(OneOffCustomers.Length)];
            raw[month] = clean + extra;
            orders.Add(new OneOffOrder(sku.Code, month, customer, extra, "Pedido único: no se repite."));

            var answers = new List<double?[]>
            {
                new double?[] { clean },
                new double?[] { (sku.History[month - 1] + sku.History[month + 1]) / 2.0 },
                new double?[] { SameMonthAverage(sku.History, month) }
            };
            string explanation = $"{sku.Code} {SalesCalendar.Label(month)}: {customer} hizo un pedido único de " +
                $"{HudFormat.Number(extra)} unidades que no se va a repetir. Se quita ese pedido (o se imputa el mes); si queda, " +
                "el modelo espera otro pico igual.";
            return new DataIssue(DataIssueKind.OneOffOrder, sku.Code, new[] { month }, new[] { raw[month] }, answers, false,
                explanation, "p2 · Marcar un atípico: |D(t) − promedio| > 3 × desviación");
        }

        private static DataIssue PreLaunch(PortfolioSku sku)
        {
            int first = SeriesStatistics.FirstSale(sku.History);
            int[] months = Enumerable.Range(0, first).ToArray();
            string explanation = $"{sku.Code} se lanzó en {SalesCalendar.Label(first)}: los {first} meses en cero de antes " +
                "no son demanda. Se excluyen y la serie empieza en el lanzamiento.";
            return new DataIssue(DataIssueKind.PreLaunch, sku.Code, months, new int[first],
                new List<double?[]> { new double?[first] }, sku.IsReal, explanation,
                "p1 · Empezar la historia en el lanzamiento: los ceros de antes no son demanda");
        }

        private static DataIssue InBoxes(PortfolioSku sku, int[] raw)
        {
            int n = raw.Length;
            for (int t = 0; t < n; t++) raw[t] = (int)Math.Round(raw[t] / (double)sku.UnitsPerBox);
            double?[] converted = raw.Select(v => (double?)(v * sku.UnitsPerBox)).ToArray();
            string explanation = $"{sku.Code} viene facturado en cajas (CJ) y los demás SKU en unidades (UN). " +
                $"Según el maestro de materiales, cada caja trae {sku.UnitsPerBox} unidades: se multiplica toda la fila.";
            return new DataIssue(DataIssueKind.UnitsInBoxes, sku.Code, Enumerable.Range(0, n).ToArray(), raw.ToArray(),
                new List<double?[]> { converted }, false, explanation, "p1 · Una sola unidad de medida en toda la tabla");
        }

        /// <summary>Average of the same calendar month in the other years (from the first sale on).</summary>
        public static double SameMonthAverage(IReadOnlyList<int> history, int month)
        {
            int first = Math.Max(0, SeriesStatistics.FirstSale(history));
            double sum = 0;
            int count = 0;
            for (int m = SalesCalendar.CalendarMonth(month); m < history.Count; m += 12)
            {
                if (m == month || m < first) continue;
                sum += history[m];
                count++;
            }
            return count == 0 ? 0 : sum / count;
        }
    }
}
