using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>A sheet function with its Spanish name, argument count and help text.</summary>
    public sealed class SheetFunction
    {
        private readonly Func<EvaluationScope, IReadOnlyList<FormulaNode>, CellValue> evaluate;

        public SheetFunction(string name, int minArguments, int maxArguments, string syntax, string help,
            Func<EvaluationScope, IReadOnlyList<FormulaNode>, CellValue> evaluate)
        {
            Name = name;
            MinArguments = minArguments;
            MaxArguments = maxArguments;
            Syntax = syntax;
            Help = help;
            this.evaluate = evaluate;
        }

        public string Name { get; }
        public int MinArguments { get; }
        public int MaxArguments { get; }

        /// <summary>"SUMA(número1; [número2]; …)".</summary>
        public string Syntax { get; }

        public string Help { get; }

        public CellValue Evaluate(EvaluationScope scope, IReadOnlyList<FormulaNode> arguments) => evaluate(scope, arguments);
    }

    /// <summary>
    /// The functions of the planner sheet, with the names and rules of Excel in Spanish. Functions
    /// over ranges skip text and empty cells; text typed directly as an argument must be a number.
    /// </summary>
    public static class SheetFunctions
    {
        private const int Many = 255;

        private static readonly Dictionary<string, SheetFunction> functions = new Dictionary<string, SheetFunction>
        {
            ["SUMA"] = new SheetFunction("SUMA", 1, Many, "SUMA(número1; [número2]; …)",
                "Suma los números de las celdas o rangos.", (scope, args) =>
                    Numbers(scope, args, out List<double> values, out CellValue error) ? CellValue.Of(Sum(values)) : error),
            ["PROMEDIO"] = new SheetFunction("PROMEDIO", 1, Many, "PROMEDIO(número1; [número2]; …)",
                "Promedio de los números (suma ÷ cuántos hay).", (scope, args) =>
                {
                    if (!Numbers(scope, args, out List<double> values, out CellValue error)) return error;
                    return values.Count == 0 ? CellValue.Fail(SheetError.DivisionByZero) : CellValue.Of(Sum(values) / values.Count);
                }),
            ["ABS"] = new SheetFunction("ABS", 1, 1, "ABS(número)",
                "Valor absoluto: el número sin signo.", (scope, args) =>
                    FormulaMath.TryNumber(args[0].Evaluate(scope), out double x, out CellValue error) ? CellValue.Of(Math.Abs(x)) : error),
            ["SI"] = new SheetFunction("SI", 2, 3, "SI(prueba_lógica; [valor_si_verdadero]; [valor_si_falso])",
                "Devuelve un valor si la prueba es verdadera y otro si es falsa.", (scope, args) =>
                {
                    CellValue test = args[0].Evaluate(scope);
                    if (!FormulaMath.TryNumber(test, out double condition, out CellValue error)) return error;
                    if (condition != 0) return args[1].Evaluate(scope);
                    return args.Count > 2 ? args[2].Evaluate(scope) : CellValue.Of(false);
                }),
            ["DESVEST.P"] = new SheetFunction("DESVEST.P", 1, Many, "DESVEST.P(número1; [número2]; …)",
                "Desviación estándar de la población: raíz del promedio de los cuadrados de las diferencias con el promedio.",
                (scope, args) =>
                {
                    if (!Numbers(scope, args, out List<double> values, out CellValue error)) return error;
                    if (values.Count == 0) return CellValue.Fail(SheetError.DivisionByZero);
                    double mean = Sum(values) / values.Count, squares = 0;
                    foreach (double v in values) squares += (v - mean) * (v - mean);
                    return CellValue.Of(Math.Sqrt(squares / values.Count));
                })
        };

        public static IEnumerable<SheetFunction> All => functions.Values;

        public static bool TryGet(string name, out SheetFunction function) =>
            functions.TryGetValue(name.ToUpperInvariant(), out function);

        /// <summary>Collects the numbers of the arguments; false with the first error found.</summary>
        private static bool Numbers(EvaluationScope scope, IReadOnlyList<FormulaNode> arguments, out List<double> values, out CellValue error)
        {
            values = new List<double>();
            error = default;
            foreach (FormulaNode argument in arguments)
            {
                foreach ((CellValue value, bool fromReference) in argument.Values(scope))
                {
                    if (value.IsError)
                    {
                        error = value;
                        return false;
                    }
                    if (fromReference)
                    {
                        if (value.IsNumber) values.Add(value.Number);
                        continue;
                    }
                    if (value.IsEmpty) continue;
                    if (!FormulaMath.TryNumber(value, out double number, out error)) return false;
                    values.Add(number);
                }
            }
            return true;
        }

        private static double Sum(List<double> values)
        {
            double sum = 0;
            foreach (double v in values) sum += v;
            return sum;
        }
    }
}
