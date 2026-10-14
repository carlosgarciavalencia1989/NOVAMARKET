using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>
    /// The planner sheet: an Excel-like book of tabs (worksheets). Values are computed on demand and
    /// cached until the next edit, so a change anywhere recalculates everything that depends on it.
    /// </summary>
    public sealed class Workbook : ISheetContext
    {
        public const int MaxSheetNameLength = 31;
        private const string ForbiddenNameCharacters = ":\\/?*[]";

        private readonly List<Worksheet> sheets = new List<Worksheet>();
        private int batchDepth;
        private bool changedInBatch;

        /// <summary>Raised after any edit (once per batch).</summary>
        public event Action Changed;

        public Workbook() => History = new SheetHistory(this);

        public IReadOnlyList<Worksheet> Sheets => sheets;

        /// <summary>Undo and redo of the player's edits (Ctrl+Z / Ctrl+Y).</summary>
        public SheetHistory History { get; }

        /// <summary>Increases with every edit; cached values of older versions are recomputed.</summary>
        internal int Version { get; private set; }

        public Worksheet Sheet(string name) =>
            name == null ? null : sheets.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        public bool HasSheet(string sheet) => Sheet(sheet) != null;

        public CellValue ValueAt(string sheet, CellAddress cell) =>
            Sheet(sheet)?.Value(cell) ?? CellValue.Fail(SheetError.Reference);

        /// <summary>Adds a tab; the name must be valid and not used yet.</summary>
        public Worksheet AddSheet(string name)
        {
            string problem = NameProblem(name);
            if (problem != null) throw new ArgumentException(problem, nameof(name));
            var sheet = new Worksheet(this, name);
            sheets.Add(sheet);
            Touch();
            return sheet;
        }

        /// <summary>"Facturación", or "Facturación (2)" when it already exists.</summary>
        public string UniqueName(string baseName)
        {
            string name = baseName.Length > MaxSheetNameLength ? baseName.Substring(0, MaxSheetNameLength) : baseName;
            for (int n = 2; HasSheet(name); n++)
            {
                string suffix = $" ({n})";
                name = (baseName.Length + suffix.Length > MaxSheetNameLength
                    ? baseName.Substring(0, MaxSheetNameLength - suffix.Length)
                    : baseName) + suffix;
            }
            return name;
        }

        public bool RemoveSheet(string name)
        {
            Worksheet sheet = Sheet(name);
            if (sheet == null) return false;
            sheets.Remove(sheet);
            Touch();
            return true;
        }

        /// <summary>Why a tab name is not allowed (Spanish), or null when it is fine.</summary>
        public string NameProblem(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "La hoja necesita un nombre.";
            if (name.Length > MaxSheetNameLength) return $"El nombre de la hoja tiene más de {MaxSheetNameLength} caracteres.";
            if (name.IndexOfAny(ForbiddenNameCharacters.ToCharArray()) >= 0)
                return "El nombre de la hoja no puede tener : \\ / ? * [ ]";
            if (name.StartsWith("'") || name.EndsWith("'")) return "El nombre de la hoja no puede empezar ni terminar con comilla.";
            return HasSheet(name) ? $"Ya hay una hoja llamada {name}." : null;
        }

        /// <summary>Runs several edits and raises <see cref="Changed"/> once at the end.</summary>
        public void Batch(Action edits)
        {
            batchDepth++;
            try
            {
                edits();
            }
            finally
            {
                batchDepth--;
                if (batchDepth == 0 && changedInBatch)
                {
                    changedInBatch = false;
                    Changed?.Invoke();
                }
            }
        }

        internal void Touch()
        {
            Version++;
            if (batchDepth > 0) changedInBatch = true;
            else Changed?.Invoke();
        }
    }
}
