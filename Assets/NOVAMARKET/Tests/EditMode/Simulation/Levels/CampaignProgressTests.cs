using NovaMarket.Simulation.Levels;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    public class CampaignProgressTests
    {
        private static LevelResult Result(LevelInfo level, bool first, bool second, bool third) =>
            new LevelResult(level, new[]
            {
                new StarCriterion("a", first, ""), new StarCriterion("b", second, ""), new StarCriterion("c", third, "")
            }, false, null, null);

        [Test]
        public void KeepsTheBestResult_AndAWorseTryNeverLowersIt()
        {
            var progress = new CampaignProgress();
            Assert.IsTrue(progress.Record(Result(ForecastWorld.CleanData, true, false, false)));
            Assert.IsTrue(progress.Record(Result(ForecastWorld.CleanData, true, true, false)));
            Assert.IsFalse(progress.Record(Result(ForecastWorld.CleanData, false, false, false)));

            LevelRecord best = progress.Best("1.2");
            Assert.AreEqual(2, best.Stars);
            Assert.AreEqual(200 + 400, best.Points);
            Assert.IsTrue(best.Gem);
            Assert.IsNull(progress.Best("1.1"));
        }

        [Test]
        public void SavedText_ReadsBackTheSameProgress()
        {
            var progress = new CampaignProgress();
            progress.Record(Result(ForecastWorld.GatherHistory, true, true, true));
            progress.Record(Result(ForecastWorld.CleanData, true, false, false));

            CampaignProgress loaded = CampaignProgress.Parse(progress.Serialize());

            Assert.AreEqual("1.1:3:600:1;1.2:1:500:1", progress.Serialize());
            Assert.AreEqual(3, loaded.Best("1.1").Stars);
            Assert.AreEqual(progress.TotalPoints, loaded.TotalPoints);
            Assert.AreEqual(2, loaded.Gems);
            Assert.IsNull(CampaignProgress.Parse("basura;1.3:x:1:0").Best("1.3"));
        }
    }
}
