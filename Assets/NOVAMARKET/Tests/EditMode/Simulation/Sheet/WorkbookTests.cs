using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class WorkbookTests
    {
        private Workbook book;
        private Worksheet sheet;

        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            sheet = book.AddSheet("Hoja1");
        }

        [Test]
        public void Inputs_BecomeNumbersTextBooleansOrFormulas()
        {
            sheet.SetInput("A1", "4.695");
            sheet.SetInput("A2", "Labial");
            sheet.SetInput("A3", "'123");
            sheet.SetInput("A4", "falso");
            sheet.SetInput("A5", "=A1*2");

            Assert.AreEqual(CellValue.Of(4695), sheet.Value("A1"));
            Assert.AreEqual("Labial", sheet.Value("A2").Text);
            Assert.AreEqual("123", sheet.Value("A3").Text);
            Assert.AreEqual(CellValue.Of(false), sheet.Value("A4"));
            Assert.AreEqual(9390, sheet.Value("A5").Number);
            Assert.AreEqual("4695", sheet.GetInput(CellAddress.Parse("A1")));
            Assert.AreEqual("'123", sheet.GetInput(CellAddress.Parse("A3")));
            Assert.AreEqual("=A1*2", sheet.GetInput(CellAddress.Parse("A5")));
            Assert.IsTrue(sheet.IsFormula(CellAddress.Parse("A5")));
        }

        [Test]
        public void ChangingACell_RecalculatesItsDependents()
        {
            sheet.SetInput("A1", "2");
            sheet.SetInput("A2", "=A1*10");
            sheet.SetInput("A3", "=A2+1");
            Assert.AreEqual(21, sheet.Value("A3").Number);

            sheet.SetInput("A1", "5");

            Assert.AreEqual(51, sheet.Value("A3").Number);
        }

        [Test]
        public void ReferenceToAnEmptyCell_ShowsZero()
        {
            sheet.SetInput("A1", "=B7");
            Assert.AreEqual(CellValue.Of(0), sheet.Value("A1"));
        }

        [Test]
        public void CircularReferences_AreReported()
        {
            sheet.SetInput("A1", "=B1+1");
            sheet.SetInput("B1", "=A1+1");
            sheet.SetInput("C1", "=C1");

            Assert.AreEqual(SheetError.Circular, sheet.Value("A1").Error);
            Assert.AreEqual(SheetError.Circular, sheet.Value("B1").Error);
            Assert.AreEqual(SheetError.Circular, sheet.Value("C1").Error);

            sheet.SetInput("B1", "1");
            Assert.AreEqual(2, sheet.Value("A1").Number, "Breaking the cycle fixes the cells.");
        }

        [Test]
        public void OtherSheets_AreReachedByName_EvenWithSpaces()
        {
            Worksheet data = book.AddSheet("Datos");
            Worksheet clean = book.AddSheet("Serie limpia");
            data.SetInput("H4", "6693");
            clean.SetInput("B3", "100");

            sheet.SetInput("A1", "=Datos!H4+1");
            sheet.SetInput("A2", "='Serie limpia'!B3*2");
            sheet.SetInput("A3", "=SUMA(Datos!H4:H5)");

            Assert.AreEqual(6694, sheet.Value("A1").Number);
            Assert.AreEqual(200, sheet.Value("A2").Number);
            Assert.AreEqual(6693, sheet.Value("A3").Number);

            book.RemoveSheet("Datos");
            Assert.AreEqual(SheetError.Reference, sheet.Value("A1").Error);
        }

        [Test]
        public void SheetNames_AreCheckedAndMadeUnique()
        {
            Assert.AreEqual("Hoja1 (2)", book.UniqueName("Hoja1"));
            Assert.Throws<System.ArgumentException>(() => book.AddSheet("hoja1"));
            Assert.Throws<System.ArgumentException>(() => book.AddSheet("a/b"));
            Assert.Throws<System.ArgumentException>(() => book.AddSheet(new string('x', 32)));
            Assert.IsNotNull(book.Sheet("HOJA1"));
            Assert.AreEqual("'Serie limpia'", FormulaLexer.QuoteSheet("Serie limpia"));
            Assert.AreEqual("Datos", FormulaLexer.QuoteSheet("Datos"));
        }

        [Test]
        public void Shifter_MovesRelativeReferencesOnly()
        {
            Assert.AreEqual("SUMA(C4:AT4)", FormulaShifter.Shift("SUMA(C3:AT3)", 0, 1));
            Assert.AreEqual("ABS(D3-PROMEDIO($C3:$AT3))", FormulaShifter.Shift("ABS(C3-PROMEDIO($C3:$AT3))", 1, 0));
            Assert.AreEqual("$A$1+B$1+$A2", FormulaShifter.Shift("$A$1+A$1+$A1", 1, 1));
            Assert.AreEqual("Datos!I4*'Serie limpia'!C3", FormulaShifter.Shift("Datos!H4*'Serie limpia'!B3", 1, 0));
            Assert.AreEqual("#¡REF!+1", FormulaShifter.Shift("A1+1", 0, -1));
            Assert.AreEqual("SUMA(#¡REF!)", FormulaShifter.Shift("SUMA(A1:B2)", -1, 0));
            Assert.AreEqual("SI(B2>3;\"A1\";B3)", FormulaShifter.Shift("SI(A1>3;\"A1\";A2)", 1, 1), "Text is not touched.");
        }

        [Test]
        public void FillDown_CopiesTheFormulaToTheWholeColumn()
        {
            for (int row = 1; row <= 5; row++) sheet.SetInput($"A{row}", (row * 10).ToString());
            sheet.SetInput("B1", "=A1*$D$1");
            sheet.SetInput("D1", "2");

            sheet.Fill(CellRange.Parse("B1"), CellRange.Parse("B1:B5"));

            Assert.AreEqual("=A5*$D$1", sheet.GetInput(CellAddress.Parse("B5")));
            for (int row = 1; row <= 5; row++) Assert.AreEqual(row * 20, sheet.Value($"B{row}").Number);
        }

        [Test]
        public void FillRight_ContinuesANumberSeries()
        {
            // Atlas, step p1: row of periods 1 2 3 … 44 above the months.
            sheet.SetInput("C1", "1");
            sheet.SetInput("D1", "2");

            sheet.Fill(CellRange.Parse("C1:D1"), CellRange.Parse("C1:AT1"));

            Assert.AreEqual(44, sheet.Value("AT1").Number);
            Assert.AreEqual(20, sheet.Value("V1").Number);
        }

        [Test]
        public void Fill_RepeatsTextAndUnevenNumbers()
        {
            sheet.SetInput("A1", "ok");
            sheet.Fill(CellRange.Parse("A1"), CellRange.Parse("A1:A3"));
            sheet.SetInput("B1", "1");
            sheet.SetInput("B2", "5");
            sheet.SetInput("B3", "6");
            sheet.Fill(CellRange.Parse("B1:B3"), CellRange.Parse("B1:B6"));

            Assert.AreEqual("ok", sheet.Value("A3").Text);
            Assert.AreEqual(1, sheet.Value("B4").Number);
            Assert.AreEqual(6, sheet.Value("B6").Number);
        }

        [Test]
        public void Fill_UpwardsAndWithFormats()
        {
            sheet.SetInput("A5", "=B5+1");
            sheet.SetFormat(CellRange.Parse("A5"), CellFormat.Percent(1));

            sheet.Fill(CellRange.Parse("A5"), CellRange.Parse("A3:A5"));

            Assert.AreEqual("=B3+1", sheet.GetInput(CellAddress.Parse("A3")));
            Assert.AreEqual(CellFormat.Percent(1), sheet.GetFormat(CellAddress.Parse("A3")));
            Assert.Throws<System.ArgumentException>(() => sheet.Fill(CellRange.Parse("A5"), CellRange.Parse("B6:B7")));
        }

        [Test]
        public void Batch_RaisesChangedOnce()
        {
            int changes = 0;
            book.Changed += () => changes++;

            book.Batch(() =>
            {
                for (int row = 0; row < 100; row++) sheet.SetNumber(new CellAddress(0, row), row);
            });
            sheet.SetInput("B1", "1");

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Display_UsesTheCellFormat()
        {
            sheet.SetInput("A1", "=14084/3");
            Assert.AreEqual("4694,666667", sheet.Display("A1"));
            sheet.SetFormat(CellRange.Parse("A1"), CellFormat.Number(0));
            Assert.AreEqual("4.695", sheet.Display("A1"));
            sheet.SetInput("A2", "=1/0");
            Assert.AreEqual("#¡DIV/0!", sheet.Display("A2"));
        }

        [Test]
        public void LargeSheet_RecalculatesQuickly()
        {
            // 200 SKUs × 44 months plus a dragged total column, as in the later levels.
            book.Batch(() =>
            {
                for (int row = 0; row < 200; row++)
                    for (int month = 0; month < 44; month++)
                        sheet.SetNumber(new CellAddress(2 + month, row), row + month);
            });
            sheet.SetInput("AU1", "=SUMA(AI1:AT1)");
            sheet.Fill(CellRange.Parse("AU1"), CellRange.Parse("AU1:AU200"));

            var watch = System.Diagnostics.Stopwatch.StartNew();
            double last = sheet.Value("AU200").Number;
            for (int row = 0; row < 200; row++) sheet.Value(new CellAddress(46, row));
            watch.Stop();

            Assert.AreEqual(12 * 199 + (32 + 43) * 6, last); // months 32..43
            Assert.Less(watch.ElapsedMilliseconds, 200);
        }
    }
}
