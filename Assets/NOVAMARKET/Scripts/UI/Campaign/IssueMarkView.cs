using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Erp;
using NovaMarket.Simulation.Levels;
using NovaMarket.Simulation.Portfolio;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// "Marcar problema" (level 1.2): for the selected cell the player chooses the kind of problem and
    /// the action (imputar, excluir, convertir), with an optional note. Marking documents the decision;
    /// the value is corrected in «Serie limpia», by hand or with a formula card.
    /// </summary>
    public sealed class IssueMarkView
    {
        private static readonly DataIssueKind[] Kinds =
            { DataIssueKind.FalseZero, DataIssueKind.OneOffOrder, DataIssueKind.PreLaunch, DataIssueKind.UnitsInBoxes };

        private static readonly CorrectionAction[] Actions = { CorrectionAction.Impute, CorrectionAction.Exclude, CorrectionAction.Convert };

        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly Label subtitle;
        private readonly List<Button> kindButtons = new List<Button>();
        private readonly List<Button> actionButtons = new List<Button>();
        private readonly TextField note;
        private readonly Button removeButton;
        private readonly Button cardsButton;
        private readonly Button saveButton;
        private CleanDataLevel level;
        private string code;
        private int month;
        private DataIssueKind? kind;
        private CorrectionAction? action;
        private Action<string, int, DataIssueKind> onCards;

        public IssueMarkView(ModalLayer modal)
        {
            this.modal = modal;
            dialog = modal.CreateDialog("mark-dialog");
            dialog.Add(HudElements.Text("Marcar un problema", "dialog-title"));
            dialog.Add(subtitle = HudElements.Text(string.Empty, "dialog-subtitle"));

            dialog.Add(HudElements.Text("TIPO DE PROBLEMA", "detail-section"));
            VisualElement kinds = HudElements.Box("chip-row");
            foreach (DataIssueKind k in Kinds)
            {
                Button button = HudElements.Button(DataIssue.Name(k), () => ChooseKind(k), "chip");
                kindButtons.Add(button);
                kinds.Add(button);
            }
            dialog.Add(kinds);

            dialog.Add(HudElements.Text("ACCIÓN", "detail-section"));
            VisualElement actions = HudElements.Box("chip-row");
            foreach (CorrectionAction a in Actions)
            {
                Button button = HudElements.Button(DataIssue.Name(a), () => ChooseAction(a), "chip");
                actionButtons.Add(button);
                actions.Add(button);
            }
            dialog.Add(actions);

            dialog.Add(HudElements.Text("NOTA PARA LA SUSTENTACIÓN (OPCIONAL)", "detail-section"));
            dialog.Add(note = new TextField { multiline = false }.WithClass("mark-note"));

            VisualElement tip = HudElements.Box("note-box", "note-box--hint");
            tip.Add(HudElements.Text("RECUERDA", "note-caption"));
            tip.Add(HudElements.Text("Marcar documenta la decisión, pero no cambia el dato: corrige el valor en «Serie limpia», " +
                                     "a mano, con tu propia fórmula o con una tarjeta de fórmula.", "note-text"));
            dialog.Add(tip);

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(HudElements.Button("Cancelar", Close, "dialog-button"));
            buttons.Add(removeButton = HudElements.Button("Quitar marca", Remove, "dialog-button"));
            buttons.Add(cardsButton = HudElements.Button("Tarjetas de fórmula", OpenCards, "dialog-button"));
            buttons.Add(saveButton = HudElements.Button("Guardar marca", Save, "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public bool IsOpen => modal.IsOpen && !dialog.ClassListContains("hidden");

        /// <param name="valueText">What the cell shows now.</param>
        /// <param name="openCards">Opens the formula cards for the chosen kind of problem.</param>
        public void Show(CleanDataLevel session, string sku, int monthIndex, string valueText, Action<string, int, DataIssueKind> openCards)
        {
            level = session;
            code = sku;
            month = monthIndex;
            onCards = openCards;
            subtitle.text = $"{sku} · {SalesCalendar.Label(monthIndex)} · la celda muestra {(string.IsNullOrEmpty(valueText) ? "(vacía)" : valueText)}";
            IssueMark mark = level.MarkAt(sku, monthIndex);
            kind = mark?.Kind;
            action = mark?.Action;
            note.SetValueWithoutNotify(mark?.Note ?? string.Empty);
            removeButton.SetEnabled(mark != null);
            Refresh();
            modal.Show(dialog);
        }

        // Exposed for the Play tests (they click the same buttons as the player).
        public void ChooseKind(DataIssueKind chosen)
        {
            kind = chosen;
            Refresh();
        }

        public void ChooseAction(CorrectionAction chosen)
        {
            action = chosen;
            Refresh();
        }

        public void Save()
        {
            if (kind == null || action == null) return;
            level.Mark(new IssueMark(code, month, kind.Value, action.Value, note.value));
            Close();
        }

        private void Remove()
        {
            level.Unmark(code, month);
            Close();
        }

        private void OpenCards()
        {
            if (kind == null) return;
            // With an action chosen the mark is saved too, so it is not lost on the way to the cards.
            if (action != null) level.Mark(new IssueMark(code, month, kind.Value, action.Value, note.value));
            modal.Hide(dialog);
            onCards?.Invoke(code, month, kind.Value);
        }

        private void Close() => modal.Hide(dialog);

        private void Refresh()
        {
            for (int i = 0; i < Kinds.Length; i++) kindButtons[i].EnableInClassList("chip--on", kind == Kinds[i]);
            for (int i = 0; i < Actions.Length; i++) actionButtons[i].EnableInClassList("chip--on", action == Actions[i]);
            saveButton.SetEnabled(kind != null && action != null);
            // Pre-launch months are only excluded: there is nothing to compute.
            cardsButton.SetEnabled(kind != null && kind != DataIssueKind.PreLaunch);
        }
    }
}
