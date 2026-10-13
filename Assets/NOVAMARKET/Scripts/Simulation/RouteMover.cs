using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>One stretch of a vehicle's plan: a route driven forwards or in reverse.</summary>
    public sealed class RouteLeg
    {
        public RouteLeg(Polyline path, float maxSpeed, bool reverse = false, bool stopAtEnd = true)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            MaxSpeed = maxSpeed;
            Reverse = reverse;
            StopAtEnd = stopAtEnd;
        }

        public Polyline Path { get; }
        public float MaxSpeed { get; }

        /// <summary>Driven backwards: the vehicle faces opposite to the direction of travel.</summary>
        public bool Reverse { get; }

        /// <summary>Brake to a stop at the end (false only for the last stretch before despawning).</summary>
        public bool StopAtEnd { get; }
    }

    /// <summary>
    /// Drives a vehicle along a queue of <see cref="RouteLeg"/>s with simple acceleration and
    /// braking. An optional limit stops it early on the current leg (e.g. queued behind a truck).
    /// </summary>
    public sealed class RouteMover
    {
        private const float MinSpeed = 0.8f;
        private const float BrakeFactor = 0.6f; // target speed = remaining meters × factor
        private const float Acceleration = 2.5f;

        private readonly Queue<RouteLeg> legs = new Queue<RouteLeg>();

        public RouteMover(Vector2 position, Vector2 heading, float bodyLength = 0f)
        {
            Position = position;
            Heading = Vector2.Normalize(heading);
            BodyLength = bodyLength;
        }

        /// <summary>
        /// Long vehicles (trucks) keep their front and rear on the route and the body on the chord
        /// between them, so in a turn the cab does not stick out of the road. 0 = a point vehicle
        /// that faces the route direction (forklifts).
        /// </summary>
        public float BodyLength { get; }

        /// <summary>
        /// Pose of a body of <paramref name="length"/> whose centre is at <paramref name="distance"/> on the
        /// route: front and rear on the route (straight extensions past its ends), centre between them,
        /// heading from rear to front.
        /// </summary>
        /// <param name="previous">Route driven just before (same direction): the rear keeps following it
        /// instead of jumping onto a straight line when a new leg starts.</param>
        public static (Vector2 Centre, Vector2 Direction) ChordPose(Polyline path, float distance, float length, Polyline previous = null)
        {
            if (length <= 0f) return (path.PointAt(distance), path.DirectionAt(distance));
            float rearAt = distance - length / 2f;
            Vector2 front = Extend(path, distance + length / 2f);
            Vector2 rear = rearAt < 0f && previous != null ? Extend(previous, previous.Length + rearAt) : Extend(path, rearAt);
            Vector2 chord = front - rear;
            return ((front + rear) / 2f, chord.LengthSquared() > 1e-6f ? Vector2.Normalize(chord) : path.DirectionAt(distance));
        }

        /// <summary>Pose the vehicle would have at <paramref name="distance"/> on its current leg.</summary>
        public (Vector2 Centre, Vector2 Heading) PoseAhead(float distance)
        {
            if (CurrentLeg == null) return (Position, Heading);
            (Vector2 centre, Vector2 direction) = ChordPose(CurrentLeg.Path, Math.Min(distance, CurrentLeg.Path.Length), BodyLength, PreviousPath(CurrentLeg));
            return (centre, CurrentLeg.Reverse ? -direction : direction);
        }

        private static Vector2 Extend(Polyline path, float distance)
        {
            if (distance < 0f) return path.Start + path.DirectionAt(0f) * distance;
            if (distance > path.Length) return path.End + path.DirectionAt(path.Length) * (distance - path.Length);
            return path.PointAt(distance);
        }

        public Vector2 Position { get; private set; }

        /// <summary>Direction the vehicle's front points to (unit vector, X/Z).</summary>
        public Vector2 Heading { get; private set; }

        public float Speed { get; private set; }
        public RouteLeg CurrentLeg { get; private set; }
        public float DistanceOnLeg { get; private set; }

        /// <summary>Number of legs completed since the last <see cref="SetPlan"/>.</summary>
        public int CompletedLegs { get; private set; }

        public bool IsIdle => CurrentLeg == null && legs.Count == 0;
        public bool IsReversing => CurrentLeg != null && CurrentLeg.Reverse;

        public void SetPlan(IEnumerable<RouteLeg> plan)
        {
            if (CurrentLeg != null) previousLeg = CurrentLeg;
            legs.Clear();
            foreach (RouteLeg leg in plan) legs.Enqueue(leg);
            CurrentLeg = null;
            DistanceOnLeg = 0f;
            CompletedLegs = 0;
            StartNextLeg();
        }

        /// <param name="limit">Furthest distance allowed on the current leg this tick.</param>
        public void Tick(float deltaTime, float limit = float.MaxValue)
        {
            if (CurrentLeg == null && !StartNextLeg()) return;

            RouteLeg leg = CurrentLeg;
            float target = Math.Min(leg.Path.Length, limit);
            float remaining = target - DistanceOnLeg;
            bool mustStop = leg.StopAtEnd || target < leg.Path.Length;

            float desired = leg.MaxSpeed;
            if (mustStop) desired = Math.Min(desired, Math.Max(MinSpeed, remaining * BrakeFactor));
            if (remaining <= 0f) desired = 0f;
            Speed = desired < Speed ? desired : Math.Min(desired, Speed + Acceleration * deltaTime);

            DistanceOnLeg = Math.Min(target, DistanceOnLeg + Math.Max(0f, Speed) * deltaTime);
            (Vector2 centre, Vector2 direction) = ChordPose(leg.Path, DistanceOnLeg, BodyLength, PreviousPath(leg));
            Position = centre;
            Heading = leg.Reverse ? -direction : direction;

            if (DistanceOnLeg >= leg.Path.Length - 1e-3f)
            {
                previousLeg = leg;
                CurrentLeg = null;
                CompletedLegs++;
                if (leg.StopAtEnd) Speed = 0f;
                StartNextLeg();
            }
        }

        private RouteLeg previousLeg;

        /// <summary>The leg driven before <paramref name="leg"/> when it continues it (same direction, end meets start).</summary>
        private Polyline PreviousPath(RouteLeg leg) =>
            previousLeg != null && previousLeg.Reverse == leg.Reverse
            && Vector2.DistanceSquared(previousLeg.Path.End, leg.Path.Start) < 0.25f
                ? previousLeg.Path
                : null;

        private bool StartNextLeg()
        {
            if (legs.Count == 0) return false;
            CurrentLeg = legs.Dequeue();
            DistanceOnLeg = 0f;
            return true;
        }
    }
}
