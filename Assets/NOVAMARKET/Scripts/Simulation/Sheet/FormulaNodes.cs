using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>Where a formula is evaluated: gives the value of any cell of the workbook.</summary>
    public interface ISheetContext
    {
        bool HasSheet(string sheet);

        /// <summary>Value of a cell; <paramref name="sheet"/> is never null here.</summary>
        CellValue ValueAt(string sheet, CellAddress cell);
    }

    /// <summary>Evaluation state: the workbook and the sheet the formula lives in.</summary>
    public sealed class EvaluationScope
    {
        public EvaluationScope(ISheetContext context, string sheet)
        {
            Context = context;
            Sheet = sheet;
        }

        public ISheetContext Context { get; }
        public string Sheet { get; }
    }

    /// <summary>A node of a parsed formula.</summary>
    public abstract class FormulaNode
    {
        public abstract CellValue Evaluate(EvaluationScope scope);

        /// <summary>
        /// Values the node gives to a function like SUMA: ranges and references give every cell
        /// (flagged as coming from a reference, so text and empty cells are skipped), the rest one value.
        /// </summary>
        public virtual IEnumerable<(CellValue Value, bool FromReference)> Values(EvaluationScope scope)
        {
            yield return (Evaluate(scope), false);
        }
    }

    public sealed class ConstantNode : FormulaNode
    {
        public ConstantNode(CellValue value) => Value = value;
        public CellValue Value { get; }
        public override CellValue Evaluate(EvaluationScope scope) => Value;
    }

    /// <summary>A reference to one cell (B3, $C$3, Datos!H4).</summary>
    public sealed class ReferenceNode : FormulaNode
    {
        public ReferenceNode(string sheet, CellAddress cell)
        {
            Sheet = sheet;
            Cell = cell;
        }

        public string Sheet { get; }
        public CellAddress Cell { get; }

        public override CellValue Evaluate(EvaluationScope scope)
        {
            string sheet = Sheet ?? scope.Sheet;
            return scope.Context.HasSheet(sheet) ? scope.Context.ValueAt(sheet, Cell) : CellValue.Fail(SheetError.Reference);
        }

        public override IEnumerable<(CellValue, bool)> Values(EvaluationScope scope)
        {
            yield return (Evaluate(scope), true);
        }
    }

    /// <summary>A rectangle of cells (B2:B9, Datos!C3:AT3).</summary>
    public sealed class RangeNode : FormulaNode
    {
        public RangeNode(string sheet, CellRange range)
        {
            Sheet = sheet;
            Range = range;
        }

        public string Sheet { get; }
        public CellRange Range { get; }

        /// <summary>A range used as a single value (=B2:B9 alone) is an error, as in Excel without spill.</summary>
        public override CellValue Evaluate(EvaluationScope scope) =>
            Range.Count == 1 ? new ReferenceNode(Sheet, Range.Start).Evaluate(scope) : CellValue.Fail(SheetError.Value);

        public override IEnumerable<(CellValue, bool)> Values(EvaluationScope scope)
        {
            string sheet = Sheet ?? scope.Sheet;
            if (!scope.Context.HasSheet(sheet))
            {
                yield return (CellValue.Fail(SheetError.Reference), true);
                yield break;
            }
            foreach (CellAddress cell in Range.Cells()) yield return (scope.Context.ValueAt(sheet, cell), true);
        }
    }

    public sealed class UnaryNode : FormulaNode
    {
        public UnaryNode(string op, FormulaNode operand)
        {
            Operator = op;
            Operand = operand;
        }

        public string Operator { get; }
        public FormulaNode Operand { get; }

        public override CellValue Evaluate(EvaluationScope scope)
        {
            CellValue value = Operand.Evaluate(scope);
            if (!FormulaMath.TryNumber(value, out double number, out CellValue error)) return error;
            return Operator switch
            {
                "-" => CellValue.Of(-number),
                "%" => CellValue.Of(number / 100),
                _ => CellValue.Of(number)
            };
        }
    }

    public sealed class BinaryNode : FormulaNode
    {
        public BinaryNode(string op, FormulaNode left, FormulaNode right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public string Operator { get; }
        public FormulaNode Left { get; }
        public FormulaNode Right { get; }

        public override CellValue Evaluate(EvaluationScope scope)
        {
            CellValue a = Left.Evaluate(scope), b = Right.Evaluate(scope);
            if (a.IsError) return a;
            if (b.IsError) return b;
            switch (Operator)
            {
                case "&":
                    return CellValue.Of(FormulaMath.AsText(a) + FormulaMath.AsText(b));
                case "=":
                case "<>":
                case "<":
                case ">":
                case "<=":
                case ">=":
                    return CellValue.Of(FormulaMath.Compare(Operator, a, b));
            }
            if (!FormulaMath.TryNumber(a, out double x, out CellValue errorA)) return errorA;
            if (!FormulaMath.TryNumber(b, out double y, out CellValue errorB)) return errorB;
            return Operator switch
            {
                "+" => CellValue.Of(x + y),
                "-" => CellValue.Of(x - y),
                "*" => CellValue.Of(x * y),
                "/" => y == 0 ? CellValue.Fail(SheetError.DivisionByZero) : CellValue.Of(x / y),
                "^" => CellValue.Of(Math.Pow(x, y)),
                _ => CellValue.Fail(SheetError.Value)
            };
        }
    }

    public sealed class FunctionNode : FormulaNode
    {
        public FunctionNode(string name, IReadOnlyList<FormulaNode> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>Upper-case function name (SUMA, DESVEST.P).</summary>
        public string Name { get; }

        public IReadOnlyList<FormulaNode> Arguments { get; }

        public override CellValue Evaluate(EvaluationScope scope) =>
            SheetFunctions.TryGet(Name, out SheetFunction function)
                ? function.Evaluate(scope, Arguments)
                : CellValue.Fail(SheetError.Name);
    }

    /// <summary>Conversions shared by operators and functions.</summary>
    public static class FormulaMath
    {
        /// <summary>A value as a number: empty → 0, booleans → 1/0, numeric text → number, other text → #¡VALOR!.</summary>
        public static bool TryNumber(CellValue value, out double number, out CellValue error)
        {
            error = default;
            number = 0;
            switch (value.Kind)
            {
                case CellValueKind.Empty:
                    return true;
                case CellValueKind.Number:
                case CellValueKind.Boolean:
                    number = value.Number;
                    return true;
                case CellValueKind.Text:
                    if (SheetNumber.TryParse(value.Text, out number)) return true;
                    error = CellValue.Fail(SheetError.Value);
                    return false;
                default:
                    error = value;
                    return false;
            }
        }

        public static string AsText(CellValue value) => value.ToString();

        /// <summary>Excel comparison: numbers before text before booleans; text ignores case.</summary>
        public static bool Compare(string op, CellValue a, CellValue b)
        {
            if (a.IsEmpty && b.IsText) a = CellValue.Of(string.Empty);
            if (b.IsEmpty && a.IsText) b = CellValue.Of(string.Empty);
            int order = Rank(a).CompareTo(Rank(b));
            if (order == 0)
                order = a.IsText
                    ? string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase)
                    : a.Number.CompareTo(b.Number);
            return op switch
            {
                "=" => order == 0,
                "<>" => order != 0,
                "<" => order < 0,
                ">" => order > 0,
                "<=" => order <= 0,
                _ => order >= 0
            };
        }

        // Empty cells compare as 0 with numbers (and as "" with text, above).
        private static int Rank(CellValue value) => value.Kind switch
        {
            CellValueKind.Text => 1,
            CellValueKind.Boolean => 2,
            _ => 0
        };
    }
}
