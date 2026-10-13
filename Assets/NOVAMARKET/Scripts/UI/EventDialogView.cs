using System;
using NovaMarket.Simulation.Planning;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Window of a season event: the character (portrait, role, mood), what happened, the Atlas
    /// hint and one button per option. The week cannot be closed until an option is chosen.
    /// </summary>
    public sealed class EventDialogView
    {
        private readonly ModalLayer modal;
        private readonly VisualElement dialog;
        private readonly CharacterBadge badge;
        private readonly Label name;
        private readonly Label role;
        private readonly Label title;
        private readonly Label message;
        private readonly Label hint;
        private readonly VisualElement options;

        public EventDialogView(ModalLayer modal)
        {
            this.modal = modal;
            dialog = modal.CreateDialog();

            VisualElement header = HudElements.Box("event-header");
            header.Add(badge = new CharacterBadge(large: true));
            VisualElement who = HudElements.Box("event-who");
            who.Add(name = HudElements.Text(string.Empty, "event-name"));
            who.Add(role = HudElements.Text(string.Empty, "event-role"));
            header.Add(who);
            dialog.Add(header);

            dialog.Add(title = HudElements.Text(string.Empty, "event-title"));
            dialog.Add(message = HudElements.Text(string.Empty, "dialog-text"));

            VisualElement hintBox = HudElements.Box("note-box", "note-box--hint");
            hintBox.Add(HudElements.Text("PISTA DEL ATLAS", "note-caption"));
            hintBox.Add(hint = HudElements.Text(string.Empty, "note-text"));
            dialog.Add(hintBox);

            dialog.Add(options = HudElements.Box("dialog-buttons"));
        }

        public bool IsOpen => modal.IsOpen && !dialog.ClassListContains("hidden");

        /// <param name="onChoose">Called with the chosen option index after the window closes.</param>
        public void Show(EventDialog dialogData, Action<int> onChoose)
        {
            badge.Show(dialogData.Character, dialogData.Mood);
            name.text = PlanningCharacters.Name(dialogData.Character);
            role.text = $"{PlanningCharacters.Role(dialogData.Character)} · {PlanningCharacters.MoodLabel(dialogData.Mood)} · " +
                        $"semana {dialogData.Week}";
            title.text = dialogData.Title;
            message.text = dialogData.Message;
            hint.text = dialogData.AtlasHint;

            options.Clear();
            for (int i = 0; i < dialogData.Options.Count; i++)
            {
                int option = i;
                options.Add(HudElements.Button(dialogData.Options[i], () =>
                {
                    modal.Hide(dialog);
                    onChoose?.Invoke(option);
                }, "dialog-button", i == 0 ? "dialog-button--primary" : "dialog-button"));
            }
            modal.Show(dialog);
        }
    }
}
