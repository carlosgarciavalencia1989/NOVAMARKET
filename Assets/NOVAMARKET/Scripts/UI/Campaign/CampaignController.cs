using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Levels;
using NovaMarket.UI.Erp;
using NovaMarket.UI.Sheet;
using UnityEngine;
using UnityEngine.UIElements;
using PortfolioModel = NovaMarket.Simulation.Portfolio.Portfolio;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// Runs the planner campaign (v2) in Play mode: start menu (role and sector) → world map → level,
    /// with the NOVA ERP window and the planner sheet. The level rules are the plain C# classes of
    /// <c>NovaMarket.Simulation.Levels</c>; this component only wires them to the UI and the HUD.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CampaignController : MonoBehaviour
    {
        [SerializeField] private HudDataSource dataSource;
        [SerializeField] private CampaignDatabase campaign;
        [SerializeField] private StrategyCameraController cameraController;
        [Tooltip("0 = a new random seed for every level. Any other value always plays that seed (useful to test).")]
        [SerializeField] private int fixedSeed;

        private readonly System.Random seeds = new System.Random();
        private VisualElement hudRoot;
        private StartMenuView menu;
        private WorldMapView map;
        private WorkspaceBarView bar;
        private ErpWindowView erp;
        private SheetWindowView sheet;
        private VisualElement menuLayer;
        private VisualElement windowLayer;
        private RoleData role;
        private SectorData sector;
        private PortfolioModel portfolio;
        private WorkspaceWindow window;

        /// <summary>The level being played, or null in the menus.</summary>
        public LevelSession Session { get; private set; }

        private MissionState Mission => dataSource.State.Mission;

        private void Start()
        {
            if (dataSource == null || dataSource.Scenario != HudScenario.PlannerCampaign || campaign == null)
            {
                enabled = false;
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            hudRoot = root.Q("hudRoot");
            menuLayer = root.Q("menuLayer");
            windowLayer = root.Q("windowLayer");

            menu = new StartMenuView(menuLayer, campaign, OnRoleAndSectorChosen);
            map = new WorldMapView(menuLayer, campaign, StartLevel, ShowMenu);
            bar = new WorkspaceBarView(root.Q("workspaceBar"), () => Toggle(WorkspaceWindow.Erp),
                () => Toggle(WorkspaceWindow.Sheet), ShowMap);
            erp = new ErpWindowView(windowLayer, () => Open(WorkspaceWindow.None), OpenSheet);
            sheet = new SheetWindowView(windowLayer, () => Open(WorkspaceWindow.None));

            ShowMenu();
        }

        private void Update()
        {
            if (cameraController != null)
                cameraController.InputSuspended = !menuLayer.ClassListContains("hidden") || window != WorkspaceWindow.None;
        }

        // ------------------------------------------------------------------ menus

        private void ShowMenu()
        {
            EndLevel();
            Mission.Reset("NOVAMARKET · Escoge tu rol y tu sector", CampaignHudData.StepsLabel, 0);
            menu.Show();
            SetMenuMode(true);
        }

        private void OnRoleAndSectorChosen(RoleData chosenRole, SectorData chosenSector)
        {
            if (chosenSector != sector || portfolio == null) portfolio = chosenSector.CreatePortfolio();
            role = chosenRole;
            sector = chosenSector;
            ShowMap();
        }

        private void ShowMap()
        {
            if (role == null)
            {
                ShowMenu();
                return;
            }
            EndLevel();
            Mission.Reset($"{role.DisplayName} · {sector.DisplayName} · Mundo 1: {ForecastWorld.Name}", "Niveles disponibles", 2);
            menu.Hide();
            map.Show(role.ToDefinition(), sector.ToDefinition());
            SetMenuMode(true);
        }

        private void SetMenuMode(bool on)
        {
            hudRoot.EnableInClassList("hud-root--menu", on);
            HudElements.SetVisible(menuLayer, on);
        }

        // ------------------------------------------------------------------ level

        private void StartLevel(LevelInfo level)
        {
            if (!level.IsBuilt) return;
            int seed = fixedSeed != 0 ? fixedSeed : seeds.Next(1, 1_000_000);
            Session = level == ForecastWorld.GatherHistory
                ? new GatherHistoryLevel(portfolio, seed)
                : new CleanDataLevel(portfolio, seed);

            Session.Book.Changed += RefreshObjectives;
            Session.Said += Say;
            if (Session is CleanDataLevel clean) clean.MarksChanged += RefreshObjectives;
            CampaignHudData.StartLevel(Mission, Session);

            erp.Bind(Session);
            sheet.Bind(Session.Book, $"Planeacion_{sector.DisplayName}_Nivel_{level.Id}");
            map.Hide();
            SetMenuMode(false);
            bar.Show($"Nivel {level.Id} · {level.Title}");
            foreach (DialogueLine line in Session.Intro) Say(line);
            Open(WorkspaceWindow.None);
        }

        private void EndLevel()
        {
            Open(WorkspaceWindow.None);
            bar?.Hide();
            if (Session == null) return;
            Session.Book.Changed -= RefreshObjectives;
            Session.Said -= Say;
            if (Session is CleanDataLevel clean) clean.MarksChanged -= RefreshObjectives;
            Session = null;
        }

        private void RefreshObjectives()
        {
            if (Session != null) CampaignHudData.Refresh(Mission, Session);
        }

        /// <summary>Character lines go to the notices for now (the dialogue window comes with the level flow).</summary>
        private void Say(DialogueLine line)
        {
            CharacterData character = campaign.FindCharacter(line.CharacterId);
            string name = character != null ? character.DisplayName : line.CharacterId;
            dataSource.State.Notifications.Push($"{name}: {line.Text}",
                line.Mood == DialogueMood.Worried || line.Mood == DialogueMood.Serious ? NotificationSeverity.Warning : NotificationSeverity.Info,
                dataSource.Now);
        }

        // ------------------------------------------------------------------ windows

        private void Toggle(WorkspaceWindow target) => Open(window == target ? WorkspaceWindow.None : target);

        private void OpenSheet(string sheetName)
        {
            Open(WorkspaceWindow.Sheet);
            sheet.Show(sheetName);
        }

        private void Open(WorkspaceWindow target)
        {
            if (erp == null) return;
            window = Session == null ? WorkspaceWindow.None : target;
            if (window == WorkspaceWindow.Erp) erp.Show();
            else erp.Hide();
            if (window == WorkspaceWindow.Sheet) sheet.Show();
            else sheet.Hide();
            HudElements.SetVisible(windowLayer, window != WorkspaceWindow.None);
            hudRoot.EnableInClassList("hud-root--window", window != WorkspaceWindow.None);
            bar.SetActive(window);
        }
    }
}
