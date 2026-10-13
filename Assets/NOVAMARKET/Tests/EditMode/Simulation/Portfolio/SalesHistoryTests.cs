using System;
using System.Linq;
using NovaMarket.Simulation.Portfolio;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Portfolio
{
    public class SalesHistoryTests
    {
        [Test]
        public void AtlasFile_HasTheFourteenRealSkusWith44Months()
        {
            Assert.AreEqual(14, PortfolioTestData.Real.Count);
            Assert.IsTrue(PortfolioTestData.Real.All(r => r.Units.Length == SalesCalendar.Months));
            HistoryRecord perfume = PortfolioTestData.Record("CV10001");
            Assert.AreEqual("Perfume hombre Kaoba Malizmor 75 ml", perfume.Description);
            Assert.AreEqual("Perfumes", perfume.Family);
            Assert.AreEqual(2227, perfume.Units[0]);
            Assert.AreEqual(1536, perfume.Units[1]);
        }

        [Test]
        public void AtlasFile_HasTheIsolatedZeroAlreadyImputed()
        {
            // Atlas, step p2: CV10004 jun-24 came as 0 and was imputed with the other Junes → 4.695.
            int[] units = PortfolioTestData.Record("CV10004").Units;
            int june24 = SalesCalendar.IndexOfKey("2024-06");
            Assert.AreEqual(4695, units[june24]);
            Assert.AreEqual(4695, Math.Round((units[5] + units[29] + units[41]) / 3.0));
        }

        [Test]
        public void Parse_RejectsBadFiles()
        {
            Assert.Throws<FormatException>(() => SalesHistoryCsv.Parse(""));
            Assert.Throws<FormatException>(() => SalesHistoryCsv.Parse("sku;descripcion;familia;2023-01\n"));
            Assert.Throws<FormatException>(() => SalesHistoryCsv.Parse("codigo;descripcion;familia;2023-02\n"));
            Assert.Throws<FormatException>(() => SalesHistoryCsv.Parse("codigo;descripcion;familia;2023-01\nX;Y;Z;-4\n"));
            Assert.Throws<FormatException>(() => SalesHistoryCsv.Parse("codigo;descripcion;familia;2023-01\nX;Y;Z\n"));
            Assert.AreEqual(1, SalesHistoryCsv.Parse("﻿codigo;descripcion;familia;2023-01\r\nX;Labial 3,5 g;Z;7\r\n").Count);
        }

        [Test]
        public void Calendar_UsesTheAtlasLabels()
        {
            Assert.AreEqual("ene-23", SalesCalendar.Label(0));
            Assert.AreEqual("jun-24", SalesCalendar.Label(17));
            Assert.AreEqual("ago-26", SalesCalendar.Label(43));
            Assert.AreEqual("2026-08", SalesCalendar.Key(43));
            Assert.AreEqual(17, SalesCalendar.IndexOfKey("2024-06"));
            Assert.AreEqual(-1, SalesCalendar.IndexOfKey("2024-13"));
        }

        [Test]
        public void SeasonalIndices_MatchTheAtlasExample()
        {
            // Atlas, step p3: "CV10001: junio 1,48 y diciembre 1,55".
            double[] indices = SeriesStatistics.SeasonalIndices(PortfolioTestData.Record("CV10001").Units);

            Assert.AreEqual(1.48, indices[5], 0.005);
            Assert.AreEqual(1.55, indices[11], 0.005);
        }

        [Test]
        public void Statistics_OfASmallSeries()
        {
            int[] series = { 0, 0, 4, 0, 8, 0, 0, 12, 0, 0 };

            Assert.AreEqual(2, SeriesStatistics.FirstSale(series));
            Assert.AreEqual(7, SeriesStatistics.LastSale(series));
            Assert.AreEqual(2.4, SeriesStatistics.Mean(series), 1e-9);
            Assert.AreEqual(2.0, SeriesStatistics.AverageDemandInterval(series), 1e-9); // 6 months, 3 sales
            Assert.AreEqual(Math.Sqrt(2.0 / 3.0 * 16.0), SeriesStatistics.StdDev(new[] { 4, 8, 12 }), 1e-9); // DESVEST.P
            Assert.AreEqual(20, SeriesStatistics.LastMonthsTotal(series, 6));
        }

        [Test]
        public void Classify_ReadsTheRealLaunches()
        {
            Assert.AreEqual(DemandPattern.Launch, SeriesStatistics.Classify(PortfolioTestData.Record("CV10019").Units));
            Assert.AreEqual(DemandPattern.Launch, SeriesStatistics.Classify(PortfolioTestData.Record("CV10023").Units));
            Assert.AreEqual(DemandPattern.Seasonal, SeriesStatistics.Classify(PortfolioTestData.Record("CV10001").Units));
            Assert.AreEqual(DemandPattern.Stable, SeriesStatistics.Classify(PortfolioTestData.Record("CV10009").Units));
        }
    }
}
