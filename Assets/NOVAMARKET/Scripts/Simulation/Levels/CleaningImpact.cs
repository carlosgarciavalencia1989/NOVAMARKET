using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Portfolio;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>
    /// How a series would hurt the forecast later (Andrés's comparison in level 1.2). The Atlas says
    /// dirty data "distorts the level and the parameters": this compares what a model would learn from
    /// the player's series with what it learns from the clean series of the Atlas — the monthly level
    /// (average of the months with data) and the seasonal index of each calendar month. A false zero or
    /// a seasonal month imputed with its neighbours lowers that month's index; a one-off order raises
    /// it; pre-launch zeros and boxes counted as units lower the level.
    /// </summary>
    public static class CleaningImpact
    {
        /// <summary>Monthly level and the 12 seasonal indices learnt from a series (null = excluded month).</summary>
        public static (double Level, double[] Indices) Learn(IReadOnlyList<double?> series)
        {
            double sum = 0;
            int count = 0;
            var monthSums = new double[12];
            var monthCounts = new int[12];
            for (int m = 0; m < series.Count; m++)
            {
                if (!series[m].HasValue) continue;
                sum += series[m].Value;
                count++;
                monthSums[SalesCalendar.CalendarMonth(m)] += series[m].Value;
                monthCounts[SalesCalendar.CalendarMonth(m)]++;
            }
            double level = count == 0 ? 0 : sum / count;
            var indices = new double[12];
            for (int c = 0; c < 12; c++)
                indices[c] = monthCounts[c] == 0 || level == 0 ? 1 : monthSums[c] / monthCounts[c] / level;
            return (level, indices);
        }

        /// <summary>Compares the player's series of a SKU with the clean one.</summary>
        public static SkuImpact Compare(string code, IReadOnlyList<double?> player, IReadOnlyList<double?> clean)
        {
            (double playerLevel, double[] playerIndices) = Learn(player);
            (double cleanLevel, double[] cleanIndices) = Learn(clean);
            int worst = 0;
            for (int c = 1; c < 12; c++)
                if (Math.Abs(playerIndices[c] - cleanIndices[c]) > Math.Abs(playerIndices[worst] - cleanIndices[worst])) worst = c;
            return new SkuImpact(code, playerLevel, cleanLevel, worst, playerIndices[worst], cleanIndices[worst]);
        }

        /// <summary>The series with the months before the first sale left out.</summary>
        public static double?[] WithoutPreLaunch(IReadOnlyList<int> history)
        {
            int first = Math.Max(0, SeriesStatistics.FirstSale(history));
            var series = new double?[history.Count];
            for (int m = first; m < history.Count; m++) series[m] = history[m];
            return series;
        }
    }
}
