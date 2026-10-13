using System;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>Short helpers to build HUD elements in code with their USS classes.</summary>
    public static class HudElements
    {
        public static VisualElement Box(params string[] classes)
        {
            var element = new VisualElement();
            foreach (string name in classes) element.AddToClassList(name);
            return element;
        }

        public static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (string name in classes) label.AddToClassList(name);
            return label;
        }

        /// <summary>A gold HUD button (<c>order-button</c> style) plus any extra classes.</summary>
        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("order-button");
            foreach (string name in classes) button.AddToClassList(name);
            return button;
        }

        public static void SetVisible(VisualElement element, bool visible) =>
            element.EnableInClassList("hidden", !visible);
    }
}
