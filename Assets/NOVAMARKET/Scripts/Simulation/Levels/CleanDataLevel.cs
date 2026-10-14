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
            IssueMark before = MarkAt(mark.Code, mark.Month);
            marks[(mark.Code, mark.Month)] = mark;
            MarksChanged?.Invoke();
            // Ctrl+Z puts back the mark that was there (or none).
            Book.History.Add($"marcar {mark.Code} {SalesCalendar.Label(mark.Month)}", () => Put(mark.Code, mark.Month, before), () => Mark(mark));
        }

        public bool Unmark(string code, int month)
        {
            IssueMark before = MarkAt(code, month);
            if (before == null) return false;
            marks.Remove((code, month));
            MarksChanged?.Invoke();
            Book.History.Add($"quitar la marca de {code} {SalesCalendar.Label(month)}", () => Mark(before), () => Unmark(code, month));
            return true;
        }

        private void Put(string code, int month, IssueMark mark)
        {
            if (mark != null) Mark(mark);
            else Unmark(code, month);
        }

        public IssueMark MarkAt(string code, int month) => marks.TryGetValue((code, month), out IssueMark mark) ? mark : null;

        /// <summary>
        /// The SKU and month of a cell of a billing tab or of «Serie limpia» (code in column A, month label
        /// in row 2). False for any other cell.
        /// </summary>
        public static bool TryLocate(Worksheet sheet, CellAddress cell, out string code, out int month)
        {
            code = null;
            month = -1;
            if (sheet == null || cell.Row < ErpTable.FirstDataRow) return false;
            CellValue header = sheet.Value(new CellAddress(cell.Column, ErpTable.HeaderRow));
            CellValue material = sheet.Value(new CellAddress(HistoryTable.CodeColumn, cell.Row));
            if (!header.IsText || !material.IsText || material.Text.Length == 0) return false;
            month = MonthOfLabel(header.Text);
            if (month < 0) return false;
            code = material.Text;
            return true;
        }

        /// <summary>Writes a value (a formula card result) into «Serie limpia» for a SKU and month.</summary>
        public bool WriteClean(string code, int month, double value)
        {
            int row = HistoryTable.FindRow(CleanSheet, code);
            if (row < 0 || month < 0 || month >= SalesCalendar.Months) return false;
            var cell = new CellAddress(HistoryTable.MonthColumn(month), row);
            Book.History.Record($"escribir en {cell} con una tarjeta", () => CleanSheet.SetNumber(cell, value));
            return true;
        }

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

            CleaningReview review = Review();
            bool allFound = found.Count == Data.Issues.Count;
            bool allCorrected = CleanSheet != null && corrected.Count == Data.Issues.Count && actions && wrong.Count == 0 && changed.Count == 0;
            int missing = Data.Issues.Count - found.Count;

            var criteria = new[]
            {
                new StarCriterion("Encontraste todos los problemas", allFound, FoundDetail(review)),
                new StarCriterion("Los corregiste bien", allCorrected, allCorrected
                    ? "Cada corrección cuadra con el Atlas y no tocaste datos buenos."
                    : CorrectionDetail(review)),
                new StarCriterion("Sin ayuda del Atlas", !Help.Used, Help.Used
                    ? $"Usaste la solución del Atlas en {Help.Topics.Count} {(Help.Topics.Count == 1 ? "tema" : "temas")}, así que esta estrella no cuenta."
                    : "Lo resolviste sin ver la solución del Atlas.")
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
            return new LevelResult(Info, criteria, allCorrected && allFormulas, debrief, lessons, impact, review);
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

        /// <summary>The first star's detail: how many problems were found and which ones were missed.</summary>
        private static string FoundDetail(CleaningReview review)
        {
            int total = review.Issues.Count;
            List<IssueReview> missed = review.Issues
                .Where(i => i.Outcome == IssueOutcome.NotMarked || i.Outcome == IssueOutcome.WrongKind).ToList();
            if (missed.Count == 0) return $"Marcaste los {total} problemas con su tipo.";
            List<string> items = missed.Select(i => i.Outcome == IssueOutcome.WrongKind
                ? $"{i.Where}, que marcaste como «{DataIssue.Name(i.MarkedKind.Value)}» y es «{DataIssue.Name(i.Issue.Kind)}»"
                : $"{i.Where}: {DataIssue.Name(i.Issue.Kind).ToLowerInvariant()}").ToList();
            string list = items.Count == 1 ? items[0] : string.Join("; ", items.Take(items.Count - 1)) + "; y " + items.Last();
            return $"Encontraste {total - missed.Count} de {total} problemas. Te faltó {list}.";
        }

        /// <summary>The second star's detail, in full sentences: what went wrong and where.</summary>
        private static string CorrectionDetail(CleaningReview review)
        {
            var parts = new List<string>();
            List<IssueReview> found = review.Issues
                .Where(i => i.Outcome != IssueOutcome.NotMarked && i.Outcome != IssueOutcome.WrongKind).ToList();
            parts.Add($"Corregiste bien {review.Issues.Count(i => i.IsCorrect)} de {review.Issues.Count} problemas.");

            List<IssueReview> pending = found.Where(i => i.Outcome == IssueOutcome.NotCorrected).ToList();
            if (pending.Count > 0)
                parts.Add($"Marcaste {Join(pending.Select(i => i.Where))}, pero no escribiste el valor corregido en «{CleanSheetName}».");
            foreach (IssueReview action in found.Where(i => i.Outcome == IssueOutcome.WrongAction)) parts.Add(action.Message);
            List<IssueReview> values = found.Where(i => i.Outcome == IssueOutcome.WrongValue).ToList();
            if (values.Count > 0)
                parts.Add($"En {Join(values.Select(i => i.Where))} el valor que dejaste no es el del Atlas.");

            List<GoodDataReview> marked = review.GoodData.Where(g => g.Marked).ToList();
            if (marked.Count > 0)
                parts.Add(marked.Count == 1
                    ? $"Marcaste como problema una celda que estaba bien: {marked[0].Where} es venta real, no un error."
                    : $"Marcaste como problema {marked.Count} celdas que estaban bien: {Join(marked.Select(g => g.Where))} son venta real, no errores.");
            List<GoodDataReview> changed = review.GoodData.Where(g => !g.Marked).ToList();
            if (changed.Count > 0)
                parts.Add($"Cambiaste {(changed.Count == 1 ? "un mes que estaba bien" : $"{changed.Count} meses que estaban bien")}: " +
                          $"{Join(changed.Select(g => g.Where))}.");
            if (review.GoodData.Count > 0)
                parts.Add("Corregir un dato bueno daña la historia: el pronóstico aprendería algo que no pasó.");
            parts.Add("La tabla de abajo explica cada caso.");
            return string.Join(" ", parts);
        }

        // ------------------------------------------------------------------ review

        /// <summary>
        /// The detailed review shown after delivering: one row per problem (type, action chosen and
        /// expected, value left and expected, what happened) and the good months marked or changed.
        /// </summary>
        public CleaningReview Review()
        {
            var issues = Data.Issues.Select(ReviewIssue).ToList();
            var good = new List<GoodDataReview>();
            foreach (IssueMark mark in marks.Values.Where(m => Data.IssueAt(m.Code, m.Month) == null)
                         .OrderBy(m => m.Code, StringComparer.Ordinal).ThenBy(m => m.Month))
                good.Add(new GoodDataReview(mark.Code, mark.Month, Where(mark.Code, mark.Month), true,
                    IsChanged(mark.Code, mark.Month), WhyGood(mark)));
            foreach ((string code, int month) in CleanSheet == null ? Enumerable.Empty<(string, int)>() : ChangedGoodData())
            {
                if (MarkAt(code, month) != null) continue;
                good.Add(new GoodDataReview(code, month, Where(code, month), false, true,
                    $"En {Where(code, month)} cambiaste el dato del ERP ({Number(Data.Billed(code)[month])}) por " +
                    $"{CleanText(code, month)}, pero ese mes estaba bien: era venta real. Corregir un dato bueno daña la " +
                    "historia: el pronóstico aprendería algo que no pasó."));
            }
            return new CleaningReview(issues, good);
        }

        /// <summary>
        /// The player's marks whose cell in «Serie limpia» still has the ERP value (or still has a value,
        /// for «Excluir»). It only looks at what the player marked, so it gives nothing away before delivering.
        /// </summary>
        public IReadOnlyList<IssueMark> UncorrectedMarks() => marks.Values
            .Where(m => m.Action == CorrectionAction.Exclude ? CleanValue(m.Code, m.Month) != null : !IsChanged(m.Code, m.Month))
            .OrderBy(m => m.Code, StringComparer.Ordinal).ThenBy(m => m.Month).ToList();

        /// <summary>What Andrés says before delivering when marked problems are still uncorrected, or null.</summary>
        public string UncorrectedWarning()
        {
            IReadOnlyList<IssueMark> pending = UncorrectedMarks();
            if (pending.Count == 0) return null;
            string list = Join(pending.Take(4).Select(m => $"{Where(m.Code, m.Month)} (sigue en {CleanText(m.Code, m.Month)})"));
            string more = pending.Count > 4 ? $" y {pending.Count - 4} más" : string.Empty;
            string count = pending.Count == 1 ? "un problema" : $"{pending.Count} problemas";
            return $"Marcaste {count}, pero todavía no escribiste el valor corregido en «{CleanSheetName}»: {list}{more}. " +
                   "Si entregas así, el pronóstico va a aprender esos datos tal como vienen del ERP.";
        }

        /// <summary>
        /// After a delivery: the cells of «Serie limpia» to highlight (badly corrected problems the player
        /// marked, good months marked or changed by mistake), each with what is wrong.
        /// </summary>
        public Dictionary<(string Code, int Month), string> ReviewNotes()
        {
            var notes = new Dictionary<(string, int), string>();
            foreach (IssueReview issue in Data.Issues.Select(ReviewIssue))
            {
                if (issue.IsCorrect || issue.MarkedKind == null) continue;
                foreach (int month in issue.WrongMonths.Count > 0 ? issue.WrongMonths : issue.Issue.Months.Take(1).ToList())
                    notes[(issue.Issue.Code, month)] = issue.Message;
            }
            foreach (GoodDataReview good in Review().GoodData) notes[(good.Code, good.Month)] = good.Message;
            return notes;
        }

        private IssueReview ReviewIssue(DataIssue issue)
        {
            string where = Where(issue);
            List<IssueMark> onIssue = issue.Months.Select(m => MarkAt(issue.Code, m)).Where(m => m != null).ToList();
            IssueMark mark = onIssue.FirstOrDefault(m => m.Kind == issue.Kind) ?? onIssue.FirstOrDefault();

            // The accepted answer closest to what the player left.
            double?[] best = issue.AcceptedValues[0];
            List<int> wrongMonths = WrongMonths(issue, best);
            foreach (double?[] answer in issue.AcceptedValues.Skip(1))
            {
                List<int> wrong = WrongMonths(issue, answer);
                if (wrong.Count < wrongMonths.Count) (best, wrongMonths) = (answer, wrong);
            }
            bool untouched = issue.Kind == DataIssueKind.PreLaunch
                ? wrongMonths.Count == issue.Months.Count
                : issue.Months.All(m => !IsChanged(issue.Code, m));
            string player = PlayerValue(issue, wrongMonths);
            string expected = ExpectedValue(issue, best);

            IssueOutcome outcome;
            string message;
            if (mark == null)
            {
                outcome = IssueOutcome.NotMarked;
                message = $"No marcaste {where}: era un problema de tipo «{DataIssue.Name(issue.Kind)}». {Fix(issue, best)}";
            }
            else if (mark.Kind != issue.Kind)
            {
                outcome = IssueOutcome.WrongKind;
                message = $"Marcaste {where} como «{DataIssue.Name(mark.Kind)}», pero es «{DataIssue.Name(issue.Kind)}». {Fix(issue, best)}";
            }
            else if (untouched)
            {
                outcome = IssueOutcome.NotCorrected;
                message = $"Marcaste el problema de {where}, pero no escribiste el valor corregido en «{CleanSheetName}»: " +
                          $"sigue con {player}. {Fix(issue, best)}";
            }
            else if (mark.Action != issue.ExpectedAction)
            {
                outcome = IssueOutcome.WrongAction;
                message = $"En {where} escogiste «{DataIssue.Name(mark.Action)}», pero {KindPhrase(issue.Kind)}: la acción " +
                          $"correcta es «{DataIssue.Name(issue.ExpectedAction)}» ({ActionMeaning(issue.ExpectedAction)}).";
            }
            else if (wrongMonths.Count > 0)
            {
                outcome = IssueOutcome.WrongValue;
                message = $"En {where} dejaste {player}, y lo correcto es {expected}. {Fix(issue, best)}";
            }
            else
            {
                outcome = IssueOutcome.Correct;
                message = $"Bien: {where} quedó como pide el Atlas.";
            }
            return new IssueReview(issue, where, outcome, mark?.Kind, mark?.Action, player, expected, message, wrongMonths);
        }

        private List<int> WrongMonths(DataIssue issue, double?[] answer)
        {
            var wrong = new List<int>();
            for (int i = 0; i < issue.Months.Count; i++)
            {
                double? value = CleanValue(issue.Code, issue.Months[i]);
                bool ok = answer[i].HasValue ? value.HasValue && Tolerance.Matches(value.Value, answer[i].Value) : !value.HasValue;
                if (!ok) wrong.Add(issue.Months[i]);
            }
            return wrong;
        }

        private string PlayerValue(DataIssue issue, List<int> wrongMonths)
        {
            if (issue.Months.Count == 1) return CleanText(issue.Code, issue.Months[0]);
            int months = issue.Months.Count;
            if (wrongMonths.Count == 0)
                return issue.Kind == DataIssueKind.PreLaunch ? $"las {months} celdas vacías" : $"los {months} meses bien";
            int first = wrongMonths[0];
            string sample = $"{SalesCalendar.Label(first)} = {CleanText(issue.Code, first)}";
            return issue.Kind == DataIssueKind.PreLaunch
                ? $"{wrongMonths.Count} de {months} celdas con valor ({sample})"
                : $"{months - wrongMonths.Count} de {months} meses bien ({sample})";
        }

        private string ExpectedValue(DataIssue issue, double?[] best)
        {
            switch (issue.Kind)
            {
                case DataIssueKind.PreLaunch:
                    return $"las {issue.Months.Count} celdas vacías";
                case DataIssueKind.UnitsInBoxes:
                    int box = Data.Portfolio.Find(issue.Code).UnitsPerBox;
                    return $"cada mes × {box} unidades por caja ({SalesCalendar.Label(issue.Months[0])} = {Number(best[0] ?? 0)})";
                default:
                    string value = Number(best[0] ?? 0);
                    List<string> others = issue.AcceptedValues.Where(a => a != best).Select(a => Number(a[0] ?? 0)).ToList();
                    return others.Count == 0 ? value : $"{value} (también vale {Join(others, "o")})";
            }
        }

        /// <summary>How the Atlas corrects the problem, in a sentence.</summary>
        private string Fix(DataIssue issue, double?[] best)
        {
            string monthName = MonthNames[SalesCalendar.CalendarMonth(issue.Months[0])];
            switch (issue.Kind)
            {
                case DataIssueKind.PreLaunch:
                    return "Los meses antes del lanzamiento se dejan vacíos (Excluir), no en cero: no son demanda.";
                case DataIssueKind.UnitsInBoxes:
                    return $"Cada mes de {issue.Code} se multiplica por {Data.Portfolio.Find(issue.Code).UnitsPerBox}, las unidades " +
                           "por caja que dice el Maestro de materiales.";
                case DataIssueKind.OneOffOrder:
                    return $"Se quita el pedido único del cliente y el mes queda en {Number(issue.AcceptedValues[0][0] ?? 0)}.";
                default:
                    return issue.AcceptedValues.Count == 1
                        ? $"Se imputa con el promedio de {monthName} en los otros años: {Number(best[0] ?? 0)}."
                        : $"Se imputa con el promedio de {monthName} en los otros años o con el de los meses vecinos.";
            }
        }

        private static string KindPhrase(DataIssueKind kind) => kind switch
        {
            DataIssueKind.PreLaunch => "son meses antes del lanzamiento",
            DataIssueKind.UnitsInBoxes => "ese SKU viene en cajas y el resto en unidades",
            DataIssueKind.OneOffOrder => "es un pedido único de un cliente",
            _ => "es un cero que no es real"
        };

        private static string ActionMeaning(CorrectionAction action) => action switch
        {
            CorrectionAction.Exclude => "dejar las celdas vacías",
            CorrectionAction.Convert => "pasar las cajas a unidades",
            _ => "reemplazar el valor por uno calculado"
        };

        /// <summary>Why a marked month was good data.</summary>
        private string WhyGood(IssueMark mark)
        {
            string where = Where(mark.Code, mark.Month);
            IReadOnlyList<int> raw = Data.Billed(mark.Code);
            int value = raw[mark.Month];
            string start = $"Marcaste {where} ({Number(value)}) como «{DataIssue.Name(mark.Kind)}», pero es venta real, no un error.";
            List<int> sameMonth = Enumerable.Range(0, SalesCalendar.Months)
                .Where(m => m != mark.Month && SalesCalendar.CalendarMonth(m) == SalesCalendar.CalendarMonth(mark.Month) && raw[m] > 0)
                .Select(m => raw[m]).ToList();
            double mean = SeriesStatistics.Mean(raw);
            double sd = SeriesStatistics.StdDev(raw);
            double z = sd > 0 ? Math.Abs(value - mean) / sd : 0;
            string monthName = MonthNames[SalesCalendar.CalendarMonth(mark.Month)];

            string why;
            if (mark.Kind == DataIssueKind.FalseZero && value != 0)
                why = "No es un cero: el ERP muestra venta ese mes.";
            else if (mark.Kind == DataIssueKind.PreLaunch && value != 0)
                why = "El producto ya se vendía ese mes, así que no es un mes antes del lanzamiento.";
            else if (mark.Kind == DataIssueKind.UnitsInBoxes)
                why = $"{mark.Code} viene en unidades, como el resto de la tabla: ese mes está en el mismo rango que toda su historia.";
            else if (sameMonth.Count > 0 && value > mean && sameMonth.Average() > mean)
                why = $"En {monthName} de los otros años vendió {Join(sameMonth.Select(v => Number(v)))}, y su promedio de todos " +
                      $"los meses es {Number(mean)}: {monthName} es temporada, no un pedido único.";
            else
                why = $"Está a {z:0.0} desviaciones del promedio del SKU, y el Atlas solo lo trata como atípico por encima de 3.";
            return $"{start} {why} Corregir un dato bueno daña la historia: el pronóstico aprendería algo que no pasó.";
        }

        private bool IsChanged(string code, int month)
        {
            double? value = CleanValue(code, month);
            return !value.HasValue || !Tolerance.Matches(value.Value, Data.Billed(code)[month]);
        }

        /// <summary>The month in «Serie limpia» (the ERP value while there is no clean tab yet); null when empty.</summary>
        private double? CleanValue(string code, int month)
        {
            int row = HistoryTable.FindRow(CleanSheet, code);
            return row < 0 ? Data.Billed(code)[month] : HistoryTable.Month(CleanSheet, row, month);
        }

        private string CleanText(string code, int month)
        {
            double? value = CleanValue(code, month);
            return value.HasValue ? Number(value.Value) : "vacía";
        }

        private static string Where(string code, int month) => $"{code} {SalesCalendar.Label(month)}";

        private static string Where(DataIssue issue)
        {
            if (issue.Months.Count == 1) return Where(issue.Code, issue.Months[0]);
            if (issue.Months.Count == SalesCalendar.Months)
                return $"{issue.Code}, toda la fila ({SalesCalendar.Label(0)} a {SalesCalendar.Label(SalesCalendar.Months - 1)})";
            return $"{issue.Code} {SalesCalendar.Label(issue.Months[0])} a {SalesCalendar.Label(issue.Months[issue.Months.Count - 1])}";
        }

        private static string Number(double value) => HudFormat.Number((long)Math.Round(value));

        private static string Join(IEnumerable<string> items, string last = "y")
        {
            List<string> list = items.ToList();
            return list.Count <= 1 ? string.Join(string.Empty, list)
                : string.Join(", ", list.Take(list.Count - 1)) + $" {last} " + list[list.Count - 1];
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
