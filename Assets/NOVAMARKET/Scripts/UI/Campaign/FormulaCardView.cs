using System;
using System.Collections.Generic;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Portfolio;
using NovaMarket.Simulation.Sheet;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// Formula cards of a cell (level 1.2, "casillas vacías"): the Atlas formula with one box per term.
    /// The player fills the boxes from the data and presses "Comprobar": a wrong box turns red with its
    /// hint; with every box right the result appears and can be written into «Serie limpia».
    /// </summary>
    public sealed class FormulaCardView
    {
        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly Label subtitle;
        private readonly VisualElement tabs;
        private readonly Label cardTitle;
        private readonly Label formula;
        private readonly VisualElement boxes;
        private readonly Label result;
        private readonly Button writeButton;
        private readonly List<(TextField Field, Label Hint, VisualElement Row)> rows = new List<(TextField, Label, VisualElement)>();
        private IReadOnlyList<FormulaCard> cards = Array.Empty<FormulaCard>();
        private FormulaCard card;
        private CleanDataLevel level;
        private string code;
        private int month;
        private double? value;
        private Action<string> onWritten;

        public FormulaCardView(ModalLayer modal)
        {
            this.modal = modal;
            dialog = modal.CreateDialog("card-dialog");
            dialog.Add(HudElements.Text("Tarjetas de fórmula", "dialog-title"));
            dialog.Add(subtitle = HudElements.Text(string.Empty, "dialog-subtitle"));
            dialog.Add(tabs = HudElements.Box("chip-row"));

            VisualElement body = HudElements.Box("card-body");
            body.Add(cardTitle = HudElements.Text(string.Empty, "card-title"));
            body.Add(formula = HudElements.Text(string.Empty, "card-formula"));
            body.Add(boxes = HudElements.Box("card-boxes"));
            body.Add(result = HudElements.Text(string.Empty, "card-result"));
            dialog.Add(body);

            dialog.Add(HudElements.Text("Escribir el resultado corrige el dato. La insignia «Excel de planeador» solo se gana " +
                                        "si la corrección es una fórmula tuya en la hoja.", "dialog-subtitle", "card-note"));

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(HudElements.Button("Cerrar", () => modal.Hide(dialog), "dialog-button"));
            buttons.Add(HudElements.Button("Comprobar", Check, "dialog-button"));
            buttons.Add(writeButton = HudElements.Button("Escribir en Serie limpia", Write, "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public bool IsOpen => modal.IsOpen && !dialog.ClassListContains("hidden");
        public FormulaCard Card => card;

        /// <param name="written">Called with a short notice after writing the result.</param>
        public void Show(CleanDataLevel session, string sku, int monthIndex, DataIssueKind kind, Action<string> written)
        {
            level = session;
            code = sku;
            month = monthIndex;
            onWritten = written;
            subtitle.text = $"{sku} · {SalesCalendar.Label(monthIndex)} · {DataIssue.Name(kind)}";
            cards = level.CardsFor(sku, monthIndex, kind);
            tabs.Clear();
            foreach (FormulaCard c in cards)
            {
                FormulaCard target = c;
                tabs.Add(HudElements.Button(c.Title, () => Select(target), "chip"));
            }
            if (cards.Count > 0) Select(cards[0]);
            modal.Show(dialog);
        }

        public void Select(FormulaCard chosen)
        {
            card = chosen;
            for (int i = 0; i < cards.Count; i++) tabs[i].EnableInClassList("chip--on", cards[i] == chosen);
            cardTitle.text = chosen.Title;
            formula.text = chosen.AtlasFormula;
            boxes.Clear();
            rows.Clear();
            for (int i = 0; i < chosen.Boxes.Count; i++)
            {
                CardBox box = chosen.Boxes[i];
                VisualElement row = HudElements.Box("card-box");
                VisualElement line = HudElements.Box("card-box-line");
                line.Add(HudElements.Text(box.Label, "card-box-label"));
                var field = new TextField().WithClass("card-box-field");
                if (chosen.Prefilled[i])
                {
                    field.SetValueWithoutNotify(SheetNumber.Format(box.Expected, CellFormat.Number(Decimals(box.Expected))));
                    field.SetEnabled(false);
                }
                line.Add(field);
                row.Add(line);
                Label hint = HudElements.Text(box.Hint, "card-box-hint", "hidden");
                row.Add(hint);
                boxes.Add(row);
                rows.Add((field, hint, row));
            }
            value = null;
            result.text = $"{chosen.ResultLabel} = ?";
            writeButton.SetEnabled(false);
        }

        /// <summary>Types into a box (used by the Play tests, as the player would).</summary>
        public void Type(int box, string text) => rows[box].Field.value = text;

        public void Check()
        {
            if (card == null) return;
            var entries = new double?[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                entries[i] = SheetNumber.TryParse(rows[i].Field.value, out double number) ? number : (double?)null;
            CardCheck check = card.Check(entries);
            for (int i = 0; i < rows.Count; i++)
            {
                bool ok = check.BoxesOk[i];
                rows[i].Row.EnableInClassList("card-box--wrong", !ok);
                rows[i].Row.EnableInClassList("card-box--ok", ok && !card.Prefilled[i]);
                HudElements.SetVisible(rows[i].Hint, !ok);
            }
            value = check.Result;
            result.text = value.HasValue
                ? $"{card.ResultLabel} = {SheetNumber.Format(value.Value, CellFormat.Number(Decimals(value.Value)))}"
                : $"{card.ResultLabel} = ?   Revisa las casillas en rojo.";
            result.EnableInClassList("card-result--ok", value.HasValue);
            writeButton.SetEnabled(value.HasValue && card.Id != "atipico");
        }

        public void Write()
        {
            if (!value.HasValue) return;
            double rounded = Math.Round(value.Value);
            if (!level.WriteClean(code, month, rounded)) return;
            modal.Hide(dialog);
            onWritten?.Invoke($"{code} {SalesCalendar.Label(month)} = {HudFormat.Number((long)rounded)} en «{CleanDataLevel.CleanSheetName}».");
        }

        private static int Decimals(double number) => Math.Abs(number - Math.Round(number)) < 1e-9 ? 0 : 2;
    }
}
