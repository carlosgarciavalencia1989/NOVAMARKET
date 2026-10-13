using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class SheetBasicsTests
    {
        [TestCase(0, "A")]
        [TestCase(25, "Z")]
        [TestCase(26, "AA")]
        [TestCase(45, "AT")]
        [TestCase(701, "ZZ")]
        public void ColumnNames_RoundTrip(int index, string name)
        {
            Assert.AreEqual(name, CellAddress.ColumnName(index));
            Assert.AreEqual(index, CellAddress.ColumnIndex(name));
            Assert.AreEqual(index, CellAddress.ColumnIndex(name.ToLowerInvariant()));
        }

        [Test]
        public void Addresses_ParseAndPrint()
        {
            CellAddress cell = CellAddress.Parse("AT3");
            Assert.AreEqual(45, cell.Column);
            Assert.AreEqual(2, cell.Row);
            Assert.AreEqual("AT3", cell.ToString());
            Assert.IsFalse(CellAddress.TryParse("A0", out _));
            Assert.IsFalse(CellAddress.TryParse("3A", out _));
            Assert.IsFalse(CellAddress.TryParse("AAA1", out _));

            CellRange range = CellRange.Parse("C3:AT3");
            Assert.AreEqual(44, range.Count);
            Assert.AreEqual("C3:AT3", range.ToString());
            Assert.AreEqual(range, CellRange.Parse("AT3:C3"), "Ranges are normalised.");
        }

        [TestCase("4695", 4695)]
        [TestCase("4.695", 4695)]
        [TestCase("1.234.567", 1234567)]
        [TestCase("4694,67", 4694.67)]
        [TestCase("4.694,67", 4694.67)]
        [TestCase("-2,5", -2.5)]
        [TestCase(",5", 0.5)]
        [TestCase("45%", 0.45)]
        [TestCase("12,5 %", 0.125)]
        public void Numbers_AreReadTheColombianWay(string text, double expected)
        {
            Assert.IsTrue(SheetNumber.TryParse(text, out double value), text);
            Assert.AreEqual(expected, value, 1e-9);
        }

        [TestCase("4.69")]
        [TestCase("1,2,3")]
        [TestCase("12a")]
        [TestCase("")]
        [TestCase("-")]
        [TestCase("1.2345")]
        public void Numbers_RejectWhatIsNotANumber(string text) => Assert.IsFalse(SheetNumber.TryParse(text, out _), text);

        [Test]
        public void Numbers_AreShownTheColombianWay()
        {
            Assert.AreEqual("4694,666667", SheetNumber.Format(14084 / 3.0, CellFormat.General));
            Assert.AreEqual("4695", SheetNumber.Format(4695, CellFormat.General));
            Assert.AreEqual("4.695", SheetNumber.Format(4694.67, CellFormat.Number(0)));
            Assert.AreEqual("4.694,67", SheetNumber.Format(14084 / 3.0, CellFormat.Number(2)));
            Assert.AreEqual("1.234.567,0", SheetNumber.Format(1234567, CellFormat.Number(1)));
            Assert.AreEqual("17,3%", SheetNumber.Format(0.173, CellFormat.Percent(1)));
            Assert.AreEqual("0", SheetNumber.Format(-0.0001, CellFormat.Number(0)), "No negative zero.");
            Assert.AreEqual("3", SheetNumber.Format(2.5, CellFormat.Number(0)), "Half rounds up, as in Excel.");
        }

        [Test]
        public void ErrorTexts_AreTheSpanishOnes()
        {
            Assert.AreEqual("#¡DIV/0!", CellValue.Fail(SheetError.DivisionByZero).ToString());
            Assert.AreEqual("#¡VALOR!", CellValue.Fail(SheetError.Value).ToString());
            Assert.AreEqual("#¡REF!", CellValue.Fail(SheetError.Reference).ToString());
            Assert.AreEqual("#¿NOMBRE?", CellValue.Fail(SheetError.Name).ToString());
            Assert.AreEqual("VERDADERO", CellValue.Of(true).ToString());
        }
    }
}
