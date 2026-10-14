using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    public class CleanDataLevelTests
    {
        private CleanDataLevel level;

        [SetUp]
        public void SetUp() => level = new CleanDataLevel(LevelPlayers.Portfolio, seed: 12);

        private DataIssue Issue(DataIssueKind kind, bool real = false) =>
            level.Data.Issues.First(i => i.Kind == kind && (kind != DataIssueKind.FalseZero || i.IsReal == real));

        [Test]
        public void Level_HasTheFiveProblems_IncludingTheRealOnes()
        {
            Assert.AreEqual(5, level.Data.Issues.Count);
            Assert.AreEqual(5, level.RequestedCodes.Count);
            DataIssue atlasZero = Issue(DataIssueKind.FalseZero, real: true);
            Assert.AreEqual("CV10004", atlasZero.Code);
            Assert.AreEqual("jun-24", SalesCalendar.Label(atlasZero.Months[0]));
            Assert.AreEqual("CV10023", Issue(DataIssueKind.PreLaunch).Code);
            Assert.IsTrue(Issue(DataIssueKind.PreLaunch).IsReal);
            Assert.AreEqual(ScaffoldMode.EmptyBoxes, level.Info.Mode);
        }

        [Test]
        public void Download_CopiesTheMonthsToTheCleanSheet_WithoutTheProjection()
        {
            Worksheet raw = LevelPlayers.Download(level, projection: true);
            Worksheet clean = level.CleanSheet;

            Assert.IsNotNull(clean);
            Assert.AreEqual(CleanDataLevel.CleanSheetName, clean.Name);
            Assert.AreEqual("ago-26", clean.Value("AT2").Text);
            Assert.IsTrue(clean.Value("AU3").IsEmpty, "No projection in the clean copy.");
            for (int row = 2; row < 7; row++)
                for (int column = 0; column < 46; column++)
                    Assert.AreEqual(raw.Value(new CellAddress(column, row)), clean.Value(new CellAddress(column, row)));

            LevelPlayers.Download(level);
            Assert.AreEqual(1, level.Book.Sheets.Count(s => s.Name.StartsWith(CleanDataLevel.CleanSheetName)), "Downloading again keeps the work.");
        }

        [Test]
        public void SolvedLevel_GivesThreeStars_AndTheBadgeWithFormulas()
        {
            LevelPlayers.SolveClean(level, formulas: true);

            LevelResult result = level.Deliver();
            Assert.AreEqual(3, result.Stars, string.Join(" | ", result.Criteria.Select(c => c.Detail)));
            Assert.IsTrue(result.ExcelBadge);
            Assert.AreEqual(DialogueKeys.Success, result.Debrief[0].Key);
            Assert.IsEmpty(result.DamagedSkus.ToList());
            Assert.AreEqual(5 + 2, result.Lessons.Count);
        }

        [Test]
        public void SolvedWithTypedNumbers_ThreeStarsWithoutTheBadge()
        {
            LevelPlayers.SolveClean(level);

            LevelResult result = level.Deliver();
            Assert.AreEqual(3, result.Stars);
            Assert.IsFalse(result.ExcelBadge);
        }

        [Test]
        public void DoingNothing_FailsAndAndresShowsTheDamage()
        {
            LevelPlayers.Download(level);

            LevelResult result = level.Deliver();
            Assert.IsFalse(result.Passed);
            Assert.AreEqual(0, result.Stars);
            Assert.AreEqual(DialogueKeys.MissingMany, result.Debrief[0].Key);
            Assert.AreEqual("5", result.Debrief[0].Args["missing"]);
            string boxes = Issue(DataIssueKind.UnitsInBoxes).Code;
            SkuImpact impact = result.Impact.Single(i => i.Code == boxes);
            Assert.Less(impact.PlayerLevel, impact.CleanLevel / 10, "A row in boxes misses almost all the units.");
            Assert.IsTrue(impact.LevelIsWorse);
            Assert.AreEqual(5, result.DamagedSkus.Count(), "Every problem distorts what the model learns.");
            Assert.IsTrue(result.Debrief.Any(l => l.Key == DialogueKeys.ImpactLevelLow && l.Args["code"] == boxes));
            SkuImpact zero = result.Impact.Single(i => i.Code == "CV10004");
            Assert.AreEqual(5, zero.Month, "The zero hurts June.");
            Assert.Less(zero.PlayerIndex, zero.CleanIndex);
        }

        [Test]
        public void ImputingASeasonalZeroWithTheNeighbours_IsNotAGoodCorrection()
        {
            LevelPlayers.SolveClean(level);
            DataIssue zero = Issue(DataIssueKind.FalseZero, real: true);
            int row = HistoryTable.FindRow(level.CleanSheet, zero.Code);
            level.CleanSheet.SetNumber(new CellAddress(HistoryTable.MonthColumn(17), row), 3302); // Atlas: neighbours → 3.302

            LevelResult result = level.Deliver();
            Assert.IsTrue(result.Passed);
            Assert.IsFalse(result.Criteria[1].Achieved);
            Assert.AreEqual(2, result.Stars);
            Assert.IsTrue(result.Impact.Single(i => i.Code == zero.Code).Damage > 0);
        }

        [Test]
        public void TouchingTheDecemberPeak_CostsTheSecondStar()
        {
            LevelPlayers.SolveClean(level);
            int december24 = SalesCalendar.IndexOfKey("2024-12");
            int row = HistoryTable.FindRow(level.CleanSheet, "CV10004");
            level.Mark(new IssueMark("CV10004", december24, DataIssueKind.OneOffOrder, CorrectionAction.Impute));
            level.CleanSheet.SetNumber(new CellAddress(HistoryTable.MonthColumn(december24), row), 4000);

            LevelResult result = level.Deliver();
            Assert.AreEqual(2, result.Stars);
            Assert.AreEqual(1, level.WrongMarks().Count());
            Assert.AreEqual(1, level.ChangedGoodData().Count());
            Assert.IsTrue(result.Debrief.Any(l => l.Key == DialogueKeys.ChangedGoodData));
        }

        [Test]
        public void AtlasOutlierRule_FlagsTheDecemberPeakToo_ThatIsTheTrap()
        {
            Worksheet raw = LevelPlayers.Download(level);
            int row = HistoryTable.FindRow(raw, "CV10004") + 1;
            raw.SetInput("C20", $"=SI(ABS(C{row}-PROMEDIO($C{row}:$AT{row}))>3*DESVEST.P($C{row}:$AT{row});\"Revisar\";\"\")");
            raw.Fill(CellRange.Parse("C20"), CellRange.Parse("C20:AT20"));

            string december24 = CellAddress.ColumnName(HistoryTable.MonthColumn(SalesCalendar.IndexOfKey("2024-12")));
            Assert.AreEqual("Revisar", raw.Value($"{december24}20").Text, "dic-24 (10.792) passes 3 deviations, but it is the season.");
        }

        [Test]
        public void WrongAction_CostsTheSecondStar()
        {
            LevelPlayers.SolveClean(level);
            DataIssue launch = Issue(DataIssueKind.PreLaunch);
            level.Mark(new IssueMark(launch.Code, launch.Months[0], launch.Kind, CorrectionAction.Impute));

            LevelResult result = level.Deliver();
            Assert.IsTrue(result.Passed);
            Assert.IsFalse(result.Criteria[1].Achieved);
            StringAssert.Contains("acción", result.Criteria[1].Detail);
        }

        [Test]
        public void Cards_ComputeTheAtlasExample_AndPointAtTheWrongBox()
        {
            LevelPlayers.Download(level);
            FormulaCard card = level.CardsFor("CV10004", 17, DataIssueKind.FalseZero)[0];

            Assert.AreEqual("D'(jun-24) = PROMEDIO(jun-23; jun-25; jun-26)", card.AtlasFormula);
            Assert.IsTrue(card.Prefilled.All(p => !p), "Empty boxes mode.");
            CardCheck wrong = card.Check(new double?[] { 6693, 4093, 3000 });
            CollectionAssert.AreEqual(new[] { true, true, false }, wrong.BoxesOk);
            Assert.IsNull(wrong.Result);
            StringAssert.Contains("jun-26", card.Boxes[2].Hint);
            CardCheck right = card.Check(new double?[] { 6693, 4093, 3298 });
            Assert.AreEqual(4694.67, right.Result.Value, 0.005);

            var guided = CleaningCards.SameMonth(level.Data, "CV10004", 17, ScaffoldMode.Guided);
            CollectionAssert.AreEqual(new[] { true, true, false }, guided.Prefilled);
            Assert.AreEqual(4694.67, guided.Check(new double?[] { null, null, 3298 }).Result.Value, 0.005);
        }

        [Test]
        public void Cards_DoNotRevealWhetherACellHasAProblem()
        {
            LevelPlayers.Download(level);
            string code = level.RequestedCodes.First(c => c != "CV10023");

            Assert.AreEqual(2, level.CardsFor(code, 20, DataIssueKind.FalseZero).Count);
            Assert.AreEqual(3, level.CardsFor(code, 20, DataIssueKind.OneOffOrder).Count);
            Assert.AreEqual(0, level.CardsFor(code, 20, DataIssueKind.OneOffOrder)[1].Boxes[1].Expected,
                "A month without a one-off order has 0 in the order box.");
        }

        [Test]
        public void ConvertCard_UsesTheMaterialMaster()
        {
            DataIssue boxes = Issue(DataIssueKind.UnitsInBoxes);
            FormulaCard card = CleaningCards.Convert(level.Data, boxes.Code, 30, ScaffoldMode.EmptyBoxes);

            Assert.AreEqual(level.Data.Billed(boxes.Code)[30] * level.Data.Portfolio.Find(boxes.Code).UnitsPerBox, card.Expected);
        }

        [Test]
        public void AtlasHelp_ExplainsTheProblemAndCostsTheThirdStar()
        {
            AtlasAnswer answer = level.AskAtlas(CleanDataLevel.TopicOf(DataIssueKind.OneOffOrder));
            LevelPlayers.SolveClean(level);

            DataIssue order = Issue(DataIssueKind.OneOffOrder);
            Assert.IsTrue(answer.Steps.Any(s => s.Contains(order.Code)));
            Assert.AreEqual(2, level.Deliver().Stars);
        }

        [Test]
        public void Marks_CanBeChangedAndRemoved()
        {
            int changes = 0;
            level.MarksChanged += () => changes++;
            level.Mark(new IssueMark("CV10004", 17, DataIssueKind.OneOffOrder, CorrectionAction.Impute));
            level.Mark(new IssueMark("CV10004", 17, DataIssueKind.FalseZero, CorrectionAction.Impute));

            Assert.AreEqual(1, level.Marks.Count);
            Assert.AreEqual(DataIssueKind.FalseZero, level.MarkAt("CV10004", 17).Kind);
            Assert.IsTrue(level.Unmark("CV10004", 17));
            Assert.IsFalse(level.Unmark("CV10004", 17));
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void World1_ListsSevenLevels_OnlyTheFirstTwoBuilt()
        {
            Assert.AreEqual(7, ForecastWorld.Levels.Count);
            CollectionAssert.AreEqual(new[] { "1.1", "1.2" }, ForecastWorld.Levels.Where(l => l.IsBuilt).Select(l => l.Id));
            Assert.AreNotEqual(ForecastWorld.GatherHistory.Gem.Shape, ForecastWorld.CleanData.Gem.Shape);
            Assert.AreNotEqual(ForecastWorld.GatherHistory.Gem.ColorHex, ForecastWorld.CleanData.Gem.ColorHex);
        }

        [Test]
        public void Tolerance_IsTwoPercent()
        {
            Assert.IsTrue(Tolerance.Matches(4695, 4694.67));
            Assert.IsTrue(Tolerance.Matches(102, 100));
            Assert.IsFalse(Tolerance.Matches(102.1, 100));
            Assert.IsTrue(Tolerance.Matches(0.4, 0), "Near zero an absolute margin applies.");
        }

        [Test]
        public void Cells_AreLocatedBySkuAndMonth_InTheRawAndTheCleanTabs()
        {
            Worksheet raw = LevelPlayers.Download(level, projection: true);
            DataIssue zero = Issue(DataIssueKind.FalseZero, real: true);
            int row = HistoryTable.FindRow(level.CleanSheet, zero.Code);
            var cell = new CellAddress(HistoryTable.MonthColumn(zero.Months[0]), row);

            Assert.IsTrue(CleanDataLevel.TryLocate(level.CleanSheet, cell, out string code, out int month));
            Assert.AreEqual((zero.Code, zero.Months[0]), (code, month));
            Assert.IsTrue(CleanDataLevel.TryLocate(raw, new CellAddress(cell.Column, HistoryTable.FindRow(raw, zero.Code)), out code, out month));
            Assert.AreEqual(zero.Months[0], month);
            Assert.IsFalse(CleanDataLevel.TryLocate(raw, CellAddress.Parse("A1"), out _, out _), "Title row.");
            Assert.IsFalse(CleanDataLevel.TryLocate(raw, new CellAddress(1, row), out _, out _), "Description column.");
            Assert.IsFalse(CleanDataLevel.TryLocate(raw, CellAddress.Parse("AU3"), out _, out _), "Projection column.");

            Assert.IsTrue(level.WriteClean(zero.Code, zero.Months[0], 4695));
            Assert.AreEqual(4695, level.CleanSheet.Value(cell).Number);
            Assert.IsFalse(level.WriteClean("NOEXISTE", 3, 1));
        }

        [Test]
        public void CleaningImpact_ShowsWhatTheModelWouldLearn()
        {
            int[] history = LevelPlayers.Portfolio.Find("CV10001").History.ToArray();
            double?[] clean = CleaningImpact.WithoutPreLaunch(history);

            Assert.AreEqual(0, CleaningImpact.Compare("CV10001", clean, clean).Damage, 1e-12);
            (double level, double[] indices) = CleaningImpact.Learn(clean);
            Assert.AreEqual(1.48, indices[5], 0.005, "Atlas: CV10001 June index 1,48.");
            Assert.AreEqual(history.Average(), level, 1e-9);

            double?[] zero = (double?[])clean.Clone();
            zero[17] = 0;
            SkuImpact falseZero = CleaningImpact.Compare("CV10001", zero, clean);
            Assert.AreEqual(5, falseZero.Month);
            Assert.Less(falseZero.PlayerIndex, falseZero.CleanIndex - 0.2);

            double?[] spike = (double?[])clean.Clone();
            spike[14] = clean[14] * 4;
            SkuImpact order = CleaningImpact.Compare("CV10001", spike, clean);
            Assert.AreEqual(2, order.Month, "mar-24 carries the order.");
            Assert.Greater(order.PlayerIndex, order.CleanIndex);

            int[] launch = LevelPlayers.Portfolio.Find("CV10023").History.ToArray();
            SkuImpact zeros = CleaningImpact.Compare("CV10023", launch.Select(v => (double?)v).ToArray(),
                CleaningImpact.WithoutPreLaunch(launch));
            Assert.Less(zeros.PlayerLevel, zeros.CleanLevel * 0.9, "Six pre-launch zeros pull the level down.");
        }
    }
}
