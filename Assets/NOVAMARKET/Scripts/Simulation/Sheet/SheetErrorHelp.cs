using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// Explains in plain Spanish why a cell shows an error, naming the cells involved: which cells read
    /// each other in a circular reference, or which cell an error comes from.
    /// </summary>
    public static class SheetErrorHelp
    {
        private const int MaxDepth = 60;

        /// <summary>
        /// The explanation of the error of <paramref name="cell"/>, or null when it has none.
        /// <paramref name="originalDataSheet"/> is the tab with the raw data to suggest, if any.
        /// </summary>
        public static string Explain(Worksheet sheet, CellAddress cell, string originalDataSheet = null)
        {
            string parseError = sheet.FormulaError(cell);
            if (parseError != null) return $"No se puede leer la fórmula de {cell}: {parseError}";
            CellValue value = sheet.Value(cell);
            if (!value.IsError) return null;

            if (value.Error == SheetError.Circular)
            {
                var start = (sheet.Name, cell);
                List<(string Sheet, CellAddress Cell)> loop = FindLoop(sheet.Book, start, start);
                string advice = originalDataSheet != null && sheet.Book.HasSheet(originalDataSheet)
                    ? $" Escribe en una de ellas el dato original de la pestaña «{originalDataSheet}» o una fórmula que no vuelva a la otra."
                    : " Cambia una de las fórmulas para que lea el dato original y no la otra celda.";
                if (loop != null) return Describe(loop, sheet.Name) + advice;

                // The cell is outside the loop but reads a cell that is in one.
                foreach (var precedent in ErrorPrecedents(sheet, cell, SheetError.Circular))
                {
                    List<(string, CellAddress)> inner = FindLoop(sheet.Book, precedent, precedent);
                    if (inner == null) continue;
                    return $"{cell} muestra {value} porque usa {Name(precedent, sheet.Name)}, que está en una referencia circular: " +
                           Describe(inner, sheet.Name) + advice;
                }
                return $"{cell} depende de sí misma a través de otras celdas, así que no puede calcularse.{advice}";
            }

            // An error that comes from another cell: point at it.
            var origin = ErrorPrecedents(sheet, cell, value.Error).FirstOrDefault();
            if (origin.Sheet != null)
                return $"{cell} muestra {value} porque usa {Name(origin, sheet.Name)}, que ya tiene ese error. " +
                       $"Corrige primero {Name(origin, sheet.Name)}.";

            return value.Error switch
            {
                SheetError.DivisionByZero => $"{cell} divide entre cero: el divisor está vacío o vale 0, o el PROMEDIO no encontró números.",
                SheetError.Value => $"{cell} intenta hacer cuentas con un texto. Revisa que las celdas que usa tengan números.",
                SheetError.Reference => $"{cell} usa una celda o una pestaña que no existe. Revisa el nombre de la pestaña.",
                SheetError.Name => $"{cell} usa una función que la hoja no conoce. Las disponibles son SUMA, PROMEDIO, ABS, SI y DESVEST.P.",
                SheetError.Number => $"{cell} da un resultado que no es un número válido.",
                _ => $"No se puede leer la fórmula de {cell}."
            };
        }

        /// <summary>The cells (with their tab) that read each other, starting at <paramref name="start"/>, or null.</summary>
        public static List<(string Sheet, CellAddress Cell)> FindLoop(Workbook book, (string Sheet, CellAddress Cell) from,
            (string Sheet, CellAddress Cell) start)
        {
            var path = new List<(string, CellAddress)>();
            var visited = new HashSet<(string, CellAddress)>();
            return Search(book, from, start, path, visited) ? path : null;
        }

        private static bool Search(Workbook book, (string Sheet, CellAddress Cell) node, (string Sheet, CellAddress Cell) start,
            List<(string, CellAddress)> path, HashSet<(string, CellAddress)> visited)
        {
            if (path.Count > MaxDepth || !visited.Add(node)) return false;
            Worksheet sheet = book.Sheet(node.Sheet);
            if (sheet == null) return false;
            path.Add(node);
            foreach (var next in sheet.Precedents(node.Cell))
            {
                if (next.Sheet == start.Sheet && next.Cell == start.Cell) return true;
                if (Search(book, next, start, path, visited)) return true;
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static IEnumerable<(string Sheet, CellAddress Cell)> ErrorPrecedents(Worksheet sheet, CellAddress cell, SheetError error)
        {
            foreach (var precedent in sheet.Precedents(cell))
            {
                Worksheet other = sheet.Book.Sheet(precedent.Sheet);
                if (other == null) continue;
                CellValue value = other.Value(precedent.Cell);
                if (value.IsError && value.Error == error) yield return precedent;
            }
        }

        private static string Describe(List<(string Sheet, CellAddress Cell)> loop, string home)
        {
            var names = loop.Select(c => Name(c, home)).ToList();
            if (names.Count == 1)
                return $"{names[0]} se usa a sí misma: su fórmula lee {names[0]}, así que no puede calcularse.";
            if (names.Count == 2)
                return $"{names[0]} y {names[1]} se están leyendo entre sí: {names[0]} usa {names[1]} y {names[1]} usa {names[0]}, " +
                       "así que ninguna de las dos puede calcularse.";
            var steps = new List<string>();
            for (int i = 0; i < names.Count; i++) steps.Add($"{names[i]} usa {names[(i + 1) % names.Count]}");
            return $"{string.Join(", ", names.Take(names.Count - 1))} y {names[names.Count - 1]} forman un círculo: " +
                   $"{string.Join(", ", steps.Take(steps.Count - 1))} y {steps[steps.Count - 1]}, así que ninguna puede calcularse.";
        }

        private static string Name((string Sheet, CellAddress Cell) cell, string home) =>
            cell.Sheet == home ? cell.Cell.ToString() : $"{cell.Cell} de la pestaña «{cell.Sheet}»";
    }
}
