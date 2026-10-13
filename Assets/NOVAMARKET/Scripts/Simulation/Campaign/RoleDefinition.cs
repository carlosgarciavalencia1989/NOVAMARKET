using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Campaign
{
    /// <summary>Lane of the Atlas supply chain map a role belongs to.</summary>
    public enum ChainLane
    {
        Plan,
        Execute,
        Return
    }

    /// <summary>
    /// A playable role: one link of the Atlas supply chain map, with what it covers and its
    /// worlds. Roles that are not playable yet are shown as "Próximamente" and their characters
    /// are played by the game.
    /// </summary>
    public sealed class RoleDefinition
    {
        public RoleDefinition(string id, string name, ChainLane lane, string covers, bool isPlayable,
            IReadOnlyList<string> worlds = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A role needs an id.", nameof(id));
            Id = id;
            Name = name;
            Lane = lane;
            Covers = covers ?? string.Empty;
            IsPlayable = isPlayable;
            Worlds = worlds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Name { get; }
        public ChainLane Lane { get; }
        public string Covers { get; }
        public bool IsPlayable { get; }

        /// <summary>World names in play order (for the planner: the PLANEAR modules of the Atlas).</summary>
        public IReadOnlyList<string> Worlds { get; }

        public string LaneName => Lane switch
        {
            ChainLane.Plan => "PLANEAR",
            ChainLane.Execute => "EJECUTAR",
            _ => "RETORNAR"
        };
    }
}
