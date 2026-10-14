using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Sheet;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Sheet
{
    /// <summary>
    /// Spreadsheet grid of the planner sheet: column letters and row numbers, cells drawn only where
    /// visible (200 SKUs stay fast), selection with mouse and keyboard, in-cell editing, Excel-style
    /// reference picking while typing a formula (click a cell, even on another tab, to insert it) and
    /// the fill handle at the corner of the selection. All rules are <see cref="Worksheet"/>'s.
    /// </summary>
    public sealed class SheetGridView : VisualElement
    {
        public const float RowHeight = 28f;
        public const float HeaderHeight = 28f;
        public const float RowHeaderWidth = 58f;
        public const float DefaultColumnWidth = 96f;
        private const float ScrollerSize = 14f;
        private const float HandleSize = 9f;

        private readonly VisualElement corner;
        private readonly VisualElement columnStrip;
        private readonly VisualElement rowStrip;
        private readonly VisualElement cells;
        private readonly VisualElement rangeBox;
        private readonly VisualElement activeBox;
        private readonly VisualElement fillTargetBox;
        private readonly VisualElement fillHandle;
        private readonly VisualElement copyBox;
        private readonly Label tooltip;
        private readonly SheetClipboard clipboard = new SheetClipboard();
        private readonly TextField editor;
        private readonly Scroller vertical;
        private readonly Scroller horizontal;
        private readonly List<Label> cellPool = new List<Label>();
        private readonly List<Label> columnPool = new List<Label>();
        private readonly List<Label> rowPool = new List<Label>();
        private readonly Dictionary<Worksheet, Dictionary<int, float>> widths = new Dictionary<Worksheet, Dictionary<int, float>>();
        private readonly Dictionary<Worksheet, (CellAddress Anchor, CellAddress Cursor)> selections =
            new Dictionary<Worksheet, (CellAddress, CellAddress)>();

        private Worksheet sheet;
        private float scrollX;
        private float scrollY;
        private float[] columnLefts = new float[1];
        private int columnCount;
        private int rowCount;
        private bool updatingScrollers;

        private CellAddress anchor;
        private CellAddress cursor;
        private bool selecting;
        private bool filling;
        private CellRange? fillTarget;

        // Editing: the cell being edited can be on another tab while the player picks references.
        private Worksheet editSheet;
        private CellAddress editCell;
        private int pickStart = -1;
        private int pickEnd = -1;
        private int caret;
        private CellAddress pickAnchor;
        private bool picking;
        private bool syncingText;
        private Vector2 lastPointer;
        private IVisualElementScheduledItem autoScroll;
        private Worksheet copySheet;
        private CellRange? copyRange;
        private (int Column, int Row) hovered = (-1, -1);

        public SheetGridView()
        {
            AddToClassList("sheet-grid");
            focusable = true;
            tabIndex = 0;

            cells = HudElements.Box("sheet-cells");
            cells.pickingMode = PickingMode.Ignore;
            Add(cells);
            rangeBox = Overlay("sheet-range");
            activeBox = Overlay("sheet-active");
            fillTargetBox = Overlay("sheet-fill-target");
            fillHandle = Overlay("fill-handle");
            copyBox = Overlay("sheet-copy");
            copyBox.style.display = DisplayStyle.None;

            columnStrip = HudElements.Box("sheet-head-strip");
            rowStrip = HudElements.Box("sheet-head-strip");
            corner = HudElements.Box("sheet-corner");
            foreach (VisualElement element in new[] { columnStrip, rowStrip, corner })
            {
                element.pickingMode = PickingMode.Ignore;
                Add(element);
            }

            editor = new TextField().WithClass("cell-editor");
            editor.style.display = DisplayStyle.None;
            editor.RegisterCallback<KeyDownEvent>(OnEditorKeyDown, TrickleDown.TrickleDown);
            editor.RegisterValueChangedCallback(e => OnEditTextChanged(e.newValue, fromBar: false));
            // Focusing the editor after a click on a cell must not select the formula (the next key would
            // replace it): the cursor goes back where it was, as in Excel.
            editor.selectAllOnFocus = false;
            editor.selectAllOnMouseUp = false;
            editor.RegisterCallback<KeyUpEvent>(_ => NoteCaret(editor.cursorIndex));
            editor.RegisterCallback<PointerUpEvent>(_ => NoteCaret(editor.cursorIndex), TrickleDown.TrickleDown);
            cells.Add(editor);

            vertical = new Scroller(0, 1, value => OnScroll(scrollX, value), SliderDirection.Vertical).WithClass("sheet-scroller");
            horizontal = new Scroller(0, 1, value => OnScroll(value, scrollY), SliderDirection.Horizontal).WithClass("sheet-scroller");
            Add(vertical);
            Add(horizontal);

            tooltip = HudElements.Text(string.Empty, "sheet-tooltip");
            tooltip.pickingMode = PickingMode.Ignore;
            tooltip.style.display = DisplayStyle.None;
            Add(tooltip);

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<NavigationMoveEvent>(StopNavigation);
            RegisterCallback<NavigationSubmitEvent>(StopNavigation);
        }

        /// <summary>Raised when the selection, the edit text or the sheet changes (formula bar, status bar).</summary>
        public event Action StateChanged;

        /// <summary>Raised when the grid switches tab by itself (committing an edit started on another tab).</summary>
        public event Action<Worksheet> SheetSwitched;

        /// <summary>Cells drawn with the "marked" highlight (problems marked in level 1.2); null = none.</summary>
        public Func<Worksheet, CellAddress, bool> IsMarked { get; set; }

        /// <summary>Cells drawn with the "review" highlight (what Andrés flagged after a delivery); null = none.</summary>
        public Func<Worksheet, CellAddress, bool> IsFlagged { get; set; }

        /// <summary>The explanation shown when the pointer rests on a cell (errors, flagged cells); null = nothing.</summary>
        public Func<Worksheet, CellAddress, string> HoverNote { get; set; }

        /// <summary>Raised after copying or pasting with a short message for the status bar.</summary>
        public event Action<string> Notice;

        public Worksheet Sheet => sheet;
        public CellAddress Active => anchor;
        public CellRange Selection => new CellRange(anchor, cursor);
        public bool IsEditing => editSheet != null;
        public string EditText => editor.value;

        /// <summary>True while a formula is being typed and a click inserts a reference.</summary>
        public bool IsPicking => IsEditing && FormulaEditing.CanInsertReference(editor.value, caret);

        /// <summary>
        /// Where the cursor is in the formula being typed (in the cell or in the formula bar): a clicked
        /// reference is written there.
        /// </summary>
        public int Caret => caret;

        public void SetSheet(Worksheet worksheet)
        {
            HideTooltip();
            if (sheet != null) selections[sheet] = (anchor, cursor);
            sheet = worksheet;
            (anchor, cursor) = sheet != null && selections.TryGetValue(sheet, out var saved)
                ? saved
                : (new CellAddress(0, 0), new CellAddress(0, 0));
            scrollX = scrollY = 0;
            if (sheet != null && !widths.ContainsKey(sheet)) widths[sheet] = AutoWidths(sheet);
            Refresh();
            ScrollIntoView(anchor);
            StateChanged?.Invoke();
        }

        /// <summary>Selects a cell (or range) and scrolls to it.</summary>
        public void Select(CellAddress from, CellAddress to)
        {
            anchor = from;
            cursor = to;
            ScrollIntoView(to);
            ScrollIntoView(from);
            Refresh();
            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------------ geometry

        private float Width(int column) =>
            sheet != null && widths.TryGetValue(sheet, out var map) && map.TryGetValue(column, out float width) ? width : DefaultColumnWidth;

        private float ViewWidth => Mathf.Max(0, layout.width - RowHeaderWidth - ScrollerSize);
        private float ViewHeight => Mathf.Max(0, layout.height - HeaderHeight - ScrollerSize);

        private void Measure()
        {
            columnCount = Mathf.Min(CellAddress.MaxColumns, Mathf.Max(26, (sheet?.UsedColumns ?? 0) + 8));
            rowCount = Mathf.Min(CellAddress.MaxRows, Mathf.Max(60, (sheet?.UsedRows ?? 0) + 30));
            if (columnLefts.Length != columnCount + 1) columnLefts = new float[columnCount + 1];
            for (int c = 0; c < columnCount; c++) columnLefts[c + 1] = columnLefts[c] + Width(c);
        }

        private Rect CellRect(int column, int row) =>
            new Rect(columnLefts[Mathf.Min(column, columnCount)] - scrollX, row * RowHeight - scrollY,
                Width(column), RowHeight);

        private Rect RangeRect(CellRange range)
        {
            Rect a = CellRect(range.Start.Column, range.Start.Row), b = CellRect(range.End.Column, range.End.Row);
            return Rect.MinMaxRect(a.xMin, a.yMin, b.xMax, b.yMax);
        }

        /// <summary>Cell under a point in grid coordinates (headers return -1 on their axis).</summary>
        private (int Column, int Row) Hit(Vector2 local)
        {
            int column = -1, row = -1;
            if (local.x >= RowHeaderWidth)
            {
                float x = local.x - RowHeaderWidth + scrollX;
                column = columnCount - 1;
                for (int c = 0; c < columnCount; c++)
                    if (x < columnLefts[c + 1]) { column = c; break; }
            }
            if (local.y >= HeaderHeight)
                row = Mathf.Clamp(Mathf.FloorToInt((local.y - HeaderHeight + scrollY) / RowHeight), 0, rowCount - 1);
            return (column, row);
        }

        // ------------------------------------------------------------------ drawing

        public void Refresh()
        {
            if (float.IsNaN(layout.width) || layout.width < 10) return;
            Measure();
            float viewWidth = ViewWidth, viewHeight = ViewHeight;

            corner.style.width = RowHeaderWidth;
            corner.style.height = HeaderHeight;
            Place(columnStrip, RowHeaderWidth, 0, viewWidth, HeaderHeight);
            Place(rowStrip, 0, HeaderHeight, RowHeaderWidth, viewHeight);
            Place(cells, RowHeaderWidth, HeaderHeight, viewWidth, viewHeight);
            Place(vertical, RowHeaderWidth + viewWidth, HeaderHeight, ScrollerSize, viewHeight);
            Place(horizontal, RowHeaderWidth, HeaderHeight + viewHeight, viewWidth, ScrollerSize);
            UpdateScrollers(viewWidth, viewHeight);

            int firstRow = Mathf.FloorToInt(scrollY / RowHeight);
            int lastRow = Mathf.Min(rowCount - 1, Mathf.FloorToInt((scrollY + viewHeight) / RowHeight));
            int firstColumn = 0;
            while (firstColumn < columnCount - 1 && columnLefts[firstColumn + 1] <= scrollX) firstColumn++;
            int lastColumn = firstColumn;
            while (lastColumn < columnCount - 1 && columnLefts[lastColumn + 1] < scrollX + viewWidth) lastColumn++;

            CellRange selection = Selection;
            int used = 0;
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                Label head = Pooled(columnPool, columnStrip, used++, "sheet-head");
                Rect rect = CellRect(column, 0);
                Place(head, rect.x, 0, rect.width, HeaderHeight);
                head.text = CellAddress.ColumnName(column);
                head.EnableInClassList("sheet-head--active", column >= selection.Start.Column && column <= selection.End.Column);
            }
            Trim(columnPool, used);

            used = 0;
            for (int row = firstRow; row <= lastRow; row++)
            {
                Label head = Pooled(rowPool, rowStrip, used++, "sheet-head");
                Place(head, 0, row * RowHeight - scrollY, RowHeaderWidth, RowHeight);
                head.text = (row + 1).ToString();
                head.EnableInClassList("sheet-head--active", row >= selection.Start.Row && row <= selection.End.Row);
            }
            Trim(rowPool, used);

            used = 0;
            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    Label cell = Pooled(cellPool, cells, used++, "sheet-cell");
                    var address = new CellAddress(column, row);
                    CellValue value = sheet != null ? sheet.Value(address) : CellValue.Empty;
                    Rect rect = CellRect(column, row);
                    float width = rect.width;
                    int span = 0;
                    // Long text spills over empty neighbours, as in a spreadsheet.
                    if (value.IsText && sheet != null)
                        while (span < 12 && column + span + 1 < columnCount
                               && sheet.Value(new CellAddress(column + span + 1, row)).IsEmpty
                               && width < value.Text.Length * 9.5f + 12f)
                        {
                            span++;
                            width += Width(column + span);
                        }
                    Place(cell, rect.x, rect.y, width, RowHeight);
                    cell.text = sheet != null ? sheet.Display(address) : string.Empty;
                    cell.EnableInClassList("sheet-cell--number", value.IsNumber);
                    cell.EnableInClassList("sheet-cell--error", value.IsError);
                    cell.EnableInClassList("sheet-cell--center", value.IsBoolean);
                    cell.EnableInClassList("sheet-cell--marked", IsMarked != null && sheet != null && IsMarked(sheet, address));
                    cell.EnableInClassList("sheet-cell--flagged", IsFlagged != null && sheet != null && IsFlagged(sheet, address));
                    column += span;
                }
            }
            Trim(cellPool, used);

            DrawSelection(selection);
            editor.BringToFront();
        }

        private void DrawSelection(CellRange selection)
        {
            Rect range = RangeRect(selection);
            Place(rangeBox, range.x, range.y, range.width, range.height);
            rangeBox.style.display = selection.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            Rect active = CellRect(anchor.Column, anchor.Row);
            Place(activeBox, active.x - 1, active.y - 1, active.width + 1, active.height + 1);
            Place(fillHandle, range.xMax - HandleSize / 2f - 1, range.yMax - HandleSize / 2f - 1, HandleSize, HandleSize);
            fillHandle.style.display = IsEditing ? DisplayStyle.None : DisplayStyle.Flex;
            if (fillTarget.HasValue)
            {
                Rect target = RangeRect(fillTarget.Value);
                Place(fillTargetBox, target.x, target.y, target.width, target.height);
                fillTargetBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                fillTargetBox.style.display = DisplayStyle.None;
            }

            if (copyRange.HasValue && copySheet == sheet)
            {
                Rect copied = RangeRect(copyRange.Value);
                Place(copyBox, copied.x - 1, copied.y - 1, copied.width + 2, copied.height + 2);
                copyBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                copyBox.style.display = DisplayStyle.None;
            }

            bool showEditor = IsEditing && editSheet == sheet;
            editor.style.display = showEditor ? DisplayStyle.Flex : DisplayStyle.None;
            if (showEditor)
            {
                Rect rect = CellRect(editCell.Column, editCell.Row);
                Place(editor, rect.x - 1, rect.y - 1, Mathf.Max(rect.width + 2, 220f), RowHeight + 2);
            }
        }

        private VisualElement Overlay(string className)
        {
            VisualElement overlay = HudElements.Box(className);
            overlay.pickingMode = PickingMode.Ignore;
            cells.Add(overlay);
            return overlay;
        }

        private static Label Pooled(List<Label> pool, VisualElement parent, int index, string className)
        {
            if (index < pool.Count)
            {
                pool[index].style.display = DisplayStyle.Flex;
                return pool[index];
            }
            Label label = HudElements.Text(string.Empty, className);
            label.pickingMode = PickingMode.Ignore;
            parent.Insert(0, label); // under the selection overlays
            pool.Add(label);
            return label;
        }

        private static void Trim(List<Label> pool, int used)
        {
            for (int i = used; i < pool.Count; i++) pool[i].style.display = DisplayStyle.None;
        }

        private static void Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
        }

        /// <summary>Gives a width to columns that got long content after the tab was first shown (never shrinks).</summary>
        public void FitNewColumns()
        {
            if (sheet == null || !widths.TryGetValue(sheet, out var map)) return;
            foreach (KeyValuePair<int, float> fitted in AutoWidths(sheet))
                if (!map.ContainsKey(fitted.Key)) map[fitted.Key] = fitted.Value;
        }

        /// <summary>Columns sized to their content (code, description) the first time a tab is shown.</summary>
        private static Dictionary<int, float> AutoWidths(Worksheet worksheet)
        {
            var map = new Dictionary<int, float>();
            // Size by the table: from the header row down to the first empty code; the title above and
            // the notes below spill over instead.
            int lastRow = 1;
            while (lastRow + 1 < worksheet.UsedRows && !worksheet.Value(new CellAddress(0, lastRow + 1)).IsEmpty) lastRow++;
            for (int column = 0; column < worksheet.UsedColumns; column++)
            {
                int longest = 0;
                for (int row = 1; row <= lastRow; row++)
                    longest = Mathf.Max(longest, worksheet.Display(new CellAddress(column, row)).Length);
                if (longest > 8) map[column] = Mathf.Clamp(longest * 9.5f + 16f, DefaultColumnWidth, 380f);
            }
            return map;
        }

        // ------------------------------------------------------------------ scrolling

        private void UpdateScrollers(float viewWidth, float viewHeight)
        {
            updatingScrollers = true;
            float maxX = Mathf.Max(0, columnLefts[columnCount] - viewWidth);
            float maxY = Mathf.Max(0, rowCount * RowHeight - viewHeight);
            scrollX = Mathf.Clamp(scrollX, 0, maxX);
            scrollY = Mathf.Clamp(scrollY, 0, maxY);
            horizontal.highValue = Mathf.Max(maxX, 0.01f);
            vertical.highValue = Mathf.Max(maxY, 0.01f);
            horizontal.value = scrollX;
            vertical.value = scrollY;
            horizontal.Adjust(columnLefts[columnCount] <= 0 ? 1 : Mathf.Clamp01(viewWidth / columnLefts[columnCount]));
            vertical.Adjust(Mathf.Clamp01(viewHeight / (rowCount * RowHeight)));
            updatingScrollers = false;
        }

        private void OnScroll(float x, float y)
        {
            if (updatingScrollers) return;
            HideTooltip();
            scrollX = x;
            scrollY = y;
            Refresh();
        }

        private void OnWheel(WheelEvent evt)
        {
            HideTooltip();
            float steps = Mathf.Sign(evt.delta.y) * (evt.shiftKey ? 2 * DefaultColumnWidth : 3 * RowHeight);
            if (evt.shiftKey) scrollX += steps;
            else scrollY += steps;
            Refresh();
            evt.StopPropagation();
        }

        private void ScrollIntoView(CellAddress cell)
        {
            if (float.IsNaN(layout.width) || layout.width < 10) return;
            Measure();
            float left = columnLefts[Mathf.Min(cell.Column, columnCount)], right = left + Width(cell.Column);
            float top = cell.Row * RowHeight, bottom = top + RowHeight;
            if (left < scrollX) scrollX = left;
            else if (right > scrollX + ViewWidth) scrollX = right - ViewWidth;
            if (top < scrollY) scrollY = top;
            else if (bottom > scrollY + ViewHeight) scrollY = bottom - ViewHeight;
        }

        // ------------------------------------------------------------------ pointer

        private bool IsInScrollers(IEventHandler target) =>
            target is VisualElement element && (vertical.Contains(element) || horizontal.Contains(element) || editor.Contains(element));

        private void OnPointerDown(PointerDownEvent evt)
        {
            HideTooltip();
            if (sheet == null || evt.button != 0 || IsInScrollers(evt.target)) return;
            Vector2 local = this.WorldToLocal(evt.position);
            (int column, int row) = Hit(local);

            Rect handle = RangeRect(Selection);
            Vector2 inCells = local - new Vector2(RowHeaderWidth, HeaderHeight);
            if (!IsEditing && Mathf.Abs(inCells.x - handle.xMax) <= HandleSize && Mathf.Abs(inCells.y - handle.yMax) <= HandleSize)
            {
                filling = true;
                fillTarget = null;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (column < 0 && row < 0) return;

            if (AcceptsPick && column >= 0 && row >= 0)
            {
                picking = true;
                pickAnchor = new CellAddress(column, row);
                InsertReference(new CellRange(pickAnchor, pickAnchor));
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (IsEditing) CommitEdit(move: false);

            Focus();
            if (column < 0)
            {
                anchor = new CellAddress(0, row);
                cursor = new CellAddress(columnCount - 1, row);
            }
            else if (row < 0)
            {
                anchor = new CellAddress(column, 0);
                cursor = new CellAddress(column, Mathf.Max(0, (sheet.UsedRows > 0 ? sheet.UsedRows : rowCount) - 1));
            }
            else if (evt.shiftKey)
            {
                cursor = new CellAddress(column, row);
            }
            else
            {
                anchor = cursor = new CellAddress(column, row);
                if (evt.clickCount >= 2)
                {
                    BeginEdit(sheet.GetInput(anchor));
                    evt.StopPropagation();
                    return;
                }
                selecting = true;
                this.CapturePointer(evt.pointerId);
            }
            Refresh();
            StateChanged?.Invoke();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!selecting && !filling && !picking)
            {
                UpdateHover(this.WorldToLocal(evt.position));
                return;
            }
            HideTooltip();
            lastPointer = this.WorldToLocal(evt.position);
            autoScroll ??= schedule.Execute(AutoScroll).Every(60);
            autoScroll.Resume();
            UpdateDrag(lastPointer);
        }

        /// <summary>While dragging past an edge the grid keeps scrolling, as a spreadsheet does.</summary>
        private void AutoScroll()
        {
            if (!selecting && !filling && !picking)
            {
                autoScroll?.Pause();
                return;
            }
            float dx = lastPointer.x > layout.width - ScrollerSize - 6 ? DefaultColumnWidth / 3
                : lastPointer.x < RowHeaderWidth + 4 ? -DefaultColumnWidth / 3 : 0;
            float dy = lastPointer.y > layout.height - ScrollerSize - 6 ? RowHeight / 2
                : lastPointer.y < HeaderHeight + 4 ? -RowHeight / 2 : 0;
            if (dx == 0 && dy == 0) return;
            scrollX = Mathf.Max(0, scrollX + dx);
            scrollY = Mathf.Max(0, scrollY + dy);
            UpdateDrag(lastPointer);
        }

        private void UpdateDrag(Vector2 local)
        {
            (int column, int row) = Hit(new Vector2(
                Mathf.Clamp(local.x, RowHeaderWidth, layout.width - ScrollerSize - 1),
                Mathf.Clamp(local.y, HeaderHeight, layout.height - ScrollerSize - 1)));
            var point = new CellAddress(column, row);

            if (picking)
            {
                InsertReference(new CellRange(pickAnchor, point));
            }
            else if (filling)
            {
                fillTarget = FillTargetFor(point);
            }
            else
            {
                cursor = point;
                StateChanged?.Invoke();
            }
            Refresh();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            if (filling && fillTarget.HasValue && sheet != null)
            {
                CellRange target = fillTarget.Value;
                CellRange source = Selection;
                sheet.Book.History.Record($"arrastrar {source} hasta {target}", () => sheet.Fill(source, target));
                anchor = target.Start;
                cursor = target.End;
            }
            if (picking) FocusEditorAtCaret();
            selecting = filling = picking = false;
            autoScroll?.Pause();
            fillTarget = null;
            Refresh();
            StateChanged?.Invoke();
        }

        /// <summary>The fill handle extends the selection down, up, right or left, whichever the pointer went further.</summary>
        private CellRange? FillTargetFor(CellAddress point)
        {
            CellRange source = Selection;
            int down = point.Row - source.End.Row, up = source.Start.Row - point.Row;
            int right = point.Column - source.End.Column, left = source.Start.Column - point.Column;
            int best = Mathf.Max(Mathf.Max(down, up), Mathf.Max(right, left));
            if (best <= 0) return null;
            if (best == down) return new CellRange(source.Start, new CellAddress(source.End.Column, point.Row));
            if (best == up) return new CellRange(new CellAddress(source.Start.Column, point.Row), source.End);
            if (best == right) return new CellRange(source.Start, new CellAddress(point.Column, source.End.Row));
            return new CellRange(new CellAddress(point.Column, source.Start.Row), source.End);
        }

        // ------------------------------------------------------------------ copy and paste

        /// <summary>Ctrl+C: copies the selection (a whole row is trimmed to its used columns).</summary>
        public void Copy()
        {
            if (sheet == null) return;
            if (IsEditing) CommitEdit(move: false);
            copyRange = clipboard.Copy(sheet, Selection);
            copySheet = sheet;
            GUIUtility.systemCopyBuffer = clipboard.Text;
            Refresh();
            Notice?.Invoke($"Copiaste {copyRange}. Selecciona la celda donde empieza el destino y pulsa Ctrl+V " +
                           "(Ctrl+Shift+V pega solo los valores).");
        }

        /// <summary>
        /// Ctrl+V (or Ctrl+Shift+V for values only): pastes at the top-left cell of the selection. Text
        /// copied in another program (tabs between columns) is pasted as if typed.
        /// </summary>
        public void Paste(bool valuesOnly = false)
        {
            if (sheet == null) return;
            if (IsEditing) CommitEdit(move: false);
            CellAddress at = Selection.Start;
            string outside = GUIUtility.systemCopyBuffer;
            bool external = !string.IsNullOrEmpty(outside) && (!clipboard.HasContent || outside != clipboard.Text);
            CellRange? pasted = null;
            sheet.Book.History.Record($"pegar en {at}", () =>
                pasted = external ? SheetClipboard.PasteText(sheet, at, outside) : clipboard.Paste(sheet, at, valuesOnly));
            if (!pasted.HasValue)
            {
                Notice?.Invoke(clipboard.HasContent || external
                    ? "No cabe ahí: lo copiado se saldría de la hoja."
                    : "Todavía no copiaste nada: selecciona celdas o una fila y pulsa Ctrl+C.");
                return;
            }
            anchor = pasted.Value.Start;
            cursor = pasted.Value.End;
            FitNewColumns();
            Refresh();
            StateChanged?.Invoke();
            Notice?.Invoke(external ? $"Pegaste en {pasted} el texto copiado de otro programa."
                : valuesOnly ? $"Pegaste solo los valores en {pasted}, sin fórmulas."
                : $"Pegaste en {pasted}. Las fórmulas se movieron igual que en Excel.");
        }

        /// <summary>Forgets the dashed copy outline (Esc, as in a spreadsheet).</summary>
        public void ClearCopyOutline()
        {
            copyRange = null;
            Refresh();
        }

        // ------------------------------------------------------------------ undo and redo

        /// <summary>Ctrl+Z: undoes the last action (writing, deleting, pasting, dragging, marks).</summary>
        public void Undo() => Replay(redoing: false);

        /// <summary>Ctrl+Y: does again the last undone action.</summary>
        public void Redo() => Replay(redoing: true);

        private void Replay(bool redoing)
        {
            if (sheet == null) return;
            // An edit in progress is dropped first, as in Excel (Ctrl+Z while typing restores the cell).
            if (IsEditing)
            {
                CancelEdit();
                if (!redoing) return;
            }
            SheetHistory history = sheet.Book.History;
            HistoryChange change = redoing ? history.Redo() : history.Undo();
            if (change == null)
            {
                Notice?.Invoke(redoing ? "No hay nada para rehacer." : "No hay nada para deshacer.");
                return;
            }
            if (change.Sheet != null && change.Cells.HasValue)
            {
                if (change.Sheet != sheet)
                {
                    SetSheet(change.Sheet);
                    SheetSwitched?.Invoke(change.Sheet);
                }
                Select(change.Cells.Value.Start, change.Cells.Value.End);
            }
            else
            {
                Refresh();
            }
            StateChanged?.Invoke();
            Notice?.Invoke((redoing ? "Rehiciste: " : "Deshiciste: ") + change.Label +
                           (redoing ? "." : ". Ctrl+Y lo vuelve a hacer."));
        }

        // ------------------------------------------------------------------ hover note

        private void UpdateHover(Vector2 local)
        {
            if (sheet == null || HoverNote == null || selecting || filling || picking)
            {
                HideTooltip();
                return;
            }
            (int column, int row) = Hit(local);
            if (column < 0 || row < 0)
            {
                HideTooltip();
                return;
            }
            if ((column, row) != hovered)
            {
                hovered = (column, row);
                string note = HoverNote(sheet, new CellAddress(column, row));
                tooltip.text = note ?? string.Empty;
                tooltip.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (tooltip.style.display == DisplayStyle.None) return;
            // Below and to the right of the pointer, kept inside the grid.
            float x = Mathf.Min(local.x + 16, Mathf.Max(0, layout.width - 460));
            float y = local.y + 22 + 120 > layout.height ? local.y - 130 : local.y + 22;
            tooltip.style.left = x;
            tooltip.style.top = Mathf.Max(0, y);
            tooltip.BringToFront();
        }

        private void HideTooltip()
        {
            hovered = (-1, -1);
            tooltip.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ keyboard

        private void StopNavigation(EventBase evt)
        {
            if (IsEditing) return;
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (sheet == null || IsEditing) return;
            bool handled = true;
            if (evt.actionKey && (evt.keyCode == KeyCode.C || evt.keyCode == KeyCode.V || evt.keyCode == KeyCode.Z || evt.keyCode == KeyCode.Y))
            {
                if (evt.keyCode == KeyCode.C) Copy();
                else if (evt.keyCode == KeyCode.V) Paste(valuesOnly: evt.shiftKey);
                // Ctrl+Z undoes and Ctrl+Y redoes; Cmd+Shift+Z also redoes, as on a Mac.
                else if (evt.keyCode == KeyCode.Y || evt.shiftKey) Redo();
                else Undo();
                evt.StopPropagation();
                focusController?.IgnoreEvent(evt);
                return;
            }
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    if (copyRange.HasValue) ClearCopyOutline();
                    else handled = false;
                    break;
                case KeyCode.UpArrow: Move(0, -1, evt.shiftKey); break;
                case KeyCode.DownArrow: Move(0, 1, evt.shiftKey); break;
                case KeyCode.LeftArrow: Move(-1, 0, evt.shiftKey); break;
                case KeyCode.RightArrow: Move(1, 0, evt.shiftKey); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: Move(0, evt.shiftKey ? -1 : 1, false); break;
                case KeyCode.Tab: Move(evt.shiftKey ? -1 : 1, 0, false); break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    CellRange selection = Selection;
                    sheet.Book.History.Record($"borrar {selection}", () => sheet.Clear(selection));
                    break;
                case KeyCode.F2: BeginEdit(sheet.GetInput(anchor)); break;
                default:
                    // Typing starts editing with that character (the key and its character may come in one
                    // event or in two; only the one carrying the character counts).
                    char c = evt.character;
                    if (c == '\0' || char.IsControl(c) || evt.ctrlKey || evt.commandKey) handled = false;
                    else BeginEdit(c.ToString());
                    break;
            }
            if (!handled) return;
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        private void Move(int columns, int rows, bool extend)
        {
            CellAddress from = extend ? cursor : anchor;
            var to = new CellAddress(Mathf.Clamp(from.Column + columns, 0, CellAddress.MaxColumns - 1),
                Mathf.Clamp(from.Row + rows, 0, CellAddress.MaxRows - 1));
            if (extend) cursor = to;
            else anchor = cursor = to;
            ScrollIntoView(to);
            Refresh();
            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------------ editing

        /// <summary>Starts editing the active cell with <paramref name="text"/> (typed character or the current input).</summary>
        public void BeginEdit(string text, bool focusEditor = true)
        {
            if (sheet == null) return;
            editSheet = sheet;
            editCell = anchor;
            cursor = anchor;
            pickStart = pickEnd = -1;
            syncingText = true;
            editor.SetValueWithoutNotify(text ?? string.Empty);
            syncingText = false;
            caret = editor.value.Length;
            Refresh();
            if (focusEditor) FocusEditorAtCaret();
            StateChanged?.Invoke();
        }

        /// <summary>Text typed in the formula bar while editing, with the cursor of the bar.</summary>
        public void SetEditText(string text, int? barCaret = null)
        {
            if (!IsEditing) BeginEdit(text, focusEditor: false);
            else
            {
                syncingText = true;
                editor.SetValueWithoutNotify(text);
                syncingText = false;
                OnEditTextChanged(text, fromBar: true);
            }
            caret = Mathf.Clamp(barCaret ?? text.Length, 0, text.Length);
            StateChanged?.Invoke();
        }

        /// <summary>The cursor moved (arrows, click inside the text, formula bar): a new pick goes there.</summary>
        public void NoteCaret(int index)
        {
            if (!IsEditing) return;
            int clamped = Mathf.Clamp(index, 0, editor.value.Length);
            if (clamped == caret) return;
            caret = clamped;
            pickStart = pickEnd = -1;
            StateChanged?.Invoke();
        }

        public void CommitEdit(bool move = true, int columns = 0, int rows = 1)
        {
            if (!IsEditing) return;
            Worksheet target = editSheet;
            CellAddress cell = editCell;
            string text = editor.value;
            editSheet = null;
            pickStart = pickEnd = -1;
            target.Book.History.Record($"escribir en {cell}", () => target.SetInput(cell, text));
            if (target != sheet)
            {
                SetSheet(target);
                SheetSwitched?.Invoke(target);
            }
            anchor = cursor = cell;
            if (move) Move(columns, rows, false);
            else Refresh();
            Focus();
            StateChanged?.Invoke();
        }

        public void CancelEdit()
        {
            if (!IsEditing) return;
            Worksheet target = editSheet;
            editSheet = null;
            pickStart = pickEnd = -1;
            if (target != sheet)
            {
                SetSheet(target);
                SheetSwitched?.Invoke(target);
            }
            Refresh();
            Focus();
            StateChanged?.Invoke();
        }

        private void OnEditorKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CommitEdit(true, 0, evt.shiftKey ? -1 : 1);
                    break;
                case KeyCode.Tab:
                    CommitEdit(true, evt.shiftKey ? -1 : 1, 0);
                    break;
                case KeyCode.Escape:
                    CancelEdit();
                    break;
                case KeyCode.Z when evt.actionKey:
                    CancelEdit();
                    break;
                default:
                    return;
            }
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        private void OnEditTextChanged(string text, bool fromBar)
        {
            if (syncingText) return;
            pickStart = pickEnd = -1; // typing (in the cell or in the formula bar) ends the reference being picked
            if (!fromBar) caret = Mathf.Clamp(editor.cursorIndex, 0, text.Length);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Writes the clicked cell or range where the cursor is ('Datos'!B3 when it is on another tab). While
        /// the player keeps clicking or dragging, the same reference is replaced, as in Excel.
        /// </summary>
        private void InsertReference(CellRange range)
        {
            string text = editor.value;
            if (pickStart < 0 || pickStart > text.Length)
            {
                pickStart = caret;
                pickEnd = caret;
            }
            string prefix = sheet != editSheet ? FormulaLexer.QuoteSheet(sheet.Name) + "!" : string.Empty;
            string updated = FormulaEditing.Insert(text, pickStart, pickEnd, prefix + range, out caret);
            pickEnd = caret;
            syncingText = true;
            editor.SetValueWithoutNotify(updated);
            syncingText = false;
            anchor = range.Start;
            cursor = range.End;
            StateChanged?.Invoke();
        }

        /// <summary>Gives the focus back to the cell editor with the cursor where it was, nothing selected.</summary>
        private void FocusEditorAtCaret()
        {
            editor.Focus();
            int at = caret;
            editor.SelectRange(at, at);
            // The focus may arrive a frame later; place the cursor again then.
            editor.schedule.Execute(() =>
            {
                if (IsEditing) editor.SelectRange(caret, caret);
            });
        }

        /// <summary>A picked reference stays replaceable until the player types again.</summary>
        public bool AcceptsPick => IsEditing && (pickStart >= 0 || FormulaEditing.CanInsertReference(editor.value, caret));
    }
}
