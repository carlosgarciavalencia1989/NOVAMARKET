using System.Collections.Generic;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>
    /// "Pedir ayuda al Atlas": shows the step solved, but it costs the "sin ayuda del Atlas" star and
    /// stays in the level report.
    /// </summary>
    public sealed class AtlasHelpLog
    {
        private readonly List<string> topics = new List<string>();

        public IReadOnlyList<string> Topics => topics;
        public bool Used => topics.Count > 0;

        public void Record(string topic) => topics.Add(topic);
    }

    /// <summary>The solved explanation the Atlas gives for a topic.</summary>
    public sealed class AtlasAnswer
    {
        public AtlasAnswer(string title, IReadOnlyList<string> steps)
        {
            Title = title;
            Steps = steps;
        }

        public string Title { get; }
        public IReadOnlyList<string> Steps { get; }
    }
}
