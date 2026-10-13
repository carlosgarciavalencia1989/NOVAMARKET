using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Tests.Simulation.Portfolio;
using NUnit.Framework;
using PortfolioModel = NovaMarket.Simulation.Portfolio.Portfolio;

namespace NovaMarket.Tests.Simulation.Erp
{
    public class DataQualityInjectorTests
    {
        private PortfolioModel portfolio;

        [SetUp]
        public void SetUp() => portfolio = PortfolioTestData.Generate(1);

        private ErpDataSet Inject(int seed, params IssueRequest[] requests) => DataQualityInjector.Inject(portfolio, requests, seed);

        [Test]
        public void AtlasZero_IsCV10004June24_AndOnlyTheSameMonthAnswerCounts()
        {
            ErpDataSet data = Inject(3, new IssueRequest(DataIssueKind.FalseZero, "CV10004") { PreferAtlasCase = true });

            DataIssue issue = data.Issues.Single();
            Assert.IsTrue(issue.IsReal);
            CollectionAssert.AreEqual(new[] { 17 }, issue.Months);
            Assert.AreEqual(0, data.Billed("CV10004")[17]);
            Assert.AreEqual(1, issue.AcceptedValues.Count, "June is a strong month: the neighbours' average is wrong.");
            Assert.AreEqual(4694.67, issue.AcceptedValues[0][0].Value, 0.005);
            Assert.AreEqual(CorrectionAction.Impute, issue.ExpectedAction);
            Assert.AreEqual(4695, portfolio.Find("CV10004").History[17], "The portfolio keeps the clean history.");
        }

        [Test]
        public void FalseZero_InAStableSku_AcceptsBothImputations()
        {
            ErpDataSet data = Inject(5, new IssueRequest(DataIssueKind.FalseZero, "CV10009"));

            DataIssue issue = data.Issues.Single();
            int month = issue.Months[0];
            int[] history = PortfolioTestData.Record("CV10009").Units;
            Assert.That(month, Is.InRange(12, 31));
            Assert.AreEqual(0, data.Billed("CV10009")[month]);
            Assert.AreEqual(2, issue.AcceptedValues.Count);
            Assert.AreEqual(DataQualityInjector.SameMonthAverage(history, month), issue.AcceptedValues[0][0].Value, 1e-9);
            Assert.AreEqual((history[month - 1] + history[month + 1]) / 2.0, issue.AcceptedValues[1][0].Value, 1e-9);
        }

        [Test]
        public void OneOffOrder_ShowsInTheBillingDetail()
        {
            ErpDataSet data = Inject(7, new IssueRequest(DataIssueKind.OneOffOrder, "CV10017"));
            var erp = new NovaErp(data);

            DataIssue issue = data.Issues.Single();
            int month = issue.Months[0];
            OneOffOrder order = data.OrderAt("CV10017", month);
            int clean = portfolio.Find("CV10017").History[month];
            Assert.AreEqual(clean + order.Units, data.Billed("CV10017")[month]);
            Assert.That(order.Units, Is.InRange(clean * 2.4, clean * 4.1));
            Assert.AreEqual(clean, issue.AcceptedValues[0][0].Value);

            var detail = erp.BillingDetail("CV10017", month);
            Assert.AreEqual(data.Billed("CV10017")[month], detail.Rows.Sum(r => r[2].Number));
            Assert.IsTrue(detail.Rows.Any(r => r[1].Text == order.Customer && r[2].Number == order.Units));
        }

        [Test]
        public void PreLaunch_IsTheRealLaunchOfCV10023_AndIsExcluded()
        {
            ErpDataSet data = Inject(1, new IssueRequest(DataIssueKind.PreLaunch, "CV10009", "CV10023"));

            DataIssue issue = data.Issues.Single();
            Assert.AreEqual("CV10023", issue.Code, "CV10009 was never 'before its launch'.");
            Assert.IsTrue(issue.IsReal);
            CollectionAssert.AreEqual(Enumerable.Range(0, 6), issue.Months);
            Assert.IsTrue(issue.AcceptedValues[0].All(v => v == null));
            Assert.AreEqual(CorrectionAction.Exclude, issue.ExpectedAction);
        }

        [Test]
        public void UnitsInBoxes_ConvertBackWithTheUnitsPerBox()
        {
            ErpDataSet data = Inject(2, new IssueRequest(DataIssueKind.UnitsInBoxes, "CV10021"));

            PortfolioSku sku = portfolio.Find("CV10021");
            DataIssue issue = data.Issues.Single();
            Assert.AreEqual(ErpDataSet.Boxes, data.SalesUnit("CV10021"));
            Assert.AreEqual(44, issue.Months.Count);
            for (int m = 0; m < 44; m++)
            {
                Assert.AreEqual(System.Math.Round(sku.History[m] / (double)sku.UnitsPerBox), data.Billed("CV10021")[m]);
                Assert.AreEqual(data.Billed("CV10021")[m] * sku.UnitsPerBox, issue.AcceptedValues[0][m].Value);
                Assert.AreEqual(sku.History[m], issue.AcceptedValues[0][m].Value, sku.UnitsPerBox / 2.0);
            }
            Assert.AreEqual(CorrectionAction.Convert, issue.ExpectedAction);
        }

        [Test]
        public void EachSkuGetsOneProblem_AndTheSameSeedGivesTheSameData()
        {
            IssueRequest[] Plan() => new[]
            {
                new IssueRequest(DataIssueKind.FalseZero, "CV10009", "CV10017", "CV10015"),
                new IssueRequest(DataIssueKind.OneOffOrder, "CV10009", "CV10017", "CV10015"),
                new IssueRequest(DataIssueKind.UnitsInBoxes, "CV10009", "CV10017", "CV10015")
            };

            ErpDataSet a = Inject(11, Plan()), b = Inject(11, Plan());

            Assert.AreEqual(3, a.Issues.Select(i => i.Code).Distinct().Count());
            CollectionAssert.AreEqual(a.Issues.Select(i => i.Code + i.Months[0]), b.Issues.Select(i => i.Code + i.Months[0]));
            var months = Enumerable.Range(1, 30).Select(seed => Inject(seed, Plan()).Issues[0].Months[0]).Distinct().Count();
            Assert.Greater(months, 5, "Seeds move the problems.");
        }

        [Test]
        public void ImpossibleRequests_AreRejected()
        {
            Assert.Throws<System.InvalidOperationException>(() => Inject(1, new IssueRequest(DataIssueKind.PreLaunch, "CV10009")));
            Assert.Throws<System.InvalidOperationException>(() => Inject(1,
                new IssueRequest(DataIssueKind.FalseZero, "CV10009"), new IssueRequest(DataIssueKind.OneOffOrder, "CV10009")));
        }

        [Test]
        public void Explanations_NameTheSkuAndTheMonth()
        {
            ErpDataSet data = Inject(4,
                new IssueRequest(DataIssueKind.FalseZero, "CV10004") { PreferAtlasCase = true },
                new IssueRequest(DataIssueKind.OneOffOrder, "CV10009"));

            StringAssert.Contains("CV10004 jun-24", data.Issues[0].Explanation);
            StringAssert.Contains("mismo mes", data.Issues[0].AtlasStep);
            StringAssert.Contains(SalesCalendar.Label(data.Issues[1].Months[0]), data.Issues[1].Explanation);
            Assert.AreSame(data.Issues[1], data.IssueAt("CV10009", data.Issues[1].Months[0]));
        }
    }
}
