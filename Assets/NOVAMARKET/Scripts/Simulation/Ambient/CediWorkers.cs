using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation.Ambient
{
    /// <summary>
    /// The people of the CEDI: the guard on the gate island, pickers walking between the pick stations, staff
    /// walking between the parking and the office (they wait while a truck is close to the office door), and
    /// two people chatting by the office. Their places match the CEDI blockout. Plain C#: call
    /// <see cref="Tick"/>; the scene draws the people.
    /// </summary>
    public sealed class CediWorkers
    {
        /// <summary>Walkway between two pick stations (x) and how far along them people walk (z).</summary>
        public static readonly float[] PickerLanesX = { 8.35f, 10.65f };
        public const float PickerHalfReach = 5.5f;
        public const float WarehouseFloor = CediLayout.IndoorFloor + 0.03f;

        /// <summary>The guard paces the middle of the gate island, north of the booth (trucks turn at its ends).</summary>
        public static readonly Vector2 GuardNorth = new Vector2(0f, -35.3f);
        public static readonly Vector2 GuardSouth = new Vector2(0f, -36.8f);
        public const float IslandTop = 0.255f;

        public static readonly Vector2 OfficeDoor = new Vector2(-53f, -17.9f);
        public static readonly Vector2 OfficeCorner = new Vector2(-58f, -17.9f);
        public static readonly Vector2 ParkingCorner = new Vector2(-58f, -38f);

        /// <summary>No truck may be closer than this to the office door while someone walks to or from it.</summary>
        public const float DoorClearance = 9f;

        private const float WalkSpeed = 1.25f;

        private readonly CediOperationSimulation cedi;
        private readonly Random random;
        private readonly List<Pedestrian> people = new List<Pedestrian>();
        private readonly List<Worker> workers = new List<Worker>();

        private enum Role
        {
            Guard,
            Picker,
            Commuter,
            Chatting
        }

        private sealed class Worker
        {
            public Role Role;
            public Pedestrian Body;
            public readonly Queue<(Vector2 Point, float Height, float Pause)> Route = new Queue<(Vector2, float, float)>();
            public float Wait;
            public bool Hidden; // inside the office or gone in a car
            public float Lane;
            public float ParkingX;
        }

        public CediWorkers(CediOperationSimulation cedi = null, int seed = 13)
        {
            this.cedi = cedi;
            random = new Random(seed);
            int id = 200000;

            Create(Role.Guard, GuardNorth, IslandTop, id++);
            foreach (float lane in PickerLanesX)
                for (int i = 0; i < 2; i++)
                    Create(Role.Picker, new Vector2(lane, RandomReach()), WarehouseFloor, id++).Lane = lane;
            for (int i = 0; i < 3; i++)
                Create(Role.Commuter, Parking(out float x), 0.08f, id++).ParkingX = x;
            Create(Role.Chatting, new Vector2(-57.2f, -22.4f), 0f, id++).Body.Heading = new Vector2(0.6f, -0.8f);
            Create(Role.Chatting, new Vector2(-56.4f, -23.4f), 0f, id++).Body.Heading = new Vector2(-0.6f, 0.8f);
        }

        public event Action<Pedestrian> Added;
        public event Action<Pedestrian> Removed;

        /// <summary>The people visible now (staff inside the office or gone in a car are not).</summary>
        public IReadOnlyList<Pedestrian> People => people;

        /// <summary>Visits to the office so far (for tests).</summary>
        public int OfficeVisits { get; private set; }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            foreach (Worker worker in workers) Step(worker, deltaTime);
        }

        private Worker Create(Role role, Vector2 at, float height, int id)
        {
            var body = new Pedestrian(id, random.Next(16), WalkSpeed, 0f) { Position = at, Height = height, State = PedestrianState.Standing };
            var worker = new Worker { Role = role, Body = body, Wait = (float)random.NextDouble() * 6f };
            workers.Add(worker);
            people.Add(body);
            return worker;
        }

        private void Step(Worker w, float dt)
        {
            if (w.Role == Role.Chatting) return;
            if (w.Wait > 0f)
            {
                w.Wait -= dt;
                w.Body.Speed = 0f;
                w.Body.State = PedestrianState.Standing;
                if (w.Wait <= 0f && w.Hidden) Show(w);
                return;
            }
            if (w.Hidden) return;
            if (w.Route.Count == 0) Plan(w);
            if (w.Route.Count == 0) return;

            (Vector2 point, float height, float pause) = w.Route.Peek();
            if (w.Role == Role.Commuter && NearDoor(point, w.Body.Position) && TruckNearDoor())
            {
                w.Body.Speed = 0f; // a truck is manoeuvring by the office: wait
                w.Body.State = PedestrianState.Standing;
                return;
            }

            Pedestrian body = w.Body;
            Vector2 to = point - body.Position;
            float distance = to.Length(), step = WalkSpeed * dt;
            body.State = PedestrianState.Walking;
            if (distance > step)
            {
                body.Heading = to / distance;
                body.Position += body.Heading * step;
                body.Speed = WalkSpeed;
                body.Height += (height - body.Height) * Math.Min(1f, step / Math.Max(distance, 1e-3f));
                return;
            }
            body.Position = point;
            body.Height = height;
            body.Speed = 0f;
            w.Route.Dequeue();
            w.Wait = pause;
            if (w.Role == Role.Commuter && Vector2.DistanceSquared(point, OfficeDoor) < 0.01f && pause > 0f) Hide(w, true);
            else if (w.Role == Role.Commuter && Math.Abs(point.Y - ParkingCorner.Y) < 0.01f && Math.Abs(point.X - w.ParkingX) < 0.01f && pause > 0f)
                Hide(w, false);
        }

        /// <summary>Next errand: the guard walks the island, pickers move along their walkway, staff come and go.</summary>
        private void Plan(Worker w)
        {
            Pedestrian body = w.Body;
            switch (w.Role)
            {
                case Role.Guard:
                    Vector2 end = Vector2.DistanceSquared(body.Position, GuardNorth) < 1f ? GuardSouth : GuardNorth;
                    w.Route.Enqueue((end, IslandTop, 8f + (float)random.NextDouble() * 20f));
                    break;
                case Role.Picker:
                    w.Route.Enqueue((new Vector2(w.Lane, RandomReach()), WarehouseFloor, 3f + (float)random.NextDouble() * 5f));
                    break;
                case Role.Commuter:
                    bool atParking = body.Position.Y < ParkingCorner.Y + 1f;
                    if (atParking)
                    {
                        w.Route.Enqueue((ParkingCorner, 0.05f, 0f));
                        w.Route.Enqueue((OfficeCorner, 0.05f, 0f));
                        w.Route.Enqueue((OfficeDoor, 0.05f, 20f + (float)random.NextDouble() * 40f));
                    }
                    else
                    {
                        w.Route.Enqueue((OfficeCorner, 0.05f, 0f));
                        w.Route.Enqueue((ParkingCorner, 0.05f, 0f));
                        w.Route.Enqueue((new Vector2(w.ParkingX, ParkingCorner.Y), 0.08f, 30f + (float)random.NextDouble() * 60f));
                    }
                    break;
            }
        }

        private void Hide(Worker w, bool inOffice)
        {
            if (inOffice) OfficeVisits++;
            w.Hidden = true;
            people.Remove(w.Body);
            Removed?.Invoke(w.Body);
        }

        private void Show(Worker w)
        {
            w.Hidden = false;
            people.Add(w.Body);
            Added?.Invoke(w.Body);
        }

        private static bool NearDoor(Vector2 target, Vector2 position) =>
            Vector2.DistanceSquared(target, OfficeDoor) < 0.01f || Vector2.DistanceSquared(position, OfficeDoor) < 0.01f;

        /// <summary>True when a CEDI truck is close to the office door (they turn there towards the receiving docks).</summary>
        public bool TruckNearDoor()
        {
            if (cedi == null) return false;
            foreach (VehicleAgent vehicle in cedi.Vehicles)
                if (vehicle.Kind != VehicleKind.Forklift
                    && vehicle.Footprint.Separation(new Footprint(OfficeDoor, new Vector2(1f, 0f), 1f, 1f)) < DoorClearance)
                    return true;
            return false;
        }

        private float RandomReach() => (float)(random.NextDouble() * 2.0 - 1.0) * PickerHalfReach;

        private Vector2 Parking(out float x)
        {
            x = -40f + (float)random.NextDouble() * 30f;
            return new Vector2(x, ParkingCorner.Y);
        }
    }
}
