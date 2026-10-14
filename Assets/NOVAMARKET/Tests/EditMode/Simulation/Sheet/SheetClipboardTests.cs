using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class SheetClipboardTests
    {
        private Workbook book;
        private Worksheet sheet;

        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            sheet = book.AddSheet("Datos");
            sheet.SetInput("A1", "CV10004");
            sheet.SetInput("B1", "10");
            sheet.SetInput("C1", "20");
            sheet.SetInput("D1", "=B1+C1");
        }

        [Test]
        public void Paste_MovesTheFormulasLikeASpreadsheet()
        {
            var clipboard = new SheetClipboard();
            clipboard.Copy(sheet, CellRange.Parse("B1:D1"));

            CellRange? pasted = clipboard.Paste(sheet, CellAddress.Parse("B3"));

            Assert.AreEqual("B3:D3", pasted.ToString());
            Assert.AreEqual("10", sheet.Display("B3"));
            Assert.AreEqual("=B3+C3", sheet.GetInput(CellAddress.Parse("D3")));
            Assert.AreEqual(30, sheet.Value("D3").Number);
            Assert.AreEqual("10\t20\t30", clipboard.Text, "Tab-separated text for other programs.");
        }

        [Test]
        public void PasteValues_WritesWhatTheCopyShowed()
        {
            var clipboard = new SheetClipboard();
            clipboard.Copy(sheet, CellRange.Parse("D1"));
            sheet.SetInput("B1", "100"); // the copy keeps what it showed when it was taken

            clipboard.Paste(sheet, CellAddress.Parse("F1"), valuesOnly: true);

            Assert.IsFalse(sheet.IsFormula(CellAddress.Parse("F1")));
            Assert.AreEqual(30, sheet.Value("F1").Number);
        }

        [Test]
        public void WholeRow_PastesFromColumnA_AndClearsWhatWasEmpty()
        {
            var clipboard = new SheetClipboard();
            CellRange copied = clipboard.Copy(sheet, new CellRange(CellAddress.Parse("A1"), new CellAddress(CellAddress.MaxColumns - 1, 0)));
            Assert.AreEqual("A1:D1", copied.ToString(), "A whole row is trimmed to the used columns.");
            sheet.SetInput("F2", "basura");

            clipboard.Paste(sheet, CellAddress.Parse("A2"));

            Assert.AreEqual("CV10004", sheet.Display("A2"));
            Assert.AreEqual("=B2+C2", sheet.GetInput(CellAddress.Parse("D2")));
            Assert.AreEqual("basura", sheet.Display("F2"), "Columns outside the copy are not touched.");
        }

        [Test]
        public void TextFromOtherPrograms_IsReadAsTyped()
        {
            CellRange? pasted = SheetClipboard.PasteText(sheet, CellAddress.Parse("A5"), "4.695\t=A5*2\r\ntexto\t1,5\r\n");

            Assert.AreEqual("A5:B6", pasted.ToString());
            Assert.AreEqual(4695, sheet.Value("A5").Number);
            Assert.AreEqual(9390, sheet.Value("B5").Number);
            Assert.AreEqual("texto", sheet.Display("A6"));
            Assert.AreEqual(1.5, sheet.Value("B6").Number, 1e-9);
        }
    }
}
