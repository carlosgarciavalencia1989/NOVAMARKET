using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// A crossing where only one vehicle may be at a time (the others wait at its edge): the gate
    /// junction for trucks, and the points where two aisles merge for forklifts.
    /// </summary>
    public sealed class TrafficZone
    {
        public TrafficZone(string name, Vector2 min, Vector2 max, bool forTrucks)
        {
            Name = name;
            Min = min;
            Max = max;
            ForTrucks = forTrucks;
        }

        public string Name { get; }
        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public bool ForTrucks { get; }

        /// <summary>The vehicle crossing it (null when free).</summary>
        public VehicleAgent Holder { get; internal set; }

        /// <summary>True once the holder's body is inside (it is released when the body has left).</summary>
        internal bool HolderInside { get; set; }

        public bool Touches(Footprint footprint) => footprint.Overlaps(Min, Max);
    }

    /// <summary>
    /// Traffic rules of the CEDI: no vehicle drives into another one, crossings take one vehicle at
    /// a time, each side of docks has a manoeuvre turn (a truck reversing into a dock and one pulling
    /// out never meet) and a forklift backing out of a spot always finds the aisle free.
    /// </summary>
    public sealed partial class CediOperationSimulation
    {
        /// <summary>Space kept between two footprints (meters).</summary>
        public const float TruckGap = 0.3f;
        public const float ForkliftGap = 0.7f;

        /// <summary>Seconds two vehicles wait for each other before one goes first.</summary>
        public const float DeadlockSeconds = 3f;

        private const float LookStep = 0.4f;

        /// <summary>Distance below which two bodies are considered touching.</summary>
        private const float PhysicalContact = 0.05f;

        private readonly VehicleAgent[] manoeuvre = new VehicleAgent[2];
        private readonly List<(Vector2 Point, VehicleAgent Holder)> heldSpots = new List<(Vector2, VehicleAgent)>();
        private readonly List<TrafficZone> zones = new List<TrafficZone>
        {
            // Behind the gate the inbound and outbound lanes meet the two yard roads.
            new TrafficZone("cruce de la portería", new Vector2(-9f, -32f), new Vector2(9f, -15.5f), forTrucks: true),
            // Where a rack aisle or a bypass row joins a cross aisle (the north one covers two close merges).
            new TrafficZone("cruce norte del pasillo este", new Vector2(3f, 7.6f), new Vector2(8f, 13.7f), forTrucks: false),
            new TrafficZone("cruce del pasillo D", new Vector2(3f, 3.4f), new Vector2(8f, 7.6f), forTrucks: false),
            new TrafficZone("cruce del pasillo B", new Vector2(3f, -7.8f), new Vector2(8f, -3.4f), forTrucks: false),
            new TrafficZone("cruce del pasillo C", new Vector2(-14.9f, -2.2f), new Vector2(-10f, 2.2f), forTrucks: false)
        };

        public IReadOnlyList<TrafficZone> Zones => zones;

        // ------------------------------------------------------------------ manoeuvre turns

        /// <summary>The truck manoeuvring on a side of docks (null when the area is free).</summary>
        public VehicleAgent ManoeuvreHolder(DockKind side) => manoeuvre[(int)side];

        internal bool TryAcquireManoeuvre(DockKind side, VehicleAgent truck)
        {
            int index = (int)side;
            if (manoeuvre[index] != null && manoeuvre[index] != truck) return false;
            manoeuvre[index] = truck;
            return true;
        }

        internal void ReleaseManoeuvre(DockKind side, VehicleAgent truck)
        {
            if (manoeuvre[(int)side] == truck) manoeuvre[(int)side] = null;
        }

        // ------------------------------------------------------------------ spots

        /// <summary>
        /// A forklift reserves the spot it works at (staging lane or rack face) until it has backed out
        /// and driven away, so another one does not drive into the spur while it is still there.
        /// </summary>
        internal bool TryHoldSpot(ForkliftSpot spot, VehicleAgent forklift)
        {
            int index = heldSpots.FindIndex(h => Vector2.DistanceSquared(h.Point, spot.Point) < 1e-4f);
            if (index >= 0) return heldSpots[index].Holder == forklift;
            // The forklift closest to the spot goes first (the one in front on the aisle).
            if (forklift is ForkliftAgent me)
                foreach (VehicleAgent vehicle in vehicles)
                    if (vehicle is ForkliftAgent other && other != me && other.IsHeadingTo(spot) && other.DistanceToTarget < me.DistanceToTarget)
                        return false;
            heldSpots.Add((spot.Point, forklift));
            return true;
        }

        internal void ReleaseSpot(ForkliftSpot spot, VehicleAgent forklift) =>
            heldSpots.RemoveAll(h => h.Holder == forklift && Vector2.DistanceSquared(h.Point, spot.Point) < 1e-4f);

        // ------------------------------------------------------------------ driving

        /// <summary>
        /// Furthest distance on its current leg the vehicle may drive this tick: it looks ahead about a
        /// braking distance and stops before its body would come within the gap of another vehicle (or of
        /// the place a forklift is backing out to), or enter a crossing someone else is using. When two
        /// vehicles wait for each other for <see cref="DeadlockSeconds"/>, one goes first: never the one
        /// backing out, otherwise the older one (lower id); it only drops the gap, bodies never touch.
        /// </summary>
        /// <param name="limit">Where the vehicle has to stop anyway (gate queue, busy spot): it does not look
        /// or book crossings beyond it.</param>
        internal float TrafficLimit(VehicleAgent me, RouteMover mover, float deltaTime, float limit = float.MaxValue)
        {
            RouteLeg leg = mover.CurrentLeg;
            if (leg == null) return limit;
            float end = Math.Min(leg.Path.Length, limit);
            if (end <= mover.DistanceOnLeg + 0.05f && end < leg.Path.Length)
            {
                // Stopped for its own reasons (busy spot, gate queue): it keeps no crossing, the others may
                // use it (bodies are still kept apart).
                foreach (TrafficZone zone in zones)
                    if (zone.Holder == me) zone.Holder = null;
                return limit;
            }

            bool truck = me.Kind != VehicleKind.Forklift;
            float gap = truck ? TruckGap : ForkliftGap;
            Footprint now = me.Footprint;
            UpdateZones(me, now);

            float lookahead = Math.Max(1.5f, mover.Speed * 1.5f + 1f);
            VehicleAgent blocker = null;
            bool waitingForZone = false;
            var reached = new List<TrafficZone>();
            float allowed = float.MaxValue;

            for (float s = LookStep; blocker == null && s <= lookahead + 1e-3f; s += LookStep)
            {
                float distance = Math.Min(mover.DistanceOnLeg + s, end);
                (Vector2 centre, Vector2 heading) = mover.PoseAhead(distance);
                Footprint ahead = me.FootprintAt(centre, heading);
                float stopAt = Math.Max(mover.DistanceOnLeg, distance - LookStep);

                foreach (TrafficZone zone in zones)
                {
                    // A vehicle already (partly) inside a crossing never waits for it: it finishes leaving.
                    if (zone.ForTrucks != truck || !zone.Touches(ahead) || zone.Touches(now) && zone.Holder != null && zone.Holder != me) continue;
                    reached.Add(zone);
                    if (zone.Holder == me) continue;
                    if (zone.Holder == null)
                    {
                        zone.Holder = me;
                        zone.HolderInside = false;
                        continue;
                    }
                    blocker = zone.Holder;
                    waitingForZone = true;
                    allowed = stopAt;
                    break;
                }
                if (blocker != null) break;

                foreach (VehicleAgent other in vehicles)
                {
                    if (other == me || Math.Abs(other.FloorHeight - me.FloorHeight) > 0.01f) continue;
                    if (Vector2.DistanceSquared(other.Position, me.Position) > 40f * 40f) continue;
                    Footprint theirs = other.Footprint;
                    bool blocks = other == me.GoesFirstPast
                        ? GetsCloser(ahead, now, theirs, PhysicalContact)
                        : GetsCloser(ahead, now, theirs, gap)
                          || other.BackingOutTarget is Footprint target && GetsCloser(ahead, now, target, gap);
                    if (!blocks) continue;
                    blocker = other;
                    allowed = stopAt;
                    break;
                }
                if (distance >= end) break;
            }

            // A crossing is kept only while the vehicle is inside it or driving into it: one that stops
            // for another reason lets it go, so nobody holds a crossing while waiting somewhere else.
            foreach (TrafficZone zone in zones)
                if (zone.Holder == me && !zone.HolderInside && (blocker != null || !reached.Contains(zone)))
                    zone.Holder = null;

            if (blocker == null)
            {
                me.BlockedSeconds = 0f;
                if (me.GoesFirstPast != null && !now.Overlaps(me.GoesFirstPast.Footprint, gap * 4f)) me.GoesFirstPast = null;
                return limit;
            }

            me.BlockedSeconds = me.PreviousBlocker == blocker ? me.BlockedSeconds + deltaTime : 0f;
            me.BlockedBy = blocker;
            bool mutual = blocker.BlockedBy == me || blocker.PreviousBlocker == me && blocker.BlockedBy == null;
            bool meBacking = mover.IsReversing, theyBacking = blocker.IsBackingOut;
            bool goFirst = meBacking != theyBacking ? !meBacking : me.Id < blocker.Id;
            if (!waitingForZone && me.BlockedSeconds > DeadlockSeconds && mutual && goFirst)
            {
                me.GoesFirstPast = blocker;
                me.BlockedBy = null;
                me.BlockedSeconds = 0f;
                return limit;
            }
            return Math.Min(allowed, limit);
        }

        /// <summary>Marks the crossings the vehicle has entered and frees the ones it has left.</summary>
        private void UpdateZones(VehicleAgent me, Footprint now)
        {
            foreach (TrafficZone zone in zones)
            {
                if (zone.Holder != me) continue;
                if (zone.Touches(now)) zone.HolderInside = true;
                else if (zone.HolderInside) zone.Holder = null;
            }
        }

        /// <summary>True when moving to <paramref name="ahead"/> comes within the gap of <paramref name="theirs"/> and closer than now.</summary>
        private static bool GetsCloser(Footprint ahead, Footprint now, Footprint theirs, float gap)
        {
            float next = ahead.Separation(theirs);
            return next < gap && next < now.Separation(theirs) - 1e-3f;
        }

        /// <summary>Who holds each crossing and turn, and who is waiting for whom (for tests and debugging).</summary>
        public string TrafficReport()
        {
            var report = new System.Text.StringBuilder();
            foreach (TrafficZone zone in zones)
                if (zone.Holder != null) report.AppendLine($"{zone.Name}: {zone.Holder.Plate}{(zone.HolderInside ? " (dentro)" : "")}");
            foreach (DockKind side in new[] { DockKind.Receiving, DockKind.Shipping })
                if (ManoeuvreHolder(side) != null) report.AppendLine($"maniobra {side}: {ManoeuvreHolder(side).Plate}");
            foreach ((Vector2 point, VehicleAgent holder) in heldSpots) report.AppendLine($"puesto {point}: {holder.Plate}");
            foreach (VehicleAgent vehicle in vehicles)
                report.AppendLine($"{vehicle.Plate} ({vehicle.Position.X:F1}, {vehicle.Position.Y:F1}) {vehicle.Status}" +
                                  $"{(vehicle.BlockedBy != null ? " espera a " + vehicle.BlockedBy.Plate : "")}");
            return report.ToString();
        }

        private void ForgetInTraffic(VehicleAgent gone)
        {
            foreach (TrafficZone zone in zones)
                if (zone.Holder == gone) zone.Holder = null;
            for (int side = 0; side < manoeuvre.Length; side++)
                if (manoeuvre[side] == gone) manoeuvre[side] = null;
            heldSpots.RemoveAll(h => h.Holder == gone);
            foreach (VehicleAgent vehicle in vehicles)
            {
                if (vehicle.BlockedBy == gone) vehicle.BlockedBy = null;
                if (vehicle.PreviousBlocker == gone) vehicle.PreviousBlocker = null;
                if (vehicle.GoesFirstPast == gone) vehicle.GoesFirstPast = null;
            }
        }
    }
}
