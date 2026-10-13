using NovaMarket.Simulation.Campaign;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// A character of the supply chain. Only data: the logic (trust, dialogues, negotiation) lives
    /// in plain C# (<see cref="NpcCharacter"/>) and the UI only draws it.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Character", fileName = "Character_")]
    public sealed class CharacterData : NovaDataAsset
    {
        [SerializeField] private string job;
        [SerializeField, Tooltip("Id of the role this character performs; empty for mentors, clients and suppliers.")]
        private string linkedRoleId;
        [SerializeField, TextArea(1, 3)] private string incentive;
        [SerializeField, TextArea(1, 3)] private string personality;
        [SerializeField, Tooltip("Smallest over-estimate of what the character says (0.05 = +5 %).")]
        private float biasMin;
        [SerializeField] private float biasMax;
        [SerializeField, Range(0, 100)] private int initialTrust = 50;
        [SerializeField, Tooltip("Badge colour until there is character art.")] private Color badgeColor = Color.gray;

        public Color BadgeColor => badgeColor;

        public CharacterProfile ToProfile() =>
            new CharacterProfile(Id, DisplayName, job, linkedRoleId, incentive, personality, biasMin, biasMax, initialTrust);
    }
}
