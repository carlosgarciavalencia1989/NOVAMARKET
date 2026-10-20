using System;
using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>
    /// The highway is shared with the city traffic (<see cref="SharedRoad"/>): trucks keep their distance
    /// from its vehicles, stop at its red lights, appear only where the road is free and leave the gate
    /// only when there is a gap. The trucks on the highway are given back to the traffic as
    /// <see cref="SharedRoadVehicle"/>s, so it brakes for them and overtakes the ones waiting at the gate.
    /// </summary>
    public sealed partial class CediOperationSimulation : ISharedRoadVehicles
    {
        /// <summary>Highway band (z) where a truck counts as being on the shared road.</summary>
        private const float HighwayBand = 12f;

        /// <summary>Trucks queued for the gate stand within this distance (x) of it.</summary>
        private const float GateQueueReach = 75f;

        private readonly List<Footprint> sharedFootprints = new List<Footprint>();

        /// <summary>City traffic on the same highway (null when the CEDI runs alone, as in the tests).</summary>
        public ISharedRoad SharedRoad { get; set; }

        public void CollectSharedRoadVehicles(List<SharedRoadVehicle> into)
        {
            foreach (VehicleAgent vehicle in vehicles)
            {
                if (vehicle.FloorHeight > 0.01f || !(vehicle is TruckAgent truck)) continue;
                if (Math.Abs(vehicle.Position.Y - (CediLayout.EastboundLaneZ + CediLayout.WestboundLaneZ) / 2f) > HighwayBand) continue;
                // Queued at the gate (not at a red light further away): the traffic may overtake it.
                bool parked = IsQueuedAtGate(truck) && truck.Speed < 0.1f && Math.Abs(vehicle.Position.X) < GateQueueReach;
                into.Add(new SharedRoadVehicle(vehicle.Footprint, vehicle.Speed, parked));
            }
        }

        private bool IsQueuedAtGate(TruckAgent truck)
        {
            foreach (TruckAgent queued in InboundGate.Queue)
                if (queued == truck) return true;
            return false;
        }

        /// <summary>A new truck needs the highway free around it and behind it (traffic comes fast from behind).</summary>
        private bool IsSharedRoadClearFor(VehicleAgent truck)
        {
            if (SharedRoad == null) return true;
            const float behind = 45f;
            var area = new Footprint(truck.Position - truck.Heading * (behind / 2f), truck.Heading, truck.Length + behind,
                truck.Width + 0.6f);
            return SharedRoad.IsAreaClear(area, Settings.QueueGap);
        }

        /// <summary>The outbound truck waits at the barrier until the westbound lane has a gap.</summary>
        internal bool IsExitMergeClear() =>
            SharedRoad == null || SharedRoad.IsMergeClear(CediLayout.ExitMergePoint, new Vector2(-1f, 0f), 70f, 25f);

        /// <summary>Furthest distance on the current leg before the stop line of a red light (trucks on the highway).</summary>
        private float RedLightLimit(VehicleAgent me, RouteMover mover, float lookahead)
        {
            if (SharedRoad == null || me.Kind == VehicleKind.Forklift || me.FloorHeight > 0.01f) return float.MaxValue;
            Vector2 front = me.Position + me.Heading * (me.Length / 2f);
            float distance = SharedRoad.DistanceToRedLight(front, me.Heading, mover.Speed, lookahead);
            return distance == float.MaxValue ? float.MaxValue : mover.DistanceOnLeg + distance;
        }

        /// <summary>True when the body at <paramref name="ahead"/> would come within the gap of a traffic vehicle.</summary>
        private bool HitsSharedTraffic(VehicleAgent me, Footprint ahead, Footprint now, float gap)
        {
            foreach (Footprint theirs in sharedFootprints)
                if (GetsCloser(ahead, now, theirs, gap)) return true;
            return false;
        }

        private void CollectSharedFootprints(VehicleAgent me, float lookahead)
        {
            sharedFootprints.Clear();
            if (SharedRoad == null || me.Kind == VehicleKind.Forklift || me.FloorHeight > 0.01f) return;
            SharedRoad.CollectFootprints(me.Position, lookahead + me.Length + 10f, sharedFootprints);
        }
    }
}
