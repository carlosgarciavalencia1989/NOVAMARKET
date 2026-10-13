using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>The player's note on a cell: what problem it is and what they do about it.</summary>
    public sealed class IssueMark
    {
        public IssueMark(string code, int month, DataIssueKind kind, CorrectionAction action, string note = null)
        {
            Code = code;
            Month = month;
            Kind = kind;
            Action = action;
            Note = note ?? string.Empty;
        }

        public string Code { get; }
        public int Month { get; }
        public DataIssueKind Kind { get; }
        public CorrectionAction Action { get; }
        public string Note { get; }
    }

    /// <summary>
    /// Level 1.2 "Limpiar los datos" (Atlas step p2, empty boxes). Sofía hands over the billing of five
    /// SKUs with problems: the real Atlas zero of CV10004 jun-24, the months before the launch of
    /// CV10023, and a false zero, a one-off customer order and a SKU billed in boxes placed by the seed.
    /// CV10004's December peaks look like outliers but are the season. After the download the level
    /// copies the data to «Serie limpia»: the player marks each problem (type and action) and corrects it
    /// there. Stars: find every problem · correct them well without touching good data · no Atlas help.
    /// </summary>
    public sealed class CleanDataLevel : LevelSession
    {
        public const string CleanSheetName = "Serie limpia";
        public const string AtlasZeroCode = DataQualityInjector.AtlasZeroCode;
        public const string LaunchCode = "CV10023";

        /// <summary>Stable real SKUs where the seed places the false zero, the one-off order and the boxes.</summary>
        public static readonly string[] Pool = { "CV10009", "CV10017", "CV10015", "CV10012", "CV10021" };

        private readonly Dictionary<(string, int), IssueMark> marks = new Dictionary<(string, int), IssueMark>();
        private readonly List<string> codes;

        public CleanDataLevel(Portfolio.Portfolio portfolio, int seed, DialogueBook dialogue = null)
            : base(ForecastWorld.CleanData, DataQualityInjector.Inject(portfolio, Plan(), seed), seed, dialogue)
        {
            codes = Data.Issues.Select(i => i.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        /// <summary>The problems of the level, in the order the injector places them.</summary>
        public static IReadOnlyList<IssueRequest> Plan() => new[]
        {
            new IssueRequest(DataIssueKind.FalseZero, AtlasZeroCode) { PreferAtlasCase = true },
            new IssueRequest(DataIssueKind.PreLaunch, LaunchCode),
            new IssueRequest(DataIssueKind.FalseZero, Pool),
            new IssueRequest(DataIssueKind.OneOffOrder, Pool),
            new IssueRequest(DataIssueKind.UnitsInBoxes, Pool)
        };

        public override IReadOnlyList<string> RequestedCodes => codes;
        public override IReadOnlyList<DialogueLine> Intro => Lines(DialogueKeys.Intro, ("skus", codes.Count.ToString()));

        public override IReadOnlyList<string> HelpTopics { get; } =
            Enum.GetValues(typeof(DataIssueKind)).Cast<DataIssueKind>().Select(TopicOf).ToArray();

        public static string TopicOf(DataIssueKind kind) => kind.ToString();

        public override string TopicName(string topic) =>
            Enum.TryParse(topic, out DataIssueKind kind) ? DataIssue.Name(kind) : topic;

        /// <summary>The tab of the raw billing download.</summary>
        public string RawSheet { get; private set; }

        public Worksheet CleanSheet => Book.Sheet(CleanSheetName);

        public IReadOnlyCollection<IssueMark> Marks => marks.Values;

        public event Action MarksChanged;

        protected override void OnDownloaded(ErpTable table, Worksheet sheet)
        {
            if (!IsBillingByMonth(table)) return;
            RawSheet = sheet.Name;
            if (CleanSheet == null) CopyToCleanSheet(table);
        }

        /// <summary>«Serie limpia»: the same table (only the months, no projection) for the player to correct.</summary>
        private void CopyToCleanSheet(ErpTable table)
        {
            int first = SalesCalendar.Months, last = -1;
            for (int column = 2; column < table.Headers.Count; column++)
            {
                int month = MonthOfLabel(table.Headers[column]);
                if (month < 0) continue;
                first = Math.Min(first, month);
                last = Math.Max(last, month);
            }

            Book.Batch(() =>
            {
                Worksheet clean = Book.AddSheet(CleanSheetName);
                clean.SetText(new CellAddress(0, ErpTable.TitleRow), "Serie limpia: corrige aquí y deja la fila en unidades");
                clean.SetText(new CellAddress(0, ErpTable.HeaderRow), "Material");
                clean.SetText(new CellAddress(1, ErpTable.HeaderRow), "Descripción");
                for (int m = 0; m < SalesCalendar.Months; m++)
                    clean.SetText(new CellAddress(HistoryTable.MonthColumn(m), ErpTable.HeaderRow), SalesCalendar.Label(m));
                clean.SetFormat(new CellRange(new CellAddress(HistoryTable.MonthColumn(0), ErpTable.FirstDataRow),
                    new CellAddress(HistoryTable.MonthColumn(SalesCalendar.Months - 1), ErpTable.FirstDataRow + Math.Max(0, table.Rows.Count - 1))),
                    CellFormat.Number(0));

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    CellValue[] values = table.Rows[r];
                    int row = ErpTable.FirstDataRow + r;
                    clean.SetText(new CellAddress(0, row), values[0].Text);
                    clean.SetText(new CellAddress(1, row), values[1].Text);
                    for (int column = 2; column < values.Length; column++)
                    {
                        int month = MonthOfLabel(table.Headers[column]);
                        if (month >= 0 && values[column].IsNumber)
                            clean.SetNumber(new CellAddress(HistoryTable.MonthColumn(month), row), values[column].Number);
                    }
                }
            });
        }

        private static int MonthOfLabel(string header)
        {
            for (int m = 0; m < SalesCalendar.Months; m++) if (SalesCalendar.Label(m) == header) return m;
            return -1;
        }

        // ------------------------------------------------------------------ marks and cards

        /// <summary>Marks (or re-marks) a cell as a problem with its action.</summary>
        public void Mark(IssueMark mark)
        {
            if (mark == null) throw new ArgumentNullException(nameof(mark));
            marks[(mark.Code, mark.Month)] = mark;
            MarksChanged?.Invoke();
        }

        public bool Unmark(string code, int month)
        {
            bool removed = marks.Remove((code, month));
            if (removed) MarksChanged?.Invoke();
            return removed;
        }

        public IssueMark MarkAt(string code, int month) => marks.TryGetValue((code, month), out IssueMark mark) ? mark : null;

        /// <summary>
        /// Formula cards for a cell and the problem type the player chose. They are built from the data
        /// for any cell, so they never reveal whether the cell really has a problem.
        /// </summary>
        public IReadOnlyList<FormulaCard> CardsFor(string code, int month, DataIssueKind kind)
        {
            var cards = new List<FormulaCard>();
            bool inside = month > 0 && month < SalesCalendar.Months - 1;
            switch (kind)
            {
                case DataIssueKind.FalseZero:
                    cards.Add(CleaningCards.SameMonth(Data, code, month, Info.Mode));
                    if (inside) cards.Add(CleaningCards.Neighbours(Data, code, month, Info.Mode));
                    break;
                case DataIssueKind.OneOffOrder:
                    cards.Add(CleaningCards.Outlier(Data, code, Info.Mode));
                    cards.Add(CleaningCards.RemoveOrder(Data, code, month, Info.Mode));
                    if (inside) cards.Add(CleaningCards.Neighbours(Data, code, month, Info.Mode));
                    break;
                case DataIssueKind.UnitsInBoxes:
                    cards.Add(CleaningCards.Convert(Data, code, month, Info.Mode));
                    break;
            }
            return cards;
        }

        // ------------------------------------------------------------------ validation

        public bool IsFound(DataIssue issue) =>
            issue.Months.Any(m => MarkAt(issue.Code, m) is IssueMark mark && mark.Kind == issue.Kind);

        public bool HasRightAction(DataIssue issue) =>
            issue.Months.Any(m => MarkAt(issue.Code, m) is IssueMark mark && mark.Kind == issue.Kind && mark.Action == issue.ExpectedAction);

        /// <summary>True when «Serie limpia» matches one of the accepted answers on every month of the problem.</summary>
        public bool IsCorrected(DataIssue issue, out bool withFormulas)
        {
            withFormulas = false;
            Worksheet clean = CleanSheet;
            int row = HistoryTable.FindRow(clean, issue.Code);
            if (row < 0) return false;
            foreach (double?[] answer in issue.AcceptedValues)
            {
                bool matches = true;
                for (int i = 0; i < issue.Months.Count && matches; i++)
                {
                    double? value = HistoryTable.Month(clean, row, issue.Months[i]);
                    matches = answer[i].HasValue ? value.HasValue && Tolerance.Matches(value.Value, answer[i].Value) : !value.HasValue;
                }
                if (!matches) continue;
                withFormulas = issue.Months.Where((m, i) => answer[i].HasValue)
                    .All(m => clean.IsFormula(new CellAddress(HistoryTable.MonthColumn(m), row)));
                return true;
            }
            return false;
        }

        /// <summary>Marks on cells without that problem (a December peak marked as an outlier…).</summary>
        public IEnumerable<IssueMark> WrongMarks() =>
            marks.Values.Where(mark => !Data.Issues.Any(i => i.Covers(mark.Code, mark.Month) && i.Kind == mark.Kind));

        /// <summary>Good months the player changed or removed in «Serie limpia».</summary>
        public IEnumerable<(string Code, int Month)> ChangedGoodData()
        {
            Worksheet clean = CleanSheet;
            foreach (string code in codes)
            {
                int row = HistoryTable.FindRow(clean, code);
                IReadOnlyList<int> raw = Data.Billed(code);
                for (int m = 0; m < SalesCalendar.Months; m++)
                {
                    if (Data.IssueAt(code, m) != null) continue;
                    double? value = row < 0 ? null : HistoryTable.Month(clean, row, m);
                    if (!value.HasValue || !Tolerance.Matches(value.Value, raw[m])) yield return (code, m);
                }
            }
        }

        public override IReadOnlyList<LevelObjective> Objectives() => new[]
        {
            new LevelObjective($"Descarga del ERP la facturación de {string.Join(", ", codes)} (ene-23 a ago-26)", CleanSheet != null),
            new LevelObjective("Marca cada problema con su tipo y la acción: imputar, excluir o convertir", marks.Count > 0),
            new LevelObjective("Corrige en «Serie limpia» y deja todo en unidades", CleanSheet != null && Data.Issues.Any(i => IsCorrected(i, out _))),
            new LevelObjective("Entrega la serie limpia a Andrés", Result != null)
        };

        protected override LevelResult Evaluate()
        {
            var found = Data.Issues.Where(IsFound).ToList();
            var corrected = new List<DataIssue>();
            bool allFormulas = true;
            foreach (DataIssue issue in Data.Issues)
            {
                if (CleanSheet == null || !IsCorrected(issue, out bool formulas)) continue;
                corrected.Add(issue);
                allFormulas &= formulas || issue.Kind == DataIssueKind.PreLaunch;
            }
            bool actions = Data.Issues.All(HasRightAction);
            List<IssueMark> wrong = WrongMarks().ToList();
            List<(string Code, int Month)> changed = CleanSheet == null ? new List<(string, int)>() : ChangedGoodData().ToList();

            bool allFound = found.Count == Data.Issues.Count;
            bool allCorrected = CleanSheet != null && corrected.Count == Data.Issues.Count && actions && wrong.Count == 0 && changed.Count == 0;
            int missing = Data.Issues.Count - found.Count;

            var criteria = new[]
            {
                new StarCriterion("Encontraste todos los problemas", allFound,
                    allFound ? $"Los {Data.Issues.Count} problemas marcados con su tipo."
                        : $"Encontraste {found.Count} de {Data.Issues.Count}."),
                new StarCriterion("Los corregiste bien", allCorrected, allCorrected
                    ? "Cada corrección cuadra con el Atlas y no tocaste datos buenos."
                    : CorrectionDetail(corrected.Count, Data.Issues.Count, actions, wrong.Count, changed.Count)),
                new StarCriterion("Sin ayuda del Atlas", !Help.Used, Help.Used
                    ? $"Pediste ayuda al Atlas {Help.Topics.Count} {(Help.Topics.Count == 1 ? "vez" : "veces")}." : "Lo resolviste solo.")
            };

            List<SkuImpact> impact = Impact();
            var debrief = new List<DialogueLine>();
            if (allFound && allCorrected) debrief.Add(Line(DialogueKeys.Success));
            else if (!allFound) debrief.Add(Line(missing == 1 ? DialogueKeys.MissingOne : DialogueKeys.MissingMany, ("missing", missing.ToString())));
            else if (corrected.Count < Data.Issues.Count || !actions || wrong.Count > 0) debrief.Add(Line(DialogueKeys.BadCorrection));
            if (changed.Count > 0) debrief.Add(Line(DialogueKeys.ChangedGoodData));
            foreach (SkuImpact sku in impact.Where(i => i.Damage > LevelResult.VisibleDamage))
                debrief.Add(ImpactLine(sku));

            var lessons = Data.Issues.Select(i => $"{i.KindName}: {i.Explanation} ({i.AtlasStep})").ToList();
            lessons.Add("No borres un cero sin reemplazarlo: el mes desaparece y los promedios se corren.");
            lessons.Add("Documenta cada imputación: en la sustentación te van a preguntar por qué cambiaste el dato.");
            return new LevelResult(Info, criteria, allCorrected && allFormulas, debrief, lessons, impact);
        }

        private static readonly string[] MonthNames =
        {
            "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
        };

        /// <summary>Andrés shows what the model would learn wrong from a SKU of the player's series.</summary>
        private DialogueLine ImpactLine(SkuImpact sku)
        {
            if (sku.LevelIsWorse)
                return Line(sku.PlayerLevel > sku.CleanLevel ? DialogueKeys.ImpactLevelHigh : DialogueKeys.ImpactLevelLow,
                    ("code", sku.Code),
                    ("player", HudFormat.Number((long)Math.Round(sku.PlayerLevel))),
                    ("clean", HudFormat.Number((long)Math.Round(sku.CleanLevel))));
            return Line(sku.PlayerIndex > sku.CleanIndex ? DialogueKeys.ImpactIndexHigh : DialogueKeys.ImpactIndexLow,
                ("code", sku.Code),
                ("month", MonthNames[sku.Month]),
                ("player", HudFormat.Decimal(sku.PlayerIndex, 2)),
                ("clean", HudFormat.Decimal(sku.CleanIndex, 2)));
        }

        private static string CorrectionDetail(int corrected, int total, bool actions, int wrong, int changed)
        {
            var parts = new List<string> { $"{corrected} de {total} correcciones cuadran con el Atlas" };
            if (!actions) parts.Add("alguna acción no corresponde al problema");
            if (wrong > 0) parts.Add($"{wrong} {(wrong == 1 ? "marca" : "marcas")} en datos buenos");
            if (changed > 0) parts.Add($"{changed} {(changed == 1 ? "dato bueno cambiado" : "datos buenos cambiados")}");
            return string.Join("; ", parts) + ".";
        }

        /// <summary>What a model would learn from the player's series of each SKU against the clean series.</summary>
        public List<SkuImpact> Impact()
        {
            Worksheet clean = CleanSheet;
            var impact = new List<SkuImpact>();
            foreach (string code in codes)
            {
                int row = HistoryTable.FindRow(clean, code);
                double?[] player = row >= 0
                    ? HistoryTable.Series(clean, row, SalesCalendar.Months)
                    : Data.Billed(code).Select(v => (double?)v).ToArray();
                impact.Add(CleaningImpact.Compare(code, player, ModelAnswer(code)));
            }
            return impact;
        }

        /// <summary>The raw billing of a SKU with the Atlas answer of its problems applied.</summary>
        public double?[] ModelAnswer(string code)
        {
            double?[] series = Data.Billed(code).Select(v => (double?)v).ToArray();
            foreach (DataIssue issue in Data.Issues.Where(i => i.Code == code))
                for (int i = 0; i < issue.Months.Count; i++)
                    series[issue.Months[i]] = issue.AcceptedValues[0][i];
            return series;
        }

        protected override AtlasAnswer Explain(string topic)
        {
            if (!Enum.TryParse(topic, out DataIssueKind kind)) return null;
            DataIssue issue = Data.Issues.FirstOrDefault(i => i.Kind == kind && (kind != DataIssueKind.FalseZero || !i.IsReal))
                ?? Data.Issues.FirstOrDefault(i => i.Kind == kind);
            if (issue == null) return null;
            var steps = new List<string> { issue.Explanation, issue.AtlasStep };
            double?[] answer = issue.AcceptedValues[0];
            switch (kind)
            {
                case DataIssueKind.PreLaunch:
                    steps.Add($"En «Serie limpia» borra {SalesCalendar.Label(issue.Months[0])} a " +
                              $"{SalesCalendar.Label(issue.Months[issue.Months.Count - 1])} de {issue.Code} y márcalos como «Excluir».");
                    break;
                case DataIssueKind.UnitsInBoxes:
                    steps.Add($"Marca la fila de {issue.Code} como «Convertir» y multiplica cada mes por " +
                              $"{Data.Portfolio.Find(issue.Code).UnitsPerBox} (arrastra la fórmula a toda la fila).");
                    break;
                default:
                    steps.Add($"{issue.Code} {SalesCalendar.Label(issue.Months[0])} queda en " +
                              $"{HudFormat.Number((long)Math.Round(answer[0] ?? 0))}. Márcalo como «Imputar».");
                    break;
            }
            return new AtlasAnswer($"{DataIssue.Name(kind)} (p2)", steps);
        }
    }
}
