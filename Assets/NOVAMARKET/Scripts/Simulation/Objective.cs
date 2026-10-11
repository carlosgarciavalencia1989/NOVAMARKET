using System;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// One mission objective. Primary objectives are required to finish the level; secondary
    /// ones only add score. Objectives with a target above 1 show their progress ("3/8").
    /// </summary>
    public sealed class Objective
    {
        public Objective(string id, string text, bool isPrimary, int target = 1, int points = 0)
        {
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target), target, "Target must be at least 1.");
            Id = id;
            Text = text;
            IsPrimary = isPrimary;
            Target = target;
            Points = points;
        }

        public string Id { get; }
        public string Text { get; }
        public bool IsPrimary { get; }
        public int Target { get; }

        /// <summary>Score awarded when the objective is completed.</summary>
        public int Points { get; }

        public int Progress { get; private set; }
        public bool IsCompleted => Progress >= Target;
        public bool ShowsProgress => Target > 1;

        /// <summary>Sets the progress, clamped to 0..Target. Returns true if it just became completed.</summary>
        internal bool SetProgress(int value)
        {
            bool wasCompleted = IsCompleted;
            Progress = Math.Max(0, Math.Min(Target, value));
            return !wasCompleted && IsCompleted;
        }
    }
}
