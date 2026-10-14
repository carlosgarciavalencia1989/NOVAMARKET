using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class FormulaEditingTests
    {
        [Test]
        public void ClickAfterOpeningParenthesis_InsertsAtTheCursor_AndKeepsTheFormula()
        {
            string text = "=PROMEDIO(";
            Assert.IsTrue(FormulaEditing.CanInsertReference(text, text.Length));

            text = FormulaEditing.Insert(text, text.Length, text.Length, "C3", out int caret);

            Assert.AreEqual("=PROMEDIO(C3", text);
            Assert.AreEqual(text.Length, caret, "The cursor stays after the reference, ready to type ;");
        }

        [Test]
        public void TypingSemicolon_ThenClickingAnotherCell_AddsASecondReference()
        {
            string text = FormulaEditing.Insert("=PROMEDIO(", 10, 10, "C3", out int caret) + ";";
            caret++;
            Assert.IsTrue(FormulaEditing.CanInsertReference(text, caret));

            text = FormulaEditing.Insert(text, caret, caret, "C5", out caret);

            Assert.AreEqual("=PROMEDIO(C3;C5", text);
            Assert.IsFalse(FormulaEditing.CanInsertReference(text, caret), "After a reference a click selects, it does not insert.");
        }

        [Test]
        public void DraggingOrClickingAgain_ReplacesTheReferenceJustPicked()
        {
            string text = FormulaEditing.Insert("=SUMA(", 6, 6, "B3", out int caret);
            text = FormulaEditing.Insert(text, 6, caret, "B3:B9", out caret);

            Assert.AreEqual("=SUMA(B3:B9", text);
            Assert.AreEqual(11, caret);
        }

        [Test]
        public void ReferenceGoesWhereTheCursorIs_NotAtTheEnd()
        {
            // =SUMA(|)*2 with the cursor after "(".
            string text = "=SUMA()*2";
            Assert.IsTrue(FormulaEditing.CanInsertReference(text, 6));

            text = FormulaEditing.Insert(text, 6, 6, "'Serie limpia'!B3", out int caret);

            Assert.AreEqual("=SUMA('Serie limpia'!B3)*2", text);
            Assert.AreEqual(23, caret);
        }

        [Test]
        public void OnlyFormulas_AfterAnOperatorOrSeparator_AcceptAReference()
        {
            Assert.IsFalse(FormulaEditing.CanInsertReference("4695", 4));
            Assert.IsFalse(FormulaEditing.CanInsertReference("=B3", 3));
            Assert.IsTrue(FormulaEditing.CanInsertReference("=B3+", 4));
            Assert.IsTrue(FormulaEditing.CanInsertReference("=SUMA(B3; ", 10), "Spaces before the cursor are skipped.");
            Assert.IsTrue(FormulaEditing.CanInsertReference("=", 1));
        }

        [Test]
        public void LowercaseFormula_IsWrittenInCapitals_AndStillWorks()
        {
            var sheet = new Workbook().AddSheet("Datos");
            sheet.SetInput("B3", "4");
            sheet.SetInput("B4", "6");

            sheet.SetInput("C1", "=promedio(b3;b4)");
            sheet.SetInput("C2", "=si(b3>1;\"alto\";\"bajo\")");
            sheet.SetInput("C3", "=desvest.p('Datos'!b3:b4)");

            Assert.AreEqual("=PROMEDIO(B3;B4)", sheet.GetInput(CellAddress.Parse("C1")));
            Assert.AreEqual(5, sheet.Value("C1").Number);
            Assert.AreEqual("=SI(B3>1;\"alto\";\"bajo\")", sheet.GetInput(CellAddress.Parse("C2")), "Texts in quotes keep their case.");
            Assert.AreEqual("alto", sheet.Value("C2").Text);
            Assert.AreEqual("=DESVEST.P('Datos'!B3:B4)", sheet.GetInput(CellAddress.Parse("C3")));
            Assert.AreEqual(1, sheet.Value("C3").Number, 1e-9);
        }
    }
}
