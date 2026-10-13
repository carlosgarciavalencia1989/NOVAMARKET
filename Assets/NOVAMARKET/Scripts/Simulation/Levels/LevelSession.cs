using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Sheet;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>
    /// A level being played: its ERP (world 1 screens), the player's planner sheet, the Atlas help
    /// log and the lines the characters say. The UI drives it; <see cref="Deliver"/> validates.
    /// </summary>
    public abstract class LevelSession
    {
        protected LevelSession(LevelInfo info, ErpDataSet data, int seed)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Seed = seed;
            Erp = new NovaErp(data, ForecastWorld.Number);
            Book = new Workbook();
            Help = new AtlasHelpLog();
            Erp.Downloaded += (table, sheet) => OnDownloaded(table, sheet);
        }

        public LevelInfo Info { get; }
        public ErpDataSet Data { get; }
        public int Seed { get; }
        public NovaErp Erp { get; }
        public Workbook Book { get; }
        public AtlasHelpLog Help { get; }

        /// <summary>Raised when a character says something during play (hints after a download…).</summary>
        public event Action<DialogueLine> Said;

        /// <summary>The SKUs the level asks for.</summary>
        public abstract IReadOnlyList<string> RequestedCodes { get; }

        /// <summary>What Andrés and Sofía say when the level opens.</summary>
        public abstract IReadOnlyList<DialogueLine> Intro { get; }

        /// <summary>The checklist of the level, computed from the sheet.</summary>
        public abstract IReadOnlyList<LevelObjective> Objectives();

        /// <summary>Topics the Atlas can solve (each one costs the "sin ayuda" star).</summary>
        public abstract IReadOnlyList<string> HelpTopics { get; }

        public LevelResult Result { get; private set; }

        /// <summary>Validates the player's sheet and keeps the result.</summary>
        public LevelResult Deliver() => Result = Evaluate();

        /// <summary>"Pedir ayuda al Atlas": the solved step. Recorded in <see cref="Help"/>.</summary>
        public AtlasAnswer AskAtlas(string topic)
        {
            AtlasAnswer answer = Explain(topic) ?? throw new ArgumentException($"No Atlas help for '{topic}'.", nameof(topic));
            Help.Record(topic);
            return answer;
        }

        protected abstract AtlasAnswer Explain(string topic);
        protected abstract LevelResult Evaluate();
        protected abstract void OnDownloaded(ErpTable table, Worksheet sheet);

        protected void Say(DialogueLine line) => Said?.Invoke(line);

        /// <summary>A billing-by-month result (not the invoice detail nor the material master).</summary>
        protected static bool IsBillingByMonth(ErpTable table) =>
            table.ScreenId == ErpScreens.Billing && table.Headers.Count > 0 && table.Headers[0] == "Material";
    }

    /// <summary>Reading a history table with the Atlas layout (codes in column A from row 3, months in C…AT).</summary>
    public static class HistoryTable
    {
        public const int CodeColumn = 0;
        public const int FirstMonthColumn = 2;

        public static int MonthColumn(int month) => FirstMonthColumn + month;

        /// <summary>Row of a SKU code in column A, or -1.</summary>
        public static int FindRow(Worksheet sheet, string code)
        {
            if (sheet == null) return -1;
            for (int row = ErpTable.FirstDataRow; row < sheet.UsedRows; row++)
            {
                CellValue value = sheet.Value(new CellAddress(CodeColumn, row));
                if (value.IsText && value.Text == code) return row;
            }
            return -1;
        }

        /// <summary>Codes listed in column A from row 3 down to the first empty cell.</summary>
        public static List<string> Codes(Worksheet sheet)
        {
            var codes = new List<string>();
            for (int row = ErpTable.FirstDataRow; sheet != null && row < sheet.UsedRows; row++)
            {
                CellValue value = sheet.Value(new CellAddress(CodeColumn, row));
                if (!value.IsText || value.Text.Length == 0) break;
                codes.Add(value.Text);
            }
            return codes;
        }

        /// <summary>A month of a row as a number, or null when empty or not a number (excluded).</summary>
        public static double? Month(Worksheet sheet, int row, int month)
        {
            CellValue value = sheet.Value(new CellAddress(MonthColumn(month), row));
            return value.IsNumber ? value.Number : (double?)null;
        }

        public static double?[] Series(Worksheet sheet, int row, int months)
        {
            var series = new double?[months];
            for (int m = 0; m < months; m++) series[m] = Month(sheet, row, m);
            return series;
        }
    }
}
