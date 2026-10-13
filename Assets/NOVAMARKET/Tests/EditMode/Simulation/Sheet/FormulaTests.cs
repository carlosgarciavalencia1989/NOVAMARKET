using NovaMarket.Simulation.Sheet;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation.Sheet
{
    public class FormulaTests
    {
        private Workbook book;
        private Worksheet sheet;

        [SetUp]
        public void SetUp()
        {
            book = new Workbook();
            sheet = book.AddSheet("Hoja1");
            // A1:A4 = 10, 20, 30, 40; B1 = "texto"; C1 = VERDADERO; D1 empty.
            sheet.SetInput("A1", "10");
            sheet.SetInput("A2", "20");
            sheet.SetInput("A3", "30");
            sheet.SetInput("A4", "40");
            sheet.SetInput("B1", "texto");
            sheet.SetInput("C1", "VERDADERO");
        }

        private CellValue Eval(string formula)
        {
            sheet.SetInput("Z99", formula);
            return sheet.Value("Z99");
        }

        private double Number(string formula)
        {
            CellValue value = Eval(formula);
            Assert.IsTrue(value.IsNumber, $"{formula} gave {value}");
            return value.Number;
        }

        [TestCase("=1+2*3", 7)]
        [TestCase("=(1+2)*3", 9)]
        [TestCase("=2^3^2", 64)]          // Excel goes left to right
        [TestCase("=-2^2", 4)]            // the sign binds tighter than ^
        [TestCase("=10/4", 2.5)]
        [TestCase("=2,5*2", 5)]           // decimal comma
        [TestCase("=50%*10", 5)]
        [TestCase("=A1+A2", 30)]
        [TestCase("=a1*2", 20)]           // references ignore case
        [TestCase("=$A$1+A$2+$A3", 60)]
        [TestCase("=D1+1", 1)]            // an empty cell counts as 0
        [TestCase("=C1+1", 2)]            // VERDADERO counts as 1
        [TestCase("=\"5\"+1", 6)]         // numeric text is converted
        public void Arithmetic(string formula, double expected) => Assert.AreEqual(expected, Number(formula), 1e-9);

        [TestCase("=SUMA(A1:A4)", 100)]
        [TestCase("=SUMA(A1:A4;5)", 105)]
        [TestCase("=SUMA(A1;A3)", 40)]
        [TestCase("=SUMA(A1:D1)", 10)]    // text and booleans in a range are skipped
        [TestCase("=PROMEDIO(A1:A4)", 25)]
        [TestCase("=PROMEDIO(A1:B1)", 10)]
        [TestCase("=ABS(-4,5)", 4.5)]
        [TestCase("=ABS(A1-A4)", 30)]
        [TestCase("=DESVEST.P(A1:A4)", 11.180339887)]
        [TestCase("=desvest.p(2;4;4;4;5;5;7;9)", 2)]
        [TestCase("=SI(A1>5;1;2)", 1)]
        [TestCase("=SI(A1<5;1;2)", 2)]
        [TestCase("=SI(A1=10;A2;A3)", 20)]
        [TestCase("=SI(A1<>10;;7)", 7)]
        [TestCase("=SUMA(A1:A2)*PROMEDIO(A3:A4)", 1050)]
        public void Functions(string formula, double expected) => Assert.AreEqual(expected, Number(formula), 1e-6);

        [Test]
        public void Si_ReturnsTextAndFalse()
        {
            Assert.AreEqual("alto", Eval("=SI(A4>35;\"alto\";\"bajo\")").Text);
            Assert.AreEqual(CellValue.Of(false), Eval("=SI(A1>35;1)"));
            Assert.AreEqual(string.Empty, Eval("=SI(A1>35;1;\"\")").Text);
            Assert.AreEqual("di \"hola\"", Eval("=\"di \"\"hola\"\"\"").Text);
            Assert.AreEqual("10 cajas", Eval("=A1&\" cajas\"").Text);
        }

        [Test]
        public void Comparisons()
        {
            Assert.AreEqual(CellValue.Of(true), Eval("=A1<A2"));
            Assert.AreEqual(CellValue.Of(true), Eval("=B1=\"TEXTO\""), "Text compares without case.");
            Assert.AreEqual(CellValue.Of(true), Eval("=D1=\"\""), "An empty cell equals empty text.");
            Assert.AreEqual(CellValue.Of(true), Eval("=D1=0"));
            Assert.AreEqual(CellValue.Of(true), Eval("=A1>=10"));
        }

        [TestCase("=A1/0", SheetError.DivisionByZero)]
        [TestCase("=PROMEDIO(B1:D1)", SheetError.DivisionByZero)]
        [TestCase("=DESVEST.P(D1:D3)", SheetError.DivisionByZero)]
        [TestCase("=B1+1", SheetError.Value)]
        [TestCase("=SUMA(A1;\"x\")", SheetError.Value)]
        [TestCase("=A1:A4", SheetError.Value)]
        [TestCase("=PROMEDIOS(A1:A4)", SheetError.Name)]
        [TestCase("=Datos!A1", SheetError.Reference)]
        [TestCase("=#¡REF!+1", SheetError.Reference)]
        [TestCase("=SUMA(A1:A4)+A1/0", SheetError.DivisionByZero)]
        public void Errors(string formula, SheetError expected)
        {
            CellValue value = Eval(formula);
            Assert.IsTrue(value.IsError, $"{formula} gave {value}");
            Assert.AreEqual(expected, value.Error);
        }

        [Test]
        public void ErrorsPropagateThroughReferences()
        {
            sheet.SetInput("E1", "=A1/0");
            Assert.AreEqual(SheetError.DivisionByZero, Eval("=SUMA(A1:A4;E1)").Error);
            Assert.AreEqual(SheetError.DivisionByZero, Eval("=E1*2").Error);
        }

        [TestCase("=SUMA(A1,A2)", "punto y coma")]
        [TestCase("=SUMA(A1:A4", "paréntesis")]
        [TestCase("=SUMA(A1:A4))", "paréntesis")]
        [TestCase("=ABS(1;2)", "ABS lleva 1 argumento")]
        [TestCase("=SI(A1)", "SI lleva de 2 a 3 argumentos")]
        [TestCase("=1+", "incompleta")]
        [TestCase("=PROMEDIO A1", "no es una celda ni una función")]
        [TestCase("=\"abc", "comillas")]
        [TestCase("=A1:", "última celda")]
        public void SyntaxErrors_ExplainWhatIsWrong(string formula, string hint)
        {
            CellValue value = Eval(formula);

            Assert.AreEqual(SheetError.Syntax, value.Error);
            StringAssert.Contains(hint, sheet.FormulaError(CellAddress.Parse("Z99")));
            Assert.AreEqual(formula, sheet.GetInput(CellAddress.Parse("Z99")), "The input is kept so the player can fix it.");
        }

        [Test]
        public void EveryFunctionHasSyntaxAndHelp()
        {
            foreach (SheetFunction function in SheetFunctions.All)
            {
                StringAssert.StartsWith(function.Name + "(", function.Syntax);
                Assert.IsNotEmpty(function.Help);
            }
            CollectionAssert.IsSubsetOf(new[] { "SUMA", "PROMEDIO", "ABS", "SI", "DESVEST.P" },
                System.Linq.Enumerable.Select(SheetFunctions.All, f => f.Name));
        }
    }
}
