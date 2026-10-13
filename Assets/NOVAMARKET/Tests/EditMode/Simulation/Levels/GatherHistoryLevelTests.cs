using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    public class GatherHistoryLevelTests
    {
        private GatherHistoryLevel level;
        private List<DialogueLine> said;

        [SetUp]
        public void SetUp()
        {
            level = new GatherHistoryLevel(LevelPlayers.Portfolio, seed: 8, TestDialogue.Book);
            said = new List<DialogueLine>();
            level.Said += said.Add;
        }

        [Test]
        public void Level_AsksForFourRealSkus()
        {
            Assert.AreEqual(4, level.RequestedCodes.Distinct().Count());
            Assert.IsTrue(level.RequestedCodes.All(c => LevelPlayers.Portfolio.Find(c).IsReal));
            CollectionAssert.AreEqual(level.RequestedCodes, new GatherHistoryLevel(LevelPlayers.Portfolio, 8).RequestedCodes);
            Assert.AreEqual("1.1", level.Info.Id);
            Assert.AreEqual(ScaffoldMode.Guided, level.Info.Mode);
        }

        [Test]
        public void Download_PreparesTheGuidedCells_AndWarnsAboutTheProjection()
        {
            Worksheet sheet = LevelPlayers.Download(level, projection: true);

            Assert.AreEqual(1, sheet.Value("C1").Number, "First period given.");
            Assert.AreEqual("=SUMA(AI3:AT3)", sheet.GetInput(CellAddress.Parse("AW3")), "First total given.");
            Assert.AreEqual(GatherHistoryLevel.TotalHeader, sheet.Value("AW2").Text);
            Assert.IsTrue(said.Any(l => l.Key == DialogueKeys.ProjectionWarning));
            Assert.IsTrue(level.HasProjection());
        }

        [Test]
        public void SolvedLevel_GivesThreeStarsTheGemAndTheExcelBadge()
        {
            LevelPlayers.SolveGather(level);

            Assert.IsTrue(level.Objectives().All(o => o.Done));
            LevelResult result = level.Deliver();
            Assert.IsTrue(result.Passed);
            Assert.AreEqual(3, result.Stars);
            Assert.IsTrue(result.ExcelBadge);
            Assert.IsTrue(result.EarnedGem);
            Assert.AreEqual(300 + ForecastWorld.GatherHistory.Gem.Points, result.Points);
            Assert.AreEqual(DialogueKeys.Success, result.Debrief[0].Key);
            Assert.AreEqual(3, result.Lessons.Count);
        }

        [Test]
        public void KeepingTheProjection_FailsTheLevel_ButDeletingTheColumnFixesIt()
        {
            LevelPlayers.SolveGather(level, projection: true);
            Assert.IsFalse(level.Deliver().Passed);
            StringAssert.Contains("proyección", level.Result.Criteria[0].Detail);

            Worksheet sheet = level.Book.Sheet(level.HistorySheet);
            sheet.Clear(CellRange.Parse("AU3:AU6"));

            Assert.IsTrue(level.Deliver().Passed);
        }

        [Test]
        public void WrongQuery_FailsTheLevel()
        {
            Worksheet sheet = level.Erp.Download(level.Erp.Billing(new BillingQuery
            {
                Codes = level.RequestedCodes.Take(3).ToList(), IncludeProjection = false
            }), level.Book);
            sheet.Fill(CellRange.Parse("AW3"), CellRange.Parse("AW3:AW5"));

            LevelResult result = level.Deliver();
            Assert.IsFalse(result.Passed);
            Assert.AreEqual(0, result.Stars);
            Assert.AreEqual(0, result.Points);
            Assert.AreEqual(DialogueKeys.WrongQuery, result.Debrief[0].Key);
        }

        [Test]
        public void TotalsTypedAsNumbers_PassWithoutTheBadge_AndPeriodsAreTheSecondStar()
        {
            Worksheet sheet = LevelPlayers.Download(level);
            for (int i = 0; i < 4; i++)
            {
                string code = level.RequestedCodes[i];
                int row = HistoryTable.FindRow(sheet, code);
                sheet.SetNumber(new CellAddress(GatherHistoryLevel.TotalColumn, row),
                    NovaMarket.Simulation.Portfolio.SeriesStatistics.LastMonthsTotal(level.Data.Billed(code)));
            }

            LevelResult result = level.Deliver();
            Assert.IsTrue(result.Passed);
            Assert.AreEqual(2, result.Stars, "No periods row.");
            Assert.IsFalse(result.ExcelBadge);
        }

        [Test]
        public void AtlasHelp_SolvesTheStepButCostsTheThirdStar()
        {
            AtlasAnswer answer = level.AskAtlas(GatherHistoryLevel.TopicTotals);
            LevelPlayers.SolveGather(level);

            Assert.IsTrue(answer.Steps.Any(s => s.Contains("=SUMA(AI3:AT3)")));
            LevelResult result = level.Deliver();
            Assert.AreEqual(2, result.Stars);
            Assert.IsFalse(result.Criteria[2].Achieved);
            Assert.Throws<System.ArgumentException>(() => level.AskAtlas("nada"));
        }

        [Test]
        public void Objectives_FollowTheSheet()
        {
            Assert.IsTrue(level.Objectives().All(o => !o.Done));
            LevelPlayers.Download(level, projection: true);
            bool[] done = level.Objectives().Select(o => o.Done).ToArray();
            CollectionAssert.AreEqual(new[] { true, false, false, false }, done);
        }
    }
}
