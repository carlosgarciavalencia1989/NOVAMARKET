using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// One gate lane (in or out): its barrier and the trucks queued at its stop line, first in
    /// first out. Each queued truck stops behind the one ahead with a safety gap.
    /// </summary>
    public sealed class GateLane
    {
        private readonly List<TruckAgent> queue = new List<TruckAgent>();
        private readonly float gap;

        public GateLane(float gap)
        {
            this.gap = gap;
        }

        public bool IsBarrierOpen { get; internal set; }
        public int QueueLength => queue.Count;
        public IReadOnlyList<TruckAgent> Queue => queue;

        internal void Join(TruckAgent truck)
        {
            if (!queue.Contains(truck)) queue.Add(truck);
        }

        internal void Leave(TruckAgent truck) => queue.Remove(truck);

        public bool IsFirst(TruckAgent truck) => queue.Count > 0 && queue[0] == truck;

        /// <summary>
        /// How far along its route (ending at the stop line) a queued truck may drive: the end,
        /// minus the length of every truck ahead plus a gap each.
        /// </summary>
        public float LimitFor(TruckAgent truck, float routeLength)
        {
            float limit = routeLength;
            foreach (TruckAgent ahead in queue)
            {
                if (ahead == truck) return limit;
                limit -= ahead.Length + gap;
            }
            return limit;
        }
    }
}
