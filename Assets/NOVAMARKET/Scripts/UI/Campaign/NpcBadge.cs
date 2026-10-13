using NovaMarket.Data;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>Round badge of a character built from its data (colour and initials) until there is character art.</summary>
    public static class NpcBadge
    {
        public static VisualElement Create(CharacterData character, bool small = false)
        {
            VisualElement badge = HudElements.Box("npc-badge");
            if (small) badge.AddToClassList("npc-badge--small");
            badge.style.backgroundColor = character.BadgeColor;
            badge.Add(HudElements.Text(character.ToProfile().Initials, "npc-initials"));
            return badge;
        }

        /// <summary>Badge plus name and job, for cast lists.</summary>
        public static VisualElement Member(CharacterData character)
        {
            VisualElement member = HudElements.Box("cast-member");
            member.Add(Create(character, small: true));
            VisualElement texts = HudElements.Box("cast-texts");
            texts.Add(HudElements.Text(character.DisplayName, "cast-name"));
            texts.Add(HudElements.Text(character.ToProfile().Job, "cast-job"));
            member.Add(texts);
            return member;
        }
    }
}
