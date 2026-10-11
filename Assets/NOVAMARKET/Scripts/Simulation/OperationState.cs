using System;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// Root of everything the HUD shows: mission, resources, notices and facilities. The HUD only
    /// reads this object and calls its methods; a future simulation replaces the sample data and
    /// drives the same methods, so the UI does not need to change.
    /// </summary>
    public sealed class OperationState
    {
        public OperationState(MissionState mission, ResourceState resources, NotificationFeed notifications,
            FacilityDirectory facilities)
        {
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        }

        /// <summary>Raised when the player gives an order: (facility id, order id).</summary>
        public event Action<string, string> OrderIssued;

        public MissionState Mission { get; }
        public ResourceState Resources { get; }
        public NotificationFeed Notifications { get; }
        public FacilityDirectory Facilities { get; }

        /// <summary>Passes a player order to the simulation and confirms it with a notice.</summary>
        public void IssueOrder(string facilityId, string orderId, double now)
        {
            if (!Facilities.TryGet(facilityId, out FacilityInfo facility))
                throw new ArgumentException($"Unknown facility '{facilityId}'.", nameof(facilityId));
            FacilityOrder order = facility.FindOrder(orderId)
                ?? throw new ArgumentException($"Facility '{facilityId}' has no order '{orderId}'.", nameof(orderId));

            OrderIssued?.Invoke(facilityId, orderId);
            Notifications.Push($"Orden enviada a {facility.Name}: {order.Label}.", NotificationSeverity.Info, now);
        }

        /// <summary>
        /// Counts one completed dispatch: advances the main counter and the matching objective,
        /// adds its points and announces it.
        /// </summary>
        public void RegisterDispatch(string objectiveId, int points, double now)
        {
            if (Mission.CounterValue >= Mission.CounterTarget) return;

            Mission.SetCounter(Mission.CounterValue + 1);
            if (objectiveId != null) Mission.AdvanceObjective(objectiveId);
            Mission.AddScore(points);

            string counter = HudFormat.Ratio(Mission.CounterValue, Mission.CounterTarget);
            Notifications.Push($"Despacho completado ({counter}). +{HudFormat.Number(points)} puntos.",
                NotificationSeverity.Success, now);
            if (Mission.CounterValue == Mission.CounterTarget)
                Notifications.Push("¡Meta de despachos cumplida!", NotificationSeverity.Success, now);
        }
    }
}
