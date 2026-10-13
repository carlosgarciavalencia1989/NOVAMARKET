using System;

namespace NovaMarket.Simulation.Campaign
{
    /// <summary>
    /// A character played by the game during a level: its profile plus the trust it has in the
    /// player (0–100). Trust goes up when the player argues with data or keeps agreements and down
    /// when they ignore the character and fail; with low trust the character escalates to Andrés or
    /// shares information later.
    /// </summary>
    public sealed class NpcCharacter
    {
        /// <summary>Below this trust the character stops sharing information early.</summary>
        public const int LowTrust = 30;

        public NpcCharacter(CharacterProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Trust = profile.InitialTrust;
        }

        public CharacterProfile Profile { get; }
        public int Trust { get; private set; }
        public bool HasLowTrust => Trust < LowTrust;

        public event Action<NpcCharacter> TrustChanged;

        /// <summary>Adds <paramref name="delta"/> (negative to lower it), kept within 0–100.</summary>
        public void ChangeTrust(int delta)
        {
            int value = Math.Max(CharacterProfile.MinTrust, Math.Min(CharacterProfile.MaxTrust, Trust + delta));
            if (value == Trust) return;
            Trust = value;
            TrustChanged?.Invoke(this);
        }
    }
}
