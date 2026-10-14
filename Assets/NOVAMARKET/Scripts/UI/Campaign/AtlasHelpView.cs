using System;
using NovaMarket.Simulation.Levels;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// "Ayuda": one row per topic of the level with a free hint from Sofía or Andrés (when the dialogue
    /// data has one) and the solved step of the Atlas, which costs the "Sin ayuda del Atlas" star.
    /// </summary>
    public sealed class AtlasHelpView
    {
        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly VisualElement topics;
        private readonly VisualElement answer;
        private readonly Label answerTitle;
        private readonly VisualElement steps;
        private readonly Label status;
        private LevelSession session;
        private Action<DialogueLine> onHint;

        public AtlasHelpView(ModalLayer modal)
        {
            this.modal = modal;
            dialog = modal.CreateDialog("dialog--wide", "help-dialog");
            dialog.Add(HudElements.Text("Ayuda", "dialog-title"));
            dialog.Add(HudElements.Text("Las pistas de tus compañeros son gratis. La solución del Atlas te muestra el paso resuelto, " +
                                        "pero cuesta la estrella «Sin ayuda del Atlas» y queda en el informe del nivel.", "dialog-text"));
            dialog.Add(topics = HudElements.Box("help-topics"));

            answer = HudElements.Box("note-box", "hidden");
            answer.Add(answerTitle = HudElements.Text(string.Empty, "note-caption"));
            answer.Add(steps = HudElements.Box("atlas-steps"));
            dialog.Add(answer);

            VisualElement buttons = HudElements.Box("dialog-buttons");
            buttons.Add(status = HudElements.Text(string.Empty, "help-status"));
            buttons.Add(HudElements.Box("planning-spacer"));
            buttons.Add(HudElements.Button("Cerrar", () => modal.Hide(dialog), "dialog-button", "dialog-button--primary"));
            dialog.Add(buttons);
        }

        public bool IsOpen => modal.IsOpen && !dialog.ClassListContains("hidden");

        /// <param name="hint">Sends a free hint to the dialogue box (the window closes so it can be read).</param>
        public void Show(LevelSession level, Action<DialogueLine> hint)
        {
            session = level;
            onHint = hint;
            topics.Clear();
            foreach (string topic in level.HelpTopics)
            {
                VisualElement row = HudElements.Box("help-topic");
                row.Add(HudElements.Text(level.TopicName(topic), "help-topic-name"));
                DialogueLine line = level.HintFor(topic);
                Button free = HudElements.Button("Pista gratis", () =>
                {
                    modal.Hide(dialog);
                    onHint?.Invoke(line);
                }, "dialog-button");
                free.SetEnabled(line != null);
                row.Add(free);
                row.Add(HudElements.Button("Solución del Atlas", () => Solve(topic), "dialog-button", "help-solve"));
                topics.Add(row);
            }
            HudElements.SetVisible(answer, false);
            UpdateStatus();
            modal.Show(dialog);
        }

        public void Solve(string topic)
        {
            AtlasAnswer solved = session.AskAtlas(topic);
            answerTitle.text = $"ATLAS · {solved.Title}".ToUpperInvariant();
            steps.Clear();
            for (int i = 0; i < solved.Steps.Count; i++)
            {
                VisualElement step = HudElements.Box("atlas-step");
                step.Add(HudElements.Text((i + 1).ToString(), "atlas-step-number"));
                step.Add(HudElements.Text(solved.Steps[i], "atlas-step-text"));
                steps.Add(step);
            }
            HudElements.SetVisible(answer, true);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            int used = session.Help.Topics.Count;
            status.text = used == 0
                ? "Todavía tienes la estrella «Sin ayuda del Atlas»."
                : $"Usaste la solución del Atlas {used} {(used == 1 ? "vez" : "veces")}: ya no ganas esa estrella.";
        }
    }
}
