using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>Stage of a truck's visit to the CEDI.</summary>
    public enum TruckState
    {
        Approaching,
        AtGate,
        Entering,
        Reversing,
        Docked,
        Leaving,
        ExitCheck,
        Exiting,
        Gone
    }

    /// <summary>
    /// A receiving truck or a dispatch "turbo". Drives from the highway to the gate, waits for
    /// the check and a free dock, crosses the yard, reverses to the dock, unloads (receiving)
    /// or loads (dispatch) pallet by pallet, and leaves through the outbound gate.
    /// </summary>
    public sealed class TruckAgent : VehicleAgent
    {
        private readonly CediOperationSettings settings;
        private readonly bool exitToEast;
        private float timer;
        private bool barrierCleared;
        private bool waitingForDock;
        private float arrivedAtGateTime;

        internal TruckAgent(int id, VehicleKind kind, string plate, CediOperationSettings settings,
            string partner, string place, string product, int pallets, bool fromEast, bool exitToEast,
            string cargo = null)
            : base(id, kind, plate, kind == VehicleKind.ReceivingTruck ? settings.TruckLength : settings.TurboLength,
                CreateMover(fromEast), 0f)
        {
            this.settings = settings;
            this.exitToEast = exitToEast;
            Partner = partner;
            Place = place;
            Product = product;
            Pallets = pallets;
            Cargo = cargo;
            Mover.SetPlan(new[] { new RouteLeg(CediLayout.Approach(fromEast, Length), settings.HighwaySpeed) });
        }

        public bool IsReceiving => Kind == VehicleKind.ReceivingTruck;

        /// <summary>Supplier (receiving) or customer area (dispatch).</summary>
        public string Partner { get; }

        /// <summary>Origin city (receiving) or destination (dispatch).</summary>
        public string Place { get; }
        public string Product { get; }
        public int Pallets { get; }

        /// <summary>Order behind the trip (SKU and boxes) when it comes from the planning level; else null.</summary>
        public string Cargo { get; }

        /// <summary>Pallets unloaded (receiving) or loaded (dispatch) so far.</summary>
        public int PalletsHandled { get; private set; }

        public int PalletsRemaining => Pallets - PalletsHandled;
        public TruckState State { get; private set; } = TruckState.Approaching;
        public Dock Dock { get; private set; }

        /// <summary>Between crossing the inbound barrier and crossing the outbound barrier.</summary>
        public bool IsInYard { get; private set; }

        /// <summary>Loading is paused because no pallet is ready at the dock.</summary>
        public bool IsWaitingForPallets { get; private set; }

        public override bool IsFinished => State == TruckState.Gone;

        public override string Status => State switch
        {
            TruckState.Approaching => "En camino a la portería",
            TruckState.AtGate when waitingForDock => "Esperando muelle libre",
            TruckState.AtGate => "Revisión de documentos en portería",
            TruckState.Entering => $"Entrando al patio hacia el muelle {Dock.Number}",
            TruckState.Reversing => $"Retrocediendo al muelle {Dock.Number}",
            TruckState.Docked when IsReceiving => "Descargando",
            TruckState.Docked when IsWaitingForPallets => "Esperando estibas",
            TruckState.Docked => "Cargando",
            TruckState.Leaving => "Saliendo del patio",
            TruckState.ExitCheck => "Control de salida",
            TruckState.Exiting => "Rumbo a la autopista",
            _ => "Fuera del CEDI"
        };

        private static RouteMover CreateMover(bool fromEast)
        {
            var start = new Vector2(fromEast ? CediLayout.SpawnDistanceX : -CediLayout.SpawnDistanceX,
                fromEast ? CediLayout.WestboundLaneZ : CediLayout.EastboundLaneZ);
            return new RouteMover(start, new Vector2(fromEast ? -1f : 1f, 0f));
        }

        internal override void Tick(CediOperationSimulation simulation, float deltaTime)
        {
            switch (State)
            {
                case TruckState.Approaching:
                    Mover.Tick(deltaTime, simulation.InboundGate.LimitFor(this, Mover.CurrentLeg?.Path.Length ?? 0f));
                    if (Mover.IsIdle && simulation.InboundGate.IsFirst(this))
                    {
                        State = TruckState.AtGate;
                        timer = settings.GateCheckSeconds;
                        arrivedAtGateTime = simulation.Time;
                        simulation.OnTruckAtGate(this);
                    }
                    break;

                case TruckState.AtGate:
                    timer -= deltaTime;
                    if (timer > 0f) break;
                    Dock ??= simulation.Docks.Reserve(IsReceiving ? DockKind.Receiving : DockKind.Shipping, this);
                    if (Dock == null)
                    {
                        if (!waitingForDock) simulation.OnNoDockFree(this);
                        waitingForDock = true;
                        break;
                    }
                    waitingForDock = false;
                    simulation.InboundGate.Leave(this);
                    simulation.InboundGate.IsBarrierOpen = true;
                    barrierCleared = false;
                    IsInYard = true;
                    Mover.SetPlan(new[]
                    {
                        new RouteLeg(CediLayout.ToStaging(Dock, Length), settings.YardSpeed),
                        new RouteLeg(CediLayout.ReverseIntoDock(Dock, Length), settings.ReverseSpeed, reverse: true)
                    });
                    State = TruckState.Entering;
                    simulation.OnEnteredYard(this, simulation.Time - arrivedAtGateTime);
                    break;

                case TruckState.Entering:
                case TruckState.Reversing:
                    Mover.Tick(deltaTime);
                    if (!barrierCleared && Mover.CompletedLegs == 0 && Mover.DistanceOnLeg > Length + 1f)
                    {
                        barrierCleared = true;
                        simulation.InboundGate.IsBarrierOpen = false;
                    }
                    if (Mover.IsReversing) State = TruckState.Reversing;
                    if (Mover.IsIdle)
                    {
                        State = TruckState.Docked;
                        timer = 0f;
                    }
                    break;

                case TruckState.Docked:
                    HandlePallets(simulation, deltaTime);
                    break;

                case TruckState.Leaving:
                    Mover.Tick(deltaTime, simulation.OutboundGate.LimitFor(this, Mover.CurrentLeg?.Path.Length ?? 0f));
                    if (Mover.IsIdle && simulation.OutboundGate.IsFirst(this))
                    {
                        State = TruckState.ExitCheck;
                        timer = settings.ExitCheckSeconds;
                    }
                    break;

                case TruckState.ExitCheck:
                    timer -= deltaTime;
                    if (timer > 0f) break;
                    simulation.OutboundGate.Leave(this);
                    simulation.OutboundGate.IsBarrierOpen = true;
                    barrierCleared = false;
                    Mover.SetPlan(new[] { new RouteLeg(CediLayout.Exit(exitToEast, Length), settings.HighwaySpeed, stopAtEnd: false) });
                    State = TruckState.Exiting;
                    break;

                case TruckState.Exiting:
                    Mover.Tick(deltaTime);
                    if (!barrierCleared && Mover.DistanceOnLeg > Length + 1f)
                    {
                        barrierCleared = true;
                        IsInYard = false;
                        simulation.OutboundGate.IsBarrierOpen = false;
                        simulation.OnLeftYard(this);
                    }
                    if (Mover.IsIdle) State = TruckState.Gone;
                    break;
            }
        }

        private void HandlePallets(CediOperationSimulation simulation, float deltaTime)
        {
            float perPallet = IsReceiving ? settings.UnloadSecondsPerPallet : settings.LoadSecondsPerPallet;
            timer += deltaTime;
            IsWaitingForPallets = false;

            while (PalletsRemaining > 0 && timer >= perPallet)
            {
                bool moved = IsReceiving ? simulation.TryStagePallet(Dock) : simulation.TryTakeStagedPallet(Dock);
                if (!moved)
                {
                    IsWaitingForPallets = !IsReceiving;
                    timer = perPallet; // ready to go as soon as there is room / a pallet
                    return;
                }
                timer -= perPallet;
                PalletsHandled++;
            }

            if (PalletsRemaining > 0) return;

            if (IsReceiving) simulation.OnUnloaded(this);
            else simulation.OnLoaded(this);
            simulation.Docks.Release(Dock);
            simulation.OutboundGate.Join(this);
            Mover.SetPlan(new[] { new RouteLeg(CediLayout.FromDock(Dock, Length), settings.YardSpeed) });
            State = TruckState.Leaving;
        }

        public override SelectionDetails Describe()
        {
            var stats = new List<FacilityStat>
            {
                IsReceiving
                    ? new FacilityStat("Proveedor", Partner)
                    : new FacilityStat("Cliente", Partner),
                IsReceiving
                    ? new FacilityStat("Origen", Place)
                    : new FacilityStat("Destino", Place),
                new FacilityStat("Carga", $"{Pallets} estibas de {Product}"),
                new FacilityStat("Estado", Status),
                new FacilityStat("Muelle", Dock != null && State <= TruckState.Docked ? Dock.Number.ToString() : "—")
            };
            if (Cargo != null) stats.Insert(3, new FacilityStat(IsReceiving ? "Pedido" : "Venta", Cargo));

            string action = IsReceiving ? "Descargue" : "Cargue";
            string subtitle = IsReceiving ? "Camión de recibo" : "Turbo de despacho";
            return new SelectionDetails(Plate, subtitle, SelectionIcon.Truck, stats,
                progress: (double)PalletsHandled / Pallets,
                progressLabel: $"{action}: {PalletsHandled}/{Pallets} estibas");
        }
    }
}
