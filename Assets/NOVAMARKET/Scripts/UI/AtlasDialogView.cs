using System;
using System.Collections.Generic;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Planning;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// "Sugerido del Atlas" window: the periodic-review calculation of one SKU step by step
    /// (plan, deviation, safety stock, target level, position, order), a reminder that the
    /// suggestion ignores the announced events, and a button to copy the order into the panel.
    /// </summary>
    public sealed class AtlasDialogView
    {
        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly Label subtitle;
        private readonly VisualElement steps;
        private readonly Label result;
        private readonly VisualElement note;
        private readonly Label noteText;
        private readonly Button useButton;
        private readonly Action<int, int> onUse;
        private int sku;
        private int order;

        /// <param name="onUse">Called with (SKU index, boxes) when the player takes the suggestion.</param>
        public AtlasDialogView(ModalLayer modal, Action<int, int> onUse)
        {
            this.modal = modal;
            this.onUse = onUse;
            dialog = modal.CreateDialog();
            dialog.Add(HudElements.Text("Sugerido del Atlas", "dialog-title"));
            dialog.Add(subtitle = HudElements.Text(string.Empty, "dialog-subtitle"));
            dialog.Add(HudElements.Text("Revisión periódica semanal con nivel objetivo: cada semana pides lo que falta " +
                                        "para llegar al nivel S, que cubre el lead time más una semana con 95 % de servicio.",
                "dialog-text"));
            dialog.Add(steps = HudElements.Box("atlas-steps"));
            dialog.Add(result = HudElements.Text(string.Empty, "atlas-result"));

            note = HudElements.Box("note-box");
            note.Add(HudElements.Text("OJO", "note-caption"));
            note.Add(noteText = HudElements.Text(string.Empty, "note-text"));
            dialog.Add(note);

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(HudElements.Button("Cerrar", () => modal.Hide(dialog), "dialog-button"));
            buttons.Add(useButton = HudElements.Button(string.Empty, Use, "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public void Show(PlanningSimulation simulation, int skuIndex)
        {
            sku = skuIndex;
            AtlasSuggestion suggestion = simulation.Suggest(skuIndex);
            order = suggestion.Order;
            PlanningSku item = simulation.Skus[skuIndex];
            subtitle.text = $"{item.Code} · {item.Name} · semana {simulation.Week} · LT {item.LeadTimeWeeks} " +
                            $"{(item.LeadTimeWeeks == 1 ? "semana" : "semanas")} · CV {HudFormat.Decimal(item.VariationCoefficient, 2)}";

            steps.Clear();
            IReadOnlyList<string> lines = suggestion.Explain();
            for (int i = 0; i < lines.Count; i++)
            {
                VisualElement step = HudElements.Box("atlas-step");
                step.Add(HudElements.Text((i + 1).ToString(), "atlas-step-number"));
                step.Add(HudElements.Text(lines[i], "atlas-step-text"));
                steps.Add(step);
            }

            result.text = $"Pedido sugerido: {HudFormat.Number(order)} cajas · {HudFormat.Pesos(order * item.CostPerBox)}";
            noteText.text = EventNote(simulation, skuIndex, suggestion);
            useButton.text = $"Usar {HudFormat.Number(order)} cajas";
            modal.Show(dialog);
        }

        private void Use()
        {
            modal.Hide(dialog);
            onUse?.Invoke(sku, order);
        }

        /// <summary>The suggestion uses the statistical plan; lists the announced events it leaves out.</summary>
        private static string EventNote(PlanningSimulation simulation, int sku, AtlasSuggestion suggestion)
        {
            var events = new List<string>();
            for (int week = suggestion.Week; week < suggestion.Week + suggestion.ProtectionWeeks; week++)
            {
                double factor = simulation.AnnouncedFactor(sku, week);
                if (factor != 1.0) events.Add($"semana {week}: {HudFormat.Factor(factor)}");
            }
            if (events.Count == 0)
                return "El sugerido usa solo el pronóstico estadístico: no incluye los avisos de los personajes. " +
                       "Esa decisión es tuya.";
            return "El sugerido NO incluye los avisos que ya conoces (" + string.Join(", ", events) + "). " +
                   "Multiplica el plan de esas semanas y súmale la diferencia al pedido.";
        }
    }
}
