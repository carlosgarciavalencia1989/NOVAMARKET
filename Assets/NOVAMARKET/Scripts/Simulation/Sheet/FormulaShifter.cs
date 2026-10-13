using System.Collections.Generic;
using System.Text;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// Rewrites a formula copied to another cell, as Excel does when dragging: relative references
    /// move with the cell, references with $ stay. A reference pushed outside the sheet becomes #¡REF!.
    /// The rest of the text is kept as the player wrote it.
    /// </summary>
    public static class FormulaShifter
    {
        /// <param name="formula">Formula without the leading "=".</param>
        public static string Shift(string formula, int columns, int rows)
        {
            if (columns == 0 && rows == 0) return formula;
            List<FormulaToken> tokens = FormulaLexer.Tokenize(formula);
            var builder = new StringBuilder(formula.Length + 8);
            int copied = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                FormulaToken token = tokens[i];
                if (token.Kind != TokenKind.Reference) continue;
                bool isRange = i + 2 < tokens.Count && tokens[i + 1].Kind == TokenKind.Colon && tokens[i + 2].Kind == TokenKind.Reference;
                FormulaToken last = isRange ? tokens[i + 2] : token;

                builder.Append(formula, copied, token.Start - copied);
                bool firstOk = TryMove(token, columns, rows, out string first);
                bool lastOk = !isRange || TryMove(last, columns, rows, out _);
                if (!firstOk || !lastOk)
                {
                    builder.Append("#¡REF!");
                }
                else if (isRange)
                {
                    TryMove(last, columns, rows, out string second);
                    builder.Append(first).Append(':').Append(second);
                }
                else
                {
                    builder.Append(first);
                }
                copied = last.Start + last.Length;
                if (isRange) i += 2;
            }
            builder.Append(formula, copied, formula.Length - copied);
            return builder.ToString();
        }

        private static bool TryMove(FormulaToken reference, int columns, int rows, out string text)
        {
            int column = reference.ColumnAbsolute ? reference.Column : reference.Column + columns;
            int row = reference.RowAbsolute ? reference.Row : reference.Row + rows;
            text = null;
            if (!CellAddress.IsValid(column, row)) return false;
            text = (reference.SheetText != null ? reference.SheetText + "!" : string.Empty)
                + (reference.ColumnAbsolute ? "$" : string.Empty) + CellAddress.ColumnName(column)
                + (reference.RowAbsolute ? "$" : string.Empty) + (row + 1);
            return true;
        }
    }
}
