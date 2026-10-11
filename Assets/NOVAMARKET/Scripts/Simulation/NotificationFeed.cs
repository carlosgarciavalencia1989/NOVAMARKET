using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>How important a HUD notice is; the HUD picks its colour and icon from this.</summary>
    public enum NotificationSeverity
    {
        Info,
        Success,
        Warning,
        Critical
    }

    /// <summary>A short message shown on the left of the HUD for a few seconds.</summary>
    public sealed class HudNotification
    {
        internal HudNotification(int id, string message, NotificationSeverity severity, double createdAt, double expiresAt)
        {
            Id = id;
            Message = message;
            Severity = severity;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public int Id { get; }
        public string Message { get; }
        public NotificationSeverity Severity { get; }
        public double CreatedAt { get; }
        public double ExpiresAt { get; }
    }

    /// <summary>
    /// Queue of timed notices. Time is passed in (seconds) instead of read from a clock, so the
    /// logic runs in tests and can follow the simulation clock later. When more than
    /// <see cref="MaxVisible"/> notices are active, the oldest one expires early.
    /// </summary>
    public sealed class NotificationFeed
    {
        private readonly List<HudNotification> active = new List<HudNotification>();
        private int nextId = 1;

        public NotificationFeed(double lifetimeSeconds = 7.0, int maxVisible = 4)
        {
            if (lifetimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            if (maxVisible < 1) throw new ArgumentOutOfRangeException(nameof(maxVisible));
            LifetimeSeconds = lifetimeSeconds;
            MaxVisible = maxVisible;
        }

        public event Action<HudNotification> Added;
        public event Action<HudNotification> Expired;

        public double LifetimeSeconds { get; }
        public int MaxVisible { get; }

        /// <summary>Active notices, oldest first.</summary>
        public IReadOnlyList<HudNotification> Active => active;

        public HudNotification Push(string message, NotificationSeverity severity, double now)
        {
            var notification = new HudNotification(nextId++, message, severity, now, now + LifetimeSeconds);
            active.Add(notification);
            Added?.Invoke(notification);

            while (active.Count > MaxVisible) Expire(0);
            return notification;
        }

        /// <summary>Removes every notice whose time is up.</summary>
        public void Tick(double now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (now >= active[i].ExpiresAt) Expire(i);
        }

        private void Expire(int index)
        {
            HudNotification notification = active[index];
            active.RemoveAt(index);
            Expired?.Invoke(notification);
        }
    }
}
