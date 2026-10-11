using System;
using System.Text;

namespace NovaMarket.Simulation
{
    /// <summary>Converts level numbers to Roman numerals ("Nivel III").</summary>
    public static class RomanNumerals
    {
        private static readonly int[] Values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        private static readonly string[] Symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        /// <summary>Roman numeral for 1..3999.</summary>
        public static string From(int number)
        {
            if (number < 1 || number > 3999)
                throw new ArgumentOutOfRangeException(nameof(number), number, "Roman numerals cover 1..3999.");

            var builder = new StringBuilder();
            for (int i = 0; i < Values.Length; i++)
            {
                while (number >= Values[i])
                {
                    builder.Append(Symbols[i]);
                    number -= Values[i];
                }
            }
            return builder.ToString();
        }
    }
}
