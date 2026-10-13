using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NovaMarket.Simulation.Levels
{
    public enum DialogueMood
    {
        Neutral,
        Happy,
        Worried,
        Serious
    }

    /// <summary>A line said by a character (id of its CharacterData: "andres", "sofia").</summary>
    public sealed class DialogueLine
    {
        private static readonly IReadOnlyDictionary<string, string> NoArgs = new Dictionary<string, string>();

        public DialogueLine(string characterId, DialogueMood mood, string text, string key = null,
            IReadOnlyDictionary<string, string> args = null)
        {
            CharacterId = characterId ?? string.Empty;
            Mood = mood;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Key = key ?? string.Empty;
            Args = args ?? NoArgs;
        }

        public string CharacterId { get; }
        public DialogueMood Mood { get; }
        public string Text { get; }

        /// <summary>Which line of the level it is ("intro", "exito"…), so the logic and the tests never depend on the wording.</summary>
        public string Key { get; }

        /// <summary>The values put into the text ({codes}, {missing}…).</summary>
        public IReadOnlyDictionary<string, string> Args { get; }

        public override string ToString() => $"{CharacterId}: {Text}";
    }

    /// <summary>
    /// A line as written in a dialogue data file: who says it, in which level, for which role and sector
    /// (empty = all), its key, its order among the lines with the same key and the text with {placeholders}.
    /// </summary>
    public sealed class DialogueEntry
    {
        public DialogueEntry(string levelId, string characterId, string key, int order, DialogueMood mood, string text,
            string roleId = null, string sectorId = null)
        {
            LevelId = levelId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            Key = key ?? string.Empty;
            Order = order;
            Mood = mood;
            Text = text ?? string.Empty;
            RoleId = roleId ?? string.Empty;
            SectorId = sectorId ?? string.Empty;
        }

        public string LevelId { get; }
        public string CharacterId { get; }
        public string Key { get; }
        public int Order { get; }
        public DialogueMood Mood { get; }
        public string Text { get; }
        public string RoleId { get; }
        public string SectorId { get; }

        /// <summary>A line for a given sector beats one for any sector; a given role beats any role.</summary>
        public int Specificity => (SectorId.Length > 0 ? 2 : 0) + (RoleId.Length > 0 ? 1 : 0);

        public bool AppliesTo(string roleId, string sectorId) =>
            (RoleId.Length == 0 || RoleId == roleId) && (SectorId.Length == 0 || SectorId == sectorId);
    }

    /// <summary>
    /// Every line the characters can say, read from the dialogue data files (one per character and level).
    /// The levels ask for a line by key with its values; the book fills the {placeholders}. A missing line
    /// shows as "[1.2/key]" so it is easy to spot and the level still works.
    /// </summary>
    public sealed class DialogueBook
    {
        private static readonly Regex Placeholder = new Regex(@"\{([a-zA-Z_]+)\}");

        private readonly Dictionary<(string Level, string Key), List<DialogueEntry>> entries =
            new Dictionary<(string, string), List<DialogueEntry>>();

        /// <param name="roleId">Keeps only the lines for this role (or for every role); null keeps all.</param>
        public DialogueBook(IEnumerable<DialogueEntry> lines, string roleId = null, string sectorId = null)
        {
            var chosen = new Dictionary<(string, string, string, int), DialogueEntry>();
            foreach (DialogueEntry entry in lines ?? Enumerable.Empty<DialogueEntry>())
            {
                if (entry == null || roleId != null && !entry.AppliesTo(roleId, sectorId)) continue;
                var slot = (entry.LevelId, entry.Key, entry.CharacterId, entry.Order);
                if (!chosen.TryGetValue(slot, out DialogueEntry current) || entry.Specificity > current.Specificity)
                    chosen[slot] = entry;
            }
            foreach (DialogueEntry entry in chosen.Values)
            {
                if (!entries.TryGetValue((entry.LevelId, entry.Key), out List<DialogueEntry> list))
                    entries[(entry.LevelId, entry.Key)] = list = new List<DialogueEntry>();
                list.Add(entry);
            }
            foreach (List<DialogueEntry> list in entries.Values) list.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public static DialogueBook Empty { get; } = new DialogueBook(null);

        public bool Has(string levelId, string key) => entries.ContainsKey((levelId, key));

        /// <summary>All the lines with this key, in order (an intro is several lines by several characters).</summary>
        public IReadOnlyList<DialogueLine> Lines(string levelId, string key, IReadOnlyDictionary<string, string> args = null)
        {
            if (!entries.TryGetValue((levelId, key), out List<DialogueEntry> list))
                return new[] { new DialogueLine(string.Empty, DialogueMood.Neutral, $"[{levelId}/{key}]", key, args) };
            return list.Select(e => new DialogueLine(e.CharacterId, e.Mood, Fill(e.Text, args), key, args)).ToList();
        }

        public DialogueLine Line(string levelId, string key, IReadOnlyDictionary<string, string> args = null) =>
            Lines(levelId, key, args)[0];

        /// <summary>Replaces each {name} with its value; unknown names stay as they are.</summary>
        public static string Fill(string text, IReadOnlyDictionary<string, string> args) =>
            args == null || args.Count == 0
                ? text
                : Placeholder.Replace(text, m => args.TryGetValue(m.Groups[1].Value, out string value) ? value : m.Value);

        /// <summary>The {placeholders} a text uses.</summary>
        public static IEnumerable<string> PlaceholdersOf(string text) =>
            Placeholder.Matches(text ?? string.Empty).Cast<Match>().Select(m => m.Groups[1].Value);
    }

    /// <summary>The keys of the dialogue lines of world 1 and the values each one can use.</summary>
    public static class DialogueKeys
    {
        public const string Intro = "intro";
        public const string Success = "exito";

        // 1.1
        public const string ProjectionWarning = "aviso-proyeccion";
        public const string WrongQuery = "consulta-incorrecta";
        public const string WrongTotals = "totales-incorrectos";

        // 1.2
        public const string MissingOne = "falta-un-problema";
        public const string MissingMany = "faltan-problemas";
        public const string BadCorrection = "correccion-incorrecta";
        public const string ChangedGoodData = "datos-buenos-cambiados";
        public const string ImpactLevelHigh = "impacto-nivel-alto";
        public const string ImpactLevelLow = "impacto-nivel-bajo";
        public const string ImpactIndexHigh = "impacto-indice-alto";
        public const string ImpactIndexLow = "impacto-indice-bajo";

        /// <summary>A free hint for a help topic (the Atlas solution, instead, costs a star).</summary>
        public static string Hint(string topic) => "pista-" + topic;

        /// <summary>Values the lines of world 1 may use, to check the data files.</summary>
        public static readonly string[] KnownArgs = { "codes", "skus", "missing", "code", "player", "clean", "month" };
    }

    /// <summary>Builds the values of a line: <c>DialogueArgs.Of(("codes", "CV10001, CV10009"))</c>.</summary>
    public static class DialogueArgs
    {
        public static IReadOnlyDictionary<string, string> Of(params (string Name, string Value)[] values)
        {
            var args = new Dictionary<string, string>();
            foreach ((string name, string value) in values) args[name] = value;
            return args;
        }
    }
}
