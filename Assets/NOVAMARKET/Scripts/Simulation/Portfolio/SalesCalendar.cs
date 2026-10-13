using System;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// Monthly history calendar of the Atlas: 44 months from January 2023 (index 0) to August 2026
    /// (index 43). Labels follow the Atlas: "ene-23", "jun-24".
    /// </summary>
    public static class SalesCalendar
    {
        public const int StartYear = 2023;
        public const int Months = 44;

        private static readonly string[] MonthNames =
            { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        /// <summary>Calendar month 0–11 (0 = January) of a history index.</summary>
        public static int CalendarMonth(int index) => ((index % 12) + 12) % 12;

        public static int Year(int index) => StartYear + (int)Math.Floor(index / 12.0);

        /// <summary>"ene-23" style label.</summary>
        public static string Label(int index) => $"{MonthNames[CalendarMonth(index)]}-{Year(index) % 100:00}";

        /// <summary>"2023-01" style key used in the CSV header.</summary>
        public static string Key(int index) => $"{Year(index)}-{CalendarMonth(index) + 1:00}";

        public static string MonthName(int calendarMonth) => MonthNames[calendarMonth];

        /// <summary>Index of a "2023-01" key, or -1.</summary>
        public static int IndexOfKey(string key)
        {
            if (key == null || key.Length != 7 || key[4] != '-') return -1;
            if (!int.TryParse(key.Substring(0, 4), out int year) || !int.TryParse(key.Substring(5, 2), out int month)) return -1;
            if (month < 1 || month > 12) return -1;
            return (year - StartYear) * 12 + month - 1;
        }
    }
}
