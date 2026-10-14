using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Sheet
{
    /// <summary>What one undo or redo changed, so the sheet can show it.</summary>
    public sealed class HistoryChange
    {
        internal HistoryChange(string label, Worksheet sheet, CellRange? cells)
        {
            Label = label;
            Sheet = sheet;
            Cells = cells;
        }

        /// <summary>The action in words ("escribir en B3", "pegar en A2:D2", "marcar CV10004 jun-24").</summary>
        public string Label { get; }

        /// <summary>The tab with the changed cells (null when only marks changed).</summary>
        public Worksheet Sheet { get; }

        /// <summary>The smallest range covering the changed cells of <see cref="Sheet"/>.</summary>
        public CellRange? Cells { get; }
    }

    /// <summary>
    /// Undo and redo of the planner sheet, as Ctrl+Z / Ctrl+Y in a spreadsheet. Each action of the player
    /// (<see cref="Record"/>) keeps a copy of every cell it touches, and the level can add its own steps
    /// (problem marks) with <see cref="Add"/>. Edits made by the game (downloads, level setup) are not
    /// recorded. Keeps the last <see cref="Limit"/> actions.
    /// </summary>
    public sealed class SheetHistory
    {
        public const int Limit = 100;

        private sealed class Entry
        {
            public Worksheet Sheet;
            public CellAddress Cell;
            public Worksheet.CellState State;
            public Action Back;
            public Action Forward;
        }

        private sealed class Step
        {
            public string Label;
            public readonly List<Entry> Entries = new List<Entry>();
            public readonly HashSet<(Worksheet, CellAddress)> Seen = new HashSet<(Worksheet, CellAddress)>();
        }

        private readonly Workbook book;
        private readonly LinkedList<Step> undo = new LinkedList<Step>();
        private readonly Stack<Step> redo = new Stack<Step>();
        private Step open;
        private int depth;
        private bool replaying;

        internal SheetHistory(Workbook book) => this.book = book;

        /// <summary>Raised when what can be undone or redone changes (enables the buttons).</summary>
        public event Action Changed;

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public string NextUndo => undo.Last?.Value.Label;
        public string NextRedo => redo.Count > 0 ? redo.Peek().Label : null;

        /// <summary>True while undoing or redoing: the changes made then are not recorded as new actions.</summary>
        public bool IsReplaying => replaying;

        /// <summary>
        /// Runs an action of the player as one undo step (a paste of 200 cells is undone at once). Nested
        /// calls join the outer step. A step that changed nothing is not kept.
        /// </summary>
        public void Record(string label, Action edits)
        {
            if (edits == null) throw new ArgumentNullException(nameof(edits));
            if (replaying)
            {
                edits();
                return;
            }
            if (depth++ == 0) open = new Step { Label = label };
            try
            {
                book.Batch(edits);
            }
            finally
            {
                if (--depth == 0) Close();
            }
        }

        /// <summary>
        /// Adds a change kept outside the cells (a problem mark): <paramref name="back"/> undoes it and
        /// <paramref name="forward"/> does it again. Inside <see cref="Record"/> it joins that step.
        /// </summary>
        public void Add(string label, Action back, Action forward)
        {
            if (replaying) return;
            var entry = new Entry { Back = back ?? throw new ArgumentNullException(nameof(back)), Forward = forward ?? throw new ArgumentNullException(nameof(forward)) };
            if (open != null)
            {
                open.Entries.Add(entry);
                return;
            }
            var step = new Step { Label = label };
            step.Entries.Add(entry);
            Push(step);
        }

        /// <summary>Undoes the last action; null when there is nothing to undo.</summary>
        public HistoryChange Undo()
        {
            if (open != null || undo.Count == 0) return null;
            Step step = undo.Last.Value;
            undo.RemoveLast();
            Step inverse = Apply(step);
            redo.Push(inverse);
            Changed?.Invoke();
            return Describe(inverse);
        }

        /// <summary>Does again the last undone action; null when there is nothing to redo.</summary>
        public HistoryChange Redo()
        {
            if (open != null || redo.Count == 0) return null;
            Step inverse = Apply(redo.Pop());
            undo.AddLast(inverse);
            Changed?.Invoke();
            return Describe(inverse);
        }

        /// <summary>Forgets everything (a new level).</summary>
        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Called by the sheet before a cell changes: keeps its first state of the step.</summary>
        internal void Remember(Worksheet sheet, CellAddress cell)
        {
            if (open == null || replaying || !open.Seen.Add((sheet, cell))) return;
            open.Entries.Add(new Entry { Sheet = sheet, Cell = cell, State = sheet.Capture(cell) });
        }

        private void Close()
        {
            Step step = open;
            open = null;
            // Cells that ended as they started (typing the same value again) are not an action.
            step.Entries.RemoveAll(e => e.Sheet != null && Worksheet.CellState.Same(e.State, e.Sheet.Capture(e.Cell)));
            if (step.Entries.Count > 0) Push(step);
        }

        private void Push(Step step)
        {
            undo.AddLast(step);
            while (undo.Count > Limit) undo.RemoveFirst();
            redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Puts back what the step saved and returns the step that reverts it.</summary>
        private Step Apply(Step step)
        {
            var inverse = new Step { Label = step.Label };
            replaying = true;
            try
            {
                book.Batch(() =>
                {
                    for (int i = step.Entries.Count - 1; i >= 0; i--)
                    {
                        Entry entry = step.Entries[i];
                        if (entry.Sheet != null)
                        {
                            inverse.Entries.Add(new Entry { Sheet = entry.Sheet, Cell = entry.Cell, State = entry.Sheet.Capture(entry.Cell) });
                            entry.Sheet.Restore(entry.Cell, entry.State);
                        }
                        else
                        {
                            entry.Back();
                            inverse.Entries.Add(new Entry { Back = entry.Forward, Forward = entry.Back });
                        }
                    }
                });
            }
            finally
            {
                replaying = false;
            }
            // The inverse keeps the entries last-first, so applying it (also last-first) replays them in order.
            return inverse;
        }

        private static HistoryChange Describe(Step step)
        {
            Worksheet sheet = step.Entries.FirstOrDefault(e => e.Sheet != null && e.Sheet.Book.Sheets.Contains(e.Sheet))?.Sheet;
            if (sheet == null) return new HistoryChange(step.Label, null, null);
            List<CellAddress> cells = step.Entries.Where(e => e.Sheet == sheet).Select(e => e.Cell).ToList();
            var range = new CellRange(
                new CellAddress(cells.Min(c => c.Column), cells.Min(c => c.Row)),
                new CellAddress(cells.Max(c => c.Column), cells.Max(c => c.Row)));
            return new HistoryChange(step.Label, sheet, range);
        }
    }
}
