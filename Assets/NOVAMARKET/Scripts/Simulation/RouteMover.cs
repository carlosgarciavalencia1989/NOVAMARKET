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

        public RouteMover(Vector2 position, Vector2 heading)
        {
            Position = position;
            Heading = Vector2.Normalize(heading);
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
            Position = leg.Path.PointAt(DistanceOnLeg);
            Vector2 direction = leg.Path.DirectionAt(DistanceOnLeg);
            Heading = leg.Reverse ? -direction : direction;

            if (DistanceOnLeg >= leg.Path.Length - 1e-3f)
            {
                CurrentLeg = null;
                CompletedLegs++;
                if (leg.StopAtEnd) Speed = 0f;
                StartNextLeg();
            }
        }

        private bool StartNextLeg()
        {
            if (legs.Count == 0) return false;
            CurrentLeg = legs.Dequeue();
            DistanceOnLeg = 0f;
            return true;
        }
    }
}
