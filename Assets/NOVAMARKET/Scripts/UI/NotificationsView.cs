using System.Collections.Generic;
using NovaMarket.Simulation;
using UnityEngine.UIElements;

namespace NovaMarket.UI
{
    /// <summary>
    /// Left-side notices. Each notice slides in when added to the <see cref="NotificationFeed"/>
    /// and fades out when the feed expires it (the animation is a USS transition).
    /// </summary>
    public sealed class NotificationsView
    {
        private const long ShowDelayMs = 20;
        private const long RemoveDelayMs = 450;

        private readonly NotificationFeed feed;
        private readonly VisualElement container;
        private readonly Dictionary<int, VisualElement> toasts = new Dictionary<int, VisualElement>();

        public NotificationsView(VisualElement root, NotificationFeed feed)
        {
            this.feed = feed;
            container = root.Q("notifications");
            feed.Added += Add;
            feed.Expired += Remove;
            foreach (HudNotification notification in feed.Active) Add(notification);
        }

        public void Dispose()
        {
            feed.Added -= Add;
            feed.Expired -= Remove;
        }

        private void Add(HudNotification notification)
        {
            var toast = new VisualElement();
            toast.AddToClassList("panel");
            toast.AddToClassList("toast");
            toast.AddToClassList("toast--" + notification.Severity.ToString().ToLowerInvariant());
            toast.AddToClassList("toast--hidden");
            toast.Add(new HudIcon { Kind = IconFor(notification.Severity) });

            var text = new Label(notification.Message);
            text.AddToClassList("toast-text");
            toast.Add(text);

            container.Add(toast);
            toasts[notification.Id] = toast;
            // Removing the class one frame later lets the transition animate the entrance.
            toast.schedule.Execute(() => toast.RemoveFromClassList("toast--hidden")).ExecuteLater(ShowDelayMs);
        }

        private void Remove(HudNotification notification)
        {
            if (!toasts.Remove(notification.Id, out VisualElement toast)) return;
            toast.AddToClassList("toast--hidden");
            toast.schedule.Execute(toast.RemoveFromHierarchy).ExecuteLater(RemoveDelayMs);
        }

        private static HudIconKind IconFor(NotificationSeverity severity) => severity switch
        {
            NotificationSeverity.Success => HudIconKind.Success,
            NotificationSeverity.Warning => HudIconKind.Warning,
            NotificationSeverity.Critical => HudIconKind.Critical,
            _ => HudIconKind.Info
        };
    }
}
