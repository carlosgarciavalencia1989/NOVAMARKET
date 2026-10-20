using System.Collections.Generic;
using System.Numerics;

namespace NovaMarket.Simulation
{
    /// <summary>A vehicle of another simulation that drives on the same road (e.g. a CEDI truck on the highway).</summary>
    public readonly struct SharedRoadVehicle
    {
        public SharedRoadVehicle(Footprint footprint, float speed, bool isParked)
        {
            Footprint = footprint;
            Speed = speed;
            IsParked = isParked;
        }

        public Footprint Footprint { get; }
        public float Speed { get; }

        /// <summary>Stopped for its own reasons (waiting at the gate): the others may overtake it.</summary>
        public bool IsParked { get; }
    }

    /// <summary>Gives the vehicles of one simulation to another one that shares the road with it.</summary>
    public interface ISharedRoadVehicles
    {
        void CollectSharedRoadVehicles(List<SharedRoadVehicle> into);
    }

    /// <summary>
    /// What the CEDI trucks need from the city traffic: the bodies to keep away from, the red lights
    /// on their way and whether they can join the highway. Implemented by the ambient traffic.
    /// </summary>
    public interface ISharedRoad
    {
        /// <summary>Footprints of the traffic vehicles within <paramref name="radius"/> of a point.</summary>
        void CollectFootprints(Vector2 centre, float radius, List<Footprint> into);

        /// <summary>
        /// Meters from <paramref name="front"/> to the stop line of a red (or amber, if it can still stop)
        /// light on the lane it drives along, or <see cref="float.MaxValue"/>.
        /// </summary>
        float DistanceToRedLight(Vector2 front, Vector2 heading, float speed, float lookahead);

        /// <summary>True when no traffic drives in the stretch from <paramref name="upstream"/> meters behind a point to <paramref name="downstream"/> ahead.</summary>
        bool IsMergeClear(Vector2 point, Vector2 heading, float upstream, float downstream);

        /// <summary>True when no traffic vehicle comes within <paramref name="gap"/> of the area.</summary>
        bool IsAreaClear(Footprint area, float gap);
    }
}
