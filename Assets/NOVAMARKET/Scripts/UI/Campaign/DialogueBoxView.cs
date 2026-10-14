using System.Collections.Generic;
using NovaMarket.Data;
using NovaMarket.Simulation.Levels;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>
    /// The characters' dialogue box at the bottom of the screen: badge, name, job and mood, the line and
    /// "Siguiente". Lines queue up (the intro, the hints after a download…) and stay in a history the
    /// player can read again with "Conversación". It floats over the ERP and the sheet without blocking them.
    /// </summary>
    public sealed class DialogueBoxView
    {
        private readonly CampaignDatabase database;
        private readonly VisualElement box;
        private readonly VisualElement badgeSlot;
        private readonly Label name;
        private readonly Label job;
        private readonly Label text;
        private readonly Label counter;
        private readonly Button previousButton;
        private readonly Button nextButton;
        private readonly List<DialogueLine> history = new List<DialogueLine>();
        private int shown = -1;

        public DialogueBoxView(VisualElement parent, CampaignDatabase database)
        {
            this.database = database;
            box = HudElements.Box("panel", "dialogue-box", "hidden");

            VisualElement header = HudElements.Box("dialogue-header");
            header.Add(badgeSlot = HudElements.Box("dialogue-badge"));
            VisualElement who = HudElements.Box("dialogue-who");
            who.Add(name = HudElements.Text(string.Empty, "dialogue-name"));
            who.Add(job = HudElements.Text(string.Empty, "dialogue-job"));
            header.Add(who);
            header.Add(HudElements.Box("planning-spacer"));
            header.Add(counter = HudElements.Text(string.Empty, "dialogue-counter"));
            box.Add(header);

            box.Add(text = HudElements.Text(string.Empty, "dialogue-text"));

            VisualElement buttons = HudElements.Box("dialogue-buttons");
            buttons.Add(previousButton = HudElements.Button("Anterior", () => ShowAt(shown - 1), "dialogue-button"));
            buttons.Add(HudElements.Button("Cerrar", Hide, "dialogue-button"));
            buttons.Add(nextButton = HudElements.Button("Siguiente", Next, "dialogue-button", "dialog-button--primary"));
            box.Add(buttons);
            parent.Add(box);
        }

        public bool IsVisible => !box.ClassListContains("hidden");

        /// <summary>The line on screen (null when closed).</summary>
        public DialogueLine Current => IsVisible && shown >= 0 ? history[shown] : null;

        /// <summary>Lines not read yet.</summary>
        public int Pending => history.Count - 1 - shown;

        /// <summary>Adds a line; the box opens on it unless the player is still reading an earlier one.</summary>
        public void Say(DialogueLine line)
        {
            history.Add(line);
            if (!IsVisible) ShowAt(shown + 1);
            else UpdateButtons();
        }

        /// <summary>Opens the conversation on the last line (the "Conversación" button).</summary>
        public void Reopen()
        {
            if (history.Count > 0) ShowAt(history.Count - 1);
        }

        public void Clear()
        {
            history.Clear();
            shown = -1;
            Hide();
        }

        public void Hide() => HudElements.SetVisible(box, false);

        private void Next()
        {
            if (shown < history.Count - 1) ShowAt(shown + 1);
            else Hide();
        }

        private void ShowAt(int index)
        {
            if (index < 0 || index >= history.Count) return;
            shown = index;
            DialogueLine line = history[index];
            CharacterData character = database.FindCharacter(line.CharacterId);
            badgeSlot.Clear();
            if (character != null) badgeSlot.Add(NpcBadge.Create(character));
            name.text = character != null ? character.DisplayName : "NOVAMARKET";
            job.text = character != null ? $"{character.ToProfile().Job} · {MoodName(line.Mood)}" : string.Empty;
            box.EnableInClassList("dialogue-box--worried", line.Mood == DialogueMood.Worried || line.Mood == DialogueMood.Serious);
            text.text = line.Text;
            HudElements.SetVisible(box, true);
            box.BringToFront();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            counter.text = $"{shown + 1} de {history.Count}";
            previousButton.SetEnabled(shown > 0);
            nextButton.text = shown < history.Count - 1 ? "Siguiente" : "Entendido";
        }

        private static string MoodName(DialogueMood mood) => mood switch
        {
            DialogueMood.Happy => "de buen ánimo",
            DialogueMood.Worried => "con preocupación",
            DialogueMood.Serious => "en tono serio",
            _ => "en tono tranquilo"
        };
    }
}
