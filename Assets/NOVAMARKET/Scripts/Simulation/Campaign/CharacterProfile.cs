using System;

namespace NovaMarket.Simulation.Campaign
{
    /// <summary>
    /// A character of the supply chain: real job, incentive and bias. Characters whose
    /// <see cref="LinkedRoleId"/> is not the player's role are played by the game (NPC); when that
    /// role becomes playable the player can take their place without changing the engine.
    /// </summary>
    public sealed class CharacterProfile
    {
        public const int MinTrust = 0;
        public const int MaxTrust = 100;

        /// <param name="linkedRoleId">Role this character performs (null for mentors, clients and suppliers).</param>
        /// <param name="biasMin">Smallest over-estimate of what the character says (0.05 = +5 %; negative = under).</param>
        /// <param name="biasMax">Largest over-estimate.</param>
        public CharacterProfile(string id, string name, string job, string linkedRoleId, string incentive,
            string personality, double biasMin = 0, double biasMax = 0, int initialTrust = 50)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A character needs an id.", nameof(id));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A character needs a name.", nameof(name));
            if (biasMax < biasMin) throw new ArgumentException("biasMax must be at least biasMin.");
            if (initialTrust < MinTrust || initialTrust > MaxTrust) throw new ArgumentOutOfRangeException(nameof(initialTrust));
            Id = id;
            Name = name;
            Job = job ?? string.Empty;
            LinkedRoleId = string.IsNullOrEmpty(linkedRoleId) ? null : linkedRoleId;
            Incentive = incentive ?? string.Empty;
            Personality = personality ?? string.Empty;
            BiasMin = biasMin;
            BiasMax = biasMax;
            InitialTrust = initialTrust;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Job shown under the name ("Director de S&amp;OP").</summary>
        public string Job { get; }

        public string LinkedRoleId { get; }
        public string Incentive { get; }
        public string Personality { get; }
        public double BiasMin { get; }
        public double BiasMax { get; }
        public int InitialTrust { get; }

        public bool HasBias => BiasMin != 0 || BiasMax != 0;

        /// <summary>Badge initials: first letters of the first two words ("Don Hernán" → "DH").</summary>
        public string Initials
        {
            get
            {
                string[] words = Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return words.Length > 1 && words[0] == "Don"
                    ? $"{words[0][0]}{words[1][0]}"
                    : words[0].Substring(0, 1);
            }
        }
    }
}
