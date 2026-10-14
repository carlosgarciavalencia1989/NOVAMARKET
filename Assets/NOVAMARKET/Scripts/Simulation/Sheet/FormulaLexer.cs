using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NovaMarket.Simulation.Sheet
{
    public enum TokenKind
    {
        Number,
        Text,
        Boolean,
        Reference,
        ErrorLiteral,
        Function,
        Name,
        Colon,
        Semicolon,
        OpenParen,
        CloseParen,
        Operator,
        Percent,
        End
    }

    /// <summary>A piece of a formula with its place in the text (to rewrite references when copying).</summary>
    public sealed class FormulaToken
    {
        public TokenKind Kind { get; set; }
        public string Text { get; set; }
        public int Start { get; set; }
        public int Length => Text.Length;
        public double Number { get; set; }

        /// <summary>Content of a text literal, without quotes.</summary>
        public string StringValue { get; set; }

        // References: optional sheet, and $ marks.
        public string Sheet { get; set; }
        public string SheetText { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public bool ColumnAbsolute { get; set; }
        public bool RowAbsolute { get; set; }

        public override string ToString() => $"{Kind} '{Text}'";
    }

    /// <summary>Thrown when a formula cannot be read; the message is shown to the player in Spanish.</summary>
    public sealed class FormulaException : Exception
    {
        public FormulaException(string message, int position) : base(message) => Position = position;
        public int Position { get; }
    }

    /// <summary>
    /// Splits a formula (without the leading "=") written with the syntax of Excel in Spanish:
    /// decimal comma, ";" between arguments, references like B3, $C$3, Datos!H4 or 'Serie limpia'!B3,
    /// and function names with dots (DESVEST.P).
    /// </summary>
    public static class FormulaLexer
    {
        private const string Letter = @"A-Za-zÁÉÍÓÚÜÑáéíóúüñ_";
        private static readonly Regex ReferencePattern = new Regex(
            @"\G(?:(?<sheet>'(?:[^']|'')+'|[" + Letter + @"][" + Letter + @"0-9\.]*)!)?(?<ca>\$?)(?<col>[A-Za-z]{1,2})(?<ra>\$?)(?<row>[0-9]+)(?![" + Letter + @"0-9\.\(!])",
            RegexOptions.CultureInvariant);
        private static readonly Regex NamePattern = new Regex(@"\G[" + Letter + @"][" + Letter + @"0-9\.]*", RegexOptions.CultureInvariant);
        private static readonly Regex NumberPattern = new Regex(@"\G[0-9]+(?:,[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);
        private static readonly string[] ErrorLiterals = { "#¡DIV/0!", "#¡VALOR!", "#¡REF!", "#¿NOMBRE?", "#N/A", "#¡NUM!" };

        public static List<FormulaToken> Tokenize(string formula)
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            var tokens = new List<FormulaToken>();
            int i = 0;
            while (i < formula.Length)
            {
                char c = formula[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                Match reference = ReferencePattern.Match(formula, i);
                if (reference.Success)
                {
                    int column = CellAddress.ColumnIndex(reference.Groups["col"].Value);
                    if (column >= 0 && int.TryParse(reference.Groups["row"].Value, out int row) && row >= 1 && row <= CellAddress.MaxRows)
                    {
                        string sheetText = reference.Groups["sheet"].Success ? reference.Groups["sheet"].Value : null;
                        tokens.Add(new FormulaToken
                        {
                            Kind = TokenKind.Reference, Text = reference.Value, Start = i,
                            SheetText = sheetText, Sheet = SheetName(sheetText),
                            Column = column, Row = row - 1,
                            ColumnAbsolute = reference.Groups["ca"].Value == "$", RowAbsolute = reference.Groups["ra"].Value == "$"
                        });
                        i += reference.Length;
                        continue;
                    }
                }

                if (char.IsDigit(c))
                {
                    Match number = NumberPattern.Match(formula, i);
                    if (!SheetNumber.TryParse(number.Value.Split('e', 'E')[0], out double mantissa))
                        throw new FormulaException($"Número no válido: {number.Value}", i);
                    double value = mantissa;
                    int exponent = number.Value.IndexOfAny(new[] { 'e', 'E' });
                    if (exponent >= 0) value *= Math.Pow(10, int.Parse(number.Value.Substring(exponent + 1)));
                    tokens.Add(new FormulaToken { Kind = TokenKind.Number, Text = number.Value, Start = i, Number = value });
                    i += number.Length;
                    continue;
                }

                if (c == '"')
                {
                    int end = i + 1;
                    var text = new System.Text.StringBuilder();
                    while (true)
                    {
                        if (end >= formula.Length) throw new FormulaException("Falta cerrar las comillas.", i);
                        if (formula[end] == '"')
                        {
                            if (end + 1 < formula.Length && formula[end + 1] == '"')
                            {
                                text.Append('"');
                                end += 2;
                                continue;
                            }
                            break;
                        }
                        text.Append(formula[end++]);
                    }
                    tokens.Add(new FormulaToken { Kind = TokenKind.Text, Text = formula.Substring(i, end - i + 1), Start = i, StringValue = text.ToString() });
                    i = end + 1;
                    continue;
                }

                if (c == '#')
                {
                    string literal = Array.Find(ErrorLiterals, e => string.CompareOrdinal(formula, i, e, 0, e.Length) == 0);
                    if (literal == null) throw new FormulaException("Error no reconocido después de #.", i);
                    tokens.Add(new FormulaToken { Kind = TokenKind.ErrorLiteral, Text = literal, Start = i });
                    i += literal.Length;
                    continue;
                }

                Match name = NamePattern.Match(formula, i);
                if (name.Success)
                {
                    int next = i + name.Length;
                    while (next < formula.Length && char.IsWhiteSpace(formula[next])) next++;
                    string upper = name.Value.ToUpperInvariant();
                    TokenKind kind = next < formula.Length && formula[next] == '(' ? TokenKind.Function
                        : upper == "VERDADERO" || upper == "FALSO" ? TokenKind.Boolean
                        : TokenKind.Name;
                    tokens.Add(new FormulaToken { Kind = kind, Text = name.Value, Start = i, Number = upper == "VERDADERO" ? 1 : 0 });
                    i += name.Length;
                    continue;
                }

                string two = i + 1 < formula.Length ? formula.Substring(i, 2) : null;
                if (two == "<>" || two == "<=" || two == ">=")
                {
                    tokens.Add(new FormulaToken { Kind = TokenKind.Operator, Text = two, Start = i });
                    i += 2;
                    continue;
                }

                TokenKind? single = c switch
                {
                    ':' => TokenKind.Colon,
                    ';' => TokenKind.Semicolon,
                    '(' => TokenKind.OpenParen,
                    ')' => TokenKind.CloseParen,
                    '%' => TokenKind.Percent,
                    '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>' => TokenKind.Operator,
                    _ => null
                };
                if (single == null)
                {
                    string hint = c == ',' ? " Separa los argumentos con punto y coma (;)." : string.Empty;
                    throw new FormulaException($"No se entiende el carácter '{c}'.{hint}", i);
                }
                tokens.Add(new FormulaToken { Kind = single.Value, Text = c.ToString(), Start = i });
                i++;
            }
            tokens.Add(new FormulaToken { Kind = TokenKind.End, Text = string.Empty, Start = formula.Length });
            return tokens;
        }

        /// <summary>
        /// Writes function names, VERDADERO/FALSO and cell references in capitals, as Excel does on Enter
        /// (=promedio(b3:b5) → =PROMEDIO(B3:B5)). Texts in quotes and tab names stay as typed; a formula
        /// that cannot be read is returned unchanged.
        /// </summary>
        /// <param name="formula">Formula without the leading "=".</param>
        public static string Normalize(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return formula;
            List<FormulaToken> tokens;
            try
            {
                tokens = Tokenize(formula);
            }
            catch (FormulaException)
            {
                return formula;
            }
            char[] text = formula.ToCharArray();
            foreach (FormulaToken token in tokens)
            {
                int from = token.Kind == TokenKind.Reference && token.SheetText != null ? token.Start + token.SheetText.Length + 1 : token.Start;
                if (token.Kind != TokenKind.Function && token.Kind != TokenKind.Boolean && token.Kind != TokenKind.Reference) continue;
                for (int i = from; i < token.Start + token.Length; i++) text[i] = char.ToUpperInvariant(text[i]);
            }
            return new string(text);
        }

        private static string SheetName(string sheetText)
        {
            if (sheetText == null) return null;
            return sheetText.StartsWith("'") ? sheetText.Substring(1, sheetText.Length - 2).Replace("''", "'") : sheetText;
        }

        /// <summary>How a sheet name is written in a reference: quoted when it has spaces or symbols.</summary>
        public static string QuoteSheet(string sheet)
        {
            foreach (char c in sheet)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                    return "'" + sheet.Replace("'", "''") + "'";
            return char.IsDigit(sheet[0]) ? "'" + sheet + "'" : sheet;
        }
    }
}
