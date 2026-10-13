using System;
using System.Collections.Generic;
using System.Linq;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Erp
{
    /// <summary>
    /// The NOVA ERP window: a navy top bar with the transaction and its SAP equivalent, a menu of
    /// screens (later worlds locked), and for world 1 the billing query (selection criteria, result
    /// grid, invoice detail) and the material master. "Descargar a hoja" sends the result to the
    /// planner sheet. Only wiring: the queries are <see cref="NovaErp"/>.
    /// </summary>
    public sealed class ErpWindowView
    {
        private static readonly string[] WorldNames = { "", "Pronóstico", "Gestión de la demanda", "Materiales" };

        private static readonly (string Section, string[] Screens)[] Menu =
        {
            ("VENTAS", new[] { ErpScreens.Billing, "historial-ventas" }),
            ("MATERIALES", new[] { ErpScreens.MaterialMaster, "stock-almacen", "necesidades" }),
            ("COMPRAS", new[] { "solped", "ordenes-compra", "entregas-pendientes", "historial-lead-times" }),
            ("COMERCIO EXTERIOR", new[] { "seguimiento-importaciones" }),
            ("ALMACÉN", new[] { "entradas-mercancia" })
        };

        private static readonly Dictionary<string, string> Transactions = new Dictionary<string, string>
        {
            [ErpScreens.Billing] = "NV-FAC1",
            [ErpScreens.MaterialMaster] = "NV-MAT3",
            ["historial-ventas"] = "NV-VEN2",
            ["stock-almacen"] = "NV-STK1",
            ["necesidades"] = "NV-NEC4",
            ["solped"] = "NV-SOL1",
            ["ordenes-compra"] = "NV-OC21",
            ["entregas-pendientes"] = "NV-ENT2",
            ["seguimiento-importaciones"] = "NV-IMP1",
            ["entradas-mercancia"] = "NV-MIG1",
            ["historial-lead-times"] = "NV-LT01"
        };

        private readonly Action<string> onOpenSheet;
        private readonly VisualElement nav;
        private readonly VisualElement content;
        private readonly Label transaction;
        private readonly Label sap;
        private readonly Label status;
        private readonly Label statusRight;

        private LevelSession session;
        private string screenId = ErpScreens.Billing;

        // Billing query state, kept while the window is closed.
        private string materials = string.Empty;
        private int fromMonth;
        private int toMonth = SalesCalendar.Months - 1;
        private bool projection = true;
        private ErpTable billing;
        private (int Row, int Column)? selectedCell;
        private ErpTable detail;
        private string masterMaterials = string.Empty;
        private ErpTable master;
        private string banner;
        private string bannerSheet;

        public ErpWindowView(VisualElement windowLayer, Action onClose, Action<string> onOpenSheet)
        {
            this.onOpenSheet = onOpenSheet;
            Root = HudElements.Box("panel", "window", "hidden");
            VisualElement screen = HudElements.Box("erp-screen");

            VisualElement top = HudElements.Box("erp-topbar");
            top.Add(HudElements.Text("NOVA", "erp-logo"));
            top.Add(HudElements.Text("ERP", "erp-logo-accent"));
            top.Add(HudElements.Box("erp-topbar-divider"));
            VisualElement transactionBox = HudElements.Box("erp-transaction");
            transactionBox.Add(HudElements.Text("TRANSACCIÓN", "erp-transaction-label"));
            transaction = HudElements.Text(string.Empty, "erp-transaction-code");
            transactionBox.Add(transaction);
            sap = HudElements.Text(string.Empty, "erp-sap");
            transactionBox.Add(sap);
            top.Add(transactionBox);
            top.Add(HudElements.Box("erp-topbar-spacer"));
            top.Add(HudElements.Text("Planeador · CEDI Medellín\nFecha: 01.09.2026", "erp-user"));
            top.Add(new Button(onClose) { text = "Cerrar" }.WithClass("erp-close"));
            screen.Add(top);

            VisualElement body = HudElements.Box("erp-body");
            nav = HudElements.Box("erp-nav");
            body.Add(nav);
            content = HudElements.Box("erp-content");
            body.Add(content);
            screen.Add(body);

            VisualElement statusBar = HudElements.Box("erp-status");
            status = HudElements.Text("Listo", "erp-status-text");
            statusRight = HudElements.Text(string.Empty, "erp-status-right");
            statusBar.Add(status);
            statusBar.Add(statusRight);
            screen.Add(statusBar);

            Root.Add(screen);
            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            windowLayer.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsVisible => !Root.ClassListContains("hidden");

        /// <summary>Starts a new level: clears the queries and points at its ERP data.</summary>
        public void Bind(LevelSession level)
        {
            session = level;
            screenId = ErpScreens.Billing;
            materials = masterMaterials = string.Empty;
            fromMonth = 0;
            toMonth = SalesCalendar.Months - 1;
            projection = true;
            billing = detail = master = null;
            selectedCell = null;
            banner = bannerSheet = null;
            statusRight.text = $"Cosmética · {HudFormat.Number(level.Data.Portfolio.Skus.Count)} materiales · facturación ene-23 a ago-26";
            SetStatus("Listo");
            Rebuild();
        }

        public void Show()
        {
            HudElements.SetVisible(Root, true);
            Root.BringToFront();
            Root.Focus();
        }

        public void Hide() => HudElements.SetVisible(Root, false);

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.F8) return;
            if (screenId == ErpScreens.Billing) RunBilling();
            else if (screenId == ErpScreens.MaterialMaster) RunMaster();
            evt.StopPropagation();
        }

        private void SetStatus(string text) => status.text = text;

        // ------------------------------------------------------------------ layout

        private void Rebuild()
        {
            BuildNav();
            content.Clear();
            ErpScreen screen = ErpScreens.Find(screenId);
            transaction.text = Transactions.TryGetValue(screenId, out string code) ? code : screenId;
            sap.text = $"≈ SAP {screen.SapEquivalent}";

            VisualElement titleRow = HudElements.Box("erp-title-row");
            titleRow.Add(HudElements.Text(screen.Name, "erp-title"));
            titleRow.Add(HudElements.Text($"Equivalente SAP: {screen.SapEquivalent}", "erp-sap-chip"));
            content.Add(titleRow);
            content.Add(HudElements.Text(screen.Purpose, "erp-purpose"));

            if (session == null || !session.Erp.IsAvailable(screenId))
            {
                VisualElement locked = HudElements.Box("erp-locked");
                locked.Add(new HudIcon { Kind = HudIconKind.Lock });
                locked.Add(HudElements.Text($"Esta pantalla se habilita en el mundo {screen.UnlockWorld} · {WorldNames[screen.UnlockWorld]}.",
                    "erp-locked-text"));
                content.Add(locked);
                return;
            }
            if (screenId == ErpScreens.Billing) BuildBilling();
            else BuildMaster();
        }

        private void BuildNav()
        {
            nav.Clear();
            foreach ((string section, string[] screens) in Menu)
            {
                nav.Add(HudElements.Text(section, "erp-nav-section"));
                foreach (string id in screens)
                {
                    ErpScreen screen = ErpScreens.Find(id);
                    bool available = session != null && session.Erp.IsAvailable(id);
                    VisualElement item = HudElements.Box("erp-nav-item");
                    if (id == screenId) item.AddToClassList("erp-nav-item--active");
                    if (!available) item.AddToClassList("erp-nav-item--locked");
                    item.Add(HudElements.Text(screen.Name, "erp-nav-name"));
                    if (!available)
                    {
                        item.Add(new HudIcon { Kind = HudIconKind.Lock });
                        item.Add(HudElements.Text($"Mundo {screen.UnlockWorld}", "erp-nav-world"));
                    }
                    item.RegisterCallback<ClickEvent>(_ =>
                    {
                        screenId = id;
                        Rebuild();
                    });
                    nav.Add(item);
                }
            }
        }

        // ------------------------------------------------------------------ billing (VF05)

        private void BuildBilling()
        {
            if (session.RequestedCodes.Count > 0)
            {
                VisualElement request = HudElements.Box("erp-request");
                request.Add(HudElements.Text($"Solicitud de Sofía: {string.Join(", ", session.RequestedCodes)} · de ene-23 a ago-26",
                    "erp-request-text"));
                request.Add(new Button(() =>
                {
                    materials = string.Join(", ", session.RequestedCodes);
                    Rebuild();
                }) { text = "Copiar materiales" }.WithClass("erp-button", "erp-button--secondary"));
                content.Add(request);
            }

            VisualElement group = HudElements.Box("erp-group");
            group.Add(HudElements.Text("CRITERIOS DE SELECCIÓN", "erp-group-title"));
            group.Add(MaterialRow(materials, value => materials = value));

            VisualElement period = HudElements.Box("erp-form-row");
            period.Add(HudElements.Text("Periodo", "erp-label"));
            List<string> labels = Enumerable.Range(0, SalesCalendar.Months).Select(SalesCalendar.Label).ToList();
            var from = new DropdownField(labels, fromMonth).WithClass("erp-dropdown");
            from.RegisterValueChangedCallback(e => fromMonth = labels.IndexOf(e.newValue));
            var to = new DropdownField(labels, toMonth).WithClass("erp-dropdown");
            to.RegisterValueChangedCallback(e => toMonth = labels.IndexOf(e.newValue));
            period.Add(from);
            period.Add(HudElements.Text("a", "erp-label").WithWidth(26));
            period.Add(to);
            var toggle = new Toggle($"Incluir proyección del mes en curso ({SalesCalendar.Label(SalesCalendar.Months)})")
                { value = projection }.WithClass("erp-toggle");
            toggle.RegisterValueChangedCallback(e => projection = e.newValue);
            period.Add(toggle);
            period.Add(HudElements.Box("erp-spacer"));
            period.Add(new Button(RunBilling) { text = "Ejecutar  (F8)" }.WithClass("erp-button"));
            group.Add(period);
            content.Add(group);

            VisualElement results = HudElements.Box("erp-results");
            content.Add(results);
            if (detail != null) BuildDetail(results);
            else if (billing != null) BuildBillingResult(results);
            else results.Add(HudElements.Text("Llena los criterios y pulsa «Ejecutar» para ver la facturación.", "erp-empty"));
        }

        private void RunBilling()
        {
            List<string> codes = ParseCodes(materials);
            if (codes.Count == 0)
            {
                SetStatus("Escribe al menos un material (por ejemplo CV10009).");
                return;
            }
            billing = session.Erp.Billing(new BillingQuery
            {
                Codes = codes, FromMonth = fromMonth, ToMonth = toMonth, IncludeProjection = projection
            });
            detail = null;
            selectedCell = null;
            banner = null;
            int months = Math.Abs(toMonth - fromMonth) + 1;
            SetStatus($"Consulta ejecutada: {billing.Rows.Count} materiales · {months} meses" +
                      (billing.Headers.Any(h => h.EndsWith(NovaErp.ProjectionSuffix)) ? " + proyección" : string.Empty) + ".");
            Rebuild();
        }

        private void BuildBillingResult(VisualElement results)
        {
            VisualElement header = HudElements.Box("erp-results-header");
            header.Add(HudElements.Text($"Resultado · {billing.Title}", "erp-results-title"));
            var detailButton = new Button(ShowDetail) { text = "Detalle de facturas" }.WithClass("erp-button", "erp-button--secondary");
            detailButton.SetEnabled(selectedCell.HasValue);
            header.Add(detailButton);
            header.Add(new Button(() => Download(billing)) { text = "Descargar a hoja" }.WithClass("erp-button", "erp-button--download"));
            results.Add(header);
            AddBanner(results);
            if (!selectedCell.HasValue)
                results.Add(HudElements.Text("Clic en un mes para ver sus facturas por cliente.", "erp-purpose"));

            int last = billing.Headers.Count - 1;
            results.Add(ErpTableView.Build(billing,
                (row, column) => column >= 2 && column < last && !billing.Headers[column].EndsWith(NovaErp.ProjectionSuffix),
                (row, column) => selectedCell.HasValue && selectedCell.Value == (row, column),
                (row, column) =>
                {
                    selectedCell = (row, column);
                    CellValue[] values = billing.Rows[row];
                    SetStatus($"{values[0].Text} · {billing.Headers[column]}: {values[column]} {values[last].Text}");
                    Rebuild();
                }));
            foreach (string note in billing.Notes) results.Add(HudElements.Text("* " + note, "erp-note"));
        }

        private void ShowDetail()
        {
            if (!selectedCell.HasValue || billing == null) return;
            (int row, int column) = selectedCell.Value;
            int month = Enumerable.Range(0, SalesCalendar.Months).FirstOrDefault(m => SalesCalendar.Label(m) == billing.Headers[column]);
            detail = session.Erp.BillingDetail(billing.Rows[row][0].Text, month);
            banner = null;
            SetStatus($"{detail.Title}: {detail.Rows.Count} facturas.");
            Rebuild();
        }

        private void BuildDetail(VisualElement results)
        {
            VisualElement header = HudElements.Box("erp-results-header");
            header.Add(HudElements.Text(detail.Title, "erp-results-title"));
            header.Add(new Button(() =>
            {
                detail = null;
                Rebuild();
            }) { text = "Volver a la consulta" }.WithClass("erp-button", "erp-button--secondary"));
            header.Add(new Button(() => Download(detail)) { text = "Descargar a hoja" }.WithClass("erp-button", "erp-button--download"));
            results.Add(header);
            AddBanner(results);
            results.Add(ErpTableView.Build(detail, null, null, null));
            foreach (string note in detail.Notes) results.Add(HudElements.Text("* " + note, "erp-note"));
        }

        // ------------------------------------------------------------------ material master (MM03)

        private void BuildMaster()
        {
            VisualElement group = HudElements.Box("erp-group");
            group.Add(HudElements.Text("CRITERIOS DE SELECCIÓN", "erp-group-title"));
            group.Add(MaterialRow(masterMaterials, value => masterMaterials = value));
            VisualElement actions = HudElements.Box("erp-actions");
            actions.Add(new Button(RunMaster) { text = "Ejecutar  (F8)" }.WithClass("erp-button"));
            group.Add(actions);
            content.Add(group);

            VisualElement results = HudElements.Box("erp-results");
            content.Add(results);
            if (master == null)
            {
                results.Add(HudElements.Text("Escribe los materiales y pulsa «Ejecutar».", "erp-empty"));
                return;
            }
            VisualElement header = HudElements.Box("erp-results-header");
            header.Add(HudElements.Text($"Resultado · {master.Rows.Count} materiales", "erp-results-title"));
            header.Add(new Button(() => Download(master)) { text = "Descargar a hoja" }.WithClass("erp-button", "erp-button--download"));
            results.Add(header);
            AddBanner(results);
            results.Add(ErpTableView.Build(master, null, null, null));
        }

        private void RunMaster()
        {
            List<string> codes = ParseCodes(masterMaterials);
            if (codes.Count == 0)
            {
                SetStatus("Escribe al menos un material.");
                return;
            }
            master = session.Erp.MaterialMaster(codes);
            banner = null;
            SetStatus($"Maestro de materiales: {master.Rows.Count} materiales.");
            Rebuild();
        }

        // ------------------------------------------------------------------ shared pieces

        /// <summary>Materials field with suggestions from the portfolio while typing a code or a name.</summary>
        private VisualElement MaterialRow(string value, Action<string> setValue)
        {
            VisualElement block = new VisualElement();
            VisualElement row = HudElements.Box("erp-form-row");
            row.Add(HudElements.Text("Materiales", "erp-label"));
            var field = new TextField { value = value }.WithClass("erp-field");
            row.Add(field);
            row.Add(HudElements.Text("separa con coma", "erp-hint"));
            block.Add(row);
            VisualElement suggestions = HudElements.Box("erp-suggestions", "hidden");
            block.Add(suggestions);

            field.RegisterValueChangedCallback(e =>
            {
                setValue(e.newValue);
                ShowSuggestions(field, suggestions, setValue);
            });
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != UnityEngine.KeyCode.Return && e.keyCode != UnityEngine.KeyCode.KeypadEnter) return;
                if (screenId == ErpScreens.Billing) RunBilling();
                else RunMaster();
            });
            return block;
        }

        private void ShowSuggestions(TextField field, VisualElement suggestions, Action<string> setValue)
        {
            suggestions.Clear();
            string text = field.value ?? string.Empty;
            int cut = text.LastIndexOfAny(new[] { ',', ';' });
            string term = text.Substring(cut + 1).Trim();
            bool exact = session.Data.Portfolio.Find(term.ToUpperInvariant()) != null;
            if (term.Length < 2 || exact)
            {
                HudElements.SetVisible(suggestions, false);
                return;
            }
            var matches = session.Data.Portfolio.Skus.Where(s =>
                    s.Code.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(6).ToList();
            foreach (PortfolioSku sku in matches)
            {
                Label option = HudElements.Text($"{sku.Code}  ·  {sku.Description}", "erp-suggestion");
                option.RegisterCallback<ClickEvent>(_ =>
                {
                    string kept = cut >= 0 ? text.Substring(0, cut + 1) + " " : string.Empty;
                    field.SetValueWithoutNotify(kept + sku.Code + ", ");
                    setValue(field.value);
                    HudElements.SetVisible(suggestions, false);
                    field.Focus();
                });
                suggestions.Add(option);
            }
            HudElements.SetVisible(suggestions, matches.Count > 0);
        }

        private void AddBanner(VisualElement results)
        {
            if (banner == null) return;
            VisualElement box = HudElements.Box("erp-banner");
            box.Add(HudElements.Text(banner, "erp-banner-text"));
            string sheet = bannerSheet;
            box.Add(new Button(() => onOpenSheet?.Invoke(sheet)) { text = "Abrir la hoja" }.WithClass("erp-button"));
            results.Add(box);
        }

        private void Download(ErpTable table)
        {
            Worksheet sheet = session.Erp.Download(table, session.Book);
            bannerSheet = sheet.Name;
            banner = $"Descargado a la hoja del planeador, pestaña «{sheet.Name}».";
            SetStatus(banner);
            Rebuild();
        }

        private static List<string> ParseCodes(string text) =>
            (text ?? string.Empty).Split(new[] { ',', ';', ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
    }
}
