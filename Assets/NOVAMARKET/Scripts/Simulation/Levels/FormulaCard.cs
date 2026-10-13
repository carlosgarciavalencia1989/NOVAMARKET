using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Portfolio;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>A box of a formula card: the term, its correct value and the hint shown when it is wrong.</summary>
    public sealed class CardBox
    {
        public CardBox(string label, double expected, string hint)
        {
            Label = label;
            Expected = expected;
            Hint = hint;
        }

        public string Label { get; }
        public double Expected { get; }
        public string Hint { get; }
    }

    /// <summary>Outcome of checking the boxes the player filled.</summary>
    public sealed class CardCheck
    {
        public CardCheck(bool[] boxesOk, double? result)
        {
            BoxesOk = boxesOk;
            Result = result;
        }

        public IReadOnlyList<bool> BoxesOk { get; }
        public bool AllOk => BoxesOk.All(ok => ok);

        /// <summary>The card's result, only when every box is right.</summary>
        public double? Result { get; }
    }

    /// <summary>
    /// A formula card (learning mode): the Atlas formula with boxes the player fills from the data on the
    /// left. In "empty boxes" mode a wrong box is highlighted with its hint; the result is computed once
    /// every box is right. Prefilled boxes (guided mode) are given.
    /// </summary>
    public sealed class FormulaCard
    {
        private readonly Func<double[], double> compute;

        public FormulaCard(string id, string title, string atlasFormula, IReadOnlyList<CardBox> boxes, string resultLabel,
            Func<double[], double> compute, ScaffoldMode mode)
        {
            Id = id;
            Title = title;
            AtlasFormula = atlasFormula;
            Boxes = boxes;
            ResultLabel = resultLabel;
            this.compute = compute;
            Prefilled = boxes.Select((_, i) => mode == ScaffoldMode.Guided && i < boxes.Count - 1).ToArray();
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>The formula as the Atlas writes it ("D'(jun-24) = PROMEDIO(jun-23; jun-25; jun-26)").</summary>
        public string AtlasFormula { get; }

        public IReadOnlyList<CardBox> Boxes { get; }
        public string ResultLabel { get; }

        /// <summary>Boxes the game fills in (guided mode: all but the last).</summary>
        public IReadOnlyList<bool> Prefilled { get; }

        /// <summary>Correct result of the card.</summary>
        public double Expected => compute(Boxes.Select(b => b.Expected).ToArray());

        public CardCheck Check(IReadOnlyList<double?> entries)
        {
            if (entries == null || entries.Count != Boxes.Count) throw new ArgumentException("One entry per box.", nameof(entries));
            var ok = new bool[Boxes.Count];
            for (int i = 0; i < ok.Length; i++)
                ok[i] = Prefilled[i] || entries[i].HasValue && Tolerance.Matches(entries[i].Value, Boxes[i].Expected);
            double? result = ok.All(b => b)
                ? compute(Boxes.Select((b, i) => Prefilled[i] ? b.Expected : entries[i].Value).ToArray())
                : (double?)null;
            return new CardCheck(ok, result);
        }
    }

    /// <summary>The cards of the Atlas step p2 for each kind of problem, built from the ERP data.</summary>
    public static class CleaningCards
    {
        /// <summary>Same month of the other years (seasonal zero).</summary>
        public static FormulaCard SameMonth(ErpDataSet data, string code, int month, ScaffoldMode mode)
        {
            IReadOnlyList<int> billed = data.Billed(code);
            int first = Math.Max(0, SeriesStatistics.FirstSale(data.Portfolio.Find(code).History));
            List<int> others = new List<int>();
            for (int m = SalesCalendar.CalendarMonth(month); m < billed.Count; m += 12)
                if (m != month && m >= first) others.Add(m);
            string target = SalesCalendar.Label(month);
            var boxes = others.Select(m => new CardBox(SalesCalendar.Label(m), billed[m],
                $"Busca {code} en la columna {SalesCalendar.Label(m)} de la facturación.")).ToList();
            return new FormulaCard("mismo-mes", "Imputar con el mismo mes de otros años",
                $"D'({target}) = PROMEDIO({string.Join("; ", others.Select(SalesCalendar.Label))})",
                boxes, $"D'({target})", values => values.Average(), mode);
        }

        /// <summary>Average of the neighbouring months (zero without seasonality).</summary>
        public static FormulaCard Neighbours(ErpDataSet data, string code, int month, ScaffoldMode mode)
        {
            IReadOnlyList<int> billed = data.Billed(code);
            string before = SalesCalendar.Label(month - 1), after = SalesCalendar.Label(month + 1);
            return new FormulaCard("vecinos", "Imputar sin estacionalidad", $"D'(t) = (D(t−1) + D(t+1)) / 2",
                new[]
                {
                    new CardBox($"D(t−1) · {before}", billed[month - 1], $"El mes anterior: {before}."),
                    new CardBox($"D(t+1) · {after}", billed[month + 1], $"El mes siguiente: {after}.")
                },
                $"D'({SalesCalendar.Label(month)})", values => (values[0] + values[1]) / 2, mode);
        }

        /// <summary>Takes a one-off order out of the month.</summary>
        public static FormulaCard RemoveOrder(ErpDataSet data, string code, int month, ScaffoldMode mode)
        {
            OneOffOrder order = data.OrderAt(code, month);
            string label = SalesCalendar.Label(month);
            return new FormulaCard("quitar-pedido", "Quitar un pedido único", $"D'({label}) = facturado − pedido único",
                new[]
                {
                    new CardBox($"Facturado {label}", data.Billed(code)[month], $"El total de {code} en {label}."),
                    new CardBox("Pedido único", order?.Units ?? 0, "Está en el detalle de facturas del mes, con su observación.")
                },
                $"D'({label})", values => values[0] - values[1], mode);
        }

        /// <summary>Boxes to units with the material master.</summary>
        public static FormulaCard Convert(ErpDataSet data, string code, int month, ScaffoldMode mode)
        {
            PortfolioSku sku = data.Portfolio.Find(code);
            string label = SalesCalendar.Label(month);
            return new FormulaCard("convertir", "Convertir cajas a unidades", "Unidades = cajas × unidades por caja",
                new[]
                {
                    new CardBox($"Cajas {label}", data.Billed(code)[month], $"La cantidad de {code} en {label}, en CJ."),
                    new CardBox("Unidades por caja", sku.UnitsPerBox, "Está en el maestro de materiales, columna «UN por caja».")
                },
                $"Unidades {label}", values => values[0] * values[1], mode);
        }

        /// <summary>The 3-deviation rule to flag outliers (gives the threshold).</summary>
        public static FormulaCard Outlier(ErpDataSet data, string code, ScaffoldMode mode)
        {
            IReadOnlyList<int> billed = data.Billed(code);
            int first = Math.Max(0, SeriesStatistics.FirstSale(billed));
            return new FormulaCard("atipico", "Marcar un atípico", "|D(t) − promedio| > 3 × desviación",
                new[]
                {
                    new CardBox("Promedio", SeriesStatistics.Mean(billed, first), "=PROMEDIO de toda la fila del SKU."),
                    new CardBox("Desviación", SeriesStatistics.StdDev(billed, first), "=DESVEST.P de toda la fila del SKU.")
                },
                "Límite: 3 × desviación", values => 3 * values[1], mode);
        }

        /// <summary>The cards that fit a problem (the first one is the model answer).</summary>
        public static IReadOnlyList<FormulaCard> For(ErpDataSet data, DataIssue issue, ScaffoldMode mode)
        {
            int month = issue.Months[0];
            return issue.Kind switch
            {
                DataIssueKind.FalseZero => new[] { SameMonth(data, issue.Code, month, mode), Neighbours(data, issue.Code, month, mode) },
                DataIssueKind.OneOffOrder => new[] { RemoveOrder(data, issue.Code, month, mode), Outlier(data, issue.Code, mode) },
                DataIssueKind.UnitsInBoxes => new[] { Convert(data, issue.Code, month, mode) },
                _ => Array.Empty<FormulaCard>()
            };
        }
    }
}
