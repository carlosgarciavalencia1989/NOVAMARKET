using System.Collections.Generic;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// Turns a formula (without "=") into a tree, with Excel's precedence from low to high:
    /// comparison (= &lt;&gt; &lt; &gt; &lt;= &gt;=), concatenation (&amp;), + −, * /, ^, sign, percent (%),
    /// and ranges (:). Errors are <see cref="FormulaException"/> with a Spanish message.
    /// </summary>
    public sealed class FormulaParser
    {
        private readonly List<FormulaToken> tokens;
        private int position;

        private FormulaParser(List<FormulaToken> tokens) => this.tokens = tokens;

        public static FormulaNode Parse(string formula)
        {
            var parser = new FormulaParser(FormulaLexer.Tokenize(formula));
            if (parser.Current.Kind == TokenKind.End) throw new FormulaException("La fórmula está vacía.", 0);
            FormulaNode node = parser.Comparison();
            if (parser.Current.Kind != TokenKind.End)
            {
                string hint = parser.Current.Kind == TokenKind.CloseParen ? " Sobra un paréntesis de cierre." : string.Empty;
                throw new FormulaException($"No se esperaba '{parser.Current.Text}'.{hint}", parser.Current.Start);
            }
            return node;
        }

        private FormulaToken Current => tokens[position];

        private FormulaToken Next() => tokens[position++];

        private bool IsOperator(params string[] ops)
        {
            if (Current.Kind != TokenKind.Operator) return false;
            foreach (string op in ops) if (Current.Text == op) return true;
            return false;
        }

        private FormulaNode Comparison()
        {
            FormulaNode left = Concatenation();
            while (IsOperator("=", "<>", "<", ">", "<=", ">="))
            {
                string op = Next().Text;
                left = new BinaryNode(op, left, Concatenation());
            }
            return left;
        }

        private FormulaNode Concatenation()
        {
            FormulaNode left = Additive();
            while (IsOperator("&"))
            {
                Next();
                left = new BinaryNode("&", left, Additive());
            }
            return left;
        }

        private FormulaNode Additive()
        {
            FormulaNode left = Multiplicative();
            while (IsOperator("+", "-"))
            {
                string op = Next().Text;
                left = new BinaryNode(op, left, Multiplicative());
            }
            return left;
        }

        private FormulaNode Multiplicative()
        {
            FormulaNode left = Power();
            while (IsOperator("*", "/"))
            {
                string op = Next().Text;
                left = new BinaryNode(op, left, Power());
            }
            return left;
        }

        private FormulaNode Power()
        {
            FormulaNode left = Unary();
            while (IsOperator("^"))
            {
                Next();
                left = new BinaryNode("^", left, Unary());
            }
            return left;
        }

        // As in Excel the sign binds tighter than ^: =-2^2 is 4.
        private FormulaNode Unary()
        {
            if (IsOperator("-", "+"))
            {
                string op = Next().Text;
                return new UnaryNode(op, Unary());
            }
            return Percent();
        }

        private FormulaNode Percent()
        {
            FormulaNode node = Primary();
            while (Current.Kind == TokenKind.Percent)
            {
                Next();
                node = new UnaryNode("%", node);
            }
            return node;
        }

        private FormulaNode Primary()
        {
            FormulaToken token = Next();
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return new ConstantNode(CellValue.Of(token.Number));
                case TokenKind.Text:
                    return new ConstantNode(CellValue.Of(token.StringValue));
                case TokenKind.Boolean:
                    return new ConstantNode(CellValue.Of(token.Number != 0));
                case TokenKind.ErrorLiteral:
                    return new ConstantNode(CellValue.Fail(ErrorOf(token.Text)));
                case TokenKind.Reference:
                    return Reference(token);
                case TokenKind.Function:
                    return Function(token);
                case TokenKind.OpenParen:
                    FormulaNode inner = Comparison();
                    Expect(TokenKind.CloseParen, "Falta cerrar un paréntesis.");
                    return inner;
                case TokenKind.Name:
                    throw new FormulaException($"'{token.Text}' no es una celda ni una función. ¿Faltan los paréntesis?", token.Start);
                case TokenKind.End:
                    throw new FormulaException("La fórmula está incompleta.", token.Start);
                case TokenKind.Semicolon:
                    throw new FormulaException("El punto y coma (;) solo separa argumentos de una función.", token.Start);
                default:
                    throw new FormulaException($"No se esperaba '{token.Text}'.", token.Start);
            }
        }

        private FormulaNode Reference(FormulaToken first)
        {
            var start = new CellAddress(first.Column, first.Row);
            if (Current.Kind != TokenKind.Colon) return new ReferenceNode(first.Sheet, start);
            Next();
            FormulaToken second = Next();
            if (second.Kind != TokenKind.Reference)
                throw new FormulaException("Después de ':' va la última celda del rango.", second.Start);
            if (second.Sheet != null && second.Sheet != first.Sheet)
                throw new FormulaException("Un rango no puede unir dos hojas.", second.Start);
            return new RangeNode(first.Sheet, new CellRange(start, new CellAddress(second.Column, second.Row)));
        }

        private FormulaNode Function(FormulaToken name)
        {
            string upper = name.Text.ToUpperInvariant();
            Expect(TokenKind.OpenParen, "Falta el paréntesis de la función.");
            var arguments = new List<FormulaNode>();
            if (Current.Kind != TokenKind.CloseParen)
            {
                while (true)
                {
                    // An empty argument (=SI(A1>3;;1)) counts as 0, as in Excel.
                    arguments.Add(Current.Kind == TokenKind.Semicolon || Current.Kind == TokenKind.CloseParen
                        ? new ConstantNode(CellValue.Of(0))
                        : Comparison());
                    if (Current.Kind != TokenKind.Semicolon) break;
                    Next();
                }
            }
            if (Current.Kind != TokenKind.CloseParen)
            {
                string hint = Current.Text == "," ? " Separa los argumentos con punto y coma (;)." : string.Empty;
                throw new FormulaException($"Falta cerrar el paréntesis de {upper}.{hint}", Current.Start);
            }
            Next();

            if (SheetFunctions.TryGet(upper, out SheetFunction function)
                && (arguments.Count < function.MinArguments || arguments.Count > function.MaxArguments))
            {
                string count = function.MinArguments == function.MaxArguments
                    ? $"{function.MinArguments} argumento{(function.MinArguments == 1 ? "" : "s")}"
                    : $"de {function.MinArguments} a {(function.MaxArguments > 3 ? "muchos" : function.MaxArguments.ToString())} argumentos";
                throw new FormulaException($"{upper} lleva {count}: {function.Syntax}", name.Start);
            }
            return new FunctionNode(upper, arguments);
        }

        private void Expect(TokenKind kind, string message)
        {
            if (Current.Kind != kind) throw new FormulaException(message, Current.Start);
            Next();
        }

        private static SheetError ErrorOf(string text) => text switch
        {
            "#¡DIV/0!" => SheetError.DivisionByZero,
            "#¡VALOR!" => SheetError.Value,
            "#¿NOMBRE?" => SheetError.Name,
            "#N/A" => SheetError.NotAvailable,
            "#¡NUM!" => SheetError.Number,
            _ => SheetError.Reference
        };
    }
}
