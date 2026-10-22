using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>How many people walk around the camera and how they behave (plain C#).</summary>
    public sealed class PedestrianSettings
    {
        public int Count { get; set; } = 150;

        /// <summary>Chance, at each corner, of stopping for a while.</summary>
        public float StandChance { get; set; } = 0.04f;

        public float MinStandSeconds { get; set; } = 3f;
        public float MaxStandSeconds { get; set; } = 12f;

        /// <summary>A person waiting longer than this at a kerb gives up and walks another way.</summary>
        public float CrossingPatienceSeconds { get; set; } = 45f;

        /// <summary>At lights, people start crossing only while the other street still has this much green.</summary>
        public float MinGreenToCross { get; set; } = 5f;

        public int MaxSpawnsPerTick { get; set; } = 8;
    }

    /// <summary>
    /// People walking on the sidewalks, park paths and zebra crossings around the camera: they keep to the
    /// right, stop now and then, and cross the streets when the lights let them or when there is a gap in
    /// the traffic. While crossing they are given to the traffic as bodies, so vehicles wait for them.
    /// Plain C#: call <see cref="Tick"/>; the scene only draws the people.
    /// </summary>
    public sealed class AmbientPedestrians : ISharedRoadVehicles
    {
        private const float FollowGap = 0.9f;

        private readonly List<Pedestrian> people = new List<Pedestrian>();
        private readonly List<WalkEdge> nearEdges = new List<WalkEdge>();
        private readonly Random random;
        private int nextId = 1;
        private bool filled;
        private Vector2 filledAt;

        public AmbientPedestrians(WalkNetwork network, AmbientTraffic traffic = null, PedestrianSettings settings = null, int seed = 5)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Traffic = traffic;
            Settings = settings ?? new PedestrianSettings();
            random = new Random(seed);
        }

        public event Action<Pedestrian> Added;
        public event Action<Pedestrian> Removed;

        /// <summary>A person stepped onto a zebra crossing (for tests).</summary>
        public event Action<Pedestrian, WalkEdge> StartedCrossing;

        public WalkNetwork Network { get; }
        public AmbientTraffic Traffic { get; }
        public PedestrianSettings Settings { get; }
        public IReadOnlyList<Pedestrian> People => people;
        public double Time { get; private set; }

        public Vector2 Focus { get; private set; }
        public float Radius { get; private set; } = 250f;

        public void Tick(float deltaTime, Vector2 focus, float radius)
        {
            if (deltaTime <= 0f) return;
            deltaTime = Math.Min(deltaTime, 0.25f);
            Time += deltaTime;
            Focus = focus;
            Radius = radius;
            foreach (Pedestrian person in people) Walk(person, deltaTime);
            RemoveFar();
            // After a jump of the camera (e.g. a click on the minimap) the new place fills at once, not from its edge.
            bool jumped = Vector2.Distance(focus, filledAt) > radius * 0.5f && people.Count < Settings.Count / 2;
            Spawn(!filled || jumped);
            if (!filled || jumped) filledAt = focus;
            filled = true;
        }

        /// <summary>People on zebra crossings, for the traffic (they are never parked: vehicles wait).</summary>
        public void CollectSharedRoadVehicles(List<SharedRoadVehicle> into)
        {
            foreach (Pedestrian person in people)
                if (person.State == PedestrianState.Crossing)
                    into.Add(new SharedRoadVehicle(person.Footprint, person.Speed, false));
        }

        public void Clear()
        {
            for (int i = people.Count - 1; i >= 0; i--) Remove(i);
            filled = false;
        }

        // ------------------------------------------------------------------ walking

        private void Walk(Pedestrian p, float dt)
        {
            switch (p.State)
            {
                case PedestrianState.Standing:
                    p.Speed = 0f;
                    if ((p.Timer -= dt) <= 0f) p.State = PedestrianState.Walking;
                    return;

                case PedestrianState.WaitingToCross:
                    p.Speed = 0f;
                    p.Timer += dt;
                    if (CanCross(p.Edge, p.Forward))
                    {
                        p.State = PedestrianState.Crossing;
                        p.Timer = 0f;
                        StartedCrossing?.Invoke(p, p.Edge);
                    }
                    else if (p.Timer > Settings.CrossingPatienceSeconds)
                        TurnAway(p);
                    return;
            }

            // On a crossing, never walk into a vehicle; after a while blocked, step back the other way (the vehicle waits for them).
            if (p.State == PedestrianState.Crossing && VehicleAhead(p))
            {
                p.Speed = 0f;
                p.Timer += dt;
                if (p.Timer > 3f)
                {
                    p.Forward = !p.Forward;
                    p.Along = p.Edge.Length - p.Along;
                    p.Timer = 0f;
                    Place(p);
                }
                return;
            }

            float speed = p.DesiredSpeed * (p.State == PedestrianState.Crossing ? 1.15f : 1f);
            speed = Math.Min(speed, SpeedBehind(p, speed));
            p.Speed = speed;
            p.Along += speed * dt;
            if (p.Along >= p.Edge.Length) ArriveAtNode(p);
            Place(p);
        }

        /// <summary>Slows down behind someone slower walking the same way on the same side.</summary>
        private static float SpeedBehind(Pedestrian p, float speed)
        {
            foreach (Pedestrian other in p.Edge.Occupants)
            {
                if (other == p || other.Forward != p.Forward || Math.Abs(other.Side - p.Side) > 0.5f) continue;
                float ahead = other.Along - p.Along;
                if (ahead > 0f && ahead < FollowGap * 2f) speed = Math.Min(speed, ahead < FollowGap ? 0f : other.Speed);
            }
            return speed;
        }

        private void ArriveAtNode(Pedestrian p)
        {
            WalkNode node = p.Target;
            WalkEdge from = p.Edge;
            if (from.IsCrosswalk) p.State = PedestrianState.Walking;
            WalkEdge next = ChooseNext(node, from);
            Enter(p, next, node, from);
            if (next.IsCrosswalk) StartWaiting(p);
            else if (random.NextDouble() < Settings.StandChance)
            {
                p.State = PedestrianState.Standing;
                p.Timer = Settings.MinStandSeconds + (float)random.NextDouble() * (Settings.MaxStandSeconds - Settings.MinStandSeconds);
            }
        }

        /// <summary>Any path but the one it came from (unless it is a dead end); crossings a bit less often.</summary>
        private WalkEdge ChooseNext(WalkNode node, WalkEdge from)
        {
            float total = 0f;
            foreach (WalkEdge edge in node.Edges)
                if (edge != from) total += Weight(edge);
            if (total <= 0f) return from;
            float pick = (float)random.NextDouble() * total;
            foreach (WalkEdge edge in node.Edges)
            {
                if (edge == from) continue;
                pick -= Weight(edge);
                if (pick <= 0f) return edge;
            }
            return from;
        }

        private static float Weight(WalkEdge edge) => edge.Kind switch
        {
            WalkEdgeKind.Crosswalk => 0.7f,
            WalkEdgeKind.ParkPath => 1.5f,
            _ => 1f
        };

        private static void Enter(Pedestrian p, WalkEdge edge, WalkNode from, WalkEdge cameFrom)
        {
            p.Edge?.Occupants.Remove(p);
            p.Edge = edge;
            p.Forward = edge.A == from;
            p.Along = 0f;
            p.CameFrom = cameFrom;
            edge.Occupants.Add(p);
        }

        private static void StartWaiting(Pedestrian p)
        {
            p.State = PedestrianState.WaitingToCross;
            p.Timer = 0f;
        }

        /// <summary>Gave up crossing: walks back along the sidewalk it came from.</summary>
        private void TurnAway(Pedestrian p)
        {
            WalkNode node = p.Start;
            WalkEdge back = p.CameFrom ?? ChooseNext(node, p.Edge);
            if (back == null || back.IsCrosswalk) back = ChooseNext(node, p.Edge);
            if (back.IsCrosswalk) return; // nowhere else to go: keep waiting
            Enter(p, back, node, p.Edge);
            p.State = PedestrianState.Walking;
        }

        private static void Place(Pedestrian p)
        {
            WalkEdge edge = p.Edge;
            float fromA = p.Forward ? p.Along : edge.Length - p.Along;
            fromA = Math.Max(0f, Math.Min(edge.Length, fromA));
            Vector2 direction = p.WalkDirection;
            var right = new Vector2(direction.Y, -direction.X);
            float side = Math.Min(p.Side, edge.HalfWidth);
            p.Position = edge.PointAt(fromA) + right * side;
            p.Height = edge.HeightAt(fromA);
            p.Heading = direction;
        }

        // ------------------------------------------------------------------ crossing streets

        /// <summary>
        /// With lights: while the crossed street has red and the other street still has some green. Without:
        /// when no vehicle comes on the arriving lanes, none is about to drive over the crossing from the
        /// crossroads and none stands on it.
        /// </summary>
        public bool CanCross(WalkEdge crossing, bool forward)
        {
            if (Traffic == null) return true;
            if (BodyOnCrossing(crossing)) return false;

            // Vehicles turning in from the crossroads drive over the crossing (also on green for the people).
            foreach (TrafficLane lane in crossing.LeavingLanes)
            {
                foreach (AmbientVehicle v in Traffic.VehiclesOn(lane))
                    if (v.Front - v.Length < 8f) return false;
                if (Traffic.IsEnteringLane(lane)) return false;
            }

            TrafficSignal signal = crossing.Signal;
            if (signal != null)
            {
                if (signal.AspectFor(crossing.CrossedGroup, Traffic.Time) != SignalAspect.Red) return false;
                return signal.GreenLeft(1 - crossing.CrossedGroup, Traffic.Time) >= Settings.MinGreenToCross;
            }

            foreach (TrafficLane lane in crossing.ArrivingLanes)
            foreach (AmbientVehicle v in Traffic.VehiclesOn(lane))
            {
                float toLine = lane.Length - lane.StopLineBack - v.Front;
                if (toLine < 25f + v.Speed * 2f && v.Speed > 1f || toLine < 1f && v.Booked != null) return false;
            }
            return true;
        }

        private bool VehicleAhead(Pedestrian p)
        {
            if (Traffic == null) return false;
            Footprint now = p.Footprint;
            var ahead = new Footprint(p.Position + p.WalkDirection * 0.7f, p.WalkDirection, Pedestrian.BodySize, Pedestrian.BodySize);
            foreach (AmbientVehicle v in Traffic.Vehicles)
            {
                if (Vector2.DistanceSquared(v.Position, p.Position) > 225f) continue;
                Footprint body = v.Footprint;
                if (AmbientTraffic.Overlaps(ahead, body, 0.3f) && AmbientTraffic.Separation(ahead, body) < AmbientTraffic.Separation(now, body))
                    return true;
            }
            return false;
        }

        private bool BodyOnCrossing(WalkEdge crossing)
        {
            Vector2 centre = (crossing.A.Position + crossing.B.Position) / 2f;
            var band = new Footprint(centre, crossing.Direction, crossing.Length, CityWalkLayout.CrosswalkWidth + 1f);
            foreach (AmbientVehicle v in Traffic.Vehicles)
                if (Vector2.DistanceSquared(v.Position, centre) < 400f && AmbientTraffic.Overlaps(band, v.Footprint, 0f))
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ appearing and leaving

        private void Spawn(bool anywhere)
        {
            int missing = Settings.Count - people.Count;
            if (missing <= 0) return;
            int budget = anywhere ? missing : Math.Min(missing, Settings.MaxSpawnsPerTick);

            Network.EdgesNear(Focus, Radius, nearEdges);
            float total = 0f;
            foreach (WalkEdge edge in nearEdges) total += edge.Length * edge.Busyness;
            if (total <= 0f) return;

            int spawned = 0;
            for (int attempt = 0; attempt < budget * 6 && spawned < budget; attempt++)
            {
                WalkEdge edge = PickEdge(total);
                if (edge == null || edge.IsCrosswalk || edge.Busyness <= 0f) continue;
                float along = (float)random.NextDouble() * edge.Length;
                float distance = Vector2.Distance(edge.PointAt(along), Focus);
                if (distance > Radius || !anywhere && distance < Radius * 0.75f) continue;

                var person = new Pedestrian(nextId++, random.Next(16), 1.05f + 0.5f * (float)random.NextDouble(),
                    0.3f + 0.6f * (float)random.NextDouble());
                bool forward = random.NextDouble() < 0.5;
                Enter(person, edge, forward ? edge.A : edge.B, null);
                person.Along = forward ? along : edge.Length - along;
                Place(person);
                people.Add(person);
                spawned++;
                Added?.Invoke(person);
            }
        }

        private WalkEdge PickEdge(float total)
        {
            float pick = (float)random.NextDouble() * total;
            foreach (WalkEdge edge in nearEdges)
            {
                pick -= edge.Length * edge.Busyness;
                if (pick <= 0f) return edge;
            }
            return nearEdges.Count > 0 ? nearEdges[nearEdges.Count - 1] : null;
        }

        private void RemoveFar()
        {
            float keep = Radius * 1.15f + 20f;
            for (int i = people.Count - 1; i >= 0; i--)
            {
                Pedestrian p = people[i];
                if (p.State == PedestrianState.Crossing) continue; // never vanish in the middle of a street
                if (p.Done || Vector2.DistanceSquared(p.Position, Focus) > keep * keep) Remove(i);
            }
        }

        private void Remove(int index)
        {
            Pedestrian p = people[index];
            p.Edge?.Occupants.Remove(p);
            people.RemoveAt(index);
            Removed?.Invoke(p);
        }
    }
}
