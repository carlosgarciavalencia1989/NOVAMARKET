using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>One of the three stars of a level, with what the player did.</summary>
    public sealed class StarCriterion
    {
        public StarCriterion(string title, bool achieved, string detail)
        {
            Title = title;
            Achieved = achieved;
            Detail = detail;
        }

        public string Title { get; }
        public bool Achieved { get; }
        public string Detail { get; }
    }

    /// <summary>A step of the level checklist shown in the HUD.</summary>
    public sealed class LevelObjective
    {
        public LevelObjective(string text, bool done)
        {
            Text = text;
            Done = done;
        }

        public string Text { get; }
        public bool Done { get; }
    }

    /// <summary>
    /// Andrés's comparison for a SKU: the monthly level and the most distorted seasonal index a model
    /// would learn from the player's series and from the clean one.
    /// </summary>
    public sealed class SkuImpact
    {
        public SkuImpact(string code, double playerLevel, double cleanLevel, int month, double playerIndex, double cleanIndex)
        {
            Code = code;
            PlayerLevel = playerLevel;
            CleanLevel = cleanLevel;
            Month = month;
            PlayerIndex = playerIndex;
            CleanIndex = cleanIndex;
        }

        public string Code { get; }
        public double PlayerLevel { get; }
        public double CleanLevel { get; }

        /// <summary>Calendar month (0 = January) whose index moved the most.</summary>
        public int Month { get; }

        public double PlayerIndex { get; }
        public double CleanIndex { get; }

        /// <summary>Relative error of the level (0.1 = 10 % off).</summary>
        public double LevelError => CleanLevel == 0 ? 0 : Math.Abs(PlayerLevel / CleanLevel - 1);

        public double IndexError => Math.Abs(PlayerIndex - CleanIndex);

        /// <summary>The larger of both distortions.</summary>
        public double Damage => Math.Max(LevelError, IndexError);

        public bool LevelIsWorse => LevelError >= IndexError;
    }

    /// <summary>
    /// Result of a delivered level. The first criterion is the level's core deliverable: without it
    /// the level is not passed and gives no stars.
    /// </summary>
    public sealed class LevelResult
    {
        /// <summary>Distortion (2 % of the level or 0.02 of an index) above which Andrés shows a SKU.</summary>
        public const double VisibleDamage = 0.02;

        public LevelResult(LevelInfo level, IReadOnlyList<StarCriterion> criteria, bool excelBadge,
            IReadOnlyList<DialogueLine> debrief, IReadOnlyList<string> lessons, IReadOnlyList<SkuImpact> impact = null)
        {
            if (criteria == null || criteria.Count != 3) throw new ArgumentException("A level has three stars.", nameof(criteria));
            Level = level;
            Criteria = criteria;
            ExcelBadge = excelBadge;
            Debrief = debrief ?? Array.Empty<DialogueLine>();
            Lessons = lessons ?? Array.Empty<string>();
            Impact = impact ?? Array.Empty<SkuImpact>();
        }

        public LevelInfo Level { get; }
        public IReadOnlyList<StarCriterion> Criteria { get; }
        public bool Passed => Criteria[0].Achieved;
        public int Stars => Passed ? Criteria.Count(c => c.Achieved) : 0;

        /// <summary>"Excel de planeador": the results were computed with the player's own formulas.</summary>
        public bool ExcelBadge { get; }

        public IReadOnlyList<DialogueLine> Debrief { get; }
        public IReadOnlyList<string> Lessons { get; }
        public IReadOnlyList<SkuImpact> Impact { get; }

        public bool EarnedGem => Passed && Level.Gem != null;

        /// <summary>100 points per star plus the gem's value.</summary>
        public int Points => Passed ? Stars * 100 + (Level.Gem?.Points ?? 0) : 0;

        public IEnumerable<SkuImpact> DamagedSkus => Impact.Where(i => i.Damage > VisibleDamage);
    }

    /// <summary>The ±2 % margin of every validation (DISENO_JUEGO_V2.md, section 5), so roundings are not punished.</summary>
    public static class Tolerance
    {
        public const double Relative = 0.02;

        /// <summary>Absolute margin for answers near zero.</summary>
        public const double Absolute = 0.5;

        public static bool Matches(double actual, double expected) =>
            Math.Abs(actual - expected) <= Math.Max(Absolute, Math.Abs(expected) * Relative);
    }
}
