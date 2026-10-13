using NovaMarket.Simulation.Levels;

namespace NovaMarket.Tests.Simulation.Levels
{
    /// <summary>A small dialogue book for the level tests: the intros have the shape of the real data files.</summary>
    public static class TestDialogue
    {
        public static DialogueBook Book { get; } = new DialogueBook(new[]
        {
            new DialogueEntry("1.1", "andres", DialogueKeys.Intro, 1, DialogueMood.Neutral, "Bienvenido."),
            new DialogueEntry("1.1", "sofia", DialogueKeys.Intro, 2, DialogueMood.Neutral, "Necesito {codes}."),
            new DialogueEntry("1.1", "andres", DialogueKeys.Intro, 3, DialogueMood.Neutral, "Un SKU por fila."),
            new DialogueEntry("1.2", "sofia", DialogueKeys.Intro, 1, DialogueMood.Worried, "Te pasé {skus} SKU.")
        });
    }
}
