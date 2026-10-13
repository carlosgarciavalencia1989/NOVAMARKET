using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Sheet;
using NovaMarket.Tests.Simulation.Portfolio;
using PortfolioModel = NovaMarket.Simulation.Portfolio.Portfolio;

namespace NovaMarket.Tests.Simulation.Levels
{
    /// <summary>Scripted players for the level tests: they use the ERP and the sheet as the UI would.</summary>
    public static class LevelPlayers
    {
        /// <summary>The portfolio the game uses (Sector_Cosmetica, seed 2026).</summary>
        public static PortfolioModel Portfolio => portfolio ??= PortfolioTestData.Generate(2026);
        private static PortfolioModel portfolio;

        public static Worksheet Download(LevelSession level, bool projection = false) =>
            level.Erp.Download(level.Erp.Billing(new BillingQuery { Codes = level.RequestedCodes, IncludeProjection = projection }),
                level.Book);

        /// <summary>1.1 done right: periods and totals by dragging, as Andrés explains.</summary>
        public static void SolveGather(GatherHistoryLevel level, bool projection = false)
        {
            Worksheet sheet = Download(level, projection);
            sheet.SetInput("D1", "2");
            sheet.Fill(CellRange.Parse("C1:D1"), CellRange.Parse("C1:AT1"));
            int last = 2 + level.RequestedCodes.Count;
            sheet.Fill(CellRange.Parse("AW3"), CellRange.Parse($"AW3:AW{last}"));
        }

        /// <summary>1.2: marks every problem with its action and writes the model answer in «Serie limpia».</summary>
        public static void SolveClean(CleanDataLevel level, bool formulas = false)
        {
            Worksheet raw = Download(level);
            Worksheet clean = level.CleanSheet;
            foreach (DataIssue issue in level.Data.Issues)
            {
                level.Mark(new IssueMark(issue.Code, issue.Months[0], issue.Kind, issue.ExpectedAction, "documentado"));
                int row = HistoryTable.FindRow(clean, issue.Code);
                double?[] answer = issue.AcceptedValues[0];
                for (int i = 0; i < issue.Months.Count; i++)
                {
                    var cell = new CellAddress(HistoryTable.MonthColumn(issue.Months[i]), row);
                    if (!answer[i].HasValue) clean.SetInput(cell, string.Empty);
                    else if (formulas) clean.SetInput(cell, Formula(level, raw, issue, i, row));
                    else clean.SetNumber(cell, answer[i].Value);
                }
            }
        }

        private static string Formula(CleanDataLevel level, Worksheet raw, DataIssue issue, int index, int row)
        {
            int month = issue.Months[index];
            string column = CellAddress.ColumnName(HistoryTable.MonthColumn(month));
            string source = $"Facturación!{column}{row + 1}";
            switch (issue.Kind)
            {
                case DataIssueKind.UnitsInBoxes:
                    return $"={source}*{level.Data.Portfolio.Find(issue.Code).UnitsPerBox}";
                case DataIssueKind.OneOffOrder:
                    return $"={source}-{level.Data.OrderAt(issue.Code, month).Units}";
                default:
                    var others = Enumerable.Range(0, 4).Select(k => month % 12 + 12 * k).Where(m => m != month && m < 44)
                        .Select(m => $"Facturación!{CellAddress.ColumnName(HistoryTable.MonthColumn(m))}{row + 1}");
                    return $"=PROMEDIO({string.Join(";", others)})";
            }
        }
    }
}
