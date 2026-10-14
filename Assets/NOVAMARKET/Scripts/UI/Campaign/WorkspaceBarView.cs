using System;
using UnityEngine.UIElements;

namespace NovaMarket.UI.Campaign
{
    /// <summary>Which planner tool is in front.</summary>
    public enum WorkspaceWindow
    {
        None,
        Erp,
        Sheet
    }

    /// <summary>Top-left bar while playing a level: level name and the buttons NOVA ERP, Hoja del planeador, Conversación, Ayuda, Entregar and Mapa.</summary>
    public sealed class WorkspaceBarView
    {
        private readonly VisualElement bar;
        private readonly Label title;
        private readonly Button erpButton;
        private readonly Button sheetButton;

        public WorkspaceBarView(VisualElement bar, Action onErp, Action onSheet, Action onTalk, Action onHelp, Action onDeliver, Action onMap)
        {
            this.bar = bar;
            title = HudElements.Text(string.Empty, "workspace-title");
            bar.Add(title);
            erpButton = IconButton("NOVA ERP", HudIconKind.Erp, onErp);
            sheetButton = IconButton("Hoja del planeador", HudIconKind.Sheet, onSheet);
            bar.Add(erpButton);
            bar.Add(sheetButton);
            bar.Add(IconButton("Conversación", HudIconKind.Worker, onTalk));
            bar.Add(IconButton("Ayuda", HudIconKind.Info, onHelp));
            bar.Add(IconButton("Entregar", HudIconKind.Check, onDeliver).WithClass("workspace-button--deliver"));
            bar.Add(IconButton("Mapa", HudIconKind.Map, onMap));
        }

        public void Show(string levelTitle)
        {
            title.text = levelTitle;
            HudElements.SetVisible(bar, true);
        }

        public void Hide() => HudElements.SetVisible(bar, false);

        public void SetActive(WorkspaceWindow window)
        {
            erpButton.EnableInClassList("workspace-button--active", window == WorkspaceWindow.Erp);
            sheetButton.EnableInClassList("workspace-button--active", window == WorkspaceWindow.Sheet);
        }

        private static Button IconButton(string text, HudIconKind icon, Action onClick)
        {
            Button button = HudElements.Button(string.Empty, onClick, "workspace-button");
            button.Add(new HudIcon { Kind = icon });
            button.Add(new Label(text) { pickingMode = PickingMode.Ignore });
            return button;
        }
    }
}
