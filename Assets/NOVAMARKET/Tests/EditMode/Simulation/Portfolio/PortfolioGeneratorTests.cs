using System.Collections.Generic;
using System.Linq;
using NovaMarket.Data;
using NovaMarket.Simulation.Portfolio;
using NUnit.Framework;
using UnityEditor;
using PortfolioModel = NovaMarket.Simulation.Portfolio.Portfolio;

namespace NovaMarket.Tests.Simulation.Portfolio
{
    public class PortfolioGeneratorTests
    {
        private static readonly int[] Seeds = Enumerable.Range(1, 20).ToArray();

        [Test]
        public void Portfolio_Has200SkusWithTheRealOnesUntouched()
        {
            PortfolioModel portfolio = PortfolioTestData.Generate(7);

            Assert.AreEqual(200, portfolio.Skus.Count);
            Assert.AreEqual(14, portfolio.Skus.Count(s => s.IsReal));
            foreach (HistoryRecord record in PortfolioTestData.Real)
            {
                PortfolioSku sku = portfolio.Find(record.Code);
                Assert.IsTrue(sku.IsReal);
                Assert.AreEqual(record.Description, sku.Description);
                CollectionAssert.AreEqual(record.Units, sku.History, record.Code);
            }
        }

        [Test]
        public void SameSeed_SamePortfolio_OtherSeed_OtherPortfolio()
        {
            PortfolioModel a = PortfolioTestData.Generate(11), b = PortfolioTestData.Generate(11), c = PortfolioTestData.Generate(12);

            CollectionAssert.AreEqual(a.Skus.Select(s => s.Description), b.Skus.Select(s => s.Description));
            CollectionAssert.AreEqual(a.Skus.SelectMany(s => s.History), b.Skus.SelectMany(s => s.History));
            CollectionAssert.AreNotEqual(a.Skus.SelectMany(s => s.History), c.Skus.SelectMany(s => s.History));
        }

        [Test]
        public void CodesAndNamesAreUnique_AndFamiliesKeepTheirSizes()
        {
            PortfolioModel portfolio = PortfolioTestData.Generate(3);

            Assert.AreEqual(200, portfolio.Skus.Select(s => s.Code).Distinct().Count());
            Assert.AreEqual(200, portfolio.Skus.Select(s => s.Description).Distinct().Count());
            var sizes = portfolio.Skus.GroupBy(s => s.Family).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(54, sizes["Labiales"]);
            Assert.AreEqual(33, sizes["Perfumes"]);
            Assert.AreEqual(37, sizes["Cremas"]);
            Assert.AreEqual(22, sizes["Sets de regalo"]);
            Assert.AreEqual(16, sizes["Brochas"]);
            Assert.AreEqual(26, sizes["Delineadores"]);
            Assert.AreEqual(12, sizes["Cuidado personal"]);
        }

        [Test]
        public void FewSkusSellMostOfTheUnits()
        {
            double average = Seeds.Average(seed => PortfolioTestData.Generate(seed).TopShare(0.2));

            Assert.That(average, Is.InRange(0.72, 0.88), "Top 20 % of SKUs should sell about 80 % of the units.");
        }

        [Test]
        public void EveryPatternAppears_WithItsShape()
        {
            var synthetic = Seeds.SelectMany(seed => PortfolioTestData.Generate(seed).Skus.Where(s => !s.IsReal)).ToList();
            List<PortfolioSku> Of(DemandPattern pattern) => synthetic.Where(s => s.Pattern == pattern).ToList();

            foreach (DemandPattern pattern in System.Enum.GetValues(typeof(DemandPattern)))
                Assert.Greater(Of(pattern).Count, 40, pattern.ToString());

            double Cv(DemandPattern p) => Of(p).Average(s => SeriesStatistics.VariationCoefficient(s.History));
            Assert.Less(Cv(DemandPattern.Stable), 0.3);
            Assert.Greater(Cv(DemandPattern.Erratic), 0.5);
            Assert.Greater(Of(DemandPattern.Intermittent).Average(s => SeriesStatistics.AverageDemandInterval(s.History)),
                SeriesStatistics.IntermittentAdi);
            Assert.IsTrue(Of(DemandPattern.Launch).All(s => SeriesStatistics.FirstSale(s.History) >= 6));
            Assert.IsTrue(Of(DemandPattern.Discontinued).All(s => SeriesStatistics.LastSale(s.History) < SalesCalendar.Months - 3));
            Assert.IsTrue(Of(DemandPattern.Seasonal).Average(s => SeriesStatistics.SeasonalIndices(s.History).Max()) > 1.4);
        }

