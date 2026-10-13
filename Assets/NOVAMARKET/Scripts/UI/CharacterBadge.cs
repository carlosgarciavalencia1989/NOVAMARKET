using NovaMarket.Simulation.Planning;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Round portrait placeholder of a character: their initials on their colour, with a ring in
    /// the colour of their mood (green happy, red angry, amber worried, blue surprised).
    /// </summary>
    public sealed class CharacterBadge : VisualElement
    {
        private readonly Label initials;
        private readonly bool large;

        public CharacterBadge(bool large = false)
        {
            this.large = large;
            pickingMode = PickingMode.Ignore;
            initials = new Label();
            initials.AddToClassList("character-initials");
            Add(initials);
            Show(PlanningCharacter.Andres, CharacterMood.Neutral);
        }

        public void Show(PlanningCharacter character, CharacterMood mood)
        {
            ClearClassList();
            AddToClassList("character-badge");
            if (large) AddToClassList("character-badge--large");
            AddToClassList("character--" + character.ToString().ToLowerInvariant());
            AddToClassList("mood--" + mood.ToString().ToLowerInvariant());
            initials.text = PlanningCharacters.Initials(character);
        }
    }
}
