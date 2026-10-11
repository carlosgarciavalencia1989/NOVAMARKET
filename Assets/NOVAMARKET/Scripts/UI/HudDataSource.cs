using System.Collections;
using NovaMarket.Simulation;
using UnityEngine;

namespace NovaMarket.UI
{
    /// <summary>
    /// Owns the <see cref="OperationState"/> the HUD displays. For now it loads
    /// <see cref="SampleOperationData"/> and, optionally, plays a short scripted demo (notices,
    /// dispatches, resource changes). When the simulation exists it will provide the state here
    /// and the HUD will keep working unchanged.
    /// </summary>
    public class HudDataSource : MonoBehaviour
    {
        [Tooltip("Plays example events so the HUD can be seen changing in Play mode.")]
        [SerializeField] private bool playDemoEvents = true;
        [SerializeField] private int pointsPerDispatch = 150;

        public OperationState State { get; private set; }

        /// <summary>Clock used for notices, in seconds (unaffected by time scale).</summary>
        public double Now => Time.unscaledTimeAsDouble;

        private void Awake() => State = SampleOperationData.Create();

        private void Start()
        {
            if (playDemoEvents) StartCoroutine(PlayDemo());
        }

        private void Update() => State.Notifications.Tick(Now);

        private IEnumerator PlayDemo()
        {
            NotificationFeed notices = State.Notifications;
            ResourceState resources = State.Resources;

            yield return new WaitForSecondsRealtime(1.5f);
            notices.Push("Bienvenido al CEDI Medellín. Julián te espera en la bodega.", NotificationSeverity.Info, Now);

            yield return new WaitForSecondsRealtime(5f);
            notices.Push("Camión de Don Hernán en la portería con un contenedor importado.", NotificationSeverity.Warning, Now);
            resources.SetTrucksInYard(resources.TrucksInYard + 1);

            yield return new WaitForSecondsRealtime(6f);
            Dispatch();

            yield return new WaitForSecondsRealtime(7f);
            resources.SetServiceLevel(0.947);
            notices.Push("Nivel de servicio en riesgo: 2 pedidos atrasados.", NotificationSeverity.Critical, Now);

            while (State.Mission.CounterValue < State.Mission.CounterTarget)
            {
                yield return new WaitForSecondsRealtime(14f);
                Dispatch();
                resources.SetServiceLevel(System.Math.Min(0.99, resources.ServiceLevel + 0.004));
            }
        }

        private void Dispatch()
        {
            ResourceState resources = State.Resources;
            State.RegisterDispatch(SampleOperationData.DispatchObjectiveId, pointsPerDispatch, Now);
            resources.SetOccupiedPositions(resources.OccupiedPositions - 3);
            resources.SetTrucksInYard(System.Math.Max(1, resources.TrucksInYard - 1));
            resources.SetBudget(resources.Budget + 4_500_000);
        }
    }
}
