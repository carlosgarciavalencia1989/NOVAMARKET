using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class SheetHistoryTests
    {
        private Workbook book;
        private Worksheet sheet;
        private SheetHistory history;

        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            sheet = book.AddSheet("Datos");
            history = book.History;
            // What the game writes (a download) is not an action of the player.
            sheet.SetInput("A1", "CV10004");
            sheet.SetInput("B1", "10");
            sheet.SetInput("C1", "20");
        }

        private void Type(string cell, string input) => history.Record($"escribir en {cell}", () => sheet.SetInput(cell, input));

        [Test]
        public void GameEdits_AreNotUndoable()
        {
            Assert.IsFalse(history.CanUndo);
            Assert.IsNull(history.Undo());
        }

        [Test]
        public void Typing_IsUndone_AndRedone()
        {
            Type("D1", "=B1+C1");
            Type("B1", "15");
            Assert.AreEqual(35, sheet.Value("D1").Number);

            HistoryChange change = history.Undo();
            Assert.AreEqual("escribir en B1", change.Label);
            Assert.AreEqual("B1", change.Cells.ToString());
            Assert.AreEqual(30, sheet.Value("D1").Number, "The formula recalculates with the old value.");

            history.Undo();
            Assert.AreEqual(string.Empty, sheet.GetInput(CellAddress.Parse("D1")));
            Assert.IsFalse(history.CanUndo);

            history.Redo();
            history.Redo();
            Assert.AreEqual("=B1+C1", sheet.GetInput(CellAddress.Parse("D1")));
            Assert.AreEqual(35, sheet.Value("D1").Number);
            Assert.IsFalse(history.CanRedo);
        }

        [Test]
        public void Deleting_IsUndone_WithFormatAndFormula()
        {
            Type("D1", "=B1+C1");
            history.Record("formato", () => sheet.SetFormat(CellRange.Parse("D1"), CellFormat.Number(2)));

            history.Record("borrar A1:D1", () => sheet.Clear(CellRange.Parse("A1:D1")));
            Assert.AreEqual(string.Empty, sheet.Display("A1"));

            history.Undo();
            Assert.AreEqual("CV10004", sheet.Display("A1"));
            Assert.AreEqual("=B1+C1", sheet.GetInput(CellAddress.Parse("D1")));
            Assert.AreEqual("30,00", sheet.Display("D1"));
        }

        [Test]
        public void PasteAndFill_AreOneStepEach()
        {
            Type("D1", "=B1+C1");
            var clipboard = new SheetClipboard();
            clipboard.Copy(sheet, CellRange.Parse("A1:D1"));
            history.Record("pegar", () => clipboard.Paste(sheet, CellAddress.Parse("A2")));
            history.Record("arrastrar", () => sheet.Fill(CellRange.Parse("D2"), CellRange.Parse("D2:D6")));
            Assert.AreEqual("=B6+C6", sheet.GetInput(CellAddress.Parse("D6")));

            Assert.AreEqual("D3:D6", history.Undo().Cells.ToString());
            Assert.AreEqual(string.Empty, sheet.GetInput(CellAddress.Parse("D6")));
            Assert.AreEqual("=B2+C2", sheet.GetInput(CellAddress.Parse("D2")), "Only the dragged cells go back.");

            Assert.AreEqual("A2:D2", history.Undo().Cells.ToString());
            Assert.AreEqual(string.Empty, sheet.Display("A2"));
            Assert.AreEqual(string.Empty, sheet.Display("D2"));
        }

        [Test]
        public void NewAction_AfterUndo_ClearsRedo()
        {
            Type("D1", "1");
            history.Undo();
            Assert.IsTrue(history.CanRedo);

            Type("D1", "2");

            Assert.IsFalse(history.CanRedo);
            Assert.AreEqual(2, sheet.Value("D1").Number);
        }

        [Test]
        public void TypingTheSameValue_IsNotAStep()
        {
            Type("B1", "10");
            Assert.IsFalse(history.CanUndo);
        }

        [Test]
        public void KeepsAtLeastFiftySteps()
        {
            Assert.GreaterOrEqual(SheetHistory.Limit, 50);
            for (int i = 1; i <= SheetHistory.Limit + 20; i++) Type("E1", i.ToString());

            Assert.AreEqual(SheetHistory.Limit, history.UndoCount);
            for (int i = 0; i < 60; i++) history.Undo();
            Assert.AreEqual(SheetHistory.Limit + 20 - 60, sheet.Value("E1").Number);
        }

        [Test]
        public void CustomSteps_AreUndoneAndRedone_WithoutBeingRecordedAgain()
        {
            bool marked = false;
            void Mark()
            {
                marked = true;
                history.Add("marcar", Unmark, Mark);
            }
            void Unmark()
            {
                marked = false;
                history.Add("quitar marca", Mark, Unmark);
            }

            Mark();
            Assert.AreEqual(1, history.UndoCount);

            history.Undo();
            Assert.IsFalse(marked);
            Assert.AreEqual(0, history.UndoCount);
            Assert.AreEqual(1, history.RedoCount);

            history.Redo();
            Assert.IsTrue(marked);
            Assert.AreEqual(1, history.UndoCount);
            Assert.AreEqual(0, history.RedoCount);
        }
    }
}
