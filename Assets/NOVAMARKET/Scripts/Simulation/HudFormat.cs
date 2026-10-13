using System;
using System.Globalization;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// Number formatting for the HUD in Colombian Spanish style ("1.250", "96,2 %", "$ 845 M").
    /// Uses a fixed format instead of a culture lookup so every platform shows the same text.
    /// </summary>
    public static class HudFormat
    {
        private static readonly NumberFormatInfo Format = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NegativeSign = "-"
        };

        /// <summary>Whole number with thousands separators: 1250 → "1.250".</summary>
        public static string Number(long value) => value.ToString("#,0", Format);

        /// <summary>Number with a fixed count of decimals: (1234.56, 1) → "1.234,6".</summary>
        public static string Decimal(double value, int decimals)
        {
            string pattern = decimals > 0 ? "#,0." + new string('0', decimals) : "#,0";
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString(pattern, Format);
        }

        /// <summary>Demand multiplier with one or two decimals: 1.9 → "×1,9", 1.35 → "×1,35".</summary>
        public static string Factor(double factor)
        {
            bool oneDecimal = Math.Abs(factor * 10 - Math.Round(factor * 10)) < 1e-9;
            return "×" + Decimal(factor, oneDecimal ? 1 : 2);
        }

        /// <summary>Fraction as a percentage with one decimal: 0.962 → "96,2 %".</summary>
        public static string Percent(double fraction) =>
            (fraction * 100.0).ToString("0.0", Format) + " %";

        /// <summary>
        /// Colombian pesos. Millions are shortened with "M" (845 000 000 → "$ 845 M",
        /// 1 250 500 000 → "$ 1.250,5 M"); smaller amounts are shown in full ("$ 12.500").
        /// </summary>
        public static string Pesos(long value)
        {
            string sign = value < 0 ? "-" : string.Empty;
            long absolute = Math.Abs(value);
            if (absolute < 1_000_000) return $"{sign}$ {Number(absolute)}";

            double millions = Math.Round(absolute / 1_000_000.0, 1, MidpointRounding.AwayFromZero);
            return $"{sign}$ {millions.ToString("#,0.#", Format)} M";
        }

        /// <summary>Progress counter: (3, 8) → "3/8".</summary>
        public static string Ratio(int value, int total) => $"{Number(value)}/{Number(total)}";
    }
}
