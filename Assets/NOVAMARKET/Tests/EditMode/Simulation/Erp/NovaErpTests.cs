using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using NovaMarket.Tests.Simulation.Portfolio;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Erp
{
    public class NovaErpTests
    {
        private static readonly string[] Codes = { "CV10009", "CV10001", "CV10021" };

        private NovaErp erp;
        private Workbook book;

        [SetUp]
        public void SetUp()
        {
            erp = new NovaErp(new ErpDataSet(PortfolioTestData.Generate(1), seed: 9));
            book = new Workbook();
        }

        [Test]
        public void World1_OpensOnlyBillingAndTheMaterialMaster()
        {
            CollectionAssert.AreEquivalent(new[] { ErpScreens.Billing, ErpScreens.MaterialMaster },
                erp.Screens.Where(s => erp.IsAvailable(s.Id)).Select(s => s.Id));
            Assert.AreEqual("VF05 / análisis de ventas", ErpScreens.Find(ErpScreens.Billing).SapEquivalent);
            Assert.AreEqual("MD04", ErpScreens.Find("necesidades").SapEquivalent);
            Assert.IsTrue(new NovaErp(erp.Data, world: 3).IsAvailable("seguimiento-importaciones"));
        }

        [Test]
        public void Billing_HasOneRowPerSkuAndOneColumnPerMonth_PlusTheProjectionTrap()
        {
            ErpTable table = erp.Billing(new BillingQuery { Codes = Codes });

            Assert.AreEqual(3, table.Rows.Count);
            Assert.AreEqual(2 + 44 + 1 + 1, table.Headers.Count);
            Assert.AreEqual("ene-23", table.Headers[2]);
            Assert.AreEqual("ago-26", table.Headers[45]);
            Assert.AreEqual("sep-26 (proy.)", table.Headers[46]);
            Assert.AreEqual("UM", table.Headers[47]);

            int[] units = PortfolioTestData.Record("CV10009").Units;
            CellValue[] labial = table.Rows[0];
            Assert.AreEqual(units[0], labial[2].Number);
            Assert.AreEqual(System.Math.Round(units.Skip(38).Average()), labial[46].Number, "Moving average of 6 months.");
            Assert.AreEqual("UN", labial[47].Text);
            StringAssert.Contains("No es venta", table.Notes[0]);
        }

        [Test]
        public void Billing_WithoutProjectionOrWithAnEarlierEnd_HasOnlySales()
        {
            ErpTable clean = erp.Billing(new BillingQuery { Codes = Codes, IncludeProjection = false });
            ErpTable earlier = erp.Billing(new BillingQuery { Codes = Codes, FromMonth = 12, ToMonth = 35 });

            Assert.IsFalse(clean.Headers.Any(h => h.EndsWith(NovaErp.ProjectionSuffix)));
            Assert.AreEqual(2 + 24 + 1, earlier.Headers.Count);
            Assert.AreEqual("ene-24", earlier.Headers[2]);
            CollectionAssert.IsEmpty(clean.Notes);
        }

        [Test]
        public void Billing_ListsUnknownMaterials()
        {
            ErpTable table = erp.Billing(new BillingQuery { Codes = new[] { "CV10009", "XX1" }, IncludeProjection = false });

            Assert.AreEqual(1, table.Rows.Count);
            StringAssert.Contains("XX1", table.Notes.Single());
        }

        [Test]
        public void Download_PutsTheMonthsInColumnsCToAT_AsInTheAtlas()
        {
            int events = 0;
            erp.Downloaded += (_, _) => events++;

            Worksheet sheet = erp.Download(erp.Billing(new BillingQuery { Codes = Codes }), book);
            Worksheet again = erp.Download(erp.Billing(new BillingQuery { Codes = Codes }), book);

            Assert.AreEqual("Facturación", sheet.Name);
            Assert.AreEqual("Facturación (2)", again.Name);
            Assert.AreEqual(2, events);
            Assert.AreEqual(2, erp.Downloads.Count);
            StringAssert.StartsWith("Facturación por material y mes", sheet.Value("A1").Text);
            Assert.AreEqual("Material", sheet.Value("A2").Text);
            Assert.AreEqual("ene-23", sheet.Value("C2").Text);
            Assert.AreEqual("ago-26", sheet.Value("AT2").Text);
            Assert.AreEqual("CV10009", sheet.Value("A3").Text);
            Assert.AreEqual(7818, sheet.Value("C3").Number);
            Assert.AreEqual("7.818", sheet.Display("C3"), "Months show with thousands separator.");

            sheet.SetInput("AW3", "=SUMA(AI3:AT3)");
            Assert.AreEqual(SeriesStatistics.LastMonthsTotal(PortfolioTestData.Record("CV10009").Units), sheet.Value("AW3").Number);
            StringAssert.Contains("No es venta", sheet.Value("A7").Text, "The note goes under the table.");
        }

        [Test]
        public void MaterialMaster_ShowsTheSupplyData()
        {
            ErpTable table = erp.MaterialMaster(new[] { "CV10001" });
            PortfolioSku sku = erp.Data.Portfolio.Find("CV10001");

            CellValue[] row = table.Rows.Single();
            Assert.AreEqual("Europa", row[table.ColumnOf("Origen")].Text);
            Assert.AreEqual(sku.UnitsPerBox, row[table.ColumnOf("UN por caja")].Number);
            Assert.AreEqual(sku.LeadTimeDays, row[table.ColumnOf("LT (días)")].Number);
            StringAssert.StartsWith("Don Hernán", row[table.ColumnOf("Proveedor")].Text);
        }

        [Test]
        public void BillingDetail_AddsUpToTheMonth()
        {
            int[] units = PortfolioTestData.Record("CV10021").Units;
            for (int month = 0; month < 44; month++)
            {
                ErpTable detail = erp.BillingDetail("CV10021", month);
                Assert.AreEqual(units[month], detail.Rows.Sum(r => r[2].Number), $"month {month}");
                Assert.That(detail.Rows.Count, Is.InRange(3, 5));
            }
        }

        [Test]
        public void LockedScreens_CannotBeQueried()
        {
            var locked = new NovaErp(erp.Data, world: 0);
            Assert.Throws<System.InvalidOperationException>(() => locked.Billing(new BillingQuery { Codes = Codes }));
        }
    }
}
