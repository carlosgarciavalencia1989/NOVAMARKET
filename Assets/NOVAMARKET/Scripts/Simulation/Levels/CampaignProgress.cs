using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NovaMarket.Simulation.Levels
{
    /// <summary>The best result of a level: stars, points and whether its gem was earned.</summary>
    public sealed class LevelRecord
    {
        public LevelRecord(string levelId, int stars, int points, bool gem)
        {
            LevelId = levelId;
            Stars = stars;
            Points = points;
            Gem = gem;
        }

        public string LevelId { get; }
        public int Stars { get; }
        public int Points { get; }
        public bool Gem { get; }
    }

    /// <summary>
    /// What the player has achieved in the campaign: the best result of each level. A worse try never
    /// lowers it. Saved as a short text ("1.1:3:700:1;1.2:2:600:1") so the scene can keep it between games.
    /// </summary>
    public sealed class CampaignProgress
    {
        private readonly Dictionary<string, LevelRecord> records = new Dictionary<string, LevelRecord>();

        public event Action Changed;

        public LevelRecord Best(string levelId) => records.TryGetValue(levelId, out LevelRecord record) ? record : null;

        public int TotalPoints => records.Values.Sum(r => r.Points);
        public int Gems => records.Values.Count(r => r.Gem);

        /// <summary>Keeps the result if it beats the best one. Returns true when it is a new best.</summary>
        public bool Record(LevelResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            LevelRecord best = Best(result.Level.Id);
            if (best != null && (best.Stars > result.Stars || best.Stars == result.Stars && best.Points >= result.Points))
                return false;
            records[result.Level.Id] = new LevelRecord(result.Level.Id, result.Stars, result.Points, result.EarnedGem || best?.Gem == true);
            Changed?.Invoke();
            return true;
        }

        public string Serialize() => string.Join(";", records.Values.OrderBy(r => r.LevelId, StringComparer.Ordinal)
            .Select(r => $"{r.LevelId}:{r.Stars}:{r.Points.ToString(CultureInfo.InvariantCulture)}:{(r.Gem ? 1 : 0)}"));

        /// <summary>Reads a saved text; unreadable parts are skipped.</summary>
        public static CampaignProgress Parse(string text)
        {
            var progress = new CampaignProgress();
            foreach (string part in (text ?? string.Empty).Split(';'))
            {
                string[] fields = part.Split(':');
                if (fields.Length != 4 || fields[0].Length == 0) continue;
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stars)) continue;
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int points)) continue;
                progress.records[fields[0]] = new LevelRecord(fields[0], Math.Max(0, Math.Min(3, stars)), points, fields[3] == "1");
            }
            return progress;
        }
    }
}
