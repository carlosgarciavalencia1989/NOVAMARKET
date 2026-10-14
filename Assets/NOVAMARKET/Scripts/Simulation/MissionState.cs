using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// State of the current level: title, main counter (e.g. completed dispatches), objectives
    /// and score. Raises <see cref="Changed"/> after every modification so the HUD can refresh.
    /// </summary>
    public sealed class MissionState
    {
        private readonly List<Objective> objectives = new List<Objective>();

        public MissionState(int levelNumber, string levelTitle, string counterLabel, int counterTarget)
        {
            LevelNumber = levelNumber;
            LevelTitle = levelTitle;
            CounterLabel = counterLabel;
            CounterTarget = counterTarget;
        }

        public event Action Changed;

        /// <summary>Raised once when an objective becomes completed.</summary>
        public event Action<Objective> ObjectiveCompleted;

        public int LevelNumber { get; private set; }
        public string LevelTitle { get; private set; }

        /// <summary>Header text, e.g. "Nivel III: Planeación S&amp;OP" (or the header given to <see cref="Reset"/>).</summary>
        public string LevelHeader => header ?? $"Nivel {RomanNumerals.From(LevelNumber)}: {LevelTitle}";

        private string header;

        /// <summary>
        /// Starts another level in the same HUD: new header, counter and no objectives. The score is
        /// kept (it is the player's total).
        /// </summary>
        public void Reset(string levelHeader, string counterLabel, int counterTarget)
        {
            header = levelHeader;
            LevelTitle = levelHeader;
            CounterLabel = counterLabel;
            CounterTarget = Math.Max(0, counterTarget);
            CounterValue = 0;
            objectives.Clear();
            Changed?.Invoke();
        }

        public string CounterLabel { get; private set; }
        public int CounterTarget { get; private set; }
        public int CounterValue { get; private set; }
        public int Score { get; private set; }

        public IReadOnlyList<Objective> Objectives => objectives;

        public bool AllPrimaryCompleted => objectives.TrueForAll(o => !o.IsPrimary || o.IsCompleted);

        public Objective AddObjective(Objective objective)
        {
            if (Find(objective.Id) != null)
                throw new ArgumentException($"Objective '{objective.Id}' already exists.", nameof(objective));
            objectives.Add(objective);
            Changed?.Invoke();
            return objective;
        }

        public Objective Find(string id) => objectives.Find(o => o.Id == id);

        /// <summary>Sets the main counter, clamped to 0..CounterTarget.</summary>
        public void SetCounter(int value)
        {
            CounterValue = Math.Max(0, Math.Min(CounterTarget, value));
            Changed?.Invoke();
        }

        public void AddScore(int points) => SetScore(Score + points);

        /// <summary>Replaces the score (the campaign shows the total of the best results of its levels).</summary>
        public void SetScore(int points)
        {
            Score = Math.Max(0, points);
            Changed?.Invoke();
        }

        /// <summary>Sets an objective's progress; awards its points the moment it completes.</summary>
        public void SetObjectiveProgress(string id, int progress)
        {
            Objective objective = Find(id) ?? throw new ArgumentException($"Unknown objective '{id}'.", nameof(id));
            if (objective.SetProgress(progress))
            {
                Score += objective.Points;
                ObjectiveCompleted?.Invoke(objective);
            }
            Changed?.Invoke();
        }

        public void AdvanceObjective(string id, int amount = 1)
        {
            Objective objective = Find(id) ?? throw new ArgumentException($"Unknown objective '{id}'.", nameof(id));
            SetObjectiveProgress(id, objective.Progress + amount);
        }
    }
}
