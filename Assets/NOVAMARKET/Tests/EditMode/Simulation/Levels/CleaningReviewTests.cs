using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    /// <summary>The detailed feedback of level 1.2: what happened with each problem, where and why.</summary>
    public class CleaningReviewTests
    {
        private CleanDataLevel level;

        [SetUp]
        public void SetUp() => level = new CleanDataLevel(LevelPlayers.Portfolio, seed: 12);

        private DataIssue Issue(DataIssueKind kind, bool real = false) =>
            level.Data.Issues.First(i => i.Kind == kind && (kind != DataIssueKind.FalseZero || i.IsReal == real));

        private IssueReview ReviewOf(DataIssue issue) => level.Review().Issues.First(r => r.Issue == issue);

        private CellAddress Cell(string code, int month) =>
            new CellAddress(HistoryTable.MonthColumn(month), HistoryTable.FindRow(level.CleanSheet, code));

        [Test]
        public void SolvedLevel_HasEveryRowCorrect_AndNothingToHighlight()
        {
            LevelPlayers.SolveClean(level);

            CleaningReview review = level.Review();
            Assert.IsTrue(review.IsClean);
            Assert.IsTrue(review.Issues.All(i => i.Outcome == IssueOutcome.Correct));
            Assert.IsEmpty(level.ReviewNotes());
            Assert.IsNull(level.UncorrectedWarning());
            Assert.AreEqual(5, level.Deliver().Cleaning.Issues.Count);
        }

        [Test]
        public void MarkedButNotCorrected_SaysSoInPlainWords_AndAndresWarnsBeforeDelivering()
        {
            LevelPlayers.Download(level);
            DataIssue zero = Issue(DataIssueKind.FalseZero, real: true);
            level.Mark(new IssueMark(zero.Code, zero.Months[0], zero.Kind, CorrectionAction.Impute));

            IssueReview row = ReviewOf(zero);
            Assert.AreEqual(IssueOutcome.NotCorrected, row.Outcome);
            StringAssert.Contains("Marcaste el problema de CV10004 jun-24, pero no escribiste el valor corregido en «Serie limpia»", row.Message);
            Assert.AreEqual("0", row.PlayerValue);
            StringAssert.StartsWith("4.695", row.ExpectedValue);

            string warning = level.UncorrectedWarning();
            StringAssert.Contains("Marcaste un problema, pero todavía no escribiste el valor corregido", warning);
            StringAssert.Contains("CV10004 jun-24 (sigue en 0)", warning);

            level.CleanSheet.SetNumber(Cell(zero.Code, zero.Months[0]), 4695);
            Assert.IsNull(level.UncorrectedWarning());
            Assert.AreEqual(IssueOutcome.Correct, ReviewOf(zero).Outcome);
        }

        [Test]
        public void WrongAction_NamesTheMonthsTheChoiceAndTheRightOne()
        {
            LevelPlayers.Download(level);
            DataIssue launch = Issue(DataIssueKind.PreLaunch);
            level.Mark(new IssueMark(launch.Code, launch.Months[0], launch.Kind, CorrectionAction.Impute));
            foreach (int month in launch.Months) level.CleanSheet.SetInput(Cell(launch.Code, month), string.Empty);

            IssueReview row = ReviewOf(launch);
            Assert.AreEqual(IssueOutcome.WrongAction, row.Outcome);
            Assert.AreEqual("CV10023 ene-23 a jun-23", row.Where);
            Assert.AreEqual("En CV10023 ene-23 a jun-23 escogiste «Imputar», pero son meses antes del lanzamiento: " +
                            "la acción correcta es «Excluir» (dejar las celdas vacías).", row.Message);
        }

        [Test]
        public void WrongValue_IsHighlighted_WithTheExpectedValue()
        {
            LevelPlayers.Download(level);
            DataIssue zero = Issue(DataIssueKind.FalseZero, real: true);
            level.Mark(new IssueMark(zero.Code, zero.Months[0], zero.Kind, CorrectionAction.Impute));
            level.CleanSheet.SetNumber(Cell(zero.Code, zero.Months[0]), 3302); // the neighbours' average

            IssueReview row = ReviewOf(zero);
            Assert.AreEqual(IssueOutcome.WrongValue, row.Outcome);
            StringAssert.Contains("En CV10004 jun-24 dejaste 3.302, y lo correcto es 4.695", row.Message);
            StringAssert.Contains("promedio de junio en los otros años", row.Message);
            Assert.IsTrue(level.ReviewNotes().ContainsKey((zero.Code, zero.Months[0])));
        }

        [Test]
        public void GoodMonthsMarkedByMistake_AreListedWithWhyTheyWereRight()
        {
            LevelPlayers.Download(level);
            int december = 11; // dic-23 of CV10004: the season, not an outlier
            level.Mark(new IssueMark("CV10004", december, DataIssueKind.OneOffOrder, CorrectionAction.Impute));

            GoodDataReview good = level.Review().GoodData.Single();
            Assert.AreEqual("CV10004 dic-23", good.Where);
            Assert.IsTrue(good.Marked);
            StringAssert.Contains("es venta real, no un error", good.Message);
            StringAssert.Contains("es temporada", good.Message);
            StringAssert.Contains("el pronóstico aprendería algo que no pasó", good.Message);
            Assert.IsTrue(level.ReviewNotes().ContainsKey(("CV10004", december)));

            LevelResult result = level.Deliver();
            StringAssert.Contains("Marcaste como problema una celda que estaba bien: CV10004 dic-23 es venta real",
                result.Criteria[1].Detail);
        }

        [Test]
        public void UnmarkedProblems_AreNamedInTheFirstStar()
        {
            LevelPlayers.Download(level);

            LevelResult result = level.Deliver();

            Assert.IsTrue(result.Cleaning.Issues.All(i => i.Outcome == IssueOutcome.NotMarked));
            StringAssert.Contains("Encontraste 0 de 5 problemas. Te faltó ", result.Criteria[0].Detail);
            StringAssert.Contains("CV10004 jun-24: cero que no es real", result.Criteria[0].Detail);
            Assert.IsFalse(result.Criteria.Any(c => c.Detail.Contains("alguna acción")));
        }
    }
}
