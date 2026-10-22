using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// People on the metro platforms: they come up the stairs and wait spread along the platform; when a
    /// train opens its doors, some get off and walk to the stairs, and those waiting walk to the nearest
    /// door and get on. Each station has two side platforms (one per track). Plain C#: hook it to a
    /// <see cref="MetroLine"/> and call <see cref="Tick"/>; the scene draws the people.
    /// </summary>
    public sealed class MetroPassengers
    {
        /// <summary>Doors of a car, from its centre (meters along the train).</summary>
        private static readonly float[] DoorOffsets = { -7.5f, -2.5f, 2.5f, 7.5f };

        private const float PlatformHeightBelowRails = 0.2f;
        private const float EdgeGap = 1.9f;      // platform edge, from the track centre
        private const float PlatformDepth = 6.3f; // usable depth, from the track centre
        private const float StairsFromCentre = 20f;
        private const float WalkSpeed = 1.3f;

        private readonly MetroLine line;
        private readonly Random random;
        private readonly List<Pedestrian> people = new List<Pedestrian>();
        private readonly Dictionary<Pedestrian, Plan> plans = new Dictionary<Pedestrian, Plan>();
        private int nextId = 100000;
        private float nextArrival;

        private enum Goal
        {
            ToSpot,
            Waiting,
            ToDoor,
            ToStairs
        }

        private sealed class Plan
        {
            public int Station;
            public MetroDirection Track;
            public Goal Goal;
            public Vector2 Target;
            public MetroTrain Train;
        }

        public MetroPassengers(MetroLine line, int waitingPerPlatform = 10, int seed = 9)
        {
            this.line = line ?? throw new ArgumentNullException(nameof(line));
            WaitingPerPlatform = waitingPerPlatform;
            random = new Random(seed);
            line.Arrived += OnArrived;
            line.Departed += OnDeparted;
            for (int station = 0; station < line.Definition.StationX.Count; station++)
            foreach (MetroDirection track in new[] { MetroDirection.Eastbound, MetroDirection.Westbound })
                for (int i = 0; i < waitingPerPlatform; i++)
                    Add(station, track, RandomSpot(station, track), Goal.Waiting);
        }

        public event Action<Pedestrian> Added;
        public event Action<Pedestrian> Removed;

        /// <summary>A person got on (true) or off (false) a train.</summary>
        public event Action<Pedestrian, MetroTrain, bool> Boarded;

        public int WaitingPerPlatform { get; }
        public IReadOnlyList<Pedestrian> People => people;

        /// <summary>Height of the platforms (just below the top of the rails).</summary>
        public float PlatformHeight => line.Definition.RailHeight - PlatformHeightBelowRails;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = people.Count - 1; i >= 0; i--) Move(people[i], deltaTime);

            // New people come up the stairs while a platform has fewer than wanted.
            nextArrival -= deltaTime;
            if (nextArrival > 0f) return;
            nextArrival = 2f + (float)random.NextDouble() * 4f;
            int station = random.Next(line.Definition.StationX.Count);
            MetroDirection track = random.Next(2) == 0 ? MetroDirection.Eastbound : MetroDirection.Westbound;
            if (CountOn(station, track) < WaitingPerPlatform) Add(station, track, Stairs(station, track), Goal.ToSpot);
        }

        private void Move(Pedestrian p, float dt)
        {
            Plan plan = plans[p];
            if (plan.Goal == Goal.Waiting)
            {
                p.Speed = 0f;
                return;
            }

            Vector2 to = plan.Target - p.Position;
            float distance = to.Length();
            float step = WalkSpeed * dt;
            if (distance > step)
            {
                p.Heading = to / distance;
                p.Position += p.Heading * step;
                p.Speed = WalkSpeed;
                return;
            }

            p.Position = plan.Target;
            p.Speed = 0f;
            switch (plan.Goal)
            {
                case Goal.ToSpot:
                    plan.Goal = Goal.Waiting;
                    p.Heading = TowardsTrack(plan.Track);
                    break;
                case Goal.ToDoor:
                    if (plan.Train != null && plan.Train.DoorsOpen)
                    {
                        Boarded?.Invoke(p, plan.Train, true);
                        Remove(p);
                    }
                    else
                    {
                        plan.Goal = Goal.ToSpot; // missed it: wait for the next one
                        plan.Target = RandomSpot(plan.Station, plan.Track);
                    }
                    break;
                case Goal.ToStairs:
                    Remove(p);
                    break;
            }
        }

        // ------------------------------------------------------------------ trains

        private void OnArrived(MetroTrain train, int station)
        {
            List<Vector2> doors = Doors(train, station);

            // Some get off and walk to the stairs.
            int off = 2 + random.Next(6);
            for (int i = 0; i < off; i++)
            {
                Pedestrian p = Add(station, train.Direction, doors[random.Next(doors.Count)], Goal.ToStairs);
                Boarded?.Invoke(p, train, false);
            }

            // Those waiting (and those still arriving) walk to the nearest door.
            foreach (Pedestrian p in people)
            {
                Plan plan = plans[p];
                if (plan.Station != station || plan.Track != train.Direction) continue;
                if (plan.Goal != Goal.Waiting && plan.Goal != Goal.ToSpot) continue;
                plan.Goal = Goal.ToDoor;
                plan.Train = train;
                plan.Target = Nearest(doors, p.Position);
            }
        }

        private void OnDeparted(MetroTrain train, int station)
        {
            foreach (Plan plan in plans.Values)
                if (plan.Train == train && plan.Goal == Goal.ToDoor)
                {
                    plan.Goal = Goal.ToSpot;
                    plan.Train = null;
                    plan.Target = RandomSpot(plan.Station, plan.Track);
                }
        }

        /// <summary>Door positions on the platform edge, on the side of the train's platform.</summary>
        private List<Vector2> Doors(MetroTrain train, int station)
        {
            var doors = new List<Vector2>();
            MetroSettings settings = line.Settings;
            float first = -train.Length / 2f + settings.CarLength / 2f;
            for (int car = 0; car < settings.Cars; car++)
            {
                float carCentre = first + car * (settings.CarLength + settings.CarGap);
                foreach (float door in DoorOffsets)
                    doors.Add(new Vector2(train.Position.X + carCentre + door, PlatformZ(train.Direction, EdgeGap + 0.4f)));
            }
            return doors;
        }

        private static Vector2 Nearest(List<Vector2> points, Vector2 from)
        {
            Vector2 best = points[0];
            foreach (Vector2 point in points)
                if (Vector2.DistanceSquared(point, from) < Vector2.DistanceSquared(best, from)) best = point;
            return best;
        }

        // ------------------------------------------------------------------ places

        private Pedestrian Add(int station, MetroDirection track, Vector2 at, Goal goal)
        {
            var p = new Pedestrian(nextId++, random.Next(16), WalkSpeed, 0f) { Position = at, Height = PlatformHeight };
            p.Heading = TowardsTrack(track);
            var plan = new Plan { Station = station, Track = track, Goal = goal };
            plan.Target = goal == Goal.ToStairs ? Stairs(station, track) : goal == Goal.ToSpot ? RandomSpot(station, track) : at;
            people.Add(p);
            plans[p] = plan;
            Added?.Invoke(p);
            return p;
        }

        private void Remove(Pedestrian p)
        {
            people.Remove(p);
            plans.Remove(p);
            Removed?.Invoke(p);
        }

        private int CountOn(int station, MetroDirection track)
        {
            int count = 0;
            foreach (Plan plan in plans.Values)
                if (plan.Station == station && plan.Track == track && plan.Goal != Goal.ToStairs) count++;
            return count;
        }

        /// <summary>A place to wait: anywhere along the train, between the edge and the back of the platform.</summary>
        private Vector2 RandomSpot(int station, MetroDirection track)
        {
            float half = line.Settings.TrainLength / 2f - 3f;
            float x = line.Definition.StationX[station] + (float)(random.NextDouble() * 2.0 - 1.0) * half;
            float depth = EdgeGap + 0.8f + (float)random.NextDouble() * (PlatformDepth - EdgeGap - 1.2f);
            return new Vector2(x, PlatformZ(track, depth));
        }

        /// <summary>Top of the stairs of a platform (the stair tower stands east of the station centre).</summary>
        private Vector2 Stairs(int station, MetroDirection track) =>
            new Vector2(line.Definition.StationX[station] + StairsFromCentre, PlatformZ(track, PlatformDepth - 0.5f));

        /// <summary>Z of a line on the platform of a track, <paramref name="fromTrack"/> meters from the track centre.</summary>
        private float PlatformZ(MetroDirection track, float fromTrack) => track == MetroDirection.Eastbound
            ? line.Definition.EastboundZ - fromTrack  // the eastbound track is the south one: its platform is further south
            : line.Definition.WestboundZ + fromTrack;

        private Vector2 TowardsTrack(MetroDirection track) =>
            track == MetroDirection.Eastbound ? new Vector2(0f, 1f) : new Vector2(0f, -1f);
    }
}