        [Test]
        public void ActiveMonthsOfRegularSkusHaveNoFalseZeros()
        {
            foreach (PortfolioSku sku in PortfolioTestData.Generate(5).Skus.Where(s => !s.IsReal && s.Pattern != DemandPattern.Intermittent))
            {
                int first = SeriesStatistics.FirstSale(sku.History), last = SeriesStatistics.LastSale(sku.History);
                for (int t = first; t <= last; t++) Assert.Greater(sku.History[t], 0, $"{sku.Code} month {t}");
            }
        }

        [Test]
        public void MasterData_FollowsTheFamily()
        {
            var skus = Seeds.SelectMany(seed => PortfolioTestData.Generate(seed).Skus).ToList();

            foreach (PortfolioSku sku in skus)
            {
                Assert.AreEqual(0, sku.MinimumOrderUnits % sku.UnitsPerBox, sku.Code);
                Assert.Greater(sku.UnitCost, 0);
                if (sku.IsImported)
                {
                    Assert.IsTrue(LeadTimeTable.IsSea(sku.Mode), sku.Code);
                    Assert.AreNotEqual(EntryPort.Ninguno, sku.Port);
                    Assert.That(sku.LeadTimeDays, Is.InRange(40, 95), sku.Code);
                }
                else
                {
                    Assert.AreEqual(TransportMode.Carretera, sku.Mode);
                    Assert.AreEqual(EntryPort.Ninguno, sku.Port);
                    Assert.That(sku.LeadTimeDays, Is.InRange(9, 36), sku.Code);
                }
            }
            Assert.IsTrue(skus.Where(s => s.Family == "Perfumes").All(s => s.Origin == SupplyOrigin.Europa));
            Assert.IsTrue(skus.Where(s => s.Family == "Sets de regalo" || s.Family == "Brochas").All(s => s.Origin == SupplyOrigin.China));
            Assert.IsTrue(skus.Where(s => s.Family == "Labiales" || s.Family == "Cremas").All(s => s.Origin == SupplyOrigin.Nacional));

            var imports = skus.Where(s => s.IsImported).ToList();
            double buenaventura = imports.Count(s => s.Port == EntryPort.Buenaventura) / (double)imports.Count;
            Assert.That(buenaventura, Is.InRange(0.72, 0.88), "Almost everything enters through Buenaventura (~80 %).");
            StringAssert.StartsWith("Don Hernán", PortfolioTestData.Generate(1).Find("CV10001").Supplier);
        }

        [Test]
        public void LeadTime_AddsThePhasesOfTheImport()
        {
            var random = new System.Random(4);
            for (int i = 0; i < 500; i++)
            {
                int days = LeadTimeTable.DrawDays(random, SupplyOrigin.China, TransportMode.MaritimoFcl, EntryPort.Buenaventura, (7, 30));
                // production 7–30 + pickup 3–7 + transit 25–35 + unloading 2–4 + customs 2–4 + road 1–2 + receiving 1–3
                Assert.That(days, Is.InRange(41, 85));
            }
            Assert.AreEqual((18, 30), LeadTimeTable.Transit(SupplyOrigin.Europa, TransportMode.MaritimoFcl));
        }

        [Test]
        public void CosmeticsSectorAsset_CreatesThePortfolio()
        {
            var sector = AssetDatabase.LoadAssetAtPath<SectorData>("Assets/NOVAMARKET/Data/Sectors/Sector_Cosmetica.asset");
            Assert.IsNotNull(sector.RealHistory, "Sector_Cosmetica needs its real history CSV.");

            PortfolioModel portfolio = sector.CreatePortfolio();

            Assert.AreEqual(200, portfolio.Skus.Count);
            Assert.AreEqual(14, portfolio.Skus.Count(s => s.IsReal));
        }
    }
}
