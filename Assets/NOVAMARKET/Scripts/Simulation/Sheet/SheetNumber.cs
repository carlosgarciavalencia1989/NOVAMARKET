using System;
using System.Globalization;
using System.Text;

namespace NovaMarket.Simulation.Sheet
{
    public enum CellFormatKind
    {
        General,
        Number,
        Percent
    }

    /// <summary>How a number is shown: general, number with thousands and N decimals, or percent.</summary>
    public readonly struct CellFormat : IEquatable<CellFormat>
    {
        public CellFormat(CellFormatKind kind, int decimals)
        {
            Kind = kind;
            Decimals = Math.Max(0, Math.Min(10, decimals));
        }

        public static readonly CellFormat General = default;
        public static CellFormat Number(int decimals = 0) => new CellFormat(CellFormatKind.Number, decimals);
        public static CellFormat Percent(int decimals = 0) => new CellFormat(CellFormatKind.Percent, decimals);

        public CellFormatKind Kind { get; }
        public int Decimals { get; }

        public bool Equals(CellFormat other) => Kind == other.Kind && Decimals == other.Decimals;
        public override bool Equals(object obj) => obj is CellFormat other && Equals(other);
        public override int GetHashCode() => (int)Kind * 31 + Decimals;
    }

    /// <summary>
    /// Numbers as Excel in Spanish (Colombia) writes them: decimal comma, dot for thousands
    /// ("4.694,67"), percent sign after the number ("45%").
    /// </summary>
    public static class SheetNumber
    {
        public const char DecimalSeparator = ',';
        public const char ThousandsSeparator = '.';

        public static string Format(double value, CellFormat format)
        {
            switch (format.Kind)
            {
                case CellFormatKind.Number:
                    return Grouped(value, format.Decimals);
                case CellFormatKind.Percent:
                    return Grouped(value * 100, format.Decimals) + "%";
                default:
                    if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
                        return ((long)value).ToString(CultureInfo.InvariantCulture);
                    string text = value.ToString("G10", CultureInfo.InvariantCulture);
                    return text.Replace('.', DecimalSeparator);
            }
        }

        private static string Grouped(double value, int decimals)
        {
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (rounded == 0) rounded = 0; // no "-0"
            string text = rounded.ToString("N" + decimals, CultureInfo.InvariantCulture); // "1,234.57"
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
                builder.Append(c == ',' ? ThousandsSeparator : c == '.' ? DecimalSeparator : c);
            return builder.ToString();
        }

        /// <summary>
        /// Reads what the player types in a cell: "4695", "4.695", "4694,67", "-2,5", "45%".
        /// Dots are only accepted as thousands separators (groups of three digits).
        /// </summary>
        public static bool TryParse(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            bool percent = s.EndsWith("%");
            if (percent) s = s.Substring(0, s.Length - 1).TrimEnd();
            bool negative = s.StartsWith("-");
            if (negative || s.StartsWith("+")) s = s.Substring(1);
            if (s.Length == 0) return false;

            int comma = s.IndexOf(DecimalSeparator);
            if (comma >= 0 && s.IndexOf(DecimalSeparator, comma + 1) >= 0) return false;
            string integer = comma >= 0 ? s.Substring(0, comma) : s;
            string fraction = comma >= 0 ? s.Substring(comma + 1) : string.Empty;
            if (integer.Length == 0 && fraction.Length == 0) return false;

            if (integer.IndexOf(ThousandsSeparator) >= 0)
            {
                string[] groups = integer.Split(ThousandsSeparator);
                if (groups[0].Length == 0 || groups[0].Length > 3) return false;
                for (int i = 1; i < groups.Length; i++) if (groups[i].Length != 3) return false;
                integer = string.Concat(groups);
            }
            foreach (char c in integer + fraction) if (c < '0' || c > '9') return false;

            string normalized = (integer.Length == 0 ? "0" : integer) + (fraction.Length > 0 ? "." + fraction : string.Empty);
            if (!double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
            if (negative) value = -value;
            if (percent) value /= 100;
            return true;
        }
    }
}
