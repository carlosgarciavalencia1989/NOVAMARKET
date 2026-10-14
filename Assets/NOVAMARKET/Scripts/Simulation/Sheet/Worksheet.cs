using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// One tab of the planner sheet. Each cell holds what the player typed (a number, a text or a
    /// formula starting with "="), its format and its computed value.
    /// </summary>
    public sealed class Worksheet
    {
        private sealed class Cell
        {
            public string FormulaText;      // without "="; null for constants
            public FormulaNode Formula;
            public string ParseError;
            public CellValue Constant;
            public CellFormat Format;
            public CellValue Cached;
            public int CachedVersion = -1;
            public bool Evaluating;

            public bool HasContent => FormulaText != null || !Constant.IsEmpty;
        }

        private readonly Workbook book;
        private readonly Dictionary<CellAddress, Cell> cells = new Dictionary<CellAddress, Cell>();

        internal Worksheet(Workbook book, string name)
        {
            this.book = book;
            Name = name;
        }

        public string Name { get; }
        public Workbook Book => book;

        /// <summary>Columns and rows up to the last cell with content or format.</summary>
        public int UsedColumns => cells.Count == 0 ? 0 : cells.Keys.Max(c => c.Column) + 1;
        public int UsedRows => cells.Count == 0 ? 0 : cells.Keys.Max(c => c.Row) + 1;

        public IEnumerable<CellAddress> UsedCells => cells.Where(c => c.Value.HasContent).Select(c => c.Key);

        // ------------------------------------------------------------------ editing

        /// <summary>
        /// Writes what the player typed: "=…" is a formula, "4.695" or "45%" a number, "VERDADERO" a
        /// boolean, "'123" the text 123, anything else text. Empty clears the content (keeps the format).
        /// </summary>
        public void SetInput(CellAddress address, string input)
        {
            Remember(address);
            Cell cell = GetOrCreate(address);
            cell.FormulaText = null;
            cell.Formula = null;
            cell.ParseError = null;
            cell.Constant = CellValue.Empty;
            input ??= string.Empty;

            if (input.StartsWith("=") && input.Length > 1)
            {
                cell.FormulaText = FormulaLexer.Normalize(input.Substring(1));
                try
                {
                    cell.Formula = FormulaParser.Parse(cell.FormulaText);
                }
                catch (FormulaException e)
                {
                    cell.ParseError = e.Message;
                }
            }
            else if (input.StartsWith("'"))
            {
                cell.Constant = CellValue.Of(input.Substring(1));
            }
            else if (input.Trim().Length > 0)
            {
                string upper = input.Trim().ToUpperInvariant();
                cell.Constant = SheetNumber.TryParse(input, out double number) ? CellValue.Of(number)
                    : upper == "VERDADERO" || upper == "FALSO" ? CellValue.Of(upper == "VERDADERO")
                    : CellValue.Of(input);
            }
            Cleanup(address, cell);
            book.Touch();
        }

        public void SetInput(string address, string input) => SetInput(CellAddress.Parse(address), input);

        public void SetNumber(CellAddress address, double value) => SetConstant(address, CellValue.Of(value));
        public void SetText(CellAddress address, string text) => SetConstant(address, CellValue.Of(text));

        private void SetConstant(CellAddress address, CellValue value)
        {
            Remember(address);
            Cell cell = GetOrCreate(address);
            cell.FormulaText = null;
            cell.Formula = null;
            cell.ParseError = null;
            cell.Constant = value;
            Cleanup(address, cell);
            book.Touch();
        }

        public void SetFormat(CellRange range, CellFormat format)
        {
            foreach (CellAddress address in range.Cells())
            {
                Remember(address);
                Cell cell = GetOrCreate(address);
                cell.Format = format;
                Cleanup(address, cell);
            }
            book.Touch();
        }

        public void Clear(CellRange range)
        {
            foreach (CellAddress address in range.Cells())
            {
                if (!cells.ContainsKey(address)) continue;
                Remember(address);
                cells.Remove(address);
            }
            book.Touch();
        }

        /// <summary>Copies a cell (content and format) moving its relative references.</summary>
        public void CopyCell(CellAddress from, CellAddress to)
        {
            cells.TryGetValue(from, out Cell source);
            if (source == null)
            {
                Remember(to);
                cells.Remove(to);
                book.Touch();
                return;
            }
            if (source.FormulaText != null)
            {
                string shifted;
                try
                {
                    shifted = FormulaShifter.Shift(source.FormulaText, to.Column - from.Column, to.Row - from.Row);
                }
                catch (FormulaException)
                {
                    shifted = source.FormulaText;
                }
                SetInput(to, "=" + shifted);
            }
            else
            {
                SetConstant(to, source.Constant);
            }
            GetOrCreate(to).Format = source.Format;
        }

        /// <summary>
        /// The fill handle: extends <paramref name="source"/> to cover <paramref name="target"/> (down,
        /// up, right or left). Formulas are copied moving their references; two or more numbers in an
        /// even step continue the series (1, 2 → 3, 4…); other constants repeat.
        /// </summary>
        public void Fill(CellRange source, CellRange target)
        {
            bool vertical = target.Start.Column == source.Start.Column && target.End.Column == source.End.Column
                && target.Start.Row <= source.Start.Row && target.End.Row >= source.End.Row;
            bool horizontal = target.Start.Row == source.Start.Row && target.End.Row == source.End.Row
                && target.Start.Column <= source.Start.Column && target.End.Column >= source.End.Column;
            if (!vertical && !horizontal)
                throw new ArgumentException("The target must extend the source in one direction.", nameof(target));
            if (target.Equals(source)) return;

            book.Batch(() =>
            {
                int lines = vertical ? source.Width : source.Height;
                int length = vertical ? source.Height : source.Width;
                for (int line = 0; line < lines; line++)
                {
                    CellAddress SourceAt(int i) => vertical
                        ? new CellAddress(source.Start.Column + line, source.Start.Row + i)
                        : new CellAddress(source.Start.Column + i, source.Start.Row + line);
                    double? step = SeriesStep(Enumerable.Range(0, length).Select(SourceAt).ToList(), out double first);

                    foreach (CellAddress to in target.Cells())
                    {
                        if (source.Contains(to) || (vertical ? to.Column - source.Start.Column : to.Row - source.Start.Row) != line) continue;
                        int index = vertical ? to.Row - source.Start.Row : to.Column - source.Start.Column;
                        int wrapped = ((index % length) + length) % length;
                        CellAddress from = SourceAt(wrapped);
                        if (step.HasValue)
                        {
                            SetNumber(to, first + step.Value * index);
                            GetOrCreate(to).Format = cells[from].Format;
                        }
                        else
                        {
                            CopyCell(from, to);
                        }
                    }
                }
            });
        }

        /// <summary>Step of an even numeric series of two or more constants, or null.</summary>
        private double? SeriesStep(List<CellAddress> addresses, out double first)
        {
            first = 0;
            if (addresses.Count < 2) return null;
            var values = new List<double>();
            foreach (CellAddress address in addresses)
            {
                if (!cells.TryGetValue(address, out Cell cell) || cell.FormulaText != null || !cell.Constant.IsNumber) return null;
                values.Add(cell.Constant.Number);
            }
            double step = values[1] - values[0];
            for (int i = 2; i < values.Count; i++)
                if (Math.Abs(values[i] - values[i - 1] - step) > 1e-9) return null;
            first = values[0];
            return step;
        }

        // ------------------------------------------------------------------ reading

        public CellValue Value(CellAddress address)
        {
            if (!cells.TryGetValue(address, out Cell cell)) return CellValue.Empty;
            if (cell.FormulaText == null) return cell.Constant;
            if (cell.Formula == null) return CellValue.Fail(SheetError.Syntax);
            if (cell.CachedVersion == book.Version) return cell.Cached;
            if (cell.Evaluating) return CellValue.Fail(SheetError.Circular);

            cell.Evaluating = true;
            CellValue value;
            try
            {
                value = cell.Formula.Evaluate(new EvaluationScope(book, Name));
            }
            finally
            {
                cell.Evaluating = false;
            }
            if (value.IsEmpty) value = CellValue.Of(0); // =B3 of an empty cell shows 0, as in Excel
            cell.Cached = value;
            cell.CachedVersion = book.Version;
            return value;
        }

        public CellValue Value(string address) => Value(CellAddress.Parse(address));

        /// <summary>What goes in the formula bar: "=SUMA(B2:B9)", "4694,67", "'123" or text.</summary>
        public string GetInput(CellAddress address)
        {
            if (!cells.TryGetValue(address, out Cell cell)) return string.Empty;
            if (cell.FormulaText != null) return "=" + cell.FormulaText;
            CellValue value = cell.Constant;
            if (value.IsText && (SheetNumber.TryParse(value.Text, out _) || value.Text.StartsWith("=")
                || value.Text.Equals("VERDADERO", StringComparison.OrdinalIgnoreCase)
                || value.Text.Equals("FALSO", StringComparison.OrdinalIgnoreCase)))
                return "'" + value.Text;
            return value.ToString();
        }

        public bool IsFormula(CellAddress address) => cells.TryGetValue(address, out Cell cell) && cell.FormulaText != null;

        /// <summary>The Spanish explanation of a formula that could not be read, or null.</summary>
        public string FormulaError(CellAddress address) => cells.TryGetValue(address, out Cell cell) ? cell.ParseError : null;

        public CellFormat GetFormat(CellAddress address) => cells.TryGetValue(address, out Cell cell) ? cell.Format : CellFormat.General;

        /// <summary>The value as the grid shows it, with the cell's format.</summary>
        public string Display(CellAddress address)
        {
            CellValue value = Value(address);
            return value.IsNumber ? SheetNumber.Format(value.Number, GetFormat(address)) : value.ToString();
        }

        public string Display(string address) => Display(CellAddress.Parse(address));

        /// <summary>Writes a value as a constant (paste values); errors are written as their text.</summary>
        public void SetValue(CellAddress address, CellValue value)
        {
            if (value.IsEmpty)
            {
                Remember(address);
                Cell cell = GetOrCreate(address);
                cell.FormulaText = null;
                cell.Formula = null;
                cell.ParseError = null;
                cell.Constant = CellValue.Empty;
                Cleanup(address, cell);
                book.Touch();
                return;
            }
            SetConstant(address, value.IsError ? CellValue.Of(value.ToString()) : value);
        }

        /// <summary>Sets the format of one cell (keeps its content).</summary>
        public void SetFormat(CellAddress address, CellFormat format) => SetFormat(new CellRange(address, address), format);

        /// <summary>
        /// The cells a formula reads (each cell of a range, up to <paramref name="limit"/>), with their tab.
        /// Empty for constants.
        /// </summary>
        public IReadOnlyList<(string Sheet, CellAddress Cell)> Precedents(CellAddress address, int limit = 2000)
        {
            var found = new List<(string, CellAddress)>();
            if (cells.TryGetValue(address, out Cell cell) && cell.Formula != null) Collect(cell.Formula, found, limit);
            return found;
        }

        private void Collect(FormulaNode node, List<(string, CellAddress)> found, int limit)
        {
            if (found.Count >= limit) return;
            switch (node)
            {
                case ReferenceNode reference:
                    found.Add((reference.Sheet ?? Name, reference.Cell));
                    break;
                case RangeNode range:
                    foreach (CellAddress cell in range.Range.Cells())
                    {
                        if (found.Count >= limit) return;
                        found.Add((range.Sheet ?? Name, cell));
                    }
                    break;
                case UnaryNode unary:
                    Collect(unary.Operand, found, limit);
                    break;
                case BinaryNode binary:
                    Collect(binary.Left, found, limit);
                    Collect(binary.Right, found, limit);
                    break;
                case FunctionNode function:
                    foreach (FormulaNode argument in function.Arguments) Collect(argument, found, limit);
                    break;
            }
        }

        // ------------------------------------------------------------------ undo

        /// <summary>A copy of one cell (content and format) kept by the undo history; null = empty cell.</summary>
        internal sealed class CellState
        {
            internal string FormulaText;
            internal FormulaNode Formula;
            internal string ParseError;
            internal CellValue Constant;
            internal CellFormat Format;

            internal static bool Same(CellState a, CellState b) =>
                a == null || b == null
                    ? a == b
                    : a.FormulaText == b.FormulaText && a.Constant.Equals(b.Constant) && a.Format.Equals(b.Format);
        }

        internal CellState Capture(CellAddress address) =>
            cells.TryGetValue(address, out Cell cell) && (cell.HasContent || !cell.Format.Equals(CellFormat.General))
                ? new CellState
                {
                    FormulaText = cell.FormulaText, Formula = cell.Formula, ParseError = cell.ParseError,
                    Constant = cell.Constant, Format = cell.Format
                }
                : null;

        /// <summary>Puts a cell back as it was (undo and redo; it is not recorded again).</summary>
        internal void Restore(CellAddress address, CellState state)
        {
            if (state == null)
            {
                cells.Remove(address);
            }
            else
            {
                Cell cell = GetOrCreate(address);
                cell.FormulaText = state.FormulaText;
                cell.Formula = state.Formula;
                cell.ParseError = state.ParseError;
                cell.Constant = state.Constant;
                cell.Format = state.Format;
                cell.CachedVersion = -1;
            }
            book.Touch();
        }

        private void Remember(CellAddress address) => book.History.Remember(this, address);

        private Cell GetOrCreate(CellAddress address)
        {
            if (!cells.TryGetValue(address, out Cell cell)) cells[address] = cell = new Cell();
            return cell;
        }

        private void Cleanup(CellAddress address, Cell cell)
        {
            if (!cell.HasContent && cell.Format.Equals(CellFormat.General)) cells.Remove(address);
        }
    }
}
