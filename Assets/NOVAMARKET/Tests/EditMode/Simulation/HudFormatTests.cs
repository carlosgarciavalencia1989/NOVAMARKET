using System;
using NovaMarket.Simulation;
using NUnit.Framework;

namespace NovaMarket.Tests.Simulation
{
    public class HudFormatTests
    {
        [TestCase(1, "I")]
        [TestCase(3, "III")]
        [TestCase(4, "IV")]
        [TestCase(9, "IX")]
        [TestCase(14, "XIV")]
        [TestCase(1994, "MCMXCIV")]
        public void RomanNumerals_ConvertsLevelNumbers(int number, string expected) =>
            Assert.AreEqual(expected, RomanNumerals.From(number));

        [Test]
        public void RomanNumerals_RejectsZero() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => RomanNumerals.From(0));

        [TestCase(0, "0")]
        [TestCase(950, "950")]
        [TestCase(1250, "1.250")]
        [TestCase(1234567, "1.234.567")]
        public void Number_UsesDotsForThousands(long value, string expected) =>
            Assert.AreEqual(expected, HudFormat.Number(value));

        [TestCase(0.962, "96,2 %")]
        [TestCase(1.0, "100,0 %")]
        [TestCase(0.05, "5,0 %")]
        public void Percent_UsesCommaDecimals(double fraction, string expected) =>
            Assert.AreEqual(expected, HudFormat.Percent(fraction));

        [TestCase(45.25, 1, "45,3")]
        [TestCase(0.18, 2, "0,18")]
        [TestCase(1234.56, 1, "1.234,6")]
        [TestCase(36.0, 0, "36")]
        public void Decimal_UsesCommaDecimalsAndHalfUpRounding(double value, int decimals, string expected) =>
            Assert.AreEqual(expected, HudFormat.Decimal(value, decimals));

        [TestCase(12_500, "$ 12.500")]
        [TestCase(845_000_000, "$ 845 M")]
        [TestCase(1_250_500_000, "$ 1.250,5 M")]
        [TestCase(-3_000_000, "-$ 3 M")]
        public void Pesos_ShortensMillions(long value, string expected) =>
            Assert.AreEqual(expected, HudFormat.Pesos(value));

        [Test]
        public void Ratio_ShowsValueOverTotal() => Assert.AreEqual("3/8", HudFormat.Ratio(3, 8));
    }
}
