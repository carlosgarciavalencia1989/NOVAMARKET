using NovaMarket.Simulation;

namespace NovaMarket.Interaction
{
    /// <summary>
    /// Implemented by selectable objects whose panel data changes while selected (e.g. a truck
    /// unloading). The HUD asks for fresh details every frame.
    /// </summary>
    public interface ISelectionDetailsProvider
    {
        SelectionDetails GetDetails();
    }
}
