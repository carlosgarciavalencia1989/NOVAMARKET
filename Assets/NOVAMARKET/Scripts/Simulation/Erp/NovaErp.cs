using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>Parameters of the billing query (the player fills them in the ERP screen).</summary>
    public sealed class BillingQuery
    {
        public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();

        /// <summary>First and last month (history index, 0 = ene-23).</summary>
        public int FromMonth { get; set; }
        public int ToMonth { get; set; } = SalesCalendar.Months - 1;

        /// <summary>
        /// Adds the open month projected with a 6-month moving average. On by default, as the real
        /// report: the Atlas trap "the June column of the file was a moving average, not a sale".
        /// </summary>
        public bool IncludeProjection { get; set; } = true;
    }

    /// <summary>
    /// NOVA ERP, the game's simulated ERP: billing (the real demand), material master and billing
    /// detail per customer, each with its SAP equivalent; "Descargar a hoja" sends any result to a new
    /// tab of the planner sheet. Screens of later worlds stay locked.
    /// </summary>
    public sealed class NovaErp
    {
        public const string ProjectionSuffix = " (proy.)";
        public const int ProjectionMonths = 6;

        private static readonly string[] Customers =
        {
            "Droguerías Alameda", "Almacenes La Montaña", "Supertiendas del Valle", "Distribuidora Aburrá",
            "Belleza Express", "Farmacias Laureles", "Comercial Envigado", "Tiendas Río Negro"
        };

        private readonly List<(ErpTable Table, string Sheet)> downloads = new List<(ErpTable, string)>();

        public NovaErp(ErpDataSet data, int world = 1)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            World = world;
        }

        public ErpDataSet Data { get; }

        /// <summary>Planner world the player is in: screens of later worlds are locked.</summary>
        public int World { get; }

        public event Action<ErpTable, Worksheet> Downloaded;

        public IReadOnlyList<ErpScreen> Screens => ErpScreens.All;

        /// <summary>Every table downloaded so far, with the tab it went to.</summary>
        public IReadOnlyList<(ErpTable Table, string Sheet)> Downloads => downloads;

        public bool IsAvailable(string screenId) => ErpScreens.Find(screenId)?.IsAvailable(World) == true;

        /// <summary>Units billed per material and month (VF05). Unknown codes are listed in a note.</summary>
        public ErpTable Billing(BillingQuery query)
        {
            RequireScreen(ErpScreens.Billing);
            if (query == null) throw new ArgumentNullException(nameof(query));
            int from = Math.Max(0, Math.Min(query.FromMonth, query.ToMonth));
            int to = Math.Min(SalesCalendar.Months - 1, Math.Max(query.FromMonth, query.ToMonth));
            bool projection = query.IncludeProjection && to == SalesCalendar.Months - 1;

            var headers = new List<string> { "Material", "Descripción" };
            for (int m = from; m <= to; m++) headers.Add(SalesCalendar.Label(m));
            if (projection) headers.Add(SalesCalendar.Label(to + 1) + ProjectionSuffix);
            headers.Add("UM");
            var formats = headers.Select((h, i) => i >= 2 && i < headers.Count - 1 ? CellFormat.Number(0) : CellFormat.General).ToList();

            var table = new ErpTable(ErpScreens.Billing, $"Facturación por material y mes · {SalesCalendar.Label(from)} a {SalesCalendar.Label(to)}",
                headers, formats) { SheetName = "Facturación" };
            var missing = new List<string>();
            foreach (string code in query.Codes)
            {
                PortfolioSku sku = Data.Portfolio.Find(code);
                if (sku == null)
                {
                    missing.Add(code);
                    continue;
                }
                IReadOnlyList<int> billed = Data.Billed(code);
                var row = new List<CellValue> { CellValue.Of(sku.Code), CellValue.Of(sku.Description) };
                for (int m = from; m <= to; m++) row.Add(CellValue.Of(billed[m]));
                if (projection) row.Add(CellValue.Of(Math.Round(Average(billed, to - ProjectionMonths + 1, to))));
                row.Add(CellValue.Of(Data.SalesUnit(code)));
                table.AddRow(row.ToArray());
            }
            if (projection)
                table.AddNote($"{SalesCalendar.Label(to + 1)}{ProjectionSuffix}: proyección del mes en curso con promedio móvil de " +
                              $"{ProjectionMonths} meses. No es venta facturada.");
            if (missing.Count > 0) table.AddNote("No existen en el maestro: " + string.Join(", ", missing));
            return table;
        }

        /// <summary>Material master data (MM03).</summary>
        public ErpTable MaterialMaster(IEnumerable<string> codes)
        {
            RequireScreen(ErpScreens.MaterialMaster);
            string[] headers =
            {
                "Material", "Descripción", "Familia", "Proveedor", "Origen", "Modalidad", "Puerto de entrada", "LT (días)",
                "MOQ (UN)", "UN por caja", "Costo unitario (COP)", "UM de venta"
            };
            var formats = headers.Select((h, i) => i >= 7 && i <= 10 ? CellFormat.Number(0) : CellFormat.General).ToList();
            var table = new ErpTable(ErpScreens.MaterialMaster, "Maestro de materiales", headers, formats) { SheetName = "Maestro" };
            foreach (string code in codes)
            {
                PortfolioSku sku = Data.Portfolio.Find(code);
                if (sku == null) continue;
                table.AddRow(CellValue.Of(sku.Code), CellValue.Of(sku.Description), CellValue.Of(sku.Family),
                    CellValue.Of(sku.Supplier), CellValue.Of(SupplyNames.Name(sku.Origin)), CellValue.Of(SupplyNames.Name(sku.Mode)),
                    CellValue.Of(SupplyNames.Name(sku.Port)), CellValue.Of(sku.LeadTimeDays), CellValue.Of(sku.MinimumOrderUnits),
                    CellValue.Of(sku.UnitsPerBox), CellValue.Of(sku.UnitCost), CellValue.Of(Data.SalesUnit(code)));
            }
            return table;
        }

        /// <summary>
        /// Invoices of one material in one month, by customer: the regular customers share the month
        /// and a one-off order shows as its own line with its note.
        /// </summary>
        public ErpTable BillingDetail(string code, int month)
        {
            RequireScreen(ErpScreens.Billing);
            PortfolioSku sku = Data.Portfolio.Find(code) ?? throw new ArgumentException($"Unknown material {code}.", nameof(code));
            string[] headers = { "Documento", "Cliente", "Cantidad", "UM", "Observación" };
            var table = new ErpTable(ErpScreens.Billing, $"Facturas de {code} en {SalesCalendar.Label(month)}", headers,
                new[] { CellFormat.General, CellFormat.General, CellFormat.Number(0), CellFormat.General, CellFormat.General })
            {
                SheetName = $"Detalle {code}"
            };

            int total = Data.Billed(code)[month];
            OneOffOrder order = Data.OrderAt(code, month);
            int regular = total - (order?.Units ?? 0);
            var random = new Random(Data.Seed ^ (StableHash(code) * 31 + month));
            int lines = regular <= 0 ? 0 : Math.Min(regular, 3 + random.Next(3));
            int document = 90000000 + month * 1000 + random.Next(100);
            List<string> customers = Customers.OrderBy(_ => random.Next()).Take(lines).ToList();
            int remaining = regular;
            for (int i = 0; i < lines; i++)
            {
                int quantity = i == lines - 1 ? remaining : Math.Max(1, (int)Math.Round(regular * (0.15 + 0.2 * random.NextDouble())));
                quantity = Math.Min(quantity, remaining - (lines - 1 - i));
                remaining -= quantity;
                table.AddRow(CellValue.Of((document + i).ToString()), CellValue.Of(customers[i]), CellValue.Of(quantity),
                    CellValue.Of(Data.SalesUnit(code)), CellValue.Of(string.Empty));
            }
            if (order != null)
                table.AddRow(CellValue.Of((document + lines).ToString()), CellValue.Of(order.Customer), CellValue.Of(order.Units),
                    CellValue.Of(Data.SalesUnit(code)), CellValue.Of(order.Note));
            if (total == 0) table.AddNote("Sin facturas registradas en el mes.");
            return table;
        }

        /// <summary>"Descargar a hoja": copies the table to a new tab and logs it.</summary>
        public Worksheet Download(ErpTable table, Workbook book, string sheetName = null)
        {
            Worksheet sheet = table.DownloadTo(book, sheetName);
            downloads.Add((table, sheet.Name));
            Downloaded?.Invoke(table, sheet);
            return sheet;
        }

        private void RequireScreen(string id)
        {
            if (!IsAvailable(id)) throw new InvalidOperationException($"The ERP screen '{id}' is locked in world {World}.");
        }

        // string.GetHashCode may change between runtimes; the detail must not.
        private static int StableHash(string text)
        {
            int hash = 17;
            foreach (char c in text) hash = unchecked(hash * 31 + c);
            return hash;
        }

        private static double Average(IReadOnlyList<int> values, int from, int to)
        {
            double sum = 0;
            for (int i = from; i <= to; i++) sum += values[i];
            return sum / (to - from + 1);
        }
    }
}
