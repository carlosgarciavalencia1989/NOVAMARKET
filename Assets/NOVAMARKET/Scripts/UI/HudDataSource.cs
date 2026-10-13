using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using UnityEngine;

namespace NovaMarket.UI
{
    /// <summary>What the HUD state starts from.</summary>
    public enum HudScenario
    {
        /// <summary>Illustrative S&amp;OP numbers with trucks arriving by themselves.</summary>
        SampleOperation,

        /// <summary>First playable level: the season order plan drives the trucks.</summary>
        SeasonPlanning
    }

    /// <summary>
    /// Owns the <see cref="OperationState"/> the HUD displays. It starts from the chosen
    /// <see cref="HudScenario"/>; the CEDI operation (<c>CediOperationRunner</c>) and, in the
    /// planning level, <see cref="PlanningLevelController"/> then update the same state.
    /// The state is created on first use, so other components can read it from Awake.
    /// </summary>
    public class HudDataSource : MonoBehaviour
    {
        [SerializeField] private HudScenario scenario = HudScenario.SeasonPlanning;
        [Tooltip("Shows a welcome notice when Play starts.")]
        [SerializeField] private bool showWelcome = true;

        private OperationState state;

        public HudScenario Scenario => scenario;

        public OperationState State => state ??= scenario == HudScenario.SeasonPlanning
            ? PlanningLevelData.CreateState()
            : SampleOperationData.Create();

        /// <summary>Clock used for notices, in seconds (unaffected by time scale).</summary>
        public double Now => Time.unscaledTimeAsDouble;

        private void Start()
        {
            if (showWelcome)
                State.Notifications.Push("Bienvenido al CEDI Medellín. Julián te espera en la bodega.",
                    NotificationSeverity.Info, Now);
        }

        private void Update() => State.Notifications.Tick(Now);
    }
}
