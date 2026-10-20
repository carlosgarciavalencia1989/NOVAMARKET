using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Sheet;
using NovaMarket.UI.Erp;
using NovaMarket.UI.Sheet;
using UnityEngine;
using UnityEngine.UIElements;
using PortfolioModel = NovaMarket.Simulation.Portfolio.Portfolio;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// Runs the planner campaign (v2) in Play mode: start menu (role and sector) → world map → level,
    /// with the NOVA ERP window, the planner sheet, the characters' dialogue box, problem marks and
    /// formula cards (1.2), the Atlas help, the delivery and the result screen. The level rules are the
    /// plain C# classes of <c>NovaMarket.Simulation.Levels</c>; this component only wires them to the UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CampaignController : MonoBehaviour
    {
        private const string ProgressKey = "NOVAMARKET.CampaignProgress";

        [SerializeField] private HudDataSource dataSource;
        [SerializeField] private CampaignDatabase campaign;
        [SerializeField] private StrategyCameraController cameraController;
        [Tooltip("0 = a new random seed for every level. Any other value always plays that seed (useful to test).")]
        [SerializeField] private int fixedSeed;
        [Tooltip("Keep the stars won between games (saved in PlayerPrefs).")]
        [SerializeField] private bool saveProgress = true;

        private readonly System.Random seeds = new System.Random();
        private VisualElement hudRoot;
        private StartMenuView menu;
        private WorldMapView map;
        private WorkspaceBarView bar;
        private ErpWindowView erp;
        private SheetWindowView sheet;
        private ModalLayer modal;
        private DialogueBoxView dialogue;
        private IssueMarkView markView;
        private FormulaCardView cardView;
        private AtlasHelpView helpView;
        private LevelResultView resultView;
        private VisualElement menuLayer;
        private VisualElement windowLayer;
        private RoleData role;
        private SectorData sector;
        private PortfolioModel portfolio;
        private LevelInfo level;
        private WorkspaceWindow window;
        private Dictionary<(string Code, int Month), string> reviewNotes;

        /// <summary>The level being played, or null in the menus.</summary>
        public LevelSession Session { get; private set; }

        public CampaignProgress Progress { get; private set; } = new CampaignProgress();

        // The views, for the Play tests that click through a level.
        public DialogueBoxView Dialogue => dialogue;
        public IssueMarkView MarkView => markView;
        public FormulaCardView CardView => cardView;
        public AtlasHelpView HelpView => helpView;
        public LevelResultView ResultView => resultView;
        public SheetWindowView SheetWindow => sheet;

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
            modal = new ModalLayer(root);

            if (saveProgress) Progress = CampaignProgress.Parse(PlayerPrefs.GetString(ProgressKey, string.Empty));
            Progress.Changed += OnProgressChanged;
            Mission.SetScore(Progress.TotalPoints);

            menu = new StartMenuView(menuLayer, campaign, OnRoleAndSectorChosen);
            map = new WorldMapView(menuLayer, campaign, StartLevel, ShowMenu) { Progress = Progress };
            bar = new WorkspaceBarView(root.Q("workspaceBar"), () => Toggle(WorkspaceWindow.Erp),
                () => Toggle(WorkspaceWindow.Sheet), () => dialogue.Reopen(), OpenHelp, Deliver, ShowMap);
            erp = new ErpWindowView(windowLayer, () => Open(WorkspaceWindow.None), OpenSheet);
            sheet = new SheetWindowView(windowLayer, () => Open(WorkspaceWindow.None));

            // The dialogue box floats over the windows but under the menus and the modal dialogs.
            VisualElement dialogueLayer = HudElements.Box("dialogue-layer");
            dialogueLayer.pickingMode = PickingMode.Ignore;
            hudRoot.Insert(hudRoot.IndexOf(menuLayer), dialogueLayer);
            dialogue = new DialogueBoxView(dialogueLayer, campaign);

            markView = new IssueMarkView(modal);
            cardView = new FormulaCardView(modal);
            helpView = new AtlasHelpView(modal);
            resultView = new LevelResultView(modal, campaign, () => StartLevel(level), ShowMap, KeepWorking);

            ShowMenu();
        }

        private void Update()
        {
            if (cameraController != null)
                cameraController.InputSuspended = !menuLayer.ClassListContains("hidden") || window != WorkspaceWindow.None || modal.IsOpen;
        }

        private void OnProgressChanged()
        {
            // The score of the objectives panel is the total of the best result of each level.
            Mission.SetScore(Progress.TotalPoints);
            if (!saveProgress) return;
            PlayerPrefs.SetString(ProgressKey, Progress.Serialize());
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ menus

        private void ShowMenu()
        {
            EndLevel();
            Mission.Reset("Supply Chain Run · Escoge tu rol y tu sector", CampaignHudData.StepsLabel, 0);
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

        private void StartLevel(LevelInfo chosen)
        {
            if (chosen == null || !chosen.IsBuilt) return;
            EndLevel();
            level = chosen;
            int seed = fixedSeed != 0 ? fixedSeed : seeds.Next(1, 1_000_000);
            DialogueBook lines = campaign.DialoguesFor(role, sector);
            Session = chosen == ForecastWorld.GatherHistory
                ? new GatherHistoryLevel(portfolio, seed, lines)
                : new CleanDataLevel(portfolio, seed, lines);

            Session.Book.Changed += RefreshObjectives;
            Session.Said += dialogue.Say;
            if (Session is CleanDataLevel clean)
            {
                clean.MarksChanged += OnMarksChanged;
                sheet.SetLevelTool("Marcar problema", OpenMark);
                sheet.AddLevelTool("Quitar marca", RemoveMark);
                sheet.Grid.IsMarked = (tab, cell) =>
                    CleanDataLevel.TryLocate(tab, cell, out string code, out int month) && clean.MarkAt(code, month) != null;
                sheet.Grid.IsFlagged = (tab, cell) => ReviewNote(tab, cell) != null;
                sheet.CellNote = ReviewNote;
            }
            CampaignHudData.StartLevel(Mission, Session);

            erp.Bind(Session);
            sheet.Bind(Session.Book, $"Planeacion_{sector.DisplayName}_Nivel_{chosen.Id}");
            map.Hide();
            SetMenuMode(false);
            bar.Show($"Nivel {chosen.Id} · {chosen.Title}");
            Open(WorkspaceWindow.None);
            foreach (DialogueLine line in Session.Intro) dialogue.Say(line);
        }

        private void EndLevel()
        {
            Open(WorkspaceWindow.None);
            bar?.Hide();
            modal?.HideAll();
            dialogue?.Clear();
            if (Session == null) return;
            Session.Book.Changed -= RefreshObjectives;
            Session.Said -= dialogue.Say;
            if (Session is CleanDataLevel clean) clean.MarksChanged -= OnMarksChanged;
            sheet.SetLevelTool(null, null);
            sheet.Grid.IsMarked = null;
            sheet.Grid.IsFlagged = null;
            sheet.CellNote = null;
            sheet.OriginalDataSheet = null;
            reviewNotes = null;
            Session = null;
        }

        private void RefreshObjectives()
        {
            if (Session == null) return;
            CampaignHudData.Refresh(Mission, Session);
            if (Session is CleanDataLevel clean)
            {
                sheet.OriginalDataSheet = clean.RawSheet;
                if (reviewNotes != null) reviewNotes = clean.ReviewNotes();
            }
        }

        private void OnMarksChanged()
        {
            RefreshObjectives();
            sheet.RefreshNotes();
        }

        /// <summary>After a delivery of 1.2: why Andrés flagged a cell of the sheet, or null.</summary>
        private string ReviewNote(Worksheet tab, CellAddress cell) =>
            reviewNotes != null && CleanDataLevel.TryLocate(tab, cell, out string code, out int month)
            && reviewNotes.TryGetValue((code, month), out string note) ? note : null;

        private void Notice(string message, NotificationSeverity severity) =>
            dataSource.State.Notifications.Push(message, severity, dataSource.Now);

        // ------------------------------------------------------------------ marks, cards, help and delivery

        /// <summary>"Marcar problema" on the active cell of the sheet (level 1.2).</summary>
        public void OpenMark()
        {
            if (!(Session is CleanDataLevel clean)) return;
            Worksheet tab = sheet.Grid.Sheet;
            CellRange selection = sheet.Grid.Selection;
            List<(string Code, int Month)> cells = CleanDataLevel.Locate(tab, selection);
            if (cells.Count == 0)
            {
                sheet.SetInfo($"{selection} no tiene meses de un SKU. Selecciona la celda de un mes, un rango de meses o una " +
                              "fila (en la facturación o en «Serie limpia») y pulsa «Marcar problema».");
                return;
            }
            sheet.Grid.CommitEdit(move: false);
            markView.Show(clean, cells, cells.Count == 1 ? tab.Display(sheet.Grid.Active) : null, OpenCards);
        }

        /// <summary>"Quitar marca" on the selected cells of the sheet (level 1.2).</summary>
        public void RemoveMark()
        {
            if (!(Session is CleanDataLevel clean)) return;
            CellRange selection = sheet.Grid.Selection;
            List<(string Code, int Month)> marked = CleanDataLevel.Locate(sheet.Grid.Sheet, selection)
                .Where(c => clean.MarkAt(c.Code, c.Month) != null).ToList();
            if (marked.Count == 0)
            {
                sheet.SetInfo($"{selection} no tiene marcas. Las celdas marcadas se ven en naranja.");
                return;
            }
            clean.Unmark(marked);
            sheet.SetInfo($"Quitaste la marca de {CleanDataLevel.Describe(marked)}. Los valores de las celdas no cambiaron " +
                          "(Ctrl+Z vuelve a poner la marca).");
        }

        private void OpenCards(string code, int month, DataIssueKind kind)
        {
            if (Session is CleanDataLevel clean)
                cardView.Show(clean, code, month, kind, message =>
                {
                    Notice(message, NotificationSeverity.Success);
                    OpenSheet(CleanDataLevel.CleanSheetName);
                    // Show the cell that was just written.
                    int row = HistoryTable.FindRow(clean.CleanSheet, code);
                    if (row < 0) return;
                    var cell = new CellAddress(HistoryTable.MonthColumn(month), row);
                    sheet.Grid.Select(cell, cell);
                });
        }

        private void OpenHelp()
        {
            if (Session != null) helpView.Show(Session, dialogue.Say);
        }

        private void Deliver()
        {
            if (Session == null) return;
            sheet.Grid.CommitEdit(move: false);
            resultView.Confirm(() =>
            {
                LevelResult result = Session.Deliver();
                bool best = Progress.Record(result);
                // From now on the sheet shows what Andrés flagged (it follows the player's corrections).
                if (Session is CleanDataLevel clean) reviewNotes = clean.ReviewNotes();
                RefreshObjectives();
                sheet.RefreshNotes();
                dialogue.Hide();
                resultView.Show(result, best);
            }, (Session as CleanDataLevel)?.UncorrectedWarning());
        }

        /// <summary>"Seguir trabajando": back to «Serie limpia» on the first cell Andrés flagged.</summary>
        private void KeepWorking()
        {
            if (!(Session is CleanDataLevel clean) || reviewNotes == null) return;
            OpenSheet(CleanDataLevel.CleanSheetName);
            if (reviewNotes.Count == 0) return;
            (string code, int month) = reviewNotes.Keys.OrderBy(k => k.Code, StringComparer.Ordinal).ThenBy(k => k.Month).First();
            int row = HistoryTable.FindRow(clean.CleanSheet, code);
            if (row >= 0)
            {
                var cell = new CellAddress(HistoryTable.MonthColumn(month), row);
                sheet.Grid.Select(cell, cell);
            }
            sheet.SetInfo($"Andrés marcó en rojo {reviewNotes.Count} {(reviewNotes.Count == 1 ? "celda" : "celdas")} para revisar. " +
                          "Selecciona una o pasa el mouse para ver qué pasó; «Quitar marca» borra una marca que pusiste por error.");
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
