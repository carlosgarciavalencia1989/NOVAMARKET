using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation.Erp;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>What happened with one problem of level 1.2 when the player delivered.</summary>
    public enum IssueOutcome
    {
        /// <summary>Marked with the right type and action, and fixed in «Serie limpia».</summary>
        Correct,

        /// <summary>Not marked at all.</summary>
        NotMarked,

        /// <summary>Marked as another type of problem.</summary>
        WrongKind,

        /// <summary>Marked, but «Serie limpia» still has the ERP value.</summary>
        NotCorrected,

        /// <summary>Marked with an action that does not fit the problem.</summary>
        WrongAction,

        /// <summary>Marked and changed, but the value is not the Atlas one.</summary>
        WrongValue
    }

    /// <summary>One row of the result table: a problem, what the player did and what the Atlas expects.</summary>
    public sealed class IssueReview
    {
        public IssueReview(DataIssue issue, string where, IssueOutcome outcome, DataIssueKind? markedKind,
            CorrectionAction? chosenAction, string playerValue, string expectedValue, string message, IReadOnlyList<int> wrongMonths)
        {
            Issue = issue;
            Where = where;
            Outcome = outcome;
            MarkedKind = markedKind;
            ChosenAction = chosenAction;
            PlayerValue = playerValue;
            ExpectedValue = expectedValue;
            Message = message;
            WrongMonths = wrongMonths;
        }

        public DataIssue Issue { get; }

        /// <summary>SKU and month(s): "CV10004 jun-24", "CV10023 ene-23 a jun-23".</summary>
        public string Where { get; }

        public IssueOutcome Outcome { get; }
        public bool IsCorrect => Outcome == IssueOutcome.Correct;

        /// <summary>The type the player marked on these months, or null when not marked.</summary>
        public DataIssueKind? MarkedKind { get; }

        public CorrectionAction? ChosenAction { get; }
        public CorrectionAction ExpectedAction => Issue.ExpectedAction;

        /// <summary>What «Serie limpia» has, in words ("0", "vacías", "40 de 44 meses bien…").</summary>
        public string PlayerValue { get; }

        public string ExpectedValue { get; }

        /// <summary>What happened, in a full sentence.</summary>
        public string Message { get; }

        /// <summary>Months of the problem whose value in «Serie limpia» is not right.</summary>
        public IReadOnlyList<int> WrongMonths { get; }
    }

    /// <summary>A good month the player marked as a problem or changed in «Serie limpia».</summary>
    public sealed class GoodDataReview
    {
        public GoodDataReview(string code, int month, string where, bool marked, bool changed, string message)
        {
            Code = code;
            Month = month;
            Where = where;
            Marked = marked;
            Changed = changed;
            Message = message;
        }

        public string Code { get; }
        public int Month { get; }
        public string Where { get; }

        /// <summary>The player marked this month as a problem.</summary>
        public bool Marked { get; }

        /// <summary>The player changed or erased the value in «Serie limpia».</summary>
        public bool Changed { get; }

        /// <summary>Why the month was right, in a full sentence.</summary>
        public string Message { get; }
    }

    /// <summary>The detailed review of level 1.2: one row per problem and every good month touched by mistake.</summary>
    public sealed class CleaningReview
    {
        public CleaningReview(IReadOnlyList<IssueReview> issues, IReadOnlyList<GoodDataReview> goodData)
        {
            Issues = issues;
            GoodData = goodData;
        }

        public IReadOnlyList<IssueReview> Issues { get; }
        public IReadOnlyList<GoodDataReview> GoodData { get; }

        public bool IsClean => Issues.All(i => i.IsCorrect) && GoodData.Count == 0;
    }
}
