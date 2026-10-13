using System;
using System.Collections.Generic;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Order plan panel (top centre): one row per SKU with stock against the rack capacity,
    /// transit with arrival weeks, the plan of the next L+1 weeks (announced events marked),
    /// the order field and the "Sugerido del Atlas" button; then the purchase cap, the last
    /// character comment and the "Cerrar semana" button. It only reads the
    /// <see cref="PlanningSimulation"/>; closing the week and the dialogs belong to the controller.
    /// </summary>
    public sealed class PlanningPanelView
    {
        private readonly VisualElement panel;
        private readonly VisualElement body;
        private readonly VisualElement rowsContainer;
        private readonly Label weekLabel;
        private readonly Label serviceLabel;
        private readonly Label costLabel;
        private readonly Button toggleButton;
        private readonly VisualElement capRow;
        private readonly Label capText;
        private readonly Button fitCapButton;
        private readonly VisualElement commentBox;
        private readonly CharacterBadge commentBadge;
        private readonly Label commentName;
        private readonly Label commentText;
        private readonly Label commentSummary;
        private readonly Label totalLabel;
        private readonly Label blockLabel;
        private readonly Button closeButton;
        private readonly Action<int> onAtlas;
        private readonly List<SkuRow> rows = new List<SkuRow>();

        private PlanningSimulation simulation;
        private int[] orders = new int[0];
        private bool collapsed;

        public PlanningPanelView(VisualElement root, Action<int> onAtlas, Action onCloseWeek)
        {
            this.onAtlas = onAtlas;
            panel = root.Q("planningPanel");
            panel.Clear();

            VisualElement header = HudElements.Box("planning-header");
            header.Add(HudElements.Text("PLAN DE PEDIDOS", "planning-title"));
            header.Add(weekLabel = HudElements.Text(string.Empty, "planning-week"));
            header.Add(HudElements.Box("planning-spacer"));
            header.Add(serviceLabel = HudElements.Text(string.Empty, "planning-kpi"));
            header.Add(costLabel = HudElements.Text(string.Empty, "planning-kpi"));
            header.Add(toggleButton = HudElements.Button("Ocultar", () => SetCollapsed(!collapsed), "planning-small-button"));
            panel.Add(header);

            body = HudElements.Box("planning-body");
            VisualElement head = HudElements.Box("planning-row", "planning-row--head");
            head.Add(HudElements.Text("SKU", "planning-head-label", "col-sku"));
            head.Add(HudElements.Text("EN BODEGA / RACK", "planning-head-label", "col-stock"));
            head.Add(HudElements.Text("EN TRÁNSITO", "planning-head-label", "col-transit"));
            head.Add(HudElements.Text("PLAN ESTADÍSTICO (L+1 SEMANAS)", "planning-head-label", "col-plan"));
            head.Add(HudElements.Text("PEDIDO (CAJAS)", "planning-head-label", "col-order"));
            body.Add(head);
            body.Add(rowsContainer = new VisualElement());

            capRow = HudElements.Box("planning-cap");
            capRow.Add(capText = HudElements.Text(string.Empty, "planning-cap-text"));
            capRow.Add(fitCapButton = HudElements.Button("Ajustar al tope", FitToCap, "planning-small-button"));
            body.Add(capRow);

            VisualElement footer = HudElements.Box("planning-footer");
            commentBox = HudElements.Box("planning-comment");
            commentBox.Add(commentBadge = new CharacterBadge());
            VisualElement texts = HudElements.Box("planning-comment-texts");
            texts.Add(commentName = HudElements.Text(string.Empty, "comment-name"));
            texts.Add(commentText = HudElements.Text(string.Empty, "comment-text"));
            texts.Add(commentSummary = HudElements.Text(string.Empty, "comment-summary"));
            commentBox.Add(texts);
            footer.Add(commentBox);

            VisualElement actions = HudElements.Box("planning-actions");
            actions.Add(totalLabel = HudElements.Text(string.Empty, "planning-total"));
            VisualElement buttons = HudElements.Box("planning-actions-row");
            buttons.Add(HudElements.Button("Usar todos los sugeridos", UseAllSuggestions, "planning-small-button"));
            buttons.Add(closeButton = new Button(onCloseWeek) { text = "Cerrar semana" });
            closeButton.AddToClassList("close-week-button");
            actions.Add(buttons);
            actions.Add(blockLabel = HudElements.Text(string.Empty, "planning-block"));
            footer.Add(actions);
            body.Add(footer);
            panel.Add(body);
        }

        /// <summary>Boxes the player typed for each SKU (catalogue order).</summary>
        public IReadOnlyList<int> Orders => orders;

        /// <summary>Shows a new season: one row per SKU, empty orders, no comment yet.</summary>
        public void Bind(PlanningSimulation season)
        {
            simulation = season ?? throw new ArgumentNullException(nameof(season));
            orders = new int[simulation.Skus.Count];
            rowsContainer.Clear();
            rows.Clear();
            for (int sku = 0; sku < simulation.Skus.Count; sku++)
            {
                var row = new SkuRow(simulation.Skus[sku], sku, this);
                rows.Add(row);
                rowsContainer.Add(row.Root);
            }
            HudElements.SetVisible(commentBox, false);
            HudElements.SetVisible(panel, true);
            Refresh();
        }

        public void SetCollapsed(bool value)
        {
            collapsed = value;
            HudElements.SetVisible(body, !collapsed);
            toggleButton.text = collapsed ? "Mostrar plan" : "Ocultar";
        }

        public void SetOrder(int sku, int boxes)
        {
            orders[sku] = Math.Max(0, boxes);
            Refresh();
        }

        public void ClearOrders()
        {
            Array.Clear(orders, 0, orders.Length);
            Refresh();
        }

        /// <summary>The character's line about the week just closed, with its numbers.</summary>
        public void ShowComment(WeekReport report)
        {
            if (report?.Comment == null)
            {
                HudElements.SetVisible(commentBox, false);
                return;
            }
            HudElements.SetVisible(commentBox, true);
            commentBadge.Show(report.Comment.Character, report.Comment.Mood);
            commentName.text = $"{PlanningCharacters.Name(report.Comment.Character)} · " +
                               PlanningCharacters.Role(report.Comment.Character);
            commentText.text = report.Comment.Text;
            commentSummary.text = $"Semana {report.Week}: vendiste {HudFormat.Number(report.Sold)} de " +
                                  $"{HudFormat.Number(report.Demand)} cajas · costo de la semana " +
                                  HudFormat.Pesos((long)Math.Round(report.Costs.Total));
        }

        /// <summary>Updates every number from the simulation and the typed orders.</summary>
        public void Refresh()
        {
            if (simulation == null) return;
            int week = simulation.Week;
            weekLabel.text = simulation.IsFinished
                ? "Temporada terminada"
                : $"Semana {week} de {PlanningRules.SeasonWeeks} · {PlanningRules.MonthName(week)}";

            double service = simulation.ServiceLevel;
            serviceLabel.text = $"Servicio {HudFormat.Percent(service)}";
            serviceLabel.EnableInClassList("planning-kpi--good", service >= PlanningRules.TwoStarService);
            serviceLabel.EnableInClassList("planning-kpi--bad", service < PlanningRules.TwoStarService);
            costLabel.text = $"Costo {HudFormat.Pesos((long)Math.Round(simulation.Costs.Total))}";

            foreach (SkuRow row in rows) row.Refresh(simulation, orders[row.Index]);

            long value = simulation.OrderValue(orders);
            totalLabel.text = $"Valor del pedido: {HudFormat.Pesos(value)}";
            bool capped = simulation.PurchaseCap.HasValue && !simulation.IsFinished;
            bool overCap = simulation.ExceedsPurchaseCap(orders);
            HudElements.SetVisible(capRow, capped);
            capRow.EnableInClassList("planning-cap--over", overCap);
            HudElements.SetVisible(fitCapButton, overCap);
            if (capped)
                capText.text = $"Tope de compras de Marcela esta semana: {HudFormat.Pesos(simulation.PurchaseCap.Value)} · " +
                               $"tu pedido vale {HudFormat.Pesos(value)}";

            string block = simulation.IsFinished ? string.Empty
                : simulation.PendingEvent != null ? "Primero responde el aviso pendiente."
                : overCap ? "El pedido supera el tope de compras."
                : string.Empty;
            blockLabel.text = block;
            closeButton.text = simulation.IsFinished ? "Ver resultados" : "Cerrar semana";
            closeButton.SetEnabled(simulation.IsFinished || (simulation.PendingEvent == null && !overCap));
        }

        private void UseAllSuggestions()
        {
            if (simulation == null || simulation.IsFinished) return;
            for (int sku = 0; sku < orders.Length; sku++) orders[sku] = simulation.Suggest(sku).Order;
            Refresh();
        }

        private void FitToCap()
        {
            int[] scaled = PlanningPolicies.ScaleToCap(simulation, orders);
            Array.Copy(scaled, orders, orders.Length);
            Refresh();
        }

        /// <summary>The widgets of one SKU row.</summary>
        private sealed class SkuRow
        {
            private readonly PlanningSku sku;
            private readonly Label stockValue;
            private readonly Label stockCap;
            private readonly VisualElement stockCell;
            private readonly VisualElement stockFill;
            private readonly VisualElement transit;
            private readonly VisualElement plan;
            private readonly IntegerField quantity;
            private readonly Label orderValue;
            private readonly Label orderWarning;

            public SkuRow(PlanningSku sku, int index, PlanningPanelView owner)
            {
                this.sku = sku;
                Index = index;
                Root = HudElements.Box("planning-row");

                VisualElement name = HudElements.Box("col-sku");
                name.Add(HudElements.Text(sku.Name, "sku-name"));
                string moq = sku.MinimumOrder > 0 ? $"MOQ {sku.MinimumOrder}" : "sin MOQ";
                name.Add(HudElements.Text($"{sku.Code} · LT {sku.LeadTimeWeeks} sem · {moq} · {sku.Supplier}", "sku-meta"));
                Root.Add(name);

                stockCell = HudElements.Box("col-stock");
                VisualElement line = HudElements.Box("stock-line");
                line.Add(stockValue = HudElements.Text(string.Empty, "stock-value"));
                line.Add(stockCap = HudElements.Text(string.Empty, "stock-cap"));
                stockCell.Add(line);
                VisualElement track = HudElements.Box("stock-track");
                track.Add(stockFill = HudElements.Box("stock-fill"));
                stockCell.Add(track);
                Root.Add(stockCell);

                Root.Add(transit = HudElements.Box("col-transit"));
                Root.Add(plan = HudElements.Box("col-plan"));

                VisualElement order = HudElements.Box("col-order");
                VisualElement orderLine = HudElements.Box("order-line");
                quantity = new IntegerField { value = 0 };
                quantity.AddToClassList("planning-qty");
                quantity.RegisterValueChangedCallback(change =>
                {
                    owner.orders[Index] = Math.Max(0, change.newValue);
                    owner.Refresh();
                });
                orderLine.Add(quantity);
                orderLine.Add(HudElements.Text("cajas", "order-unit"));
                order.Add(orderLine);
                order.Add(orderValue = HudElements.Text(string.Empty, "order-value"));
                order.Add(orderWarning = HudElements.Text(string.Empty, "order-warning"));
                Root.Add(order);

                VisualElement atlas = HudElements.Box("col-atlas");
                atlas.Add(HudElements.Button("Sugerido del Atlas", () => owner.onAtlas?.Invoke(Index)));
                Root.Add(atlas);
            }

            public VisualElement Root { get; }
            public int Index { get; }

            public void Refresh(PlanningSimulation simulation, int ordered)
            {
                int onHand = simulation.OnHand(Index);
                int capacity = simulation.RackCapacity(Index);
                stockValue.text = HudFormat.Number(onHand);
                stockCap.text = $"/ {HudFormat.Number(capacity)}";
                stockCell.EnableInClassList("stock--over", onHand > capacity);
                stockFill.style.width = Length.Percent(Math.Min(100f, 100f * onHand / Math.Max(1, capacity)));

                transit.Clear();
                foreach (TransitLot lot in simulation.InTransit)
                {
                    if (lot.Sku != Index) continue;
                    string how = lot.IsAirFreight ? " (avión)" : lot.DelayWeeks > 0 ? " (retrasado)" : string.Empty;
                    Label line = HudElements.Text($"{HudFormat.Number(lot.Boxes)} cajas · llega S{lot.ArrivalWeek}{how}",
                        "transit-line");
                    line.EnableInClassList("transit-line--late", lot.DelayWeeks > 0);
                    transit.Add(line);
                }
                if (transit.childCount == 0) transit.Add(HudElements.Text("—", "transit-line"));

                plan.Clear();
                if (!simulation.IsFinished)
                    for (int week = simulation.Week; week <= simulation.Week + sku.LeadTimeWeeks; week++)
                        plan.Add(PlanChip(simulation, week));

                if (quantity.value != ordered) quantity.SetValueWithoutNotify(ordered);
                quantity.SetEnabled(!simulation.IsFinished);
                orderValue.text = ordered > 0 ? HudFormat.Pesos(ordered * sku.CostPerBox) : string.Empty;
                orderWarning.text = ordered > 0 && ordered < sku.MinimumOrder
                    ? $"Debajo del pedido mínimo ({sku.MinimumOrder})"
                    : string.Empty;
            }

            private VisualElement PlanChip(PlanningSimulation simulation, int week)
            {
                VisualElement chip = HudElements.Box("plan-chip");
                chip.Add(HudElements.Text(week > PlanningRules.SeasonWeeks ? "Ene" : $"S{week}", "plan-chip-week"));
                chip.Add(HudElements.Text(HudFormat.Decimal(simulation.PlanMean(Index, week), 1), "plan-chip-value"));
                double factor = simulation.AnnouncedFactor(Index, week);
                if (factor != 1.0)
                {
                    chip.AddToClassList("plan-chip--event");
                    chip.Add(HudElements.Text($"aviso {HudFormat.Factor(factor)}", "plan-chip-factor"));
                }
                return chip;
            }
        }
    }
}
