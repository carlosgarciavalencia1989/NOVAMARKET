using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class SheetErrorHelpTests
    {
        private Workbook book;
        private Worksheet clean;

        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            book.AddSheet("Facturación").SetInput("C6", "33");
            clean = book.AddSheet("Serie limpia");
        }

        [Test]
        public void TwoCellsReadingEachOther_AreNamed_AndTheOriginalDataIsSuggested()
        {
            clean.SetInput("C8", "=C6*24");
            clean.SetInput("C6", "=C8");

            string help = SheetErrorHelp.Explain(clean, CellAddress.Parse("C6"), "Facturación");

            StringAssert.Contains("C6 y C8 se están leyendo entre sí", help);
            StringAssert.Contains("C6 usa C8 y C8 usa C6", help);
            StringAssert.Contains("«Facturación»", help);
        }

        [Test]
        public void ACellOutsideTheLoop_SaysWhichCellItReads()
        {
            clean.SetInput("C8", "=C6*24");
            clean.SetInput("C6", "=C8");
            clean.SetInput("D6", "=C6+1");

            string help = SheetErrorHelp.Explain(clean, CellAddress.Parse("D6"));

            StringAssert.Contains("D6 muestra #¡CIRCULAR! porque usa C6", help);
            StringAssert.Contains("C6 y C8 se están leyendo entre sí", help);
        }

        [Test]
        public void LongerLoopsAndSelfReferences_AreExplained()
        {
            clean.SetInput("A1", "=A1+1");
            StringAssert.Contains("A1 se usa a sí misma", SheetErrorHelp.Explain(clean, CellAddress.Parse("A1")));

            clean.SetInput("B1", "=B2");
            clean.SetInput("B2", "=B3");
            clean.SetInput("B3", "=B1");
            StringAssert.Contains("B1, B2 y B3 forman un círculo: B1 usa B2, B2 usa B3 y B3 usa B1",
                SheetErrorHelp.Explain(clean, CellAddress.Parse("B1")));
        }

        [Test]
        public void OtherErrors_SayWhatHappenedAndWhere()
        {
            clean.SetInput("A1", "=10/0");
            clean.SetInput("A2", "=A1*2");

            StringAssert.Contains("A1 divide entre cero", SheetErrorHelp.Explain(clean, CellAddress.Parse("A1")));
            StringAssert.Contains("porque usa A1", SheetErrorHelp.Explain(clean, CellAddress.Parse("A2")));
            Assert.IsNull(SheetErrorHelp.Explain(book.Sheet("Facturación"), CellAddress.Parse("C6")));
        }
    }
}
