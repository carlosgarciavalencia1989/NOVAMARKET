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
        private float timer;

        internal ForkliftAgent(int id, string code, CediOperationSettings settings, ForkliftSpot park)
            : base(id, VehicleKind.Forklift, code, settings.ForkliftLength,
                new RouteMover(park.Point, new System.Numerics.Vector2(0f, 1f)), CediLayout.IndoorFloor)
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
            ForkliftState.ToPickup =>$"Va por una estiba al {Task.Pickup.Label}",
            ForkliftState.Lifting => "Levantando estiba",
            ForkliftState.ToDrop => $"Llevando estiba al {Task.Drop.Label}",
            ForkliftState.Dropping => "Bajando estiba",
            ForkliftState.Parking => "Regresando a la zona de espera",
            _ => "En espera"
        };

        internal override void Tick(CediOperationSimulation simulation, float deltaTime)
        {
            switch (State)
            {
                case ForkliftState.Idle:
                case ForkliftState.Parking:
                    if (State == ForkliftState.Parking)
                    {
                        Mover.Tick(deltaTime);
                        if (!Mover.IsIdle) break;
                        spot = park;
                        State = ForkliftState.Idle;
                    }
                    ForkliftTask task = simulation.AssignTask(this);
                    if (task != null)
                    {
                        Task = task;
                        Drive(task.Pickup);
                        State = ForkliftState.ToPickup;
                    }
                    else if (spot != park)
                    {
                        Drive(park);
                        State = ForkliftState.Parking;
                    }
                    break;

                case ForkliftState.ToPickup:
                case ForkliftState.ToDrop:
                    Mover.Tick(deltaTime);
                    if (!Mover.IsIdle) break;
                    timer = settings.ForkHandlingSeconds;
                    State = State == ForkliftState.ToPickup ? ForkliftState.Lifting : ForkliftState.Dropping;
                    break;

                case ForkliftState.Lifting:
                    timer -= deltaTime;
                    if (timer > 0f) break;
                    IsCarrying = true;
                    simulation.OnPalletLifted(Task);
                    Drive(Task.Drop);
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

        private void Drive(ForkliftSpot target)
        {
            Mover.SetPlan(ForkliftNetwork.Route(spot, target, settings.ForkliftSpeed));
            spot = target;
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
