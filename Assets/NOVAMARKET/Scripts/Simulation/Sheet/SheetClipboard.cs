using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// Copy and paste of the planner sheet (Ctrl+C / Ctrl+V). Copying keeps what each cell holds (formula,
    /// value and format) as it was at that moment; pasting moves the relative references of the formulas,
    /// as a spreadsheet does, or writes only the computed values. A whole row pastes from column A.
    /// The copy also gives tab-separated text for the system clipboard, and text copied elsewhere (one
    /// line per row, tabs between columns) can be pasted too.
    /// </summary>
    public sealed class SheetClipboard
    {
        private sealed class Item
        {
            public int Column;
            public int Row;
            public string Input;     // what the player typed ("=…" for formulas); null when empty
            public CellValue Value;  // computed value at copy time
            public CellFormat Format;
        }

        private readonly List<Item> items = new List<Item>();
        private CellAddress copyStart;

        public bool HasContent => Size.HasValue;

        /// <summary>Width and height of what was copied.</summary>
        public (int Columns, int Rows)? Size { get; private set; }

        /// <summary>Tab-separated text of the copied values (for the system clipboard).</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>
        /// Copies <paramref name="range"/>. Whole rows or columns are trimmed to the used part of the tab.
        /// Returns the copied range.
        /// </summary>
        public CellRange Copy(Worksheet sheet, CellRange range)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            int lastColumn = Math.Min(range.End.Column, Math.Max(range.Start.Column, sheet.UsedColumns - 1));
            int lastRow = Math.Min(range.End.Row, Math.Max(range.Start.Row, sheet.UsedRows - 1));
            var copied = new CellRange(range.Start, new CellAddress(lastColumn, lastRow));

            items.Clear();
            var text = new StringBuilder();
            for (int row = copied.Start.Row; row <= copied.End.Row; row++)
            {
                for (int column = copied.Start.Column; column <= copied.End.Column; column++)
                {
                    var address = new CellAddress(column, row);
                    string input = sheet.GetInput(address);
                    items.Add(new Item
                    {
                        Column = column - copied.Start.Column,
                        Row = row - copied.Start.Row,
                        Input = input.Length == 0 ? null : input,
                        Value = sheet.Value(address),
                        Format = sheet.GetFormat(address)
                    });
                    if (column > copied.Start.Column) text.Append('\t');
                    text.Append(sheet.Display(address));
                }
                if (row < copied.End.Row) text.Append('\n');
            }
            copyStart = copied.Start;
            Size = (copied.Width, copied.Height);
            Text = text.ToString();
            return copied;
        }

        /// <summary>
        /// Pastes at <paramref name="at"/> (the top-left cell of the target). With <paramref name="valuesOnly"/>
        /// the cells get the values the copy showed, without formulas, keeping the target's format.
        /// Returns the pasted range, or null when there is nothing to paste.
        /// </summary>
        public CellRange? Paste(Worksheet sheet, CellAddress at, bool valuesOnly = false)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            if (!Size.HasValue) return null;
            (int columns, int rows) = Size.Value;
            if (at.Column + columns > CellAddress.MaxColumns || at.Row + rows > CellAddress.MaxRows) return null;

            sheet.Book.Batch(() =>
            {
                foreach (Item item in items)
                {
                    var to = new CellAddress(at.Column + item.Column, at.Row + item.Row);
                    if (valuesOnly)
                    {
                        sheet.SetValue(to, item.Value);
                        continue;
                    }
                    if (item.Input == null) sheet.SetValue(to, CellValue.Empty);
                    else if (item.Input.StartsWith("=") && item.Input.Length > 1)
                        sheet.SetInput(to, "=" + Shift(item.Input.Substring(1), at.Column - copyStart.Column, at.Row - copyStart.Row));
                    else if (item.Value.IsEmpty) sheet.SetInput(to, item.Input);
                    else sheet.SetValue(to, item.Value);
                    sheet.SetFormat(to, item.Format);
                }
            });
            return new CellRange(at, new CellAddress(at.Column + columns - 1, at.Row + rows - 1));
        }

        /// <summary>
        /// Pastes text copied outside the game: one line per row, tabs between columns. Each piece is
        /// read as if typed ("4.695", "=SUMA(…)", text).
        /// </summary>
        public static CellRange? PasteText(Worksheet sheet, CellAddress at, string text)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            if (string.IsNullOrEmpty(text)) return null;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
            int width = lines.Max(l => l.Split('\t').Length);
            if (at.Column + width > CellAddress.MaxColumns || at.Row + lines.Length > CellAddress.MaxRows) return null;
            sheet.Book.Batch(() =>
            {
                for (int r = 0; r < lines.Length; r++)
                {
                    string[] pieces = lines[r].Split('\t');
                    for (int c = 0; c < pieces.Length; c++) sheet.SetInput(new CellAddress(at.Column + c, at.Row + r), pieces[c]);
                }
            });
            return new CellRange(at, new CellAddress(at.Column + width - 1, at.Row + lines.Length - 1));
        }

        private static string Shift(string formula, int columns, int rows)
        {
            try
            {
                return FormulaShifter.Shift(formula, columns, rows);
            }
            catch (FormulaException)
            {
                return formula; // a reference would fall off the sheet: keep the formula as it was
            }
        }
    }
}
