using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaMarket.Simulation.Campaign
{
    /// <summary>
    /// Every role, sector and character of the game, loaded from data. Answers what can be played
    /// and builds the cast of NPCs for the chosen role.
    /// </summary>
    public sealed class CampaignCatalog
    {
        public CampaignCatalog(IEnumerable<RoleDefinition> roles, IEnumerable<SectorDefinition> sectors,
            IEnumerable<CharacterProfile> characters)
        {
            Roles = (roles ?? throw new ArgumentNullException(nameof(roles))).ToList();
            Sectors = (sectors ?? throw new ArgumentNullException(nameof(sectors))).ToList();
            Characters = (characters ?? throw new ArgumentNullException(nameof(characters))).ToList();
        }

        public IReadOnlyList<RoleDefinition> Roles { get; }
        public IReadOnlyList<SectorDefinition> Sectors { get; }
        public IReadOnlyList<CharacterProfile> Characters { get; }

        public RoleDefinition Role(string id) => Roles.FirstOrDefault(r => r.Id == id);
        public SectorDefinition Sector(string id) => Sectors.FirstOrDefault(s => s.Id == id);
        public CharacterProfile Character(string id) => Characters.FirstOrDefault(c => c.Id == id);

        /// <summary>True when both the role and the sector exist and are enabled.</summary>
        public bool CanPlay(string roleId, string sectorId) =>
            Role(roleId)?.IsPlayable == true && Sector(sectorId)?.IsPlayable == true;

        /// <summary>The characters the game plays for <paramref name="playerRoleId"/>: everyone except
        /// the characters that perform the player's own role.</summary>
        public IReadOnlyList<NpcCharacter> CreateCast(string playerRoleId) =>
            Characters.Where(c => c.LinkedRoleId != playerRoleId).Select(c => new NpcCharacter(c)).ToList();

        /// <summary>Data errors (duplicated ids, links to unknown roles); empty when the data is valid.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            AddDuplicates(errors, "role", Roles.Select(r => r.Id));
            AddDuplicates(errors, "sector", Sectors.Select(s => s.Id));
            AddDuplicates(errors, "character", Characters.Select(c => c.Id));
            foreach (CharacterProfile character in Characters)
                if (character.LinkedRoleId != null && Role(character.LinkedRoleId) == null)
                    errors.Add($"Character '{character.Id}' is linked to unknown role '{character.LinkedRoleId}'.");
            foreach (RoleDefinition role in Roles)
                if (role.IsPlayable && role.Worlds.Count == 0)
                    errors.Add($"Playable role '{role.Id}' has no worlds.");
            return errors;
        }

        private static void AddDuplicates(List<string> errors, string kind, IEnumerable<string> ids)
        {
            foreach (IGrouping<string, string> group in ids.GroupBy(id => id).Where(g => g.Count() > 1))
                errors.Add($"Duplicated {kind} id '{group.Key}'.");
        }
    }
}
