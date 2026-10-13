namespace NovaMarket.Simulation.Planning
{
    /// <summary>The CEDI team that brings events and comments on the player's weeks.</summary>
    public enum PlanningCharacter
    {
        Valeria,
        DonHernan,
        Julian,
        Marcela,
        Andres,
        Sofia
    }

    /// <summary>How the character feels about the news; the dialog picks its colour from this.</summary>
    public enum CharacterMood
    {
        Neutral,
        Happy,
        Angry,
        Worried,
        Surprised
    }

    /// <summary>Names, roles and mood labels of the characters, in Spanish.</summary>
    public static class PlanningCharacters
    {
        public static string Name(PlanningCharacter character) => character switch
        {
            PlanningCharacter.Valeria => "Valeria",
            PlanningCharacter.DonHernan => "Don Hernán",
            PlanningCharacter.Julian => "Julián",
            PlanningCharacter.Marcela => "Marcela",
            PlanningCharacter.Andres => "Andrés",
            _ => "Sofía"
        };

        public static string Role(PlanningCharacter character) => character switch
        {
            PlanningCharacter.Valeria => "Comercial",
            PlanningCharacter.DonHernan => "Proveedor importado",
            PlanningCharacter.Julian => "Jefe de bodega",
            PlanningCharacter.Marcela => "Gerente financiera",
            PlanningCharacter.Andres => "Director de S&OP",
            _ => "Analista SAP"
        };

        public static string Initials(PlanningCharacter character) => character switch
        {
            PlanningCharacter.DonHernan => "DH",
            _ => Name(character).Substring(0, 1)
        };

        public static string MoodLabel(CharacterMood mood) => mood switch
        {
            CharacterMood.Happy => "feliz",
            CharacterMood.Angry => "enojado",
            CharacterMood.Worried => "preocupado",
            CharacterMood.Surprised => "sorprendido",
            _ => "tranquilo"
        };
    }
}
