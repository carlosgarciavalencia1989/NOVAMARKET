using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using NovaMarket.Tests.Simulation.Portfolio;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    /// <summary>The Excel formulas of the Atlas steps p1 and p2, typed exactly as the Atlas writes them.</summary>
    public class AtlasFormulaTests
    {
        private Workbook book;
        private Worksheet data;

        /// <summary>Atlas layout: one SKU per row from row 3, months ene-23…ago-26 in columns C…AT.</summary>
        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            data = book.AddSheet("Datos");
            string[] codes = { "CV10009", "CV10004" }; // rows 3 and 4
            for (int i = 0; i < codes.Length; i++)
            {
                int[] units = PortfolioTestData.Record(codes[i]).Units;
                data.SetText(new CellAddress(0, 2 + i), codes[i]);
                for (int month = 0; month < units.Length; month++) data.SetNumber(new CellAddress(2 + month, 2 + i), units[month]);
            }
            data.SetNumber(CellAddress.Parse("T4"), 0); // CV10004 jun-24 as it came before the Atlas imputed it
        }

        [Test]
        public void P1_TotalOfTheLast12Months()
        {
            data.SetInput("AU3", "=SUMA(AI3:AT3)");

            Assert.AreEqual(SeriesStatistics.LastMonthsTotal(PortfolioTestData.Record("CV10009").Units), data.Value("AU3").Number);
        }

        [Test]
        public void P2_ImputeAZeroWithTheSameMonthOfOtherYears()
        {
            Worksheet clean = book.AddSheet("Limpia");
            clean.SetInput("T4", "=PROMEDIO(Datos!H4;Datos!AF4;Datos!AR4)");
            clean.SetFormat(CellRange.Parse("T4"), CellFormat.Number(0));

            Assert.AreEqual(4694.67, clean.Value("T4").Number, 0.005);
            Assert.AreEqual("4.695", clean.Display("T4"), "Atlas: CV10004 jun-24 → 4.695.");

            clean.SetInput("U4", "=(Datos!S4+Datos!U4)/2");
            Assert.AreEqual(3301.5, clean.Value("U4").Number, 1e-9, "Atlas: with the neighbours it would be 3.302.");
        }

        [Test]
        public void P2_FlagOutliersWithThreeDeviations()
        {
            data.SetNumber(CellAddress.Parse("M3"), 60000); // a one-off customer order in CV10009 oct-23
            data.SetInput("C5", "=SI(ABS(C3-PROMEDIO($C3:$AT3))>3*DESVEST.P($C3:$AT3);\"Revisar\";\"\")");

            data.Fill(CellRange.Parse("C5"), CellRange.Parse("C5:AT5"));

            Assert.AreEqual("=SI(ABS(M3-PROMEDIO($C3:$AT3))>3*DESVEST.P($C3:$AT3);\"Revisar\";\"\")",
                data.GetInput(CellAddress.Parse("M5")));
            int flagged = 0;
            for (int column = 2; column <= 45; column++)
                if (data.Value(new CellAddress(column, 4)).Text == "Revisar") flagged++;
            Assert.AreEqual("Revisar", data.Value("M5").Text);
            Assert.AreEqual(1, flagged, "Only the one-off order is flagged.");
        }
    }
}
