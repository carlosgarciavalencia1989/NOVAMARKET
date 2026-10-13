using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Portfolio
{
    /// <summary>
    /// Descriptive statistics of a monthly series, the ones the Atlas uses to read a pattern:
    /// mean, population standard deviation, CV, ADI (average interval between months with sales),
    /// first and last month with sales, and the seasonal index of each calendar month.
    /// </summary>
    public static class SeriesStatistics
    {
        /// <summary>Months with ADI above this are intermittent (Syntetos–Boylan cut-off).</summary>
        public const double IntermittentAdi = 1.32;

        public static double Mean(IReadOnlyList<int> series, int from = 0, int to = -1)
        {
            to = to < 0 ? series.Count - 1 : to;
            double sum = 0;
            for (int i = from; i <= to; i++) sum += series[i];
            return to < from ? 0 : sum / (to - from + 1);
        }

        /// <summary>Population standard deviation (DESVEST.P).</summary>
        public static double StdDev(IReadOnlyList<int> series, int from = 0, int to = -1)
        {
            to = to < 0 ? series.Count - 1 : to;
            if (to < from) return 0;
            double mean = Mean(series, from, to), sum = 0;
            for (int i = from; i <= to; i++) sum += (series[i] - mean) * (series[i] - mean);
            return Math.Sqrt(sum / (to - from + 1));
        }

        /// <summary>Coefficient of variation from the first month with sales on.</summary>
        public static double VariationCoefficient(IReadOnlyList<int> series)
        {
            int first = FirstSale(series);
            if (first < 0) return 0;
            int last = LastSale(series);
            double mean = Mean(series, first, last);
            return mean == 0 ? 0 : StdDev(series, first, last) / mean;
        }

        /// <summary>Average number of months between sales, from the first to the last sale.</summary>
        public static double AverageDemandInterval(IReadOnlyList<int> series)
        {
            int first = FirstSale(series), last = LastSale(series), count = 0;
            if (first < 0) return double.PositiveInfinity;
            for (int i = first; i <= last; i++) if (series[i] > 0) count++;
            return (double)(last - first + 1) / count;
        }

        public static int FirstSale(IReadOnlyList<int> series)
        {
            for (int i = 0; i < series.Count; i++) if (series[i] > 0) return i;
            return -1;
        }

        public static int LastSale(IReadOnlyList<int> series)
        {
            for (int i = series.Count - 1; i >= 0; i--) if (series[i] > 0) return i;
            return -1;
        }

        /// <summary>
        /// Twelve seasonal indices (average of each calendar month ÷ overall average), from the first
        /// sale on; months without data get 1. They average 1.00 when every month has data.
        /// </summary>
        public static double[] SeasonalIndices(IReadOnlyList<int> series)
        {
            var indices = new double[12];
            int first = FirstSale(series);
            if (first < 0)
            {
                for (int m = 0; m < 12; m++) indices[m] = 1;
                return indices;
            }
            var sums = new double[12];
            var counts = new int[12];
            for (int i = first; i < series.Count; i++)
            {
                sums[SalesCalendar.CalendarMonth(i)] += series[i];
                counts[SalesCalendar.CalendarMonth(i)]++;
            }
            double overall = Mean(series, first);
            for (int m = 0; m < 12; m++)
                indices[m] = counts[m] == 0 || overall == 0 ? 1 : sums[m] / counts[m] / overall;
            return indices;
        }

        /// <summary>Total of the last <paramref name="months"/> months (the "=SUMA(AI3:AT3)" of the Atlas).</summary>
        public static int LastMonthsTotal(IReadOnlyList<int> series, int months = 12)
        {
            int total = 0;
            for (int i = Math.Max(0, series.Count - months); i < series.Count; i++) total += series[i];
            return total;
        }

        /// <summary>Reads the pattern of a real series with simple rules (used for the Atlas SKUs).</summary>
        public static DemandPattern Classify(IReadOnlyList<int> series)
        {
            int first = FirstSale(series), last = LastSale(series);
            if (first >= 6) return DemandPattern.Launch;
            if (last >= 0 && last < series.Count - 3) return DemandPattern.Discontinued;
            if (AverageDemandInterval(series) > IntermittentAdi) return DemandPattern.Intermittent;
            if (VariationCoefficient(series) > 0.5) return DemandPattern.Erratic;
            double peak = 0;
            foreach (double index in SeasonalIndices(series)) peak = Math.Max(peak, index);
            return peak >= 1.4 ? DemandPattern.Seasonal : DemandPattern.Stable;
        }
    }
}
