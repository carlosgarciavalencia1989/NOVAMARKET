using System.Linq;
using NovaMarket.Simulation.Levels;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Levels
{
    public class DialogueBookTests
    {
        [Test]
        public void Lines_ComeInOrderAcrossCharacters_WithTheirValuesFilledIn()
        {
            var level = new GatherHistoryLevel(LevelPlayers.Portfolio, seed: 8, TestDialogue.Book);

            var intro = level.Intro;
            CollectionAssert.AreEqual(new[] { "andres", "sofia", "andres" }, intro.Select(l => l.CharacterId));
            Assert.AreEqual($"Necesito {string.Join(", ", level.RequestedCodes)}.", intro[1].Text);
            Assert.IsTrue(intro.All(l => l.Key == DialogueKeys.Intro));
        }

        [Test]
        public void MissingLine_ShowsItsKey_AndKeepsTheValues()
        {
            DialogueLine line = DialogueBook.Empty.Line("1.2", DialogueKeys.MissingMany, DialogueArgs.Of(("missing", "3")));

            Assert.AreEqual("[1.2/faltan-problemas]", line.Text);
            Assert.AreEqual("3", line.Args["missing"]);
        }

        [Test]
        public void SectorAndRoleLines_ReplaceTheGeneralOnes()
        {
            var book = new DialogueBook(new[]
            {
                new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Neutral, "General."),
                new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Neutral, "Repuestos.", sectorId: "repuestos"),
                new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Neutral, "Comprador.", roleId: "abastecer"),
                new DialogueEntry("1.2", "andres", DialogueKeys.Intro, 2, DialogueMood.Neutral, "Andrés.")
            }, roleId: "planeador", sectorId: "repuestos");

            CollectionAssert.AreEqual(new[] { "Repuestos.", "Andrés." }, book.Lines("1.2", DialogueKeys.Intro).Select(l => l.Text));
            var cosmetics = new DialogueBook(new[]
            {
                new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Neutral, "General."),
                new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Neutral, "Repuestos.", sectorId: "repuestos")
            }, roleId: "planeador", sectorId: "cosmetica");
            Assert.AreEqual("General.", cosmetics.Line("1.2", DialogueKeys.Intro).Text);
        }

        [Test]
        public void Fill_LeavesUnknownPlaceholders_AndListsThem()
        {
            Assert.AreEqual("CV10004 y {otro}", DialogueBook.Fill("{code} y {otro}", DialogueArgs.Of(("code", "CV10004"))));
            CollectionAssert.AreEqual(new[] { "code", "otro" }, DialogueBook.PlaceholdersOf("{code} y {otro}"));
        }
    }
}
