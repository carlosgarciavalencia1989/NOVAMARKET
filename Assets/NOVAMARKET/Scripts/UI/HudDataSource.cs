using NovaMarket.Simulation;
using UnityEngine;

namespace NovaMarket.UI
{
    /// <summary>
    /// Owns the <see cref="OperationState"/> the HUD displays. It starts from
    /// <see cref="SampleOperationData"/>; the CEDI operation (<c>CediOperationRunner</c>) then
    /// updates the same state as trucks and forklifts work, and the HUD follows it.
    /// </summary>
    public class HudDataSource : MonoBehaviour
    {
        [Tooltip("Shows a welcome notice when Play starts.")]
        [SerializeField] private bool showWelcome = true;

        public OperationState State { get; private set; }

        /// <summary>Clock used for notices, in seconds (unaffected by time scale).</summary>
        public double Now => Time.unscaledTimeAsDouble;

        private void Awake() => State = SampleOperationData.Create();

        private void Start()
        {
            if (showWelcome)
                State.Notifications.Push("Bienvenido al CEDI Medellín. Julián te espera en la bodega.",
                    NotificationSeverity.Info, Now);
        }

        private void Update() => State.Notifications.Tick(Now);
    }
}
