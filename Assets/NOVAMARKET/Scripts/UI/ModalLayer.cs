using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Full-screen dark layer for dialogs (<c>modalLayer</c> in Hud.uxml). It blocks clicks on the
    /// scene while a dialog is open and shows one dialog at a time, centred.
    /// </summary>
    public sealed class ModalLayer
    {
        private readonly VisualElement layer;

        public ModalLayer(VisualElement root)
        {
            layer = root.Q("modalLayer");
        }

        public bool IsOpen => !layer.ClassListContains("hidden");

        /// <summary>Creates an empty, hidden dialog panel inside the layer.</summary>
        public VisualElement CreateDialog(params string[] classes)
        {
            VisualElement dialog = HudElements.Box("panel", "dialog", "hidden");
            foreach (string name in classes) dialog.AddToClassList(name);
            layer.Add(dialog);
            return dialog;
        }

        public void Show(VisualElement dialog)
        {
            foreach (VisualElement child in layer.Children()) HudElements.SetVisible(child, child == dialog);
            HudElements.SetVisible(layer, true);
        }

        public void Hide(VisualElement dialog)
        {
            HudElements.SetVisible(dialog, false);
            bool anyOpen = false;
            foreach (VisualElement child in layer.Children())
                if (!child.ClassListContains("hidden")) anyOpen = true;
            HudElements.SetVisible(layer, anyOpen);
        }

        public void HideAll()
        {
            foreach (VisualElement child in layer.Children()) HudElements.SetVisible(child, false);
            HudElements.SetVisible(layer, false);
        }
    }
}
