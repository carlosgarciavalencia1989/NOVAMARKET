using NovaMarket.Simulation;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Planning;
using UnityEngine;

namespace NovaMarket.UI
{
    /// <summary>What the HUD state starts from.</summary>
    public enum HudScenario
    {
        /// <summary>Illustrative S&amp;OP numbers with trucks arriving by themselves.</summary>
        SampleOperation,

        /// <summary>"Temporada de fin de año" (v1): the season order plan drives the trucks. Future boss level.</summary>
        SeasonPlanning,

        /// <summary>Planner campaign (v2): start menu, world map and the levels with NOVA ERP and the planner sheet.</summary>
        PlannerCampaign
    }

    /// <summary>
    /// Owns the <see cref="OperationState"/> the HUD displays. It starts from the chosen
    /// <see cref="HudScenario"/>; the CEDI operation (<c>CediOperationRunner</c>) and the level
    /// controllers (<see cref="PlanningLevelController"/>, <c>CampaignController</c>) then update
    /// the same state. The state is created on first use, so other components can read it from Awake.
    /// </summary>
    public class HudDataSource : MonoBehaviour
    {
        [SerializeField] private HudScenario scenario = HudScenario.PlannerCampaign;
        [Tooltip("Shows a welcome notice when Play starts.")]
        [SerializeField] private bool showWelcome = true;

        private OperationState state;

        public HudScenario Scenario => scenario;

        public OperationState State => state ??= scenario switch
        {
            HudScenario.SeasonPlanning => PlanningLevelData.CreateState(),
            HudScenario.PlannerCampaign => CampaignHudData.CreateState(),
            _ => SampleOperationData.Create()
        };

        /// <summary>Clock used for notices, in seconds (unaffected by time scale).</summary>
        public double Now => Time.unscaledTimeAsDouble;

        private void Start()
        {
            // The campaign greets the player from its own start menu.
            if (showWelcome && scenario != HudScenario.PlannerCampaign)
                State.Notifications.Push("Bienvenido al CEDI Medellín. Julián te espera en la bodega.",
                    NotificationSeverity.Info, Now);
        }

        private void Update() => State.Notifications.Tick(Now);
    }
}
