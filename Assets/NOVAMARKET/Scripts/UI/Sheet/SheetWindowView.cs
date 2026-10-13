using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Sheet;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Sheet
{
    /// <summary>
    /// The planner sheet window: title bar, number formats, formula bar (name box, fx and the input of
    /// the active cell), the grid, a tab per downloaded query and a status bar with the error
    /// explanation or the sum, average and count of the selection, as in a spreadsheet.
    /// </summary>
    public sealed class SheetWindowView
    {
        private readonly Label fileName;
        private readonly Label nameBox;
        private readonly TextField formulaField;
        private readonly SheetGridView grid;
        private readonly VisualElement tabs;
        private readonly Label emptyMessage;
        private readonly VisualElement statusBar;
        private readonly Label statusText;
        private readonly Label statusStats;
        private readonly List<string> tabNames = new List<string>();
        private Workbook book;
        private bool syncingBar;

        public SheetWindowView(VisualElement windowLayer, Action onClose)
        {
            Root = HudElements.Box("panel", "window", "hidden");
            VisualElement screen = HudElements.Box("sheet-screen");

            VisualElement title = HudElements.Box("sheet-titlebar");
            title.Add(new HudIcon { Kind = HudIconKind.Sheet });
            title.Add(HudElements.Text("Hoja del planeador", "sheet-title"));
            fileName = HudElements.Text(string.Empty, "sheet-file");
            title.Add(fileName);
            title.Add(HudElements.Box("sheet-titlebar-spacer"));
            title.Add(new Button(onClose) { text = "Cerrar" }.WithClass("sheet-close"));
            screen.Add(title);

            VisualElement toolbar = HudElements.Box("sheet-toolbar");
            toolbar.Add(Tool("General", () => ApplyFormat(_ => CellFormat.General)));
            toolbar.Add(Tool("1.234", () => ApplyFormat(f => CellFormat.Number(f.Kind == CellFormatKind.Number ? f.Decimals : 0))));
            toolbar.Add(Tool("%", () => ApplyFormat(f => CellFormat.Percent(f.Kind == CellFormatKind.Percent ? f.Decimals : 1))));
            toolbar.Add(HudElements.Box("sheet-tool-separator"));
            toolbar.Add(Tool("+ decimal", () => ApplyFormat(f => Decimals(f, +1))));
            toolbar.Add(Tool("− decimal", () => ApplyFormat(f => Decimals(f, -1))));
            toolbar.Add(HudElements.Text("Funciones: SUMA · PROMEDIO · ABS · SI · DESVEST.P   ·   separa con ; y usa coma decimal",
                "sheet-functions"));
            screen.Add(toolbar);

            VisualElement bar = HudElements.Box("formula-bar");
            nameBox = HudElements.Text("A1", "name-box");
            bar.Add(nameBox);
            bar.Add(HudElements.Text("fx", "fx-label"));
            formulaField = new TextField().WithClass("formula-field");
            formulaField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (!grid.IsEditing && grid.Sheet != null) grid.BeginEdit(grid.Sheet.GetInput(grid.Active), focusEditor: false);
            });
            formulaField.RegisterValueChangedCallback(e =>
            {
                if (!syncingBar) grid.SetEditText(e.newValue);
            });
            formulaField.RegisterCallback<KeyDownEvent>(OnFormulaKeyDown, TrickleDown.TrickleDown);
            bar.Add(formulaField);
            screen.Add(bar);

            grid = new SheetGridView();
            grid.StateChanged += UpdateBars;
            grid.SheetSwitched += _ => RebuildTabs();
            emptyMessage = HudElements.Text("La hoja está vacía. Abre NOVA ERP, haz la consulta y pulsa «Descargar a hoja»: " +
                                            "cada descarga llega como una pestaña nueva.", "sheet-empty");
            grid.Add(emptyMessage);
            screen.Add(grid);

            tabs = HudElements.Box("sheet-tabs");
            screen.Add(tabs);

            statusBar = HudElements.Box("sheet-status");
            statusText = HudElements.Text("Listo", "sheet-status-text");
            statusStats = HudElements.Text(string.Empty, "sheet-status-stats");
            statusBar.Add(statusText);
            statusBar.Add(statusStats);
            screen.Add(statusBar);

            Root.Add(screen);
            windowLayer.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsVisible => !Root.ClassListContains("hidden");
        public SheetGridView Grid => grid;

        public void Bind(Workbook workbook, string file)
        {
            if (book != null) book.Changed -= OnBookChanged;
            book = workbook;
            book.Changed += OnBookChanged;
            fileName.text = file;
            tabNames.Clear();
            grid.SetSheet(book.Sheets.FirstOrDefault());
            RebuildTabs();
            UpdateBars();
        }

        public void Show(string sheetName = null)
        {
            HudElements.SetVisible(Root, true);
            Root.BringToFront();
            if (sheetName != null && book?.Sheet(sheetName) is Worksheet sheet) SwitchTo(sheet);
            grid.schedule.Execute(() =>
            {
                grid.Refresh();
                grid.Focus();
            });
        }

        public void Hide()
        {
            grid.CommitEdit(move: false);
            HudElements.SetVisible(Root, false);
        }

        private static Button Tool(string text, Action action) => new Button(action) { text = text }.WithClass("sheet-tool");

        private static CellFormat Decimals(CellFormat format, int delta)
        {
            CellFormatKind kind = format.Kind == CellFormatKind.General ? CellFormatKind.Number : format.Kind;
            return new CellFormat(kind, Math.Max(0, format.Decimals + delta));
        }

        private void ApplyFormat(Func<CellFormat, CellFormat> change)
        {
            if (grid.Sheet == null) return;
            grid.Sheet.SetFormat(grid.Selection, change(grid.Sheet.GetFormat(grid.Active)));
            grid.Focus();
        }

        // ------------------------------------------------------------------ book and tabs

        private void OnBookChanged()
        {
            if (grid.Sheet == null || !book.Sheets.Contains(grid.Sheet)) grid.SetSheet(book.Sheets.FirstOrDefault());
            if (!tabNames.SequenceEqual(book.Sheets.Select(s => s.Name))) RebuildTabs();
            grid.FitNewColumns();
            grid.Refresh();
            UpdateBars();
        }

        private void RebuildTabs()
        {
            tabs.Clear();
            tabNames.Clear();
            if (book == null) return;
            foreach (Worksheet sheet in book.Sheets)
            {
                tabNames.Add(sheet.Name);
                Button tab = new Button(() => SwitchTo(sheet)) { text = sheet.Name }.WithClass("sheet-tab");
                tab.EnableInClassList("sheet-tab--active", sheet == grid.Sheet);
                tabs.Add(tab);
            }
            emptyMessage.style.display = book.Sheets.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SwitchTo(Worksheet sheet)
        {
            // While picking references the edit stays open, so a formula can point at another tab.
            if (grid.IsEditing && !grid.AcceptsPick) grid.CommitEdit(move: false);
            grid.SetSheet(sheet);
            RebuildTabs();
        }

        // ------------------------------------------------------------------ formula and status bars

        private void OnFormulaKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    grid.CommitEdit();
                    break;
                case KeyCode.Tab:
                    grid.CommitEdit(true, 1, 0);
                    break;
                case KeyCode.Escape:
                    grid.CancelEdit();
                    break;
                default:
                    return;
            }
            evt.StopPropagation();
            formulaField.focusController?.IgnoreEvent(evt);
        }

        private void UpdateBars()
        {
            Worksheet sheet = grid.Sheet;
            CellRange selection = grid.Selection;
            nameBox.text = sheet == null ? string.Empty : selection.ToString();

            syncingBar = true;
            string input = grid.IsEditing ? grid.EditText : sheet?.GetInput(grid.Active) ?? string.Empty;
            if (formulaField.value != input) formulaField.SetValueWithoutNotify(input);
            syncingBar = false;

            statusBar.EnableInClassList("sheet-status--error", false);
            statusStats.text = string.Empty;
            if (sheet == null)
            {
                statusText.text = "Sin pestañas todavía.";
                return;
            }
            if (grid.IsEditing)
            {
                statusText.text = grid.AcceptsPick
                    ? "Señalar: haz clic en una celda o arrastra un rango (también en otra pestaña) para escribir su referencia."
                    : "Editando: Enter acepta · Tab acepta y va a la derecha · Esc cancela.";
                return;
            }

            string error = sheet.FormulaError(grid.Active);
            CellValue value = sheet.Value(grid.Active);
            if (error != null)
            {
                statusBar.EnableInClassList("sheet-status--error", true);
                statusText.text = $"Fórmula de {grid.Active}: {error}";
            }
            else if (value.IsError)
            {
                statusBar.EnableInClassList("sheet-status--error", true);
                statusText.text = $"{grid.Active} muestra {value}: {ErrorHelp(value.Error)}";
            }
            else
            {
                statusText.text = "Listo";
            }
            statusStats.text = Stats(sheet, selection);
        }

        /// <summary>Sum, average and count of the selection (only with two or more cells).</summary>
        private static string Stats(Worksheet sheet, CellRange selection)
        {
            if (selection.Count < 2 || selection.Count > 50_000) return string.Empty;
            int count = 0, numbers = 0;
            double sum = 0;
            int lastRow = Math.Min(selection.End.Row, Math.Max(selection.Start.Row, sheet.UsedRows - 1));
            int lastColumn = Math.Min(selection.End.Column, Math.Max(selection.Start.Column, sheet.UsedColumns - 1));
            for (int row = selection.Start.Row; row <= lastRow; row++)
                for (int column = selection.Start.Column; column <= lastColumn; column++)
                {
                    CellValue value = sheet.Value(new CellAddress(column, row));
                    if (value.IsEmpty) continue;
                    count++;
                    if (!value.IsNumber) continue;
                    numbers++;
                    sum += value.Number;
                }
            if (count == 0) return string.Empty;
            return numbers == 0
                ? $"Recuento: {count}"
                : $"Promedio: {SheetNumber.Format(sum / numbers, CellFormat.Number(2))}    Recuento: {count}    " +
                  $"Suma: {SheetNumber.Format(sum, CellFormat.Number(sum == Math.Floor(sum) ? 0 : 2))}";
        }

        private static string ErrorHelp(SheetError error) => error switch
        {
            SheetError.DivisionByZero => "una división entre cero (o un promedio sin números).",
            SheetError.Value => "un texto donde se esperaba un número.",
            SheetError.Reference => "una referencia a una celda u hoja que no existe.",
            SheetError.Name => "una función que no existe. Revisa el nombre (SUMA, PROMEDIO, ABS, SI, DESVEST.P).",
            SheetError.Circular => "la fórmula depende de sí misma (referencia circular).",
            SheetError.Number => "un resultado que no es un número válido.",
            _ => "la fórmula no se puede leer."
        };
    }
}
