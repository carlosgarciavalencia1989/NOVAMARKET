using NovaMarket.Operation;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Runs the "Temporada de fin de año" level in Play mode: creates the season
    /// (<see cref="PlanningSimulation"/>) and its Atlas reference, shows the order plan panel and
    /// the Atlas, event and result windows, and passes every closed week to
    /// <see cref="PlanningOperationBridge"/>, which sends the trucks and updates the HUD.
    /// All rules live in the plain C# classes; this component only wires them to the UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlanningLevelController : MonoBehaviour
    {
        [SerializeField] private HudDataSource dataSource;
        [SerializeField] private CediOperationRunner operationRunner;
        [Tooltip("0 = a new random season every time. Any other value always plays that seed (useful to test).")]
        [SerializeField] private int fixedSeed;

        private readonly System.Random seeds = new System.Random();
        private PlanningSimulation reference;
        private SeasonResult result;
        private ModalLayer modal;
        private PlanningPanelView panel;
        private AtlasDialogView atlasDialog;
        private EventDialogView eventDialog;
        private SeasonResultView resultView;

        /// <summary>Connection with the operation; null outside the planning level.</summary>
        public PlanningOperationBridge Bridge { get; private set; }

        public PlanningSimulation Simulation { get; private set; }

        private double Now => dataSource.Now;

        private void Start()
        {
            if (dataSource == null || dataSource.Scenario != HudScenario.SeasonPlanning)
            {
                enabled = false;
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            modal = new ModalLayer(root);
            panel = new PlanningPanelView(root, ShowAtlas, OnCloseWeekClicked);
            atlasDialog = new AtlasDialogView(modal, (sku, boxes) => panel.SetOrder(sku, boxes));
            eventDialog = new EventDialogView(modal);
            resultView = new SeasonResultView(modal, StartSeason);

            CediOperationSimulation operation = operationRunner != null ? operationRunner.Simulation : null;
            Bridge = new PlanningOperationBridge(dataSource.State, operation, seeds.Next());
            dataSource.State.OrderIssued += OnOrderIssued;
            StartSeason();
        }

        private void OnDestroy()
        {
            if (Bridge != null) dataSource.State.OrderIssued -= OnOrderIssued;
        }

        /// <summary>Starts a season; a new random seed unless <c>fixedSeed</c> is set.</summary>
        public void StartSeason()
        {
            int seed = fixedSeed != 0 ? fixedSeed : seeds.Next(1, 1_000_000);
            Simulation = new PlanningSimulation(seed);
            reference = PlanningPolicies.PlayReference(seed);
            result = null;

            modal.HideAll();
            Bridge.Begin(Simulation, Now);
            panel.Bind(Simulation);
            panel.SetCollapsed(false);
        }

        private void ShowAtlas(int sku)
        {
            if (Simulation.IsFinished) return;
            atlasDialog.Show(Simulation, sku);
        }

        private void OnCloseWeekClicked()
        {
            if (Simulation.IsFinished)
            {
                ShowResult();
                return;
            }
            if (Simulation.PendingEvent != null)
            {
                ShowEvent(Simulation.PendingEvent);
                return;
            }
            int[] orders = new int[panel.Orders.Count];
            for (int i = 0; i < orders.Length; i++) orders[i] = panel.Orders[i];
            if (Simulation.ExceedsPurchaseCap(orders)) return;

            WeekReport report = Simulation.CloseWeek(orders);
            Bridge.OnWeekClosed(report, Now);
            panel.ClearOrders();
            panel.ShowComment(report);

            if (Simulation.IsFinished) ShowResult();
            else if (Simulation.PendingEvent != null) ShowEvent(Simulation.PendingEvent);
        }

        private void ShowEvent(EventDialog dialog) =>
            eventDialog.Show(dialog, option =>
            {
                Simulation.ResolveEvent(option);
                Bridge.OnEventAnswered(dialog, Now);
                panel.Refresh();
            });

        private void ShowResult()
        {
            if (result == null)
            {
                result = SeasonResult.Evaluate(Simulation, reference);
                Bridge.OnSeasonEnded(result, Now);
                panel.Refresh();
            }
            resultView.Show(result);
        }

        private void OnOrderIssued(string facilityId, string orderId)
        {
            if (orderId == PlanningLevelData.OpenPlanOrderId) panel.SetCollapsed(false);
        }
    }
}
