using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation
{
    public enum ForkliftTaskKind
    {
        /// <summary>Receiving staging lane → rack (adds a stored position).</summary>
        PutAway,

        /// <summary>Rack → shipping staging lane (frees a position).</summary>
        Pick
    }

    /// <summary>One pallet move assigned to a forklift.</summary>
    public sealed class ForkliftTask
    {
        public ForkliftTask(ForkliftTaskKind kind, ForkliftSpot pickup, ForkliftSpot drop, Dock dock)
        {
            Kind = kind;
            Pickup = pickup;
            Drop = drop;
            Dock = dock;
        }

        public ForkliftTaskKind Kind { get; }
        public ForkliftSpot Pickup { get; }
        public ForkliftSpot Drop { get; }

        /// <summary>The dock whose staging lane is the pickup (put-away) or the drop (pick).</summary>
        public Dock Dock { get; }
    }

    public enum ForkliftState
    {
        Idle,
        ToPickup,
        Lifting,
        ToDrop,
        Dropping,
        Parking
    }

    /// <summary>
    /// Forklift inside the warehouse. Takes tasks from the simulation, drives along the aisle
    /// network, lifts a pallet, carries it and sets it down; parks when there is nothing to do.
    /// </summary>
    public sealed class ForkliftAgent : VehicleAgent
    {
        private readonly CediOperationSettings settings;
        private readonly ForkliftSpot park;
        private ForkliftSpot spot;
        private ForkliftSpot heldSpot;
        private ForkliftSpot leavingSpot;
        private float timer;

        internal ForkliftAgent(int id, string code, CediOperationSettings settings, ForkliftSpot park)
            : base(id, VehicleKind.Forklift, code, settings.ForkliftLength,
                new RouteMover(park.Point, ParkedHeading(park)), CediLayout.IndoorFloor)
        {
            this.settings = settings;
            this.park = park;
            spot = park;
        }

        public ForkliftState State { get; private set; } = ForkliftState.Idle;
        public ForkliftTask Task { get; private set; }
        public bool IsCarrying { get; private set; }
        public bool IsBusy => Task != null;
        public override bool IsFinished => false;

        public override string Status => State switch
        {
            _ when IsWaitingInTraffic => "Cediendo el paso a otro montacargas",
            ForkliftState.ToPickup =>$"Va por una estiba al {Task.Pickup.Label}",
            ForkliftState.Lifting => "Levantando estiba",
            ForkliftState.ToDrop => $"Llevando estiba al {Task.Drop.Label}",
            ForkliftState.Dropping => "Bajando estiba",
            ForkliftState.Parking => "Regresando a la zona de espera",
            _ => "En espera"
        };

        internal override void Tick(CediOperationSimulation simulation, float deltaTime)
        {
            // The spot just left is free once the forklift has driven away from its spur.
            if (leavingSpot != null && System.Numerics.Vector2.Distance(Position, leavingSpot.Access) > SpurClearance)
            {
                simulation.ReleaseSpot(leavingSpot, this);
                leavingSpot = null;
            }

            switch (State)
            {
                case ForkliftState.Idle:
                case ForkliftState.Parking:
                    if (State == ForkliftState.Parking)
                    {
                        Mover.Tick(deltaTime, simulation.TrafficLimit(this, Mover, deltaTime));
                        if (!Mover.IsIdle) break;
                        spot = park;
                        State = ForkliftState.Idle;
                    }
                    ForkliftTask task = simulation.AssignTask(this);
                    if (task != null)
                    {
                        Task = task;
                        Drive(simulation, task.Pickup);
                        State = ForkliftState.ToPickup;
                    }
                    else if (spot != park)
                    {
                        Drive(simulation, park);
                        State = ForkliftState.Parking;
                    }
                    break;

                case ForkliftState.ToPickup:
                case ForkliftState.ToDrop:
                    Mover.Tick(deltaTime, simulation.TrafficLimit(this, Mover, deltaTime, SpotLimit(simulation)));
                    if (!Mover.IsIdle) break;
                    timer = settings.ForkHandlingSeconds;
                    State = State == ForkliftState.ToPickup ? ForkliftState.Lifting : ForkliftState.Dropping;
                    break;

                case ForkliftState.Lifting:
                    timer -= deltaTime;
                    if (timer > 0f) break;
                    IsCarrying = true;
                    simulation.OnPalletLifted(Task);
                    Drive(simulation, Task.Drop);
                    State = ForkliftState.ToDrop;
                    break;

                case ForkliftState.Dropping:
                    timer -= deltaTime;
                    if (timer > 0f) break;
                    IsCarrying = false;
                    simulation.OnPalletDropped(Task);
                    Task = null;
                    State = ForkliftState.Idle;
                    break;
            }
        }

        private void Drive(CediOperationSimulation simulation, ForkliftSpot target)
        {
            if (heldSpot != null && heldSpot != target)
            {
                leavingSpot = heldSpot;
                heldSpot = null;
            }
            Mover.SetPlan(ForkliftNetwork.Route(spot, target, settings.ForkliftSpeed));
            spot = target;
        }

        /// <summary>The wait point moved back before the first crossing on the way (a forklift never waits inside one).</summary>
        private float BeforeFirstCrossing(CediOperationSimulation simulation, float waitAt)
        {
            for (float d = Math.Max(Mover.DistanceOnLeg, waitAt - SpotAskDistance - 2f); d <= waitAt; d += 0.25f)
            {
                (System.Numerics.Vector2 centre, System.Numerics.Vector2 heading) = Mover.PoseAhead(d);
                Footprint body = FootprintAt(centre, heading);
                foreach (TrafficZone zone in simulation.Zones)
                    if (!zone.ForTrucks && zone.Touches(body)) return Math.Max(Mover.DistanceOnLeg, d - 0.5f);
            }
            return waitAt;
        }

        /// <summary>A parked forklift faces away from the aisle, as it drove in.</summary>
        private static System.Numerics.Vector2 ParkedHeading(ForkliftSpot park) =>
            System.Numerics.Vector2.DistanceSquared(park.Point, park.Access) < 1e-4f
                ? new System.Numerics.Vector2(0f, 1f)
                : System.Numerics.Vector2.Normalize(park.Point - park.Access);

        /// <summary>True while driving to <paramref name="target"/> without having reserved it yet.</summary>
        internal bool IsHeadingTo(ForkliftSpot target) =>
            (State == ForkliftState.ToPickup || State == ForkliftState.ToDrop) && heldSpot != spot
            && System.Numerics.Vector2.DistanceSquared(spot.Point, target.Point) < 1e-4f;

        /// <summary>Meters left to the target spot (backing out of the previous one counts as far).</summary>
        internal float DistanceToTarget =>
            Mover.CurrentLeg == null ? 0f : Mover.CurrentLeg.Reverse ? float.MaxValue : Mover.CurrentLeg.Path.Length - Mover.DistanceOnLeg;

        /// <summary>Distance from a spot's access at which a forklift has left its spur.</summary>
        private const float SpurClearance = 3.5f;

        /// <summary>Where a forklift waits for a busy spot: far enough for the other one to back out and turn.</summary>
        private const float SpotWaitDistance = 6.5f;

        /// <summary>How far before its wait point a forklift asks for the spot.</summary>
        private const float SpotAskDistance = 6f;

        /// <summary>
        /// While another forklift still works at (or backs out of) the target spot, stop 6.5 m before its
        /// access so it can back out onto the aisle and leave.
        /// </summary>
        private float SpotLimit(CediOperationSimulation simulation)
        {
            if (heldSpot == spot || Mover.CurrentLeg == null || Mover.CurrentLeg.Reverse) return float.MaxValue;
            float spur = System.Numerics.Vector2.Distance(spot.Point, spot.Access);
            float waitAt = Math.Max(0f, Mover.CurrentLeg.Path.Length - spur - SpotWaitDistance);
            // Ask for the spot when getting close to it (a queue keeps the order of arrival)...
            if (Mover.DistanceOnLeg < waitAt - SpotAskDistance) return float.MaxValue;
            if (simulation.TryHoldSpot(spot, this))
            {
                heldSpot = spot;
                return float.MaxValue;
            }
            // ...and wait for it before the last crossing on the way, so nobody waits inside one.
            return BeforeFirstCrossing(simulation, waitAt);
        }

        public override SelectionDetails Describe()
        {
            string task = Task == null ? "Sin tarea"
                : Task.Kind == ForkliftTaskKind.PutAway ? $"Ubicar estiba del {Task.Pickup.Label} en {Task.Drop.Label}"
                : $"Alistar estiba del {Task.Pickup.Label} para el {Task.Drop.Label}";
            var stats = new List<FacilityStat>
            {
                new FacilityStat("Tarea", task),
                new FacilityStat("Carga", IsCarrying ? "1 estiba" : "Vacío"),
                new FacilityStat("Estado", Status)
            };
            return new SelectionDetails($"Montacargas {Plate}", "Bodega principal · operador de turno", SelectionIcon.Forklift, stats);
        }
    }
}
