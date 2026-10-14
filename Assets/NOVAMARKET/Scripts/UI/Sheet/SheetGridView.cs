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
        private const string ReferenceStarters = "=(;+-*/^:<>&";

        private readonly VisualElement corner;
        private readonly VisualElement columnStrip;
        private readonly VisualElement rowStrip;
        private readonly VisualElement cells;
        private readonly VisualElement rangeBox;
        private readonly VisualElement activeBox;
        private readonly VisualElement fillTargetBox;
        private readonly VisualElement fillHandle;
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
        private CellAddress pickAnchor;
        private bool picking;
        private bool syncingText;
        private Vector2 lastPointer;
        private IVisualElementScheduledItem autoScroll;

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
            cells.Add(editor);

            vertical = new Scroller(0, 1, value => OnScroll(scrollX, value), SliderDirection.Vertical).WithClass("sheet-scroller");
            horizontal = new Scroller(0, 1, value => OnScroll(value, scrollY), SliderDirection.Horizontal).WithClass("sheet-scroller");
            Add(vertical);
            Add(horizontal);

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
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

        public Worksheet Sheet => sheet;
        public CellAddress Active => anchor;
        public CellRange Selection => new CellRange(anchor, cursor);
        public bool IsEditing => editSheet != null;
        public string EditText => editor.value;

        /// <summary>True while a formula is being typed and a click inserts a reference.</summary>
        public bool IsPicking => IsEditing && CanPick(editor.value);

        public void SetSheet(Worksheet worksheet)
        {
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
            scrollX = x;
            scrollY = y;
            Refresh();
        }

        private void OnWheel(WheelEvent evt)
        {
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
            if (!selecting && !filling && !picking) return;
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
                sheet.Fill(Selection, target);
                anchor = target.Start;
                cursor = target.End;
            }
            if (picking) editor.Focus();
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
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow: Move(0, -1, evt.shiftKey); break;
                case KeyCode.DownArrow: Move(0, 1, evt.shiftKey); break;
                case KeyCode.LeftArrow: Move(-1, 0, evt.shiftKey); break;
                case KeyCode.RightArrow: Move(1, 0, evt.shiftKey); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: Move(0, evt.shiftKey ? -1 : 1, false); break;
                case KeyCode.Tab: Move(evt.shiftKey ? -1 : 1, 0, false); break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    sheet.Clear(Selection);
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
            pickStart = -1;
            syncingText = true;
            editor.SetValueWithoutNotify(text ?? string.Empty);
            syncingText = false;
            Refresh();
            if (focusEditor)
            {
                editor.Focus();
                editor.schedule.Execute(() => editor.SelectRange(editor.value.Length, editor.value.Length));
            }
            StateChanged?.Invoke();
        }

        /// <summary>Text typed in the formula bar while editing.</summary>
        public void SetEditText(string text)
        {
            if (!IsEditing) BeginEdit(text, focusEditor: false);
            else
            {
                syncingText = true;
                editor.SetValueWithoutNotify(text);
                syncingText = false;
                OnEditTextChanged(text, fromBar: true);
            }
        }

        public void CommitEdit(bool move = true, int columns = 0, int rows = 1)
        {
            if (!IsEditing) return;
            Worksheet target = editSheet;
            CellAddress cell = editCell;
            string text = editor.value;
            editSheet = null;
            pickStart = -1;
            target.SetInput(cell, text);
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
            pickStart = -1;
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
                default:
                    return;
            }
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        private void OnEditTextChanged(string text, bool fromBar)
        {
            if (syncingText) return;
            pickStart = -1; // typing (in the cell or in the formula bar) ends the reference being picked
            StateChanged?.Invoke();
        }

        private static bool CanPick(string text) =>
            !string.IsNullOrEmpty(text) && text[0] == '=' && ReferenceStarters.IndexOf(text[text.Length - 1]) >= 0;

        /// <summary>Writes the clicked cell or range in the formula ('Datos'!B3 when it is on another tab).</summary>
        private void InsertReference(CellRange range)
        {
            string text = editor.value;
            if (pickStart < 0 || pickStart > text.Length) pickStart = text.Length;
            string prefix = sheet != editSheet ? FormulaLexer.QuoteSheet(sheet.Name) + "!" : string.Empty;
            string reference = prefix + range;
            syncingText = true;
            editor.SetValueWithoutNotify(text.Substring(0, pickStart) + reference);
            syncingText = false;
            anchor = range.Start;
            cursor = range.End;
            StateChanged?.Invoke();
        }

        /// <summary>A picked reference stays replaceable until the player types again.</summary>
        public bool AcceptsPick => IsEditing && (pickStart >= 0 || CanPick(editor.value));
    }
}
